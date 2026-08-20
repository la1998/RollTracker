using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using Dalamud.Game.Chat;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace RollTracker.Services;

internal sealed partial class RollTrackerService : IDisposable
{
    private const string DefaultResultCommandTemplate = "/y \"{highest}\"({highestRoll})>>>\"{lowest}\"({lowestRoll})";
    private const string LegacySecondPairMacroText = "/y ♦ Time for Truth or Dare 2 ♦  Highest asks lowest, second highest asks second lowest. Type /random in chat! 60 seconds... And GO!\n/wait 50\n/y 10 seconds remain...\n/wait 10\n/y End";
    private const string DefaultSecondPairMacroText = "/y ♦ Time for Truth or Dare 2 ♦  Highest asks lowest, second highest asks second lowest,  \"Truth or Dare?\" Type /random in chat! 60 seconds... And GO!\n/wait 50\n/y 10 seconds remain...\n/wait 10\n/y End";
    private const string LegacySecondPairResultCommandTemplate = "/y \"{highest}\"({highestRoll})>>>\"{lowest}\"({lowestRoll}) 2nd: \"{secondHighest}\"({secondHighestRoll})>>>\"{secondLowest}\"({secondLowestRoll})";
    private const string DefaultSecondPairResultCommandTemplate = "/y \"{highest}\"({highestRoll})>>>\"{lowest}\"({lowestRoll})\n/y 2nd: \"{secondHighest}\"({secondHighestRoll})>>>\"{secondLowest}\"({secondLowestRoll})";

    private readonly IChatGui chatGui;
    private readonly ICommandManager commandManager;
    private readonly IFramework framework;
    private readonly IClientState clientState;
    private readonly IPluginLog log;
    private readonly Configuration configuration;
    private readonly System.Action saveConfiguration;
    private readonly TextCommandService textCommandService = new();
    private readonly List<RollEntry> rolls = [];
    private readonly Queue<MacroStep> pendingMacroSteps = [];
    private readonly Queue<MacroStep> pendingWifiMacroSteps = [];
    private readonly Queue<DelayedCommand> pendingTodPromptCommands = [];
    private readonly HashSet<uint> housingInteriorTerritoryIds;
    private readonly List<string> worldNames;

    private DateTimeOffset? roundEndsAt;
    private DateTimeOffset nextMacroStepAt;
    private DateTimeOffset nextWifiMacroStepAt;
    private RoundKind currentRoundKind = RoundKind.Normal;
    private int nextManualRollNumber = 1;
    private bool wasInHousingInterior;

    public RollTrackerService(
        IChatGui chatGui,
        ICommandManager commandManager,
        IFramework framework,
        IClientState clientState,
        IDataManager dataManager,
        IPluginLog log,
        Configuration configuration,
        System.Action saveConfiguration)
    {
        this.chatGui = chatGui;
        this.commandManager = commandManager;
        this.framework = framework;
        this.clientState = clientState;
        this.log = log;
        this.configuration = configuration;
        this.saveConfiguration = saveConfiguration;
        NormalizeLegacyDefaults();
        housingInteriorTerritoryIds = dataManager.GetExcelSheet<HousingIndoorTerritory>()?.Select(row => row.RowId).ToHashSet() ?? [];
        worldNames = BuildWorldNames(dataManager);
        wasInHousingInterior = IsHousingInterior(clientState.TerritoryType);

        this.chatGui.ChatMessage += OnHandleableChatMessage;
        this.chatGui.LogMessage += OnLogMessage;
        this.clientState.TerritoryChanged += OnTerritoryChanged;
        this.framework.Update += OnFrameworkUpdate;
    }

    public ReadOnlyCollection<RollEntry> Rolls => rolls.AsReadOnly();

    public RollEntry? HighestRoll => rolls.Count == 0 ? null : rolls.MaxBy(roll => roll.Value);

    public RollEntry? LowestRoll => rolls.Count == 0 ? null : rolls.MinBy(roll => roll.Value);

    public bool IsRoundRunning => roundEndsAt is not null;

    public bool IsWifiMacroRunning => pendingWifiMacroSteps.Count > 0;

    public TimeSpan RemainingRoundTime => roundEndsAt is null
        ? TimeSpan.Zero
        : roundEndsAt.Value - DateTimeOffset.Now;

    private void NormalizeLegacyDefaults()
    {
        var changed = false;

        if (string.Equals(configuration.TodSecondPairMacroText, LegacySecondPairMacroText, StringComparison.Ordinal))
        {
            configuration.TodSecondPairMacroText = DefaultSecondPairMacroText;
            changed = true;
        }

        if (string.Equals(configuration.TodSecondPairResultCommandTemplate, LegacySecondPairResultCommandTemplate, StringComparison.Ordinal))
        {
            configuration.TodSecondPairResultCommandTemplate = DefaultSecondPairResultCommandTemplate;
            changed = true;
        }

        if (changed)
        {
            saveConfiguration();
        }
    }

    public void Dispose()
    {
        chatGui.ChatMessage -= OnHandleableChatMessage;
        chatGui.LogMessage -= OnLogMessage;
        clientState.TerritoryChanged -= OnTerritoryChanged;
        framework.Update -= OnFrameworkUpdate;
    }

    public void Reset()
    {
        rolls.Clear();
        nextManualRollNumber = 1;
    }

    public void FinishRoundAndReset()
    {
        roundEndsAt = null;
        pendingMacroSteps.Clear();

        if (rolls.Count == 0)
        {
            chatGui.Print("No rolls recorded for this round.", "RollTracker");
            return;
        }

        var highest = HighestRoll;
        var lowest = LowestRoll;

        if (highest is null || lowest is null)
        {
            return;
        }

        var resultCommands = currentRoundKind == RoundKind.SecondPair
            ? BuildSecondPairResultCommands(highest, lowest)
            : BuildResultCommands(highest, lowest);
        var anyCommandFailed = false;
        foreach (var resultCommand in resultCommands)
        {
            if (!TryExecuteTextCommand(resultCommand))
            {
                anyCommandFailed = true;
            }
        }

        if (anyCommandFailed)
        {
            chatGui.PrintError("Could not send one or more result chat messages.", "RollTracker");
        }

        Reset();
        currentRoundKind = RoundKind.Normal;
    }

    private List<string> BuildResultCommands(RollEntry highest, RollEntry lowest)
    {
        var resultCommand = BuildResultCommand(highest, lowest);
        var resultCommands = SplitCommandLines(resultCommand);
        if (resultCommands.Count == 0)
        {
            resultCommands.Add(BuildResultCommandFromTemplate(DefaultResultCommandTemplate, highest, lowest));
        }

        AppendSpecialRuleCommands(resultCommands, 0, BuildTodSpecialRuleTexts(highest, lowest));
        return resultCommands;
    }

    private string BuildResultCommand(RollEntry highest, RollEntry lowest)
    {
        var template = string.IsNullOrWhiteSpace(configuration.ResultCommandTemplate)
            ? DefaultResultCommandTemplate
            : configuration.ResultCommandTemplate;

        return BuildResultCommandFromTemplate(template, highest, lowest);
    }

    private List<string> BuildSecondPairResultCommands(RollEntry highest, RollEntry lowest)
    {
        if (!TryGetTodSecondPair(highest, lowest, out var secondHighest, out var secondLowest))
        {
            return BuildResultCommands(highest, lowest);
        }

        var template = string.IsNullOrWhiteSpace(configuration.TodSecondPairResultCommandTemplate)
            ? DefaultSecondPairResultCommandTemplate
            : configuration.TodSecondPairResultCommandTemplate;
        if (template.Equals(LegacySecondPairResultCommandTemplate, StringComparison.Ordinal))
        {
            template = DefaultSecondPairResultCommandTemplate;
        }

        var result = template
            .Replace("{highest}", highest.PlayerName, StringComparison.OrdinalIgnoreCase)
            .Replace("{lowest}", lowest.PlayerName, StringComparison.OrdinalIgnoreCase)
            .Replace("{highestRoll}", highest.Value.ToString(), StringComparison.OrdinalIgnoreCase)
            .Replace("{lowestRoll}", lowest.Value.ToString(), StringComparison.OrdinalIgnoreCase)
            .Replace("{secondHighest}", secondHighest.PlayerName, StringComparison.OrdinalIgnoreCase)
            .Replace("{secondLowest}", secondLowest.PlayerName, StringComparison.OrdinalIgnoreCase)
            .Replace("{secondHighestRoll}", secondHighest.Value.ToString(), StringComparison.OrdinalIgnoreCase)
            .Replace("{secondLowestRoll}", secondLowest.Value.ToString(), StringComparison.OrdinalIgnoreCase);

        var resultCommands = SplitCommandLines(result);
        if (resultCommands.Count == 0)
        {
            return BuildResultCommands(highest, lowest);
        }

        AppendSpecialRuleCommands(resultCommands, 0, BuildTodSpecialRuleTexts(highest, lowest));

        if (resultCommands.Count > 1)
        {
            AppendSpecialRuleCommands(resultCommands, 1, BuildTodSpecialRuleTexts(secondHighest, secondLowest));
        }
        else
        {
            AppendSpecialRuleCommands(resultCommands, 0, BuildTodSpecialRuleTexts(secondHighest, secondLowest));
        }

        return resultCommands;
    }

    private static string BuildResultCommandFromTemplate(string template, RollEntry highest, RollEntry lowest)
    {
        return template
            .Replace("{highest}", highest.PlayerName, StringComparison.OrdinalIgnoreCase)
            .Replace("{lowest}", lowest.PlayerName, StringComparison.OrdinalIgnoreCase)
            .Replace("{highestRoll}", highest.Value.ToString(), StringComparison.OrdinalIgnoreCase)
            .Replace("{lowestRoll}", lowest.Value.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static List<string> SplitCommandLines(string commandText)
    {
        return commandText
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .ToList();
    }

    private static void AppendSpecialRuleCommands(List<string> commands, int sourceCommandIndex, IEnumerable<string> specialRuleTexts)
    {
        if (sourceCommandIndex < 0 || sourceCommandIndex >= commands.Count)
        {
            return;
        }

        var commandPrefix = GetCommandPrefix(commands[sourceCommandIndex]);
        foreach (var specialRuleText in specialRuleTexts.Where(text => !string.IsNullOrWhiteSpace(text)))
        {
            commands.Add($"{commandPrefix} {specialRuleText}");
        }
    }

    private static string GetCommandPrefix(string command)
    {
        var trimmedCommand = command.Trim();
        var firstSpaceIndex = trimmedCommand.IndexOf(' ', StringComparison.Ordinal);

        return firstSpaceIndex <= 0
            ? "/y"
            : trimmedCommand[..firstSpaceIndex];
    }

    private List<string> BuildTodSpecialRuleTexts(RollEntry highest, RollEntry lowest)
    {
        if (!configuration.TodSpecialRulesEnabled)
        {
            return [];
        }

        var specialRuleTexts = new List<string>();
        var pairRolls = ReferenceEquals(highest, lowest)
            ? [lowest.Value]
            : new[] { lowest.Value, highest.Value };
        var lowestRules = BuildSpecialRuleMatchesForRoll(lowest, "lowest", pairRolls);
        specialRuleTexts.AddRange(lowestRules.Select(rule => rule.Text));

        if (ReferenceEquals(highest, lowest))
        {
            return specialRuleTexts;
        }

        var highestRules = BuildSpecialRuleMatchesForRoll(highest, "highest", pairRolls);
        var stopAfterLowest = lowestRules.Any(rule => rule.StopPairAfterMatch);
        if (stopAfterLowest)
        {
            specialRuleTexts.AddRange(highestRules
                .Where(rule => rule.AlwaysShown)
                .Select(rule => rule.Text));
            return specialRuleTexts;
        }

        specialRuleTexts.AddRange(highestRules.Select(rule => rule.Text));
        return specialRuleTexts;
    }

    private List<SpecialRuleMatch> BuildSpecialRuleMatchesForRoll(RollEntry roll, string role, IReadOnlyCollection<int> pairRolls)
    {
        return configuration.TodSpecialRules
            .Where(rule => rule.Roll == roll.Value)
            .Where(rule => !ShouldSkipSpecialRule(rule, pairRolls))
            .Select(rule => BuildSpecialRuleMatch(rule, roll, role))
            .Where(rule => !string.IsNullOrWhiteSpace(rule.Text))
            .ToList();
    }

    private static SpecialRuleMatch BuildSpecialRuleMatch(TodSpecialRule rule, RollEntry roll, string role)
    {
        var text = rule.Text.Trim()
            .Replace("{player}", roll.PlayerName, StringComparison.OrdinalIgnoreCase)
            .Replace("{roll}", roll.Value.ToString(), StringComparison.OrdinalIgnoreCase)
            .Replace("{role}", role, StringComparison.OrdinalIgnoreCase);

        return new SpecialRuleMatch(text, rule.StopPairAfterMatch, rule.AlwaysShown);
    }

    private static bool ShouldSkipSpecialRule(TodSpecialRule rule, IReadOnlyCollection<int> pairRolls)
    {
        var blockedRolls = ParseSpecialRuleRollList(rule.DoNotTriggerWith);

        return blockedRolls.Count > 0 && pairRolls.Any(blockedRolls.Contains);
    }

    private static HashSet<int> ParseSpecialRuleRollList(string rollList)
    {
        return rollList
            .Split([',', ';', ' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(text => int.TryParse(text, out var value) ? value : (int?)null)
            .Where(value => value is >= 0 and <= 9999)
            .Select(value => value!.Value)
            .ToHashSet();
    }

    private bool TryGetTodSecondPair(
        RollEntry highest,
        RollEntry lowest,
        out RollEntry secondHighest,
        out RollEntry secondLowest)
    {
        if (!configuration.TodSecondPairEnabled || rolls.Count < 4)
        {
            secondHighest = null!;
            secondLowest = null!;
            return false;
        }

        var secondHighestCandidate = rolls
            .OrderByDescending(roll => roll.Value)
            .ThenBy(roll => roll.Time)
            .FirstOrDefault(roll => !ReferenceEquals(roll, highest));
        var secondLowestCandidate = rolls
            .OrderBy(roll => roll.Value)
            .ThenBy(roll => roll.Time)
            .FirstOrDefault(roll => !ReferenceEquals(roll, lowest));

        if (secondHighestCandidate is null ||
            secondLowestCandidate is null ||
            ReferenceEquals(secondHighestCandidate, secondLowestCandidate))
        {
            secondHighest = null!;
            secondLowest = null!;
            return false;
        }

        secondHighest = secondHighestCandidate;
        secondLowest = secondLowestCandidate;
        return true;
    }

    private static string BuildTodSecondPairText(RollEntry secondHighest, RollEntry secondLowest)
    {
        return $"2nd: \"{secondHighest.PlayerName}\"({secondHighest.Value})>>>\"{secondLowest.PlayerName}\"({secondLowest.Value})";
    }

    public void AddTestRolls()
    {
        AddRoll("Example One", Random.Shared.Next(1, 1000));
        AddRoll("Example Two", Random.Shared.Next(1, 1000));
        AddRoll("Example Three", Random.Shared.Next(1, 1000));
        AddRoll("Example Four", Random.Shared.Next(1, 1000));
    }

    public bool TryAddManualRoll(int value, out string message)
    {
        if (!IsRoundRunning)
        {
            message = "Start a Truth or Dare round before adding manual test rolls.";
            return false;
        }

        if (value is < 0 or > 9999)
        {
            message = "Manual test rolls must be between 0 and 9999.";
            return false;
        }

        var playerName = $"Manual Test {nextManualRollNumber++}";
        AddRoll(playerName, value);
        message = $"Added {playerName} with roll {value}.";
        return true;
    }

    public void SetEnabled(bool enabled)
    {
        configuration.Enabled = enabled;
        if (enabled)
        {
            configuration.TruthTriggerEnabled = true;
            configuration.DareTriggerEnabled = true;
        }

        saveConfiguration();

        if (!enabled)
        {
            roundEndsAt = null;
            pendingMacroSteps.Clear();
            pendingTodPromptCommands.Clear();
        }

        chatGui.Print($"RollTracker {(enabled ? "enabled" : "disabled")}.", "RollTracker");
    }

    public void SetAllModulesEnabled(bool enabled)
    {
        configuration.Enabled = enabled;
        configuration.TruthTriggerEnabled = enabled;
        configuration.DareTriggerEnabled = enabled;
        configuration.HelpTriggerEnabled = enabled;
        configuration.TodSpecialRulesEnabled = enabled;
        configuration.TodSecondPairEnabled = enabled;
        configuration.WifiEnabled = enabled;
        saveConfiguration();

        if (!enabled)
        {
            roundEndsAt = null;
            pendingMacroSteps.Clear();
            pendingWifiMacroSteps.Clear();
            pendingTodPromptCommands.Clear();
        }

        chatGui.Print($"RollTracker modules {(enabled ? "enabled" : "disabled")}.", "RollTracker");
    }

    public void SetTruthTriggerEnabled(bool enabled)
    {
        configuration.TruthTriggerEnabled = enabled;
        saveConfiguration();

        if (!enabled)
        {
            ClearDelayedTodPrompts("Truth");
        }

        chatGui.Print($"RollTracker !truth {(enabled ? "enabled" : "disabled")}.", "RollTracker");
    }

    public void SetDareTriggerEnabled(bool enabled)
    {
        configuration.DareTriggerEnabled = enabled;
        saveConfiguration();

        if (!enabled)
        {
            ClearDelayedTodPrompts("Dare");
        }

        chatGui.Print($"RollTracker !dare {(enabled ? "enabled" : "disabled")}.", "RollTracker");
    }

    public void SetHelpTriggerEnabled(bool enabled)
    {
        configuration.HelpTriggerEnabled = enabled;
        saveConfiguration();

        if (!enabled)
        {
            ClearDelayedTodPrompts("Help");
        }

        chatGui.Print($"RollTracker !help {(enabled ? "enabled" : "disabled")}.", "RollTracker");
    }

    public void SetSecondPairEnabled(bool enabled)
    {
        configuration.TodSecondPairEnabled = enabled;
        saveConfiguration();

        if (!enabled && IsSecondPairRoundRunning)
        {
            roundEndsAt = null;
            pendingMacroSteps.Clear();
            Reset();
            currentRoundKind = RoundKind.Normal;
        }

        chatGui.Print($"RollTracker !tod2 second pair rounds {(enabled ? "enabled" : "disabled")}.", "RollTracker");
    }

    public void SetWifiEnabled(bool enabled)
    {
        configuration.WifiEnabled = enabled;
        saveConfiguration();

        if (!enabled)
        {
            pendingWifiMacroSteps.Clear();
        }

        chatGui.Print($"RollTracker !wifi {(enabled ? "enabled" : "disabled")}.", "RollTracker");
    }

    public void StartRoundFromTrigger(string triggeredBy)
    {
        if (!configuration.Enabled)
        {
            return;
        }

        if (IsRoundRunning)
        {
            log.Debug("Ignored !tod from {PlayerName}; round is already running.", triggeredBy);
            return;
        }

        Reset();
        pendingMacroSteps.Clear();
        roundEndsAt = null;
        currentRoundKind = RoundKind.Normal;

        BuildMacroQueue(configuration.MacroText);
        roundEndsAt = DateTimeOffset.Now.AddSeconds(Math.Clamp(configuration.MacroDurationSeconds, 1, 600));
        nextMacroStepAt = DateTimeOffset.Now;

        chatGui.Print($"Round started by {triggeredBy}.", "RollTracker");
    }

    public void StartSecondPairRoundFromTrigger(string triggeredBy)
    {
        if (!configuration.TodSecondPairEnabled)
        {
            return;
        }

        if (IsRoundRunning)
        {
            log.Debug("Ignored !tod2 from {PlayerName}; round is already running.", triggeredBy);
            return;
        }

        Reset();
        pendingMacroSteps.Clear();
        roundEndsAt = null;
        currentRoundKind = RoundKind.SecondPair;

        BuildMacroQueue(configuration.TodSecondPairMacroText);
        roundEndsAt = DateTimeOffset.Now.AddSeconds(Math.Clamp(configuration.TodSecondPairMacroDurationSeconds, 1, 600));
        nextMacroStepAt = DateTimeOffset.Now;

        chatGui.Print($"Second pair round started by {triggeredBy}.", "RollTracker");
    }

    public void StartWifiMacro(string triggeredBy)
    {
        if (!configuration.WifiEnabled)
        {
            return;
        }

        if (IsWifiMacroRunning)
        {
            log.Debug("Ignored !wifi from {PlayerName}; wifi macro is already running.", triggeredBy);
            return;
        }

        BuildWifiMacroQueue();
        nextWifiMacroStepAt = DateTimeOffset.Now;
        chatGui.Print($"!wifi triggered by {triggeredBy}.", "RollTracker");
    }

    private void OnHandleableChatMessage(IHandleableChatMessage chatMessage)
    {
        OnChatMessage(chatMessage);
    }

    private void OnChatMessage(IChatMessage chatMessage)
    {
        var sender = chatMessage.Sender.TextValue.Trim();
        var message = chatMessage.Message.TextValue.Trim();

        if (configuration.HelpTriggerEnabled && IsHelpTrigger(message))
        {
            SendHelp();
            return;
        }

        if (IsWifiTrigger(message))
        {
            StartWifiMacro(string.IsNullOrWhiteSpace(sender) ? "Unknown" : sender);
            return;
        }

        if (configuration.Enabled && configuration.TruthTriggerEnabled && IsTruthTrigger(message))
        {
            SendRandomTodPrompt("Truth", configuration.TruthPrompts);
            return;
        }

        if (configuration.Enabled && configuration.DareTriggerEnabled && IsDareTrigger(message))
        {
            SendRandomTodPrompt("Dare", configuration.DarePrompts);
            return;
        }

        if (configuration.Enabled && IsRoundEndMarker(message))
        {
            StartRoundFromTrigger(string.IsNullOrWhiteSpace(sender) ? "Unknown" : sender);
            return;
        }

        if (IsSecondPairRoundMarker(message))
        {
            StartSecondPairRoundFromTrigger(string.IsNullOrWhiteSpace(sender) ? "Unknown" : sender);
            return;
        }

        if (!configuration.Enabled && !IsSecondPairRoundRunning)
        {
            return;
        }

        if (!TryParseRoll(sender, message, out var playerName, out var value))
        {
            return;
        }

        AddRoll(playerName, value);
    }

    private void OnLogMessage(ILogMessage logMessage)
    {
        if (!configuration.Enabled && !IsSecondPairRoundRunning)
        {
            return;
        }

        var source = logMessage.SourceEntity;
        if (source is null || !source.IsPlayer)
        {
            return;
        }

        var playerName = NormalizePlayerName(source.Name.ToString() ?? string.Empty);
        if (string.IsNullOrWhiteSpace(playerName))
        {
            return;
        }

        var intParameters = new List<int>();
        for (var i = 0; i < logMessage.ParameterCount; i++)
        {
            if (logMessage.TryGetIntParameter(i, out var intParameter))
            {
                intParameters.Add(intParameter);
            }
        }

        if (intParameters.Count == 0)
        {
            return;
        }

        if (LooksLikeLimitedRoll(intParameters))
        {
            log.Debug("Ignored limited roll from {PlayerName}. Parameters: {Parameters}", playerName, string.Join(", ", intParameters));
            return;
        }

        var value = intParameters[0];
        if (value is < 0 or > 9999)
        {
            return;
        }

        AddRoll(playerName, value);
    }

    private void AddRoll(string playerName, int value)
    {
        playerName = NormalizePlayerName(playerName);

        if (string.IsNullOrWhiteSpace(playerName))
        {
            playerName = "Unknown";
        }

        if (playerName.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
        {
            log.Debug("Ignored roll without player name: {Value}", value);
            return;
        }

        if (rolls.Any(roll => roll.PlayerName.Equals(playerName, StringComparison.OrdinalIgnoreCase)))
        {
            log.Debug("Ignored later roll from {PlayerName}: {Value}", playerName, value);
            return;
        }

        var entry = new RollEntry(playerName, value, DateTimeOffset.Now);
        rolls.Add(entry);

        log.Debug("Recorded first roll: {PlayerName} -> {Value}", playerName, value);
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        var now = DateTimeOffset.Now;

        if (roundEndsAt is not null && pendingMacroSteps.Count > 0 && now >= nextMacroStepAt)
        {
            ExecuteNextMacroStep();
        }

        if (configuration.WifiEnabled && pendingWifiMacroSteps.Count > 0 && now >= nextWifiMacroStepAt)
        {
            ExecuteNextWifiMacroStep();
        }

        if (pendingTodPromptCommands.Count > 0 && now >= pendingTodPromptCommands.Peek().ExecuteAt)
        {
            ExecuteNextTodPromptCommand();
        }

        if (roundEndsAt is not null && now >= roundEndsAt.Value)
        {
            FinishRoundAndReset();
        }
    }

    private void OnTerritoryChanged(uint territoryType)
    {
        var isInHousingInterior = IsHousingInterior(territoryType);

        if (configuration.AutoDisableWhenLeavingHousing &&
            (configuration.Enabled ||
             configuration.TodSecondPairEnabled ||
             configuration.TruthTriggerEnabled ||
             configuration.DareTriggerEnabled ||
             configuration.HelpTriggerEnabled ||
             configuration.WifiEnabled) &&
            wasInHousingInterior &&
            !isInHousingInterior)
        {
            configuration.Enabled = false;
            configuration.TruthTriggerEnabled = false;
            configuration.DareTriggerEnabled = false;
            configuration.HelpTriggerEnabled = false;
            configuration.TodSecondPairEnabled = false;
            configuration.WifiEnabled = false;
            roundEndsAt = null;
            pendingMacroSteps.Clear();
            pendingWifiMacroSteps.Clear();
            pendingTodPromptCommands.Clear();
            saveConfiguration();
            chatGui.Print("RollTracker disabled because you left the house.", "RollTracker");
        }

        wasInHousingInterior = isInHousingInterior;
    }

    private bool IsHousingInterior(uint territoryType)
    {
        return housingInteriorTerritoryIds.Contains(territoryType);
    }

    private bool IsSecondPairRoundRunning => roundEndsAt is not null && currentRoundKind == RoundKind.SecondPair;

    private void BuildMacroQueue(string macroText)
    {
        pendingMacroSteps.Clear();

        var lines = macroText
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            pendingMacroSteps.Enqueue(ParseMacroStep(line));
        }
    }

    private void BuildWifiMacroQueue()
    {
        pendingWifiMacroSteps.Clear();

        var lines = configuration.WifiMacroText
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            pendingWifiMacroSteps.Enqueue(ParseWifiMacroStep(line, configuration.WifiChatChannel));
        }
    }

    private void ExecuteNextMacroStep()
    {
        var step = pendingMacroSteps.Dequeue();

        if (step.WaitMilliseconds > 0)
        {
            nextMacroStepAt = DateTimeOffset.Now.AddMilliseconds(step.WaitMilliseconds);
            return;
        }

        if (!TryExecuteTextCommand(step.Command))
        {
            chatGui.PrintError($"Could not run macro line: {step.Command}", "RollTracker");
        }

        nextMacroStepAt = DateTimeOffset.Now.AddMilliseconds(Math.Clamp(configuration.MacroLineDelayMilliseconds, 100, 10000));
    }

    private void ExecuteNextWifiMacroStep()
    {
        var step = pendingWifiMacroSteps.Dequeue();

        if (step.WaitMilliseconds > 0)
        {
            nextWifiMacroStepAt = DateTimeOffset.Now.AddMilliseconds(step.WaitMilliseconds);
            return;
        }

        if (!TryExecuteTextCommand(step.Command))
        {
            chatGui.PrintError($"Could not run !wifi macro line: {step.Command}", "RollTracker");
        }

        nextWifiMacroStepAt = DateTimeOffset.Now.AddMilliseconds(Math.Clamp(configuration.MacroLineDelayMilliseconds, 100, 10000));
    }

    private void ExecuteNextTodPromptCommand()
    {
        var delayedCommand = pendingTodPromptCommands.Dequeue();
        if (!TryExecuteTextCommand(delayedCommand.Command))
        {
            chatGui.PrintError($"Could not send {delayedCommand.PromptType} prompt.", "RollTracker");
        }
    }

    private void ClearDelayedTodPrompts(string promptType)
    {
        if (pendingTodPromptCommands.Count == 0)
        {
            return;
        }

        var remainingCommands = pendingTodPromptCommands
            .Where(command => !command.PromptType.Equals(promptType, StringComparison.OrdinalIgnoreCase))
            .ToList();

        pendingTodPromptCommands.Clear();
        foreach (var command in remainingCommands)
        {
            pendingTodPromptCommands.Enqueue(command);
        }
    }

    private static MacroStep ParseMacroStep(string line)
    {
        if (line.StartsWith("/wait ", StringComparison.OrdinalIgnoreCase) &&
            double.TryParse(line[6..].Trim(), out var waitSeconds))
        {
            return new MacroStep(string.Empty, (int)Math.Clamp(waitSeconds * 1000, 100, 60000));
        }

        return new MacroStep(line, 0);
    }

    private static MacroStep ParseWifiMacroStep(string line, string channel)
    {
        var step = ParseMacroStep(line);
        if (step.WaitMilliseconds > 0 || step.Command.StartsWith("/", StringComparison.Ordinal))
        {
            return step;
        }

        return new MacroStep($"{GetChatCommand(channel)} {step.Command}", 0);
    }

    private static string GetChatCommand(string channel)
    {
        return channel switch
        {
            "Say" => "/s",
            "Party" => "/p",
            _ => "/y",
        };
    }

    private bool TryExecuteTextCommand(string command)
    {
        try
        {
            if (commandManager.ProcessCommand(command))
            {
                return true;
            }

            textCommandService.Execute(command);
            return true;
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed to execute text command: {Command}", command);
            return false;
        }
    }

    private void SendRandomTodPrompt(string promptType, IReadOnlyList<string> prompts)
    {
        var usablePrompts = prompts
            .Select(prompt => prompt.Trim())
            .Where(prompt => !string.IsNullOrWhiteSpace(prompt))
            .ToList();

        if (usablePrompts.Count == 0)
        {
            chatGui.PrintError($"No {promptType} prompts configured.", "RollTracker");
            return;
        }

        var prompt = usablePrompts[Random.Shared.Next(usablePrompts.Count)];
        pendingTodPromptCommands.Enqueue(new DelayedCommand(
            $"{GetChatCommand(configuration.TodPromptChatChannel)} {promptType}: {prompt}",
            promptType,
            DateTimeOffset.Now.AddMilliseconds(500)));
    }

    private void SendHelp()
    {
        var helpLines = BuildHelpLines();
        var chatCommand = GetChatCommand(configuration.HelpChatChannel);

        for (var i = 0; i < helpLines.Count; i++)
        {
            pendingTodPromptCommands.Enqueue(new DelayedCommand(
                $"{chatCommand} {helpLines[i]}",
                "Help",
                DateTimeOffset.Now.AddMilliseconds(500 + (i * 800))));
        }
    }

    private List<string> BuildHelpLines()
    {
        var helpLines = new List<string>();

        if (configuration.HelpTriggerEnabled)
        {
            helpLines.Add("!help - Show currently available RollTracker chat commands.");
        }

        if (configuration.Enabled)
        {
            helpLines.Add("!tod - Start a Truth or Dare roll round.");
        }

        if (configuration.TodSecondPairEnabled)
        {
            helpLines.Add("!tod2 - Start a second-pair Truth or Dare roll round.");
        }

        if (configuration.Enabled && configuration.TruthTriggerEnabled)
        {
            helpLines.Add("!truth - Send a random Truth prompt.");
        }

        if (configuration.Enabled && configuration.DareTriggerEnabled)
        {
            helpLines.Add("!dare - Send a random Dare prompt.");
        }

        if (configuration.WifiEnabled)
        {
            helpLines.Add("!wifi - Show KinkHouse Shells and Discord info.");
        }

        return helpLines;
    }

    private static bool IsRoundEndMarker(string message)
    {
        return message.Equals("!tod", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsHelpTrigger(string message)
    {
        return message.Equals("!help", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSecondPairRoundMarker(string message)
    {
        return message.Equals("!tod2", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTruthTrigger(string message)
    {
        return message.Equals("!truth", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDareTrigger(string message)
    {
        return message.Equals("!dare", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsWifiTrigger(string message)
    {
        return message.Equals("!wifi", StringComparison.OrdinalIgnoreCase);
    }

    private bool TryParseRoll(string sender, string message, out string playerName, out int value)
    {
        foreach (var regex in RollRegexes())
        {
            var match = regex.Match(message);
            if (!match.Success)
            {
                continue;
            }

            if (!IsAllowedRollRange(match))
            {
                continue;
            }

            playerName = NormalizePlayerName(match.Groups["name"].Success ? match.Groups["name"].Value : sender);
            if (playerName.Equals("You", StringComparison.OrdinalIgnoreCase))
            {
                playerName = string.IsNullOrWhiteSpace(sender) ? "You" : NormalizePlayerName(sender);
            }

            if (int.TryParse(match.Groups["value"].Value, out value))
            {
                return true;
            }
        }

        playerName = string.Empty;
        value = 0;
        return false;
    }

    private string NormalizePlayerName(string rawName)
    {
        var name = rawName.Trim().TrimEnd('.', ':');
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var atIndex = name.IndexOf('@', StringComparison.Ordinal);
        if (atIndex > 0)
        {
            name = name[..atIndex].Trim();
        }

        foreach (var worldName in worldNames)
        {
            if (name.EndsWith($" {worldName}", StringComparison.OrdinalIgnoreCase))
            {
                return name[..^worldName.Length].Trim();
            }

            if (name.EndsWith(worldName, StringComparison.OrdinalIgnoreCase) &&
                name.Length > worldName.Length &&
                char.IsLower(name[name.Length - worldName.Length - 1]))
            {
                return name[..^worldName.Length].Trim();
            }
        }

        return name;
    }

    private static List<string> BuildWorldNames(IDataManager dataManager)
    {
        var names = dataManager.GetExcelSheet<World>()?
            .Select(row => row.Name.ToString().Trim())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(name => name.Length)
            .ToList() ?? [];

        if (names.Count > 0)
        {
            return names;
        }

        return
        [
            "Alpha", "Balmung", "Behemoth", "Bismarck", "Brynhildr", "Cactuar", "Cerberus", "Coeurl",
            "Diabolos", "Excalibur", "Exodus", "Faerie", "Famfrit", "Gilgamesh", "Goblin", "Hyperion",
            "Jenova", "Lamia", "Leviathan", "Lich", "Louisoix", "Malboro", "Mateus", "Midgardsormr",
            "Moogle", "Odin", "Omega", "Phantom", "Phoenix", "Ragnarok", "Raiden", "Ravana",
            "Sargatanas", "Sagittarius", "Sephirot", "Seraph", "Shiva", "Siren", "Sophia", "Spriggan",
            "Twintania", "Ultros", "Zalera", "Zodiark", "Zurvan"
        ];
    }

    private static bool IsAllowedRollRange(Match match)
    {
        if (!match.Groups["min"].Success && !match.Groups["max"].Success)
        {
            return true;
        }

        if (!int.TryParse(match.Groups["min"].Value, out var min) ||
            !int.TryParse(match.Groups["max"].Value, out var max))
        {
            return false;
        }

        return min is 0 or 1 && max >= 999;
    }

    private static bool LooksLikeLimitedRoll(IReadOnlyList<int> intParameters)
    {
        if (intParameters.Count < 2)
        {
            return false;
        }

        return intParameters.Skip(1).Any(parameter => parameter is > 0 and < 999);
    }

    private static Regex[] RollRegexes()
    {
        return
        [
            EnglishRollRegex(),
            GermanRollRegex(),
            SenderOnlyEnglishRollRegex(),
            SenderOnlyGermanRollRegex(),
            YouRollRegex(),
        ];
    }

    [GeneratedRegex(@"^(?<name>.+?)\s+rolls\s+(?:a\s+)?(?<value>\d{1,4})(?:\s+\((?<min>\d+)-(?<max>\d+)\))?[.!]?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EnglishRollRegex();

    [GeneratedRegex(@"^(?<name>.+?)\s+w[üu]rfelt\s+(?:eine\s+|einen\s+)?(?<value>\d{1,4})(?:\s+\((?<min>\d+)-(?<max>\d+)\))?[.!]?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex GermanRollRegex();

    [GeneratedRegex(@"^rolls\s+(?:a\s+)?(?<value>\d{1,4})(?:\s+\((?<min>\d+)-(?<max>\d+)\))?[.!]?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SenderOnlyEnglishRollRegex();

    [GeneratedRegex(@"^w[üu]rfelt\s+(?:eine\s+|einen\s+)?(?<value>\d{1,4})(?:\s+\((?<min>\d+)-(?<max>\d+)\))?[.!]?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SenderOnlyGermanRollRegex();

    [GeneratedRegex(@"^you\s+roll\s+(?:a\s+)?(?<value>\d{1,4})(?:\s+\((?<min>\d+)-(?<max>\d+)\))?[.!]?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex YouRollRegex();

    private readonly record struct MacroStep(string Command, int WaitMilliseconds);

    private readonly record struct DelayedCommand(string Command, string PromptType, DateTimeOffset ExecuteAt);

    private readonly record struct SpecialRuleMatch(string Text, bool StopPairAfterMatch, bool AlwaysShown);

    private enum RoundKind
    {
        Normal,
        SecondPair,
    }
}

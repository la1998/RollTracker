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
    private readonly HashSet<uint> housingInteriorTerritoryIds;
    private readonly List<string> worldNames;

    private DateTimeOffset? roundEndsAt;
    private DateTimeOffset nextMacroStepAt;
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

    public TimeSpan RemainingRoundTime => roundEndsAt is null
        ? TimeSpan.Zero
        : roundEndsAt.Value - DateTimeOffset.Now;

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

        var result = BuildResultCommand(highest, lowest);
        if (!TryExecuteTextCommand(result))
        {
            chatGui.PrintError("Could not send result to yell chat.", "RollTracker");
        }

        Reset();
    }

    private string BuildResultCommand(RollEntry highest, RollEntry lowest)
    {
        var template = string.IsNullOrWhiteSpace(configuration.ResultCommandTemplate)
            ? "/y \"{highest}\">\"{lowest}\""
            : configuration.ResultCommandTemplate;

        return template
            .Replace("{highest}", highest.PlayerName, StringComparison.OrdinalIgnoreCase)
            .Replace("{lowest}", lowest.PlayerName, StringComparison.OrdinalIgnoreCase)
            .Replace("{highestRoll}", highest.Value.ToString(), StringComparison.OrdinalIgnoreCase)
            .Replace("{lowestRoll}", lowest.Value.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    public void AddTestRolls()
    {
        AddRoll("Example One", Random.Shared.Next(1, 1000));
        AddRoll("Example Two", Random.Shared.Next(1, 1000));
        AddRoll("Example Three", Random.Shared.Next(1, 1000));
    }

    public void SetEnabled(bool enabled)
    {
        configuration.Enabled = enabled;
        saveConfiguration();

        if (!enabled)
        {
            roundEndsAt = null;
            pendingMacroSteps.Clear();
        }

        chatGui.Print($"RollTracker {(enabled ? "enabled" : "disabled")}.", "RollTracker");
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

        BuildMacroQueue();
        roundEndsAt = DateTimeOffset.Now.AddSeconds(Math.Clamp(configuration.MacroDurationSeconds, 1, 600));
        nextMacroStepAt = DateTimeOffset.Now;

        chatGui.Print($"Round started by {triggeredBy}.", "RollTracker");
    }

    private void OnHandleableChatMessage(IHandleableChatMessage chatMessage)
    {
        OnChatMessage(chatMessage);
    }

    private void OnChatMessage(IChatMessage chatMessage)
    {
        var sender = chatMessage.Sender.TextValue.Trim();
        var message = chatMessage.Message.TextValue.Trim();

        if (!configuration.Enabled)
        {
            return;
        }

        if (IsRoundEndMarker(message))
        {
            StartRoundFromTrigger(string.IsNullOrWhiteSpace(sender) ? "Unknown" : sender);
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
        if (!configuration.Enabled)
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
        if (!configuration.Enabled || roundEndsAt is null)
        {
            return;
        }

        var now = DateTimeOffset.Now;

        if (pendingMacroSteps.Count > 0 && now >= nextMacroStepAt)
        {
            ExecuteNextMacroStep();
        }

        if (now >= roundEndsAt.Value)
        {
            FinishRoundAndReset();
        }
    }

    private void OnTerritoryChanged(uint territoryType)
    {
        var isInHousingInterior = IsHousingInterior(territoryType);

        if (configuration.AutoDisableWhenLeavingHousing &&
            configuration.Enabled &&
            wasInHousingInterior &&
            !isInHousingInterior)
        {
            configuration.Enabled = false;
            roundEndsAt = null;
            pendingMacroSteps.Clear();
            saveConfiguration();
            chatGui.Print("RollTracker disabled because you left the house.", "RollTracker");
        }

        wasInHousingInterior = isInHousingInterior;
    }

    private bool IsHousingInterior(uint territoryType)
    {
        return housingInteriorTerritoryIds.Contains(territoryType);
    }

    private void BuildMacroQueue()
    {
        pendingMacroSteps.Clear();

        var lines = configuration.MacroText
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            pendingMacroSteps.Enqueue(ParseMacroStep(line));
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

    private static MacroStep ParseMacroStep(string line)
    {
        if (line.StartsWith("/wait ", StringComparison.OrdinalIgnoreCase) &&
            double.TryParse(line[6..].Trim(), out var waitSeconds))
        {
            return new MacroStep(string.Empty, (int)Math.Clamp(waitSeconds * 1000, 100, 60000));
        }

        return new MacroStep(line, 0);
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

    private static bool IsRoundEndMarker(string message)
    {
        return message.Equals("!tod", StringComparison.OrdinalIgnoreCase);
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
}

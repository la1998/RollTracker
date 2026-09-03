using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Chat;
using Dalamud.Game.Text;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;

namespace RollTracker.Services;

internal sealed partial class RollTrackerService : IDisposable
{
    private const string DefaultResultCommandTemplate = "/y \"{highest}\"({highestRoll})>>>\"{lowest}\"({lowestRoll})";
    private const int ChatAliasFeedbackDelayMilliseconds = 1500;
    private const int AutoStatusEffectDelayMilliseconds = 2500;
    private const string LegacySecondPairMacroText = "/y ♦ Time for Truth or Dare 2 ♦  Highest asks lowest, second highest asks second lowest. Type /random in chat! 60 seconds... And GO!\n/wait 50\n/y 10 seconds remain...\n/wait 10\n/y End";
    private const string DefaultSecondPairMacroText = "/y ♦ Time for Truth or Dare 2 ♦  Highest asks lowest, second highest asks second lowest,  \"Truth or Dare?\" Type /random in chat! 60 seconds... And GO!\n/wait 50\n/y 10 seconds remain...\n/wait 10\n/y End";
    private const string LegacySecondPairResultCommandTemplate = "/y \"{highest}\"({highestRoll})>>>\"{lowest}\"({lowestRoll}) 2nd: \"{secondHighest}\"({secondHighestRoll})>>>\"{secondLowest}\"({secondLowestRoll})";
    private const string DefaultSecondPairResultCommandTemplate = "/y \"{highest}\"({highestRoll})>>>\"{lowest}\"({lowestRoll})\n/y 2nd: \"{secondHighest}\"({secondHighestRoll})>>>\"{secondLowest}\"({secondLowestRoll})";

    private readonly IChatGui chatGui;
    private readonly ICommandManager commandManager;
    private readonly IFramework framework;
    private readonly IClientState clientState;
    private readonly IPlayerState playerState;
    private readonly ICondition condition;
    private readonly IPluginLog log;
    private readonly Configuration configuration;
    private readonly System.Action saveConfiguration;
    private readonly TextCommandService textCommandService = new();
    private readonly List<RollEntry> rolls = [];
    private readonly Queue<MacroStep> pendingMacroSteps = [];
    private readonly Queue<MacroStep> pendingWifiMacroSteps = [];
    private readonly Queue<DelayedCommand> pendingTodPromptCommands = [];
    private readonly Queue<DelayedCommand> pendingStatusEffectCommands = [];
    private readonly HashSet<uint> housingInteriorTerritoryIds;
    private readonly HashSet<uint> residentialTerritoryIds;
    private readonly List<string> worldNames;
    private readonly Dictionary<ushort, WorldDebugInfo> worldInfos;
    private readonly Dictionary<uint, string> territoryNames;

    private DateTimeOffset? roundEndsAt;
    private DateTimeOffset nextMacroStepAt;
    private DateTimeOffset nextWifiMacroStepAt;
    private RoundKind currentRoundKind = RoundKind.Normal;
    private int nextManualRollNumber = 1;
    private bool wasInHousingInterior;
    private bool wasInResidentialArea;
    private bool wasBetweenAreas;
    private bool lastAutoOffTransitionWasHousingInteriorMove;
    private DateTimeOffset? pendingAutoOnCheckUntil;
    private bool pendingAutoOnEnteringAutoOff;
    private bool pendingAutoOnTerritoryChangeAutoOff;
    private string? lastHousingAddressKey;
    private uint lastTerritoryType;

    public RollTrackerService(
        IChatGui chatGui,
        ICommandManager commandManager,
        IFramework framework,
        IClientState clientState,
        IPlayerState playerState,
        ICondition condition,
        IDataManager dataManager,
        IPluginLog log,
        Configuration configuration,
        System.Action saveConfiguration)
    {
        this.chatGui = chatGui;
        this.commandManager = commandManager;
        this.framework = framework;
        this.clientState = clientState;
        this.playerState = playerState;
        this.condition = condition;
        this.log = log;
        this.configuration = configuration;
        this.saveConfiguration = saveConfiguration;
        NormalizeLegacyDefaults();
        housingInteriorTerritoryIds = dataManager.GetExcelSheet<HousingIndoorTerritory>()?.Select(row => row.RowId).ToHashSet() ?? [];
        residentialTerritoryIds = CreateDefaultResidentialTerritoryIds();
        worldNames = BuildWorldNames(dataManager);
        worldInfos = BuildWorldInfos(dataManager);
        territoryNames = BuildTerritoryNames(dataManager);
        lastTerritoryType = clientState.TerritoryType;
        wasInHousingInterior = IsHousingInterior(clientState.TerritoryType);
        wasInResidentialArea = IsResidentialArea(clientState.TerritoryType);
        wasBetweenAreas = IsBetweenAreas();

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

    public unsafe HousingDebugInfo GetCurrentHousingDebugInfo()
    {
        var territoryType = clientState.TerritoryType;
        var isHousingInterior = IsHousingInterior(territoryType);
        var currentWorldInfo = GetCurrentWorldDebugInfo();
        var currentTerritoryName = GetTerritoryName(territoryType);
        var manager = HousingManager.Instance();
        if (manager is null)
        {
            return new HousingDebugInfo(
                territoryType,
                isHousingInterior,
                IsResidentialArea(territoryType),
                IsBetweenAreas(),
                false,
                string.Empty,
                sbyte.MinValue,
                sbyte.MinValue,
                byte.MinValue,
                short.MinValue,
                string.Empty,
                string.Empty,
                0,
                false,
                currentWorldInfo.WorldId,
                currentWorldInfo.WorldName,
                currentWorldInfo.DataCenterName,
                currentTerritoryName,
                BuildCurrentLocationPreview(currentWorldInfo, currentTerritoryName),
                "-",
                false);
        }

        var currentHouseId = manager->GetCurrentHouseId();
        var currentIndoorHouseId = manager->GetCurrentIndoorHouseId();
        var addressHouseId = currentIndoorHouseId.Id != 0 ? currentIndoorHouseId : currentHouseId;
        var originalHouseTerritoryTypeId = HousingManager.GetOriginalHouseTerritoryTypeId();
        var worldInfo = isHousingInterior && addressHouseId.WorldId != 0 && addressHouseId.WorldId != ushort.MaxValue
            ? GetWorldDebugInfo(addressHouseId.WorldId)
            : currentWorldInfo;
        var districtName = GetTerritoryName(originalHouseTerritoryTypeId, addressHouseId.TerritoryTypeId, territoryType);
        var addressPreview = isHousingInterior
            ? BuildHousingAddressPreview(worldInfo, districtName, addressHouseId)
            : "-";

        return new HousingDebugInfo(
            territoryType,
            isHousingInterior,
            IsResidentialArea(territoryType),
            IsBetweenAreas(),
            true,
            manager->GetCurrentHousingTerritoryType().ToString(),
            manager->GetCurrentWard(),
            manager->GetCurrentPlot(),
            manager->GetCurrentDivision(),
            manager->GetCurrentRoom(),
            FormatHouseId(currentHouseId),
            FormatHouseId(currentIndoorHouseId),
            originalHouseTerritoryTypeId,
            manager->HasHousePermissions(),
            addressHouseId.WorldId,
            worldInfo.WorldName,
            worldInfo.DataCenterName,
            districtName,
            BuildCurrentLocationPreview(currentWorldInfo, currentTerritoryName),
            addressPreview,
            isHousingInterior);
    }

    private static string FormatHouseId(HouseId houseId)
    {
        return $"Id={houseId.Id}, WardIndex={houseId.WardIndex}, PlotIndex={houseId.PlotIndex}, Room={houseId.RoomNumber}, Apartment={houseId.IsApartment}, ApartmentDivision={houseId.ApartmentDivision}, Territory={houseId.TerritoryTypeId}, World={houseId.WorldId}";
    }

    private WorldDebugInfo GetWorldDebugInfo(ushort worldId)
    {
        return worldInfos.TryGetValue(worldId, out var worldInfo)
            ? worldInfo
            : new WorldDebugInfo(worldId, worldId == 0 ? "-" : $"World {worldId}", "-");
    }

    private WorldDebugInfo GetCurrentWorldDebugInfo()
    {
        var currentWorld = playerState.CurrentWorld;
        if (currentWorld.IsValid && currentWorld.ValueNullable is { } world)
        {
            var worldName = world.Name.ToString().Trim();
            var dataCenterName = world.DataCenter.ValueNullable?.Name.ToString().Trim() ?? string.Empty;
            var worldId = (ushort)(world.RowId != 0 ? world.RowId : currentWorld.RowId);
            return new WorldDebugInfo(worldId, string.IsNullOrWhiteSpace(worldName) ? $"World {worldId}" : worldName, string.IsNullOrWhiteSpace(dataCenterName) ? "-" : dataCenterName);
        }

        return new WorldDebugInfo(0, "-", "-");
    }

    private string GetTerritoryName(params uint[] territoryIds)
    {
        foreach (var territoryId in territoryIds)
        {
            if (territoryId != 0 &&
                territoryNames.TryGetValue(territoryId, out var territoryName) &&
                !string.IsNullOrWhiteSpace(territoryName))
            {
                return territoryName;
            }
        }

        return "-";
    }

    private static string BuildHousingAddressPreview(WorldDebugInfo worldInfo, string districtName, HouseId houseId)
    {
        var locationPrefix = $"{worldInfo.DataCenterName} / {worldInfo.WorldName} / {districtName}";
        var ward = houseId.WardIndex + 1;
        var plot = houseId.PlotIndex + 1;

        if (houseId.IsApartment)
        {
            return $"{locationPrefix} / Ward {ward} / Apartment Room {houseId.RoomNumber}";
        }

        if (houseId.RoomNumber > 0)
        {
            return $"{locationPrefix} / Ward {ward} / Plot {plot} / Room {houseId.RoomNumber}";
        }

        return $"{locationPrefix} / Ward {ward} / Plot {plot}";
    }

    private static string BuildCurrentLocationPreview(WorldDebugInfo worldInfo, string territoryName)
    {
        return $"{worldInfo.DataCenterName} / {worldInfo.WorldName} / {territoryName}";
    }

    public unsafe bool TryCreateCurrentHousingAddressEntry(string name, out HousingAddressEntry entry, out string message)
    {
        entry = new HousingAddressEntry();
        var territoryType = clientState.TerritoryType;
        if (!IsHousingInterior(territoryType))
        {
            message = "Enter a housing interior before saving an address.";
            return false;
        }

        var manager = HousingManager.Instance();
        if (manager is null)
        {
            message = "Housing manager is not available yet.";
            return false;
        }

        var currentHouseId = manager->GetCurrentHouseId();
        var currentIndoorHouseId = manager->GetCurrentIndoorHouseId();
        var houseId = currentIndoorHouseId.Id != 0 ? currentIndoorHouseId : currentHouseId;
        if (houseId.Id == 0 || houseId.WorldId == 0 || houseId.WorldId == ushort.MaxValue)
        {
            message = "The current interior address is not reliable yet.";
            return false;
        }

        var originalHouseTerritoryTypeId = HousingManager.GetOriginalHouseTerritoryTypeId();
        var worldInfo = GetWorldDebugInfo(houseId.WorldId);
        var districtName = GetTerritoryName(originalHouseTerritoryTypeId, houseId.TerritoryTypeId, territoryType);
        var address = BuildHousingAddressPreview(worldInfo, districtName, houseId);

        entry = new HousingAddressEntry
        {
            Enabled = true,
            Name = string.IsNullOrWhiteSpace(name) ? address : name.Trim(),
            Address = address,
            DataCenterName = worldInfo.DataCenterName,
            WorldName = worldInfo.WorldName,
            WorldId = houseId.WorldId,
            DistrictName = districtName,
            TerritoryTypeId = houseId.TerritoryTypeId,
            OriginalHouseTerritoryTypeId = originalHouseTerritoryTypeId,
            WardIndex = (sbyte)houseId.WardIndex,
            PlotIndex = (sbyte)houseId.PlotIndex,
            Division = houseId.IsApartment ? houseId.ApartmentDivision : manager->GetCurrentDivision(),
            RoomNumber = houseId.RoomNumber,
            IsApartment = houseId.IsApartment,
            HouseId = houseId.Id,
        };
        message = $"Saved address: {address}";
        return true;
    }

    public static bool IsSameHousingAddress(HousingAddressEntry left, HousingAddressEntry right)
    {
        return left.WorldId == right.WorldId &&
            left.OriginalHouseTerritoryTypeId == right.OriginalHouseTerritoryTypeId &&
            left.WardIndex == right.WardIndex &&
            left.PlotIndex == right.PlotIndex &&
            left.RoomNumber == right.RoomNumber &&
            left.IsApartment == right.IsApartment;
    }

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

        var resultCommands = BuildRoundResultCommands();
        if (currentRoundKind == RoundKind.SecondPair)
        {
            QueueSecondPairResultCommands(resultCommands);
            Reset();
            currentRoundKind = RoundKind.Normal;
            return;
        }

        var anyCommandFailed = false;
        foreach (var resultCommand in resultCommands)
        {
            if (resultCommand.IsSpecialRule)
            {
                continue;
            }

            if (!TryExecuteTextCommand(resultCommand.Command))
            {
                anyCommandFailed = true;
            }
        }

        QueueSpecialRuleResultCommands(resultCommands);

        if (anyCommandFailed)
        {
            chatGui.PrintError("Could not send one or more result chat messages.", "RollTracker");
        }

        Reset();
        currentRoundKind = RoundKind.Normal;
    }

    private void QueueSecondPairResultCommands(IEnumerable<RoundResultCommand> resultCommands)
    {
        var delayMilliseconds = ClampMacroLineDelay(configuration.TodSecondPairResultLineDelayMilliseconds);
        var nextExecuteAt = DateTimeOffset.Now;
        var resultCommandList = resultCommands.ToList();

        foreach (var resultCommand in resultCommandList.Where(command => !command.IsSpecialRule))
        {
            pendingTodPromptCommands.Enqueue(new DelayedCommand(resultCommand.Command, "result message", nextExecuteAt));
            nextExecuteAt = nextExecuteAt.AddMilliseconds(delayMilliseconds);
        }

        QueueSpecialRuleResultCommands(resultCommandList, nextExecuteAt);
    }

    private void QueueSpecialRuleResultCommands(IEnumerable<RoundResultCommand> resultCommands, DateTimeOffset? firstExecuteAt = null)
    {
        var delayMilliseconds = ClampMacroLineDelay(configuration.TodSpecialRuleLineDelayMilliseconds);
        var nextExecuteAt = firstExecuteAt ?? DateTimeOffset.Now.AddMilliseconds(delayMilliseconds);

        foreach (var resultCommand in resultCommands.Where(command => command.IsSpecialRule))
        {
            pendingTodPromptCommands.Enqueue(new DelayedCommand(resultCommand.Command, "Special rule result", nextExecuteAt));
            nextExecuteAt = nextExecuteAt.AddMilliseconds(delayMilliseconds);
        }
    }

    private List<RoundResultCommand> BuildRoundResultCommands()
    {
        if (rolls.Count == 0)
        {
            return currentRoundKind == RoundKind.SecondPair
                ? BuildNotEnoughPlayersResultCommands(
                    configuration.TodSecondPairNotEnoughRoundPlayersResultText,
                    DefaultSecondPairResultCommandTemplate,
                    "Not enough players for a !tod2 round.")
                : BuildNotEnoughPlayersResultCommands(
                    configuration.NotEnoughPlayersResultText,
                    DefaultResultCommandTemplate,
                    "Not enough players for a round.");
        }

        var highest = HighestRoll;
        var lowest = LowestRoll;

        if (highest is null || lowest is null)
        {
            return currentRoundKind == RoundKind.SecondPair
                ? BuildNotEnoughPlayersResultCommands(
                    configuration.TodSecondPairNotEnoughRoundPlayersResultText,
                    DefaultSecondPairResultCommandTemplate,
                    "Not enough players for a !tod2 round.")
                : BuildNotEnoughPlayersResultCommands(
                    configuration.NotEnoughPlayersResultText,
                    DefaultResultCommandTemplate,
                    "Not enough players for a round.");
        }

        return currentRoundKind == RoundKind.SecondPair
            ? BuildSecondPairResultCommands(highest, lowest)
            : BuildResultCommands(highest, lowest);
    }

    private List<RoundResultCommand> BuildResultCommands(RollEntry highest, RollEntry lowest)
    {
        if (rolls.Count < 2)
        {
            return BuildNotEnoughPlayersResultCommands(
                configuration.NotEnoughPlayersResultText,
                DefaultResultCommandTemplate,
                "Not enough players for a round.");
        }

        var resultCommands = BuildPrimaryPairResultCommands(highest, lowest);
        AppendSpecialRuleCommands(resultCommands, 0, BuildTodSpecialRuleTexts());
        return resultCommands;
    }

    private List<RoundResultCommand> BuildPrimaryPairResultCommands(RollEntry highest, RollEntry lowest)
    {
        var resultCommand = BuildResultCommand(highest, lowest);
        var resultCommands = BuildNormalResultCommands(resultCommand);
        if (resultCommands.Count == 0)
        {
            resultCommands.Add(new RoundResultCommand(
                BuildResultCommandFromTemplate(DefaultResultCommandTemplate, highest, lowest),
                false));
        }

        return resultCommands;
    }

    private string BuildResultCommand(RollEntry highest, RollEntry lowest)
    {
        var template = string.IsNullOrWhiteSpace(configuration.ResultCommandTemplate)
            ? DefaultResultCommandTemplate
            : configuration.ResultCommandTemplate;

        return BuildResultCommandFromTemplate(template, highest, lowest);
    }

    private List<RoundResultCommand> BuildSecondPairResultCommands(RollEntry highest, RollEntry lowest)
    {
        if (rolls.Count < 2)
        {
            return BuildNotEnoughPlayersResultCommands(
                configuration.TodSecondPairNotEnoughRoundPlayersResultText,
                DefaultSecondPairResultCommandTemplate,
                "Not enough players for a !tod2 round.");
        }

        if (!TryGetTodSecondPair(highest, lowest, out var secondHighest, out var secondLowest))
        {
            var partialResultCommands = BuildPrimaryPairResultCommands(highest, lowest);
            AppendSecondPairNotEnoughPlayersResultCommands(partialResultCommands);
            AppendSpecialRuleCommands(partialResultCommands, 0, BuildTodSpecialRuleTexts());
            return partialResultCommands;
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

        var resultCommands = BuildNormalResultCommands(result);
        if (resultCommands.Count == 0)
        {
            return BuildResultCommands(highest, lowest);
        }

        AppendSpecialRuleCommands(resultCommands, resultCommands.Count - 1, BuildTodSpecialRuleTexts());

        return resultCommands;
    }

    private static List<RoundResultCommand> BuildNotEnoughPlayersResultCommands(
        string text,
        string fallbackCommandTemplate,
        string fallbackText)
    {
        var commandPrefix = GetCommandPrefix(fallbackCommandTemplate);
        var resultCommands = BuildTextResultCommands(text, commandPrefix);
        if (resultCommands.Count == 0)
        {
            resultCommands.Add(new RoundResultCommand($"{commandPrefix} {fallbackText}", false));
        }

        return resultCommands;
    }

    private void AppendSecondPairNotEnoughPlayersResultCommands(List<RoundResultCommand> resultCommands)
    {
        var commandPrefix = resultCommands.Count > 0
            ? GetCommandPrefix(resultCommands[0].Command)
            : GetCommandPrefix(DefaultSecondPairResultCommandTemplate);
        var notEnoughCommands = BuildTextResultCommands(configuration.TodSecondPairNotEnoughPlayersResultText, commandPrefix);

        if (notEnoughCommands.Count == 0)
        {
            resultCommands.Add(new RoundResultCommand($"{commandPrefix} 2nd: Not enough players for second pair.", false));
            return;
        }

        resultCommands.AddRange(notEnoughCommands);
    }

    private static List<RoundResultCommand> BuildNormalResultCommands(string commandText)
    {
        return SplitCommandLines(commandText)
            .Select(command => new RoundResultCommand(command, false))
            .ToList();
    }

    private static List<RoundResultCommand> BuildTextResultCommands(string text, string commandPrefix)
    {
        return SplitCommandLines(text)
            .Select(line => line.StartsWith("/", StringComparison.Ordinal)
                ? line
                : $"{commandPrefix} {line}")
            .Select(command => new RoundResultCommand(command, false))
            .ToList();
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

    private static void AppendSpecialRuleCommands(List<RoundResultCommand> commands, int sourceCommandIndex, IEnumerable<string> specialRuleTexts)
    {
        if (sourceCommandIndex < 0 || sourceCommandIndex >= commands.Count)
        {
            return;
        }

        var commandPrefix = GetCommandPrefix(commands[sourceCommandIndex].Command);
        foreach (var specialRuleText in specialRuleTexts.Where(text => !string.IsNullOrWhiteSpace(text)))
        {
            commands.Add(new RoundResultCommand($"{commandPrefix} {specialRuleText}", true));
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

    private List<string> BuildTodSpecialRuleTexts()
    {
        if (!configuration.TodSpecialRulesEnabled)
        {
            return [];
        }

        var roundRolls = rolls.Select(roll => roll.Value).ToHashSet();
        return rolls
            .OrderBy(roll => roll.Time)
            .SelectMany(roll => BuildSpecialRuleTextsForRoll(roll, GetRollRole(roll), roundRolls))
            .ToList();
    }

    private List<string> BuildSpecialRuleTextsForRoll(RollEntry roll, string role, IReadOnlyCollection<int> roundRolls)
    {
        return GetActiveSpecialRules()
            .Where(rule => rule.Roll == roll.Value)
            .Where(rule => !ShouldSkipSpecialRule(rule, roundRolls))
            .Select(rule => BuildSpecialRuleText(rule, roll, role))
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToList();
    }

    private IEnumerable<TodSpecialRule> GetActiveSpecialRules()
    {
        if (configuration.TodSpecialRuleSets.Count == 0)
        {
            return configuration.TodSpecialRules;
        }

        return configuration.TodSpecialRuleSets
            .Where(ruleSet => ruleSet.Enabled)
            .SelectMany(ruleSet => ruleSet.Rules ?? []);
    }

    private string GetRollRole(RollEntry roll)
    {
        if (HighestRoll is { } highest && ReferenceEquals(roll, highest))
        {
            return "highest";
        }

        if (LowestRoll is { } lowest && ReferenceEquals(roll, lowest))
        {
            return "lowest";
        }

        return "roll";
    }

    private static string BuildSpecialRuleText(TodSpecialRule rule, RollEntry roll, string role)
    {
        return rule.Text.Trim()
            .Replace("{player}", roll.PlayerName, StringComparison.OrdinalIgnoreCase)
            .Replace("{roll}", roll.Value.ToString(), StringComparison.OrdinalIgnoreCase)
            .Replace("{role}", role, StringComparison.OrdinalIgnoreCase);
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
        var changed = configuration.Enabled != enabled;
        configuration.Enabled = enabled;
        ApplySuggestionLinkForTodModules(enabled || configuration.TodSecondPairEnabled);

        saveConfiguration();

        if (!enabled)
        {
            roundEndsAt = null;
            pendingMacroSteps.Clear();
            pendingTodPromptCommands.Clear();
        }

        if (changed)
        {
            TriggerModuleStatusEffects(StatusEffectModule.Tod, enabled);
        }

        chatGui.Print($"RollTracker !tod {(enabled ? "enabled" : "disabled")}.", "RollTracker");
    }

    public void SetAllModulesEnabled(bool enabled)
    {
        var changedModules = GetChangedStatusEffectModulesForAllToggle(enabled);
        configuration.Enabled = enabled;
        configuration.TruthTriggerEnabled = enabled;
        configuration.DareTriggerEnabled = enabled;
        configuration.HelpTriggerEnabled = enabled;
        configuration.ChatAliasEnabled = enabled;
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

        TriggerModuleStatusEffects(changedModules, enabled);
        chatGui.Print($"RollTracker modules {(enabled ? "enabled" : "disabled")}.", "RollTracker");
    }

    public void SetTruthTriggerEnabled(bool enabled)
    {
        var changed = configuration.TruthTriggerEnabled != enabled;
        configuration.TruthTriggerEnabled = enabled;
        saveConfiguration();

        if (!enabled)
        {
            ClearDelayedTodPrompts("Truth");
        }

        if (changed)
        {
            TriggerModuleStatusEffects(StatusEffectModule.Truth, enabled);
        }

        chatGui.Print($"RollTracker !truth {(enabled ? "enabled" : "disabled")}.", "RollTracker");
    }

    public void SetDareTriggerEnabled(bool enabled)
    {
        var changed = configuration.DareTriggerEnabled != enabled;
        configuration.DareTriggerEnabled = enabled;
        saveConfiguration();

        if (!enabled)
        {
            ClearDelayedTodPrompts("Dare");
        }

        if (changed)
        {
            TriggerModuleStatusEffects(StatusEffectModule.Dare, enabled);
        }

        chatGui.Print($"RollTracker !dare {(enabled ? "enabled" : "disabled")}.", "RollTracker");
    }

    public void SetHelpTriggerEnabled(bool enabled)
    {
        var changed = configuration.HelpTriggerEnabled != enabled;
        configuration.HelpTriggerEnabled = enabled;
        saveConfiguration();

        if (!enabled)
        {
            ClearDelayedTodPrompts("Help");
        }

        if (changed)
        {
            TriggerModuleStatusEffects(StatusEffectModule.Help, enabled);
        }

        chatGui.Print($"RollTracker !help {(enabled ? "enabled" : "disabled")}.", "RollTracker");
    }

    public void SetChatAliasEnabled(bool enabled)
    {
        var changed = configuration.ChatAliasEnabled != enabled;
        configuration.ChatAliasEnabled = enabled;
        saveConfiguration();
        if (changed)
        {
            TriggerModuleStatusEffects(StatusEffectModule.ChatAlias, enabled);
        }

        chatGui.Print($"RollTracker chat alias {(enabled ? "enabled" : "disabled")}.", "RollTracker");
    }

    public void SetSecondPairEnabled(bool enabled)
    {
        var changed = configuration.TodSecondPairEnabled != enabled;
        configuration.TodSecondPairEnabled = enabled;
        ApplySuggestionLinkForTodModules(enabled || configuration.Enabled);
        saveConfiguration();

        if (!enabled && IsSecondPairRoundRunning)
        {
            roundEndsAt = null;
            pendingMacroSteps.Clear();
            Reset();
            currentRoundKind = RoundKind.Normal;
        }

        if (changed)
        {
            TriggerModuleStatusEffects(StatusEffectModule.TodSecondPair, enabled);
        }

        chatGui.Print($"RollTracker !tod2 second pair rounds {(enabled ? "enabled" : "disabled")}.", "RollTracker");
    }

    private void ApplySuggestionLinkForTodModules(bool enabled)
    {
        if (!configuration.LinkSuggestionsToTodModules)
        {
            return;
        }

        var truthChanged = configuration.TruthTriggerEnabled != enabled;
        var dareChanged = configuration.DareTriggerEnabled != enabled;

        configuration.TruthTriggerEnabled = enabled;
        configuration.DareTriggerEnabled = enabled;

        if (!enabled)
        {
            ClearDelayedTodPrompts("Truth");
            ClearDelayedTodPrompts("Dare");
        }

        if (truthChanged)
        {
            TriggerModuleStatusEffects(StatusEffectModule.Truth, enabled);
        }

        if (dareChanged)
        {
            TriggerModuleStatusEffects(StatusEffectModule.Dare, enabled);
        }
    }

    public void SetTodSpecialRulesEnabled(bool enabled)
    {
        var changed = configuration.TodSpecialRulesEnabled != enabled;
        configuration.TodSpecialRulesEnabled = enabled;
        saveConfiguration();

        if (changed)
        {
            TriggerModuleStatusEffects(StatusEffectModule.TodSpecialRules, enabled);
        }

        chatGui.Print($"RollTracker ToD special rules {(enabled ? "enabled" : "disabled")}.", "RollTracker");
    }

    public void SetWifiEnabled(bool enabled)
    {
        var changed = configuration.WifiEnabled != enabled;
        configuration.WifiEnabled = enabled;
        saveConfiguration();

        if (!enabled)
        {
            pendingWifiMacroSteps.Clear();
        }

        if (changed)
        {
            TriggerModuleStatusEffects(StatusEffectModule.Wifi, enabled);
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

        if (TryHandleChatAlias(sender, message))
        {
            return;
        }

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

        if (configuration.TruthTriggerEnabled && IsTruthTrigger(message))
        {
            SendRandomTodPrompt("Truth", configuration.TruthPromptSets);
            return;
        }

        if (configuration.DareTriggerEnabled && IsDareTrigger(message))
        {
            SendRandomTodPrompt("Dare", configuration.DarePromptSets);
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

        if (!IsRandomNumberLogMessage(logMessage))
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

    private static bool IsRandomNumberLogMessage(ILogMessage logMessage)
    {
        if (!logMessage.GameData.IsValid)
        {
            return false;
        }

        var logKind = logMessage.GameData.Value.LogKind;
        return logKind.IsValid && logKind.RowId == (uint)XivChatType.RandomNumber;
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
        TrackAutoOffLocationState();
        ProcessPendingAutoOn(now);
        TrackAutoOnAddressState();

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

        if (pendingStatusEffectCommands.Count > 0 && now >= pendingStatusEffectCommands.Peek().ExecuteAt)
        {
            ExecuteNextStatusEffectCommand(now);
        }

        if (roundEndsAt is not null && now >= roundEndsAt.Value)
        {
            FinishRoundAndReset();
        }
    }

    private void OnTerritoryChanged(uint territoryType)
    {
        ProcessTerritoryChange(territoryType, forceZoneChange: false);
    }

    private void TrackAutoOffLocationState()
    {
        var territoryType = clientState.TerritoryType;
        var isBetweenAreas = IsBetweenAreas();

        if (territoryType != 0 && territoryType != lastTerritoryType)
        {
            ProcessTerritoryChange(territoryType, forceZoneChange: false);
        }
        else if (territoryType != 0 && wasBetweenAreas && !isBetweenAreas)
        {
            ProcessTerritoryChange(territoryType, forceZoneChange: true);
        }

        wasBetweenAreas = isBetweenAreas;
    }

    private void ProcessTerritoryChange(uint territoryType, bool forceZoneChange)
    {
        if (territoryType == 0)
        {
            return;
        }

        var isInHousingInterior = IsHousingInterior(territoryType);
        var isInResidentialArea = IsResidentialArea(territoryType);
        var territoryChanged = (lastTerritoryType != 0 && lastTerritoryType != territoryType) || forceZoneChange;
        var leftHousingInterior = wasInHousingInterior && !isInHousingInterior;
        var enteredHousingInterior = !wasInHousingInterior && isInHousingInterior;
        var changedHousingInterior = wasInHousingInterior && isInHousingInterior && territoryChanged;
        var housingInteriorMove = leftHousingInterior || enteredHousingInterior || changedHousingInterior;
        var shouldCheckAutoOnForCurrentInterior = enteredHousingInterior || changedHousingInterior;
        var currentAutoOnAddressReliable = false;
        var matchedAutoOnAddress = shouldCheckAutoOnForCurrentInterior
            ? TryEnableAutoOnForCurrentAddress(out currentAutoOnAddressReliable)
            : null;
        var waitingForAutoOnAddress = false;

        if (leftHousingInterior)
        {
            ClearPendingAutoOn();
            lastHousingAddressKey = null;
        }

        if (shouldCheckAutoOnForCurrentInterior &&
            matchedAutoOnAddress is null &&
            !currentAutoOnAddressReliable &&
            ShouldCheckAutoOnAddresses())
        {
            pendingAutoOnCheckUntil = DateTimeOffset.Now.AddSeconds(5);
            pendingAutoOnEnteringAutoOff = enteredHousingInterior && configuration.AutoDisableOnEnteringHousingInterior;
            pendingAutoOnTerritoryChangeAutoOff = changedHousingInterior && configuration.AutoDisableOnTerritoryChange;
            waitingForAutoOnAddress = true;
        }

        var autoOffReason = GetAutoDisableReason(
            territoryChanged,
            forceZoneChange,
            leftHousingInterior,
            enteredHousingInterior,
            changedHousingInterior,
            housingInteriorMove,
            matchedAutoOnAddress is not null || waitingForAutoOnAddress,
            isInResidentialArea);

        if (autoOffReason is not null && HasEnabledModules())
        {
            DisableModulesForAutoOff(autoOffReason);
        }

        lastTerritoryType = territoryType;
        lastAutoOffTransitionWasHousingInteriorMove = housingInteriorMove;
        wasInHousingInterior = isInHousingInterior;
        wasInResidentialArea = isInResidentialArea;
    }

    private void ProcessPendingAutoOn(DateTimeOffset now)
    {
        if (pendingAutoOnCheckUntil is null)
        {
            return;
        }

        if (!IsHousingInterior(clientState.TerritoryType))
        {
            ClearPendingAutoOn();
            lastHousingAddressKey = null;
            return;
        }

        if (TryEnableAutoOnForCurrentAddress() is not null)
        {
            ClearPendingAutoOn();
            return;
        }

        if (now < pendingAutoOnCheckUntil.Value)
        {
            return;
        }

        var shouldRunDeferredEnteringAutoOff =
            pendingAutoOnEnteringAutoOff &&
            configuration.AutoDisableWhenLeavingHousing &&
            configuration.AutoDisableOnEnteringHousingInterior &&
            HasEnabledModules();
        var shouldRunDeferredTerritoryChangeAutoOff =
            pendingAutoOnTerritoryChangeAutoOff &&
            configuration.AutoDisableWhenLeavingHousing &&
            configuration.AutoDisableOnTerritoryChange &&
            HasEnabledModules();

        ClearPendingAutoOn();

        if (shouldRunDeferredEnteringAutoOff)
        {
            DisableModulesForAutoOff("you entered the house");
        }
        else if (shouldRunDeferredTerritoryChangeAutoOff)
        {
            DisableModulesForAutoOff("you changed territory");
        }
    }

    private void ClearPendingAutoOn()
    {
        pendingAutoOnCheckUntil = null;
        pendingAutoOnEnteringAutoOff = false;
        pendingAutoOnTerritoryChangeAutoOff = false;
    }

    private void TrackAutoOnAddressState()
    {
        if (!IsHousingInterior(clientState.TerritoryType))
        {
            lastHousingAddressKey = null;
            return;
        }

        if (!TryCreateCurrentHousingAddressEntry(string.Empty, out var currentAddress, out _))
        {
            return;
        }

        var currentAddressKey = GetHousingAddressKey(currentAddress);
        if (string.Equals(lastHousingAddressKey, currentAddressKey, StringComparison.Ordinal))
        {
            return;
        }

        lastHousingAddressKey = currentAddressKey;
        if (!ShouldCheckAutoOnAddresses())
        {
            return;
        }

        var matchedAddress = GetAutoOnAddressMatch(currentAddress);
        if (matchedAddress is not null)
        {
            EnableModulesForAutoOn(matchedAddress);
        }
    }

    private bool IsBetweenAreas()
    {
        return condition[ConditionFlag.BetweenAreas] || condition[ConditionFlag.BetweenAreas51];
    }

    private bool IsHousingInterior(uint territoryType)
    {
        return housingInteriorTerritoryIds.Contains(territoryType);
    }

    private bool IsResidentialArea(uint territoryType)
    {
        return IsHousingInterior(territoryType) || residentialTerritoryIds.Contains(territoryType);
    }

    private string? GetAutoDisableReason(
        bool territoryChanged,
        bool forceZoneChange,
        bool leftHousingInterior,
        bool enteredHousingInterior,
        bool changedHousingInterior,
        bool housingInteriorMove,
        bool suppressAutoOffForAutoOnAddress,
        bool isInResidentialArea)
    {
        if (!configuration.AutoDisableWhenLeavingHousing)
        {
            return null;
        }

        if (configuration.AutoDisableOnLeavingHousingInterior && leftHousingInterior)
        {
            return "you left the house";
        }

        if (configuration.AutoDisableOnEnteringHousingInterior && enteredHousingInterior && !suppressAutoOffForAutoOnAddress)
        {
            return "you entered the house";
        }

        if (configuration.AutoDisableOnLeavingResidentialArea && wasInResidentialArea && !isInResidentialArea)
        {
            return "you left the residential area";
        }

        var isGeneralTerritoryChange = territoryChanged &&
            !housingInteriorMove &&
            !(forceZoneChange && lastAutoOffTransitionWasHousingInteriorMove);
        var isUnsuppressedInteriorTerritoryChange = territoryChanged &&
            changedHousingInterior &&
            !suppressAutoOffForAutoOnAddress;

        if (configuration.AutoDisableOnTerritoryChange &&
            (isGeneralTerritoryChange || isUnsuppressedInteriorTerritoryChange))
        {
            return "you changed territory";
        }

        return null;
    }

    private bool ShouldCheckAutoOnAddresses()
    {
        return configuration.AutoEnableWhenEnteringHousing &&
            configuration.AutoOnHousingAddresses is { Count: > 0 } &&
            configuration.AutoOnHousingAddresses.Any(address => address.Enabled);
    }

    private HousingAddressEntry? GetCurrentAutoOnAddressMatch()
    {
        if (!TryCreateCurrentHousingAddressEntry(string.Empty, out var currentAddress, out _))
        {
            return null;
        }

        return GetAutoOnAddressMatch(currentAddress);
    }

    private HousingAddressEntry? GetAutoOnAddressMatch(HousingAddressEntry currentAddress)
    {
        return configuration.AutoOnHousingAddresses.FirstOrDefault(address =>
            address.Enabled &&
            IsSameHousingAddress(address, currentAddress));
    }

    private static string GetHousingAddressKey(HousingAddressEntry address)
    {
        return string.Join(
            ':',
            address.WorldId,
            address.OriginalHouseTerritoryTypeId,
            address.WardIndex,
            address.PlotIndex,
            address.RoomNumber,
            address.IsApartment);
    }

    private HousingAddressEntry? TryEnableAutoOnForCurrentAddress()
    {
        return TryEnableAutoOnForCurrentAddress(out _);
    }

    private HousingAddressEntry? TryEnableAutoOnForCurrentAddress(out bool currentAddressReliable)
    {
        if (!ShouldCheckAutoOnAddresses())
        {
            currentAddressReliable = false;
            return null;
        }

        if (!TryCreateCurrentHousingAddressEntry(string.Empty, out var currentAddress, out _))
        {
            currentAddressReliable = false;
            return null;
        }

        currentAddressReliable = true;
        var matchedAddress = GetAutoOnAddressMatch(currentAddress);
        if (matchedAddress is not null)
        {
            EnableModulesForAutoOn(matchedAddress);
        }

        return matchedAddress;
    }

    private bool HasEnabledModules()
    {
        return (configuration.AutoDisableAffectsTod && configuration.Enabled) ||
            (configuration.AutoDisableAffectsTodSecondPair && configuration.TodSecondPairEnabled) ||
            (configuration.AutoDisableAffectsTodSpecialRules && configuration.TodSpecialRulesEnabled) ||
            (configuration.AutoDisableAffectsTruth && configuration.TruthTriggerEnabled) ||
            (configuration.AutoDisableAffectsDare && configuration.DareTriggerEnabled) ||
            (configuration.AutoDisableAffectsHelp && configuration.HelpTriggerEnabled) ||
            (configuration.AutoDisableAffectsChatAlias && configuration.ChatAliasEnabled) ||
            (configuration.AutoDisableAffectsWifi && configuration.WifiEnabled);
    }

    private void DisableModulesForAutoOff(string reason)
    {
        if (configuration.AutoDisableAffectsTod)
        {
            configuration.Enabled = false;
            TriggerModuleStatusEffects(StatusEffectModule.Tod, false, delayUntilStable: true);
            roundEndsAt = null;
            pendingMacroSteps.Clear();
        }

        if (configuration.AutoDisableAffectsTodSecondPair)
        {
            configuration.TodSecondPairEnabled = false;
            TriggerModuleStatusEffects(StatusEffectModule.TodSecondPair, false, delayUntilStable: true);
        }

        if (configuration.AutoDisableAffectsTodSpecialRules)
        {
            configuration.TodSpecialRulesEnabled = false;
            TriggerModuleStatusEffects(StatusEffectModule.TodSpecialRules, false, delayUntilStable: true);
        }

        if (configuration.AutoDisableAffectsTruth)
        {
            configuration.TruthTriggerEnabled = false;
            ClearDelayedTodPrompts("Truth");
            TriggerModuleStatusEffects(StatusEffectModule.Truth, false, delayUntilStable: true);
        }

        if (configuration.AutoDisableAffectsDare)
        {
            configuration.DareTriggerEnabled = false;
            ClearDelayedTodPrompts("Dare");
            TriggerModuleStatusEffects(StatusEffectModule.Dare, false, delayUntilStable: true);
        }

        if (configuration.AutoDisableAffectsHelp)
        {
            configuration.HelpTriggerEnabled = false;
            ClearDelayedTodPrompts("Help");
            TriggerModuleStatusEffects(StatusEffectModule.Help, false, delayUntilStable: true);
        }

        if (configuration.AutoDisableAffectsChatAlias)
        {
            configuration.ChatAliasEnabled = false;
            TriggerModuleStatusEffects(StatusEffectModule.ChatAlias, false, delayUntilStable: true);
        }

        if (configuration.AutoDisableAffectsWifi)
        {
            configuration.WifiEnabled = false;
            pendingWifiMacroSteps.Clear();
            TriggerModuleStatusEffects(StatusEffectModule.Wifi, false, delayUntilStable: true);
        }

        saveConfiguration();
        chatGui.Print($"RollTracker disabled because {reason}.", "RollTracker");
    }

    private void EnableModulesForAutoOn(HousingAddressEntry address)
    {
        var changed = false;

        if (configuration.AutoEnableAffectsTod && !configuration.Enabled)
        {
            configuration.Enabled = true;
            TriggerModuleStatusEffects(StatusEffectModule.Tod, true, delayUntilStable: true);
            changed = true;
        }

        if (configuration.AutoEnableAffectsTodSecondPair && !configuration.TodSecondPairEnabled)
        {
            configuration.TodSecondPairEnabled = true;
            TriggerModuleStatusEffects(StatusEffectModule.TodSecondPair, true, delayUntilStable: true);
            changed = true;
        }

        if (configuration.AutoEnableAffectsTodSpecialRules && !configuration.TodSpecialRulesEnabled)
        {
            configuration.TodSpecialRulesEnabled = true;
            TriggerModuleStatusEffects(StatusEffectModule.TodSpecialRules, true, delayUntilStable: true);
            changed = true;
        }

        if (configuration.AutoEnableAffectsTruth && !configuration.TruthTriggerEnabled)
        {
            configuration.TruthTriggerEnabled = true;
            TriggerModuleStatusEffects(StatusEffectModule.Truth, true, delayUntilStable: true);
            changed = true;
        }

        if (configuration.AutoEnableAffectsDare && !configuration.DareTriggerEnabled)
        {
            configuration.DareTriggerEnabled = true;
            TriggerModuleStatusEffects(StatusEffectModule.Dare, true, delayUntilStable: true);
            changed = true;
        }

        if (configuration.AutoEnableAffectsHelp && !configuration.HelpTriggerEnabled)
        {
            configuration.HelpTriggerEnabled = true;
            TriggerModuleStatusEffects(StatusEffectModule.Help, true, delayUntilStable: true);
            changed = true;
        }

        if (configuration.AutoEnableAffectsChatAlias && !configuration.ChatAliasEnabled)
        {
            configuration.ChatAliasEnabled = true;
            TriggerModuleStatusEffects(StatusEffectModule.ChatAlias, true, delayUntilStable: true);
            changed = true;
        }

        if (configuration.AutoEnableAffectsWifi && !configuration.WifiEnabled)
        {
            configuration.WifiEnabled = true;
            TriggerModuleStatusEffects(StatusEffectModule.Wifi, true, delayUntilStable: true);
            changed = true;
        }

        if (!changed)
        {
            return;
        }

        saveConfiguration();
        chatGui.Print($"RollTracker enabled for saved address: {address.Name}.", "RollTracker");
    }

    private List<StatusEffectModule> GetChangedStatusEffectModulesForAllToggle(bool enabled)
    {
        List<StatusEffectModule> modules = [];
        if (configuration.Enabled != enabled)
        {
            modules.Add(StatusEffectModule.Tod);
        }

        if (configuration.TodSecondPairEnabled != enabled)
        {
            modules.Add(StatusEffectModule.TodSecondPair);
        }

        if (configuration.TodSpecialRulesEnabled != enabled)
        {
            modules.Add(StatusEffectModule.TodSpecialRules);
        }

        if (configuration.TruthTriggerEnabled != enabled)
        {
            modules.Add(StatusEffectModule.Truth);
        }

        if (configuration.DareTriggerEnabled != enabled)
        {
            modules.Add(StatusEffectModule.Dare);
        }

        if (configuration.HelpTriggerEnabled != enabled)
        {
            modules.Add(StatusEffectModule.Help);
        }

        if (configuration.ChatAliasEnabled != enabled)
        {
            modules.Add(StatusEffectModule.ChatAlias);
        }

        if (configuration.WifiEnabled != enabled)
        {
            modules.Add(StatusEffectModule.Wifi);
        }

        return modules;
    }

    private void TriggerModuleStatusEffects(IEnumerable<StatusEffectModule> modules, bool enabled, bool delayUntilStable = false)
    {
        configuration.ModuleStatusEffects ??= [];
        configuration.ModuleStatusMacros ??= [];
        var moduleList = modules.ToList();
        foreach (var effect in configuration.ModuleStatusEffects.Where(effect =>
            ShouldHandleStatusEffect(effect, enabled) &&
            moduleList.Any(module => IsStatusEffectTriggeredByModule(effect, module))))
        {
            TriggerStatusEffect(effect, enabled, moduleList, delayUntilStable);
        }

        if (configuration.ModuleStatusEffects.Any(effect =>
            effect.UseHonorific &&
            moduleList.Any(module => IsStatusEffectTriggeredByModule(effect, module))))
        {
            SyncHonorificStatusEffects(delayUntilStable);
        }

        foreach (var macro in configuration.ModuleStatusMacros.Where(macro =>
            ShouldHandleStatusMacro(macro, enabled) &&
            moduleList.Any(module => IsStatusMacroTriggeredByModule(macro, module))))
        {
            TriggerStatusMacro(macro, enabled, moduleList, delayUntilStable);
        }
    }

    private void TriggerModuleStatusEffects(StatusEffectModule module, bool enabled, bool delayUntilStable = false)
    {
        configuration.ModuleStatusEffects ??= [];
        configuration.ModuleStatusMacros ??= [];
        foreach (var effect in configuration.ModuleStatusEffects.Where(effect => ShouldHandleStatusEffect(effect, enabled) && IsStatusEffectTriggeredByModule(effect, module)))
        {
            TriggerStatusEffect(effect, enabled, [module], delayUntilStable);
        }

        if (configuration.ModuleStatusEffects.Any(effect => effect.UseHonorific && IsStatusEffectTriggeredByModule(effect, module)))
        {
            SyncHonorificStatusEffects(delayUntilStable);
        }

        foreach (var macro in configuration.ModuleStatusMacros.Where(macro => ShouldHandleStatusMacro(macro, enabled) && IsStatusMacroTriggeredByModule(macro, module)))
        {
            TriggerStatusMacro(macro, enabled, [module], delayUntilStable);
        }
    }

    private void TriggerStatusEffect(ModuleStatusEffect effect, bool enabled, IReadOnlyCollection<StatusEffectModule> triggeringModules, bool delayUntilStable)
    {
        if (!effect.UseMoodle || string.IsNullOrWhiteSpace(effect.MoodleName))
        {
            return;
        }

        if (enabled && effect.IsApplied)
        {
            return;
        }

        if (!enabled && !effect.IsApplied)
        {
            return;
        }

        if (enabled && HasAnySelectedStatusEffectModuleEnabledOutside(effect, triggeringModules))
        {
            return;
        }

        if (!enabled && HasAnySelectedStatusEffectModuleEnabled(effect))
        {
            return;
        }

        var action = enabled ? "apply" : "remove";
        ExecuteStatusEffectCommand($"/moodle {action} self moodle {QuoteCommandArgument(effect.MoodleName)}", delayUntilStable);

        effect.IsApplied = enabled;
        saveConfiguration();
    }

    private static bool ShouldHandleStatusEffect(ModuleStatusEffect effect, bool enabled)
    {
        return enabled ? effect.Enabled : effect.Enabled || effect.IsApplied || effect.HonorificIsApplied;
    }

    public void DisableStatusEffectEntry(int index)
    {
        configuration.ModuleStatusEffects ??= [];
        if (index < 0 || index >= configuration.ModuleStatusEffects.Count)
        {
            return;
        }

        var effect = configuration.ModuleStatusEffects[index];
        effect.Enabled = false;

        if (effect.UseMoodle && effect.IsApplied && !string.IsNullOrWhiteSpace(effect.MoodleName))
        {
            ExecuteStatusEffectCommand($"/moodle remove self moodle {QuoteCommandArgument(effect.MoodleName)}", delayUntilStable: false);
            effect.IsApplied = false;
        }

        configuration.ModuleStatusEffects[index] = effect;
        if (effect.UseHonorific)
        {
            SyncHonorificStatusEffects(delayUntilStable: false);
        }
        else
        {
            saveConfiguration();
        }
    }

    public void EnableStatusEffectEntry(int index)
    {
        configuration.ModuleStatusEffects ??= [];
        if (index < 0 || index >= configuration.ModuleStatusEffects.Count)
        {
            return;
        }

        var effect = configuration.ModuleStatusEffects[index];
        effect.Enabled = true;

        if (effect.UseMoodle &&
            !effect.IsApplied &&
            !string.IsNullOrWhiteSpace(effect.MoodleName) &&
            HasAnySelectedStatusEffectModuleEnabled(effect))
        {
            ExecuteStatusEffectCommand($"/moodle apply self moodle {QuoteCommandArgument(effect.MoodleName)}", delayUntilStable: false);
            effect.IsApplied = true;
        }

        configuration.ModuleStatusEffects[index] = effect;
        if (effect.UseHonorific)
        {
            SyncHonorificStatusEffects(delayUntilStable: false);
        }
        else
        {
            saveConfiguration();
        }
    }

    public void ClearActiveStatusEffectsForShutdown()
    {
        configuration.ModuleStatusEffects ??= [];
        configuration.ModuleStatusMacros ??= [];
        var changed = false;
        var clearHonorific = false;

        for (var i = 0; i < configuration.ModuleStatusEffects.Count; i++)
        {
            var effect = configuration.ModuleStatusEffects[i];
            if (effect.UseMoodle && effect.IsApplied && !string.IsNullOrWhiteSpace(effect.MoodleName))
            {
                ExecuteStatusEffectCommand($"/moodle remove self moodle {QuoteCommandArgument(effect.MoodleName)}", delayUntilStable: false);
                effect.IsApplied = false;
                changed = true;
            }

            if (effect.UseHonorific && (effect.HonorificIsApplied || (!effect.UseMoodle && effect.IsApplied)))
            {
                effect.HonorificIsApplied = false;
                if (!effect.UseMoodle)
                {
                    effect.IsApplied = false;
                }

                clearHonorific = true;
                changed = true;
            }

            configuration.ModuleStatusEffects[i] = effect;
        }

        for (var i = 0; i < configuration.ModuleStatusMacros.Count; i++)
        {
            var macro = configuration.ModuleStatusMacros[i];
            if (!macro.IsApplied)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(macro.DisableMacroText))
            {
                ExecuteStatusEffectMacro(macro.DisableMacroText, delayUntilStable: false);
            }

            macro.IsApplied = false;
            configuration.ModuleStatusMacros[i] = macro;
            changed = true;
        }

        if (clearHonorific)
        {
            ExecuteStatusEffectCommand("/honorific force clear", delayUntilStable: false);
        }

        pendingStatusEffectCommands.Clear();

        if (changed)
        {
            saveConfiguration();
        }
    }

    private void SyncHonorificStatusEffects(bool delayUntilStable)
    {
        var currentIndex = GetCurrentHonorificStatusEffectIndex();
        var desiredIndex = GetDesiredHonorificStatusEffectIndex();
        if (currentIndex == desiredIndex)
        {
            return;
        }

        if (desiredIndex >= 0)
        {
            ExecuteStatusEffectCommand(BuildHonorificSetCommand(configuration.ModuleStatusEffects[desiredIndex]), delayUntilStable);
        }
        else if (currentIndex >= 0)
        {
            ExecuteStatusEffectCommand("/honorific force clear", delayUntilStable);
        }
        else
        {
            return;
        }

        for (var i = 0; i < configuration.ModuleStatusEffects.Count; i++)
        {
            var effect = configuration.ModuleStatusEffects[i];
            if (!effect.UseHonorific)
            {
                continue;
            }

            effect.HonorificIsApplied = i == desiredIndex;
            if (!effect.UseMoodle)
            {
                effect.IsApplied = false;
            }

            configuration.ModuleStatusEffects[i] = effect;
        }

        saveConfiguration();
    }

    private int GetCurrentHonorificStatusEffectIndex()
    {
        for (var i = 0; i < configuration.ModuleStatusEffects.Count; i++)
        {
            var effect = configuration.ModuleStatusEffects[i];
            if (effect.UseHonorific && (effect.HonorificIsApplied || (!effect.UseMoodle && effect.IsApplied)))
            {
                return i;
            }
        }

        return -1;
    }

    private int GetDesiredHonorificStatusEffectIndex()
    {
        var desiredIndex = -1;
        var desiredPriority = int.MaxValue;
        for (var i = 0; i < configuration.ModuleStatusEffects.Count; i++)
        {
            var effect = configuration.ModuleStatusEffects[i];
            if (!effect.Enabled ||
                !effect.UseHonorific ||
                string.IsNullOrWhiteSpace(effect.HonorificTitle) ||
                !HasAnySelectedStatusEffectModuleEnabled(effect))
            {
                continue;
            }

            var priority = Math.Max(1, effect.HonorificPriority);
            if (priority < desiredPriority)
            {
                desiredPriority = priority;
                desiredIndex = i;
            }
        }

        return desiredIndex;
    }

    private void TriggerStatusMacro(ModuleStatusMacro macro, bool enabled, IReadOnlyCollection<StatusEffectModule> triggeringModules, bool delayUntilStable)
    {
        var macroText = enabled ? macro.EnableMacroText : macro.DisableMacroText;
        if (string.IsNullOrWhiteSpace(macroText))
        {
            return;
        }

        if (enabled && macro.IsApplied)
        {
            return;
        }

        if (!enabled && !macro.IsApplied)
        {
            return;
        }

        if (enabled && HasAnySelectedStatusMacroModuleEnabledOutside(macro, triggeringModules))
        {
            return;
        }

        if (!enabled && HasAnySelectedStatusMacroModuleEnabled(macro))
        {
            return;
        }

        ExecuteStatusEffectMacro(macroText, delayUntilStable);
        macro.IsApplied = enabled;
        saveConfiguration();
    }

    private static bool ShouldHandleStatusMacro(ModuleStatusMacro macro, bool enabled)
    {
        return enabled ? macro.Enabled : macro.Enabled || macro.IsApplied;
    }

    public void DisableStatusMacroEntry(int index)
    {
        configuration.ModuleStatusMacros ??= [];
        if (index < 0 || index >= configuration.ModuleStatusMacros.Count)
        {
            return;
        }

        var macro = configuration.ModuleStatusMacros[index];
        macro.Enabled = false;

        if (macro.IsApplied)
        {
            if (!string.IsNullOrWhiteSpace(macro.DisableMacroText))
            {
                ExecuteStatusEffectMacro(macro.DisableMacroText, delayUntilStable: false);
            }

            macro.IsApplied = false;
        }

        configuration.ModuleStatusMacros[index] = macro;
        saveConfiguration();
    }

    public void EnableStatusMacroEntry(int index)
    {
        configuration.ModuleStatusMacros ??= [];
        if (index < 0 || index >= configuration.ModuleStatusMacros.Count)
        {
            return;
        }

        var macro = configuration.ModuleStatusMacros[index];
        macro.Enabled = true;

        if (!macro.IsApplied &&
            !string.IsNullOrWhiteSpace(macro.EnableMacroText) &&
            HasAnySelectedStatusMacroModuleEnabled(macro))
        {
            ExecuteStatusEffectMacro(macro.EnableMacroText, delayUntilStable: false);
            macro.IsApplied = true;
        }

        configuration.ModuleStatusMacros[index] = macro;
        saveConfiguration();
    }

    private void ExecuteStatusEffectCommand(string command, bool delayUntilStable)
    {
        if (delayUntilStable)
        {
            pendingStatusEffectCommands.Enqueue(new DelayedCommand(
                command,
                "status effect",
                DateTimeOffset.Now.AddMilliseconds(AutoStatusEffectDelayMilliseconds)));
            return;
        }

        if (!TryExecuteTextCommand(command))
        {
            chatGui.PrintError($"Could not run status effect command: {command}", "RollTracker");
        }
    }

    private void ExecuteStatusEffectMacro(string macroText, bool delayUntilStable)
    {
        var nextExecuteAt = DateTimeOffset.Now.AddMilliseconds(delayUntilStable ? AutoStatusEffectDelayMilliseconds : 0);
        var lines = macroText
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var step = ParseMacroStep(line);
            if (step.WaitMilliseconds > 0)
            {
                nextExecuteAt = nextExecuteAt.AddMilliseconds(step.WaitMilliseconds);
                continue;
            }

            if (delayUntilStable)
            {
                pendingStatusEffectCommands.Enqueue(new DelayedCommand(step.Command, "status effect macro", nextExecuteAt));
            }
            else
            {
                ExecuteStatusEffectCommand(step.Command, delayUntilStable: false);
            }

            nextExecuteAt = nextExecuteAt.AddMilliseconds(ClampMacroLineDelay(configuration.MacroLineDelayMilliseconds));
        }
    }

    private void ExecuteNextStatusEffectCommand(DateTimeOffset now)
    {
        if (IsBetweenAreas())
        {
            return;
        }

        var delayedCommand = pendingStatusEffectCommands.Dequeue();
        if (!TryExecuteTextCommand(delayedCommand.Command))
        {
            chatGui.PrintError($"Could not run status effect command: {delayedCommand.Command}", "RollTracker");
        }
    }

    private static string BuildHonorificSetCommand(ModuleStatusEffect effect)
    {
        var command = new StringBuilder($"/honorific force set {FormatHonorificTitle(effect.HonorificTitle)}");
        var position = NormalizeHonorificPosition(effect.HonorificPosition);
        if (!string.IsNullOrWhiteSpace(position))
        {
            command.Append(" | ").Append(position);
        }

        if (!string.IsNullOrWhiteSpace(effect.HonorificColor))
        {
            command.Append(" | ").Append(NormalizeHexColor(effect.HonorificColor));
        }

        if (!string.IsNullOrWhiteSpace(effect.HonorificGlow))
        {
            command.Append(" | ").Append(NormalizeHexColor(effect.HonorificGlow));
        }

        return command.ToString();
    }

    private bool HasAnySelectedStatusEffectModuleEnabled(ModuleStatusEffect effect)
    {
        return (effect.TriggerOnTod && configuration.Enabled) ||
            (effect.TriggerOnTodSecondPair && configuration.TodSecondPairEnabled) ||
            (effect.TriggerOnTodSpecialRules && configuration.TodSpecialRulesEnabled) ||
            (effect.TriggerOnTruth && configuration.TruthTriggerEnabled) ||
            (effect.TriggerOnDare && configuration.DareTriggerEnabled) ||
            (effect.TriggerOnHelp && configuration.HelpTriggerEnabled) ||
            (effect.TriggerOnChatAlias && configuration.ChatAliasEnabled) ||
            (effect.TriggerOnWifi && configuration.WifiEnabled);
    }

    private bool HasAnySelectedStatusEffectModuleEnabledOutside(ModuleStatusEffect effect, IReadOnlyCollection<StatusEffectModule> excludedModules)
    {
        return (effect.TriggerOnTod && !excludedModules.Contains(StatusEffectModule.Tod) && configuration.Enabled) ||
            (effect.TriggerOnTodSecondPair && !excludedModules.Contains(StatusEffectModule.TodSecondPair) && configuration.TodSecondPairEnabled) ||
            (effect.TriggerOnTodSpecialRules && !excludedModules.Contains(StatusEffectModule.TodSpecialRules) && configuration.TodSpecialRulesEnabled) ||
            (effect.TriggerOnTruth && !excludedModules.Contains(StatusEffectModule.Truth) && configuration.TruthTriggerEnabled) ||
            (effect.TriggerOnDare && !excludedModules.Contains(StatusEffectModule.Dare) && configuration.DareTriggerEnabled) ||
            (effect.TriggerOnHelp && !excludedModules.Contains(StatusEffectModule.Help) && configuration.HelpTriggerEnabled) ||
            (effect.TriggerOnChatAlias && !excludedModules.Contains(StatusEffectModule.ChatAlias) && configuration.ChatAliasEnabled) ||
            (effect.TriggerOnWifi && !excludedModules.Contains(StatusEffectModule.Wifi) && configuration.WifiEnabled);
    }

    private bool HasAnySelectedStatusMacroModuleEnabled(ModuleStatusMacro macro)
    {
        return (macro.TriggerOnTod && configuration.Enabled) ||
            (macro.TriggerOnTodSecondPair && configuration.TodSecondPairEnabled) ||
            (macro.TriggerOnTodSpecialRules && configuration.TodSpecialRulesEnabled) ||
            (macro.TriggerOnTruth && configuration.TruthTriggerEnabled) ||
            (macro.TriggerOnDare && configuration.DareTriggerEnabled) ||
            (macro.TriggerOnHelp && configuration.HelpTriggerEnabled) ||
            (macro.TriggerOnChatAlias && configuration.ChatAliasEnabled) ||
            (macro.TriggerOnWifi && configuration.WifiEnabled);
    }

    private bool HasAnySelectedStatusMacroModuleEnabledOutside(ModuleStatusMacro macro, IReadOnlyCollection<StatusEffectModule> excludedModules)
    {
        return (macro.TriggerOnTod && !excludedModules.Contains(StatusEffectModule.Tod) && configuration.Enabled) ||
            (macro.TriggerOnTodSecondPair && !excludedModules.Contains(StatusEffectModule.TodSecondPair) && configuration.TodSecondPairEnabled) ||
            (macro.TriggerOnTodSpecialRules && !excludedModules.Contains(StatusEffectModule.TodSpecialRules) && configuration.TodSpecialRulesEnabled) ||
            (macro.TriggerOnTruth && !excludedModules.Contains(StatusEffectModule.Truth) && configuration.TruthTriggerEnabled) ||
            (macro.TriggerOnDare && !excludedModules.Contains(StatusEffectModule.Dare) && configuration.DareTriggerEnabled) ||
            (macro.TriggerOnHelp && !excludedModules.Contains(StatusEffectModule.Help) && configuration.HelpTriggerEnabled) ||
            (macro.TriggerOnChatAlias && !excludedModules.Contains(StatusEffectModule.ChatAlias) && configuration.ChatAliasEnabled) ||
            (macro.TriggerOnWifi && !excludedModules.Contains(StatusEffectModule.Wifi) && configuration.WifiEnabled);
    }

    private static string FormatHonorificTitle(string title)
    {
        return title.Trim().Trim('"');
    }

    private static string NormalizeHonorificPosition(string position)
    {
        return position.Trim().ToLowerInvariant() switch
        {
            "prefix" => "prefix",
            "suffix" => "suffix",
            _ => string.Empty,
        };
    }

    private static string NormalizeHexColor(string color)
    {
        var trimmed = color.Trim();
        return trimmed.StartsWith('#') ? trimmed : $"#{trimmed}";
    }

    private static string QuoteCommandArgument(string value)
    {
        return $"\"{value.Trim().Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
    }

    private static bool IsStatusEffectTriggeredByModule(ModuleStatusEffect effect, StatusEffectModule module)
    {
        return module switch
        {
            StatusEffectModule.Tod => effect.TriggerOnTod,
            StatusEffectModule.TodSecondPair => effect.TriggerOnTodSecondPair,
            StatusEffectModule.TodSpecialRules => effect.TriggerOnTodSpecialRules,
            StatusEffectModule.Truth => effect.TriggerOnTruth,
            StatusEffectModule.Dare => effect.TriggerOnDare,
            StatusEffectModule.Help => effect.TriggerOnHelp,
            StatusEffectModule.ChatAlias => effect.TriggerOnChatAlias,
            StatusEffectModule.Wifi => effect.TriggerOnWifi,
            _ => false,
        };
    }

    private static bool IsStatusMacroTriggeredByModule(ModuleStatusMacro macro, StatusEffectModule module)
    {
        return module switch
        {
            StatusEffectModule.Tod => macro.TriggerOnTod,
            StatusEffectModule.TodSecondPair => macro.TriggerOnTodSecondPair,
            StatusEffectModule.TodSpecialRules => macro.TriggerOnTodSpecialRules,
            StatusEffectModule.Truth => macro.TriggerOnTruth,
            StatusEffectModule.Dare => macro.TriggerOnDare,
            StatusEffectModule.Help => macro.TriggerOnHelp,
            StatusEffectModule.ChatAlias => macro.TriggerOnChatAlias,
            StatusEffectModule.Wifi => macro.TriggerOnWifi,
            _ => false,
        };
    }

    private static HashSet<uint> CreateDefaultResidentialTerritoryIds()
    {
        return
        [
            339,
            340,
            341,
            641,
            979,
        ];
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

        nextMacroStepAt = DateTimeOffset.Now.AddMilliseconds(GetCurrentTodMacroLineDelayMilliseconds());
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

        nextWifiMacroStepAt = DateTimeOffset.Now.AddMilliseconds(ClampMacroLineDelay(configuration.WifiMacroLineDelayMilliseconds));
    }

    private int GetCurrentTodMacroLineDelayMilliseconds()
    {
        return currentRoundKind == RoundKind.SecondPair
            ? ClampMacroLineDelay(configuration.TodSecondPairMacroLineDelayMilliseconds)
            : ClampMacroLineDelay(configuration.MacroLineDelayMilliseconds);
    }

    private static int ClampMacroLineDelay(int delayMilliseconds)
    {
        return Math.Clamp(delayMilliseconds <= 0 ? 1500 : delayMilliseconds, 100, 10000);
    }

    private void ExecuteNextTodPromptCommand()
    {
        var delayedCommand = pendingTodPromptCommands.Dequeue();
        if (!TryExecuteTextCommand(delayedCommand.Command))
        {
            chatGui.PrintError($"Could not send {delayedCommand.PromptType}.", "RollTracker");
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

    private void SendRandomTodPrompt(string promptType, IReadOnlyList<TodPromptSet> promptSets)
    {
        var usablePrompts = promptSets
            .Where(promptSet => promptSet.Enabled)
            .SelectMany(promptSet => promptSet.Prompts)
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
                DateTimeOffset.Now.AddMilliseconds(
                    Math.Clamp(configuration.HelpInitialDelayMilliseconds, 0, 10000) +
                    (i * ClampMacroLineDelay(configuration.HelpLineDelayMilliseconds)))));
        }
    }

    private bool TryHandleChatAlias(string sender, string message)
    {
        if (string.IsNullOrWhiteSpace(configuration.ChatAliasWord) ||
            configuration.ChatAliasCommands.Count == 0)
        {
            return false;
        }

        var aliasWord = configuration.ChatAliasWord.Trim();
        if (!message.StartsWith(aliasWord, StringComparison.OrdinalIgnoreCase) ||
            message.Length <= aliasWord.Length ||
            !char.IsWhiteSpace(message[aliasWord.Length]))
        {
            return false;
        }

        var requestedAlias = NormalizeChatAliasTrigger(message[aliasWord.Length..]);
        var aliasCommand = configuration.ChatAliasCommands.FirstOrDefault(command =>
            command.Enabled &&
            NormalizeChatAliasTrigger(command.TriggerText).Equals(requestedAlias, StringComparison.OrdinalIgnoreCase));
        if (aliasCommand is null)
        {
            return false;
        }

        if (!configuration.ChatAliasEnabled &&
            (!configuration.ChatAliasAllowEnableWhenDisabled || !IsChatAliasEnableCommand(aliasCommand.RtCommandArgs)))
        {
            return false;
        }

        if (ExecuteChatAliasCommand(aliasCommand.RtCommandArgs, string.IsNullOrWhiteSpace(sender) ? "Unknown" : sender) &&
            aliasCommand.FeedbackEnabled &&
            TryBuildChatAliasFeedbackMessage(aliasCommand.RtCommandArgs, out var feedbackMessage))
        {
            var feedbackCommand = $"{GetChatCommand(configuration.ChatAliasFeedbackChatChannel)} {feedbackMessage}";
            pendingTodPromptCommands.Enqueue(new DelayedCommand(
                feedbackCommand,
                "Chat alias feedback",
                DateTimeOffset.Now.AddMilliseconds(ChatAliasFeedbackDelayMilliseconds)));
        }

        return true;
    }

    private static string NormalizeChatAliasTrigger(string trigger)
    {
        trigger = trigger.Trim();
        if (trigger.StartsWith("/", StringComparison.Ordinal))
        {
            trigger = trigger[1..].TrimStart();
        }

        return Regex.Replace(trigger, @"\s+", " ");
    }

    private static bool IsChatAliasEnableCommand(string args)
    {
        args = args.Trim();
        return args.Equals("on", StringComparison.OrdinalIgnoreCase) ||
            args.Equals("on all", StringComparison.OrdinalIgnoreCase) ||
            args.Equals("all on", StringComparison.OrdinalIgnoreCase) ||
            args.Equals("on alias", StringComparison.OrdinalIgnoreCase) ||
            args.Equals("alias on", StringComparison.OrdinalIgnoreCase) ||
            args.Equals("toggle", StringComparison.OrdinalIgnoreCase) ||
            args.Equals("toggle all", StringComparison.OrdinalIgnoreCase) ||
            args.Equals("all toggle", StringComparison.OrdinalIgnoreCase) ||
            args.Equals("toggel", StringComparison.OrdinalIgnoreCase) ||
            args.Equals("toggel all", StringComparison.OrdinalIgnoreCase) ||
            args.Equals("all toggel", StringComparison.OrdinalIgnoreCase) ||
            args.Equals("toggle alias", StringComparison.OrdinalIgnoreCase) ||
            args.Equals("alias toggle", StringComparison.OrdinalIgnoreCase) ||
            args.Equals("toggel alias", StringComparison.OrdinalIgnoreCase) ||
            args.Equals("alias toggel", StringComparison.OrdinalIgnoreCase);
    }

    private bool ExecuteChatAliasCommand(string args, string sender)
    {
        var normalizedArgs = args.Trim();
        chatGui.Print($"Chat alias from {sender}: /rt {normalizedArgs}", "RollTracker");

        if (TryExecuteModuleToggle(normalizedArgs, true) ||
            TryExecuteModuleToggle(normalizedArgs, false) ||
            TryExecuteModuleToggleSwitch(normalizedArgs) ||
            TryExecuteReversedModuleToggle(normalizedArgs))
        {
            return true;
        }

        if (normalizedArgs.Equals("reset", StringComparison.OrdinalIgnoreCase) ||
            normalizedArgs.Equals("clear", StringComparison.OrdinalIgnoreCase))
        {
            Reset();
            chatGui.Print("Roll list reset.", "RollTracker");
            return true;
        }

        if (normalizedArgs.Equals("end", StringComparison.OrdinalIgnoreCase))
        {
            FinishRoundAndReset();
            return true;
        }

        if (normalizedArgs.Equals("test", StringComparison.OrdinalIgnoreCase))
        {
            AddTestRolls();
            return true;
        }

        return false;
    }

    private bool TryBuildChatAliasFeedbackMessage(string args, out string message)
    {
        var target = GetModuleTargetFromToggleCommand(args.Trim());
        if (target.Length == 0 || target.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            message = HasAnyModuleEnabled()
                ? "RollTracker modules have been enabled."
                : "RollTracker modules have been disabled.";
            return true;
        }

        var moduleName = GetModuleFeedbackName(target);
        if (moduleName.Length == 0)
        {
            message = string.Empty;
            return false;
        }

        message = $"{moduleName} has been {(IsModuleTargetEnabled(target) ? "enabled" : "disabled")}.";
        return true;
    }

    private static string GetModuleTargetFromToggleCommand(string args)
    {
        if (args.Equals("on", StringComparison.OrdinalIgnoreCase) ||
            args.Equals("off", StringComparison.OrdinalIgnoreCase) ||
            IsToggleWord(args))
        {
            return string.Empty;
        }

        if (args.StartsWith("on ", StringComparison.OrdinalIgnoreCase))
        {
            return args["on ".Length..].Trim();
        }

        if (args.StartsWith("off ", StringComparison.OrdinalIgnoreCase))
        {
            return args["off ".Length..].Trim();
        }

        if (args.StartsWith("toggle ", StringComparison.OrdinalIgnoreCase))
        {
            return args["toggle ".Length..].Trim();
        }

        if (args.StartsWith("toggel ", StringComparison.OrdinalIgnoreCase))
        {
            return args["toggel ".Length..].Trim();
        }

        var parts = args.Split(' ', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2 &&
            (parts[1].Equals("on", StringComparison.OrdinalIgnoreCase) ||
             parts[1].Equals("off", StringComparison.OrdinalIgnoreCase) ||
             IsToggleWord(parts[1])))
        {
            return parts[0];
        }

        return string.Empty;
    }

    private static string GetModuleFeedbackName(string target)
    {
        return target.Trim().ToLowerInvariant() switch
        {
            "all" => string.Empty,
            "tod" => "Truth or Dare module",
            "todrules" or "tod rules" or "special" => "Truth or Dare special rules",
            "todsecond" or "tod second" or "second" => "Truth or Dare doubles module",
            "truth" or "!truth" => "Truth prompt module",
            "dare" or "!dare" => "Dare prompt module",
            "help" or "!help" => "Command help module",
            "alias" or "chat alias" => "Chat alias module",
            "wifi" => "Wifi module",
            _ => string.Empty,
        };
    }

    private bool IsModuleTargetEnabled(string target)
    {
        return target.Trim().ToLowerInvariant() switch
        {
            "tod" => configuration.Enabled,
            "todrules" or "tod rules" or "special" => configuration.TodSpecialRulesEnabled,
            "todsecond" or "tod second" or "second" => configuration.TodSecondPairEnabled,
            "truth" or "!truth" => configuration.TruthTriggerEnabled,
            "dare" or "!dare" => configuration.DareTriggerEnabled,
            "help" or "!help" => configuration.HelpTriggerEnabled,
            "alias" or "chat alias" => configuration.ChatAliasEnabled,
            "wifi" => configuration.WifiEnabled,
            _ => false,
        };
    }

    private bool TryExecuteModuleToggle(string args, bool enabled)
    {
        var command = enabled ? "on" : "off";
        if (!args.StartsWith(command, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var target = args[command.Length..].Trim();
        if (target.Length == 0 || target.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            SetAllModulesEnabled(enabled);
            return true;
        }

        if (target.Equals("tod", StringComparison.OrdinalIgnoreCase))
        {
            SetEnabled(enabled);
            return true;
        }

        if (target.Equals("todrules", StringComparison.OrdinalIgnoreCase) ||
            target.Equals("tod rules", StringComparison.OrdinalIgnoreCase) ||
            target.Equals("special", StringComparison.OrdinalIgnoreCase))
        {
            var changed = configuration.TodSpecialRulesEnabled != enabled;
            configuration.TodSpecialRulesEnabled = enabled;
            saveConfiguration();
            if (changed)
            {
                TriggerModuleStatusEffects(StatusEffectModule.TodSpecialRules, enabled);
            }

            chatGui.Print($"RollTracker ToD special rules {(enabled ? "enabled" : "disabled")}.", "RollTracker");
            return true;
        }

        if (target.Equals("todsecond", StringComparison.OrdinalIgnoreCase) ||
            target.Equals("tod second", StringComparison.OrdinalIgnoreCase) ||
            target.Equals("second", StringComparison.OrdinalIgnoreCase))
        {
            SetSecondPairEnabled(enabled);
            return true;
        }

        if (target.Equals("truth", StringComparison.OrdinalIgnoreCase) ||
            target.Equals("!truth", StringComparison.OrdinalIgnoreCase))
        {
            SetTruthTriggerEnabled(enabled);
            return true;
        }

        if (target.Equals("dare", StringComparison.OrdinalIgnoreCase) ||
            target.Equals("!dare", StringComparison.OrdinalIgnoreCase))
        {
            SetDareTriggerEnabled(enabled);
            return true;
        }

        if (target.Equals("help", StringComparison.OrdinalIgnoreCase) ||
            target.Equals("!help", StringComparison.OrdinalIgnoreCase))
        {
            SetHelpTriggerEnabled(enabled);
            return true;
        }

        if (target.Equals("alias", StringComparison.OrdinalIgnoreCase))
        {
            SetChatAliasEnabled(enabled);
            return true;
        }

        if (target.Equals("wifi", StringComparison.OrdinalIgnoreCase))
        {
            SetWifiEnabled(enabled);
            return true;
        }

        return false;
    }

    private bool TryExecuteModuleToggleSwitch(string args)
    {
        if (IsToggleWord(args))
        {
            SetAllModulesEnabled(!HasAnyModuleEnabled());
            return true;
        }

        var target = string.Empty;
        if (args.StartsWith("toggle ", StringComparison.OrdinalIgnoreCase))
        {
            target = args["toggle ".Length..].Trim();
        }
        else if (args.StartsWith("toggel ", StringComparison.OrdinalIgnoreCase))
        {
            target = args["toggel ".Length..].Trim();
        }
        else
        {
            var parts = args.Split(' ', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2 || !IsToggleWord(parts[1]))
            {
                return false;
            }

            target = parts[0];
        }

        return ToggleModuleTarget(target);
    }

    private bool ToggleModuleTarget(string target)
    {
        if (target.Length == 0 || target.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            SetAllModulesEnabled(!HasAnyModuleEnabled());
            return true;
        }

        if (target.Equals("tod", StringComparison.OrdinalIgnoreCase))
        {
            SetEnabled(!configuration.Enabled);
            return true;
        }

        if (target.Equals("todrules", StringComparison.OrdinalIgnoreCase) ||
            target.Equals("tod rules", StringComparison.OrdinalIgnoreCase) ||
            target.Equals("special", StringComparison.OrdinalIgnoreCase))
        {
            configuration.TodSpecialRulesEnabled = !configuration.TodSpecialRulesEnabled;
            saveConfiguration();
            TriggerModuleStatusEffects(StatusEffectModule.TodSpecialRules, configuration.TodSpecialRulesEnabled);
            chatGui.Print($"RollTracker ToD special rules {(configuration.TodSpecialRulesEnabled ? "enabled" : "disabled")}.", "RollTracker");
            return true;
        }

        if (target.Equals("todsecond", StringComparison.OrdinalIgnoreCase) ||
            target.Equals("tod second", StringComparison.OrdinalIgnoreCase) ||
            target.Equals("second", StringComparison.OrdinalIgnoreCase))
        {
            SetSecondPairEnabled(!configuration.TodSecondPairEnabled);
            return true;
        }

        if (target.Equals("truth", StringComparison.OrdinalIgnoreCase) ||
            target.Equals("!truth", StringComparison.OrdinalIgnoreCase))
        {
            SetTruthTriggerEnabled(!configuration.TruthTriggerEnabled);
            return true;
        }

        if (target.Equals("dare", StringComparison.OrdinalIgnoreCase) ||
            target.Equals("!dare", StringComparison.OrdinalIgnoreCase))
        {
            SetDareTriggerEnabled(!configuration.DareTriggerEnabled);
            return true;
        }

        if (target.Equals("help", StringComparison.OrdinalIgnoreCase) ||
            target.Equals("!help", StringComparison.OrdinalIgnoreCase))
        {
            SetHelpTriggerEnabled(!configuration.HelpTriggerEnabled);
            return true;
        }

        if (target.Equals("alias", StringComparison.OrdinalIgnoreCase) ||
            target.Equals("chat alias", StringComparison.OrdinalIgnoreCase))
        {
            SetChatAliasEnabled(!configuration.ChatAliasEnabled);
            return true;
        }

        if (target.Equals("wifi", StringComparison.OrdinalIgnoreCase))
        {
            SetWifiEnabled(!configuration.WifiEnabled);
            return true;
        }

        return false;
    }

    private bool HasAnyModuleEnabled()
    {
        return configuration.Enabled ||
            configuration.TodSecondPairEnabled ||
            configuration.TodSpecialRulesEnabled ||
            configuration.TruthTriggerEnabled ||
            configuration.DareTriggerEnabled ||
            configuration.HelpTriggerEnabled ||
            configuration.ChatAliasEnabled ||
            configuration.WifiEnabled;
    }

    private static bool IsToggleWord(string text)
    {
        return text.Equals("toggle", StringComparison.OrdinalIgnoreCase) ||
            text.Equals("toggel", StringComparison.OrdinalIgnoreCase);
    }

    private bool TryExecuteReversedModuleToggle(string args)
    {
        var parts = args.Split(' ', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            return false;
        }

        if (parts[1].Equals("on", StringComparison.OrdinalIgnoreCase))
        {
            return TryExecuteModuleToggle($"on {parts[0]}", true);
        }

        if (parts[1].Equals("off", StringComparison.OrdinalIgnoreCase))
        {
            return TryExecuteModuleToggle($"off {parts[0]}", false);
        }

        if (IsToggleWord(parts[1]))
        {
            return TryExecuteModuleToggleSwitch(args);
        }

        return false;
    }

    private List<string> BuildHelpLines()
    {
        return configuration.HelpPreset switch
        {
            "Compact" =>
            [
                $"Commands: {string.Join("; ", GetHelpCommandInfos().Select(command => $"{command.Command} ({(command.Enabled ? "On" : "Off")})"))}",
            ],
            "Macro Mode" when configuration.AdvancedMode => BuildMacroHelpLines(),
            _ => (configuration.HelpLines ?? Configuration.CreateDefaultHelpLines())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line => line.Trim())
                .Where(IsHelpLineAvailable)
                .ToList(),
        };
    }

    private List<string> BuildMacroHelpLines()
    {
        var macroText = configuration.HelpMacroText ?? string.Empty;

        return macroText
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(ApplyHelpMacroPlaceholders)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => line!.Trim())
            .Where(IsHelpLineAvailable)
            .ToList();
    }

    private string? ApplyHelpMacroPlaceholders(string line)
    {
        line = ApplyHelpMacroSegmentFilters(line);

        return line
            .Replace("{activeCommands}", string.Join(", ", GetHelpCommandInfos().Where(command => command.Enabled).Select(command => command.Command)), StringComparison.OrdinalIgnoreCase)
            .Replace("{commandStates}", string.Join("; ", GetHelpCommandInfos().Select(command => $"{command.Command} ({(command.Enabled ? "On" : "Off")})")), StringComparison.OrdinalIgnoreCase);
    }

    private string ApplyHelpMacroSegmentFilters(string line)
    {
        var output = new StringBuilder();
        var index = 0;

        while (index < line.Length)
        {
            var nextGate = FindNextHelpMacroGate(line, index);
            if (nextGate is null)
            {
                output.Append(line[index..]);
                break;
            }

            if (nextGate.Value.Index > index)
            {
                output.Append(line[index..nextGate.Value.Index]);
            }

            var segmentStart = nextGate.Value.Index + nextGate.Value.Token.Length;
            var followingGate = FindNextHelpMacroGate(line, segmentStart);
            var segmentEnd = followingGate?.Index ?? line.Length;

            if (nextGate.Value.Command.Enabled)
            {
                output.Append(line[segmentStart..segmentEnd]);
            }

            index = segmentEnd;
        }

        return output.ToString();
    }

    private (int Index, string Token, HelpCommandInfo Command)? FindNextHelpMacroGate(string line, int startIndex)
    {
        (int Index, string Token, HelpCommandInfo Command)? nextGate = null;
        foreach (var command in GetHelpCommandInfos())
        {
            var token = $"{{{command.Command}}}";
            var index = line.IndexOf(token, startIndex, StringComparison.OrdinalIgnoreCase);
            if (index < 0 || (nextGate is not null && index >= nextGate.Value.Index))
            {
                continue;
            }

            nextGate = (index, token, command);
        }

        return nextGate;
    }

    private List<HelpCommandInfo> GetHelpCommandInfos()
    {
        return
        [
            new("!help", configuration.HelpTriggerEnabled),
            new("!tod", configuration.Enabled),
            new("!tod2", configuration.TodSecondPairEnabled),
            new("!truth", configuration.TruthTriggerEnabled),
            new("!dare", configuration.DareTriggerEnabled),
            new("!wifi", configuration.WifiEnabled),
        ];
    }

    private bool IsHelpLineAvailable(string helpLine)
    {
        if (StartsWithHelpCommand(helpLine, "!help"))
        {
            return configuration.HelpTriggerEnabled;
        }

        if (StartsWithHelpCommand(helpLine, "!tod2"))
        {
            return configuration.TodSecondPairEnabled;
        }

        if (StartsWithHelpCommand(helpLine, "!tod"))
        {
            return configuration.Enabled;
        }

        if (StartsWithHelpCommand(helpLine, "!truth"))
        {
            return configuration.TruthTriggerEnabled;
        }

        if (StartsWithHelpCommand(helpLine, "!dare"))
        {
            return configuration.DareTriggerEnabled;
        }

        if (StartsWithHelpCommand(helpLine, "!wifi"))
        {
            return configuration.WifiEnabled;
        }

        return true;
    }

    private static bool StartsWithHelpCommand(string helpLine, string command)
    {
        if (!helpLine.StartsWith(command, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return helpLine.Length == command.Length ||
               char.IsWhiteSpace(helpLine[command.Length]) ||
               helpLine[command.Length] is '-' or ':' or '=';
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

        const string randomPrefix = "Random!";
        if (name.StartsWith(randomPrefix, StringComparison.OrdinalIgnoreCase))
        {
            name = name[randomPrefix.Length..].Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                return string.Empty;
            }
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

    private static Dictionary<ushort, WorldDebugInfo> BuildWorldInfos(IDataManager dataManager)
    {
        return dataManager.GetExcelSheet<World>()?
            .Select(row =>
            {
                var worldName = row.Name.ToString().Trim();
                var dataCenterName = row.DataCenter.ValueNullable?.Name.ToString().Trim() ?? string.Empty;
                return new WorldDebugInfo((ushort)row.RowId, worldName, dataCenterName);
            })
            .Where(info => !string.IsNullOrWhiteSpace(info.WorldName))
            .ToDictionary(info => info.WorldId, info => info) ?? [];
    }

    private static Dictionary<uint, string> BuildTerritoryNames(IDataManager dataManager)
    {
        return dataManager.GetExcelSheet<TerritoryType>()?
            .Select(row =>
            {
                var placeName = row.PlaceName.ValueNullable?.NameNoArticle.ToString().Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(placeName))
                {
                    placeName = row.PlaceName.ValueNullable?.Name.ToString().Trim() ?? string.Empty;
                }

                return (row.RowId, PlaceName: placeName);
            })
            .Where(entry => !string.IsNullOrWhiteSpace(entry.PlaceName))
            .ToDictionary(entry => entry.RowId, entry => entry.PlaceName) ?? [];
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

    private readonly record struct RoundResultCommand(string Command, bool IsSpecialRule);

    private readonly record struct HelpCommandInfo(string Command, bool Enabled);

    internal readonly record struct HousingDebugInfo(
        uint TerritoryType,
        bool IsHousingInterior,
        bool IsResidentialArea,
        bool IsBetweenAreas,
        bool HasHousingManager,
        string HousingTerritoryType,
        sbyte Ward,
        sbyte Plot,
        byte Division,
        short Room,
        string CurrentHouseId,
        string CurrentIndoorHouseId,
        uint OriginalHouseTerritoryTypeId,
        bool HasHousePermissions,
        ushort WorldId,
        string WorldName,
        string DataCenterName,
        string DistrictName,
        string CurrentLocationPreview,
        string AddressPreview,
        bool HasReliableInteriorAddress);

    private readonly record struct WorldDebugInfo(ushort WorldId, string WorldName, string DataCenterName);

    private enum RoundKind
    {
        Normal,
        SecondPair,
    }

    private enum StatusEffectModule
    {
        Tod,
        TodSecondPair,
        TodSpecialRules,
        Truth,
        Dare,
        Help,
        ChatAlias,
        Wifi,
    }
}

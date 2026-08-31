using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using RollTracker.Services;

namespace RollTracker.Windows;

internal sealed class MainWindow : Window, IDisposable
{
    private static readonly string[] UiThemeNames =
    [
        "Dalamud Blue",
        "Dalamud Night",
        "Emerald",
        "Graphite",
    ];

    private static readonly string[] UiLayoutNames =
    [
        "Standard",
        "Modern",
        "Legacy",
    ];

    private static readonly string[] HelpPresetNames =
    [
        "Standard",
        "Compact",
        "Macro Mode",
    ];
    private static readonly string[] StandardHelpCommands =
    [
        "!help",
        "!tod",
        "!tod2",
        "!truth",
        "!dare",
        "!wifi",
    ];
    private static readonly Vector2 SettingsButtonSize = new(190, 0);

    private static readonly (string Label, string Args)[] ChatAliasCommandOptions =
    [
        ("/rt on", "on"),
        ("/rt off", "off"),
        ("/rt on tod", "on tod"),
        ("/rt off tod", "off tod"),
        ("/rt on todsecond", "on todsecond"),
        ("/rt off todsecond", "off todsecond"),
        ("/rt on todrules", "on todrules"),
        ("/rt off todrules", "off todrules"),
        ("/rt on truth", "on truth"),
        ("/rt off truth", "off truth"),
        ("/rt on dare", "on dare"),
        ("/rt off dare", "off dare"),
        ("/rt on help", "on help"),
        ("/rt off help", "off help"),
        ("/rt on alias", "on alias"),
        ("/rt off alias", "off alias"),
        ("/rt on wifi", "on wifi"),
        ("/rt off wifi", "off wifi"),
        ("/rt reset", "reset"),
        ("/rt end", "end"),
        ("/rt test", "test"),
    ];

    private const string HelpMacroPlaceholder =
        "{!tod}!tod - Start a Truth or Dare roll round.\n" +
        "{!tod2} !tod2 - Start a second-pair Truth or Dare roll round.\n" +
        "{!truth}!truth - Send a random Truth prompt.\n" +
        "{!dare}!dare - Send a random Dare prompt.\n" +
        "{!wifi} !wifi - Show the Shells and Discord info.\n\n" +
        "{!tod}!tod - Start a Truth or Dare roll round. {!tod2} !tod2 - Start a second-pair Truth or Dare roll round. {!truth}!truth - Send a random Truth prompt. {!dare}!dare - Send a random Dare prompt. {!wifi} !wifi - Show the Shells and Discord info.";

    private static Vector4 AccentColor = new(0.42f, 0.72f, 1.00f, 1.00f);
    private static Vector4 SuccessColor = new(0.46f, 0.86f, 0.58f, 1.00f);
    private static Vector4 WarningColor = new(1.00f, 0.72f, 0.35f, 1.00f);
    private static Vector4 MutedColor = new(0.62f, 0.66f, 0.72f, 1.00f);
    private static Vector4 DangerColor = new(1.00f, 0.32f, 0.30f, 1.00f);
    private static Vector4 PanelColor = new(0.08f, 0.10f, 0.11f, 0.92f);
    private static Vector4 WindowBgColor = new(0.06f, 0.08f, 0.09f, 0.98f);
    private static Vector4 BorderColor = new(0.22f, 0.26f, 0.30f, 0.90f);
    private static Vector4 FrameBgColor = new(0.13f, 0.15f, 0.17f, 1.00f);
    private static Vector4 FrameBgHoveredColor = new(0.18f, 0.22f, 0.26f, 1.00f);
    private static Vector4 ButtonColor = new(0.12f, 0.28f, 0.48f, 1.00f);
    private static Vector4 ButtonHoveredColor = new(0.17f, 0.38f, 0.64f, 1.00f);
    private static Vector4 ButtonActiveColor = new(0.08f, 0.22f, 0.40f, 1.00f);

    private readonly RollTrackerService rollTrackerService;
    private readonly Configuration configuration;
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly IChatGui chatGui;
    private readonly Action saveConfiguration;
    private readonly Action openRollHistoryWindow;
    private readonly Action openChangelogWindow;

    private string newTruthPrompt = string.Empty;
    private string newDarePrompt = string.Empty;
    private int editingTruthSetIndex = -1;
    private int editingDareSetIndex = -1;
    private string editingTruthSetName = string.Empty;
    private string editingDareSetName = string.Empty;
    private int editingSpecialRuleSetIndex = -1;
    private string editingSpecialRuleSetName = string.Empty;
    private int selectedTruthSetIndex;
    private int selectedDareSetIndex;
    private int selectedSpecialRuleSetIndex;
    private Page selectedPage = Page.TruthOrDare;
    private int newSpecialRuleRoll;
    private string newSpecialRuleText = string.Empty;
    private int selectedChatAliasCommandIndex;
    private string newChatAliasTriggerText = string.Empty;

    public MainWindow(
        RollTrackerService rollTrackerService,
        Configuration configuration,
        IDalamudPluginInterface pluginInterface,
        IChatGui chatGui,
        Action saveConfiguration,
        Action openRollHistoryWindow,
        Action openChangelogWindow)
        : base("RollTracker##RollTrackerMainWindow")
    {
        this.rollTrackerService = rollTrackerService;
        this.configuration = configuration;
        this.pluginInterface = pluginInterface;
        this.chatGui = chatGui;
        this.saveConfiguration = saveConfiguration;
        this.openRollHistoryWindow = openRollHistoryWindow;
        this.openChangelogWindow = openChangelogWindow;

        Flags = ImGuiWindowFlags.None;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(760, 500),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }

    public void Dispose()
    {
    }

    public override void Draw()
    {
        ApplyLayoutConstraints();
        ApplyUiTheme(configuration.UiTheme);
        PushWindowStyle();
        try
        {
            if (IsLegacyLayout)
            {
                DrawLegacyLayout();
                return;
            }

            if (IsStandardLayout)
            {
                DrawStandardLayout();
                return;
            }

            var navWidth = 148 * ImGuiHelpers.GlobalScale;
            BeginPanel("Navigation", new Vector2(navWidth, 0), drawTitle: false);
            DrawSidebar();
            EndPanel();

            ImGui.SameLine();

            BeginPanel(GetPageTitle(selectedPage), Vector2.Zero);
            DrawSelectedPage();
            EndPanel();
        }
        finally
        {
            PopWindowStyle();
        }
    }

    private void ApplyLayoutConstraints()
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = IsLegacyLayout
                ? new Vector2(420, 420)
                : new Vector2(760, 500),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }

    private bool IsStandardLayout => configuration.UiLayout.Equals("Standard", StringComparison.Ordinal);

    private bool IsLegacyLayout => configuration.UiLayout.Equals("Legacy", StringComparison.Ordinal);

    private void DrawStandardLayout()
    {
        if (!ImGui.BeginTabBar("RollTrackerTopTabs"))
        {
            return;
        }

        if (ImGui.BeginTabItem("Truth or Dare"))
        {
            DrawTruthOrDareTab();
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("ToD Suggestions"))
        {
            DrawTruthDarePromptTab();
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("Special Rules"))
        {
            DrawSpecialRulesTab();
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("Command Help"))
        {
            DrawCommandHelpTab();
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("Chat Alias"))
        {
            DrawChatAliasTab();
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("Shell Infos"))
        {
            DrawWifiTab();
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("Settings"))
        {
            DrawSettingsTab();
            ImGui.EndTabItem();
        }

        ImGui.EndTabBar();
    }

    private void DrawLegacyLayout()
    {
        if (!ImGui.BeginTabBar("RollTrackerLegacyTabs"))
        {
            return;
        }

        if (ImGui.BeginTabItem("Truth or Dare"))
        {
            DrawLegacyTruthOrDareTab();
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("ToD Suggestions"))
        {
            DrawLegacyTruthDarePromptTab();
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("Special Rules"))
        {
            DrawLegacySpecialRulesTab();
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("Command Help"))
        {
            DrawLegacyCommandHelpTab();
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("Chat Alias"))
        {
            DrawLegacyChatAliasTab();
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("Shell Infos"))
        {
            DrawLegacyWifiTab();
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("Settings"))
        {
            DrawLegacySettingsTab();
            ImGui.EndTabItem();
        }

        ImGui.EndTabBar();
    }

    private void DrawSidebar()
    {
        ImGui.TextColored(AccentColor, "RollTracker");
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        DrawNavButton(Page.TruthOrDare, "Truth or Dare");
        DrawNavButton(Page.TruthDare, "ToD Suggestions");
        DrawNavButton(Page.SpecialRules, "Special Rules");
        DrawNavButton(Page.CommandHelp, "Command Help");
        DrawNavButton(Page.ChatAlias, "Chat Alias");
        DrawNavButton(Page.Wifi, "Shell Infos");

        var bottomButtonHeight = ImGui.GetFrameHeightWithSpacing() + ImGui.GetStyle().ItemSpacing.Y + 2 * ImGuiHelpers.GlobalScale;
        var remainingHeight = ImGui.GetContentRegionAvail().Y - bottomButtonHeight;
        if (remainingHeight > 0)
        {
            ImGui.Dummy(new Vector2(1, remainingHeight));
        }

        ImGui.Separator();
        DrawNavButton(Page.Settings, "Settings");
    }

    private void DrawNavButton(Page page, string label)
    {
        var active = selectedPage == page;
        if (active)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, ButtonColor);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, ButtonHoveredColor);
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, ButtonActiveColor);
        }
        else
        {
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.11f, 0.13f, 0.15f, 1.00f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.16f, 0.20f, 0.24f, 1.00f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.12f, 0.24f, 0.38f, 1.00f));
        }

        if (ImGui.Button($"{label}##Nav{page}", new Vector2(-1, 30 * ImGuiHelpers.GlobalScale)))
        {
            selectedPage = page;
        }

        ImGui.PopStyleColor(3);
    }

    private void DrawSelectedPage()
    {
        switch (selectedPage)
        {
            case Page.TruthOrDare:
                DrawTruthOrDareTab();
                break;
            case Page.TruthDare:
                DrawTruthDarePromptTab();
                break;
            case Page.SpecialRules:
                DrawSpecialRulesTab();
                break;
            case Page.CommandHelp:
                DrawCommandHelpTab();
                break;
            case Page.ChatAlias:
                DrawChatAliasTab();
                break;
            case Page.Wifi:
                DrawWifiTab();
                break;
            case Page.Settings:
                DrawSettingsTab();
                break;
        }
    }

    private static string GetPageTitle(Page page)
    {
        return page switch
        {
            Page.TruthOrDare => "Truth or Dare",
            Page.TruthDare => "ToD Suggestions",
            Page.SpecialRules => "Special Rules",
            Page.CommandHelp => "Command Help",
            Page.ChatAlias => "Chat Alias",
            Page.Wifi => "Shell Infos",
            Page.Settings => "Settings",
            _ => "RollTracker",
        };
    }

    private void DrawHeader()
    {
        var remainingSeconds = Math.Max(0, (int)Math.Ceiling(rollTrackerService.RemainingRoundTime.TotalSeconds));
        var roundText = rollTrackerService.IsRoundRunning ? $"{remainingSeconds}s left" : "Idle";

        ImGui.TextColored(AccentColor, "RollTracker");
        ImGui.SameLine();
        ImGui.TextDisabled("Truth or Dare round control");

        ImGui.Spacing();
        if (ImGui.BeginTable("RollTrackerHeaderStats", 4, ImGuiTableFlags.SizingStretchSame))
        {
            ImGui.TableNextColumn();
            DrawStatCard("Round", roundText, rollTrackerService.IsRoundRunning ? SuccessColor : MutedColor);
            ImGui.TableNextColumn();
            DrawStatCard("Rolls", rollTrackerService.Rolls.Count.ToString(), AccentColor);
            ImGui.TableNextColumn();
            DrawStatCard("Highest", FormatRollSummary(rollTrackerService.HighestRoll), SuccessColor);
            ImGui.TableNextColumn();
            DrawStatCard("Lowest", FormatRollSummary(rollTrackerService.LowestRoll), WarningColor);
            ImGui.EndTable();
        }

        ImGui.Spacing();
        DrawStatusPill("ToD", configuration.Enabled);
        ImGui.SameLine();
        DrawStatusPill("!tod2", configuration.TodSecondPairEnabled);
        ImGui.SameLine();
        DrawStatusPill("Rules", configuration.TodSpecialRulesEnabled);
        ImGui.SameLine();
        DrawStatusPill("!truth", configuration.TruthTriggerEnabled);
        ImGui.SameLine();
        DrawStatusPill("!dare", configuration.DareTriggerEnabled);
        ImGui.SameLine();
        DrawStatusPill("!help", configuration.HelpTriggerEnabled);
        ImGui.SameLine();
        DrawStatusPill("!wifi", configuration.WifiEnabled);
        ImGui.Separator();
    }

    private void DrawTruthOrDareTab()
    {
        var available = ImGui.GetContentRegionAvail();
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var rightWidth = Math.Min(260 * ImGuiHelpers.GlobalScale, Math.Max(210 * ImGuiHelpers.GlobalScale, available.X * 0.24f));
        var leftWidth = Math.Max(360 * ImGuiHelpers.GlobalScale, available.X - rightWidth - spacing);

        BeginPanel("Controls", new Vector2(leftWidth, 0));
        DrawCompactRoundStatus();

        if (ImGui.Button("Reset rolls", new Vector2(-1, 0)))
        {
            rollTrackerService.Reset();
        }

        if (ImGui.Button("End round and post result", new Vector2(-1, 0)))
        {
            rollTrackerService.FinishRoundAndReset();
        }

        ImGui.Spacing();
        DrawTodQuickControls();
        EndPanel();

        ImGui.SameLine();

        BeginPanel("Round Info", new Vector2(rightWidth, 0));
        DrawRoundInfoPanel();
        EndPanel();
    }

    private void DrawCompactRoundStatus()
    {
        DrawKeyValue("Round", rollTrackerService.IsRoundRunning ? "Running" : "Idle", rollTrackerService.IsRoundRunning ? SuccessColor : MutedColor);
        DrawKeyValue("Rolls", rollTrackerService.Rolls.Count.ToString(), AccentColor);
        DrawKeyValue("Highest", FormatRollSummary(rollTrackerService.HighestRoll), SuccessColor);
        DrawKeyValue("Lowest", FormatRollSummary(rollTrackerService.LowestRoll), WarningColor);
        ImGui.Spacing();
    }

    private void DrawTodQuickControls()
    {
        DrawSectionTitle("Normal ToD Round");

        var durationSeconds = configuration.MacroDurationSeconds;
        var lineDelayMilliseconds = configuration.MacroLineDelayMilliseconds;
        DrawTimingInputs("Tod", ref durationSeconds, ref lineDelayMilliseconds);
        configuration.MacroDurationSeconds = durationSeconds;
        configuration.MacroLineDelayMilliseconds = lineDelayMilliseconds;

        var macroText = configuration.MacroText;
        ImGui.TextDisabled("Macro");
        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputTextMultiline("##TodMacroText", ref macroText, 4096, new Vector2(0, 64 * ImGuiHelpers.GlobalScale)))
        {
            configuration.MacroText = macroText;
            saveConfiguration();
        }

        var resultCommand = configuration.ResultCommandTemplate;
        ImGui.TextDisabled("Result command");
        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputText("##TodResult", ref resultCommand, 512))
        {
            configuration.ResultCommandTemplate = resultCommand;
            saveConfiguration();
        }

        var notEnoughPlayersResultText = configuration.NotEnoughPlayersResultText;
        ImGui.TextDisabled("Not enough players text");
        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputText("##TodQuickNotEnoughPlayers", ref notEnoughPlayersResultText, 512))
        {
            configuration.NotEnoughPlayersResultText = notEnoughPlayersResultText;
            saveConfiguration();
        }

        if (ImGui.Button("Run !tod Now", new Vector2(-1, 0)))
        {
            rollTrackerService.StartRoundFromTrigger("manual");
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        DrawSectionTitle("Double ToD Round");

        var secondPairDurationSeconds = configuration.TodSecondPairMacroDurationSeconds;
        var secondPairLineDelayMilliseconds = configuration.TodSecondPairMacroLineDelayMilliseconds;
        DrawTimingInputs("Tod2", ref secondPairDurationSeconds, ref secondPairLineDelayMilliseconds);
        configuration.TodSecondPairMacroDurationSeconds = secondPairDurationSeconds;
        configuration.TodSecondPairMacroLineDelayMilliseconds = secondPairLineDelayMilliseconds;

        var secondMacroText = configuration.TodSecondPairMacroText;
        ImGui.TextDisabled("Macro");
        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputTextMultiline("##Tod2MacroText", ref secondMacroText, 4096, new Vector2(0, 64 * ImGuiHelpers.GlobalScale)))
        {
            configuration.TodSecondPairMacroText = secondMacroText;
            saveConfiguration();
        }

        var secondResultCommand = configuration.TodSecondPairResultCommandTemplate;
        ImGui.TextDisabled("Result command");
        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputTextMultiline("##Tod2Result", ref secondResultCommand, 1024, new Vector2(0, 46 * ImGuiHelpers.GlobalScale)))
        {
            configuration.TodSecondPairResultCommandTemplate = secondResultCommand;
            saveConfiguration();
        }
        DrawTodSecondPairResultLineDelayInput("Quick");

        var notEnoughRoundPlayersResultText = configuration.TodSecondPairNotEnoughRoundPlayersResultText;
        ImGui.TextDisabled("Not enough players text");
        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputText("##Tod2QuickNotEnoughPlayers", ref notEnoughRoundPlayersResultText, 512))
        {
            configuration.TodSecondPairNotEnoughRoundPlayersResultText = notEnoughRoundPlayersResultText;
            saveConfiguration();
        }

        var notEnoughSecondPairText = configuration.TodSecondPairNotEnoughPlayersResultText;
        ImGui.TextDisabled("Not enough second pair text");
        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputText("##Tod2QuickNotEnoughSecondPair", ref notEnoughSecondPairText, 512))
        {
            configuration.TodSecondPairNotEnoughPlayersResultText = notEnoughSecondPairText;
            saveConfiguration();
        }

        if (ImGui.Button("Run !tod2 Now", new Vector2(-1, 0)))
        {
            rollTrackerService.StartSecondPairRoundFromTrigger("manual");
        }
    }

    private void DrawRoundInfoPanel()
    {
        var remainingSeconds = Math.Max(0, (int)Math.Ceiling(rollTrackerService.RemainingRoundTime.TotalSeconds));
        DrawKeyValue("Status", rollTrackerService.IsRoundRunning ? "Running" : "Idle", rollTrackerService.IsRoundRunning ? SuccessColor : MutedColor);
        DrawKeyValue("Remaining", rollTrackerService.IsRoundRunning ? $"{remainingSeconds}s" : "-", AccentColor);
        DrawKeyValue("Duration", $"{configuration.MacroDurationSeconds}s", MutedColor);
        DrawKeyValue("Rolls", rollTrackerService.Rolls.Count.ToString(), AccentColor);

        ImGui.Spacing();
        if (ImGui.Button("Open Roll History", new Vector2(-1, 0)))
        {
            openRollHistoryWindow();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        DrawRollDetail("Highest Roll", rollTrackerService.HighestRoll, SuccessColor);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        DrawRollDetail("Lowest Roll", rollTrackerService.LowestRoll, DangerColor);
    }

    private static void DrawRollDetail(string title, RollEntry? roll, Vector4 color)
    {
        DrawSectionTitle(title);
        if (roll is null)
        {
            ImGui.TextDisabled("No roll yet.");
            return;
        }

        DrawKeyValue("Player", roll.PlayerName, color);
        DrawKeyValue("Roll", roll.Value.ToString(), color);
        DrawKeyValue("Time", roll.Time.ToLocalTime().ToString("HH:mm:ss"), MutedColor);
    }

    private void DrawTodEditor()
    {
        DrawSectionTitle("Normal ToD Round");

        var durationSeconds = configuration.MacroDurationSeconds;
        var lineDelayMilliseconds = configuration.MacroLineDelayMilliseconds;
        DrawTimingInputs("Tod", ref durationSeconds, ref lineDelayMilliseconds);
        configuration.MacroDurationSeconds = durationSeconds;
        configuration.MacroLineDelayMilliseconds = lineDelayMilliseconds;

        var macroText = configuration.MacroText;
        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputTextMultiline("Macro##Tod", ref macroText, 4096, new Vector2(0, 90 * ImGuiHelpers.GlobalScale)))
        {
            configuration.MacroText = macroText;
            saveConfiguration();
        }

        var resultCommandTemplate = configuration.ResultCommandTemplate;
        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputText("Result command##Tod", ref resultCommandTemplate, 512))
        {
            configuration.ResultCommandTemplate = resultCommandTemplate;
            saveConfiguration();
        }

        var notEnoughPlayersResultText = configuration.NotEnoughPlayersResultText;
        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputText("Not enough players text##Tod", ref notEnoughPlayersResultText, 512))
        {
            configuration.NotEnoughPlayersResultText = notEnoughPlayersResultText;
            saveConfiguration();
        }
    }

    private void DrawLegacyTruthOrDareTab()
    {
        var highest = rollTrackerService.HighestRoll;
        var lowest = rollTrackerService.LowestRoll;

        ImGui.TextUnformatted(rollTrackerService.IsRoundRunning
            ? $"Round: {Math.Max(0, (int)Math.Ceiling(rollTrackerService.RemainingRoundTime.TotalSeconds))}s"
            : "Round: idle");

        ImGui.Separator();
        ImGui.TextUnformatted($"Rolls: {rollTrackerService.Rolls.Count}");
        ImGui.Spacing();

        DrawSummaryLine("Highest", highest);
        DrawSummaryLine("Lowest", lowest);

        ImGui.Spacing();

        if (ImGui.Button("Reset"))
        {
            rollTrackerService.Reset();
        }

        ImGui.SameLine();

        if (ImGui.Button("End Round"))
        {
            rollTrackerService.FinishRoundAndReset();
        }

        ImGui.Separator();

        ImGui.TextUnformatted("Normal ToD Round");

        var duration = configuration.MacroDurationSeconds;
        if (ImGui.InputInt("Macro duration (s)", ref duration))
        {
            configuration.MacroDurationSeconds = Math.Clamp(duration, 1, 600);
            saveConfiguration();
        }

        var lineDelay = configuration.MacroLineDelayMilliseconds;
        if (DrawAdvancedInputInt("Line delay (ms)", ref lineDelay))
        {
            configuration.MacroLineDelayMilliseconds = Math.Clamp(lineDelay, 100, 10000);
            saveConfiguration();
        }
        DrawLineDelayTooltip("Delay between !tod macro lines. Leave this alone unless chat lines are skipped.");

        var macroText = configuration.MacroText;
        if (ImGui.InputTextMultiline("Macro##TodLegacy", ref macroText, 4096, new Vector2(0, 90 * ImGuiHelpers.GlobalScale)))
        {
            configuration.MacroText = macroText;
            saveConfiguration();
        }

        var resultCommandTemplate = configuration.ResultCommandTemplate;
        if (ImGui.InputText("Result command##TodLegacy", ref resultCommandTemplate, 512))
        {
            configuration.ResultCommandTemplate = resultCommandTemplate;
            saveConfiguration();
        }

        var notEnoughPlayersResultText = configuration.NotEnoughPlayersResultText;
        if (ImGui.InputText("Not enough players text##TodLegacy", ref notEnoughPlayersResultText, 512))
        {
            configuration.NotEnoughPlayersResultText = notEnoughPlayersResultText;
            saveConfiguration();
        }

        ImGui.Separator();

        ImGui.TextUnformatted("Double ToD Round");

        var secondPairDuration = configuration.TodSecondPairMacroDurationSeconds;
        if (ImGui.InputInt("Macro duration (s)##Tod2Legacy", ref secondPairDuration))
        {
            configuration.TodSecondPairMacroDurationSeconds = Math.Clamp(secondPairDuration, 1, 600);
            saveConfiguration();
        }

        var secondPairLineDelay = configuration.TodSecondPairMacroLineDelayMilliseconds;
        if (DrawAdvancedInputInt("Line delay (ms)##Tod2Legacy", ref secondPairLineDelay))
        {
            configuration.TodSecondPairMacroLineDelayMilliseconds = Math.Clamp(secondPairLineDelay, 100, 10000);
            saveConfiguration();
        }
        DrawLineDelayTooltip("Delay between !tod2 macro lines. Leave this alone unless chat lines are skipped.");

        var secondPairMacroText = configuration.TodSecondPairMacroText;
        if (ImGui.InputTextMultiline("Macro##Tod2Legacy", ref secondPairMacroText, 4096, new Vector2(0, 90 * ImGuiHelpers.GlobalScale)))
        {
            configuration.TodSecondPairMacroText = secondPairMacroText;
            saveConfiguration();
        }

        var secondPairResultCommandTemplate = configuration.TodSecondPairResultCommandTemplate;
        if (ImGui.InputTextMultiline("Result command##Tod2Legacy", ref secondPairResultCommandTemplate, 1024, new Vector2(0, 55 * ImGuiHelpers.GlobalScale)))
        {
            configuration.TodSecondPairResultCommandTemplate = secondPairResultCommandTemplate;
            saveConfiguration();
        }
        DrawTodSecondPairResultLineDelayInput("Legacy");

        var secondPairNotEnoughRoundPlayersResultText = configuration.TodSecondPairNotEnoughRoundPlayersResultText;
        if (ImGui.InputText("Not enough players text##Tod2Legacy", ref secondPairNotEnoughRoundPlayersResultText, 512))
        {
            configuration.TodSecondPairNotEnoughRoundPlayersResultText = secondPairNotEnoughRoundPlayersResultText;
            saveConfiguration();
        }

        var secondPairNotEnoughPlayersResultText = configuration.TodSecondPairNotEnoughPlayersResultText;
        if (ImGui.InputText("Not enough second pair text##Tod2Legacy", ref secondPairNotEnoughPlayersResultText, 512))
        {
            configuration.TodSecondPairNotEnoughPlayersResultText = secondPairNotEnoughPlayersResultText;
            saveConfiguration();
        }

        ImGui.Separator();
        DrawLegacyRollTable(new Vector2(0, 150 * ImGuiHelpers.GlobalScale));
    }

    private void DrawLegacyTruthDarePromptTab()
    {
        ImGui.TextUnformatted($"!truth: {(configuration.TruthTriggerEnabled ? "active" : "inactive")} / !dare: {(configuration.DareTriggerEnabled ? "active" : "inactive")}");
        DrawChatChannelCombo("Chat", configuration.TodPromptChatChannel, channel => configuration.TodPromptChatChannel = channel);
        ImGui.Separator();

        var managerWidth = Math.Min(260 * ImGuiHelpers.GlobalScale, Math.Max(210 * ImGuiHelpers.GlobalScale, ImGui.GetContentRegionAvail().X * 0.26f));
        var promptTabsWidth = Math.Max(360 * ImGuiHelpers.GlobalScale, ImGui.GetContentRegionAvail().X - managerWidth - ImGui.GetStyle().ItemSpacing.X);

        ImGui.BeginChild("##LegacyPromptEditor", new Vector2(promptTabsWidth, 0), false, ImGuiWindowFlags.None);
        if (ImGui.BeginTabBar("RollTrackerLegacyPromptTabs"))
        {
            if (ImGui.BeginTabItem("Truths"))
            {
                DrawLegacyPromptSetTabs(
                    "Truth",
                    configuration.TruthPromptSets,
                    ref newTruthPrompt,
                    ref editingTruthSetIndex,
                    ref editingTruthSetName);
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Dares"))
            {
                DrawLegacyPromptSetTabs(
                    "Dare",
                    configuration.DarePromptSets,
                    ref newDarePrompt,
                    ref editingDareSetIndex,
                    ref editingDareSetName);
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }
        ImGui.EndChild();

        ImGui.SameLine();
        DrawLegacySetManager(new Vector2(managerWidth, 0));
    }

    private void DrawLegacySpecialRulesTab()
    {
        EnsureSpecialRuleSets();

        var available = ImGui.GetContentRegionAvail();
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var managerWidth = Math.Max(240 * ImGuiHelpers.GlobalScale, available.X * 0.24f);
        var editorWidth = Math.Max(420 * ImGuiHelpers.GlobalScale, available.X - managerWidth - spacing);

        ImGui.BeginChild("##LegacySpecialRuleEditor", new Vector2(editorWidth, 0), true, ImGuiWindowFlags.None);
        DrawSpecialRuleSetToolbar("Legacy", ref selectedSpecialRuleSetIndex);
        ImGui.Separator();
        DrawSpecialRuleSetEditor("Legacy", selectedSpecialRuleSetIndex, legacyStyle: true);
        ImGui.EndChild();

        ImGui.SameLine();
        DrawSpecialRuleSetManager(new Vector2(managerWidth, 0), legacyStyle: true);
    }

    private void DrawLegacyWifiTab()
    {
        ImGui.TextUnformatted(rollTrackerService.IsWifiMacroRunning ? "Macro: running" : "Macro: idle");
        DrawChatChannelCombo("Chat", configuration.WifiChatChannel, channel => configuration.WifiChatChannel = channel);

        var wifiLineDelay = configuration.WifiMacroLineDelayMilliseconds;
        if (DrawAdvancedInputInt("Line delay (ms)##LegacyWifi", ref wifiLineDelay))
        {
            configuration.WifiMacroLineDelayMilliseconds = Math.Clamp(wifiLineDelay, 100, 10000);
            saveConfiguration();
        }
        DrawLineDelayTooltip("Delay between !wifi chat lines. Leave this alone unless chat lines are skipped.");

        var wifiMacroText = configuration.WifiMacroText;
        if (ImGui.InputTextMultiline("Macro##LegacyWifi", ref wifiMacroText, 4096, new Vector2(0, 170 * ImGuiHelpers.GlobalScale)))
        {
            configuration.WifiMacroText = wifiMacroText;
            saveConfiguration();
        }

        if (ImGui.Button("Run !wifi"))
        {
            rollTrackerService.StartWifiMacro("manual");
        }
    }

    private void DrawLegacyCommandHelpTab()
    {
        DrawCommandHelpContent("Legacy", legacyStyle: true);
    }

    private void DrawLegacyChatAliasTab()
    {
        DrawChatAliasContent("Legacy", legacyStyle: true);
    }

    private void DrawLegacySettingsTab()
    {
        DrawSettingsSection("Appearance");
        DrawUiLayoutCombo();
        DrawUiThemeCombo();
        DrawOpenChangelogButton();

        DrawSettingsSection("Truth or Dare");
        DrawModuleToggle("Enable ToD", configuration.Enabled, rollTrackerService.SetEnabled);
        DrawModuleToggle("Enable ToD - Doubles", configuration.TodSecondPairEnabled, rollTrackerService.SetSecondPairEnabled);

        var todSpecialRulesEnabled = configuration.TodSpecialRulesEnabled;
        if (ImGui.Checkbox("Enable ToD special rules", ref todSpecialRulesEnabled))
        {
            configuration.TodSpecialRulesEnabled = todSpecialRulesEnabled;
            saveConfiguration();
        }

        DrawSettingsSection("Truth / Dare Suggestions");
        DrawModuleToggle("Enable !truth", configuration.TruthTriggerEnabled, rollTrackerService.SetTruthTriggerEnabled);
        DrawModuleToggle("Enable !dare", configuration.DareTriggerEnabled, rollTrackerService.SetDareTriggerEnabled);

        DrawSettingsSection("General");
        DrawModuleToggle("Enable !help", configuration.HelpTriggerEnabled, rollTrackerService.SetHelpTriggerEnabled);
        DrawModuleToggle("Enable chat alias", configuration.ChatAliasEnabled, rollTrackerService.SetChatAliasEnabled);

        var autoDisableWhenLeavingHousing = configuration.AutoDisableWhenLeavingHousing;
        if (ImGui.Checkbox("Auto off outside house", ref autoDisableWhenLeavingHousing))
        {
            configuration.AutoDisableWhenLeavingHousing = autoDisableWhenLeavingHousing;
            saveConfiguration();
        }

        if (DrawSettingsButton("Open config folder"))
        {
            OpenConfigFolder();
        }

        DrawSettingsSection("Advanced");
        DrawAdvancedModeControls();

        DrawSettingsSection("Wifi");
        DrawModuleToggle("Enable Wifi", configuration.WifiEnabled, rollTrackerService.SetWifiEnabled);

        DrawSettingsSection("Global");
        if (DrawSettingsButton("Enable all"))
        {
            rollTrackerService.SetAllModulesEnabled(true);
        }

        ImGui.SameLine();

        if (DrawSettingsButton("Disable all"))
        {
            rollTrackerService.SetAllModulesEnabled(false);
        }
    }

    private void DrawLegacyPromptSetTabs(
        string label,
        List<TodPromptSet> promptSets,
        ref string newPrompt,
        ref int editingSetIndex,
        ref string editingSetName)
    {
        if (promptSets.Count == 0)
        {
            promptSets.Add(new TodPromptSet());
            saveConfiguration();
        }

        if (ImGui.Button($"+##LegacyAdd{label}Set"))
        {
            promptSets.Add(new TodPromptSet
            {
                Name = $"Set {promptSets.Count + 1}",
                Enabled = true,
                Prompts = [],
            });
            saveConfiguration();
        }

        ImGui.SameLine();
        ImGui.TextUnformatted($"Active: {promptSets.Count(set => set.Enabled)} / {promptSets.Count}");
        ImGui.Separator();

        if (!ImGui.BeginTabBar($"{label}LegacyPromptSetTabs"))
        {
            return;
        }

        for (var i = 0; i < promptSets.Count; i++)
        {
            var promptSet = promptSets[i];
            var enabledMarker = promptSet.Enabled ? "[X]" : "[ ]";
            var tabName = string.IsNullOrWhiteSpace(promptSet.Name) ? $"Set {i + 1}" : promptSet.Name.Trim();

            if (!ImGui.BeginTabItem($"{enabledMarker} {tabName}##Legacy{label}Set{i}"))
            {
                continue;
            }

            DrawLegacyPromptSet(label, promptSets, i, ref newPrompt, ref editingSetIndex, ref editingSetName);
            ImGui.EndTabItem();
        }

        ImGui.EndTabBar();
    }

    private void DrawLegacyPromptSet(
        string label,
        List<TodPromptSet> promptSets,
        int setIndex,
        ref string newPrompt,
        ref int editingSetIndex,
        ref string editingSetName)
    {
        var promptSet = promptSets[setIndex];
        promptSet.Prompts ??= [];

        ImGui.TextColored(promptSet.Enabled ? SuccessColor : MutedColor, promptSet.Enabled ? "Enabled in Sets Manager" : "Disabled in Sets Manager");
        ImGui.SameLine();

        if (ImGui.Button($"Edit name##Legacy{label}SetEdit{setIndex}"))
        {
            editingSetIndex = setIndex;
            editingSetName = promptSet.Name;
        }

        if (promptSets.Count > 1)
        {
            ImGui.SameLine();

            if (ImGui.Button($"Delete set##Legacy{label}SetDelete{setIndex}"))
            {
                promptSets.RemoveAt(setIndex);
                if (editingSetIndex == setIndex)
                {
                    editingSetIndex = -1;
                    editingSetName = string.Empty;
                }
                saveConfiguration();
                return;
            }
        }

        if (editingSetIndex == setIndex)
        {
            ImGui.SetNextItemWidth(-1);
            ImGui.InputText($"Set name##Legacy{label}SetName{setIndex}", ref editingSetName, 128);

            if (ImGui.Button($"Save name##Legacy{label}SetSave{setIndex}"))
            {
                promptSet.Name = string.IsNullOrWhiteSpace(editingSetName)
                    ? $"Set {setIndex + 1}"
                    : editingSetName.Trim();
                promptSets[setIndex] = promptSet;
                editingSetIndex = -1;
                editingSetName = string.Empty;
                saveConfiguration();
            }

            ImGui.SameLine();

            if (ImGui.Button($"Cancel##Legacy{label}SetCancel{setIndex}"))
            {
                editingSetIndex = -1;
                editingSetName = string.Empty;
            }
        }

        DrawLegacyPromptList(label, promptSet.Prompts, ref newPrompt);
        promptSets[setIndex] = promptSet;
    }

    private void DrawLegacyPromptList(string label, List<string> prompts, ref string newPrompt)
    {
        ImGui.TextUnformatted($"{label}s: {prompts.Count}");
        ImGui.Spacing();

        for (var i = 0; i < prompts.Count; i++)
        {
            ImGui.PushID($"Legacy{label}{i}");

            if (ImGui.Button("Delete"))
            {
                prompts.RemoveAt(i);
                saveConfiguration();
                ImGui.PopID();
                i--;
                continue;
            }

            ImGui.SameLine();

            var prompt = prompts[i];
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputText("##Prompt", ref prompt, 1024))
            {
                prompts[i] = prompt;
                saveConfiguration();
            }

            ImGui.PopID();
        }

        ImGui.Separator();

        ImGui.SetNextItemWidth(-1);
        ImGui.InputText($"New {label}", ref newPrompt, 1024);

        if (ImGui.Button($"Add {label}") && !string.IsNullOrWhiteSpace(newPrompt))
        {
            prompts.Add(newPrompt.Trim());
            newPrompt = string.Empty;
            saveConfiguration();
        }
    }

    private void DrawLegacySetManager(Vector2 size)
    {
        ImGui.BeginChild("##LegacySetManager", size, true, ImGuiWindowFlags.None);
        ImGui.TextUnformatted("Sets Manager");
        ImGui.Separator();

        if (ImGui.BeginTable("RollTrackerLegacySetManagerTable", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp))
        {
            ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthFixed, 48 * ImGuiHelpers.GlobalScale);
            ImGui.TableSetupColumn("Set Name");
            ImGui.TableSetupColumn("State", ImGuiTableColumnFlags.WidthFixed, 48 * ImGuiHelpers.GlobalScale);
            ImGui.TableHeadersRow();

            DrawLegacySetManagerRows("Truth", configuration.TruthPromptSets);
            DrawLegacySetManagerRows("Dare", configuration.DarePromptSets);

            ImGui.EndTable();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextUnformatted("Active Set Preview");
        DrawLegacyCountLine("Truth prompts", CountEnabledPrompts(configuration.TruthPromptSets));
        DrawLegacyCountLine("Dare prompts", CountEnabledPrompts(configuration.DarePromptSets));
        ImGui.Spacing();
        ImGui.TextWrapped("Click a set name to enable or disable it.");
        ImGui.EndChild();
    }

    private static void DrawLegacyCountLine(string label, int value)
    {
        ImGui.TextUnformatted($"{label}:");
        ImGui.SameLine(115 * ImGuiHelpers.GlobalScale);
        ImGui.TextColored(AccentColor, value.ToString());
    }

    private void DrawLegacySetManagerRows(string type, List<TodPromptSet> promptSets)
    {
        for (var i = 0; i < promptSets.Count; i++)
        {
            var promptSet = promptSets[i];
            var setName = string.IsNullOrWhiteSpace(promptSet.Name) ? $"Set {i + 1}" : promptSet.Name.Trim();
            var enabled = promptSet.Enabled;
            var textColor = enabled ? SuccessColor : MutedColor;

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(type);
            ImGui.TableNextColumn();
            ImGui.PushStyleColor(ImGuiCol.Text, textColor);
            ImGui.TextWrapped(setName);
            if (ImGui.IsItemClicked())
            {
                promptSet.Enabled = !enabled;
                promptSets[i] = promptSet;
                saveConfiguration();
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(enabled ? "Click to disable this set." : "Click to enable this set.");
            }

            ImGui.PopStyleColor();
            ImGui.TableNextColumn();
            ImGui.TextColored(textColor, enabled ? "On" : "Off");
        }
    }

    private void DrawTodSecondPairEditor()
    {
        DrawSectionTitle("Double ToD Round");

        var durationSeconds = configuration.TodSecondPairMacroDurationSeconds;
        var lineDelayMilliseconds = configuration.TodSecondPairMacroLineDelayMilliseconds;
        DrawTimingInputs("Tod2", ref durationSeconds, ref lineDelayMilliseconds);
        configuration.TodSecondPairMacroDurationSeconds = durationSeconds;
        configuration.TodSecondPairMacroLineDelayMilliseconds = lineDelayMilliseconds;

        var macroText = configuration.TodSecondPairMacroText;
        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputTextMultiline("Macro##Tod2", ref macroText, 4096, new Vector2(0, 90 * ImGuiHelpers.GlobalScale)))
        {
            configuration.TodSecondPairMacroText = macroText;
            saveConfiguration();
        }

        var resultCommandTemplate = configuration.TodSecondPairResultCommandTemplate;
        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputTextMultiline("Result command##Tod2", ref resultCommandTemplate, 1024, new Vector2(0, 54 * ImGuiHelpers.GlobalScale)))
        {
            configuration.TodSecondPairResultCommandTemplate = resultCommandTemplate;
            saveConfiguration();
        }
        DrawTodSecondPairResultLineDelayInput("Modern");

        var notEnoughRoundPlayersResultText = configuration.TodSecondPairNotEnoughRoundPlayersResultText;
        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputText("Not enough players text##Tod2", ref notEnoughRoundPlayersResultText, 512))
        {
            configuration.TodSecondPairNotEnoughRoundPlayersResultText = notEnoughRoundPlayersResultText;
            saveConfiguration();
        }

        var notEnoughPlayersResultText = configuration.TodSecondPairNotEnoughPlayersResultText;
        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputText("Not enough second pair text##Tod2", ref notEnoughPlayersResultText, 512))
        {
            configuration.TodSecondPairNotEnoughPlayersResultText = notEnoughPlayersResultText;
            saveConfiguration();
        }
    }

    private void DrawTimingInputs(string id, ref int durationSeconds, ref int lineDelayMilliseconds)
    {
        if (ImGui.BeginTable($"{id}TimingTable", 2, ImGuiTableFlags.SizingStretchSame))
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TextDisabled("Macro duration (s)");
            ImGui.TableNextColumn();
            ImGui.TextDisabled("Line delay (ms)");

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputInt($"##Duration{id}", ref durationSeconds))
            {
                durationSeconds = Math.Clamp(durationSeconds, 1, 600);
                saveConfiguration();
            }

            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(-1);
            if (DrawAdvancedInputInt($"##LineDelay{id}", ref lineDelayMilliseconds))
            {
                lineDelayMilliseconds = Math.Clamp(lineDelayMilliseconds, 100, 10000);
                saveConfiguration();
            }
            DrawLineDelayTooltip($"Delay between !{id.ToLowerInvariant()} macro lines. Leave this alone unless chat lines are skipped.");
            ImGui.EndTable();
        }
    }

    private void DrawTodSecondPairResultLineDelayInput(string id)
    {
        var resultLineDelayMilliseconds = configuration.TodSecondPairResultLineDelayMilliseconds;
        ImGui.SetNextItemWidth(170 * ImGuiHelpers.GlobalScale);
        if (DrawAdvancedInputInt($"Result line delay (ms)##{id}Tod2ResultLineDelay", ref resultLineDelayMilliseconds))
        {
            configuration.TodSecondPairResultLineDelayMilliseconds = Math.Clamp(resultLineDelayMilliseconds, 100, 10000);
            saveConfiguration();
        }

        DrawLineDelayTooltip("Delay between !tod2 result command lines. Leave this alone unless result chat lines are skipped.");
    }

    private void DrawTruthDarePromptTab()
    {
        var visibleSize = ImGui.GetContentRegionAvail();
        var contentWidth = Math.Max(820 * ImGuiHelpers.GlobalScale, visibleSize.X);
        ImGui.SetNextWindowContentSize(new Vector2(contentWidth, 0));

        if (!ImGui.BeginChild(
            "##RollTrackerTruthDarePromptScroll",
            Vector2.Zero,
            false,
            ImGuiWindowFlags.HorizontalScrollbar))
        {
            ImGui.EndChild();
            return;
        }

        BeginPanel("Prompt toolbar", new Vector2(contentWidth, 70 * ImGuiHelpers.GlobalScale));
        DrawStatusPill("!truth", configuration.TruthTriggerEnabled);
        ImGui.SameLine();
        DrawStatusPill("!dare", configuration.DareTriggerEnabled);
        ImGui.SameLine();
        DrawChatChannelCombo("Chat", configuration.TodPromptChatChannel, channel => configuration.TodPromptChatChannel = channel);
        EndPanel();

        ImGui.Spacing();

        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var managerWidth = 270 * ImGuiHelpers.GlobalScale;
        var promptWidth = Math.Max(500 * ImGuiHelpers.GlobalScale, contentWidth - managerWidth - spacing);

        BeginPanel("Prompt Sets", new Vector2(promptWidth, 0));
        if (ImGui.BeginTabBar("RollTrackerModernSuggestionTabs"))
        {
            if (ImGui.BeginTabItem("Truth"))
            {
                DrawPromptSetTabs(
                    "Truth",
                    configuration.TruthPromptSets,
                    ref newTruthPrompt,
                    ref editingTruthSetIndex,
                    ref editingTruthSetName,
                    ref selectedTruthSetIndex,
                    Vector2.Zero,
                    showEnabledToggle: false);
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Dare"))
            {
                DrawPromptSetTabs(
                    "Dare",
                    configuration.DarePromptSets,
                    ref newDarePrompt,
                    ref editingDareSetIndex,
                    ref editingDareSetName,
                    ref selectedDareSetIndex,
                    Vector2.Zero,
                    showEnabledToggle: false);
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }
        EndPanel();

        ImGui.SameLine();
        DrawSetManager(new Vector2(managerWidth, 0));
        ImGui.EndChild();
    }

    private void DrawPromptSetTabs(
        string label,
        List<TodPromptSet> promptSets,
        ref string newPrompt,
        ref int editingSetIndex,
        ref string editingSetName,
        ref int selectedSetIndex,
        Vector2 size,
        bool showEnabledToggle)
    {
        if (promptSets.Count == 0)
        {
            promptSets.Add(new TodPromptSet());
            saveConfiguration();
        }

        selectedSetIndex = Math.Clamp(selectedSetIndex, 0, promptSets.Count - 1);

        BeginPanel($"{label} Prompts ({promptSets[selectedSetIndex].Prompts?.Count ?? 0})", size);
        DrawPromptSetToolbar(label, promptSets, ref selectedSetIndex, ref editingSetIndex, ref editingSetName);
        ImGui.Spacing();

        DrawPromptSet(label, promptSets, selectedSetIndex, ref newPrompt, ref editingSetIndex, ref editingSetName, showEnabledToggle);
        EndPanel();
    }

    private void DrawPromptSetToolbar(
        string label,
        List<TodPromptSet> promptSets,
        ref int selectedSetIndex,
        ref int editingSetIndex,
        ref string editingSetName)
    {
        var selectedSet = promptSets[selectedSetIndex];
        var selectedName = string.IsNullOrWhiteSpace(selectedSet.Name) ? $"Set {selectedSetIndex + 1}" : selectedSet.Name.Trim();

        ImGui.TextUnformatted("Active Set:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(Math.Max(120 * ImGuiHelpers.GlobalScale, ImGui.GetContentRegionAvail().X * 0.32f));
        if (ImGui.BeginCombo($"##{label}ActiveSet", selectedName))
        {
            for (var i = 0; i < promptSets.Count; i++)
            {
                var promptSet = promptSets[i];
                var name = string.IsNullOrWhiteSpace(promptSet.Name) ? $"Set {i + 1}" : promptSet.Name.Trim();
                if (ImGui.Selectable($"{name}##{label}Combo{i}", selectedSetIndex == i))
                {
                    selectedSetIndex = i;
                }
            }

            ImGui.EndCombo();
        }

        ImGui.SameLine();
        if (ImGui.Button($"New Set##Add{label}Set"))
        {
            promptSets.Add(new TodPromptSet
            {
                Name = $"Set {promptSets.Count + 1}",
                Enabled = true,
                Prompts = [],
            });
            selectedSetIndex = promptSets.Count - 1;
            saveConfiguration();
        }

        ImGui.SameLine();
        if (ImGui.Button($"Duplicate##Duplicate{label}Set"))
        {
            promptSets.Add(new TodPromptSet
            {
                Name = $"{selectedName} Copy",
                Enabled = selectedSet.Enabled,
                Prompts = selectedSet.Prompts is null ? [] : [.. selectedSet.Prompts],
            });
            selectedSetIndex = promptSets.Count - 1;
            saveConfiguration();
        }

        ImGui.SameLine();
        if (ImGui.Button($"Rename##{label}SetEditToolbar"))
        {
            editingSetIndex = selectedSetIndex;
            editingSetName = selectedSet.Name;
        }

        if (promptSets.Count > 1)
        {
            ImGui.SameLine();
            if (ImGui.Button($"Delete##{label}SetDeleteToolbar"))
            {
                promptSets.RemoveAt(selectedSetIndex);
                selectedSetIndex = Math.Clamp(selectedSetIndex, 0, promptSets.Count - 1);
                editingSetIndex = -1;
                editingSetName = string.Empty;
                saveConfiguration();
            }
        }
    }

    private void DrawPromptSet(
        string label,
        List<TodPromptSet> promptSets,
        int setIndex,
        ref string newPrompt,
        ref int editingSetIndex,
        ref string editingSetName,
        bool showEnabledToggle)
    {
        var promptSet = promptSets[setIndex];
        promptSet.Prompts ??= [];

        if (showEnabledToggle)
        {
            var enabled = promptSet.Enabled;
            if (ImGui.Checkbox($"Enabled##{label}SetEnabled{setIndex}", ref enabled))
            {
                promptSet.Enabled = enabled;
                promptSets[setIndex] = promptSet;
                saveConfiguration();
            }
        }
        else
        {
            ImGui.TextColored(promptSet.Enabled ? SuccessColor : MutedColor, promptSet.Enabled ? "Enabled in Sets Manager" : "Disabled in Sets Manager");
        }

        if (editingSetIndex == setIndex)
        {
            ImGui.Spacing();
            ImGui.SetNextItemWidth(-1);
            ImGui.InputText($"Set name##{label}SetName{setIndex}", ref editingSetName, 128);

            if (ImGui.Button($"Save name##{label}SetSave{setIndex}"))
            {
                promptSet.Name = string.IsNullOrWhiteSpace(editingSetName)
                    ? $"Set {setIndex + 1}"
                    : editingSetName.Trim();
                promptSets[setIndex] = promptSet;
                editingSetIndex = -1;
                editingSetName = string.Empty;
                saveConfiguration();
            }

            ImGui.SameLine();

            if (ImGui.Button($"Cancel##{label}SetCancel{setIndex}"))
            {
                editingSetIndex = -1;
                editingSetName = string.Empty;
            }
        }

        DrawPromptList(label, promptSet.Prompts, ref newPrompt);
        promptSets[setIndex] = promptSet;
    }

    private void DrawPromptList(string label, List<string> prompts, ref string newPrompt)
    {
        ImGui.Spacing();

        var tableSize = new Vector2(0, Math.Max(180 * ImGuiHelpers.GlobalScale, ImGui.GetContentRegionAvail().Y - 78 * ImGuiHelpers.GlobalScale));
        if (ImGui.BeginTable($"{label}PromptTable", 2, ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.SizingStretchProp, tableSize))
        {
            ImGui.TableSetupColumn("Prompt");
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 82 * ImGuiHelpers.GlobalScale);
            ImGui.TableHeadersRow();

            for (var i = 0; i < prompts.Count; i++)
            {
                ImGui.PushID($"{label}{i}");
                ImGui.TableNextRow();
                ImGui.TableNextColumn();

                var prompt = prompts[i];
                ImGui.SetNextItemWidth(-1);
                if (ImGui.InputText("##Prompt", ref prompt, 1024))
                {
                    prompts[i] = prompt;
                    saveConfiguration();
                }

                ImGui.TableNextColumn();
                if (ImGui.Button("Delete", new Vector2(-1, 0)))
                {
                    prompts.RemoveAt(i);
                    saveConfiguration();
                    ImGui.PopID();
                    i--;
                    continue;
                }

                ImGui.PopID();
            }

            ImGui.EndTable();
        }

        ImGui.Spacing();
        ImGui.SetNextItemWidth(-1);
        ImGui.InputText($"New {label}", ref newPrompt, 1024);

        if (ImGui.Button($"Add {label}") && !string.IsNullOrWhiteSpace(newPrompt))
        {
            prompts.Add(newPrompt.Trim());
            newPrompt = string.Empty;
            saveConfiguration();
        }
    }

    private void DrawSetManager(Vector2 size)
    {
        BeginPanel("Sets Manager", size);

        if (ImGui.BeginTable("RollTrackerSetManagerTable", 3, ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingStretchProp))
        {
            ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthFixed, 48 * ImGuiHelpers.GlobalScale);
            ImGui.TableSetupColumn("Set Name");
            ImGui.TableSetupColumn("State", ImGuiTableColumnFlags.WidthFixed, 48 * ImGuiHelpers.GlobalScale);
            ImGui.TableHeadersRow();

            DrawSetManagerRows("Truth", configuration.TruthPromptSets);
            DrawSetManagerRows("Dare", configuration.DarePromptSets);

            ImGui.EndTable();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        DrawSectionTitle("Active Set Preview");
        DrawKeyValue("Truth prompts", CountEnabledPrompts(configuration.TruthPromptSets).ToString(), AccentColor);
        DrawKeyValue("Dare prompts", CountEnabledPrompts(configuration.DarePromptSets).ToString(), AccentColor);
        ImGui.Spacing();
        ImGui.TextWrapped("Click a set name above to enable or disable it for random prompt rotation.");

        EndPanel();
    }

    private void DrawSetManagerRows(string type, List<TodPromptSet> promptSets)
    {
        for (var i = 0; i < promptSets.Count; i++)
        {
            var promptSet = promptSets[i];
            var setName = string.IsNullOrWhiteSpace(promptSet.Name) ? $"Set {i + 1}" : promptSet.Name.Trim();
            var enabled = promptSet.Enabled;
            var textColor = enabled ? SuccessColor : MutedColor;

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TextDisabled(type);
            ImGui.TableNextColumn();
            ImGui.PushStyleColor(ImGuiCol.Text, textColor);
            ImGui.TextWrapped(setName);
            if (ImGui.IsItemClicked())
            {
                promptSet.Enabled = !enabled;
                promptSets[i] = promptSet;
                saveConfiguration();
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(enabled ? "Click to disable this set." : "Click to enable this set.");
            }

            ImGui.PopStyleColor();
            ImGui.TableNextColumn();
            ImGui.TextColored(textColor, enabled ? "On" : "Off");
        }
    }

    private static int CountEnabledPrompts(IEnumerable<TodPromptSet> promptSets)
    {
        return promptSets
            .Where(promptSet => promptSet.Enabled)
            .Sum(promptSet => promptSet.Prompts?.Count ?? 0);
    }

    private void DrawSpecialRulesTab()
    {
        EnsureSpecialRuleSets();

        var available = ImGui.GetContentRegionAvail();
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var managerWidth = Math.Max(220 * ImGuiHelpers.GlobalScale, available.X * 0.22f);
        var editorWidth = Math.Max(430 * ImGuiHelpers.GlobalScale, available.X - managerWidth - spacing);

        BeginPanel("Special rule library", new Vector2(editorWidth, 0));
        DrawSpecialRuleSetToolbar("Modern", ref selectedSpecialRuleSetIndex);
        ImGui.Spacing();
        DrawSpecialRuleSetEditor("Modern", selectedSpecialRuleSetIndex, legacyStyle: false);
        EndPanel();

        ImGui.SameLine();
        DrawSpecialRuleSetManager(new Vector2(managerWidth, 0), legacyStyle: false);
    }

    private void EnsureSpecialRuleSets()
    {
        configuration.TodSpecialRuleSets ??= [];
        if (configuration.TodSpecialRuleSets.Count == 0)
        {
            configuration.TodSpecialRuleSets.Add(new TodSpecialRuleSet
            {
                Name = "Set 1",
                Enabled = true,
                Rules = configuration.TodSpecialRules.Count > 0
                    ? [.. configuration.TodSpecialRules.Select(CloneSpecialRule)]
                    : Configuration.CreateDefaultTodSpecialRules(),
            });
            saveConfiguration();
        }

        selectedSpecialRuleSetIndex = Math.Clamp(selectedSpecialRuleSetIndex, 0, configuration.TodSpecialRuleSets.Count - 1);
    }

    private void DrawSpecialRuleSetToolbar(string id, ref int selectedSetIndex)
    {
        var ruleSets = configuration.TodSpecialRuleSets;
        selectedSetIndex = Math.Clamp(selectedSetIndex, 0, ruleSets.Count - 1);
        var selectedSet = ruleSets[selectedSetIndex];
        var selectedName = GetSpecialRuleSetName(selectedSet, selectedSetIndex);

        ImGui.TextUnformatted("Active Set:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(Math.Max(130 * ImGuiHelpers.GlobalScale, ImGui.GetContentRegionAvail().X * 0.28f));
        if (ImGui.BeginCombo($"##{id}SpecialRuleActiveSet", selectedName))
        {
            for (var i = 0; i < ruleSets.Count; i++)
            {
                var name = GetSpecialRuleSetName(ruleSets[i], i);
                if (ImGui.Selectable($"{name}##{id}SpecialRuleCombo{i}", selectedSetIndex == i))
                {
                    selectedSetIndex = i;
                }
            }

            ImGui.EndCombo();
        }

        ImGui.SameLine();
        if (ImGui.Button($"New Set##{id}AddSpecialRuleSet"))
        {
            ruleSets.Add(new TodSpecialRuleSet
            {
                Name = $"Set {ruleSets.Count + 1}",
                Enabled = true,
                Rules = [],
            });
            selectedSetIndex = ruleSets.Count - 1;
            saveConfiguration();
        }

        ImGui.SameLine();
        if (ImGui.Button($"Duplicate##{id}DuplicateSpecialRuleSet"))
        {
            ruleSets.Add(new TodSpecialRuleSet
            {
                Name = $"{selectedName} Copy",
                Enabled = selectedSet.Enabled,
                Rules = selectedSet.Rules is null ? [] : [.. selectedSet.Rules.Select(CloneSpecialRule)],
            });
            selectedSetIndex = ruleSets.Count - 1;
            saveConfiguration();
        }

        ImGui.SameLine();
        if (ImGui.Button($"Rename##{id}RenameSpecialRuleSet"))
        {
            editingSpecialRuleSetIndex = selectedSetIndex;
            editingSpecialRuleSetName = selectedSet.Name;
        }

        if (ruleSets.Count > 1)
        {
            ImGui.SameLine();
            if (ImGui.Button($"Delete##{id}DeleteSpecialRuleSet"))
            {
                ruleSets.RemoveAt(selectedSetIndex);
                selectedSetIndex = Math.Clamp(selectedSetIndex, 0, ruleSets.Count - 1);
                editingSpecialRuleSetIndex = -1;
                editingSpecialRuleSetName = string.Empty;
                saveConfiguration();
            }
        }
    }

    private void DrawSpecialRuleSetEditor(string id, int selectedSetIndex, bool legacyStyle)
    {
        var ruleSet = configuration.TodSpecialRuleSets[selectedSetIndex];
        ruleSet.Rules ??= [];

        ImGui.TextColored(ruleSet.Enabled ? SuccessColor : MutedColor, ruleSet.Enabled ? "Enabled in Sets Manager" : "Disabled in Sets Manager");

        if (editingSpecialRuleSetIndex == selectedSetIndex)
        {
            ImGui.Spacing();
            ImGui.SetNextItemWidth(-1);
            ImGui.InputText($"Set name##{id}SpecialRuleSetName{selectedSetIndex}", ref editingSpecialRuleSetName, 128);

            if (ImGui.Button($"Save name##{id}SaveSpecialRuleSetName{selectedSetIndex}"))
            {
                ruleSet.Name = string.IsNullOrWhiteSpace(editingSpecialRuleSetName)
                    ? $"Set {selectedSetIndex + 1}"
                    : editingSpecialRuleSetName.Trim();
                configuration.TodSpecialRuleSets[selectedSetIndex] = ruleSet;
                editingSpecialRuleSetIndex = -1;
                editingSpecialRuleSetName = string.Empty;
                saveConfiguration();
            }

            ImGui.SameLine();

            if (ImGui.Button($"Cancel##{id}CancelSpecialRuleSetName{selectedSetIndex}"))
            {
                editingSpecialRuleSetIndex = -1;
                editingSpecialRuleSetName = string.Empty;
            }
        }

        ImGui.Spacing();
        DrawSpecialRulePlaceholderHints();
        ImGui.Spacing();

        var specialRuleLineDelay = configuration.TodSpecialRuleLineDelayMilliseconds;
        ImGui.SetNextItemWidth(160 * ImGuiHelpers.GlobalScale);
        if (DrawAdvancedInputInt($"Special rule line delay (ms)##{id}", ref specialRuleLineDelay))
        {
            configuration.TodSpecialRuleLineDelayMilliseconds = Math.Clamp(specialRuleLineDelay, 100, 10000);
            saveConfiguration();
        }
        DrawLineDelayTooltip("Delay before and between Special Rule result lines. Leave this alone unless chat lines are skipped.");

        ImGui.Spacing();
        DrawSpecialRuleTable(id, ruleSet.Rules, legacyStyle);
        DrawAddSpecialRule(id, ruleSet.Rules, legacyStyle);
        configuration.TodSpecialRuleSets[selectedSetIndex] = ruleSet;
    }

    private void DrawSpecialRuleTable(string id, List<TodSpecialRule> rules, bool legacyStyle)
    {
        var tableFlags = (legacyStyle ? ImGuiTableFlags.Borders : ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.ScrollY) |
                         ImGuiTableFlags.RowBg |
                         ImGuiTableFlags.SizingStretchProp;
        var tableSize = legacyStyle
            ? Vector2.Zero
            : new Vector2(0, Math.Max(210 * ImGuiHelpers.GlobalScale, ImGui.GetContentRegionAvail().Y - 110 * ImGuiHelpers.GlobalScale));

        if (!ImGui.BeginTable($"RollTracker{id}SpecialRulesTable", 4, tableFlags, tableSize))
        {
            return;
        }

        ImGui.TableSetupColumn("Roll", ImGuiTableColumnFlags.WidthFixed, 80 * ImGuiHelpers.GlobalScale);
        ImGui.TableSetupColumn(legacyStyle ? "Text" : "Result text");
        ImGui.TableSetupColumn("Do not trigger with", ImGuiTableColumnFlags.WidthFixed, legacyStyle ? 140 * ImGuiHelpers.GlobalScale : 150 * ImGuiHelpers.GlobalScale);
        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, legacyStyle ? 75 * ImGuiHelpers.GlobalScale : 82 * ImGuiHelpers.GlobalScale);
        ImGui.TableHeadersRow();

        for (var i = 0; i < rules.Count; i++)
        {
            ImGui.PushID($"{id}SpecialRule{i}");
            ImGui.TableNextRow();

            ImGui.TableNextColumn();
            var roll = rules[i].Roll;
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputInt("##Roll", ref roll))
            {
                rules[i].Roll = Math.Clamp(roll, 0, 9999);
                saveConfiguration();
            }

            ImGui.TableNextColumn();
            var text = rules[i].Text;
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputText("##Text", ref text, 1024))
            {
                rules[i].Text = text;
                saveConfiguration();
            }

            ImGui.TableNextColumn();
            var doNotTriggerWith = rules[i].DoNotTriggerWith;
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputText("##DoNotTriggerWith", ref doNotTriggerWith, 256))
            {
                rules[i].DoNotTriggerWith = doNotTriggerWith;
                saveConfiguration();
            }

            ImGui.TableNextColumn();
            if (ImGui.Button("Delete", new Vector2(-1, 0)))
            {
                rules.RemoveAt(i);
                saveConfiguration();
                ImGui.PopID();
                i--;
                continue;
            }

            ImGui.PopID();
        }

        ImGui.EndTable();
    }

    private void DrawAddSpecialRule(string id, List<TodSpecialRule> rules, bool legacyStyle)
    {
        ImGui.Spacing();
        if (legacyStyle)
        {
            ImGui.SetNextItemWidth(90 * ImGuiHelpers.GlobalScale);
            ImGui.InputInt($"New roll##{id}", ref newSpecialRuleRoll);
            newSpecialRuleRoll = Math.Clamp(newSpecialRuleRoll, 0, 9999);

            ImGui.SetNextItemWidth(-1);
            ImGui.InputText($"New text##{id}", ref newSpecialRuleText, 1024);

            if (ImGui.Button($"Add rule##{id}") && !string.IsNullOrWhiteSpace(newSpecialRuleText))
            {
                AddSpecialRule(rules);
            }

            return;
        }

        if (ImGui.BeginTable($"RollTracker{id}AddSpecialRule", 3, ImGuiTableFlags.SizingStretchProp))
        {
            ImGui.TableSetupColumn("Roll", ImGuiTableColumnFlags.WidthFixed, 95 * ImGuiHelpers.GlobalScale);
            ImGui.TableSetupColumn("Text");
            ImGui.TableSetupColumn("Action", ImGuiTableColumnFlags.WidthFixed, 92 * ImGuiHelpers.GlobalScale);
            ImGui.TableNextRow();

            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(-1);
            ImGui.InputInt($"##NewRoll{id}", ref newSpecialRuleRoll);
            newSpecialRuleRoll = Math.Clamp(newSpecialRuleRoll, 0, 9999);

            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(-1);
            ImGui.InputText($"##NewText{id}", ref newSpecialRuleText, 1024);

            ImGui.TableNextColumn();
            if (ImGui.Button($"Add rule##{id}", new Vector2(-1, 0)) && !string.IsNullOrWhiteSpace(newSpecialRuleText))
            {
                AddSpecialRule(rules);
            }

            ImGui.EndTable();
        }
    }

    private void AddSpecialRule(List<TodSpecialRule> rules)
    {
        rules.Add(new TodSpecialRule
        {
            Roll = newSpecialRuleRoll,
            Text = newSpecialRuleText.Trim(),
        });
        newSpecialRuleText = string.Empty;
        saveConfiguration();
    }

    private void DrawSpecialRuleSetManager(Vector2 size, bool legacyStyle)
    {
        if (legacyStyle)
        {
            ImGui.BeginChild("##LegacySpecialRuleSetManager", size, true, ImGuiWindowFlags.None);
            ImGui.TextUnformatted("Sets Manager");
            ImGui.Separator();
        }
        else
        {
            BeginPanel("Sets Manager", size);
        }

        if (ImGui.BeginTable($"RollTracker{(legacyStyle ? "Legacy" : "Modern")}SpecialRuleSetManagerTable", 3, ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingStretchProp))
        {
            ImGui.TableSetupColumn("Set Name");
            ImGui.TableSetupColumn("State", ImGuiTableColumnFlags.WidthFixed, 48 * ImGuiHelpers.GlobalScale);
            ImGui.TableSetupColumn("Rules", ImGuiTableColumnFlags.WidthFixed, 54 * ImGuiHelpers.GlobalScale);
            ImGui.TableHeadersRow();

            DrawSpecialRuleSetManagerRows(legacyStyle);

            ImGui.EndTable();
        }

        ImGui.Spacing();
        ImGui.Separator();
        if (legacyStyle)
        {
            ImGui.TextUnformatted("Active Set Preview");
            DrawLegacyCountLine("Rules", CountEnabledSpecialRules(configuration.TodSpecialRuleSets));
            ImGui.TextWrapped("Click a set name to enable or disable it.");
            ImGui.EndChild();
        }
        else
        {
            DrawSectionTitle("Active Set Preview");
            DrawKeyValue("Rules", CountEnabledSpecialRules(configuration.TodSpecialRuleSets).ToString(), AccentColor);
            ImGui.Spacing();
            ImGui.TextWrapped("Click a set name above to enable or disable it for special rule checks.");
            EndPanel();
        }
    }

    private void DrawSpecialRuleSetManagerRows(bool legacyStyle)
    {
        for (var i = 0; i < configuration.TodSpecialRuleSets.Count; i++)
        {
            var ruleSet = configuration.TodSpecialRuleSets[i];
            var setName = GetSpecialRuleSetName(ruleSet, i);
            var enabled = ruleSet.Enabled;
            var textColor = enabled ? SuccessColor : MutedColor;

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.PushStyleColor(ImGuiCol.Text, textColor);
            if (ImGui.Selectable($"{setName}##{(legacyStyle ? "Legacy" : "Modern")}SpecialRuleSetManager{i}", enabled, ImGuiSelectableFlags.SpanAllColumns))
            {
                ruleSet.Enabled = !enabled;
                configuration.TodSpecialRuleSets[i] = ruleSet;
                saveConfiguration();
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(enabled ? "Click to disable this set." : "Click to enable this set.");
            }

            ImGui.PopStyleColor();
            ImGui.TableNextColumn();
            ImGui.TextColored(textColor, enabled ? "On" : "Off");
            ImGui.TableNextColumn();
            ImGui.TextColored(textColor, (ruleSet.Rules?.Count ?? 0).ToString());
        }
    }

    private static int CountEnabledSpecialRules(IEnumerable<TodSpecialRuleSet> ruleSets)
    {
        return ruleSets
            .Where(ruleSet => ruleSet.Enabled)
            .Sum(ruleSet => ruleSet.Rules?.Count ?? 0);
    }

    private static string GetSpecialRuleSetName(TodSpecialRuleSet ruleSet, int index)
    {
        return string.IsNullOrWhiteSpace(ruleSet.Name) ? $"Set {index + 1}" : ruleSet.Name.Trim();
    }

    private static TodSpecialRule CloneSpecialRule(TodSpecialRule rule)
    {
        return new TodSpecialRule
        {
            Roll = rule.Roll,
            Text = rule.Text,
            DoNotTriggerWith = rule.DoNotTriggerWith,
        };
    }

    private void DrawWifiTab()
    {
        BeginPanel("Shell Infos", Vector2.Zero);
        DrawStatusPill("Macro", rollTrackerService.IsWifiMacroRunning);
        ImGui.SameLine();
        DrawChatChannelCombo("Chat", configuration.WifiChatChannel, channel => configuration.WifiChatChannel = channel);

        ImGui.Spacing();
        var wifiLineDelay = configuration.WifiMacroLineDelayMilliseconds;
        ImGui.SetNextItemWidth(170 * ImGuiHelpers.GlobalScale);
        if (DrawAdvancedInputInt("Line delay (ms)##Wifi", ref wifiLineDelay))
        {
            configuration.WifiMacroLineDelayMilliseconds = Math.Clamp(wifiLineDelay, 100, 10000);
            saveConfiguration();
        }
        DrawLineDelayTooltip("Delay between !wifi chat lines. Leave this alone unless chat lines are skipped.");

        ImGui.SetNextItemWidth(-1);
        var wifiMacroText = configuration.WifiMacroText;
        if (ImGui.InputTextMultiline("Macro##Wifi", ref wifiMacroText, 4096, new Vector2(0, Math.Max(220 * ImGuiHelpers.GlobalScale, ImGui.GetContentRegionAvail().Y - 42 * ImGuiHelpers.GlobalScale))))
        {
            configuration.WifiMacroText = wifiMacroText;
            saveConfiguration();
        }

        if (ImGui.Button("Run !wifi", new Vector2(140 * ImGuiHelpers.GlobalScale, 0)))
        {
            rollTrackerService.StartWifiMacro("manual");
        }
        EndPanel();
    }

    private void DrawCommandHelpTab()
    {
        BeginPanel("Command Help", Vector2.Zero);
        DrawCommandHelpContent("Modern", legacyStyle: false);
        EndPanel();
    }

    private void DrawChatAliasTab()
    {
        BeginPanel("Chat Alias", Vector2.Zero);
        DrawChatAliasContent("Modern", legacyStyle: false);
        EndPanel();
    }

    private void DrawChatAliasContent(string id, bool legacyStyle)
    {
        configuration.ChatAliasCommands ??= [];

        var aliasWord = configuration.ChatAliasWord;
        ImGui.SetNextItemWidth(180 * ImGuiHelpers.GlobalScale);
        if (ImGui.InputText($"Alias word##{id}ChatAliasWord", ref aliasWord, 64))
        {
            configuration.ChatAliasWord = string.IsNullOrWhiteSpace(aliasWord) ? "alias" : aliasWord.Trim();
            saveConfiguration();
        }
        DrawHelpTooltip("Players must type this word before the alias command text.");

        ImGui.Spacing();
        DrawChatAliasAddRow(id);
        ImGui.Separator();
        DrawChatAliasCommandTable(id, legacyStyle);
    }

    private void DrawChatAliasAddRow(string id)
    {
        selectedChatAliasCommandIndex = Math.Clamp(selectedChatAliasCommandIndex, 0, ChatAliasCommandOptions.Length - 1);
        var selectedCommand = ChatAliasCommandOptions[selectedChatAliasCommandIndex];

        if (!ImGui.BeginTable($"RollTracker{id}ChatAliasAddRow", 3, ImGuiTableFlags.SizingStretchProp))
        {
            return;
        }

        ImGui.TableSetupColumn("RT command", ImGuiTableColumnFlags.WidthFixed, 220 * ImGuiHelpers.GlobalScale);
        ImGui.TableSetupColumn("Typed text");
        ImGui.TableSetupColumn("Action", ImGuiTableColumnFlags.WidthFixed, 92 * ImGuiHelpers.GlobalScale);
        ImGui.TableNextRow();

        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth(-1);
        if (ImGui.BeginCombo($"##{id}ChatAliasCommand", selectedCommand.Label))
        {
            for (var i = 0; i < ChatAliasCommandOptions.Length; i++)
            {
                var option = ChatAliasCommandOptions[i];
                if (ImGui.Selectable($"{option.Label}##{id}ChatAliasCommandOption{i}", selectedChatAliasCommandIndex == i))
                {
                    selectedChatAliasCommandIndex = i;
                }
            }

            ImGui.EndCombo();
        }

        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth(-1);
        ImGui.InputText($"##{id}ChatAliasTrigger", ref newChatAliasTriggerText, 128);
        DrawHelpTooltip("The text players type after the alias word.");

        ImGui.TableNextColumn();
        if (ImGui.Button($"Add##{id}ChatAliasAdd", new Vector2(-1, 0)) &&
            !string.IsNullOrWhiteSpace(newChatAliasTriggerText))
        {
            configuration.ChatAliasCommands.Add(new ChatAliasCommand
            {
                Enabled = true,
                TriggerText = newChatAliasTriggerText.Trim(),
                RtCommandArgs = selectedCommand.Args,
            });
            newChatAliasTriggerText = string.Empty;
            saveConfiguration();
        }

        ImGui.EndTable();
    }

    private void DrawChatAliasCommandTable(string id, bool legacyStyle)
    {
        var tableFlags = (legacyStyle ? ImGuiTableFlags.Borders : ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.ScrollY) |
                         ImGuiTableFlags.RowBg |
                         ImGuiTableFlags.SizingStretchProp;
        var tableSize = legacyStyle
            ? Vector2.Zero
            : new Vector2(0, Math.Max(190 * ImGuiHelpers.GlobalScale, ImGui.GetContentRegionAvail().Y - 36 * ImGuiHelpers.GlobalScale));

        if (!ImGui.BeginTable($"RollTracker{id}ChatAliasTable", 5, tableFlags, tableSize))
        {
            return;
        }

        ImGui.TableSetupColumn("On", ImGuiTableColumnFlags.WidthFixed, 42 * ImGuiHelpers.GlobalScale);
        ImGui.TableSetupColumn("Chat input");
        ImGui.TableSetupColumn("Runs");
        ImGui.TableSetupColumn("Preview");
        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 82 * ImGuiHelpers.GlobalScale);
        ImGui.TableHeadersRow();

        for (var i = 0; i < configuration.ChatAliasCommands.Count; i++)
        {
            var aliasCommand = configuration.ChatAliasCommands[i];
            ImGui.PushID($"{id}ChatAlias{i}");
            ImGui.TableNextRow();

            ImGui.TableNextColumn();
            var enabled = aliasCommand.Enabled;
            if (ImGui.Checkbox("##Enabled", ref enabled))
            {
                aliasCommand.Enabled = enabled;
                configuration.ChatAliasCommands[i] = aliasCommand;
                saveConfiguration();
            }

            ImGui.TableNextColumn();
            var triggerText = aliasCommand.TriggerText;
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputText("##TriggerText", ref triggerText, 128))
            {
                aliasCommand.TriggerText = triggerText.Trim();
                configuration.ChatAliasCommands[i] = aliasCommand;
                saveConfiguration();
            }

            ImGui.TableNextColumn();
            var selectedIndex = Math.Max(0, Array.FindIndex(ChatAliasCommandOptions, option => option.Args.Equals(aliasCommand.RtCommandArgs, StringComparison.Ordinal)));
            var selectedLabel = ChatAliasCommandOptions[selectedIndex].Label;
            ImGui.SetNextItemWidth(-1);
            if (ImGui.BeginCombo("##Command", selectedLabel))
            {
                for (var optionIndex = 0; optionIndex < ChatAliasCommandOptions.Length; optionIndex++)
                {
                    var option = ChatAliasCommandOptions[optionIndex];
                    if (ImGui.Selectable($"{option.Label}##AliasRowCommand{optionIndex}", selectedIndex == optionIndex))
                    {
                        aliasCommand.RtCommandArgs = option.Args;
                        configuration.ChatAliasCommands[i] = aliasCommand;
                        saveConfiguration();
                    }
                }

                ImGui.EndCombo();
            }

            ImGui.TableNextColumn();
            var previewColor = aliasCommand.Enabled ? AccentColor : MutedColor;
            ImGui.TextColored(previewColor, aliasCommand.Enabled ? "Enabled" : "Disabled");
            ImGui.TextWrapped($"Player: {configuration.ChatAliasWord.Trim()} {aliasCommand.TriggerText}");
            ImGui.TextWrapped($"You: /rt {aliasCommand.RtCommandArgs}");

            ImGui.TableNextColumn();
            if (ImGui.Button("Delete", new Vector2(-1, 0)))
            {
                configuration.ChatAliasCommands.RemoveAt(i);
                saveConfiguration();
                ImGui.PopID();
                i--;
                continue;
            }

            ImGui.PopID();
        }

        ImGui.EndTable();
    }

    private void DrawCommandHelpContent(string id, bool legacyStyle)
    {
        configuration.HelpLines ??= [];
        if (configuration.HelpLines.Count == 0)
        {
            configuration.HelpLines.AddRange(Configuration.CreateDefaultHelpLines());
            saveConfiguration();
        }

        DrawChatChannelCombo("Chat", configuration.HelpChatChannel, channel => configuration.HelpChatChannel = channel);
        var helpPresetComboOpen = DrawHelpPresetCombo(id);

        var helpInitialDelay = configuration.HelpInitialDelayMilliseconds;
        ImGui.SetNextItemWidth(170 * ImGuiHelpers.GlobalScale);
        if (DrawAdvancedInputInt($"Initial delay (ms)##{id}HelpInitialDelay", ref helpInitialDelay))
        {
            configuration.HelpInitialDelayMilliseconds = Math.Clamp(helpInitialDelay, 0, 10000);
            saveConfiguration();
        }
        if (!helpPresetComboOpen)
        {
            DrawLineDelayTooltip("Delay before the first help line is sent.");
        }

        var helpLineDelay = configuration.HelpLineDelayMilliseconds;
        ImGui.SetNextItemWidth(170 * ImGuiHelpers.GlobalScale);
        if (DrawAdvancedInputInt($"Line delay (ms)##{id}HelpLineDelay", ref helpLineDelay))
        {
            configuration.HelpLineDelayMilliseconds = Math.Clamp(helpLineDelay, 100, 10000);
            saveConfiguration();
        }
        if (!helpPresetComboOpen)
        {
            DrawLineDelayTooltip("Delay between help chat lines.");
        }

        ImGui.Spacing();
        if (configuration.HelpPreset.Equals("Standard", StringComparison.Ordinal) &&
            ImGui.Button($"Reset default help##{id}HelpDefaults"))
        {
            configuration.HelpLines = Configuration.CreateDefaultHelpLines();
            saveConfiguration();
        }

        ImGui.Separator();
        DrawCommandHelpPresetPreview();
        ImGui.Separator();

        if (configuration.HelpPreset.Equals("Macro Mode", StringComparison.Ordinal) && configuration.AdvancedMode)
        {
            DrawCommandHelpMacroEditor(id);
        }
        else if (configuration.HelpPreset.Equals("Standard", StringComparison.Ordinal))
        {
            DrawCommandHelpLineEditor(id, legacyStyle);
        }
        else
        {
            DrawCommandHelpPresetPreview();
        }
    }

    private bool DrawHelpPresetCombo(string id)
    {
        var selectedPreset = HelpPresetNames.Contains(configuration.HelpPreset)
            ? configuration.HelpPreset
            : HelpPresetNames[0];

        var comboOpen = false;
        ImGui.SetNextItemWidth(230 * ImGuiHelpers.GlobalScale);
        if (ImGui.BeginCombo($"Help preset##{id}", selectedPreset))
        {
            comboOpen = true;
            foreach (var preset in HelpPresetNames)
            {
                var requiresAdvanced = preset.Equals("Macro Mode", StringComparison.Ordinal);
                if (requiresAdvanced && !configuration.AdvancedMode)
                {
                    ImGui.BeginDisabled();
                }

                if (ImGui.Selectable(preset, selectedPreset.Equals(preset, StringComparison.Ordinal)) &&
                    (!requiresAdvanced || configuration.AdvancedMode))
                {
                    configuration.HelpPreset = preset;
                    saveConfiguration();
                }

                if (requiresAdvanced && !configuration.AdvancedMode)
                {
                    DrawAdvancedModeOnlyTooltip("Enable Advanced mode in Settings to use Macro Mode.");
                    ImGui.EndDisabled();
                }
            }

            ImGui.EndCombo();
        }

        if (selectedPreset.Equals("Macro Mode", StringComparison.Ordinal) && !configuration.AdvancedMode)
        {
            configuration.HelpPreset = "Standard";
            saveConfiguration();
        }

        return comboOpen;
    }

    private void DrawCommandHelpPresetPreview()
    {
        DrawSectionTitle("Chat Preview");
        var chatCommand = GetChatCommand(configuration.HelpChatChannel);
        var previewLines = BuildCommandHelpPreviewLines();

        if (previewLines.Count == 0)
        {
            ImGui.TextDisabled("No help lines would be sent with the current settings.");
            return;
        }

        foreach (var line in previewLines)
        {
            ImGui.TextWrapped($"{chatCommand} {line}");
        }
    }

    private void DrawCommandHelpMacroEditor(string id)
    {
        DrawCommandHelpMacroHints();
        ImGui.Spacing();

        var helpMacroText = configuration.HelpMacroText;
        var isMacroTextEmpty = string.IsNullOrEmpty(helpMacroText);
        var macroInputSize = new Vector2(0, Math.Max(170 * ImGuiHelpers.GlobalScale, ImGui.GetContentRegionAvail().Y - 10 * ImGuiHelpers.GlobalScale));
        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputTextMultiline($"Macro##{id}HelpMacro", ref helpMacroText, 2048, macroInputSize))
        {
            configuration.HelpMacroText = helpMacroText;
            saveConfiguration();
        }

        if (isMacroTextEmpty)
        {
            DrawMultilineInputPlaceholder(HelpMacroPlaceholder);
        }
    }

    private List<string> BuildCommandHelpPreviewLines()
    {
        return configuration.HelpPreset switch
        {
            "Compact" =>
            [
                $"Commands: {string.Join("; ", GetHelpCommandInfos().Select(command => $"{command.Command} ({(command.Enabled ? "On" : "Off")})"))}",
            ],
            "Macro Mode" when configuration.AdvancedMode => BuildCommandHelpMacroPreviewLines(),
            _ => (configuration.HelpLines ?? Configuration.CreateDefaultHelpLines())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line => line.Trim())
                .Where(IsHelpLineAvailable)
                .ToList(),
        };
    }

    private List<string> BuildCommandHelpMacroPreviewLines()
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

    private void DrawCommandHelpLineEditor(string id, bool legacyStyle)
    {
        NormalizeStandardHelpLines();

        var tableFlags = (legacyStyle ? ImGuiTableFlags.Borders : ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.ScrollY) |
                         ImGuiTableFlags.RowBg |
                         ImGuiTableFlags.SizingStretchProp;
        var tableSize = legacyStyle
            ? Vector2.Zero
            : new Vector2(0, Math.Max(170 * ImGuiHelpers.GlobalScale, ImGui.GetContentRegionAvail().Y - 76 * ImGuiHelpers.GlobalScale));

        if (ImGui.BeginTable($"RollTracker{id}CommandHelpLines", 2, tableFlags, tableSize))
        {
            ImGui.TableSetupColumn("Command", ImGuiTableColumnFlags.WidthFixed, 78 * ImGuiHelpers.GlobalScale);
            ImGui.TableSetupColumn("Text");
            ImGui.TableHeadersRow();

            for (var i = 0; i < StandardHelpCommands.Length; i++)
            {
                var command = StandardHelpCommands[i];
                ImGui.PushID($"{id}HelpLine{i}");
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TextColored(AccentColor, command);
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Command trigger is fixed so module toggles can filter help lines correctly.");
                }

                ImGui.TableNextColumn();
                var description = GetHelpLineDescription(configuration.HelpLines[i], command);
                ImGui.SetNextItemWidth(-1);
                if (ImGui.InputText("##HelpLineText", ref description, 512))
                {
                    configuration.HelpLines[i] = BuildStandardHelpLine(command, description);
                    saveConfiguration();
                }

                ImGui.PopID();
            }

            ImGui.EndTable();
        }
    }

    private void NormalizeStandardHelpLines()
    {
        var defaultLines = Configuration.CreateDefaultHelpLines();
        var normalizedLines = new List<string>();

        for (var i = 0; i < StandardHelpCommands.Length; i++)
        {
            var command = StandardHelpCommands[i];
            var defaultLine = i < defaultLines.Count ? defaultLines[i] : $"{command} -";
            var sourceLine = configuration.HelpLines
                .FirstOrDefault(line => StartsWithHelpCommand(line, command)) ?? defaultLine;
            normalizedLines.Add(BuildStandardHelpLine(command, GetHelpLineDescription(sourceLine, command)));
        }

        if (!configuration.HelpLines.SequenceEqual(normalizedLines, StringComparer.Ordinal))
        {
            configuration.HelpLines = normalizedLines;
            saveConfiguration();
        }
    }

    private static string BuildStandardHelpLine(string command, string description)
    {
        description = description.Trim();
        return string.IsNullOrWhiteSpace(description)
            ? $"{command} -"
            : $"{command} - {description}";
    }

    private static string GetHelpLineDescription(string helpLine, string command)
    {
        var description = helpLine.Trim();
        if (StartsWithHelpCommand(description, command))
        {
            description = description[command.Length..].TrimStart();
        }

        description = description.TrimStart('-', ':', '=', ' ', '\t');
        return description.Trim();
    }

    private void DrawSettingsTab()
    {
        var available = ImGui.GetContentRegionAvail();
        var columnWidth = (available.X - ImGui.GetStyle().ItemSpacing.X) / 2f;

        BeginPanel("Modules", new Vector2(columnWidth, 0));
        DrawSectionTitle("Truth or Dare");
        DrawModuleToggle("Enable ToD", configuration.Enabled, rollTrackerService.SetEnabled);
        DrawModuleToggle("Enable ToD - Doubles", configuration.TodSecondPairEnabled, rollTrackerService.SetSecondPairEnabled);

        var todSpecialRulesEnabled = configuration.TodSpecialRulesEnabled;
        if (ImGui.Checkbox("Enable ToD special rules", ref todSpecialRulesEnabled))
        {
            configuration.TodSpecialRulesEnabled = todSpecialRulesEnabled;
            saveConfiguration();
        }

        ImGui.Spacing();
        DrawSectionTitle("Suggestions");
        DrawModuleToggle("Enable !truth", configuration.TruthTriggerEnabled, rollTrackerService.SetTruthTriggerEnabled);
        DrawModuleToggle("Enable !dare", configuration.DareTriggerEnabled, rollTrackerService.SetDareTriggerEnabled);

        ImGui.Spacing();
        DrawSectionTitle("Command Help");
        DrawModuleToggle("Enable !help", configuration.HelpTriggerEnabled, rollTrackerService.SetHelpTriggerEnabled);

        ImGui.Spacing();
        DrawSectionTitle("Chat Alias");
        DrawModuleToggle("Enable chat alias", configuration.ChatAliasEnabled, rollTrackerService.SetChatAliasEnabled);

        ImGui.Spacing();
        DrawSectionTitle("Wifi");
        DrawModuleToggle("Enable !wifi", configuration.WifiEnabled, rollTrackerService.SetWifiEnabled);
        EndPanel();

        ImGui.SameLine();

        BeginPanel("General", new Vector2(columnWidth, 0));
        DrawSectionTitle("Appearance");
        DrawUiLayoutCombo();
        DrawUiThemeCombo();
        DrawOpenChangelogButton();
        ImGui.Spacing();

        var autoDisableWhenLeavingHousing = configuration.AutoDisableWhenLeavingHousing;
        if (ImGui.Checkbox("Auto off outside house", ref autoDisableWhenLeavingHousing))
        {
            configuration.AutoDisableWhenLeavingHousing = autoDisableWhenLeavingHousing;
            saveConfiguration();
        }

        ImGui.Spacing();
        if (DrawSettingsButton("Open config folder"))
        {
            OpenConfigFolder();
        }

        ImGui.Spacing();
        DrawSectionTitle("Advanced");
        DrawAdvancedModeControls();

        ImGui.Spacing();
        DrawSectionTitle("Global controls");
        if (DrawSettingsButton("Enable all"))
        {
            rollTrackerService.SetAllModulesEnabled(true);
        }

        ImGui.SameLine();

        if (DrawSettingsButton("Disable all"))
        {
            rollTrackerService.SetAllModulesEnabled(false);
        }
        EndPanel();
    }

    private void DrawUiThemeCombo()
    {
        var selectedTheme = string.IsNullOrWhiteSpace(configuration.UiTheme)
            ? UiThemeNames[0]
            : configuration.UiTheme;

        ImGui.SetNextItemWidth(-1);
        if (ImGui.BeginCombo("UI design", selectedTheme))
        {
            foreach (var themeName in UiThemeNames)
            {
                var selected = selectedTheme.Equals(themeName, StringComparison.Ordinal);
                if (ImGui.Selectable(themeName, selected))
                {
                    configuration.UiTheme = themeName;
                    saveConfiguration();
                }

                if (selected)
                {
                    ImGui.SetItemDefaultFocus();
                }
            }

            ImGui.EndCombo();
        }
    }

    private void DrawOpenChangelogButton()
    {
        ImGui.Spacing();
        if (DrawSettingsButton("Open Changelog"))
        {
            openChangelogWindow();
        }
    }

    private void DrawAdvancedModeControls()
    {
        if (configuration.AdvancedMode)
        {
            ImGui.TextColored(WarningColor, "Advanced mode is enabled.");
            ImGui.SameLine();
            if (DrawSettingsButton("Turn off Advanced mode"))
            {
                configuration.AdvancedMode = false;
                saveConfiguration();
            }
            return;
        }

        if (DrawSettingsButton("Turn on Advanced mode"))
        {
            ImGui.OpenPopup("Enable Advanced Mode?##RollTrackerAdvancedMode");
        }

        DrawAdvancedModePopup();
    }

    private void DrawAdvancedModePopup()
    {
        var popupOpen = true;
        if (!ImGui.BeginPopupModal("Enable Advanced Mode?##RollTrackerAdvancedMode", ref popupOpen, ImGuiWindowFlags.AlwaysAutoResize))
        {
            return;
        }

        ImGui.TextColored(WarningColor, "Warning");
        ImGui.Separator();
        ImGui.TextWrapped("Advanced mode may expose settings that can make RollTracker stop working correctly if you change things without knowing what they do.");
        ImGui.Spacing();

        if (DrawSettingsButton("Enable Advanced mode"))
        {
            configuration.AdvancedMode = true;
            saveConfiguration();
            ImGui.CloseCurrentPopup();
        }

        ImGui.SameLine();

        if (DrawSettingsButton("Cancel"))
        {
            ImGui.CloseCurrentPopup();
        }

        ImGui.EndPopup();
    }

    private void DrawUiLayoutCombo()
    {
        var selectedLayout = string.IsNullOrWhiteSpace(configuration.UiLayout)
            ? UiLayoutNames[0]
            : configuration.UiLayout;

        ImGui.SetNextItemWidth(-1);
        if (ImGui.BeginCombo("UI layout", selectedLayout))
        {
            foreach (var layoutName in UiLayoutNames)
            {
                var selected = selectedLayout.Equals(layoutName, StringComparison.Ordinal);
                if (ImGui.Selectable(layoutName, selected))
                {
                    configuration.UiLayout = layoutName;
                    saveConfiguration();
                }

                if (selected)
                {
                    ImGui.SetItemDefaultFocus();
                }
            }

            ImGui.EndCombo();
        }
    }

    private void DrawRollTable(Vector2 size)
    {
        DrawSectionTitle("Roll history");

        var tableFlags = ImGuiTableFlags.BordersInnerV |
                         ImGuiTableFlags.RowBg |
                         ImGuiTableFlags.ScrollY |
                         ImGuiTableFlags.SizingStretchProp;

        if (!ImGui.BeginTable("RollTrackerRollsTable", 4, tableFlags, size))
        {
            return;
        }

        ImGui.TableSetupColumn("#", ImGuiTableColumnFlags.WidthFixed, 38 * ImGuiHelpers.GlobalScale);
        ImGui.TableSetupColumn("Time", ImGuiTableColumnFlags.WidthFixed, 75 * ImGuiHelpers.GlobalScale);
        ImGui.TableSetupColumn("Player");
        ImGui.TableSetupColumn("Roll", ImGuiTableColumnFlags.WidthFixed, 70 * ImGuiHelpers.GlobalScale);
        ImGui.TableHeadersRow();

        var sortedRolls = rollTrackerService.Rolls
            .OrderByDescending(roll => roll.Value)
            .ThenBy(roll => roll.Time)
            .ToList();
        for (var i = 0; i < sortedRolls.Count; i++)
        {
            var roll = sortedRolls[i];
            var rollColor = ReferenceEquals(roll, rollTrackerService.HighestRoll)
                ? SuccessColor
                : ReferenceEquals(roll, rollTrackerService.LowestRoll)
                    ? DangerColor
                    : MutedColor;

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TextDisabled((sortedRolls.Count - i).ToString());
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(roll.Time.ToLocalTime().ToString("HH:mm:ss"));
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(roll.PlayerName);
            ImGui.TableNextColumn();
            ImGui.TextColored(rollColor, roll.Value.ToString());
        }

        ImGui.EndTable();
    }

    private void DrawLegacyRollTable(Vector2 size)
    {
        var tableFlags = ImGuiTableFlags.Borders |
                         ImGuiTableFlags.RowBg |
                         ImGuiTableFlags.ScrollY |
                         ImGuiTableFlags.SizingStretchProp;

        if (!ImGui.BeginTable("RollTrackerLegacyRollsTable", 3, tableFlags, size))
        {
            return;
        }

        ImGui.TableSetupColumn("Player");
        ImGui.TableSetupColumn("Roll", ImGuiTableColumnFlags.WidthFixed, 70 * ImGuiHelpers.GlobalScale);
        ImGui.TableSetupColumn("Time", ImGuiTableColumnFlags.WidthFixed, 75 * ImGuiHelpers.GlobalScale);
        ImGui.TableHeadersRow();

        foreach (var roll in rollTrackerService.Rolls.OrderByDescending(roll => roll.Value))
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(roll.PlayerName);
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(roll.Value.ToString());
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(roll.Time.ToLocalTime().ToString("HH:mm:ss"));
        }

        ImGui.EndTable();
    }

    private void DrawChatChannelCombo(string label, string channel, Action<string> setChannel)
    {
        ImGui.SetNextItemWidth(160 * ImGuiHelpers.GlobalScale);
        if (ImGui.BeginCombo(label, channel))
        {
            foreach (var option in new[] { "Yell", "Say", "Party" })
            {
                var selected = channel.Equals(option, StringComparison.OrdinalIgnoreCase);
                if (ImGui.Selectable(option, selected))
                {
                    setChannel(option);
                    saveConfiguration();
                }

                if (selected)
                {
                    ImGui.SetItemDefaultFocus();
                }
            }

            ImGui.EndCombo();
        }
    }

    private static void DrawModuleToggle(string label, bool currentValue, Action<bool> setValue)
    {
        var value = currentValue;
        if (ImGui.Checkbox(label, ref value))
        {
            setValue(value);
        }
    }

    private static void DrawKeyValue(string label, string value, Vector4 valueColor)
    {
        ImGui.TextDisabled($"{label}:");
        ImGui.SameLine(95 * ImGuiHelpers.GlobalScale);
        ImGui.TextColored(valueColor, value);
    }

    private static void BeginPanel(string title, Vector2 size, bool drawTitle = true)
    {
        ImGui.PushStyleColor(ImGuiCol.ChildBg, PanelColor);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 7 * ImGuiHelpers.GlobalScale);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildBorderSize, 1);
        ImGui.BeginChild($"##Panel{title}", size, true, ImGuiWindowFlags.None);
        if (drawTitle)
        {
            DrawSectionTitle(title);
        }
    }

    private static void EndPanel()
    {
        ImGui.EndChild();
        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor();
    }

    private static void DrawSectionTitle(string title)
    {
        ImGui.TextColored(AccentColor, title);
    }

    private static void DrawStatusPill(string label, bool enabled)
    {
        ImGui.TextColored(enabled ? SuccessColor : MutedColor, $"{label}: {(enabled ? "on" : "off")}");
    }

    private static void DrawSettingsSection(string title)
    {
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.TextUnformatted(title);
        ImGui.Spacing();
    }

    private static bool DrawSettingsButton(string label)
    {
        return ImGui.Button(label, SettingsButtonSize * ImGuiHelpers.GlobalScale);
    }

    private static void DrawSummaryLine(string label, RollEntry? roll)
    {
        ImGui.TextUnformatted($"{label}:");
        ImGui.SameLine(95 * ImGuiHelpers.GlobalScale);

        if (roll is null)
        {
            ImGui.TextDisabled("none");
            return;
        }

        ImGui.TextUnformatted($"{roll.PlayerName} ({roll.Value})");
    }

    private static void DrawStatCard(string label, string value, Vector4 valueColor)
    {
        ImGui.TextDisabled(label);
        ImGui.TextColored(valueColor, value);
    }

    private static void ApplyUiTheme(string themeName)
    {
        switch (themeName)
        {
            case "Dalamud Night":
                AccentColor = new Vector4(0.92f, 0.48f, 0.72f, 1.00f);
                SuccessColor = new Vector4(0.52f, 0.88f, 0.63f, 1.00f);
                WarningColor = new Vector4(1.00f, 0.74f, 0.38f, 1.00f);
                MutedColor = new Vector4(0.66f, 0.62f, 0.70f, 1.00f);
                DangerColor = new Vector4(1.00f, 0.36f, 0.42f, 1.00f);
                WindowBgColor = new Vector4(0.08f, 0.06f, 0.09f, 0.98f);
                PanelColor = new Vector4(0.11f, 0.08f, 0.12f, 0.94f);
                BorderColor = new Vector4(0.32f, 0.24f, 0.34f, 0.90f);
                FrameBgColor = new Vector4(0.16f, 0.12f, 0.18f, 1.00f);
                FrameBgHoveredColor = new Vector4(0.24f, 0.16f, 0.26f, 1.00f);
                ButtonColor = new Vector4(0.42f, 0.16f, 0.30f, 1.00f);
                ButtonHoveredColor = new Vector4(0.58f, 0.22f, 0.42f, 1.00f);
                ButtonActiveColor = new Vector4(0.32f, 0.12f, 0.24f, 1.00f);
                break;

            case "Emerald":
                AccentColor = new Vector4(0.36f, 0.86f, 0.68f, 1.00f);
                SuccessColor = new Vector4(0.45f, 0.92f, 0.56f, 1.00f);
                WarningColor = new Vector4(0.96f, 0.76f, 0.34f, 1.00f);
                MutedColor = new Vector4(0.60f, 0.68f, 0.66f, 1.00f);
                DangerColor = new Vector4(1.00f, 0.36f, 0.34f, 1.00f);
                WindowBgColor = new Vector4(0.05f, 0.08f, 0.08f, 0.98f);
                PanelColor = new Vector4(0.07f, 0.12f, 0.11f, 0.94f);
                BorderColor = new Vector4(0.18f, 0.34f, 0.30f, 0.90f);
                FrameBgColor = new Vector4(0.10f, 0.18f, 0.17f, 1.00f);
                FrameBgHoveredColor = new Vector4(0.14f, 0.26f, 0.23f, 1.00f);
                ButtonColor = new Vector4(0.09f, 0.36f, 0.30f, 1.00f);
                ButtonHoveredColor = new Vector4(0.12f, 0.48f, 0.40f, 1.00f);
                ButtonActiveColor = new Vector4(0.07f, 0.28f, 0.24f, 1.00f);
                break;

            case "Graphite":
                AccentColor = new Vector4(0.78f, 0.84f, 0.92f, 1.00f);
                SuccessColor = new Vector4(0.52f, 0.82f, 0.58f, 1.00f);
                WarningColor = new Vector4(0.92f, 0.72f, 0.38f, 1.00f);
                MutedColor = new Vector4(0.62f, 0.65f, 0.70f, 1.00f);
                DangerColor = new Vector4(0.94f, 0.40f, 0.38f, 1.00f);
                WindowBgColor = new Vector4(0.07f, 0.08f, 0.09f, 0.98f);
                PanelColor = new Vector4(0.10f, 0.11f, 0.12f, 0.94f);
                BorderColor = new Vector4(0.28f, 0.30f, 0.34f, 0.90f);
                FrameBgColor = new Vector4(0.15f, 0.16f, 0.18f, 1.00f);
                FrameBgHoveredColor = new Vector4(0.22f, 0.23f, 0.26f, 1.00f);
                ButtonColor = new Vector4(0.22f, 0.25f, 0.30f, 1.00f);
                ButtonHoveredColor = new Vector4(0.32f, 0.36f, 0.42f, 1.00f);
                ButtonActiveColor = new Vector4(0.18f, 0.20f, 0.24f, 1.00f);
                break;

            default:
                AccentColor = new Vector4(0.42f, 0.72f, 1.00f, 1.00f);
                SuccessColor = new Vector4(0.46f, 0.86f, 0.58f, 1.00f);
                WarningColor = new Vector4(1.00f, 0.72f, 0.35f, 1.00f);
                MutedColor = new Vector4(0.62f, 0.66f, 0.72f, 1.00f);
                DangerColor = new Vector4(1.00f, 0.32f, 0.30f, 1.00f);
                WindowBgColor = new Vector4(0.06f, 0.08f, 0.09f, 0.98f);
                PanelColor = new Vector4(0.08f, 0.10f, 0.11f, 0.92f);
                BorderColor = new Vector4(0.22f, 0.26f, 0.30f, 0.90f);
                FrameBgColor = new Vector4(0.13f, 0.15f, 0.17f, 1.00f);
                FrameBgHoveredColor = new Vector4(0.18f, 0.22f, 0.26f, 1.00f);
                ButtonColor = new Vector4(0.12f, 0.28f, 0.48f, 1.00f);
                ButtonHoveredColor = new Vector4(0.17f, 0.38f, 0.64f, 1.00f);
                ButtonActiveColor = new Vector4(0.08f, 0.22f, 0.40f, 1.00f);
                break;
        }
    }

    private static void PushWindowStyle()
    {
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 3 * ImGuiHelpers.GlobalScale);
        ImGui.PushStyleVar(ImGuiStyleVar.GrabRounding, 3 * ImGuiHelpers.GlobalScale);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(8, 8) * ImGuiHelpers.GlobalScale);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(7, 6) * ImGuiHelpers.GlobalScale);
        ImGui.PushStyleColor(ImGuiCol.WindowBg, WindowBgColor);
        ImGui.PushStyleColor(ImGuiCol.Border, BorderColor);
        ImGui.PushStyleColor(ImGuiCol.FrameBg, FrameBgColor);
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, FrameBgHoveredColor);
        ImGui.PushStyleColor(ImGuiCol.Button, ButtonColor);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, ButtonHoveredColor);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, ButtonActiveColor);
        ImGui.PushStyleColor(ImGuiCol.Header, ButtonColor);
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, ButtonHoveredColor);
        ImGui.PushStyleColor(ImGuiCol.Tab, new Vector4(0.10f, 0.12f, 0.14f, 1.00f));
        ImGui.PushStyleColor(ImGuiCol.TabHovered, ButtonHoveredColor);
        ImGui.PushStyleColor(ImGuiCol.TabActive, ButtonColor);
    }

    private static void PopWindowStyle()
    {
        ImGui.PopStyleColor(12);
        ImGui.PopStyleVar(4);
    }

    private static string FormatRollSummary(RollEntry? roll)
    {
        return roll is null ? "None" : $"{roll.PlayerName} ({roll.Value})";
    }

    private static void DrawHelpTooltip(string text)
    {
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(text);
        }
    }

    private static void DrawSpecialRulePlaceholderHints()
    {
        ImGui.TextDisabled("Placeholders:");
        ImGui.SameLine();
        DrawPlaceholderHint("{player}", "Shows the name of the player who rolled the matching number.");
        ImGui.SameLine();
        DrawPlaceholderHint("{roll}", "Shows the rolled number that triggered this special rule.");
        ImGui.SameLine();
        ImGui.TextDisabled("Do not trigger with accepts numbers separated by commas or spaces.");
    }

    private static void DrawPlaceholderHint(string placeholder, string tooltip)
    {
        ImGui.TextColored(AccentColor, placeholder);
        DrawHelpTooltip(tooltip);
    }

    private static void DrawCommandHelpMacroHints()
    {
        ImGui.TextDisabled("Placeholders:");
        ImGui.SameLine();
        DrawPlaceholderHint("{activeCommands}", "Shows only commands that are currently enabled.");
        ImGui.SameLine();
        DrawPlaceholderHint("{commandStates}", "Shows every command with its current On or Off state.");

        ImGui.TextDisabled("Filters:");
        ImGui.SameLine();
        DrawPlaceholderHint("{!tod}", "Shows the following text segment only when !tod is enabled.");
        ImGui.SameLine();
        DrawPlaceholderHint("{!tod2}", "Shows the following text segment only when !tod2 is enabled.");
        ImGui.SameLine();
        DrawPlaceholderHint("{!truth}", "Shows the following text segment only when !truth is enabled.");
        ImGui.SameLine();
        DrawPlaceholderHint("{!dare}", "Shows the following text segment only when !dare is enabled.");
        ImGui.SameLine();
        DrawPlaceholderHint("{!wifi}", "Shows the following text segment only when !wifi is enabled.");
    }

    private static void DrawMultilineInputPlaceholder(string placeholder)
    {
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var padding = ImGui.GetStyle().FramePadding * ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        drawList.PushClipRect(min, max, true);
        drawList.AddText(min + padding, ImGui.GetColorU32(ImGuiCol.TextDisabled), placeholder);
        drawList.PopClipRect();
    }

    private bool DrawAdvancedInputInt(string label, ref int value)
    {
        if (configuration.AdvancedMode)
        {
            return ImGui.InputInt(label, ref value);
        }

        ImGui.BeginDisabled();
        ImGui.InputInt(label, ref value);
        ImGui.EndDisabled();
        DrawAdvancedModeOnlyTooltip("Enable Advanced mode in Settings to edit line delay fields.");
        return false;
    }

    private void DrawLineDelayTooltip(string text)
    {
        if (configuration.AdvancedMode)
        {
            DrawHelpTooltip(text);
            return;
        }

        DrawAdvancedModeOnlyTooltip("Enable Advanced mode in Settings to edit line delay fields.");
    }

    private static void DrawAdvancedModeOnlyTooltip(string text)
    {
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            ImGui.SetTooltip(text);
        }
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

    private static string GetChatCommand(string channel)
    {
        return channel switch
        {
            "Say" => "/s",
            "Party" => "/p",
            _ => "/y",
        };
    }

    private void OpenConfigFolder()
    {
        try
        {
            saveConfiguration();
            var configDirectory = pluginInterface.GetPluginConfigDirectory();
            Directory.CreateDirectory(configDirectory);
            Process.Start(new ProcessStartInfo
            {
                FileName = configDirectory,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            chatGui.PrintError($"Could not open config folder: {ex.Message}", "RollTracker");
        }
    }

    private enum Page
    {
        TruthOrDare,
        TruthDare,
        SpecialRules,
        CommandHelp,
        ChatAlias,
        Wifi,
        Settings,
    }

    private readonly record struct HelpCommandInfo(string Command, bool Enabled);
}

using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using RollTracker.Services;

namespace RollTracker.Windows;

internal sealed class MainWindow : Window, IDisposable
{
    private readonly RollTrackerService rollTrackerService;
    private readonly Configuration configuration;
    private readonly Action saveConfiguration;

    public MainWindow(RollTrackerService rollTrackerService, Configuration configuration, Action saveConfiguration)
        : base("RollTracker##RollTrackerMainWindow", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        this.rollTrackerService = rollTrackerService;
        this.configuration = configuration;
        this.saveConfiguration = saveConfiguration;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(420, 420),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }

    public void Dispose()
    {
    }

    public override void Draw()
    {
        if (!ImGui.BeginTabBar("RollTrackerTabs"))
        {
            return;
        }

        if (ImGui.BeginTabItem("Truth or Dare"))
        {
            DrawTruthOrDareTab();
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("!wifi"))
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

    private void DrawTruthOrDareTab()
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

        var duration = configuration.MacroDurationSeconds;
        if (ImGui.InputInt("Macro duration (s)", ref duration))
        {
            configuration.MacroDurationSeconds = Math.Clamp(duration, 1, 600);
            saveConfiguration();
        }

        var lineDelay = configuration.MacroLineDelayMilliseconds;
        if (ImGui.InputInt("Line delay (ms)", ref lineDelay))
        {
            configuration.MacroLineDelayMilliseconds = Math.Clamp(lineDelay, 100, 10000);
            saveConfiguration();
        }

        var macroText = configuration.MacroText;
        if (ImGui.InputTextMultiline("Macro", ref macroText, 4096, new Vector2(0, 90 * ImGuiHelpers.GlobalScale)))
        {
            configuration.MacroText = macroText;
            saveConfiguration();
        }

        var resultCommandTemplate = configuration.ResultCommandTemplate;
        if (ImGui.InputText("Result command", ref resultCommandTemplate, 512))
        {
            configuration.ResultCommandTemplate = resultCommandTemplate;
            saveConfiguration();
        }

        ImGui.Separator();

        var tableFlags = ImGuiTableFlags.Borders |
                         ImGuiTableFlags.RowBg |
                         ImGuiTableFlags.ScrollY |
                         ImGuiTableFlags.SizingStretchProp;

        var tableSize = new Vector2(0, 150 * ImGuiHelpers.GlobalScale);
        if (!ImGui.BeginTable("RollTrackerRollsTable", 3, tableFlags, tableSize))
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

    private void DrawWifiTab()
    {
        ImGui.TextUnformatted(rollTrackerService.IsWifiMacroRunning ? "Macro: running" : "Macro: idle");

        var channel = configuration.WifiChatChannel;
        if (ImGui.BeginCombo("Chat", channel))
        {
            foreach (var option in new[] { "Yell", "Say", "Party" })
            {
                var selected = channel.Equals(option, StringComparison.OrdinalIgnoreCase);
                if (ImGui.Selectable(option, selected))
                {
                    configuration.WifiChatChannel = option;
                    saveConfiguration();
                }

                if (selected)
                {
                    ImGui.SetItemDefaultFocus();
                }
            }

            ImGui.EndCombo();
        }

        var wifiMacroText = configuration.WifiMacroText;
        if (ImGui.InputTextMultiline("Macro##Wifi", ref wifiMacroText, 4096, new Vector2(0, 170 * ImGuiHelpers.GlobalScale)))
        {
            configuration.WifiMacroText = wifiMacroText;
            saveConfiguration();
        }

        if (ImGui.Button("Run !wifi"))
        {
            rollTrackerService.StartWifiMacro("manual");
        }
    }

    private void DrawSettingsTab()
    {
        var todEnabled = configuration.Enabled;
        if (ImGui.Checkbox("Enable ToD", ref todEnabled))
        {
            rollTrackerService.SetEnabled(todEnabled);
        }

        var todSpecialRulesEnabled = configuration.TodSpecialRulesEnabled;
        if (ImGui.Checkbox("Enable ToD special rules", ref todSpecialRulesEnabled))
        {
            configuration.TodSpecialRulesEnabled = todSpecialRulesEnabled;
            saveConfiguration();
        }

        var todSecondPairEnabled = configuration.TodSecondPairEnabled;
        if (ImGui.Checkbox("Enable ToD second pair", ref todSecondPairEnabled))
        {
            configuration.TodSecondPairEnabled = todSecondPairEnabled;
            saveConfiguration();
        }

        var wifiEnabled = configuration.WifiEnabled;
        if (ImGui.Checkbox("Enable Wifi", ref wifiEnabled))
        {
            rollTrackerService.SetWifiEnabled(wifiEnabled);
        }

        var autoDisableWhenLeavingHousing = configuration.AutoDisableWhenLeavingHousing;
        if (ImGui.Checkbox("Auto off outside house", ref autoDisableWhenLeavingHousing))
        {
            configuration.AutoDisableWhenLeavingHousing = autoDisableWhenLeavingHousing;
            saveConfiguration();
        }

        ImGui.Separator();

        if (ImGui.Button("Enable all"))
        {
            rollTrackerService.SetAllModulesEnabled(true);
        }

        ImGui.SameLine();

        if (ImGui.Button("Disable all"))
        {
            rollTrackerService.SetAllModulesEnabled(false);
        }
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
}

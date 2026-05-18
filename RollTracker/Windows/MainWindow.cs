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
        var highest = rollTrackerService.HighestRoll;
        var lowest = rollTrackerService.LowestRoll;
        var enabled = configuration.Enabled;

        if (ImGui.Checkbox("Enabled", ref enabled))
        {
            rollTrackerService.SetEnabled(enabled);
        }

        var autoDisableWhenLeavingHousing = configuration.AutoDisableWhenLeavingHousing;
        if (ImGui.Checkbox("Auto off outside house", ref autoDisableWhenLeavingHousing))
        {
            configuration.AutoDisableWhenLeavingHousing = autoDisableWhenLeavingHousing;
            saveConfiguration();
        }

        ImGui.SameLine();
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

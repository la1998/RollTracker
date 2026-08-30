using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using RollTracker.Services;

namespace RollTracker.Windows;

internal sealed class RollHistoryWindow : Window, IDisposable
{
    private readonly RollTrackerService rollTrackerService;

    public RollHistoryWindow(RollTrackerService rollTrackerService)
        : base("Roll History##RollTrackerRollHistoryWindow")
    {
        this.rollTrackerService = rollTrackerService;

        Flags = ImGuiWindowFlags.None;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(360, 260),
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

        ImGui.TextUnformatted($"Rolls: {rollTrackerService.Rolls.Count}");
        DrawSummaryLine("Highest", highest, new Vector4(0.46f, 0.86f, 0.58f, 1.00f));
        DrawSummaryLine("Lowest", lowest, new Vector4(1.00f, 0.32f, 0.30f, 1.00f));
        ImGui.Spacing();

        if (ImGui.Button("Reset rolls", new Vector2(130 * ImGuiHelpers.GlobalScale, 0)))
        {
            rollTrackerService.Reset();
        }

        ImGui.SameLine();

        if (ImGui.Button("End round and post result", new Vector2(190 * ImGuiHelpers.GlobalScale, 0)))
        {
            rollTrackerService.FinishRoundAndReset();
        }

        ImGui.Separator();

        var tableFlags = ImGuiTableFlags.BordersInnerV |
                         ImGuiTableFlags.RowBg |
                         ImGuiTableFlags.ScrollY |
                         ImGuiTableFlags.SizingStretchProp;
        var tableSize = new Vector2(0, Math.Max(180 * ImGuiHelpers.GlobalScale, ImGui.GetContentRegionAvail().Y));

        if (!ImGui.BeginTable("RollTrackerDetachedRollsTable", 4, tableFlags, tableSize))
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
            var rollColor = ReferenceEquals(roll, highest)
                ? new Vector4(0.46f, 0.86f, 0.58f, 1.00f)
                : ReferenceEquals(roll, lowest)
                    ? new Vector4(1.00f, 0.32f, 0.30f, 1.00f)
                    : new Vector4(0.62f, 0.66f, 0.72f, 1.00f);

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

    private static void DrawSummaryLine(string label, RollEntry? roll, Vector4 color)
    {
        ImGui.TextUnformatted($"{label}:");
        ImGui.SameLine(80 * ImGuiHelpers.GlobalScale);

        if (roll is null)
        {
            ImGui.TextDisabled("none");
            return;
        }

        ImGui.TextColored(color, $"{roll.PlayerName} ({roll.Value})");
    }
}

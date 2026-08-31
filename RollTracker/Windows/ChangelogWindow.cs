using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;

namespace RollTracker.Windows;

internal sealed class ChangelogWindow : Window, IDisposable
{
    private static readonly Vector4 AccentColor = new(0.42f, 0.72f, 1.00f, 1.00f);
    private static readonly Vector4 SuccessColor = new(0.46f, 0.86f, 0.58f, 1.00f);
    private static readonly Vector4 PanelColor = new(0.08f, 0.10f, 0.11f, 0.92f);

    private static readonly IReadOnlyList<ChangelogEntry> Entries =
    [
        new("0.1.0.49", """
            UI rework highlights:
            - Added selectable Modern, Standard, and Legacy layouts.
            - Added selectable themes: Dalamud Blue, Dalamud Night, Emerald, and Graphite.
            - Added Advanced Mode for technical settings and line-delay controls.
            - Added separate Roll History, Command Help, Chat Alias, and Changelog windows or tabs.
            - Reworked Truth or Dare round controls with clearer Normal ToD and Double ToD sections.
            - Added a Roll History window with roll count, highest roll, lowest roll, full table, and reset/end actions.
            - Added managers for Truth prompts, Dare prompts, and Special Rule Sets.
            - Special Rule Sets can be created, duplicated, renamed, deleted, enabled, and disabled.
            - Reworked !help with Standard, Compact, and Advanced Macro Mode presets plus chat preview.
            - Added configurable Chat Alias commands, including /rt on alias and /rt off alias.
            - Renamed !wifi UI to Shell Infos and moved technical delays behind Advanced Mode.
            - Added migration for old prompt lists and Special Rules into Set 1 while keeping existing saved config values.
            """),
        new("0.1.0.48", "Testing update: UI rework with selectable layouts, themes, Roll History window, Command Help, Chat Alias, Advanced Mode, and Special Rule Sets."),
        new("0.1.0.47", "Stable metadata bump so stable keeps the Random! duplicate roll fix while testing stays one version ahead."),
        new("0.1.0.46", "Bug fix: /random chat lines that include a leading Random! label no longer count as a second player entry."),
        new("0.1.0.44", "Stable update: adds delayed help output, separate macro delays, /rt help, config folder support, not-enough-player result texts, Truth/Dare prompt sets, Special Rule fixes, and Special Rule result delay controls."),
        new("0.1.0.43", "Testing update: separate !tod2 not-enough-player fallback, visible fallback commands, and delayed Special Rule result output."),
        new("0.1.0.42", "Testing bug fix: config files now save readable Unicode symbols instead of escaped sequences."),
        new("0.1.0.41", "Testing bug fix: RollTracker now stores and reads its active config from the plugin config folder."),
        new("0.1.0.39", "Testing update: safer help output timing, separate macro line delays, local /rt help, config folder shortcut, too-few-player result texts, and Truth/Dare prompt sets."),
        new("0.1.0.37", "Bug fix: default prompt and special rule lists no longer duplicate on future loads."),
        new("0.1.0.36", "Bug fix: duplicate Truth, Dare, and Special Rule entries are cleaned up on load."),
        new("0.1.0.35", "Bug fix: !truth and !dare now work from their own toggles even when ToD is off."),
        new("0.1.0.34", "Polished the Settings tab with clearer sections."),
        new("0.1.0.33", "Bug fix: Special Rules now check every roll in the round and the add form has one text field."),
        new("0.1.0.32", "Bug fix: Special Rules now use Do not trigger with so custom rules are not hidden by 0/1 weighting."),
        new("0.1.0.31", "Added repository cache-busting metadata for testing updates."),
        new("0.1.0.27", "Added an editable Special Rules tab and sends special rule texts as separate chat messages."),
        new("0.1.0.26", "Added this update changelog popup."),
        new("0.1.0.25", "/rt on tod turns !truth and !dare on; /rt off tod leaves them unchanged."),
        new("0.1.0.24", "Added /rt tod on and /rt tod off aliases."),
        new("0.1.0.23", "Updated !tod2 defaults and sends first pair and second pair results as separate chat messages."),
        new("0.1.0.22", "Added !help, independent !truth and !dare toggles, !tod2 rounds, and testing build metadata."),
    ];

    private readonly Configuration configuration;
    private readonly Action saveConfiguration;
    private readonly string currentVersion;

    public ChangelogWindow(Configuration configuration, Action saveConfiguration, string currentVersion)
        : base("RollTracker Changelog##RollTrackerChangelogWindow")
    {
        this.configuration = configuration;
        this.saveConfiguration = saveConfiguration;
        this.currentVersion = currentVersion;

        IsOpen = ShouldOpen();
        Flags = ImGuiWindowFlags.None;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(560, 430),
            MaximumSize = new Vector2(760, 760),
        };
    }

    public void Dispose()
    {
    }

    public override void Draw()
    {
        PushStyle();
        try
        {
            ImGui.TextColored(AccentColor, "RollTracker Changelog");
            ImGui.TextDisabled($"Updated to {currentVersion}");
            ImGui.Separator();
            DrawNavigationStrip();

            ImGui.Spacing();
            ImGui.TextColored(AccentColor, $"RollTracker {currentVersion}");
            ImGui.TextDisabled("Recent plugin changes and fixes");
            ImGui.Separator();

            var footerHeight = 38 * ImGuiHelpers.GlobalScale;
            if (ImGui.BeginChild("##RollTrackerChangelogEntries", new Vector2(0, Math.Max(160 * ImGuiHelpers.GlobalScale, ImGui.GetContentRegionAvail().Y - footerHeight)), true))
            {
                DrawEntries();
            }
            ImGui.EndChild();

            ImGui.Spacing();
            DrawFooter();
        }
        finally
        {
            PopStyle();
        }
    }

    private static void DrawNavigationStrip()
    {
        ImGui.PushStyleColor(ImGuiCol.ChildBg, PanelColor);
        if (ImGui.BeginChild("##RollTrackerChangelogStrip", new Vector2(0, 32 * ImGuiHelpers.GlobalScale), true))
        {
            var text = "Changelog";
            var textWidth = ImGui.CalcTextSize(text).X;
            ImGui.SetCursorPosX(Math.Max(0, (ImGui.GetContentRegionAvail().X - textWidth) * 0.5f));
            ImGui.TextColored(AccentColor, text);
        }
        ImGui.EndChild();
        ImGui.PopStyleColor();
    }

    private void DrawEntries()
    {
        var visibleEntries = GetVisibleEntries().ToList();
        if (visibleEntries.Count == 0)
        {
            ImGui.TextWrapped("No detailed changelog entries are available for this version.");
            return;
        }

        foreach (var entry in visibleEntries)
        {
            DrawEntry(entry);
        }
    }

    private static void DrawEntry(ChangelogEntry entry)
    {
        ImGui.TextColored(SuccessColor, entry.Version);
        ImGui.SameLine();
        ImGui.TextDisabled("RollTracker update");
        ImGui.Spacing();
        ImGui.Bullet();
        ImGui.SameLine();
        ImGui.TextWrapped(entry.Text);
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
    }

    private void DrawFooter()
    {
        var buttonWidth = 130 * ImGuiHelpers.GlobalScale;
        ImGui.SetCursorPosX(Math.Max(0, (ImGui.GetContentRegionAvail().X - buttonWidth) * 0.5f));
        if (ImGui.Button("Close", new Vector2(buttonWidth, 0)))
        {
            MarkSeenAndClose();
        }
    }

    private static void PushStyle()
    {
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(8, 8) * ImGuiHelpers.GlobalScale);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 3 * ImGuiHelpers.GlobalScale);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 4 * ImGuiHelpers.GlobalScale);
        ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0.06f, 0.08f, 0.09f, 0.98f));
        ImGui.PushStyleColor(ImGuiCol.ChildBg, PanelColor);
        ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(0.22f, 0.26f, 0.30f, 0.90f));
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.12f, 0.28f, 0.48f, 1.00f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.17f, 0.38f, 0.64f, 1.00f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.08f, 0.22f, 0.40f, 1.00f));
    }

    private static void PopStyle()
    {
        ImGui.PopStyleColor(6);
        ImGui.PopStyleVar(3);
    }

    private bool ShouldOpen()
    {
        return !string.Equals(configuration.LastSeenChangelogVersion, currentVersion, StringComparison.OrdinalIgnoreCase);
    }

    private IEnumerable<ChangelogEntry> GetVisibleEntries()
    {
        if (!Version.TryParse(configuration.LastSeenChangelogVersion, out var lastSeenVersion))
        {
            return Entries.Take(5);
        }

        return Entries.Where(entry =>
            Version.TryParse(entry.Version, out var entryVersion) &&
            entryVersion > lastSeenVersion);
    }

    private void MarkSeenAndClose()
    {
        configuration.LastSeenChangelogVersion = currentVersion;
        saveConfiguration();
        IsOpen = false;
    }

    private sealed record ChangelogEntry(string Version, string Text);
}

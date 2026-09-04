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
    private static readonly Version HistoryStartVersion = new(0, 1, 0, 58);

    private static readonly IReadOnlyList<ChangelogEntry> Entries =
    [
        new("0.1.0.65", """
- Added /rt history to open the Roll History window directly.
- Changed /rt history so it toggles the Roll History window closed again when it is already open.
- Added /rt history to the command help, README, and Chat Alias command picker.
- Improved the Moodles help window with clearer spacing between setup sections.
- Added Moodles folder-path guidance, including the Truth or Dare/ToD example for Moodles stored inside folders.
- Modernized the GitHub README so it focuses on installation, key features, main commands, chat triggers, Status Effects, and safety notes.
"""),
        new("0.1.0.64", """
- Added Status Effects support for Moodles, Honorific titles, and advanced custom macros.
- Added separate Moodles, Honorific, and Macro Mode sections with module-based triggers.
- Added automatic apply/remove handling when linked RollTracker modules are enabled or disabled.
- Added cleanup for active Moodles, Honorific titles, and macros when entries are disabled or the plugin unloads.
- Added Honorific priority handling so the highest-priority active title wins and lower-priority titles can take over when needed.
- Added color and glow pickers with clear buttons for Honorific entries.
- Added a Moodles help window with setup instructions and a copyable example import code.
- Added configurable Auto On and Auto Off settings, guarded behind Advanced Mode.
- Added an address book for Auto On, using reliable housing-interior addresses across data centers and worlds.
- Added separate Auto On and Auto Off subtabs.
- Added an optional setting to link !truth and !dare to !tod / !tod2.
- Improved fresh-install defaults so RollTracker starts with gameplay modules disabled.
- Improved Chat Alias feedback controls with a single all-feedback toggle.
- Improved Shell Infos placeholder text for venue-specific sync and Discord details.
- Moved housing debug info into a separate debug window opened from the title bar.
- Fixed Auto Off incorrectly firing during saved-address Auto On transitions.
- Fixed Status Effects not triggering when ToD special rules were toggled directly.
- Fixed module enable chat output so individual module toggles name the affected module.
- Fixed table layout, row coloring, resizing, and alignment issues in Auto On/Off and Status Effects.
"""),
        new("0.1.0.63", """
Status Effects UI:
- Added a Moodles help button next to the Moodle name column.
- Added a dedicated Moodles help window that explains how to create or import a Moodle before using it in RollTracker.
- Added a copy button for an example ToD Moodle import code.
- Made the example Moodle import code wrap inside a scrollable help panel.
"""),
        new("0.1.0.62", """
Settings and defaults:
- New installations now start with all RollTracker modules disabled, matching /rt off behavior.
- Auto On and Auto Off settings remain in the module settings list, but can only be changed while Advanced Mode is enabled.
- Updated the Shell Infos placeholder text for fresh or empty shell info configs.

Status Effects UI:
- Fixed the Moodle table row alignment after the Honorific priority layout update.
"""),
        new("0.1.0.61", """
Status Effects:
- Added support for Moodles, Honorific titles, and advanced custom macros.
- Added separate Moodles and Honorific tables with per-entry module selection.
- Added Honorific priority handling so multiple titles can be configured and the highest-priority active title is applied.
- Added Honorific color and glow pickers with clear support.
- Fixed Status Effects triggering from the ToD special rules settings toggle.
- Fixed delayed Status Effects execution during housing transitions.

Auto On improvements:
- Added a housing address book for Auto On.
- Saved housing interiors can now automatically turn selected modules back on when entered.
- Added separate Auto On affected modules so Auto On and Auto Off can manage different module sets.
- Added separate Enable Auto On and Enable Auto Off settings.
- Fixed Auto Off conflicts when entering or leaving saved Auto On addresses.
- Fixed housing address detection for interiors, FC rooms, private houses, and apartments across worlds/data centers.

UI and Chat Alias:
- Added separate Auto On and Auto Off tabs, settings, and affected-module selections.
- Added a debug info window opened from the title bar.
- Added configurable Chat Alias feedback, including delayed feedback messages and a global feedback toggle.
- Added an option to link !truth and !dare to !tod / !tod2.
- Changed displayed /rt command syntax to show on/off/toggle at the end.
- Changed default ToD suggestions and Shell Infos placeholders for fresh installs.
- Fixed Chat Alias /rt on and /rt toggle handling while Chat Alias is disabled when allowed in settings.
"""),
        new("0.1.0.60", """
Auto On improvements:
- Added a housing address book for Auto On.
- Saved housing interiors can now automatically turn selected modules back on when entered.
- Added separate Auto On affected modules so Auto On and Auto Off can manage different module sets.
- Added separate Enable Auto On and Enable Auto Off settings.
- Saved Auto On addresses skip the Entering house interior Auto Off trigger to avoid immediately turning modules off again.
- Added delayed Auto On matching after entering an interior so housing data can settle before matching the saved address.

Auto On/Off UI:
- Renamed the Advanced tab from Auto Off to Auto On/Off.
- Split Auto On and Auto Off into separate subtabs.
- Moved housing debug details out of the Auto On/Off tab.
- Added a title bar bug icon that opens a dedicated RollTracker Debug Info window.

Chat Alias improvements:
- Added one-click controls to turn all Chat Alias feedback toggles on or off while keeping individual row toggles editable.
"""),
        new("0.1.0.59", """
Chat Alias improvements:
- Added optional chat feedback for Chat Alias commands.
- Added a per-alias Feedback toggle so only selected aliases send a public status message.
- Added a Feedback Chat selector for choosing where alias feedback is sent.
- Alias feedback is delayed slightly so the original player message appears before the status response.
- Added /rt toggle commands for all modules and individual modules.
- Updated displayed command syntax to show /rt <module> on/off/toggle while keeping older command order supported.
- Added optional support for allowing enable/toggle aliases while Chat Alias is disabled.

Auto Off improvements:
- Added a dedicated Auto Off tab behind Advanced Mode.
- Added configurable Auto Off triggers for leaving a house, entering a house, leaving residential areas, and teleport/zone changes.
- Added module selection for choosing which features Auto Off disables.
- Improved housing and territory detection so house enter/leave and general zone changes can be handled separately.

UI improvements:
- Increased Truth or Dare macro input height so default macros are easier to read.
- Added horizontal scrolling to Truth or Dare macro fields.
- Made Chat Alias and Special Rules table rows use consistent colors.
- Added resizable table columns for Chat Alias and Special Rules.
- Fixed duplicate Chat Preview display in Compact command help mode.

Bug fixes:
- Fixed Chat Alias /rt on handling when Chat Alias was disabled and the wake option was enabled.
- Fixed /rt alias toggle and /rt toggle alias handling while Chat Alias is disabled when allowed in settings.
- Fixed displayed Chat Alias command labels falling back incorrectly for older saved command syntax.
"""),
        new("0.1.0.58", """
Chat Alias improvements:
- Added optional chat feedback for Chat Alias commands.
- Added a per-alias Feedback toggle so only selected aliases send a public status message.
- Added a Feedback Chat selector for choosing where alias feedback is sent.
- Alias feedback is delayed slightly so the original player message appears before the status response.
- Added /rt toggle commands for all modules and individual modules.
- Updated displayed command syntax to show /rt <module> on/off/toggle while keeping older command order supported.
- Added optional support for allowing enable/toggle aliases while Chat Alias is disabled.

Auto Off improvements:
- Added a dedicated Auto Off tab behind Advanced Mode.
- Added configurable Auto Off triggers for leaving a house, entering a house, leaving residential areas, and teleport/zone changes.
- Added module selection for choosing which features Auto Off disables.
- Improved housing and territory detection so house enter/leave and general zone changes can be handled separately.

UI improvements:
- Increased Truth or Dare macro input height so default macros are easier to read.
- Added horizontal scrolling to Truth or Dare macro fields.
- Made Chat Alias and Special Rules table rows use consistent colors.
- Added resizable table columns for Chat Alias and Special Rules.
- Fixed duplicate Chat Preview display in Compact command help mode.

Bug fixes:
- Fixed Chat Alias /rt on handling when Chat Alias was disabled and the wake option was enabled.
- Fixed /rt alias toggle and /rt toggle alias handling while Chat Alias is disabled when allowed in settings.
- Fixed displayed Chat Alias command labels falling back incorrectly for older saved command syntax.
"""),
    ];

    private readonly Configuration configuration;
    private readonly Action saveConfiguration;
    private readonly string currentVersion;
    private bool showHistory;
    private int selectedHistoryEntryIndex = -1;

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

    public void OpenHistory()
    {
        showHistory = true;
        selectedHistoryEntryIndex = -1;
        IsOpen = true;
    }

    public override void Draw()
    {
        PushStyle();
        try
        {
            ImGui.TextColored(AccentColor, "RollTracker Changelog");
            ImGui.TextDisabled(showHistory ? $"Installed version: {currentVersion}" : $"Updated to {currentVersion}");
            ImGui.Separator();
            DrawNavigationStrip();

            ImGui.Spacing();
            ImGui.TextColored(AccentColor, GetEntryHeaderText());
            ImGui.TextDisabled(showHistory ? "Browse current and older plugin changelogs" : "Recent plugin changes and fixes");
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

    private void DrawNavigationStrip()
    {
        ImGui.PushStyleColor(ImGuiCol.ChildBg, PanelColor);
        if (ImGui.BeginChild("##RollTrackerChangelogStrip", new Vector2(0, 32 * ImGuiHelpers.GlobalScale), true))
        {
            if (showHistory)
            {
                DrawHistorySelector();
            }
            else
            {
                var text = "Changelog";
                var textWidth = ImGui.CalcTextSize(text).X;
                ImGui.SetCursorPosX(Math.Max(0, (ImGui.GetContentRegionAvail().X - textWidth) * 0.5f));
                ImGui.TextColored(AccentColor, text);
            }
        }
        ImGui.EndChild();
        ImGui.PopStyleColor();
    }

    private void DrawHistorySelector()
    {
        var historyEntries = GetHistoryEntries().ToList();
        selectedHistoryEntryIndex = Math.Clamp(selectedHistoryEntryIndex, -1, historyEntries.Count - 1);

        ImGui.TextColored(AccentColor, "Version");
        ImGui.SameLine();

        var preview = selectedHistoryEntryIndex < 0
            ? "All versions"
            : historyEntries[Math.Clamp(selectedHistoryEntryIndex, 0, historyEntries.Count - 1)].Version;
        ImGui.SetNextItemWidth(Math.Max(180 * ImGuiHelpers.GlobalScale, ImGui.GetContentRegionAvail().X * 0.42f));
        if (!ImGui.BeginCombo("##RollTrackerChangelogHistoryVersion", preview))
        {
            return;
        }

        if (ImGui.Selectable("All versions", selectedHistoryEntryIndex < 0))
        {
            selectedHistoryEntryIndex = -1;
        }

        for (var i = 0; i < historyEntries.Count; i++)
        {
            var entry = historyEntries[i];
            if (ImGui.Selectable(entry.Version, selectedHistoryEntryIndex == i))
            {
                selectedHistoryEntryIndex = i;
            }
        }

        ImGui.EndCombo();
    }

    private string GetEntryHeaderText()
    {
        if (!showHistory || selectedHistoryEntryIndex < 0)
        {
            return $"RollTracker {currentVersion}";
        }

        var historyEntries = GetHistoryEntries().ToList();
        return historyEntries.Count == 0
            ? $"RollTracker {currentVersion}"
            : $"RollTracker {historyEntries[Math.Clamp(selectedHistoryEntryIndex, 0, historyEntries.Count - 1)].Version}";
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
        DrawEntryText(entry.Text);
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
    }

    private static void DrawEntryText(string text)
    {
        var lines = text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n');

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd();
            if (string.IsNullOrWhiteSpace(line))
            {
                ImGui.Spacing();
                continue;
            }

            var trimmed = line.TrimStart();
            var indent = line.Length - trimmed.Length;
            if (trimmed.StartsWith("- ", StringComparison.Ordinal))
            {
                ImGui.Indent(indent >= 2 ? 18 * ImGuiHelpers.GlobalScale : 0);
                ImGui.Bullet();
                ImGui.SameLine();
                ImGui.TextWrapped(trimmed[2..]);
                ImGui.Unindent(indent >= 2 ? 18 * ImGuiHelpers.GlobalScale : 0);
                continue;
            }

            ImGui.TextColored(AccentColor, trimmed.TrimEnd(':'));
        }
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
        if (showHistory)
        {
            var historyEntries = GetHistoryEntries().ToList();
            return selectedHistoryEntryIndex < 0
                ? historyEntries
                : [historyEntries[Math.Clamp(selectedHistoryEntryIndex, 0, historyEntries.Count - 1)]];
        }

        if (!Version.TryParse(configuration.LastSeenChangelogVersion, out var lastSeenVersion))
        {
            return GetHistoryEntries().Take(5);
        }

        return Entries.Where(entry =>
            Version.TryParse(entry.Version, out var entryVersion) &&
            Version.TryParse(currentVersion, out var currentEntryVersion) &&
            entryVersion >= HistoryStartVersion &&
            entryVersion <= currentEntryVersion &&
            entryVersion > lastSeenVersion);
    }

    private IEnumerable<ChangelogEntry> GetHistoryEntries()
    {
        return Entries.Where(entry =>
            Version.TryParse(entry.Version, out var entryVersion) &&
            Version.TryParse(currentVersion, out var currentEntryVersion) &&
            entryVersion >= HistoryStartVersion &&
            entryVersion <= currentEntryVersion);
    }

    private void MarkSeenAndClose()
    {
        configuration.LastSeenChangelogVersion = currentVersion;
        saveConfiguration();
        IsOpen = false;
    }

    private sealed record ChangelogEntry(string Version, string Text);
}


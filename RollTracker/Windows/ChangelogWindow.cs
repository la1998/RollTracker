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
    private static readonly Version HistoryStartVersion = new(0, 1, 0, 56);

    private static readonly IReadOnlyList<ChangelogEntry> Entries =
    [
        new("0.1.0.57", """
            Stable UI Rework Release
            
            Highlights
            - Added a full UI rework with selectable layouts.
            - Added selectable color themes for all layouts.
            - Added Advanced Mode for technical and risky settings.
            - Added separate Roll History, Command Help, Chat Alias, and Changelog windows/tabs.
            - Added set management for Truth prompts, Dare prompts, and Special Rules.
            - Added automatic config backup before plugin update migration.
            
            UI Layouts
            - Added selectable Modern, Standard, and Legacy layouts.
            - Modern uses a left sidebar navigation.
            - Standard keeps the classic top tab navigation.
            - Legacy keeps an original-style layout while supporting the new theme system.
            - New installs now start with the Standard layout by default.
            - Existing users keep their selected UI layout when updating.
            
            Themes
            - Added selectable themes:
              - Dalamud Blue
              - Dalamud Night
              - Emerald
              - Graphite
            - Legacy mode now also uses the selected theme colors.
            
            Truth Or Dare
            - Reworked the main Truth or Dare controls for better spacing and readability.
            - Added clearer Normal ToD Round and Double ToD Round sections.
            - Added field labels for Macro, Result command, Not enough players text, and Not enough second pair text in Modern and Standard layouts.
            - Added missing Not enough players text fields in Modern and Standard layouts.
            - Added missing Double ToD fallback fields in Modern and Standard layouts.
            - Added a separate !tod2 result line delay setting.
            - Multi-line !tod2 result commands are now sent with a delay between lines.
            - Fixed a critical issue where combat/action log events could be detected as /random rolls.
            - RollTracker now only tracks real RandomNumber log messages and normal /random chat output.
            
            Roll History
            - Moved Roll History out of the main Truth or Dare page for Modern and Standard layouts.
            - Added a separate Roll History window.
            - Roll History shows roll count, highest roll, lowest roll, and the full roll table.
            - Added high/low roll color feedback.
            - Added Reset rolls and End round and post result actions to the Roll History window.
            
            ToD Suggestions
            - Renamed Truth / Dare to ToD Suggestions.
            - Added a Sets Manager for Truth and Dare prompt sets.
            - Prompt sets can be enabled or disabled from the Sets Manager.
            - Added visual feedback for enabled and disabled sets.
            - Removed the old per-set enabled checkbox from the edit area.
            - Improved the Sets Manager layout for small windows.
            - Added horizontal scrolling so the Sets Manager stays reachable when the window is narrow.
            - Removed the Prompts column from the Sets Manager table.
            - Removed duplicate prompt count text above Truth and Dare prompt lists.
            - Set names in the Sets Manager now wrap instead of being squeezed unreadably.
            
            Special Rules
            - Added Special Rule Sets.
            - Existing special rules are migrated into Set 1.
            - Special Rule Sets can be created, duplicated, renamed, deleted, enabled, and disabled.
            - Only enabled Special Rule Sets are checked when roll results are posted.
            - Added a Special Rules Sets Manager in Modern, Standard, and Legacy layouts.
            - Removed the old single-list-only Special Rules behavior from the UI.
            - Moved Special rule line delay above the Special Rules table.
            - Made the Special Rules line delay editable only in Advanced Mode.
            - Added hover tooltips for Special Rules placeholders.
            - Removed {role} from the visible placeholder hint list.
            
            Command Help
            - Reworked !help into its own Command Help tab.
            - Added editable help text lines for the Standard preset.
            - Standard preset command triggers are now fixed and cannot be edited.
            - Only the help description text can be edited in the Standard preset.
            - !help now filters output by active and available modules again.
            - Added Help presets:
              - Standard
              - Compact
              - Macro Mode
            - Added a real Chat Preview showing what would be sent to chat.
            - Fixed duplicate Chat Preview display in Compact mode.
            - Compact sends one line with command states.
            - Macro Mode requires Advanced Mode.
            - Added Macro Mode placeholders and segment filters.
            - Macro Mode filters can now be used multiple times on the same line.
            - Fixed Macro Mode tooltip behavior when Advanced Mode is disabled.
            
            Chat Alias
            - Added a new Chat Alias tab.
            - Added Enable chat alias toggle in Settings.
            - Added configurable alias words.
            - Added configurable alias commands.
            - Each Chat Alias row can be edited, deleted, enabled, or disabled.
            - Added /rt on alias and /rt off alias.
            - Added Chat Alias to /rt status.
            - Chat Alias is included in global enable/disable behavior.
            - Auto-disable outside housing also disables Chat Alias.
            
            Shell Infos
            - Renamed the old !wifi UI to Shell Infos.
            - Kept Shell Infos macro editing and manual run action.
            - Shell Infos line delay is now Advanced Mode only.
            
            Settings
            - Reworked Settings layout.
            - Kept module toggles centralized in Settings.
            - Added Open Changelog button in Settings.
            - Added Enable chat alias button in Settings.
            - Settings action buttons now use a consistent fixed size.
            - Settings action buttons no longer stretch across the full panel width.
            
            Changelog
            - Added/reworked the Changelog window.
            - Added a cleaner RollTracker-style changelog layout.
            - Fixed manual changelog opening from Settings showing an empty changelog.
            - Added changelog history view for current and future versions.
            
            Config Backup
            - Before a new plugin version migrates or saves config values, RollTracker now writes one backup of the user's existing config.
            - The backup file is replaced on the next update instead of creating many old backup files.
            - The backup file uses the .bak suffix, for example RollTracker.json.bak.
            
            Defaults And Migration
            - New default line delays are 1500 ms.
            - Existing saved line delay values are kept during migration.
            - Old prompt lists are migrated into prompt sets.
            - Old Special Rules are migrated into Special Rule Sets.
            - Missing default help lines are added automatically.
            - Saved Help preset values are validated.
            - Invalid Chat Alias entries are cleaned during config migration.
            """),
        new("0.1.0.56", """
            Stable UI Rework Release
            
            Highlights
            - Added a full UI rework with selectable layouts.
            - Added selectable color themes for all layouts.
            - Added Advanced Mode for technical and risky settings.
            - Added separate Roll History, Command Help, Chat Alias, and Changelog windows/tabs.
            - Added set management for Truth prompts, Dare prompts, and Special Rules.
            - Added automatic config backup before plugin update migration.
            
            UI Layouts
            - Added selectable Modern, Standard, and Legacy layouts.
            - Modern uses a left sidebar navigation.
            - Standard keeps the classic top tab navigation.
            - Legacy keeps an original-style layout while supporting the new theme system.
            - New installs now start with the Standard layout by default.
            - Existing users keep their selected UI layout when updating.
            
            Themes
            - Added selectable themes:
              - Dalamud Blue
              - Dalamud Night
              - Emerald
              - Graphite
            - Legacy mode now also uses the selected theme colors.
            
            Truth Or Dare
            - Reworked the main Truth or Dare controls for better spacing and readability.
            - Added clearer Normal ToD Round and Double ToD Round sections.
            - Added field labels for Macro, Result command, Not enough players text, and Not enough second pair text in Modern and Standard layouts.
            - Added missing Not enough players text fields in Modern and Standard layouts.
            - Added missing Double ToD fallback fields in Modern and Standard layouts.
            - Added a separate !tod2 result line delay setting.
            - Multi-line !tod2 result commands are now sent with a delay between lines.
            - Fixed a critical issue where combat/action log events could be detected as /random rolls.
            - RollTracker now only tracks real RandomNumber log messages and normal /random chat output.
            
            Roll History
            - Moved Roll History out of the main Truth or Dare page for Modern and Standard layouts.
            - Added a separate Roll History window.
            - Roll History shows roll count, highest roll, lowest roll, and the full roll table.
            - Added high/low roll color feedback.
            - Added Reset rolls and End round and post result actions to the Roll History window.
            
            ToD Suggestions
            - Renamed Truth / Dare to ToD Suggestions.
            - Added a Sets Manager for Truth and Dare prompt sets.
            - Prompt sets can be enabled or disabled from the Sets Manager.
            - Added visual feedback for enabled and disabled sets.
            - Removed the old per-set enabled checkbox from the edit area.
            - Improved the Sets Manager layout for small windows.
            - Added horizontal scrolling so the Sets Manager stays reachable when the window is narrow.
            - Removed the Prompts column from the Sets Manager table.
            - Removed duplicate prompt count text above Truth and Dare prompt lists.
            - Set names in the Sets Manager now wrap instead of being squeezed unreadably.
            
            Special Rules
            - Added Special Rule Sets.
            - Existing special rules are migrated into Set 1.
            - Special Rule Sets can be created, duplicated, renamed, deleted, enabled, and disabled.
            - Only enabled Special Rule Sets are checked when roll results are posted.
            - Added a Special Rules Sets Manager in Modern, Standard, and Legacy layouts.
            - Removed the old single-list-only Special Rules behavior from the UI.
            - Moved Special rule line delay above the Special Rules table.
            - Made the Special Rules line delay editable only in Advanced Mode.
            - Added hover tooltips for Special Rules placeholders.
            - Removed {role} from the visible placeholder hint list.
            
            Command Help
            - Reworked !help into its own Command Help tab.
            - Added editable help text lines for the Standard preset.
            - Standard preset command triggers are now fixed and cannot be edited.
            - Only the help description text can be edited in the Standard preset.
            - !help now filters output by active and available modules again.
            - Added Help presets:
              - Standard
              - Compact
              - Macro Mode
            - Added a real Chat Preview showing what would be sent to chat.
            - Fixed duplicate Chat Preview display in Compact mode.
            - Compact sends one line with command states.
            - Macro Mode requires Advanced Mode.
            - Added Macro Mode placeholders and segment filters.
            - Macro Mode filters can now be used multiple times on the same line.
            - Fixed Macro Mode tooltip behavior when Advanced Mode is disabled.
            
            Chat Alias
            - Added a new Chat Alias tab.
            - Added Enable chat alias toggle in Settings.
            - Added configurable alias words.
            - Added configurable alias commands.
            - Each Chat Alias row can be edited, deleted, enabled, or disabled.
            - Added /rt on alias and /rt off alias.
            - Added Chat Alias to /rt status.
            - Chat Alias is included in global enable/disable behavior.
            - Auto-disable outside housing also disables Chat Alias.
            
            Shell Infos
            - Renamed the old !wifi UI to Shell Infos.
            - Kept Shell Infos macro editing and manual run action.
            - Shell Infos line delay is now Advanced Mode only.
            
            Settings
            - Reworked Settings layout.
            - Kept module toggles centralized in Settings.
            - Added Open Changelog button in Settings.
            - Added Enable chat alias button in Settings.
            - Settings action buttons now use a consistent fixed size.
            - Settings action buttons no longer stretch across the full panel width.
            
            Changelog
            - Added/reworked the Changelog window.
            - Added a cleaner RollTracker-style changelog layout.
            - Fixed manual changelog opening from Settings showing an empty changelog.
            - Added changelog history view for current and future versions.
            
            Config Backup
            - Before a new plugin version migrates or saves config values, RollTracker now writes one backup of the user's existing config.
            - The backup file is replaced on the next update instead of creating many old backup files.
            - The backup file uses the .bak suffix, for example RollTracker.json.bak.
            
            Defaults And Migration
            - New default line delays are 1500 ms.
            - Existing saved line delay values are kept during migration.
            - Old prompt lists are migrated into prompt sets.
            - Old Special Rules are migrated into Special Rule Sets.
            - Missing default help lines are added automatically.
            - Saved Help preset values are validated.
            - Invalid Chat Alias entries are cleaned during config migration.
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


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
        new("0.1.0.50", """
            Highlights
            - Added a full UI rework with selectable layouts.
            - Added selectable color themes for all layouts.
            - Added a new Advanced Mode for risky or technical settings.
            - Added separate Roll History, Command Help, Chat Alias, and Changelog windows/tabs.
            - Added set management for Truth prompts, Dare prompts, and Special Rules.

            UI Layouts
            - Added a UI layout dropdown with three modes:
              - Modern
              - Standard
              - Legacy
            - Modern uses a left sidebar navigation.
            - Standard keeps the classic top tab navigation.
            - Legacy keeps an original-style layout while still supporting the newer theme system.
            - Renamed Truth / Dare to ToD Suggestions.
            - Renamed !wifi to Shell Infos.
            - Added Command Help as its own module tab.
            - Added Chat Alias as its own module tab.

            Themes
            - Added a UI theme dropdown.
            - Added multiple themes:
              - Dalamud Blue
              - Dalamud Night
              - Emerald
              - Graphite
            - Legacy mode now also uses the selected theme colors.

            Truth Or Dare Round UI
            - Reworked the main Truth or Dare controls for better spacing and readability.
            - Renamed round sections:
              - Normal ToD Round
              - Double ToD Round
            - Moved Roll History out of the main Truth or Dare page for Modern and Standard layouts.
            - Added Open Roll History in the Round Info panel.
            - Made the Round Info panel narrower so the main controls have more room.
            - Added the Reset rolls and End round and post result actions to the Roll History window too.

            Roll History Window
            - Added a separate Roll History window.
            - The Roll History window shows:
              - current roll count
              - highest roll
              - lowest roll
              - full roll table
            - Added high/low roll color feedback.
            - Added direct actions:
              - Reset rolls
              - End round and post result

            ToD Suggestions
            - Added a Sets Manager for Truth and Dare prompt sets.
            - Prompt sets can be enabled or disabled from the Sets Manager by clicking the set name.
            - Added visual feedback for enabled and disabled sets.
            - Removed the per-set enabled checkbox from the edit area where the Sets Manager now handles that state.
            - Legacy mode also has a compact Sets Manager for Truth and Dare sets.

            Special Rules
            - Added Special Rule Sets.
            - Existing special rules are migrated into Set 1.
            - Special Rule Sets can be:
              - created
              - duplicated
              - renamed
              - deleted
              - enabled or disabled
            - Only enabled Special Rule Sets are checked when roll results are posted.
            - Added a Special Rules Sets Manager in Modern, Standard, and Legacy layouts.
            - Removed the old single-list-only Special Rules behavior from the UI.
            - Moved Special rule line delay (ms) above the Special Rules table.
            - Made the Special Rules line delay editable only in Advanced Mode.
            - Added hover tooltips for Special Rules placeholders:
              - {player}
              - {roll}
            - Removed {role} from the visible placeholder hint list.

            Command Help
            - Reworked !help into its own Command Help tab.
            - Added editable help text lines for the Standard preset.
            - !help now again filters output by active/available modules.
            - Added a Help preset dropdown:
              - Standard
              - Compact
              - Macro Mode
            - Added a real Chat Preview showing what would be sent to chat, including the selected chat command prefix.
            - Compact sends one line with command states.
            - Macro Mode requires Advanced Mode.
            - Added Macro Mode placeholders:
              - {activeCommands}
              - {commandStates}
            - Added Macro Mode segment filters:
              - {!tod}
              - {!tod2}
              - {!truth}
              - {!dare}
              - {!wifi}
            - Macro Mode filters can now be used multiple times on the same line.
            - A disabled filter hides only the text segment until the next filter placeholder, instead of hiding the whole line.
            - Added grey placeholder/example text inside the Macro Mode text box.
            - The Macro Mode placeholder disappears as soon as the user types into the box.

            Chat Alias
            - Added a new Chat Alias tab.
            - Added Enable chat alias toggle in Settings.
            - Added configurable alias word, for example Rainbow, banane, or any custom word.
            - Added a command dropdown with supported /rt commands.
            - Added a custom typed-text field for each alias command.
            - Added a table for configured Chat Alias commands.
            - Each Chat Alias row can be edited, deleted, enabled, or disabled.
            - Disabled Chat Alias rows stay saved but are ignored by chat detection.
            - Added /rt on alias and /rt off alias.
            - Added Chat Alias to /rt status.
            - Chat Alias is included in global enable/disable behavior.
            - Auto-disable outside housing also disables Chat Alias.

            Advanced Mode
            - Added Advanced Mode in Settings.
            - Advanced Mode is enabled through a confirmation popup.
            - Added warning text before enabling Advanced Mode.
            - Added a button to turn Advanced Mode off again.
            - Moved technical line-delay fields behind Advanced Mode.
            - Line-delay fields stay visible in normal mode but are greyed out.
            - Hovering disabled line-delay fields explains that Advanced Mode is needed.
            - Advanced-only fields include Normal ToD, Double ToD, Special Rules, Shell Infos, and Command Help delay controls.

            Shell Infos
            - Renamed the old !wifi tab to Shell Infos.
            - Kept Shell Infos macro editing and manual run action.
            - Shell Infos line delay is now Advanced Mode only.

            Settings
            - Renamed Enable ToD Second pair to Enable ToD - Doubles.
            - Kept module toggles centralized in Settings.
            - Added Open Changelog button in Settings.
            - Added Enable chat alias button in Settings.
            - Put the !help chat channel control inside the Command Help tab.

            Changelog Window
            - Added/reworked the Changelog window.
            - Added a cleaner RollTracker-style changelog layout.
            - Added Open Changelog button in Settings.

            Defaults And Migration
            - New default line delays are 1500 ms.
            - Existing saved line delay values are kept during migration.
            - Migrates old prompt lists into prompt sets.
            - Migrates old Special Rules into Special Rule Sets.
            - Adds default help lines when missing.
            - Validates saved Help preset values.
            - Cleans invalid Chat Alias entries during config migration.
            """),
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

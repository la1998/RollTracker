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
    private static readonly IReadOnlyList<ChangelogEntry> Entries =
    [
        new("0.1.0.47", "Testing metadata bump so the testing build stays one version ahead of stable."),
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
        Flags = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.AlwaysAutoResize;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(360, 180),
            MaximumSize = new Vector2(560, 640),
        };
    }

    public void Dispose()
    {
    }

    public override void Draw()
    {
        ImGui.TextUnformatted($"RollTracker updated to {currentVersion}");
        ImGui.Separator();

        var visibleEntries = GetVisibleEntries().ToList();
        if (visibleEntries.Count == 0)
        {
            ImGui.TextWrapped("No detailed changelog entries are available for this version.");
        }
        else
        {
            foreach (var entry in visibleEntries)
            {
                ImGui.TextUnformatted(entry.Version);
                ImGui.SameLine();
                ImGui.TextWrapped(entry.Text);
            }
        }

        ImGui.Spacing();
        if (ImGui.Button("Got it", new Vector2(110 * ImGuiHelpers.GlobalScale, 0)))
        {
            MarkSeenAndClose();
        }
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

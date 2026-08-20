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
        new("0.1.0.29", "Fixed duplicate default Special Rules after loading saved configuration."),
        new("0.1.0.28", "Added per-rule Stop pair control for ToD special rules."),
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

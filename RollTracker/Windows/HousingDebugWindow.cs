using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using RollTracker.Services;

namespace RollTracker.Windows;

internal sealed class HousingDebugWindow : Window, IDisposable
{
    private static readonly Vector4 AccentColor = new(0.92f, 0.48f, 0.72f, 1.00f);

    private readonly RollTrackerService rollTrackerService;

    public HousingDebugWindow(RollTrackerService rollTrackerService)
        : base("RollTracker Debug Info##RollTrackerHousingDebugWindow")
    {
        this.rollTrackerService = rollTrackerService;

        Flags = ImGuiWindowFlags.None;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(520, 360),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }

    public void Dispose()
    {
    }

    public override void Draw()
    {
        var housingInfo = rollTrackerService.GetCurrentHousingDebugInfo();

        ImGui.TextColored(AccentColor, "Current housing info");
        ImGui.Separator();
        ImGui.Spacing();

        DrawDebugInfoLine("Territory ID", housingInfo.TerritoryType.ToString());
        DrawDebugInfoLine("Current location", housingInfo.CurrentLocationPreview);
        DrawDebugInfoLine("Interior address reliable", housingInfo.HasReliableInteriorAddress ? "Yes" : "No");
        DrawDebugInfoLine("Interior address preview", housingInfo.AddressPreview);
        DrawDebugInfoLine("Data center", housingInfo.DataCenterName);
        DrawDebugInfoLine("World", $"{housingInfo.WorldName} ({housingInfo.WorldId})");
        DrawDebugInfoLine("District", housingInfo.DistrictName);
        DrawDebugInfoLine("Housing interior", housingInfo.IsHousingInterior ? "Yes" : "No");
        DrawDebugInfoLine("Residential area", housingInfo.IsResidentialArea ? "Yes" : "No");
        DrawDebugInfoLine("Between areas", housingInfo.IsBetweenAreas ? "Yes" : "No");
        DrawDebugInfoLine("Housing manager", housingInfo.HasHousingManager ? "Available" : "Unavailable");
        DrawDebugInfoLine("Housing territory type", string.IsNullOrWhiteSpace(housingInfo.HousingTerritoryType) ? "-" : housingInfo.HousingTerritoryType);
        DrawDebugInfoLine("Ward", $"{FormatDisplayIndex(housingInfo.Ward)} (raw {FormatSignedDebugValue(housingInfo.Ward)})");
        DrawDebugInfoLine("Plot", $"{FormatDisplayIndex(housingInfo.Plot)} (raw {FormatSignedDebugValue(housingInfo.Plot)})");
        DrawDebugInfoLine("Division", housingInfo.Division.ToString());
        DrawDebugInfoLine("Room", FormatSignedDebugValue(housingInfo.Room));
        DrawDebugInfoLine("Current house ID", string.IsNullOrWhiteSpace(housingInfo.CurrentHouseId) ? "-" : housingInfo.CurrentHouseId);
        DrawDebugInfoLine("Current indoor house ID", string.IsNullOrWhiteSpace(housingInfo.CurrentIndoorHouseId) ? "-" : housingInfo.CurrentIndoorHouseId);
        DrawDebugInfoLine("Original house territory ID", housingInfo.OriginalHouseTerritoryTypeId.ToString());
        DrawDebugInfoLine("House permissions", housingInfo.HasHousePermissions ? "Yes" : "No");
    }

    private static void DrawDebugInfoLine(string label, string value)
    {
        ImGui.TextDisabled($"{label}:");
        ImGui.SameLine(190 * ImGuiHelpers.GlobalScale);
        ImGui.TextUnformatted(value);
    }

    private static string FormatSignedDebugValue<T>(T value)
        where T : struct, IConvertible
    {
        var rawValue = Convert.ToInt64(value);
        return rawValue == sbyte.MinValue || rawValue == short.MinValue
            ? "-"
            : rawValue.ToString();
    }

    private static string FormatDisplayIndex<T>(T value)
        where T : struct, IConvertible
    {
        var rawValue = Convert.ToInt64(value);
        return rawValue < 0 ? "-" : (rawValue + 1).ToString();
    }
}

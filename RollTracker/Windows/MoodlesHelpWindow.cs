using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;

namespace RollTracker.Windows;

internal sealed class MoodlesHelpWindow : Window, IDisposable
{
    private const string ExampleMoodleImportCode = """{"IconID":217601,"Title":"[color=red]ToD Bot Active [/color]","Description":"\n[color=500]Use !help for all avaiebl comands[/color]","CustomFXPath":"","Type":2,"Modifiers":0,"Stacks":1,"StackSteps":0,"ChainedStatus":"00000000-0000-0000-0000-000000000000","ChainTrigger":0,"Applier":"","Dispeller":"","Days":0,"Hours":0,"Minutes":0,"Seconds":0,"NoExpire":true,"AsPermanent":true}""";

    private static readonly Vector4 AccentColor = new(0.92f, 0.48f, 0.72f, 1.00f);
    private static readonly Vector4 SuccessColor = new(0.46f, 0.86f, 0.58f, 1.00f);

    private bool copied;

    public MoodlesHelpWindow()
        : base("RollTracker Moodles Help##RollTrackerMoodlesHelpWindow")
    {
        Flags = ImGuiWindowFlags.None;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(520, 420),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }

    public void Dispose()
    {
    }

    public override void Draw()
    {
        ImGui.TextColored(AccentColor, "Moodle setup");
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextWrapped("RollTracker does not create Moodles by itself. First create or import a Moodle in the Moodles plugin, then enter that exact Moodle name in the RollTracker Moodle name field.");
        ImGui.TextWrapped("For example, if you import the sample below into Moodles and name it ToD, enter ToD as the Moodle name in RollTracker.");

        ImGui.Spacing();
        if (ImGui.Button("Copy example Moodle import code", new Vector2(250 * ImGuiHelpers.GlobalScale, 0)))
        {
            ImGui.SetClipboardText(ExampleMoodleImportCode);
            copied = true;
        }

        if (copied)
        {
            ImGui.SameLine();
            ImGui.TextColored(SuccessColor, "Copied");
        }

        ImGui.Spacing();
        ImGui.TextColored(AccentColor, "Example import code");
        ImGui.Separator();

        var available = ImGui.GetContentRegionAvail();
        if (ImGui.BeginChild(
            "RollTrackerMoodleImportCode",
            new Vector2(available.X, Math.Max(160 * ImGuiHelpers.GlobalScale, available.Y)),
            true,
            ImGuiWindowFlags.HorizontalScrollbar))
        {
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + Math.Max(240 * ImGuiHelpers.GlobalScale, ImGui.GetContentRegionAvail().X));
            ImGui.TextUnformatted(ExampleMoodleImportCode);
            ImGui.PopTextWrapPos();
        }

        ImGui.EndChild();
    }
}

using System;
using System.Collections.Generic;
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
        : base("RollTracker##RollTrackerMainWindow")
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
        if (!ImGui.BeginTabBar("RollTrackerTabs"))
        {
            return;
        }

        if (ImGui.BeginTabItem("Truth or Dare"))
        {
            DrawTruthOrDareTab();
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("Truth / Dare"))
        {
            DrawTruthDarePromptTab();
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("Special Rules"))
        {
            DrawSpecialRulesTab();
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("!wifi"))
        {
            DrawWifiTab();
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("Settings"))
        {
            DrawSettingsTab();
            ImGui.EndTabItem();
        }

        ImGui.EndTabBar();
    }

    private void DrawTruthOrDareTab()
    {
        var highest = rollTrackerService.HighestRoll;
        var lowest = rollTrackerService.LowestRoll;

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

        ImGui.TextUnformatted("!tod");

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
        if (ImGui.InputTextMultiline("Macro##Tod", ref macroText, 4096, new Vector2(0, 90 * ImGuiHelpers.GlobalScale)))
        {
            configuration.MacroText = macroText;
            saveConfiguration();
        }

        var resultCommandTemplate = configuration.ResultCommandTemplate;
        if (ImGui.InputText("Result command##Tod", ref resultCommandTemplate, 512))
        {
            configuration.ResultCommandTemplate = resultCommandTemplate;
            saveConfiguration();
        }

        ImGui.Separator();
        ImGui.TextUnformatted("!tod2");

        var secondPairDuration = configuration.TodSecondPairMacroDurationSeconds;
        if (ImGui.InputInt("Macro duration (s)##Tod2", ref secondPairDuration))
        {
            configuration.TodSecondPairMacroDurationSeconds = Math.Clamp(secondPairDuration, 1, 600);
            saveConfiguration();
        }

        var secondPairMacroText = configuration.TodSecondPairMacroText;
        if (ImGui.InputTextMultiline("Macro##Tod2", ref secondPairMacroText, 4096, new Vector2(0, 90 * ImGuiHelpers.GlobalScale)))
        {
            configuration.TodSecondPairMacroText = secondPairMacroText;
            saveConfiguration();
        }

        var secondPairResultCommandTemplate = configuration.TodSecondPairResultCommandTemplate;
        if (ImGui.InputTextMultiline("Result command##Tod2", ref secondPairResultCommandTemplate, 1024, new Vector2(0, 55 * ImGuiHelpers.GlobalScale)))
        {
            configuration.TodSecondPairResultCommandTemplate = secondPairResultCommandTemplate;
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

    private string newTruthPrompt = string.Empty;
    private string newDarePrompt = string.Empty;
    private int newSpecialRuleRoll;
    private string newSpecialRuleText = string.Empty;

    private void DrawTruthDarePromptTab()
    {
        ImGui.TextUnformatted(configuration.Enabled
            ? $"!truth: {(configuration.TruthTriggerEnabled ? "active" : "inactive")} / !dare: {(configuration.DareTriggerEnabled ? "active" : "inactive")}"
            : "!truth and !dare are inactive");

        DrawChatChannelCombo("Chat", configuration.TodPromptChatChannel, channel => configuration.TodPromptChatChannel = channel);

        ImGui.Separator();

        if (ImGui.BeginTabBar("RollTrackerPromptTabs"))
        {
            if (ImGui.BeginTabItem("Truths"))
            {
                DrawPromptList("Truth", configuration.TruthPrompts, ref newTruthPrompt);
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Dares"))
            {
                DrawPromptList("Dare", configuration.DarePrompts, ref newDarePrompt);
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }
    }

    private void DrawPromptList(string label, List<string> prompts, ref string newPrompt)
    {
        ImGui.TextUnformatted($"{label}s: {prompts.Count}");
        ImGui.Spacing();

        for (var i = 0; i < prompts.Count; i++)
        {
            ImGui.PushID($"{label}{i}");

            if (ImGui.Button("Delete"))
            {
                prompts.RemoveAt(i);
                saveConfiguration();
                ImGui.PopID();
                i--;
                continue;
            }

            ImGui.SameLine();

            var prompt = prompts[i];
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputText("##Prompt", ref prompt, 1024))
            {
                prompts[i] = prompt;
                saveConfiguration();
            }

            ImGui.PopID();
        }

        ImGui.Separator();

        ImGui.SetNextItemWidth(-1);
        ImGui.InputText($"New {label}", ref newPrompt, 1024);

        if (ImGui.Button($"Add {label}") && !string.IsNullOrWhiteSpace(newPrompt))
        {
            prompts.Add(newPrompt.Trim());
            newPrompt = string.Empty;
            saveConfiguration();
        }
    }

    private void DrawSpecialRulesTab()
    {
        var todSpecialRulesEnabled = configuration.TodSpecialRulesEnabled;
        if (ImGui.Checkbox("Enable ToD special rules", ref todSpecialRulesEnabled))
        {
            configuration.TodSpecialRulesEnabled = todSpecialRulesEnabled;
            saveConfiguration();
        }

        ImGui.TextWrapped("Placeholders: {player}, {roll}, {role}. Do not trigger with accepts numbers separated by commas or spaces.");
        ImGui.Separator();

        var tableFlags = ImGuiTableFlags.Borders |
                         ImGuiTableFlags.RowBg |
                         ImGuiTableFlags.SizingStretchProp;

        if (ImGui.BeginTable("RollTrackerSpecialRulesTable", 4, tableFlags))
        {
            ImGui.TableSetupColumn("Roll", ImGuiTableColumnFlags.WidthFixed, 80 * ImGuiHelpers.GlobalScale);
            ImGui.TableSetupColumn("Text");
            ImGui.TableSetupColumn("Do not trigger with", ImGuiTableColumnFlags.WidthFixed, 140 * ImGuiHelpers.GlobalScale);
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 75 * ImGuiHelpers.GlobalScale);
            ImGui.TableHeadersRow();

            for (var i = 0; i < configuration.TodSpecialRules.Count; i++)
            {
                ImGui.PushID($"SpecialRule{i}");
                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                var roll = configuration.TodSpecialRules[i].Roll;
                ImGui.SetNextItemWidth(-1);
                if (ImGui.InputInt("##Roll", ref roll))
                {
                    configuration.TodSpecialRules[i].Roll = Math.Clamp(roll, 0, 9999);
                    saveConfiguration();
                }

                ImGui.TableNextColumn();
                var text = configuration.TodSpecialRules[i].Text;
                ImGui.SetNextItemWidth(-1);
                if (ImGui.InputText("##Text", ref text, 1024))
                {
                    configuration.TodSpecialRules[i].Text = text;
                    saveConfiguration();
                }

                ImGui.TableNextColumn();
                var doNotTriggerWith = configuration.TodSpecialRules[i].DoNotTriggerWith;
                ImGui.SetNextItemWidth(-1);
                if (ImGui.InputText("##DoNotTriggerWith", ref doNotTriggerWith, 256))
                {
                    configuration.TodSpecialRules[i].DoNotTriggerWith = doNotTriggerWith;
                    saveConfiguration();
                }

                ImGui.TableNextColumn();
                if (ImGui.Button("Delete"))
                {
                    configuration.TodSpecialRules.RemoveAt(i);
                    saveConfiguration();
                    ImGui.PopID();
                    i--;
                    continue;
                }

                ImGui.PopID();
            }

            ImGui.EndTable();
        }

        ImGui.Separator();
        ImGui.SetNextItemWidth(90 * ImGuiHelpers.GlobalScale);
        ImGui.InputInt("New roll", ref newSpecialRuleRoll);
        newSpecialRuleRoll = Math.Clamp(newSpecialRuleRoll, 0, 9999);

        ImGui.SetNextItemWidth(-1);
        ImGui.InputText("New text", ref newSpecialRuleText, 1024);

        if (ImGui.Button("Add rule") && !string.IsNullOrWhiteSpace(newSpecialRuleText))
        {
            configuration.TodSpecialRules.Add(new TodSpecialRule
            {
                Roll = newSpecialRuleRoll,
                Text = newSpecialRuleText.Trim(),
            });
            newSpecialRuleText = string.Empty;
            saveConfiguration();
        }
    }

    private void DrawWifiTab()
    {
        ImGui.TextUnformatted(rollTrackerService.IsWifiMacroRunning ? "Macro: running" : "Macro: idle");

        DrawChatChannelCombo("Chat", configuration.WifiChatChannel, channel => configuration.WifiChatChannel = channel);

        var wifiMacroText = configuration.WifiMacroText;
        if (ImGui.InputTextMultiline("Macro##Wifi", ref wifiMacroText, 4096, new Vector2(0, 170 * ImGuiHelpers.GlobalScale)))
        {
            configuration.WifiMacroText = wifiMacroText;
            saveConfiguration();
        }

        if (ImGui.Button("Run !wifi"))
        {
            rollTrackerService.StartWifiMacro("manual");
        }
    }

    private void DrawChatChannelCombo(string label, string channel, Action<string> setChannel)
    {
        if (ImGui.BeginCombo(label, channel))
        {
            foreach (var option in new[] { "Yell", "Say", "Party" })
            {
                var selected = channel.Equals(option, StringComparison.OrdinalIgnoreCase);
                if (ImGui.Selectable(option, selected))
                {
                    setChannel(option);
                    saveConfiguration();
                }

                if (selected)
                {
                    ImGui.SetItemDefaultFocus();
                }
            }

            ImGui.EndCombo();
        }
    }

    private void DrawSettingsTab()
    {
        var todEnabled = configuration.Enabled;
        if (ImGui.Checkbox("Enable ToD", ref todEnabled))
        {
            rollTrackerService.SetEnabled(todEnabled);
        }

        var todSecondPairEnabled = configuration.TodSecondPairEnabled;
        if (ImGui.Checkbox("Enable ToD second pair", ref todSecondPairEnabled))
        {
            rollTrackerService.SetSecondPairEnabled(todSecondPairEnabled);
        }

        var truthTriggerEnabled = configuration.TruthTriggerEnabled;
        if (ImGui.Checkbox("Enable !truth", ref truthTriggerEnabled))
        {
            rollTrackerService.SetTruthTriggerEnabled(truthTriggerEnabled);
        }

        var dareTriggerEnabled = configuration.DareTriggerEnabled;
        if (ImGui.Checkbox("Enable !dare", ref dareTriggerEnabled))
        {
            rollTrackerService.SetDareTriggerEnabled(dareTriggerEnabled);
        }

        var helpTriggerEnabled = configuration.HelpTriggerEnabled;
        if (ImGui.Checkbox("Enable !help", ref helpTriggerEnabled))
        {
            rollTrackerService.SetHelpTriggerEnabled(helpTriggerEnabled);
        }

        DrawChatChannelCombo("!help chat", configuration.HelpChatChannel, channel => configuration.HelpChatChannel = channel);

        var wifiEnabled = configuration.WifiEnabled;
        if (ImGui.Checkbox("Enable Wifi", ref wifiEnabled))
        {
            rollTrackerService.SetWifiEnabled(wifiEnabled);
        }

        var autoDisableWhenLeavingHousing = configuration.AutoDisableWhenLeavingHousing;
        if (ImGui.Checkbox("Auto off outside house", ref autoDisableWhenLeavingHousing))
        {
            configuration.AutoDisableWhenLeavingHousing = autoDisableWhenLeavingHousing;
            saveConfiguration();
        }

        ImGui.Separator();

        if (ImGui.Button("Enable all"))
        {
            rollTrackerService.SetAllModulesEnabled(true);
        }

        ImGui.SameLine();

        if (ImGui.Button("Disable all"))
        {
            rollTrackerService.SetAllModulesEnabled(false);
        }
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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using RollTracker.Services;

namespace RollTracker.Windows;

internal sealed class MainWindow : Window, IDisposable
{
    private readonly RollTrackerService rollTrackerService;
    private readonly Configuration configuration;
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly IChatGui chatGui;
    private readonly Action saveConfiguration;

    public MainWindow(
        RollTrackerService rollTrackerService,
        Configuration configuration,
        IDalamudPluginInterface pluginInterface,
        IChatGui chatGui,
        Action saveConfiguration)
        : base("RollTracker##RollTrackerMainWindow")
    {
        this.rollTrackerService = rollTrackerService;
        this.configuration = configuration;
        this.pluginInterface = pluginInterface;
        this.chatGui = chatGui;
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
        DrawHelpTooltip("Delay between !tod macro lines. Leave this alone unless chat lines are skipped.");

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

        var notEnoughPlayersResultText = configuration.NotEnoughPlayersResultText;
        if (ImGui.InputText("Not enough players text##Tod", ref notEnoughPlayersResultText, 512))
        {
            configuration.NotEnoughPlayersResultText = notEnoughPlayersResultText;
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

        var secondPairLineDelay = configuration.TodSecondPairMacroLineDelayMilliseconds;
        if (ImGui.InputInt("Line delay (ms)##Tod2", ref secondPairLineDelay))
        {
            configuration.TodSecondPairMacroLineDelayMilliseconds = Math.Clamp(secondPairLineDelay, 100, 10000);
            saveConfiguration();
        }
        DrawHelpTooltip("Delay between !tod2 macro lines. Leave this alone unless chat lines are skipped.");

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

        var secondPairNotEnoughRoundPlayersResultText = configuration.TodSecondPairNotEnoughRoundPlayersResultText;
        if (ImGui.InputText("Not enough players text##Tod2", ref secondPairNotEnoughRoundPlayersResultText, 512))
        {
            configuration.TodSecondPairNotEnoughRoundPlayersResultText = secondPairNotEnoughRoundPlayersResultText;
            saveConfiguration();
        }

        var secondPairNotEnoughPlayersResultText = configuration.TodSecondPairNotEnoughPlayersResultText;
        if (ImGui.InputText("Not enough second pair text##Tod2", ref secondPairNotEnoughPlayersResultText, 512))
        {
            configuration.TodSecondPairNotEnoughPlayersResultText = secondPairNotEnoughPlayersResultText;
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
    private int editingTruthSetIndex = -1;
    private int editingDareSetIndex = -1;
    private string editingTruthSetName = string.Empty;
    private string editingDareSetName = string.Empty;
    private int newSpecialRuleRoll;
    private string newSpecialRuleText = string.Empty;

    private void DrawTruthDarePromptTab()
    {
        ImGui.TextUnformatted($"!truth: {(configuration.TruthTriggerEnabled ? "active" : "inactive")} / !dare: {(configuration.DareTriggerEnabled ? "active" : "inactive")}");

        DrawChatChannelCombo("Chat", configuration.TodPromptChatChannel, channel => configuration.TodPromptChatChannel = channel);

        ImGui.Separator();

        if (ImGui.BeginTabBar("RollTrackerPromptTabs"))
        {
            if (ImGui.BeginTabItem("Truths"))
            {
                DrawPromptSetTabs(
                    "Truth",
                    configuration.TruthPromptSets,
                    ref newTruthPrompt,
                    ref editingTruthSetIndex,
                    ref editingTruthSetName);
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Dares"))
            {
                DrawPromptSetTabs(
                    "Dare",
                    configuration.DarePromptSets,
                    ref newDarePrompt,
                    ref editingDareSetIndex,
                    ref editingDareSetName);
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }
    }

    private void DrawPromptSetTabs(
        string label,
        List<TodPromptSet> promptSets,
        ref string newPrompt,
        ref int editingSetIndex,
        ref string editingSetName)
    {
        if (promptSets.Count == 0)
        {
            promptSets.Add(new TodPromptSet());
            saveConfiguration();
        }

        if (ImGui.Button($"+##Add{label}Set"))
        {
            promptSets.Add(new TodPromptSet
            {
                Name = $"Set {promptSets.Count + 1}",
                Enabled = true,
                Prompts = [],
            });
            saveConfiguration();
        }

        ImGui.SameLine();
        ImGui.TextUnformatted($"Active: {promptSets.Count(set => set.Enabled)} / {promptSets.Count}");
        ImGui.Separator();

        if (!ImGui.BeginTabBar($"{label}PromptSetTabs"))
        {
            return;
        }

        for (var i = 0; i < promptSets.Count; i++)
        {
            var promptSet = promptSets[i];
            var enabledMarker = promptSet.Enabled ? "[X]" : "[ ]";
            var tabName = string.IsNullOrWhiteSpace(promptSet.Name) ? $"Set {i + 1}" : promptSet.Name.Trim();

            if (!ImGui.BeginTabItem($"{enabledMarker} {tabName}##{label}Set{i}"))
            {
                continue;
            }

            DrawPromptSet(label, promptSets, i, ref newPrompt, ref editingSetIndex, ref editingSetName);
            ImGui.EndTabItem();
        }

        ImGui.EndTabBar();
    }

    private void DrawPromptSet(
        string label,
        List<TodPromptSet> promptSets,
        int setIndex,
        ref string newPrompt,
        ref int editingSetIndex,
        ref string editingSetName)
    {
        var promptSet = promptSets[setIndex];
        promptSet.Prompts ??= [];

        var enabled = promptSet.Enabled;
        if (ImGui.Checkbox($"Enabled##{label}SetEnabled{setIndex}", ref enabled))
        {
            promptSet.Enabled = enabled;
            promptSets[setIndex] = promptSet;
            saveConfiguration();
        }

        ImGui.SameLine();

        if (ImGui.Button($"Edit name##{label}SetEdit{setIndex}"))
        {
            editingSetIndex = setIndex;
            editingSetName = promptSet.Name;
        }

        if (promptSets.Count > 1)
        {
            ImGui.SameLine();

            if (ImGui.Button($"Delete set##{label}SetDelete{setIndex}"))
            {
                promptSets.RemoveAt(setIndex);
                if (editingSetIndex == setIndex)
                {
                    editingSetIndex = -1;
                    editingSetName = string.Empty;
                }
                saveConfiguration();
                return;
            }
        }

        if (editingSetIndex == setIndex)
        {
            ImGui.SetNextItemWidth(-1);
            ImGui.InputText($"Set name##{label}SetName{setIndex}", ref editingSetName, 128);

            if (ImGui.Button($"Save name##{label}SetSave{setIndex}"))
            {
                promptSet.Name = string.IsNullOrWhiteSpace(editingSetName)
                    ? $"Set {setIndex + 1}"
                    : editingSetName.Trim();
                promptSets[setIndex] = promptSet;
                editingSetIndex = -1;
                editingSetName = string.Empty;
                saveConfiguration();
            }

            ImGui.SameLine();

            if (ImGui.Button($"Cancel##{label}SetCancel{setIndex}"))
            {
                editingSetIndex = -1;
                editingSetName = string.Empty;
            }
        }

        DrawPromptList(label, promptSet.Prompts, ref newPrompt);
        promptSets[setIndex] = promptSet;
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

        ImGui.Separator();
        var specialRuleLineDelay = configuration.TodSpecialRuleLineDelayMilliseconds;
        if (ImGui.InputInt("Special rule line delay (ms)", ref specialRuleLineDelay))
        {
            configuration.TodSpecialRuleLineDelayMilliseconds = Math.Clamp(specialRuleLineDelay, 100, 10000);
            saveConfiguration();
        }
        DrawHelpTooltip("Delay before and between Special Rule result lines. Leave this alone unless chat lines are skipped.");
    }

    private void DrawWifiTab()
    {
        ImGui.TextUnformatted(rollTrackerService.IsWifiMacroRunning ? "Macro: running" : "Macro: idle");

        DrawChatChannelCombo("Chat", configuration.WifiChatChannel, channel => configuration.WifiChatChannel = channel);

        var wifiLineDelay = configuration.WifiMacroLineDelayMilliseconds;
        if (ImGui.InputInt("Line delay (ms)##Wifi", ref wifiLineDelay))
        {
            configuration.WifiMacroLineDelayMilliseconds = Math.Clamp(wifiLineDelay, 100, 10000);
            saveConfiguration();
        }
        DrawHelpTooltip("Delay between !wifi chat lines. Leave this alone unless chat lines are skipped.");

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
        DrawSettingsSection("Truth or Dare");

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

        var todSpecialRulesEnabled = configuration.TodSpecialRulesEnabled;
        if (ImGui.Checkbox("Enable ToD special rules", ref todSpecialRulesEnabled))
        {
            configuration.TodSpecialRulesEnabled = todSpecialRulesEnabled;
            saveConfiguration();
        }

        DrawSettingsSection("Truth / Dare Suggestions");

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

        DrawSettingsSection("General");

        var helpTriggerEnabled = configuration.HelpTriggerEnabled;
        if (ImGui.Checkbox("Enable !help", ref helpTriggerEnabled))
        {
            rollTrackerService.SetHelpTriggerEnabled(helpTriggerEnabled);
        }

        DrawChatChannelCombo("!help chat", configuration.HelpChatChannel, channel => configuration.HelpChatChannel = channel);

        var autoDisableWhenLeavingHousing = configuration.AutoDisableWhenLeavingHousing;
        if (ImGui.Checkbox("Auto off outside house", ref autoDisableWhenLeavingHousing))
        {
            configuration.AutoDisableWhenLeavingHousing = autoDisableWhenLeavingHousing;
            saveConfiguration();
        }

        if (ImGui.Button("Open config folder"))
        {
            OpenConfigFolder();
        }

        DrawSettingsSection("Wifi");

        var wifiEnabled = configuration.WifiEnabled;
        if (ImGui.Checkbox("Enable Wifi", ref wifiEnabled))
        {
            rollTrackerService.SetWifiEnabled(wifiEnabled);
        }

        DrawSettingsSection("Global");

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

    private static void DrawSettingsSection(string title)
    {
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.TextUnformatted(title);
        ImGui.Spacing();
    }

    private static void DrawHelpTooltip(string text)
    {
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(text);
        }
    }

    private void OpenConfigFolder()
    {
        try
        {
            saveConfiguration();
            var configDirectory = pluginInterface.GetPluginConfigDirectory();
            Directory.CreateDirectory(configDirectory);
            Process.Start(new ProcessStartInfo
            {
                FileName = configDirectory,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            chatGui.PrintError($"Could not open config folder: {ex.Message}", "RollTracker");
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

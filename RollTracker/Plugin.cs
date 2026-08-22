using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using RollTracker.Services;
using RollTracker.Windows;

namespace RollTracker;

public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/rolltracker";
    private const string AliasCommandName = "/rt";
    private const string CommandHelp =
        "Commands:\n" +
        "/rt - open or close the window.\n" +
        "/rt on - enable all modules.\n" +
        "/rt off - disable all modules.\n" +
        "/rt on tod - enable Truth or Dare.\n" +
        "/rt off tod - disable Truth or Dare.\n" +
        "/rt on todrules - enable Truth or Dare special rules.\n" +
        "/rt off todrules - disable Truth or Dare special rules.\n" +
        "/rt on todsecond - enable !tod2 second pair rounds.\n" +
        "/rt off todsecond - disable !tod2 second pair rounds.\n" +
        "/rt on truth - enable !truth.\n" +
        "/rt off truth - disable !truth.\n" +
        "/rt on dare - enable !dare.\n" +
        "/rt off dare - disable !dare.\n" +
        "/rt on help - enable !help.\n" +
        "/rt off help - disable !help.\n" +
        "/rt on wifi - enable !wifi.\n" +
        "/rt off wifi - disable !wifi.\n" +
        "/rt status - show module states.\n" +
        "/rt reset - clear the current roll list.\n" +
        "/rt end - send the current result, then clear the list.\n" +
        "/rt add <number> - add a manual test roll to the running round.\n" +
        "/rt test - add sample rolls for checking the UI.";
    private const string AliasCommandHelp = "Alias for /rt. Use /rt to see the full command list.";

    [PluginService]
    internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;

    [PluginService]
    internal static ICommandManager CommandManager { get; private set; } = null!;

    [PluginService]
    internal static IChatGui ChatGui { get; private set; } = null!;

    [PluginService]
    internal static IFramework Framework { get; private set; } = null!;

    [PluginService]
    internal static IClientState ClientState { get; private set; } = null!;

    [PluginService]
    internal static IDataManager DataManager { get; private set; } = null!;

    [PluginService]
    internal static IPluginLog Log { get; private set; } = null!;

    public WindowSystem WindowSystem { get; } = new("RollTracker");

    internal RollTrackerService RollTrackerService { get; }

    internal Configuration Configuration { get; }

    private MainWindow MainWindow { get; }

    private ChangelogWindow ChangelogWindow { get; }

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        MigrateConfiguration();
        RollTrackerService = new RollTrackerService(
            ChatGui,
            CommandManager,
            Framework,
            ClientState,
            DataManager,
            Log,
            Configuration,
            SaveConfiguration);
        MainWindow = new MainWindow(RollTrackerService, Configuration, SaveConfiguration);
        ChangelogWindow = new ChangelogWindow(Configuration, SaveConfiguration, GetPluginVersion());

        WindowSystem.AddWindow(MainWindow);
        WindowSystem.AddWindow(ChangelogWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = AliasCommandHelp,
        });
        CommandManager.AddHandler(AliasCommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = CommandHelp,
        });

        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;
        PluginInterface.UiBuilder.OpenConfigUi += OpenConfigUi;
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;
        PluginInterface.UiBuilder.OpenConfigUi -= OpenConfigUi;

        CommandManager.RemoveHandler(CommandName);
        CommandManager.RemoveHandler(AliasCommandName);

        WindowSystem.RemoveAllWindows();
        MainWindow.Dispose();
        ChangelogWindow.Dispose();
        RollTrackerService.Dispose();
    }

    private void OnCommand(string command, string args)
    {
        var trimmedArgs = args.Trim();

        if (trimmedArgs.Equals("clear", StringComparison.OrdinalIgnoreCase) ||
            trimmedArgs.Equals("reset", StringComparison.OrdinalIgnoreCase))
        {
            RollTrackerService.Reset();
            ChatGui.Print("Roll list reset.", "RollTracker");
            return;
        }

        if (TryHandleModuleToggle(trimmedArgs, true))
        {
            return;
        }

        if (TryHandleModuleToggle(trimmedArgs, false))
        {
            return;
        }

        if (TryHandleReversedModuleToggle(trimmedArgs))
        {
            return;
        }

        if (trimmedArgs.Equals("status", StringComparison.OrdinalIgnoreCase))
        {
            ChatGui.Print($"RollTracker ToD is {(Configuration.Enabled ? "on" : "off")}; !truth is {(Configuration.TruthTriggerEnabled ? "on" : "off")}; !dare is {(Configuration.DareTriggerEnabled ? "on" : "off")}; !help is {(Configuration.HelpTriggerEnabled ? "on" : "off")}; ToD special rules are {(Configuration.TodSpecialRulesEnabled ? "on" : "off")}; !tod2 is {(Configuration.TodSecondPairEnabled ? "on" : "off")}; !wifi is {(Configuration.WifiEnabled ? "on" : "off")}.", "RollTracker");
            return;
        }

        if (trimmedArgs.Equals("end", StringComparison.OrdinalIgnoreCase))
        {
            RollTrackerService.FinishRoundAndReset();
            return;
        }

        if (TryHandleManualRoll(trimmedArgs))
        {
            return;
        }

        if (trimmedArgs.StartsWith("test", StringComparison.OrdinalIgnoreCase))
        {
            RollTrackerService.AddTestRolls();
            MainWindow.IsOpen = true;
            return;
        }

        ToggleMainUi();
    }

    private bool TryHandleModuleToggle(string args, bool enabled)
    {
        var command = enabled ? "on" : "off";
        if (!args.StartsWith(command, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var target = args[command.Length..].Trim();
        if (target.Length == 0 || target.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            RollTrackerService.SetAllModulesEnabled(enabled);
            return true;
        }

        if (target.Equals("tod", StringComparison.OrdinalIgnoreCase))
        {
            RollTrackerService.SetEnabled(enabled);
            return true;
        }

        if (target.Equals("todrules", StringComparison.OrdinalIgnoreCase) ||
            target.Equals("tod rules", StringComparison.OrdinalIgnoreCase) ||
            target.Equals("special", StringComparison.OrdinalIgnoreCase))
        {
            Configuration.TodSpecialRulesEnabled = enabled;
            SaveConfiguration();
            ChatGui.Print($"RollTracker ToD special rules {(enabled ? "enabled" : "disabled")}.", "RollTracker");
            return true;
        }

        if (target.Equals("todsecond", StringComparison.OrdinalIgnoreCase) ||
            target.Equals("tod second", StringComparison.OrdinalIgnoreCase) ||
            target.Equals("second", StringComparison.OrdinalIgnoreCase))
        {
            RollTrackerService.SetSecondPairEnabled(enabled);
            return true;
        }

        if (target.Equals("truth", StringComparison.OrdinalIgnoreCase) ||
            target.Equals("!truth", StringComparison.OrdinalIgnoreCase))
        {
            RollTrackerService.SetTruthTriggerEnabled(enabled);
            return true;
        }

        if (target.Equals("dare", StringComparison.OrdinalIgnoreCase) ||
            target.Equals("!dare", StringComparison.OrdinalIgnoreCase))
        {
            RollTrackerService.SetDareTriggerEnabled(enabled);
            return true;
        }

        if (target.Equals("help", StringComparison.OrdinalIgnoreCase) ||
            target.Equals("!help", StringComparison.OrdinalIgnoreCase))
        {
            RollTrackerService.SetHelpTriggerEnabled(enabled);
            return true;
        }

        if (target.Equals("wifi", StringComparison.OrdinalIgnoreCase))
        {
            RollTrackerService.SetWifiEnabled(enabled);
            return true;
        }

        return false;
    }

    private bool TryHandleReversedModuleToggle(string args)
    {
        var parts = args.Split(' ', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            return false;
        }

        if (parts[1].Equals("on", StringComparison.OrdinalIgnoreCase))
        {
            return TryHandleModuleToggle($"on {parts[0]}", true);
        }

        if (parts[1].Equals("off", StringComparison.OrdinalIgnoreCase))
        {
            return TryHandleModuleToggle($"off {parts[0]}", false);
        }

        return false;
    }

    private bool TryHandleManualRoll(string args)
    {
        if (!args.StartsWith("add", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var valueText = args["add".Length..].Trim();
        if (!int.TryParse(valueText, out var value))
        {
            ChatGui.PrintError("Usage: /rt add <number>", "RollTracker");
            return true;
        }

        if (RollTrackerService.TryAddManualRoll(value, out var message))
        {
            ChatGui.Print(message, "RollTracker");
            MainWindow.IsOpen = true;
            return true;
        }

        ChatGui.PrintError(message, "RollTracker");
        return true;
    }

    private void ToggleMainUi()
    {
        MainWindow.Toggle();
    }

    private void OpenConfigUi()
    {
        MainWindow.IsOpen = true;
    }

    private void SaveConfiguration()
    {
        PluginInterface.SavePluginConfig(Configuration);
    }

    private void MigrateConfiguration()
    {
        var changed = false;

        if (Configuration.ResultCommandTemplate.Equals("/y \"{highest}\">\"{lowest}\"", StringComparison.OrdinalIgnoreCase) ||
            Configuration.ResultCommandTemplate.Equals("/y \"{highest}\">>>\"{lowest}\"", StringComparison.OrdinalIgnoreCase))
        {
            Configuration.ResultCommandTemplate = "/y \"{highest}\"({highestRoll})>>>\"{lowest}\"({lowestRoll})";
            changed = true;
        }

        Configuration.TruthPrompts ??= [];
        Configuration.DarePrompts ??= [];
        changed |= RemoveDuplicatePrompts(Configuration.TruthPrompts);
        changed |= RemoveDuplicatePrompts(Configuration.DarePrompts);

        if (Configuration.TruthPrompts.Count == 0)
        {
            Configuration.TruthPrompts.AddRange(Configuration.CreateDefaultTruthPrompts());
            changed = true;
        }

        if (Configuration.DarePrompts.Count == 0)
        {
            Configuration.DarePrompts.AddRange(Configuration.CreateDefaultDarePrompts());
            changed = true;
        }

        if (Configuration.TodSpecialRules is null)
        {
            Configuration.TodSpecialRules = [];
            changed = true;
        }

        changed |= RemoveDuplicateSpecialRules();

        if (Configuration.TodSpecialRules.Count == 0)
        {
            Configuration.TodSpecialRules.AddRange(Configuration.CreateDefaultTodSpecialRules());
            changed = true;
        }

        foreach (var rule in Configuration.TodSpecialRules)
        {
            if (rule.Roll == 999 &&
                string.IsNullOrWhiteSpace(rule.DoNotTriggerWith) &&
                rule.Text.Equals("{player} can ask both Truth and Dare.", StringComparison.Ordinal))
            {
                rule.DoNotTriggerWith = "0, 1";
                changed = true;
            }
        }

        if (changed)
        {
            SaveConfiguration();
        }
    }

    private static bool RemoveDuplicatePrompts(List<string> prompts)
    {
        var seenPrompts = new HashSet<string>(StringComparer.Ordinal);
        var deduplicatedPrompts = new List<string>();

        foreach (var originalPrompt in prompts)
        {
            var prompt = originalPrompt.Trim();
            if (string.IsNullOrWhiteSpace(prompt))
            {
                continue;
            }

            if (seenPrompts.Add(prompt))
            {
                deduplicatedPrompts.Add(prompt);
            }
        }

        if (deduplicatedPrompts.SequenceEqual(prompts, StringComparer.Ordinal))
        {
            return false;
        }

        prompts.Clear();
        prompts.AddRange(deduplicatedPrompts);
        return true;
    }

    private bool RemoveDuplicateSpecialRules()
    {
        var changed = false;
        for (var i = Configuration.TodSpecialRules.Count - 1; i >= 0; i--)
        {
            var currentRule = Configuration.TodSpecialRules[i];
            currentRule.Text = currentRule.Text.Trim();
            currentRule.DoNotTriggerWith = MergeRollLists(currentRule.DoNotTriggerWith);
            if (!currentRule.Text.Equals(Configuration.TodSpecialRules[i].Text, StringComparison.Ordinal) ||
                !currentRule.DoNotTriggerWith.Equals(Configuration.TodSpecialRules[i].DoNotTriggerWith, StringComparison.Ordinal))
            {
                changed = true;
            }

            var duplicateIndex = Configuration.TodSpecialRules.FindIndex(0, i, rule =>
                rule.Roll == currentRule.Roll &&
                rule.Text.Trim().Equals(currentRule.Text, StringComparison.Ordinal));
            if (duplicateIndex < 0)
            {
                Configuration.TodSpecialRules[i] = currentRule;
                continue;
            }

            Configuration.TodSpecialRules[duplicateIndex].DoNotTriggerWith = MergeRollLists(
                Configuration.TodSpecialRules[duplicateIndex].DoNotTriggerWith,
                currentRule.DoNotTriggerWith);
            Configuration.TodSpecialRules.RemoveAt(i);
            changed = true;
        }

        return changed;
    }

    private static string MergeRollLists(params string[] rollLists)
    {
        var values = rollLists
            .SelectMany(rollList => rollList.Split([',', ';', ' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(text => int.TryParse(text, out var value) ? value : (int?)null)
            .Where(value => value is >= 0 and <= 9999)
            .Select(value => value!.Value)
            .Distinct()
            .Order()
            .ToList();

        return string.Join(", ", values);
    }

    private static string GetPluginVersion()
    {
        var informationalVersion = typeof(Plugin).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            return informationalVersion.Split('+', 2)[0];
        }

        return typeof(Plugin).Assembly.GetName().Version?.ToString(3) ?? "unknown";
    }
}

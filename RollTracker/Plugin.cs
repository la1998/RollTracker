using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using Dalamud.Game.Command;
using Dalamud.Game.ClientState.Conditions;
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
        "/rt help - show RollTracker commands locally.";
    private const string AliasCommandHelp = "Alias for /rt. Use /rt help to see the command list.";
    private static readonly string[] CommandHelpLines =
    [
        "/rt - open or close the window.",
        "/rt help - show this command list locally.",
        "/rt on - enable all modules.",
        "/rt off - disable all modules.",
        "/rt toggle - toggle all modules.",
        "/rt tod on - enable Truth or Dare.",
        "/rt tod off - disable Truth or Dare.",
        "/rt tod toggle - toggle Truth or Dare.",
        "/rt todrules on - enable Truth or Dare special rules.",
        "/rt todrules off - disable Truth or Dare special rules.",
        "/rt todrules toggle - toggle Truth or Dare special rules.",
        "/rt todsecond on - enable !tod2 second pair rounds.",
        "/rt todsecond off - disable !tod2 second pair rounds.",
        "/rt todsecond toggle - toggle !tod2 second pair rounds.",
        "/rt truth on - enable !truth.",
        "/rt truth off - disable !truth.",
        "/rt truth toggle - toggle !truth.",
        "/rt dare on - enable !dare.",
        "/rt dare off - disable !dare.",
        "/rt dare toggle - toggle !dare.",
        "/rt help on - enable !help.",
        "/rt help off - disable !help.",
        "/rt help toggle - toggle !help.",
        "/rt alias on - enable chat alias.",
        "/rt alias off - disable chat alias.",
        "/rt alias toggle - toggle chat alias.",
        "/rt wifi on - enable !wifi.",
        "/rt wifi off - disable !wifi.",
        "/rt wifi toggle - toggle !wifi.",
        "/rt status - show module states.",
        "/rt reset - clear the current roll list.",
        "/rt end - send the current result, then clear the list.",
        "/rt add <number> - add a manual test roll to the running round.",
        "/rt test - add sample rolls for checking the UI.",
        "/rolltracker - supports the same commands as /rt.",
    ];

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
    internal static IPlayerState PlayerState { get; private set; } = null!;

    [PluginService]
    internal static ICondition Condition { get; private set; } = null!;

    [PluginService]
    internal static IDataManager DataManager { get; private set; } = null!;

    [PluginService]
    internal static IPluginLog Log { get; private set; } = null!;

    public WindowSystem WindowSystem { get; } = new("RollTracker");

    internal RollTrackerService RollTrackerService { get; }

    internal Configuration Configuration { get; }

    private MainWindow MainWindow { get; }

    private RollHistoryWindow RollHistoryWindow { get; }

    private ChangelogWindow ChangelogWindow { get; }

    private HousingDebugWindow HousingDebugWindow { get; }

    public Plugin()
    {
        var currentVersion = GetPluginVersion();
        Configuration = LoadConfiguration();
        BackupConfigurationBeforeUpdate(currentVersion);
        MigrateConfiguration();
        RollTrackerService = new RollTrackerService(
            ChatGui,
            CommandManager,
            Framework,
            ClientState,
            PlayerState,
            Condition,
            DataManager,
            Log,
            Configuration,
            SaveConfiguration);
        RollHistoryWindow = new RollHistoryWindow(RollTrackerService);
        ChangelogWindow = new ChangelogWindow(Configuration, SaveConfiguration, currentVersion);
        HousingDebugWindow = new HousingDebugWindow(RollTrackerService);
        MainWindow = new MainWindow(
            RollTrackerService,
            Configuration,
            PluginInterface,
            ChatGui,
            SaveConfiguration,
            () => RollHistoryWindow.IsOpen = true,
            ChangelogWindow.OpenHistory,
            () => HousingDebugWindow.IsOpen = true);

        WindowSystem.AddWindow(MainWindow);
        WindowSystem.AddWindow(RollHistoryWindow);
        WindowSystem.AddWindow(ChangelogWindow);
        WindowSystem.AddWindow(HousingDebugWindow);

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
        RollHistoryWindow.Dispose();
        ChangelogWindow.Dispose();
        HousingDebugWindow.Dispose();
        RollTrackerService.Dispose();
    }

    private void OnCommand(string command, string args)
    {
        var trimmedArgs = args.Trim();

        if (trimmedArgs.Equals("help", StringComparison.OrdinalIgnoreCase))
        {
            PrintCommandHelp();
            return;
        }

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

        if (TryHandleModuleToggleSwitch(trimmedArgs))
        {
            return;
        }

        if (TryHandleReversedModuleToggle(trimmedArgs))
        {
            return;
        }

        if (trimmedArgs.Equals("status", StringComparison.OrdinalIgnoreCase))
        {
            ChatGui.Print($"RollTracker ToD is {(Configuration.Enabled ? "on" : "off")}; !truth is {(Configuration.TruthTriggerEnabled ? "on" : "off")}; !dare is {(Configuration.DareTriggerEnabled ? "on" : "off")}; !help is {(Configuration.HelpTriggerEnabled ? "on" : "off")}; chat alias is {(Configuration.ChatAliasEnabled ? "on" : "off")}; ToD special rules are {(Configuration.TodSpecialRulesEnabled ? "on" : "off")}; !tod2 is {(Configuration.TodSecondPairEnabled ? "on" : "off")}; !wifi is {(Configuration.WifiEnabled ? "on" : "off")}.", "RollTracker");
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

    private static void PrintCommandHelp()
    {
        ChatGui.Print("RollTracker commands:", "RollTracker");

        foreach (var line in CommandHelpLines)
        {
            ChatGui.Print(line, "RollTracker");
        }
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
            RollTrackerService.SetTodSpecialRulesEnabled(enabled);
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

        if (target.Equals("alias", StringComparison.OrdinalIgnoreCase) ||
            target.Equals("chat alias", StringComparison.OrdinalIgnoreCase))
        {
            RollTrackerService.SetChatAliasEnabled(enabled);
            return true;
        }

        if (target.Equals("wifi", StringComparison.OrdinalIgnoreCase))
        {
            RollTrackerService.SetWifiEnabled(enabled);
            return true;
        }

        return false;
    }

    private bool TryHandleModuleToggleSwitch(string args)
    {
        if (IsToggleWord(args))
        {
            RollTrackerService.SetAllModulesEnabled(!HasAnyModuleEnabled());
            return true;
        }

        var target = string.Empty;
        if (args.StartsWith("toggle ", StringComparison.OrdinalIgnoreCase))
        {
            target = args["toggle ".Length..].Trim();
        }
        else if (args.StartsWith("toggel ", StringComparison.OrdinalIgnoreCase))
        {
            target = args["toggel ".Length..].Trim();
        }
        else
        {
            var parts = args.Split(' ', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2 || !IsToggleWord(parts[1]))
            {
                return false;
            }

            target = parts[0];
        }

        return ToggleModuleTarget(target);
    }

    private bool ToggleModuleTarget(string target)
    {
        if (target.Length == 0 || target.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            RollTrackerService.SetAllModulesEnabled(!HasAnyModuleEnabled());
            return true;
        }

        if (target.Equals("tod", StringComparison.OrdinalIgnoreCase))
        {
            RollTrackerService.SetEnabled(!Configuration.Enabled);
            return true;
        }

        if (target.Equals("todrules", StringComparison.OrdinalIgnoreCase) ||
            target.Equals("tod rules", StringComparison.OrdinalIgnoreCase) ||
            target.Equals("special", StringComparison.OrdinalIgnoreCase))
        {
            RollTrackerService.SetTodSpecialRulesEnabled(!Configuration.TodSpecialRulesEnabled);
            return true;
        }

        if (target.Equals("todsecond", StringComparison.OrdinalIgnoreCase) ||
            target.Equals("tod second", StringComparison.OrdinalIgnoreCase) ||
            target.Equals("second", StringComparison.OrdinalIgnoreCase))
        {
            RollTrackerService.SetSecondPairEnabled(!Configuration.TodSecondPairEnabled);
            return true;
        }

        if (target.Equals("truth", StringComparison.OrdinalIgnoreCase) ||
            target.Equals("!truth", StringComparison.OrdinalIgnoreCase))
        {
            RollTrackerService.SetTruthTriggerEnabled(!Configuration.TruthTriggerEnabled);
            return true;
        }

        if (target.Equals("dare", StringComparison.OrdinalIgnoreCase) ||
            target.Equals("!dare", StringComparison.OrdinalIgnoreCase))
        {
            RollTrackerService.SetDareTriggerEnabled(!Configuration.DareTriggerEnabled);
            return true;
        }

        if (target.Equals("help", StringComparison.OrdinalIgnoreCase) ||
            target.Equals("!help", StringComparison.OrdinalIgnoreCase))
        {
            RollTrackerService.SetHelpTriggerEnabled(!Configuration.HelpTriggerEnabled);
            return true;
        }

        if (target.Equals("alias", StringComparison.OrdinalIgnoreCase) ||
            target.Equals("chat alias", StringComparison.OrdinalIgnoreCase))
        {
            RollTrackerService.SetChatAliasEnabled(!Configuration.ChatAliasEnabled);
            return true;
        }

        if (target.Equals("wifi", StringComparison.OrdinalIgnoreCase))
        {
            RollTrackerService.SetWifiEnabled(!Configuration.WifiEnabled);
            return true;
        }

        return false;
    }

    private bool HasAnyModuleEnabled()
    {
        return Configuration.Enabled ||
            Configuration.TodSecondPairEnabled ||
            Configuration.TodSpecialRulesEnabled ||
            Configuration.TruthTriggerEnabled ||
            Configuration.DareTriggerEnabled ||
            Configuration.HelpTriggerEnabled ||
            Configuration.ChatAliasEnabled ||
            Configuration.WifiEnabled;
    }

    private static bool IsToggleWord(string text)
    {
        return text.Equals("toggle", StringComparison.OrdinalIgnoreCase) ||
            text.Equals("toggel", StringComparison.OrdinalIgnoreCase);
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

        if (IsToggleWord(parts[1]))
        {
            return TryHandleModuleToggleSwitch(args);
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
        try
        {
            var configFile = GetPluginConfigFilePath();
            var configDirectory = Path.GetDirectoryName(configFile);
            if (!string.IsNullOrWhiteSpace(configDirectory))
            {
                Directory.CreateDirectory(configDirectory);
            }

            var json = JsonSerializer.Serialize(Configuration, new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                WriteIndented = true,
            });
            File.WriteAllText(configFile, json);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to save RollTracker config.");
            ChatGui.PrintError($"Could not save RollTracker config: {ex.Message}", "RollTracker");
        }
    }

    private static Configuration LoadConfiguration()
    {
        try
        {
            var configFile = GetPluginConfigFilePath();
            if (!File.Exists(configFile))
            {
                return new Configuration();
            }

            var configJson = File.ReadAllText(configFile);
            return JsonSerializer.Deserialize<Configuration>(configJson) ?? new Configuration();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load RollTracker config.");
            ChatGui.PrintError($"Could not load RollTracker config from plugin folder: {ex.Message}", "RollTracker");
            return new Configuration();
        }
    }

    private static string GetPluginConfigFilePath()
    {
        return Path.Combine(PluginInterface.GetPluginConfigDirectory(), PluginInterface.ConfigFile.Name);
    }

    private void BackupConfigurationBeforeUpdate(string currentVersion)
    {
        try
        {
            var configFile = GetPluginConfigFilePath();
            if (!File.Exists(configFile) ||
                string.Equals(Configuration.LastConfigBackupVersion, currentVersion, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var configDirectory = Path.GetDirectoryName(configFile) ?? PluginInterface.GetPluginConfigDirectory();
            var backupFile = Path.Combine(configDirectory, $"{Path.GetFileName(configFile)}.bak");

            File.Copy(configFile, backupFile, overwrite: true);
            Configuration.LastConfigBackupVersion = currentVersion;
            SaveConfiguration();
            Log.Information("Backed up RollTracker config before update to {Version}: {BackupFile}", currentVersion, backupFile);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to back up RollTracker config before update.");
            ChatGui.PrintError($"Could not back up RollTracker config before update: {ex.Message}", "RollTracker");
        }
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

        if (string.IsNullOrWhiteSpace(Configuration.NotEnoughPlayersResultText))
        {
            Configuration.NotEnoughPlayersResultText = "/y Not enough players for a round.";
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(Configuration.TodSecondPairNotEnoughPlayersResultText))
        {
            Configuration.TodSecondPairNotEnoughPlayersResultText = "/y 2nd: Not enough players for second pair.";
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(Configuration.TodSecondPairNotEnoughRoundPlayersResultText))
        {
            Configuration.TodSecondPairNotEnoughRoundPlayersResultText = "/y Not enough players for a !tod2 round.";
            changed = true;
        }

        if (Configuration.HelpInitialDelayMilliseconds <= 0)
        {
            Configuration.HelpInitialDelayMilliseconds = 500;
            changed = true;
        }

        if (Configuration.TodSecondPairResultLineDelayMilliseconds <= 0)
        {
            Configuration.TodSecondPairResultLineDelayMilliseconds = 1500;
            changed = true;
        }

        Configuration.HelpLines ??= [];
        if (Configuration.HelpLines.Count == 0)
        {
            Configuration.HelpLines.AddRange(Configuration.CreateDefaultHelpLines());
            changed = true;
        }

        changed |= RemoveDuplicatePrompts(Configuration.HelpLines);

        if (string.IsNullOrWhiteSpace(Configuration.ChatAliasWord))
        {
            Configuration.ChatAliasWord = "alias";
            changed = true;
        }

        Configuration.ChatAliasCommands ??= [];
        for (var i = Configuration.ChatAliasCommands.Count - 1; i >= 0; i--)
        {
            var aliasCommand = Configuration.ChatAliasCommands[i];
            aliasCommand.TriggerText = aliasCommand.TriggerText.Trim();
            aliasCommand.RtCommandArgs = aliasCommand.RtCommandArgs.Trim();
            if (string.IsNullOrWhiteSpace(aliasCommand.TriggerText) ||
                string.IsNullOrWhiteSpace(aliasCommand.RtCommandArgs))
            {
                Configuration.ChatAliasCommands.RemoveAt(i);
                changed = true;
                continue;
            }

            Configuration.ChatAliasCommands[i] = aliasCommand;
        }

        if (string.IsNullOrWhiteSpace(Configuration.HelpPreset))
        {
            Configuration.HelpPreset = "Standard";
            changed = true;
        }

        if (!Configuration.HelpPreset.Equals("Standard", StringComparison.Ordinal) &&
            !Configuration.HelpPreset.Equals("Compact", StringComparison.Ordinal) &&
            !Configuration.HelpPreset.Equals("Macro Mode", StringComparison.Ordinal))
        {
            Configuration.HelpPreset = "Standard";
            changed = true;
        }

        if (Configuration.HelpMacroText.Equals("Commands: {activeCommands}", StringComparison.Ordinal))
        {
            Configuration.HelpMacroText = string.Empty;
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(Configuration.UiLayout) ||
            (!Configuration.UiLayout.Equals("Standard", StringComparison.Ordinal) &&
             !Configuration.UiLayout.Equals("Modern", StringComparison.Ordinal) &&
             !Configuration.UiLayout.Equals("Legacy", StringComparison.Ordinal)))
        {
            Configuration.UiLayout = "Standard";
            changed = true;
        }

        changed |= EnsureTextCommandPrefix(
            value => Configuration.NotEnoughPlayersResultText = value,
            Configuration.NotEnoughPlayersResultText);
        changed |= EnsureTextCommandPrefix(
            value => Configuration.TodSecondPairNotEnoughRoundPlayersResultText = value,
            Configuration.TodSecondPairNotEnoughRoundPlayersResultText);
        changed |= EnsureTextCommandPrefix(
            value => Configuration.TodSecondPairNotEnoughPlayersResultText = value,
            Configuration.TodSecondPairNotEnoughPlayersResultText);

        Configuration.TruthPrompts ??= [];
        Configuration.DarePrompts ??= [];
        changed |= RemoveDuplicatePrompts(Configuration.TruthPrompts);
        changed |= RemoveDuplicatePrompts(Configuration.DarePrompts);
        Configuration.TruthPromptSets ??= [];
        Configuration.DarePromptSets ??= [];
        changed |= MigratePromptSets(Configuration.TruthPromptSets, Configuration.TruthPrompts, Configuration.CreateDefaultTruthPrompts());
        changed |= MigratePromptSets(Configuration.DarePromptSets, Configuration.DarePrompts, Configuration.CreateDefaultDarePrompts());

        if (Configuration.TodSpecialRules is null)
        {
            Configuration.TodSpecialRules = [];
            changed = true;
        }

        if (Configuration.TodSpecialRules.Count == 0)
        {
            Configuration.TodSpecialRules.AddRange(Configuration.CreateDefaultTodSpecialRules());
            changed = true;
        }

        changed |= RemoveDuplicateSpecialRules(Configuration.TodSpecialRules);
        changed |= EnsureDefaultSpecialRuleBlockers(Configuration.TodSpecialRules);
        Configuration.TodSpecialRuleSets ??= [];
        changed |= MigrateSpecialRuleSets(Configuration.TodSpecialRuleSets, Configuration.TodSpecialRules);

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

    private static bool EnsureTextCommandPrefix(Action<string> setValue, string value)
    {
        var trimmedValue = value.TrimStart();
        if (string.IsNullOrWhiteSpace(trimmedValue) || trimmedValue.StartsWith("/", StringComparison.Ordinal))
        {
            return false;
        }

        setValue($"/y {trimmedValue}");
        return true;
    }

    private static bool MigratePromptSets(List<TodPromptSet> promptSets, List<string> legacyPrompts, List<string> defaultPrompts)
    {
        var changed = false;

        if (promptSets.Count == 0)
        {
            var prompts = legacyPrompts.Count > 0
                ? legacyPrompts
                : defaultPrompts;
            promptSets.Add(new TodPromptSet
            {
                Name = "Set 1",
                Enabled = true,
                Prompts = [.. prompts],
            });
            changed = true;
        }

        for (var i = 0; i < promptSets.Count; i++)
        {
            var promptSet = promptSets[i];
            if (string.IsNullOrWhiteSpace(promptSet.Name))
            {
                promptSet.Name = $"Set {i + 1}";
                changed = true;
            }

            promptSet.Prompts ??= [];
            changed |= RemoveDuplicatePrompts(promptSet.Prompts);
            promptSets[i] = promptSet;
        }

        return changed;
    }

    private static bool MigrateSpecialRuleSets(List<TodSpecialRuleSet> specialRuleSets, List<TodSpecialRule> legacyRules)
    {
        var changed = false;

        if (specialRuleSets.Count == 0)
        {
            specialRuleSets.Add(new TodSpecialRuleSet
            {
                Name = "Set 1",
                Enabled = true,
                Rules = [.. legacyRules],
            });
            changed = true;
        }

        for (var i = 0; i < specialRuleSets.Count; i++)
        {
            var ruleSet = specialRuleSets[i];
            if (string.IsNullOrWhiteSpace(ruleSet.Name))
            {
                ruleSet.Name = $"Set {i + 1}";
                changed = true;
            }

            ruleSet.Rules ??= [];
            changed |= RemoveDuplicateSpecialRules(ruleSet.Rules);
            changed |= EnsureDefaultSpecialRuleBlockers(ruleSet.Rules);
            specialRuleSets[i] = ruleSet;
        }

        return changed;
    }

    private static bool EnsureDefaultSpecialRuleBlockers(List<TodSpecialRule> rules)
    {
        var changed = false;
        foreach (var rule in rules)
        {
            if (rule.Roll == 999 &&
                string.IsNullOrWhiteSpace(rule.DoNotTriggerWith) &&
                rule.Text.Equals("{player} can ask both Truth and Dare.", StringComparison.Ordinal))
            {
                rule.DoNotTriggerWith = "0, 1";
                changed = true;
            }
        }

        return changed;
    }

    private static bool RemoveDuplicateSpecialRules(List<TodSpecialRule> rules)
    {
        var changed = false;
        for (var i = rules.Count - 1; i >= 0; i--)
        {
            var currentRule = rules[i];
            currentRule.Text = currentRule.Text.Trim();
            currentRule.DoNotTriggerWith = MergeRollLists(currentRule.DoNotTriggerWith);
            if (!currentRule.Text.Equals(rules[i].Text, StringComparison.Ordinal) ||
                !currentRule.DoNotTriggerWith.Equals(rules[i].DoNotTriggerWith, StringComparison.Ordinal))
            {
                changed = true;
            }

            var duplicateIndex = rules.FindIndex(0, i, rule =>
                rule.Roll == currentRule.Roll &&
                rule.Text.Trim().Equals(currentRule.Text, StringComparison.Ordinal));
            if (duplicateIndex < 0)
            {
                rules[i] = currentRule;
                continue;
            }

            rules[duplicateIndex].DoNotTriggerWith = MergeRollLists(
                rules[duplicateIndex].DoNotTriggerWith,
                currentRule.DoNotTriggerWith);
            rules.RemoveAt(i);
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

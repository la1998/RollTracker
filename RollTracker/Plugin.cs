using System;
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
        "/rt on todsecond - enable Truth or Dare second pair.\n" +
        "/rt off todsecond - disable Truth or Dare second pair.\n" +
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

        WindowSystem.AddWindow(MainWindow);

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

        if (trimmedArgs.Equals("status", StringComparison.OrdinalIgnoreCase))
        {
            ChatGui.Print($"RollTracker ToD is {(Configuration.Enabled ? "on" : "off")}; ToD special rules are {(Configuration.TodSpecialRulesEnabled ? "on" : "off")}; ToD second pair is {(Configuration.TodSecondPairEnabled ? "on" : "off")}; !wifi is {(Configuration.WifiEnabled ? "on" : "off")}.", "RollTracker");
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
            Configuration.TodSecondPairEnabled = enabled;
            SaveConfiguration();
            ChatGui.Print($"RollTracker ToD second pair {(enabled ? "enabled" : "disabled")}.", "RollTracker");
            return true;
        }

        if (target.Equals("wifi", StringComparison.OrdinalIgnoreCase))
        {
            RollTrackerService.SetWifiEnabled(enabled);
            return true;
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
        if (Configuration.ResultCommandTemplate.Equals("/y \"{highest}\">\"{lowest}\"", StringComparison.OrdinalIgnoreCase) ||
            Configuration.ResultCommandTemplate.Equals("/y \"{highest}\">>>\"{lowest}\"", StringComparison.OrdinalIgnoreCase))
        {
            Configuration.ResultCommandTemplate = "/y \"{highest}\"({highestRoll})>>>\"{lowest}\"({lowestRoll})";
            SaveConfiguration();
        }
    }
}

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
        "Commands: /rt opens the UI; /rt on or /rt on all enables all modules; /rt off or /rt off all disables all modules; " +
        "/rt on tod and /rt off tod control Truth or Dare; /rt on wifi and /rt off wifi control !wifi; " +
        "/rt status shows module states; /rt reset clears rolls; /rt end ends the current round; /rt test adds test rolls.";

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
            HelpMessage = CommandHelp,
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
            ChatGui.Print($"RollTracker ToD is {(Configuration.Enabled ? "on" : "off")}; !wifi is {(Configuration.WifiEnabled ? "on" : "off")}.", "RollTracker");
            return;
        }

        if (trimmedArgs.Equals("end", StringComparison.OrdinalIgnoreCase))
        {
            RollTrackerService.FinishRoundAndReset();
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

        if (target.Equals("wifi", StringComparison.OrdinalIgnoreCase))
        {
            RollTrackerService.SetWifiEnabled(enabled);
            return true;
        }

        return false;
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
}

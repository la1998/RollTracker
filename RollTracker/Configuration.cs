using Dalamud.Configuration;

namespace RollTracker;

public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public bool Enabled { get; set; } = true;

    public bool AutoDisableWhenLeavingHousing { get; set; } = true;

    public int MacroDurationSeconds { get; set; } = 60;

    public int MacroLineDelayMilliseconds { get; set; } = 1000;

    public string MacroText { get; set; } = "/y ♦ Time for Truth or Dare ♦  Highest number asks the lowest number, \"Truth or Dare?\"  Type /random in chat! 60 seconds... And GO!\n/wait 50\n/y 10 seconds remain...\n/wait 10\n/y End";

    public string ResultCommandTemplate { get; set; } = "/y \"{highest}\">\"{lowest}\"";
}

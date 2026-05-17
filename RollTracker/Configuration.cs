using Dalamud.Configuration;

namespace RollTracker;

public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public bool Enabled { get; set; } = true;

    public int MacroDurationSeconds { get; set; } = 60;

    public int MacroLineDelayMilliseconds { get; set; } = 1000;

    public string MacroText { get; set; } = "/y ♪ Type /random in chat!  Highest number asks the lowest number, \"Truth or Dare?\" 60 seconds... Begin!\n/wait 55\n/y 5 seconds remain...\n/wait 5\n/y End";
}

using Dalamud.Configuration;

namespace RollTracker;

public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public bool Enabled { get; set; } = true;

    public bool TodSpecialRulesEnabled { get; set; } = true;

    public bool TodSecondPairEnabled { get; set; }

    public bool WifiEnabled { get; set; } = true;

    public bool AutoDisableWhenLeavingHousing { get; set; } = true;

    public int MacroDurationSeconds { get; set; } = 60;

    public int MacroLineDelayMilliseconds { get; set; } = 1000;

    public string MacroText { get; set; } = "/y ♦ Time for Truth or Dare ♦  Highest number asks the lowest number, \"Truth or Dare?\"  Type /random in chat! 60 seconds... And GO!\n/wait 50\n/y 10 seconds remain...\n/wait 10\n/y End";

    public string ResultCommandTemplate { get; set; } = "/y \"{highest}\"({highestRoll})>>>\"{lowest}\"({lowestRoll})";

    public string WifiChatChannel { get; set; } = "Yell";

    public string WifiMacroText { get; set; } = "KinkHouse Shells and Discord:\nLightless - our main sync:\nID: LLS-SWN693A68P5R  PW: KinkHausOCE\n\nPlayerSync - our optional/backup sync:\nID: MSS-6AC6326WFU4P  PW: KinkHausOCE\n\nDiscord:\nhttps://discord.gg/7N7xaghGTr";
}

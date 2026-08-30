using System.Collections.Generic;
using Dalamud.Configuration;

namespace RollTracker;

public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public bool Enabled { get; set; } = true;

    public bool TodSpecialRulesEnabled { get; set; } = true;

    public bool TodSecondPairEnabled { get; set; }

    public bool TruthTriggerEnabled { get; set; } = true;

    public bool DareTriggerEnabled { get; set; } = true;

    public bool HelpTriggerEnabled { get; set; } = true;

    public bool ChatAliasEnabled { get; set; }

    public bool WifiEnabled { get; set; } = true;

    public bool AutoDisableWhenLeavingHousing { get; set; } = true;

    public bool AdvancedMode { get; set; }

    public string LastSeenChangelogVersion { get; set; } = string.Empty;

    public string UiLayout { get; set; } = "Modern";

    public string UiTheme { get; set; } = "Dalamud Blue";

    public int MacroDurationSeconds { get; set; } = 60;

    public int TodSecondPairMacroDurationSeconds { get; set; } = 60;

    public int MacroLineDelayMilliseconds { get; set; } = 1500;

    public int TodSecondPairMacroLineDelayMilliseconds { get; set; } = 1500;

    public int TodSpecialRuleLineDelayMilliseconds { get; set; } = 1500;

    public string MacroText { get; set; } = "/y ♦ Time for Truth or Dare ♦  Highest number asks the lowest number, \"Truth or Dare?\"  Type /random in chat! 60 seconds... And GO!\n/wait 50\n/y 10 seconds remain...\n/wait 10\n/y End";

    public string ResultCommandTemplate { get; set; } = "/y \"{highest}\"({highestRoll})>>>\"{lowest}\"({lowestRoll})";

    public string NotEnoughPlayersResultText { get; set; } = "/y Not enough players for a round.";

    public string TodSecondPairMacroText { get; set; } = "/y ♦ Time for Truth or Dare 2 ♦  Highest asks lowest, second highest asks second lowest,  \"Truth or Dare?\" Type /random in chat! 60 seconds... And GO!\n/wait 50\n/y 10 seconds remain...\n/wait 10\n/y End";

    public string TodSecondPairResultCommandTemplate { get; set; } = "/y \"{highest}\"({highestRoll})>>>\"{lowest}\"({lowestRoll})\n/y 2nd: \"{secondHighest}\"({secondHighestRoll})>>>\"{secondLowest}\"({secondLowestRoll})";

    public string TodSecondPairNotEnoughRoundPlayersResultText { get; set; } = "/y Not enough players for a !tod2 round.";

    public string TodSecondPairNotEnoughPlayersResultText { get; set; } = "/y 2nd: Not enough players for second pair.";

    public string TodPromptChatChannel { get; set; } = "Yell";

    public string HelpChatChannel { get; set; } = "Yell";

    public string ChatAliasWord { get; set; } = "alias";

    public List<ChatAliasCommand> ChatAliasCommands { get; set; } = [];

    public int HelpInitialDelayMilliseconds { get; set; } = 500;

    public int HelpLineDelayMilliseconds { get; set; } = 1500;

    public string HelpPreset { get; set; } = "Standard";

    public string HelpMacroText { get; set; } = string.Empty;

    public List<string> HelpLines { get; set; } = [];

    public List<TodSpecialRule> TodSpecialRules { get; set; } = [];

    public List<TodSpecialRuleSet> TodSpecialRuleSets { get; set; } = [];

    public List<string> TruthPrompts { get; set; } = [];

    public List<string> DarePrompts { get; set; } = [];

    public List<TodPromptSet> TruthPromptSets { get; set; } = [];

    public List<TodPromptSet> DarePromptSets { get; set; } = [];

    public string WifiChatChannel { get; set; } = "Yell";

    public int WifiMacroLineDelayMilliseconds { get; set; } = 1500;

    public string WifiMacroText { get; set; } = "KinkHouse Shells and Discord:\nLightless - our main sync:\nID: LLS-SWN693A68P5R  PW: KinkHausOCE\n\nPlayerSync - our optional/backup sync:\nID: MSS-6AC6326WFU4P  PW: KinkHausOCE\n\nDiscord:\nhttps://discord.gg/7N7xaghGTr";

    public static List<TodSpecialRule> CreateDefaultTodSpecialRules()
    {
        return
        [
            new TodSpecialRule { Roll = 0, Text = "{player} gets asked Truth and Dare." },
            new TodSpecialRule { Roll = 1, Text = "{player} gets asked Truth and Dare." },
            new TodSpecialRule { Roll = 999, Text = "{player} can ask both Truth and Dare.", DoNotTriggerWith = "0, 1" },
        ];
    }

    public static List<string> CreateDefaultHelpLines()
    {
        return
        [
            "!help - Show currently available RollTracker chat commands.",
            "!tod - Start a Truth or Dare roll round.",
            "!tod2 - Start a second-pair Truth or Dare roll round.",
            "!truth - Send a random Truth prompt.",
            "!dare - Send a random Dare prompt.",
            "!wifi - Show KinkHouse Shells and Discord info.",
        ];
    }

    public static List<string> CreateDefaultTruthPrompts()
    {
        return
        [
            "Anyone here whose design, or kinks they mentioned, makes you curious about them?",
            "What was the funniest \"oops\" in ERP you had?",
            "Ever had a laugh while being kinky, and what happened?",
            "What does your ideal partner look and behave like?",
            "Ever got to try out your biggest fantasy, and how did that turn out?",
            "What is the lore/explanation behind your appearance and/or your name?",
            "What is a guilty pleasure of yours (kinky, either, or normal) that you really care about?",
            "What are you looking to find or achieve here the most?",
            "What is the best or right way for someone to approach you? What should someone really know about you if they want to get to know you?",
            "What is one of the biggest actual embarrassments you have had in FFXIV or kink circles?",
            "What is the thing you want the most right now, and who here would be best suited for it?",
            "Pick someone here you are not already in a relationship or dating with. You are going out for lunch/dinner - who is it and what are you getting?",
            "What is a life lesson that was difficult for you or meant a lot to you?",
            "Share one of your best relationship highlights.",
            "What is a vice you have?",
            "Favourite moment in the KH so far?",
            "What is a side of you most people here do not know about?",
            "What do you genuinely think of the person sitting opposite of you?",
            "Who here have you imagined yourself in a NSFW or kinky way with? Excluding existing relationships.",
            "What is your most deviant fetish?",
            "What is your favourite type (or types) of restraint?",
            "What animal is your favourite? And do you own any pets?",
            "What do you care for the most in a person?",
            "What is a major turn-on almost no one here knows about?",
            "What is a major turn-off almost no one here knows about?",
            "What is something you are actually really good at, but people do not know about?",
            "What is the furthest you have gone for someone? Share what you can.",
            "What is the best meal you have ever had? Describe it as best you can.",
            "What is the most interesting thing you have put under your clothes or underwear?",
            "What was the last thing that made you genuinely scream?",
            "Have you ever stalked someone? Tell us about it.",
            "What is the best gift someone could give you, here and/or in general?",
            "What is a question you wish you were asked more often?",
        ];
    }

    public static List<string> CreateDefaultDarePrompts()
    {
        return
        [
            "Talk in UwU speak (https://lingojam.com/Englishtouwu if you do not want to do it manually) for x rounds",
            "Strip any parts, or be completely naked for x rounds",
            "Write a bad pick up line for (person)",
            "Ask a person of your choice to sit on your lap, or let them sit on your lap",
            "Dress in your lewdest non-naked outfit for x rounds",
            "Show us the last lewd screenshot you made (or the last one you can show)",
            "Equip one of your kinks as a moodle for x rounds",
            "Roll /random for the number of outfits you have, and equip the rolled number",
            "Pick a person you are not familiar with whatsoever, approach them with a compliment, and try to learn at least one thing about them that you are curious about",
            "Let me pick your outfit for the next 20m",
            "Pick 3 people from those present and give each at least one compliment",
            "Describe 3 people here with songs",
            "End every sentence with <whatever> for the next 20m",
            "Make sure to greet every person who comes or leaves (including going AFK)",
            "Make sure to greet every person who comes or leaves while addressing them with Miss/Mister and bowing",
            "Find someone here to Strip-Deathroll",
            "Come be my chair or footrest for the next 15m",
            "Let me be your chair or footrest for the next 15m",
            "Show off the most cursed thing you have",
            "Showcase your latest addition(s) to your kink toolbelt (mod, plugin, trick, or whatever)",
            "Change your colour scheme to this for 30m",
        ];
    }
}

public sealed class TodPromptSet
{
    public string Name { get; set; } = "Set 1";

    public bool Enabled { get; set; } = true;

    public List<string> Prompts { get; set; } = [];
}

public sealed class TodSpecialRuleSet
{
    public string Name { get; set; } = "Set 1";

    public bool Enabled { get; set; } = true;

    public List<TodSpecialRule> Rules { get; set; } = [];
}

public sealed class TodSpecialRule
{
    public int Roll { get; set; }

    public string Text { get; set; } = string.Empty;

    public string DoNotTriggerWith { get; set; } = string.Empty;
}

public sealed class ChatAliasCommand
{
    public bool Enabled { get; set; } = true;

    public string TriggerText { get; set; } = string.Empty;

    public string RtCommandArgs { get; set; } = string.Empty;
}

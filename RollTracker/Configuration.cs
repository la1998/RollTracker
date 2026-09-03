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

    public bool LinkSuggestionsToTodModules { get; set; }

    public bool HelpTriggerEnabled { get; set; } = true;

    public bool ChatAliasEnabled { get; set; }

    public bool ChatAliasAllowEnableWhenDisabled { get; set; }

    public bool WifiEnabled { get; set; } = true;

    public bool AutoDisableWhenLeavingHousing { get; set; } = true;

    public bool AutoEnableWhenEnteringHousing { get; set; } = true;

    public bool AutoDisableOnLeavingHousingInterior { get; set; } = true;

    public bool AutoDisableOnEnteringHousingInterior { get; set; }

    public bool AutoDisableOnLeavingResidentialArea { get; set; }

    public bool AutoDisableOnTerritoryChange { get; set; }

    public bool AutoDisableAffectsTod { get; set; } = true;

    public bool AutoDisableAffectsTodSecondPair { get; set; } = true;

    public bool AutoDisableAffectsTodSpecialRules { get; set; } = true;

    public bool AutoDisableAffectsTruth { get; set; } = true;

    public bool AutoDisableAffectsDare { get; set; } = true;

    public bool AutoDisableAffectsHelp { get; set; } = true;

    public bool AutoDisableAffectsChatAlias { get; set; } = true;

    public bool AutoDisableAffectsWifi { get; set; } = true;

    public bool AutoEnableAffectsTod { get; set; } = true;

    public bool AutoEnableAffectsTodSecondPair { get; set; } = true;

    public bool AutoEnableAffectsTodSpecialRules { get; set; } = true;

    public bool AutoEnableAffectsTruth { get; set; } = true;

    public bool AutoEnableAffectsDare { get; set; } = true;

    public bool AutoEnableAffectsHelp { get; set; } = true;

    public bool AutoEnableAffectsChatAlias { get; set; } = true;

    public bool AutoEnableAffectsWifi { get; set; } = true;

    public List<HousingAddressEntry> AutoOnHousingAddresses { get; set; } = [];

    public List<ModuleStatusEffect> ModuleStatusEffects { get; set; } = [];

    public List<ModuleStatusMacro> ModuleStatusMacros { get; set; } = [];

    public bool AdvancedMode { get; set; }

    public string LastSeenChangelogVersion { get; set; } = string.Empty;

    public string LastConfigBackupVersion { get; set; } = string.Empty;

    public string UiLayout { get; set; } = "Standard";

    public string UiTheme { get; set; } = "Dalamud Blue";

    public int MacroDurationSeconds { get; set; } = 60;

    public int TodSecondPairMacroDurationSeconds { get; set; } = 60;

    public int MacroLineDelayMilliseconds { get; set; } = 1500;

    public int TodSecondPairMacroLineDelayMilliseconds { get; set; } = 1500;

    public int TodSecondPairResultLineDelayMilliseconds { get; set; } = 1500;

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

    public string ChatAliasFeedbackChatChannel { get; set; } = "Say";

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

    public string WifiMacroText { get; set; } = string.Empty;

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
            "!wifi - Show Shells and Discord info.",
        ];
    }

    public static List<string> CreateDefaultTruthPrompts()
    {
        return
        [
            "What is the weirdest thing you find attractive in someone?",
            "Have you ever lied during a Truth or Dare game?",
            "Who here do you think would be the most dangerous person to date?",
            "What is the most embarrassing thing you've done to impress someone?",
            "What is something you pretend not to care about but actually care about a lot?",
            "Have you ever stalked someone's social media way further back than you'd admit?",
            "What is your biggest weakness when someone is flirting with you?",
            "What is the strangest dream you've ever had about someone you know?",
            "Who here do you think knows you the least?",
            "What is something you've done because you were jealous?",
            "What is the worst excuse you've ever used to avoid someone?",
            "Have you ever developed feelings for someone you originally didn't like?",
            "What is one compliment you still remember because it meant a lot to you?",
            "What personality trait instantly makes someone attractive to you?",
            "What is something embarrassing you secretly enjoy?",
            "Have you ever completely misunderstood someone flirting with you?",
            "What is the boldest thing you've ever done because you liked someone?",
            "If everyone here had to rate your flirting skills from 1-10, what score do you think you'd get?",
            "What is something about yourself that you're surprisingly insecure about?",
            "Who here do you think you'd get along with best if you were stuck together for an entire week?",
        ];
    }

    public static List<string> CreateDefaultDarePrompts()
    {
        return
        [
            "Pick someone and give them the cheesiest pickup line you can think of.",
            "Describe your character/avatar like you're trying to sell them on a dating website.",
            "Pick another player and dramatically confess your completely fictional love for them.",
            "Give everyone currently playing a ridiculous nickname.",
            "Describe your ideal date using only five words.",
            "Pick someone and give them three compliments.",
            "Pretend you're an NPC and give another player a completely ridiculous quest.",
            "Speak only in questions until your next turn.",
            "Narrate everything your character does for the next 3 rounds like an overly dramatic documentary narrator.",
            "Let the group choose three random words that you have to naturally use in your next message.",
            "Pick another player and create the most ridiculous romantic backstory possible about how your characters supposedly met.",
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

    public bool FeedbackEnabled { get; set; }

    public string TriggerText { get; set; } = string.Empty;

    public string RtCommandArgs { get; set; } = string.Empty;
}

public sealed class HousingAddressEntry
{
    public bool Enabled { get; set; } = true;

    public string Name { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public string DataCenterName { get; set; } = string.Empty;

    public string WorldName { get; set; } = string.Empty;

    public ushort WorldId { get; set; }

    public string DistrictName { get; set; } = string.Empty;

    public uint TerritoryTypeId { get; set; }

    public uint OriginalHouseTerritoryTypeId { get; set; }

    public sbyte WardIndex { get; set; }

    public sbyte PlotIndex { get; set; }

    public byte Division { get; set; }

    public short RoomNumber { get; set; }

    public bool IsApartment { get; set; }

    public ulong HouseId { get; set; }
}

public sealed class ModuleStatusEffect
{
    public bool Enabled { get; set; } = true;

    public bool IsApplied { get; set; }

    public bool HonorificIsApplied { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool UseMoodle { get; set; } = true;

    public string MoodleName { get; set; } = string.Empty;

    public bool UseHonorific { get; set; }

    public string HonorificTitle { get; set; } = string.Empty;

    public string HonorificPosition { get; set; } = "title";

    public string HonorificColor { get; set; } = string.Empty;

    public string HonorificGlow { get; set; } = string.Empty;

    public int HonorificPriority { get; set; } = 1;

    public bool TriggerOnTod { get; set; } = true;

    public bool TriggerOnTodSecondPair { get; set; }

    public bool TriggerOnTodSpecialRules { get; set; }

    public bool TriggerOnTruth { get; set; }

    public bool TriggerOnDare { get; set; }

    public bool TriggerOnHelp { get; set; }

    public bool TriggerOnChatAlias { get; set; }

    public bool TriggerOnWifi { get; set; }
}

public sealed class ModuleStatusMacro
{
    public bool Enabled { get; set; } = true;

    public bool IsApplied { get; set; }

    public string Name { get; set; } = string.Empty;

    public string EnableMacroText { get; set; } = string.Empty;

    public string DisableMacroText { get; set; } = string.Empty;

    public bool TriggerOnTod { get; set; } = true;

    public bool TriggerOnTodSecondPair { get; set; }

    public bool TriggerOnTodSpecialRules { get; set; }

    public bool TriggerOnTruth { get; set; }

    public bool TriggerOnDare { get; set; }

    public bool TriggerOnHelp { get; set; }

    public bool TriggerOnChatAlias { get; set; }

    public bool TriggerOnWifi { get; set; }
}

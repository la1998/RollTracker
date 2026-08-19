# RollTracker

It reads chat messages through Dalamud chat events, detects normal `/random` rolls, stores the first roll per player for the current round, and shows the round in a small ImGui window. When `!tod` appears in chat while the plugin is enabled, it starts a configured plugin macro and a timed round. When the configured duration ends, it sends the result to yell chat as `"HighestPlayer"(999)>>>"LowestPlayer"(1)` and clears the list.

This plugin does not use networking or external APIs. It does send configured slash commands and a final `/y` message when enabled, so keep it private and use it only where everyone involved expects it.

## Custom Repository

Add this URL in Dalamud under `/xlsettings` > `Experimental` > `Custom Plugin Repositories`:

```text
https://raw.githubusercontent.com/la1998/RollTracker/main/repo.json
```

## Commands

- `/rt` - opens or closes the window.
- `/rt on` - enables all modules.
- `/rt off` - disables all modules.
- `/rt on tod` - enables only Truth or Dare.
- `/rt off tod` - disables only Truth or Dare.
- `/rt on todrules` - enables the Truth or Dare special result text.
- `/rt off todrules` - disables the Truth or Dare special result text.
- `/rt on todsecond` - enables the second-highest to second-lowest Truth or Dare result pair.
- `/rt off todsecond` - disables the second-highest to second-lowest Truth or Dare result pair.
- `/rt on wifi` - enables only `!wifi`.
- `/rt off wifi` - disables only `!wifi`.
- `/rt status` - prints whether Truth or Dare and `!wifi` are on or off.
- `/rt reset` - clears the current roll list.
- `/rt end` - sends the current highest/lowest result to yell chat, then clears the list.
- `/rt add <number>` - adds a manual test roll to the running Truth or Dare round.
- `/rt test` - adds four fake rolls so you can check the UI.
- `/rolltracker` - supports the same commands as `/rt`.

These commands are also listed in the Dalamud plugin installer description and in Dalamud command help.

When Truth or Dare is enabled, chat triggers `!truth` and `!dare` are also active. They ignore casing and send one random entry from the configured Truth or Dare lists to the selected chat channel.

The window also has `Auto off outside house`. When enabled, RollTracker turns itself off automatically when you leave a housing interior.

The result command can be edited in the window. Supported placeholders are `{highest}`, `{lowest}`, `{highestRoll}`, and `{lowestRoll}`. ToD special rules are enabled by default and can be disabled in `Settings`; when enabled, rolls of 0 or 1 add `<name> gets asked Truth and Dare.`, while a highest roll of 999 adds `<name> can ask both Truth and Dare.`. If both happen in the same round, only the 0/1 message is added. The second pair option is disabled by default; when enabled and at least four people rolled, the result also adds the second-highest to second-lowest pair.

`!wifi` is a separate trigger with its own tab, enabled switch, macro text, and chat target. It can send to Yell, Say, or Party and is also turned off automatically when you leave a housing interior.

The `Truth / Dare` tab lets you choose the chat channel and add, edit, and delete the prompt lists used by `!truth` and `!dare`. Truth and Dare prompts include default starter lists.

The `Settings` tab contains `Enable ToD`, `Enable ToD special rules`, `Enable ToD second pair`, `Enable Wifi`, `Auto off outside house`, and quick buttons to enable or disable all modules.

## Notes

Only the first normal `/random` roll per player counts during a round. Later rolls by the same player are ignored. Limited rolls such as `/random 10` are ignored when the game exposes the roll range to Dalamud. For testing, `/rt add <number>` can add manual rolls such as `/rt add 0`, `/rt add 1`, or `/rt add 999` while a Truth or Dare round is running.

Cross-world player names are normalized before being stored, so `Name@World`, `Name World`, and `NameWorld` are treated as the same player name where the world name can be recognized.

When `!tod` starts a round, the current roll list and any previous round state are cleared before the macro starts.

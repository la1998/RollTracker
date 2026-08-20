# RollTracker

It reads chat messages through Dalamud chat events, detects normal `/random` rolls, stores the first roll per player for the current round, and shows the round in a small ImGui window. When `!tod` appears in chat while the plugin is enabled, it starts a configured plugin macro and a timed round. When `!tod2` appears in chat while second pair rounds are enabled, it starts its own configured macro and timed round. When the configured duration ends, it sends the result to chat and clears the list.

This plugin does not use networking or external APIs. It does send configured slash commands and a final `/y` message when enabled, so keep it private and use it only where everyone involved expects it.

## Custom Repository

Add this URL in Dalamud under `/xlsettings` > `Experimental` > `Custom Plugin Repositories`:

```text
https://raw.githubusercontent.com/la1998/RollTracker/main/repo.json
```

## Testing Builds

RollTracker publishes stable builds through `dist/RollTracker/latest.zip` and testing builds through `dist/RollTracker/testing.zip`.

To test unreleased updates in Dalamud, enable plugin testing builds in Dalamud's Experimental settings. Testing builds are offered only when `TestingAssemblyVersion` in `repo.json` is higher than the stable `AssemblyVersion`.

Current stable: `0.1.0.26`
Current testing: `0.1.0.32`

## Commands

- `/rt` - opens or closes the window.
- `/rt on` - enables all modules.
- `/rt off` - disables all modules.
- `/rt on tod` - enables Truth or Dare, `!truth`, and `!dare`.
- `/rt off tod` - disables only Truth or Dare.
- `/rt on todrules` - enables the Truth or Dare special result text.
- `/rt off todrules` - disables the Truth or Dare special result text.
- `/rt on todsecond` - enables `!tod2` second pair rounds.
- `/rt off todsecond` - disables `!tod2` second pair rounds.
- `/rt on truth` - enables `!truth`.
- `/rt off truth` - disables `!truth`.
- `/rt on dare` - enables `!dare`.
- `/rt off dare` - disables `!dare`.
- `/rt on help` - enables `!help`.
- `/rt off help` - disables `!help`.
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

## Command Relationships

- `/rt on` enables ToD, `!truth`, `!dare`, `!help`, ToD special rules, `!tod2`, and `!wifi`.
- `/rt off` disables ToD, `!truth`, `!dare`, `!help`, ToD special rules, `!tod2`, and `!wifi`.
- `/rt on tod` enables ToD and turns `!truth` and `!dare` on.
- `/rt off tod` only disables ToD itself. `!truth` and `!dare` keep their own enabled or disabled state.
- `!tod` and `!tod2` use separate macro text, duration, and result command settings.
- `!tod2` is triggered separately and does not add second-pair output to normal `!tod` rounds.
- `/rt on truth` and `/rt off truth` only change `!truth`. ToD still has to be enabled for `!truth` to respond in chat.
- `/rt on dare` and `/rt off dare` only change `!dare`. ToD still has to be enabled for `!dare` to respond in chat.
- `/rt on help` and `/rt off help` only change `!help`. `!help` lists only the `!` commands that are currently active.
- `/rt on todrules` and `/rt off todrules` only change whether the configured Special Rules are applied.
- `/rt on todsecond` and `/rt off todsecond` only change whether `!tod2` can start second-highest to second-lowest rounds.
- `/rt on wifi` and `/rt off wifi` only change `!wifi`.
- `Auto off outside house` disables ToD, `!truth`, `!dare`, `!help`, `!tod2`, and `!wifi` when you leave a housing interior.

The window also has `Auto off outside house`. When enabled, RollTracker turns itself off automatically when you leave a housing interior.

The normal result command can be edited in the window. Supported placeholders are `{highest}`, `{lowest}`, `{highestRoll}`, and `{lowestRoll}`. The `!tod2` result command also supports `{secondHighest}`, `{secondLowest}`, `{secondHighestRoll}`, and `{secondLowestRoll}`. Each non-empty `!tod2` result command line is sent as its own chat message, so the default result sends the highest/lowest pair and the second pair separately.

The `Special Rules` tab lets you enable or disable special rules, edit the default rules for rolls 0, 1, and 999, and add or delete custom roll-number rules. Special rule text supports `{player}`, `{roll}`, and `{role}`. Matching rule texts are sent as separate chat messages after the normal result line instead of being appended to it. `Do not trigger with` accepts comma- or space-separated roll numbers that suppress that rule when those numbers are also in the pair.

`!wifi` is a separate trigger with its own tab, enabled switch, macro text, and chat target. It can send to Yell, Say, or Party and is also turned off automatically when you leave a housing interior.

The `Truth / Dare` tab lets you choose the chat channel and add, edit, and delete the prompt lists used by `!truth` and `!dare`. Truth and Dare prompts include default starter lists.

The `Settings` tab contains `Enable ToD`, `Enable ToD second pair`, `Enable !truth`, `Enable !dare`, `Enable !help`, `Enable Wifi`, `Auto off outside house`, and quick buttons to enable or disable all modules.

After an update, RollTracker can show a small dismissible changelog window. It remembers the last version you acknowledged and only opens again when the installed plugin version changes.

## Notes

Only the first normal `/random` roll per player counts during a round. Later rolls by the same player are ignored. Limited rolls such as `/random 10` are ignored when the game exposes the roll range to Dalamud. For testing, `/rt add <number>` can add manual rolls such as `/rt add 0`, `/rt add 1`, or `/rt add 999` while a Truth or Dare round is running.

Cross-world player names are normalized before being stored, so `Name@World`, `Name World`, and `NameWorld` are treated as the same player name where the world name can be recognized.

When `!tod` starts a round, the current roll list and any previous round state are cleared before the macro starts.

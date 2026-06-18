# RollTracker

It reads chat messages through Dalamud chat events, detects normal `/random` rolls, stores the first roll per player for the current round, and shows the round in a small ImGui window. When `!tod` appears in chat while the plugin is enabled, it starts a configured plugin macro and a timed round. When the configured duration ends, it sends the result to yell chat as `"HighestPlayer">"LowestPlayer"` and clears the list.

This plugin does not use networking or external APIs. It does send configured slash commands and a final `/y` message when enabled, so keep it private and use it only where everyone involved expects it.

## Custom Repository

Add this URL in Dalamud under `/xlsettings` > `Experimental` > `Custom Plugin Repositories`:

```text
https://raw.githubusercontent.com/la1998/RollTracker/main/repo.json
```

## Commands

- `/rolltracker` opens or closes the window.
- `/rt` opens or closes the window.
- `/rt on` or `/rolltracker on` enables chat monitoring and `!tod` reaction.
- `/rt off` or `/rolltracker off` disables chat monitoring and `!tod` reaction.
- `/rt status` prints whether the plugin is on or off.
- `/rolltracker reset` or `/rolltracker clear` clears the current roll list.
- `/rolltracker end` sends the current highest/lowest result to yell chat, then clears the list.
- `/rolltracker test` adds three fake rolls so you can check the UI.

The window also has `Auto off outside house`. When enabled, RollTracker turns itself off automatically when you leave a housing interior.

The result command can be edited in the window. Supported placeholders are `{highest}`, `{lowest}`, `{highestRoll}`, and `{lowestRoll}`.

## Notes

Only the first normal `/random` roll per player counts during a round. Later rolls by the same player are ignored. Limited rolls such as `/random 10` are ignored when the game exposes the roll range to Dalamud.

Cross-world player names are normalized before being stored, so `Name@World`, `Name World`, and `NameWorld` are treated as the same player name where the world name can be recognized.

When `!tod` starts a round, the current roll list and any previous round state are cleared before the macro starts.

# RollTracker

RollTracker is a private Dalamud plugin for running Truth or Dare style `/random` rounds in Final Fantasy XIV.

It watches chat for your configured commands, tracks each player's first valid roll, shows the current round in an ImGui window, and can send the final result back to chat when the round ends.

## Install

Add this custom repository in Dalamud:

```text
https://raw.githubusercontent.com/la1998/RollTracker/main/repo.json
```

You can add it under `/xlsettings` > `Experimental` > `Custom Plugin Repositories`.

## What It Does

- Starts Truth or Dare roll rounds with `!tod`.
- Supports a second-pair round mode with `!tod2`.
- Sends random Truth and Dare prompts with `!truth` and `!dare`.
- Provides editable command help with `!help`.
- Provides a configurable Shells/Discord info command with `!wifi`.
- Supports special roll rules for rolls like `0`, `1`, `999`, or custom values.
- Can automatically turn modules off when leaving housing.
- Can automatically turn modules on again when entering saved housing interiors.
- Can apply Moodles, Honorific titles, or custom macros when selected RollTracker modules are active.

## Main Commands

```text
/rt
/rt help
/rt on
/rt off
/rt status
/rt reset
/rt end
```

Module commands use this format:

```text
/rt <module> on
/rt <module> off
/rt <module> toggle
```

Available modules:

```text
tod
tod2
todrules
truth
dare
help
alias
wifi
```

Examples:

```text
/rt tod on
/rt tod2 toggle
/rt wifi off
```

`/rolltracker` works as an alternative to `/rt`.

## Chat Triggers

These triggers can be enabled or disabled in the plugin settings:

```text
!tod
!tod2
!truth
!dare
!help
!wifi
```

The command text, chat channel, prompt lists, macros, timers, and result messages are configurable in the plugin window.

## Status Effects

RollTracker can optionally run commands for other plugins when selected RollTracker modules turn on or off.

Supported helpers:

- Moodles
- Honorific
- Advanced custom macros

RollTracker does not create Moodles for you. Create them in their own plugins first, then enter the matching name in RollTracker.

## Safety Note

RollTracker does not use networking or external APIs.

It can send configured chat commands and plugin commands while enabled, so use it only in spaces where everyone involved expects it.

# RollTracker

RollTracker is a private Dalamud plugin based on the official `goatcorp/SamplePlugin` structure.

It reads chat messages through Dalamud chat events, detects normal `/random` rolls, stores the first roll per player for the current round, and shows the round in a small ImGui window. When `!tod` appears in chat while the plugin is enabled, it starts a configured plugin macro and a timed round. When the configured duration ends, it sends the result to yell chat as `"HighestPlayer">"LowestPlayer"` and clears the list.

This plugin does not use networking or external APIs. It does send configured slash commands and a final `/y` message when enabled, so keep it private and use it only where everyone involved expects it.

## Custom Repository

Add this URL in Dalamud under `/xlsettings` > `Experimental` > `Custom Plugin Repositories`:

```text
https://raw.githubusercontent.com/la1998/RollTracker/main/repo.json
```

## Project Structure

```text
RollTracker.sln
RollTracker/
  RollTracker.csproj
  RollTracker.json
  Configuration.cs
  Plugin.cs
  Services/
    RollEntry.cs
    RollTrackerService.cs
    TextCommandService.cs
  Windows/
    MainWindow.cs
dist/
  RollTracker/
    latest.zip
repo.json
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

## Build

Prerequisites:

- XIVLauncher and Dalamud installed, with the game launched at least once.
- .NET 10 SDK, because Dalamud API 15 targets .NET 10.

Build from a terminal:

```powershell
dotnet build .\RollTracker.sln -c Release -p:Platform=x64
```

The release DLL will be here:

```text
RollTracker\bin\x64\Release\RollTracker.dll
```

The Dalamud repository ZIP will be here:

```text
RollTracker\bin\x64\Release\RollTracker\latest.zip
```

Copy it into the repository distribution folder before pushing updates:

```powershell
Copy-Item .\RollTracker\bin\x64\Release\RollTracker\latest.zip .\dist\RollTracker\latest.zip -Force
```

## Local Dalamud Test

1. Start Final Fantasy XIV through XIVLauncher.
2. Open Dalamud settings with `/xlsettings`.
3. Go to `Experimental`.
4. Add the full path to `RollTracker.dll` under `Dev Plugin Locations`.
5. Open `/xlplugins`.
6. Go to `Dev Tools` > `Installed Dev Plugins`.
7. Enable `RollTracker`.
8. Use `/rolltracker` or `/rt`.
9. Open `/rt`, configure the macro text and duration. Example macro:

```text
/y ♪ Type /random in chat!  Highest number asks the lowest number, "Truth or Dare?" 60 seconds... Begin!
/wait 55
/y 5 seconds remain...
/wait 5
/y End
```

10. Set the duration to `60`.
11. Use `/rt on`.
12. Let a player type `!tod` in chat to start the macro and timed round.
13. Use `/random` during the round.
14. When the timer ends, RollTracker sends `/y "HighestPlayer">"LowestPlayer"` and resets.

## Notes

Only the first normal `/random` roll per player counts during a round. Later rolls by the same player are ignored. Limited rolls such as `/random 10` are ignored when the game exposes the roll range to Dalamud.

Cross-world player names are normalized before being stored, so `Name@World`, `Name World`, and `NameWorld` are treated as the same player name where the world name can be recognized.

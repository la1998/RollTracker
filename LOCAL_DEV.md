# RollTracker lokal als Dalamud Dev Plugin testen

Dieser Workflow ist fuer Variante 1 gedacht: Die GitHub-/Repo-Version von RollTracker bleibt dieselbe Plugin-Identitaet, wird in Dalamud aber deaktiviert, solange du lokal testest.

## Grundidee

- Repo-Version in Dalamud deaktivieren.
- Lokalen Debug-Build als Dev Plugin laden.
- Aenderungen lokal bauen und direkt in Dalamud testen.
- Wenn alles passt, normal Version bumpen, Testing-ZIP bauen, committen und nach GitHub pushen.
- Danach lokale Dev-Version deaktivieren und die Repo-/Testing-Version in Dalamud wieder aktivieren.

Weil beide Builds denselben Plugin-Namen und dieselbe Plugin-Identitaet verwenden, sollten sie nicht gleichzeitig aktiv sein.

## Einmalig in Dalamud einrichten

1. Ingame `/xlsettings` oeffnen.
2. `Experimental` oeffnen.
3. `Enable Dev Plugins` aktivieren.
4. Bei den Dev-Plugin-Locations die lokal gebaute DLL hinzufuegen:

```text
<repo path>\RollTracker\bin\x64\Debug\RollTracker.dll
```

5. Repo-Version von RollTracker in `/xlplugins` deaktivieren.
6. Dalamud Plugins neu laden oder das Spiel neu starten, falls RollTracker nicht sofort als Dev Plugin erscheint.

## Lokalen Dev-Build bauen

Im Projektordner:

```powershell
.\scripts\build-local-dev.ps1
```

Das Script baut die Dev-DLL hier:

```text
RollTracker\bin\x64\Debug\RollTracker.dll
```

Nach einem Build in Dalamud RollTracker neu laden. Falls Dalamud die alte DLL noch haelt, einmal Plugin deaktivieren/aktivieren oder Dalamud neu laden.

## Danach auf GitHub Testing pushen

Wenn der lokale Test passt:

1. Version in `RollTracker/RollTracker.csproj` erhoehen.
2. Testing-ZIP bauen/kopieren.
3. `repo.json` Testing-Version und Testing-Changelog anpassen.
4. Commit und Push nach GitHub.
5. In Dalamud Dev Plugin deaktivieren.
6. Repo-/Testing-Version aktivieren und nochmal ueber den echten Update-Weg testen.

## Wichtig

Dieser Workflow trennt nicht die Config. Die lokale Dev-Version und die Repo-Version verwenden dieselbe RollTracker-Plugin-Identitaet und damit dieselbe aktive Config. Das ist Absicht bei Variante 1, damit du wirklich genau die spaetere Repo-Version testest.

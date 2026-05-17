# RollTracker auf GitHub veroeffentlichen

## 1. Repository erstellen

Erstelle auf GitHub ein neues Repository, zum Beispiel:

```text
RollTracker
```

Wenn du das Plugin ueber Dalamud als Drittanbieter-Quelle installieren willst, muss mindestens `repo.json` und `dist/RollTracker/latest.zip` oeffentlich erreichbar sein. Ein privates GitHub-Repository funktioniert fuer Dalamud normalerweise nicht, weil Dalamud keinen GitHub-Login fuer Raw-Dateien nutzt.

## 2. Platzhalter ersetzen

Die `repo.json` ist bereits auf dieses Repository eingestellt:

```text
https://github.com/la1998/RollTracker
```

Der Dalamud-Repo-Link ist:

```text
https://raw.githubusercontent.com/la1998/RollTracker/main/repo.json
```

## 3. Dateien hochladen

Lade diese Dateien und Ordner in dein GitHub-Repository hoch:

```text
.gitignore
GITHUB_PUBLISHING.md
README.md
repo.json
RollTracker.sln
RollTracker/
dist/RollTracker/latest.zip
```

Nicht hochladen musst du:

```text
RollTracker/bin/
RollTracker/obj/
```

Die fertige Plugin-ZIP fuer Dalamud liegt hier:

```text
dist/RollTracker/latest.zip
```

## 4. Repo-Link fuer Dalamud

Nach dem Upload ist dein Dalamud-Repo-Link:

```text
https://raw.githubusercontent.com/la1998/RollTracker/main/repo.json
```

## 5. In Dalamud hinzufuegen

1. Ingame `/xlsettings` oeffnen.
2. Tab `Experimental` oeffnen.
3. Unter `Custom Plugin Repositories` den Raw-Link zu `repo.json` einfuegen.
4. Auf `+` klicken.
5. Speichern.
6. `/xlplugins` oeffnen.
7. Nach `RollTracker` suchen und installieren.

## 6. Neue Version veroeffentlichen

Wenn du spaeter Aenderungen machst:

1. Version in `RollTracker/RollTracker.csproj` erhoehen.
2. Release bauen:

```powershell
dotnet build .\RollTracker.sln -c Release -p:Platform=x64
```

3. Neue ZIP kopieren:

```powershell
Copy-Item .\RollTracker\bin\x64\Release\RollTracker\latest.zip .\dist\RollTracker\latest.zip -Force
```

4. In `repo.json` `AssemblyVersion` auf die neue Version setzen.
5. Alles committen und nach GitHub pushen.

Dalamud erkennt Updates anhand der hoeheren `AssemblyVersion`.

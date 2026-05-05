# Maze Project — Menu, Saves, Monster und Fallen — Implementierungsplan

> Dieser Plan baut auf der aktuellen Projektstruktur mit `Main`, `Hud`, `MazeView3D`, `PlayerCharacter3D` und den vorhandenen Generatoren auf. Ziel ist kein generischer UI-Entwurf, sondern eine konkrete Roadmap fuer dieses Repository.

**Goal:** Ein zweistufiges Menuesystem fuer das Maze-Spiel einfuehren. Im **Startmenue** soll der Spieler Labyrinthe erstellen, gespeicherte Labyrinthe aufrufen und Labyrinthe loeschen koennen. Beim Erstellen eines neuen Labyrinths sollen folgende Optionen verfuegbar sein: leuchtender Weg, Groesse, Dunkelmodus, Fallen generieren, Monster koennen gestunnt werden, Monster generieren, Tag-Nacht-Zyklus, naechte Sichtbegrenzung und Algorithmus zur Maze-Erstellung. Im **Ingame-Menue** sollen dagegen nur die Punkte **Visuelles**, **Ton** und **Zurueck zum Startmenue** erscheinen. Unter **Visuelles** werden **Helligkeit**, **FOV** und **Effects** angeboten. Unter **Ton** werden **Monster**, **Laufgeraeusche**, **Ziel gefunden** und **Gesamtlautstaerke** angeboten. Monster duerfen nur nachts erscheinen.

**Architektur-Entscheidung:** Das bestehende `Hud` bleibt nicht das finale Hauptmenue. Stattdessen wird die UI in drei Bereiche getrennt:

- `MainMenu` fuer Erstellen, Laden und Loeschen.
- `PauseMenu` fuer Ingame-Einstellungen.
- `Hud` nur noch fuer Laufzeitinfos oder spaetere Debug-/Schueler-Steuerungen.

Dadurch bleibt `scripts/Main.cs` der zentrale Orchestrator, aber die Menues werden nicht in einem einzigen CanvasLayer vermischt.

## Phase 1 — Spielzustand und Konfigurationsdaten einfuehren

Ziel: Vor der UI zuerst die Datenstruktur schaffen, damit Startmenue, Save-System und Laufzeit dieselben Daten benutzen.

**Neue Dateien:**

- `scripts/Game/MazeGameConfig.cs`
- `scripts/Game/MazeSaveData.cs`
- `scripts/Game/GameSessionState.cs`
- `scripts/Game/Settings/VisualSettings.cs`
- `scripts/Game/Settings/AudioSettings.cs`

**MazeGameConfig** sollte mindestens diese Felder enthalten:

- `int Width`
- `int Height`
- `string GeneratorId`
- `bool PathGlowEnabled`
- `bool DarkModeEnabled`
- `bool TrapGenerationEnabled`
- `bool MonsterCanBeStunned`
- `bool MonsterGenerationEnabled`
- `bool DayNightCycleEnabled`
- `bool MonstersOnlyAtNight`
- `float NightViewDistance`
- `int Seed`

**Wichtige Regel:** `MonstersOnlyAtNight` sollte von Beginn an standardmaessig `true` sein und spaeter nicht durch ein anderes System umgangen werden. Falls `MonsterGenerationEnabled == false`, duerfen keine Monster erzeugt werden. Falls `MonsterGenerationEnabled == true`, duerfen sie trotzdem nur aktiv sein, wenn Nacht ist.

**GameSessionState** sollte Laufzeitdaten enthalten, die nicht ins Basiskonfigurationsobjekt gehoeren:

- aktuelles Maze
- aktuelle Start-/Zielzellen
- aktive Monster
- aktive Fallen
- aktueller Tageszeitstand
- Spiel laeuft oder Pause
- Spieler lebt, Ziel erreicht, Manual-Mode aktiv

**Implementierung in `Main`:**

- `OnGenerateRequested(int width, int height, string generatorId)` spaeter durch `StartNewGame(MazeGameConfig config)` ersetzen.
- Das bisherige direkte UI-zu-`Main`-Binding wird schrittweise auf ein Konfigurationsobjekt umgestellt.

## Phase 2 — Startmenue als eigene Szene einfuehren

Ziel: Eine echte Einstiegsoberflaeche vor dem Spiel statt direkter HUD-Steuerung.

**Neue Dateien:**

- `scenes/MainMenu.tscn`
- `scripts/UI/MainMenu.cs`
- `scenes/NewMazePanel.tscn`
- `scripts/UI/NewMazePanel.cs`
- `scenes/SaveListPanel.tscn`
- `scripts/UI/SaveListPanel.cs`

**Startmenue-Struktur:**

- `Neues Labyrinth`
- `Gespeicherte Labyrinthe`
- `Loeschen`
- `Spiel starten`

**Unterbereich `Neues Labyrinth`:**

- Eingabe fuer Name oder Save-Slot
- Auswahl `Leuchtender Weg`
- Auswahl `Groesse` mit Width/Height
- Auswahl `Dunkelmodus`
- Auswahl `Fallen generieren`
- Auswahl `Monster koennen gestunnt werden`
- Auswahl `Monster generieren`
- Auswahl `Tag-Nacht-Zyklus`
- Auswahl `Algorithmus`

**Konkrete Mapping-Regel auf die aktuelle Codebasis:**

- Die Algorithmusliste kann aus den vorhandenen Generator-IDs in `scripts/Main.cs` befuellt werden.
- Die bestehende Groessensteuerung aus `Hud` kann konzeptionell uebernommen werden, aber nicht direkt wiederverwendet werden, weil sie aktuell an den Debug-HUD gebunden ist.

**Signals fuer `MainMenu`:**

- `StartNewMazeRequested(MazeGameConfig config)`
- `LoadMazeRequested(string saveId)`
- `DeleteMazeRequested(string saveId)`

**Implementierung in `Main`:**

- Beim Start nur `MainMenu` sichtbar.
- `MazeView2D`, `MazeView3D` und Spiel-HUD zunaechst ausblenden.
- Nach `StartNewMazeRequested` Maze erzeugen und Spielansicht aktivieren.
- Nach `LoadMazeRequested` gespeichertes Maze laden und Spielansicht aktivieren.

## Phase 3 — Save-, Load- und Delete-System bauen

Ziel: Labyrinthe nicht nur konfigurieren, sondern reproduzierbar speichern, wieder aufrufen und loeschen.

**Neue Dateien:**

- `scripts/Save/SaveGameService.cs`
- `scripts/Save/MazeSerializer.cs`
- `scripts/Save/SaveSlotSummary.cs`

**Speicherinhalt pro Save:**

- Anzeigename oder Save-ID
- Erstellungsdatum
- komplette `MazeGameConfig`
- serialisierte Zellstruktur mit Waenden
- Start- und Zielposition
- Fallenpositionen und Fallentypen
- Monster-Spawnpunkte
- optional Seed

**Wichtige Implementierungsentscheidung:** Nicht nur `Seed + Algorithmus` speichern. Stattdessen die erzeugte Maze-Struktur direkt speichern. Sonst koennen spaetere Generator-Aenderungen alte Saves ungewollt veraendern.

**Dateipfad-Vorschlag:**

- Benutzerordner unter `user://saves/`
- pro Save eine JSON-Datei

**Noetige Funktionen:**

- `SaveMaze(MazeSaveData saveData)`
- `LoadMaze(string saveId)`
- `DeleteMaze(string saveId)`
- `ListSaves()`

**Einbindung in `Main`:**

- Nach erfolgreicher Generierung kann das Spiel direkt in einen Save-Slot geschrieben werden.
- `LoadMaze` muss `MazeView2D`, `MazeView3D`, Start/Ziel, Fallen und Monster-Manager neu initialisieren.

## Phase 4 — `Main` in einen sauberen Spielzustands-Controller umbauen

Ziel: Die bestehende `Main`-Klasse von reiner HUD-Steuerung zu einem klaren State-Controller entwickeln.

**Neue Enum:**

- `Boot`
- `MainMenu`
- `Playing`
- `Paused`
- `Loading`

**Neue private Felder in `Main`:**

- Referenz auf `MainMenu`
- Referenz auf `PauseMenu`
- Referenz auf `SaveGameService`
- Referenz auf `DayNightController`
- Referenz auf `MonsterManager`
- Referenz auf `TrapManager`
- aktuelle `MazeGameConfig`
- aktueller `GameSessionState`

**Aufgaben von `Main`:**

- Sichtbarkeit von `MainMenu`, `PauseMenu`, `Hud`, `MazeView2D`, `MazeView3D` steuern
- Spielstart, Pause, Rueckkehr zum Hauptmenue koordinieren
- beim Szenenwechsel Input sauber freigeben oder sperren
- Monster und Fallen nur nach erfolgreichem Maze-Aufbau initialisieren

**Wichtig fuer die aktuelle Architektur:**

- Der bestehende Manual-Mode in `scripts/Main.cs` bleibt erhalten.
- Das Ingame-Menue muss bei geoeffneter Pause die Eingaben des Players und der Kamera blockieren.

## Phase 5 — Ingame-Menue als Pause-Menue einfuehren

Ziel: Im Labyrinth per Escape ein separates Menue oeffnen, das bewusst weniger Optionen hat als das Startmenue.

**Neue Dateien:**

- `scenes/PauseMenu.tscn`
- `scripts/UI/PauseMenu.cs`
- `scenes/VisualSettingsPanel.tscn`
- `scripts/UI/VisualSettingsPanel.cs`
- `scenes/AudioSettingsPanel.tscn`
- `scripts/UI/AudioSettingsPanel.cs`

**Menuestruktur im Labyrinth:**

- `Visuelles`
- `Ton`
- `Zurueck zum Startmenue`

**Explizit nicht enthalten:**

- kein Groessenwechsel
- keine Algorithmuswahl
- kein Fallen/Monster-Generierungs-Toggle
- kein Neuerstellen des Labyrinths aus dem Pause-Menue

**Input-Verhalten:**

- `Escape` oeffnet oder schliesst das Pause-Menue
- bei offenem Menue: Spielerbewegung aus, Kameraeingabe aus, Maus sichtbar
- bei geschlossenem Menue: Gameplay-Input wieder aktiv

**Signals fuer `PauseMenu`:**

- `VisualSettingsChanged(VisualSettings settings)`
- `AudioSettingsChanged(AudioSettings settings)`
- `ReturnToMainMenuRequested()`

## Phase 6 — Visuelle Einstellungen implementieren

Ziel: Die Punkte `Helligkeit`, `FOV` und `Effects` technisch an die vorhandene 3D-Struktur anbinden.

### 6.1 Helligkeit

**UI-Element:** Slider von dunkel bis hell.

**Bestehende Anker im Projekt:**

- `DirectionalLight3D Sun` in `MazeView3D`
- `WorldEnvironment`
- `OmniLight3D PlayerLight`

**Implementierung:**

- In `scripts/Views/MazeView3D.cs` eine Methode `ApplyBrightness(float brightness)` einfuehren.
- Diese Methode skaliert:
  - `Sun.LightEnergy`
  - `Environment.AmbientLightEnergy`
  - nachts optional `PlayerLight.LightEnergy`
- Der Regler wirkt als Multiplikator, nicht als harter Preset-Switch.

**Wichtig:** Bei aktiviertem Tag-Nacht-Zyklus darf der Helligkeitsregler nicht die Nachtlogik aushebeln, sondern nur innerhalb eines sinnvollen Bereichs modulieren.

### 6.2 FOV

**UI-Element:** Slider fuer Sichtfeld.

**Bestehender Anker im Projekt:**

- `Camera3D` mit `CameraController3D`

**Implementierung:**

- In `scripts/Views/CameraController3D.cs` eine Methode `SetFieldOfView(float fov)` ergaenzen.
- Der Wert wird direkt an `Fov` gesetzt.
- Typischer Bereich: `55` bis `100`.

### 6.3 Effects

**Bedeutung in diesem Plan:** Effekte, die auftreten, wenn sich der Spieler einem Monster naehrt.

**Sinnvolle technische Aufteilung:**

- Bildschirm-Overlay fuer Vignette oder Farbverfaerbung
- leichtes Kamera-Shake
- optional Atem- oder Herzschlag-Postprocessing via Audio/Overlay-Kombination

**Neue Dateien:**

- `scripts/Effects/ProximityEffectController.cs`
- `scenes/MonsterProximityOverlay.tscn`

**Implementierung:**

- `MonsterManager` liefert pro Frame die Distanz zum naechsten aktiven Monster.
- `ProximityEffectController` wandelt Distanz in eine `Intensity` um.
- Mit steigender Intensitaet werden aktiviert:
  - staerkere Randabdunkelung
  - leichtes Kamera-Zittern
  - Farbverschiebung oder Emission
- Wenn `Effects` im Menue deaktiviert oder klein gestellt werden, werden Intensitaet oder Teilkomponenten reduziert.

## Phase 7 — Audio-Menue implementieren

Ziel: Die vier angeforderten Tonpunkte als getrennte, spaeter speicherbare Einstellungen abbilden.

**Ton-Menuepunkte:**

- `Monster`
- `Laufgeraeusche`
- `Ziel gefunden`
- `Gesamtlautstaerke`

**Neue Dateien:**

- `scripts/Audio/AudioBusController.cs`
- `scripts/Audio/PlayerFootstepController.cs`
- `scripts/Audio/GoalAudioController.cs`

**Empfohlene Audio-Busse:**

- `Master`
- `Monster`
- `Footsteps`
- `Goal`

**Implementierung:**

- `Gesamtlautstaerke` steuert den `Master`-Bus.
- `Monster` steuert alle Monster-Sounds.
- `Laufgeraeusche` steuert Schrittgeraeusche des Spielers.
- `Ziel gefunden` steuert den Sieg- oder Zielsound.

**Anbindung an bestehende Systeme:**

- Laufgeraeusche koennen in `scripts/Views/PlayerCharacter3D.cs` beim Zellenwechsel getriggert werden.
- Das Erreichen des Ziels kann wie bisher ueber `GoalReached` in `Main` verarbeitet werden, dort wird zusaetzlich der Zielsound abgespielt.
- Monster erhalten eigene `AudioStreamPlayer3D`-Nodes.

## Phase 8 — Tag-Nacht-Zyklus einfuehren

Ziel: Ein zentrales System, das Licht, Sichtweite und Monster-Aktivierung koppelt.

**Neue Dateien:**

- `scripts/World/DayNightController.cs`

**Verantwortung des Controllers:**

- Tageszeit von `0.0` bis `1.0`
- `bool IsNight`
- Zyklusdauer in Sekunden
- Signale fuer `DayStarted` und `NightStarted`

**Implementierung:**

- `DayNightController` laeuft zentral in der Hauptszene.
- Pro Frame wird Tageszeit fortgeschrieben, falls `DayNightCycleEnabled` aktiv ist.
- `MazeView3D` reagiert darauf mit Licht-, Fog- und Sichtweitenanpassung.

**Nacht-Sichtbegrenzung:**

- nachts groessere Fog-Density
- geringere Ambient-/Sun-Energy
- hoeheres Gewicht des `PlayerLight`
- optional geringerer Kamera-Far-Clip oder ein shaderbasierter Sichtkreis

**Regel fuer Dunkelmodus:**

- `DarkModeEnabled` kann als statischer dunkler Look genutzt werden, falls kein echter Zyklus aktiv ist.
- Wenn `DayNightCycleEnabled` aktiv ist, wird `DarkModeEnabled` nicht als zweites konkurrierendes System implementiert, sondern als Startstil oder Verstarker fuer die Nachtwerte.

## Phase 9 — Monster-System einfuehren

Ziel: Monster als Nacht-Gameplay mit optionalem Stun.

**Neue Dateien:**

- `scripts/Gameplay/Monster/MonsterController.cs`
- `scripts/Gameplay/Monster/MonsterManager.cs`
- `scenes/Monster.tscn`

**Monster-Regeln:**

- Monster werden nur erzeugt, wenn `MonsterGenerationEnabled == true`.
- Monster werden nur sichtbar oder aktiv, wenn Nacht ist.
- Monster duerfen tagsueber nicht spawnen oder muessen deaktiviert werden.
- Monster koennen nur dann gestunnt werden, wenn `MonsterCanBeStunned == true`.

**Kernregel dieses Plans:** Monster nur nachts erscheinen.

Das bedeutet fuer die Implementierung konkret:

- `MonsterManager` subscribt auf `DayNightController`.
- Bei `NightStarted`: Monster aktivieren oder spawnen.
- Bei `DayStarted`: Monster deaktivieren, verstecken oder despawnen.
- Proximity-Effekte werden nur gegen aktuell aktive Nacht-Monster berechnet.

**Spawn-Strategien fuer die erste Version:**

- Monster in weit entfernten Zellen vom Start
- bevorzugt in Sackgassen oder Randzonen
- nie auf Start- oder Zielzelle
- Spawnpunkte direkt nach der Maze-Generierung berechnen und speichern

**Stun-Implementierung:**

- einfacher Zustandsautomat `Idle`, `Patrol`, `Chase`, `Stunned`
- bei Stun: Bewegung gestoppt, Material oder Lichtsignal geaendert, Timer laeuft
- nach Ablauf Rueckkehr in aktiven Zustand, aber nur falls noch Nacht ist

## Phase 10 — Fallen-System einfuehren

Ziel: Fallen als zweite Gameplay-Ebene neben Monstern.

**Neue Dateien:**

- `scripts/Gameplay/Traps/TrapController.cs`
- `scripts/Gameplay/Traps/TrapManager.cs`
- `scenes/Trap.tscn`

**Fallen-Regeln:**

- Fallen nur, wenn `TrapGenerationEnabled == true`
- nie auf Start oder Ziel
- nicht direkt auf den ersten Schritten des Startpfades

**Sinnvolle Fallen fuer dieses Projekt:**

- `SlowTrap`: verringert Bewegungsgeschwindigkeit kurzzeitig
- `LightTrap`: dimmt kurzzeitig Licht oder Sichtweite
- `NoiseTrap`: lockt nachts Monster an und verstaerkt Proximity-Effekte indirekt

**Implementierung:**

- `TrapManager` erzeugt Fallen nach der Maze-Erstellung.
- Fallenpositionen werden zusammen mit dem Save gesichert.
- `PlayerCharacter3D` oder `Main` reagiert auf Trigger und aktiviert den Effekt.

## Phase 11 — Leuchtender Weg implementieren

Ziel: Die Option `dein Weg leuchtet` sichtbar und spielerisch nuetzlich machen.

**Bestehende Basis im Projekt:**

- `MazeView3D` hat bereits Trail-Unterstuetzung.
- `Main` markiert bereits besuchte Zellen ueber `OnPlayerCellVisited`.

**Implementierung:**

- Wenn `PathGlowEnabled == true`, erhalten besuchte Zellen ein deutlich emissiveres Trail-Material.
- Nachts kann das Trail-Leuchten etwas staerker sein als tagsueber.
- Optional fuer spaeter: nur selbst gegangener Weg leuchtet, nicht die echte Loesung.

## Phase 12 — Szenen- und Node-Struktur der Hauptszene erweitern

Ziel: Die neuen Systeme sauber in die vorhandene Hauptszene integrieren.

**Empfohlene neue Hauptstruktur unter `Main`:**

- `MainMenu`
- `PauseMenu`
- `Runner`
- `WorldControllers`
- `MazeView2D`
- `MazeView3D`
- `Hud`

**Unter `WorldControllers`:**

- `DayNightController`
- `MonsterManager`
- `TrapManager`
- `AudioBusController`
- `SaveGameService` als Node oder reiner Dienst via Klasse

**Warum diese Trennung zur aktuellen Codebasis passt:**

- `Main` bleibt steuernd, aber nicht alles landet direkt in einer grossen Klasse.
- `MazeView3D` bleibt View-Schicht fuer Licht, Sicht und 3D-Darstellung.
- Monster, Fallen und Tageszeit werden nicht in `PlayerCharacter3D` oder `Hud` vermischt.

## Empfohlene Implementierungsreihenfolge

1. `MazeGameConfig`, `MazeSaveData` und `SaveGameService` erstellen.
2. `MainMenu` mit `Neues Labyrinth`, `Gespeicherte Labyrinthe`, `Loeschen` bauen.
3. `Main` auf Spielzustands-Management umbauen.
4. `PauseMenu` mit `Visuelles`, `Ton`, `Zurueck zum Startmenue` einfuehren.
5. `Helligkeit` und `FOV` an `MazeView3D` und `CameraController3D` anbinden.
6. `Effects` ueber Monster-Naehe-Controller und Overlay bauen.
7. `DayNightController` einfuehren.
8. `MonsterManager` mit der Regel `Monster nur nachts` implementieren.
9. `TrapManager` und erste Fallentypen ergaenzen.
10. Audio-Regler und Ziel-/Monster-/Schritt-Sounds final anschliessen.

## Minimale erste Umsetzungsstufe

Falls die Umsetzung in kleine Schritte zerlegt werden soll, ist die beste erste lieferbare Ausbaustufe:

1. Startmenue mit Neuerstellen, Laden, Loeschen.
2. `MazeGameConfig` statt direkter Width/Height/Generator-Parameter.
3. Pause-Menue mit `Helligkeit`, `FOV` und `Gesamtlautstaerke`.
4. einfacher `DayNightController`.
5. Platzhalter-Monster, die nur nachts sichtbar werden.

Damit ist die Grundarchitektur stabil, ohne dass sofort alle Monster-, Fallen- und Effekt-Systeme fertig sein muessen.
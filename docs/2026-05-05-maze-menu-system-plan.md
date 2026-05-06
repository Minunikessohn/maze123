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

**Neue Dateien ueber die Teilphasen verteilt:**

- `scripts/Gameplay/Monster/MonsterController.cs`
- `scripts/Gameplay/Monster/MonsterManager.cs`
- `scenes/Monster.tscn`

**Monster-Regeln:**

- Monster werden nur erzeugt, wenn `MonsterGenerationEnabled == true`.
- Monster werden nur sichtbar oder aktiv, wenn Nacht ist.
- Monster duerfen tagsueber nicht spawnen oder muessen deaktiviert werden.
- Monster koennen nur dann gestunnt werden, wenn `MonsterCanBeStunned == true`.
- Monster sollen im Durchschnitt in `4%` der begehbaren Zellen spawnen.
- Monster duerfen niemals durch Waende oder diagonal durch gesperrte Zellkanten laufen.

**Kernregel dieses Plans:** Monster nur nachts erscheinen.

Damit die Umsetzung nicht zu gross wird, sollte Phase 9 in kleinere Zwischenschritte aufgeteilt werden.

### Phase 9.1 — Monster-Grundgeruest und Nacht-Aktivierung

Ziel: Ein minimales Monster-System schaffen, das sauber an den Tag-Nacht-Zyklus angebunden ist, aber noch keine echte KI braucht.

**Umfang:**

- `MonsterManager` als zentralen Einstiegspunkt anlegen.
- `Monster.tscn` als einfache Platzhalter-Szene anlegen.
- `MonsterController` mit Minimalzustand `Idle` vorbereiten.
- `MonsterManager` auf `DayNightController` subscriben.
- Bei `NightStarted`: Monster aktivieren oder erzeugen.
- Bei `DayStarted`: Monster deaktivieren oder despawnen.
- Proximity-Effekte nur gegen aktuell aktive Nacht-Monster berechnen.

**Ergebnis nach diesem Schritt:**

- Es gibt sichtbare oder logisch registrierte Monster nur waehrend der Nacht.
- Tagsueber existiert kein aktives Monster-Gameplay.

### Phase 9.2 — Spawnlogik und Spawnpunkte sauber einfuehren

Ziel: Monster in reproduzierbarer Anzahl und an gueltigen Positionen erzeugen.

**Umfang:**

- Spawnanzahl aus der Maze ableiten: `spawnCount = max(1, round(begehbareZellen * 0.04))`, sofern Monster aktiviert sind.
- Nur begehbare Zellen ohne Wandblockade zum Zentrum der Nachbarzellen als Spawnpunkte zulassen.
- Monster in etwas entfernten Zellen vom Start spawnen, damit der Spieler nicht direkt beim Nachtwechsel getroffen wird.
- Nie auf Start- oder Zielzelle spawnen.
- Spawnpunkte direkt nach der Maze-Generierung berechnen und speichern, damit Saves reproduzierbar bleiben.
- Optional fuer spaeter: Mindestabstand zwischen Monstern, damit nicht mehrere Gegner auf derselben Region clustern.

**Noetige Hilfsfunktion in diesem Schritt:**

- `ComputeMonsterSpawnCells(Maze maze, Vector2I startCell, Vector2I goalCell)`

**Ergebnis nach diesem Schritt:**

- Die Spawnrate ist an die Maze-Groesse gekoppelt.
- Spawnpunkte sind stabil, nachvollziehbar und Save-kompatibel.

### Phase 9.3 — Zellbasierte Bewegung ohne Wanddurchgang

Ziel: Erst die korrekte Basisbewegung bauen, bevor Verfolgungslogik hinzukommt.

**Umfang:**

- Bewegung strikt zellbasiert ueber die vorhandene Maze-Topologie umsetzen.
- Keine direkte freie Navigation im 3D-Raum fuer die erste Version.
- Stattdessen die existierende Maze-Topologie aus `Maze`, `Cell` und ihren offenen Richtungen als alleinige Bewegungsquelle nutzen.
- Jede Bewegungsentscheidung ueber `GetReachableNeighbors` oder eine aehnliche Hilfsmethode laufen lassen.
- Dadurch sicherstellen, dass Monster nicht durch Waende gehen koennen.
- Falls spaeter Kollisionen mit 3D-Waenden zusaetzlich sichtbar abgesichert werden sollen, ist das nur eine zweite Schutzschicht, nicht die primaere Bewegungslogik.

**Noetige Hilfsfunktion in diesem Schritt:**

- `GetReachableNeighbors(Vector2I cell)`

**Ergebnis nach diesem Schritt:**

- Monster koennen sich regelkonform durch das Maze bewegen.
- Wanddurchgaenge sind bereits auf Logik-Ebene ausgeschlossen.

### Phase 9.4 — Zufaelliges Wander-Verhalten einfuehren

Ziel: Monster sollen sich auch ohne Spielerkontakt glaubhaft bewegen.

**Umfang:**

- `Wander` oder `Patrol` als ersten echten Laufzustand einfuehren.
- Solange ein Monster niemanden sieht, laeuft es einfach zufaellig herum.
- Die einfachste robuste Variante ist ein zufaellig gewaehltener gueltiger Nachbar pro Bewegungsintervall.
- Alternativ kann ein kurzer Zufallspfad von `3` bis `6` Zellen erzeugt werden, damit die Bewegung weniger hektisch wirkt.
- Auch im Wander-Zustand duerfen nur Nachbarn benutzt werden, die ueber eine offene Maze-Kante erreichbar sind.
- Wenn ein Monster in einer Sackgasse steht, kehrt es ueber den einzigen offenen Rueckweg um, statt durch Waende zu clippen.

**Ergebnis nach diesem Schritt:**

- Nachts wirken Monster bereits lebendig, auch wenn noch keine Jagdlogik aktiv ist.

### Phase 9.5 — Sichtpruefung und Reichweitenregel einfuehren

Ziel: Die Verfolgung an eine klare, testbare Bedingung koppeln.

**Umfang:**

- Ein Monster darf den Spieler nur in den Chase-Zustand uebernehmen, wenn es den Spieler gesehen hat.
- "Gesehen" bedeutet in diesem Plan: Der Spieler ist innerhalb einer Maze-Distanz von `13` Zellen und es gibt eine gueltige Sicht- oder Verbindungspruefung entlang offener Zellkanten.
- Zunaechst reicht eine logische Verbindungspruefung auf Zellbasis; spaetere echte Sichtkegel koennen darauf aufbauen.
- Wenn die Distanz groesser als `13` Zellen ist, bleibt das Monster im `Wander`-Zustand.

**Noetige Hilfsfunktion in diesem Schritt:**

- `CanSeePlayer(Vector2I monsterCell, Vector2I playerCell, int maxRangeCells)`

**Ergebnis nach diesem Schritt:**

- Die Monster-Aggro ist klar begrenzt und nicht global ueber das gesamte Maze verteilt.

### Phase 9.6 — Wegfindung und Chase-Verhalten bauen

Ziel: Monster sollen den Spieler nach Sichtkontakt intelligent ueber das Maze verfolgen.

**Umfang:**

- Fuer die Verfolgung einen Wegfinde-Algorithmus verwenden, idealerweise A* auf dem vorhandenen Zellgraphen.
- Der Pfad wird nur ueber begehbare Nachbarzellen berechnet; geschlossene Waende blockieren die Kante komplett.
- Sobald ein gueltiger Pfad zum Spieler existiert und die Distanzbedingung erfuellt ist, folgt das Monster diesem Pfad schrittweise.
- Faellt der Spieler aus der Reichweite von `13` Zellen heraus oder gibt es keine gueltige Verbindung mehr, soll das Monster nach kurzer Zeit wieder in `Wander` oder `Search` zurueckfallen.
- Bewegung entlang des Pfads sauber vom visuellen Interpolieren trennen.

**Noetige Hilfsfunktionen in diesem Schritt:**

- `FindPathToPlayer(Vector2I startCell, Vector2I playerCell)`
- `AdvanceAlongPath(double delta)`

**Ergebnis nach diesem Schritt:**

- Monster koennen den Spieler durch das Labyrinth verfolgen, ohne dabei gegen die Maze-Regeln zu verstossen.

### Phase 9.7 — Zustandsautomat aufraeumen und Stun anschliessen

Ziel: Die bisher getrennt entstandenen Verhaltensbausteine in ein stabiles KI-Modell ueberfuehren.

**Empfohlenes Zustandsmodell fuer Version 1:**

- `Idle`: kurzer Startzustand direkt nach Spawn oder Reaktivierung.
- `Wander`: Monster laufen zufaellig durch das Labyrinth, solange sie keinen Spieler sehen.
- `Chase`: Monster verfolgen den Spieler aktiv, sobald die Sichtbedingungen erfuellt sind.
- `Search`: optionaler kurzer Nachlaufzustand, falls der Spieler gerade ausser Sicht geraten ist.
- `Stunned`: Monster sind bewegungsunfaehig und reagieren nicht, solange der Stun-Timer laeuft.

**Stun-Implementierung:**

- einfacher Zustandsautomat `Idle`, `Patrol`, `Chase`, `Stunned`
- bei Stun: Bewegung gestoppt, Material oder Lichtsignal geaendert, Timer laeuft
- nach Ablauf Rueckkehr in aktiven Zustand, aber nur falls noch Nacht ist
- Falls der Code bei `Patrol` bleibt, sollte `Patrol` hier funktional dasselbe wie `Wander` bedeuten: zufaellige Bewegung ohne Zielverfolgung.

**Ergebnis nach diesem Schritt:**

- Das Monster-System ist funktional geschlossen und kann spaeter um Animation, Sound und Balancing erweitert werden.

**Validierungsziele fuer diese Phase:**

- In einem Test-Maze mit z. B. `100` begehbaren Zellen erscheinen im Mittel etwa `4` Monster.
- Kein Monster spawned auf Start oder Ziel.
- Ein Monster mit freier Verbindung und Spielerabstand `<= 13` Zellen wechselt in `Chase`.
- Ein Monster ohne Sichtkontakt bewegt sich weiter zufaellig durch offene Nachbarzellen.
- Kein Monster ueberschreitet jemals eine geschlossene Wandkante.

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
# Maze Project — Multiplayer-Implementierungsplan

> Dieser Plan beschreibt einen realistischen Umbau des bestehenden Godot-4-.NET-Projekts von einem klar singleplayer-orientierten Aufbau zu einem ersten funktionierenden Koop-Multiplayer. Ziel ist kein technischer Komplettneustart, sondern ein kontrollierter Umbau auf Basis der bereits vorhandenen Architektur mit `Main`, `GameSessionState`, `PlayerCharacter3D`, `MazeView3D`, `MonsterManager`, `TrapManager`, `DayNightController`, `HorrorAudioController`, `MazeSerializer` und `SaveGameService`.

**Goal:** Das Spiel soll einen ersten spielbaren Koop-Multiplayer erhalten, bei dem ein Host die Welt autoritativ simuliert und ein oder mehrere Clients sich verbinden, dieselbe Maze-Struktur sehen, eigene Spielerfiguren steuern und konsistente Monster-, Trap- und Tag/Nacht-Zustaende erleben.

**Zentrale Architektur-Entscheidung:** Der Multiplayer wird als **host-authoritative Session** geplant. Ein Spieler hostet, dieser Host simuliert die Welt, und alle Clients senden nur Eingaben oder Bewegungswuensche und erhalten Zustands-Snapshots zurueck. Das passt deutlich besser zur aktuellen Architektur als Peer-to-Peer, weil die wesentliche Spiellogik bereits zentral in `scripts/Main.cs` orchestriert wird.

## Ausgangslage im aktuellen Projekt

**Bereits vorhanden und wiederverwendbar:**

- `scripts/Main.cs` steuert Start, Laden, Reset, Spielzustand, Monster, Fallen, Day/Night, Audio und Spielerverhalten zentral.
- `scripts/Game/GameSessionState.cs` kapselt bereits globalen Weltzustand, ist aber noch komplett auf Singleplayer ausgelegt.
- `scripts/Game/MazeGameConfig.cs` und `scripts/Game/MazeSaveData.cs` enthalten bereits einen grossen Teil der Daten, die fuer Session-Start und Welt-Synchronisation gebraucht werden.
- `scripts/Save/MazeSerializer.cs` und `scripts/Save/SaveGameService.cs` liefern bereits datenorientierte Welt- und Save-Strukturen, die als Grundlage fuer Multiplayer-Snapshots dienen koennen.
- `scripts/Views/PlayerCharacter3D.cs` enthaelt schon Bewegung, Stamina, Signale und First-Person-nahe Darstellung.
- `scripts/Gameplay/Monster/MonsterManager.cs` und die zugehoerigen Monster-Controller kapseln bereits Laufzeitlogik fuer Spawns, Sicht, Chase und Catch.
- `scripts/Audio/HorrorAudioController.cs` ist bereits als lokales Audio-Laufzeitsystem getrennt genug, um spaeter pro Client weiterzuarbeiten.
- `scripts/UI/MainMenu.cs` und `scenes/MainMenu.tscn` liefern einen klaren Einstiegspunkt fuer Host/Join-UI.

**Aktuelle Architekturgrenzen:**

- Es gibt genau einen lokalen Spieler (`_player`) in `Main`.
- Monster- und Trap-Logik beziehen sich implizit auf genau einen Spieler.
- Der aktuelle `GameSessionState` kennt keine Spielerliste.
- Der lokale Input ist direkt mit der Spielerfigur verdrahtet.
- Save-Daten repraesentieren lokale Persistenz, nicht laufende Netzwerksessions.

## Zielbild fuer den Multiplayer

Das erste Multiplayer-Ziel sollte bewusst eng und stabil sein:

- 2 bis 4 Spieler im LAN per IP und Port.
- Ein Host erzeugt oder laedt die Welt.
- Alle Spieler sehen dieselbe Maze.
- Jeder Spieler bewegt eine eigene Figur.
- Monster, Fallen, Tag/Nacht und Sieg/Niederlage werden nur vom Host entschieden.
- Kamera, HUD und Audio bleiben pro Client lokal.
- Kein Matchmaking, kein Relay, kein Reconnect im ersten MVP.

Dieses Ziel ist gross genug fuer echten Koop, aber klein genug, um die bestehende Architektur kontrolliert umzubauen.

## Nicht-Ziele fuer den ersten MVP

Folgende Dinge sollten bewusst nicht in Phase 1 bis 3 erzwungen werden:

- Peer-to-Peer-Simulation
- automatisches Internet-Matchmaking
- NAT-Traversal oder Relay-Server
- voll synchronisierte Audio-Wiedergabe
- Resume laufender Online-Sessions aus Savegames
- Client Prediction und Rollback
- dedizierter Headless-Server ausserhalb des Godot-Hosts

## Technische Grundentscheidung fuer Godot

Der Multiplayer sollte mit Godots eingebautem Netzwerk-Stack aufgebaut werden:

- `ENetMultiplayerPeer` fuer Host und Client
- `SceneMultiplayer` bzw. Godots Standard-Multiplayer-API
- RPCs fuer Lobby, Handshake und Ereignisse
- eigene Snapshot-Daten fuer Welt- und Spielerzustand

Diese Route ist fuer das bestehende Godot-4-.NET-Projekt die risikoaermste und schnellste.

## Phase 1 — Netzwerk-Grundgeruest und Session-Rollen einfuehren

Ziel: Das Projekt bekommt eine saubere Verbindungs- und Session-Schicht, ohne direkt die gesamte Spiellogik zu vermischen.

**Noetige Aenderungen:**

- Eine neue Netzwerk-Session-Komponente einfuehren, die nur diese Aufgaben uebernimmt:
  - Host starten
  - Client verbinden
  - Peer-Join/Leave behandeln
  - Session-Rolle bereitstellen: Offline, Host, Client
  - Verbindungsfehler melden
- `scripts/Main.cs` entlasten und Session-Aufgaben aus der Gameplay-Orchestrierung herausziehen.
- Die Multiplayer-Signale von Godot zentral abonnieren und nicht verstreut in einzelnen Gameplay-Klassen behandeln.

**Neue Bausteine:**

- `scripts/Network/MultiplayerSession.cs`
- optional `scripts/Network/SessionRole.cs`
- optional `scripts/Network/ConnectionStatus.cs`

**Betroffene bestehende Dateien:**

- `scripts/Main.cs`
- `scripts/UI/MainMenu.cs`
- `scenes/MainMenu.tscn`

**Ergebnis der Phase:**

- Das Spiel kann hosten und joinen.
- Verbindungsaufbau, Verbindungsfehler und Peer-Lifecycle sind sauber gekapselt.
- Es existiert noch kein vollstaendiger Multiplayer-Run, aber der Transportweg steht.

## Phase 2 — Hauptmenue fuer Host/Join/Lobby erweitern

Ziel: Das bestehende Menuesystem wird von einem reinen Singleplayer-/Save-Menue zu einem Einstiegspunkt fuer Multiplayer-Sessions erweitert.

**Noetige Aenderungen in der UI:**

- `scripts/UI/MainMenu.cs` um neue Benutzeraktionen erweitern:
  - `HostGameRequested`
  - `JoinGameRequested`
  - optional `CancelConnectionRequested`
- `scenes/MainMenu.tscn` um eine Multiplayer-Sektion erweitern:
  - Spielmodus: Singleplayer / Host / Join
  - IP-Feld
  - Port-Feld
  - optional Spielername
  - Verbindungsstatus
  - Host-spezifischer Start-Button
- Bestehende Save- und New-Game-Flaechen so umbauen, dass klar ist, ob der Lauf offline oder online gestartet wird.

**Wichtige UX-Regeln:**

- Host und Join muessen klar getrennt sein.
- Ein Join-Versuch darf die bestehende UI nicht in einen unklaren Zwischenzustand bringen.
- Fehler wie falsche IP, Port belegt oder Server nicht erreichbar muessen explizit angezeigt werden.

**Betroffene Dateien:**

- `scripts/UI/MainMenu.cs`
- `scenes/MainMenu.tscn`
- `scripts/Main.cs`

**Ergebnis der Phase:**

- Spieler koennen per UI hosten oder joinen.
- Die Verbindung kann ohne Debug-Konsole initiiert werden.

## Phase 3 — Lobby-Handshake und Startvertrag definieren

Ziel: Vor dem eigentlichen Spielstart muss klar definiert werden, welche Daten der Host den Clients sendet und wann ein Client als voll synchronisiert gilt.

**Startdaten, die der Host autoritativ festlegt:**

- `MazeGameConfig`
- Seed
- Maze-Struktur oder vollstaendiger Welt-Snapshot
- Start- und Zielzelle
- Monster-Spawnzellen
- Trap-Definitionen
- Session-Spielerliste
- Zuordnung von Peer-ID zu Spieler-Slot oder Spawnpunkt

**Empfohlene Datenstrategie:**

- Nicht nur den Seed senden, sondern zunaechst einen vollstaendigen Start-Snapshot vorsehen.
- Das ist robuster, weil Monster-Spawns, Fallen und andere abgeleitete Zustandsdaten bereits im Projekt datenorientiert modelliert sind.

**Noetige neue Datenmodelle:**

- `scripts/Network/PlayerIdentity.cs`
- `scripts/Network/PlayerRuntimeState.cs`
- `scripts/Network/PlayerSnapshot.cs`
- `scripts/Network/GameWorldSnapshot.cs`
- `scripts/Network/SessionStartPayload.cs`

**Betroffene bestehende Dateien:**

- `scripts/Game/MazeSaveData.cs`
- `scripts/Game/MazeGameConfig.cs`
- `scripts/Save/MazeSerializer.cs`
- `scripts/Save/SaveGameService.cs`
- `scripts/Main.cs`

**Ergebnis der Phase:**

- Ein Client kann sich nicht nur verbinden, sondern eine definierte Welt vom Host erhalten und daraus denselben Spielstart herstellen.

## Phase 4 — Globalen Session-State von Singleplayer auf Mehrspieler erweitern

Ziel: Der Runtime-State muss mehrere Spieler gleichzeitig abbilden koennen.

**Hauptproblem im Bestand:**

- `scripts/Game/GameSessionState.cs` haelt derzeit nur globale Singleplayer-Daten.
- Mehrere Spieler koennen aktuell nirgends sauber registriert werden.

**Noetige Aenderungen:**

- `GameSessionState` um per-player Runtime-Daten erweitern.
- Globale Weltzustandsdaten von Spielerdaten trennen.
- Eindeutige Peer- oder Player-IDs durchgaengig als Schluessel verwenden.

**Empfohlene Struktur:**

- globaler Weltzustand:
  - FlowState
  - CurrentMaze
  - Start/Goal
  - Monster-Spawnzellen
  - aktive Monsterzellen
  - aktive Fallen
  - Day/Night-Fortschritt
- per-player Zustand:
  - PeerId
  - Position
  - aktuelle Zelle
  - Stamina
  - IsAlive
  - GoalReached
  - IsManualMode oder Kontrollstatus

**Betroffene Dateien:**

- `scripts/Game/GameSessionState.cs`
- `scripts/Main.cs`

**Ergebnis der Phase:**

- Die Laufzeitarchitektur kann mehrere Spieler gleichzeitig abbilden, ohne bestaende Singleplayer-Pfade komplett zu zerstoeren.

## Phase 5 — Spielerinstanzen von lokalem Input entkoppeln

Ziel: `PlayerCharacter3D` darf nicht mehr implizit gleichbedeutend mit dem lokalen Spieler sein.

**Hauptproblem im Bestand:**

- `scripts/Views/PlayerCharacter3D.cs` verarbeitet lokalen Input direkt und verwaltet gleichzeitig Darstellung, Bewegung und Stamina.

**Noetige Aenderungen:**

- Lokalen Input von der reinen Bewegungs- und Avatarlogik trennen.
- `PlayerCharacter3D` fuer zwei Betriebsarten oeffnen:
  - lokale Autoritaet
  - Remote-Replikat
- Ereignisse wie `GoalReached`, `CellVisited` und `StaminaChanged` nicht mehr implizit fuer nur einen Spieler behandeln.
- Pro Spieler eine Avatar-Instanz erzeugen koennen.

**Empfohlene neue Hilfsbausteine:**

- `scripts/Network/NetworkInputFrame.cs`
- optional `scripts/Views/RemotePlayerAvatar.cs` falls die Trennung zur Lesbarkeit hilft
- optional `scripts/Game/PlayerRegistry.cs`

**Betroffene Dateien:**

- `scripts/Views/PlayerCharacter3D.cs`
- `scripts/Main.cs`
- `scripts/Views/MazeView3D.cs`

**Ergebnis der Phase:**

- Lokaler und entfernter Spieler koennen dieselbe Avatar- oder Bewegungsbasis nutzen.
- Input ist nicht mehr direkt in die einzige Figur eingebrannt.

## Phase 6 — Replizierte Spielerbewegung einfuehren

Ziel: Mehrere Spieler koennen sich sichtbar in derselben Welt bewegen.

**Empfohlene Synchronisationsregel fuer den MVP:**

- Clients senden Eingaben oder Bewegungswuensche an den Host.
- Der Host simuliert und validiert.
- Der Host sendet periodisch Spieler-Snapshots.
- Clients interpolieren Remote-Spieler zwischen Snapshots.

**Noetige Snapshot-Felder pro Spieler:**

- Peer-ID
- Weltposition
- Blickrichtung oder Rotation
- aktuelle Zelle
- IsMoving
- IsSprinting
- aktuelle Stamina
- Alive-Status

**Noetige Umbauten:**

- `PlayerCharacter3D` so umbauen, dass Bewegung sowohl lokal als auch per externem Zustand gesetzt werden kann.
- `Main` oder eine dedizierte Replikationsklasse muss eingehende Snapshots auf bestehende Spielerinstanzen anwenden.
- Eine feste Tick-Rate fuer Zustandsupdates festlegen.

**Betroffene Dateien:**

- `scripts/Main.cs`
- `scripts/Views/PlayerCharacter3D.cs`
- `scripts/Views/MazeView3D.cs`
- neue Netzwerk-Snapshot-Dateien

**Ergebnis der Phase:**

- Zwei oder mehr Spieler koennen gleichzeitig sichtbar sein und sich bewegen.
- Das Spiel fuehlt sich erstmals wie echter Koop an.

## Phase 7 — Weltstart und Spawn-Logik fuer mehrere Spieler erweitern

Ziel: Das Spiel muss mehrere Spieler geordnet in die Welt einsetzen koennen.

**Noetige Aenderungen:**

- Spawnlogik in `Main` von einem Startpunkt auf mehrere Startpunkte erweitern.
- Entweder gemeinsamer Startbereich oder leicht versetzte Spawn-Zellen.
- Spawnpunkte muessen mit der Maze-Struktur kompatibel bleiben.
- Nach Join oder Match-Start muessen alle Spielerinstanzen aufgebaut werden.

**Wichtige Entscheidung:**

- Fuer den MVP ist ein gemeinsamer Startbereich mit leichter lokaler Versetzung am einfachsten.
- Eine spaetere per-slot-Spawnlogik kann danach sauber ergaenzt werden.

**Betroffene Dateien:**

- `scripts/Main.cs`
- `scripts/Game/GameSessionState.cs`
- `scripts/Views/MazeView3D.cs`

**Ergebnis der Phase:**

- Mehrere Spieler koennen konsistent starten und werden nicht nur technisch, sondern auch spielerisch korrekt in die Welt eingesetzt.

## Phase 8 — Monster-System von einem Spieler auf mehrere Ziele umbauen

Ziel: Monster muessen mehrere Spieler kennen, aber ihre Entscheidungen duerfen weiterhin nur vom Host getroffen werden.

**Hauptproblem im Bestand:**

- `scripts/Gameplay/Monster/MonsterManager.cs` kennt derzeit genau eine `_playerCell` und genau eine Spieler-Weltposition.

**Noetige Aenderungen:**

- Den Monster-Manager von einem Einzelspielerbezug auf eine Spielerliste umstellen.
- Pro Monster muss bei Bedarf ein Zielspieler gewaehlt werden:
  - naechster sichtbarer Spieler
  - zuletzt gesehener Spieler
  - definierte Prioritaetsregel
- Ereignisse wie `PlayerSpotted` und `PlayerCaught` muessen Peer-bezogen werden.
- Catch- und Chase-Logik darf nur auf dem Host laufen.

**Empfohlene API-Aenderung:**

- statt `UpdatePlayerCell(Vector2I? playerCell)` eher ein spielerlistenbasierter Pfad wie `UpdatePlayers(...)`

**Betroffene Dateien:**

- `scripts/Gameplay/Monster/MonsterManager.cs`
- zugehoerige Monster-Controller in `scripts/Gameplay/Monster/`
- `scripts/Main.cs`
- `scripts/Game/GameSessionState.cs`

**Ergebnis der Phase:**

- Monster verhalten sich gegenueber mehreren Spielern konsistent.
- Desyncs durch clientseitige Monsterentscheidung werden vermieden.

## Phase 9 — Trap- und Day/Night-System host-authoritative synchronisieren

Ziel: Andere Weltsysteme muessen denselben Multiplayer-Grundregeln folgen wie die Monster.

**Trap-System:**

- Fallen duerfen nur auf dem Host konsumiert und deaktiviert werden.
- Clients bekommen nur den aktualisierten Trap-Zustand.
- Spieler duerfen Trap-Ausloesung nicht lokal entscheiden.

**Day/Night-System:**

- `DayNightController` laeuft autoritativ nur auf dem Host.
- Clients bekommen Fortschritt und darstellen ihn lokal.

**Noetige Aenderungen:**

- Trap-Zustandsupdates als Snapshot oder Event uebertragen.
- Day/Night-Fortschritt periodisch oder bei relevanten Aenderungen uebertragen.
- Sicherstellen, dass Pause und Session-Status nicht zu divergierenden Zeitlinien fuehren.

**Betroffene Dateien:**

- `scripts/Main.cs`
- `scripts/Game/GameSessionState.cs`
- Trap-bezogene Klassen unter `scripts/Gameplay/Traps/`
- `scripts/World/DayNightController.cs`

**Ergebnis der Phase:**

- Wichtige globale Weltzustandsmaschinen laufen nur einmal autoritativ.

## Phase 10 — HUD, Kamera und lokale Wahrnehmung sauber trennen

Ziel: Jeder Client sieht dieselbe Welt, aber nicht dieselbe Wahrnehmung.

**Wichtige Regel:**

- Die Welt ist global synchronisiert.
- HUD, Kamera und viele Audioeffekte bleiben lokal.

**Noetige Aenderungen:**

- `MazeView3D` und Kamera-Controller so anpassen, dass sie den lokalen Spieler verfolgen und Remote-Spieler nur darstellen.
- Das HUD darf nur den lokalen Spieler spiegeln:
  - lokale Stamina
  - lokaler Status
  - lokaler Goal- oder Catch-Zustand
- Remote-Spieler sollten optional Namen oder Marker erhalten.

**Betroffene Dateien:**

- `scripts/Views/MazeView3D.cs`
- `scripts/Views/CameraController3D.cs`
- HUD-bezogene Klassen unter `scripts/Hud/`
- `scripts/Main.cs`

**Ergebnis der Phase:**

- Die Darstellung bleibt pro Client logisch und lesbar.
- Lokale Kamera und lokale UI kollidieren nicht mit Remote-Spielern.

## Phase 11 — Audio-Schicht lokal halten und netzwerkfaehig andocken

Ziel: Horror-Audio bleibt pro Client stimmig, ohne globale Audio-Synchronisierung zu erzwingen.

**Wichtige Regel:**

- `scripts/Audio/HorrorAudioController.cs` soll nicht zur autoritativen Netzlogik werden.
- Audio bleibt ein lokales Reaktionssystem.

**Noetige Aenderungen:**

- Den Horror-Audio-Controller weiter auf den lokalen Spieler ausrichten.
- Netzwerk- oder Welt-Events abonnieren fuer:
  - lokaler Spieler gesichtet
  - lokaler Spieler gefangen
  - Trap in der Naehe ausgeloest
  - Match start/end
- Optional andere Spieler mit 3D-Audio-Quellen darstellen.

**Was lokal bleiben sollte:**

- Herzschlag
- Erschoepfung
- Schritt-Feedback des lokalen Spielers
- lokale Naehegefahr

**Betroffene Dateien:**

- `scripts/Audio/HorrorAudioController.cs`
- `scripts/Main.cs`
- eventuell `scripts/Views/MazeView3D.cs`

**Ergebnis der Phase:**

- Audio bleibt atmosphaerisch sauber und wird nicht durch unnoetige Netzsynchronisation verkompliziert.

## Phase 12 — Savegames und Multiplayer sauber gegeneinander abgrenzen

Ziel: Lokale Persistenz und Online-Session duerfen nicht chaotisch ineinander laufen.

**Empfohlene Regel fuer den MVP:**

- Nur der Host darf eine Welt laden oder erzeugen.
- Clients speichern die autoritative Welt nicht als eigenen Live-Session-Save.
- Savegames bleiben zunaechst ein hostseitiges Werkzeug fuer Weltstart.

**Noetige Aenderungen:**

- `scripts/Main.cs` so umbauen, dass Multiplayer-Load nur hostseitig geht.
- Save-Daten bei Bedarf um Session-Metadaten erweitern.
- Nicht versuchen, einen laufenden Onlinezustand sofort als vollwertigen Resume-Save zu behandeln.

**Betroffene Dateien:**

- `scripts/Save/SaveGameService.cs`
- `scripts/Save/MazeSerializer.cs`
- `scripts/Game/MazeSaveData.cs`
- `scripts/Main.cs`

**Ergebnis der Phase:**

- Save- und Netzwerkpfade bleiben beherrschbar und vermischen sich nicht unnötig.

## Phase 13 — Fehlerfaelle, Disconnects und Session-Ende absichern

Ziel: Der Multiplayer ist nicht nur im Happy Path nutzbar, sondern verhaelt sich bei Fehlern kontrolliert.

**Noetige Fehlerfaelle:**

- Client kann Server nicht erreichen
- Host beendet Session
- Client verliert Verbindung waehrend des Spiels
- Peer verlaesst Lobby vor Matchstart
- Version oder Datenmodell stimmen nicht ueberein

**Noetige Aenderungen:**

- Rueckkehrpfade ins Hauptmenue definieren
- Session-Aufraeumen in `Main` und Session-Manager robust machen
- Laufzeit-Instanzen fuer Remote-Spieler bei Disconnect sauber entfernen

**Betroffene Dateien:**

- `scripts/Main.cs`
- `scripts/UI/MainMenu.cs`
- `scenes/MainMenu.tscn`
- neue Netzwerk-Session-Klassen

**Ergebnis der Phase:**

- Verbindungsabbrueche hinterlassen keinen kaputten Spielzustand.

## Phase 14 — Validierung, Testpfade und stabile MVP-Abnahme

Ziel: Jede Ausbaustufe muss mit einem engen Test pruefbar bleiben.

**Empfohlene Validierung pro Ausbaupunkt:**

1. Host startet erfolgreich auf Port.
2. Client kann per IP verbinden.
3. Client erhaelt Welt-Snapshot.
4. Zwei Spieler sind gleichzeitig sichtbar.
5. Remote-Bewegung laeuft flussig genug.
6. Monster jagen denselben Spieler auf allen Instanzen konsistent.
7. Fallen werden genau einmal konsumiert.
8. Goal/Catch-Ereignisse sind auf allen Clients gleich.
9. Disconnect fuehrt zu sauberem Rueckfall.

**Empfohlene technische Checks:**

- `dotnet build .\Maze123.csproj`
- Host/Client-Playtest mit zwei gestarteten Instanzen
- gezielte Debug-Ausgaben fuer Peer-Join, Snapshot-Empfang, Catch-Events und Disconnects

## Empfohlene Umsetzungsreihenfolge

Damit frueh ein spielbarer Zwischenstand entsteht, sollte die Reihenfolge strikt bleiben:

1. Netzwerk-Session und Host/Join-Menue
2. Lobby-Handshake und Session-Startdaten
3. Mehrspielerfaehiger Session-State
4. Mehrere Spielerinstanzen
5. Replizierte Bewegung
6. Host-authoritative Monster
7. Host-authoritative Fallen und Day/Night
8. HUD-, Kamera- und Audio-Lokalisierung
9. Save/Load-Grenzen fuer Multiplayer
10. Fehlerfaelle und Aufraeumen

Der entscheidende Punkt ist, dass nach Schritt 5 bereits ein sichtbarer Koop-Prototyp existiert. Erst danach lohnt es sich, die schwereren Weltsysteme voll auf Multiplayer umzubauen.

## Konkreter MVP-Schnitt

Wenn der Umfang klein und kontrollierbar bleiben soll, sollte der erste echte Multiplayer-Meilenstein nur diese Punkte enthalten:

- 2 Spieler
- LAN per IP und Port
- Host erzeugt oder laedt die Welt
- beide Spieler bewegen sich sichtbar in derselben Maze
- Monster laufen host-authoritativ
- Fallen und Day/Night werden host-authoritativ gespiegelt
- kein Reconnect
- kein Matchmaking
- keine Internet-NAT-Loesung
- keine laufenden Online-Resume-Saves

Dieser Schnitt ist gross genug, um Multiplayer als Feature glaubhaft zu haben, und klein genug, um den Umbau im aktuellen Projekt kontrolliert zu halten.

## Hauptrisiken und Gegenmassnahmen

**Risiko 1: `Main` wird weiter zum Ueber-Gottobjekt.**

- Gegenmassnahme: Session-, Snapshot- und Player-Registry-Logik frueh aus `scripts/Main.cs` auslagern.

**Risiko 2: Monster-Logik bleibt implizit auf einen Spieler verdrahtet.**

- Gegenmassnahme: Monster-Manager API frueh auf Spielerliste umstellen, auch wenn intern zuerst nur der naechste Spieler genutzt wird.

**Risiko 3: `PlayerCharacter3D` bleibt Input- und Netzwerk-Mischobjekt.**

- Gegenmassnahme: lokale Eingabe strikt von Bewegungsdarstellung trennen.

**Risiko 4: Savegames und Session-Snapshots werden vermischt.**

- Gegenmassnahme: hostseitige Saves und laufende Netzwerksnapshots als getrennte Pfade behandeln.

**Risiko 5: Audio wird unnoetig netzwerkzentralisiert.**

- Gegenmassnahme: Audio lokal halten, nur Welt- oder Event-Signale einspeisen.

## Betroffene Dateien im Bestand

**Hohe Aenderungswahrscheinlichkeit:**

- `scripts/Main.cs`
- `scripts/Game/GameSessionState.cs`
- `scripts/Game/MazeGameConfig.cs`
- `scripts/Game/MazeSaveData.cs`
- `scripts/UI/MainMenu.cs`
- `scenes/MainMenu.tscn`
- `scripts/Views/PlayerCharacter3D.cs`
- `scripts/Views/MazeView3D.cs`
- `scripts/Gameplay/Monster/MonsterManager.cs`
- `scripts/Audio/HorrorAudioController.cs`
- `scripts/Save/MazeSerializer.cs`
- `scripts/Save/SaveGameService.cs`

**Wahrscheinliche neue Dateien oder Ordner:**

- `scripts/Network/MultiplayerSession.cs`
- `scripts/Network/PlayerIdentity.cs`
- `scripts/Network/PlayerRuntimeState.cs`
- `scripts/Network/PlayerSnapshot.cs`
- `scripts/Network/GameWorldSnapshot.cs`
- `scripts/Network/SessionStartPayload.cs`
- `scripts/Network/NetworkInputFrame.cs`
- optional `scripts/Game/PlayerRegistry.cs`

## Abschluss

Der Multiplayer sollte in diesem Projekt nicht als kleiner Add-on-Patch verstanden werden. Die bestehende Architektur ist bereits stark genug, um einen Koop-Modus zu tragen, aber nur dann, wenn sauber zwischen Session, autoritativer Weltlogik und lokaler Darstellung getrennt wird. Der richtige Weg ist deshalb nicht, moeglichst viel automatisch zu synchronisieren, sondern die bereits zentrale Weltsteuerung in `Main` schrittweise in eine host-authoritative Mehrspielerarchitektur zu ueberfuehren.

Wenn dieser Plan umgesetzt wird, entsteht zuerst ein kleiner, robuster LAN-Koop-MVP. Auf dieser Basis koennen spaeter Matchmaking, bessere Lobby-UX, Client Prediction, dedizierter Server oder Resume-Sessions folgen, ohne dass der Kern erneut umgebaut werden muss.
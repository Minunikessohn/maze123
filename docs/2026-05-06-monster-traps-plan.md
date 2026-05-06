# Maze Project — Monster-Fallen im Labyrinth — Implementierungsplan

> Dieser Plan baut auf der aktuellen Architektur mit `Main`, `GameSessionState`, `MazeSaveData`, `MazeView3D`, `MonsterManager` und den vorhandenen Zell-/Maze-Datenstrukturen auf. Ziel ist ein erstes, robustes Fallen-System fuer Monster, nicht direkt ein allgemeines Item- oder Kampf-System.

**Goal:** Im Labyrinth sollen Fallen generiert werden, die genau **eine Zelle** belegen. Der Spieler wird von diesen Fallen **nicht beeinflusst** und kann ohne Effekt durch ihre Zelle laufen. Ein Monster, das eine Fallen-Zelle betritt, wird **despawnt**. Die Fallen sollen mit dem bestehenden Save-/Load-System kompatibel sein und spaeter erweiterbar bleiben.

**Kernentscheidung:** Fuer die erste Ausbaustufe gibt es **genau einen Fallentyp**: `MonsterTrap`. Keine Schadenswerte, keine Spielerinteraktion, kein komplexer Zustand. Sobald ein aktives Monster dieselbe Zelle betritt, wird es vom `MonsterManager` entfernt und die Falle entweder verbraucht oder sichtbar deaktiviert. Fuer die erste Version ist **Einweg-Falle** die sauberste Regel, weil sie eindeutiges Feedback gibt und Spawn-Balancing einfacher macht.

## Architektur-Idee

Die bestehende Architektur trennt bereits sauber zwischen:

- persistierten Spawn-Daten (`GameSessionState.MonsterSpawnCells`, `MazeSaveData.MonsterSpawnCells`)
- zur Laufzeit aktiven Instanzen (`MonsterManager`, `ActiveMonsterCells`)
- zentraler Spielfluss-Steuerung in `Main`

Die Fallen sollten deshalb **nicht** in `Main` als lose Listen und **nicht** in `MonsterController` als Sonderlogik leben. Stattdessen wird ein eigener `TrapManager` eingefuehrt, analog zum `MonsterManager`:

- `Main` berechnet oder laedt Fallen-Zellen
- `GameSessionState` haelt persistente und aktive Trap-Daten
- `TrapManager` erzeugt/verwaltet sichtbare Fallen im 3D-Maze
- `MonsterManager` meldet Zellwechsel oder Despawn-Ereignisse an den `TrapManager`

Damit bleibt die Regel "Monster trifft Falle -> Monster despawnt" an einer einzigen Stelle kontrollierbar.

## Phase 1 — Datenmodell fuer Fallen festziehen

Ziel: Die Fallendaten muessen dieselbe Sprache sprechen wie Monster-Spawnzellen und Save-Daten.

**Neue Dateien:**

- `scripts/Gameplay/Traps/TrapManager.cs`
- `scripts/Gameplay/Traps/TrapInstance.cs`
- `scripts/Game/TrapDefinition.cs`

**Bestehende Dateien erweitern:**

- `scripts/Game/GameSessionState.cs`
- `scripts/Game/MazeSaveData.cs`
- optional `scripts/Game/MazeGameConfig.cs`

**Empfohlenes Minimalmodell:**

`TrapDefinition`

- `string TrapId`
- `Vector2I Cell`
- `bool IsArmed`

`GameSessionState`

- `List<TrapDefinition> TrapDefinitions`
- `List<Vector2I> ActiveTrapCells`

`MazeSaveData`

- vorhandene `TrapSaveData` weiterverwenden
- um `bool IsArmed` erweitern, falls Verbrauch gespeichert werden soll

**Wichtige Regel:** In Phase 1 darf eine Falle **rein zellbasiert** sein. Keine freie Weltposition, keine Rotationsdaten, keine Groessenvarianz. Das passt zur vorhandenen Maze- und Monsterbewegung und vermeidet unnötige Genauigkeitsprobleme.

## Phase 2 — Spawnregeln fuer Fallen definieren

Ziel: Fallen sollen fair, deterministisch und mit den vorhandenen Maze-Regeln verträglich generiert werden.

**Generierungsregeln fuer Version 1:**

- Fallen werden nur erzeugt, wenn `MazeGameConfig.TrapGenerationEnabled == true`.
- Jede Falle belegt genau eine offene Maze-Zelle.
- Startzelle und Zielzelle sind tabu.
- Monster-Spawnzellen sind tabu.
- Optional: direkte Nachbarn von Start und Ziel ebenfalls tabu, damit Fallen den Anfang und das Ende nicht unruhig machen.
- Pro Zelle hoechstens eine Falle.

**Spawnformel-Vorschlag:**

- konservativ starten mit etwa `maze.Width * maze.Height * 0.005`
- also ungefaehr `0.5%` aller Zellen
- mindestens `1`, hoechstens Anzahl gueltiger Kandidaten

**Warum niedrig starten:** Monster sind aktuell bereits ein Drucksystem. Wenn Fallen zu dicht stehen, verschwinden Monster zu schnell, und der Spieler erlebt die Mechanik kaum. Eine geringe Dichte erlaubt spaeter Balancing mit echten Beobachtungen.

**Determinismus:**

- Fallen mit demselben `Random`/Seed wie das restliche Spiel erzeugen
- nach Save/Load niemals neu ausrollen, sondern aus `MazeSaveData.Traps` laden

## Phase 3 — TrapManager als owning abstraction einfuehren

Ziel: Fallen nicht als Hilfslogik in `Main`, sondern als eigenes Laufzeitsystem behandeln.

**Neue Szene:**

- `scenes/MonsterTrap.tscn`

**Neue Skripte:**

- `scripts/Gameplay/Traps/TrapManager.cs`
- `scripts/Gameplay/Traps/TrapInstance.cs`

**Node-Struktur-Vorschlag:**

- `MazeView3D`
- `MonsterManager`
- `TrapManager`
- einzelne `MonsterTrap`-Instanzen als Kinder von `TrapManager`

**TrapManager-Aufgaben:**

- Fallen aus `TrapDefinition`-Listen instanziieren
- aktive Fallen-Zellen in einem `Dictionary<Vector2I, TrapInstance>` halten
- Zellabfrage `TryConsumeTrapAtCell(Vector2I cell)` anbieten
- bei neuem Spiel, Reset und Load sauber despawnen bzw. neu aufbauen

**TrapInstance-Aufgaben:**

- eigene Zellkoordinate kennen
- `IsArmed`-Zustand halten
- sichtbares Armed/Consumed-Feedback steuern

**Bewusste Abgrenzung:** `TrapInstance` entscheidet **nicht**, ob ein Monster stirbt. Diese Entscheidung bleibt im Zusammenspiel `MonsterManager` <-> `TrapManager`, damit Trap-Logik nicht ueber Szenen verstreut wird.

## Phase 4 — Kollision als Zellereignis statt 3D-Overlap

Ziel: Die Fallen-Regel muss stabil zur vorhandenen Monsterbewegung passen.

Die Monster bewegen sich bereits **zellbasiert** und `MonsterManager` kennt ueber `CellChanged` die aktuelle Monster-Zelle. Genau diese Information sollte benutzt werden. Keine Area3D-Kollision, kein Physics-Layer-System fuer die erste Version.

**Empfohlener Ablauf:**

1. `MonsterController` beendet eine Bewegung und emittiert `CellChanged`.
2. `MonsterManager` aktualisiert wie bisher `ActiveMonsterCells`.
3. Direkt danach fragt `MonsterManager` beim `TrapManager`: `TryConsumeTrapAtCell(currentCell)`.
4. Falls `true`: Monster aus der aktiven Liste entfernen und `QueueFree()`.
5. Session-State und Proximity-Daten danach sofort synchronisieren.

**Warum so:**

- passt exakt zur vorhandenen Monster-Architektur
- keine Probleme mit schwebender Modellmitte oder Light-Offset
- Zelle ist bereits die Spielregel-Einheit fuer Maze, Monster und Save-System

## Phase 5 — Despawn sauber ueber MonsterManager kapseln

Ziel: Das Entfernen eines Monsters darf nicht als Sonderfall halb manuell an mehreren Stellen passieren.

Aktuell besitzt `MonsterManager` nur `DespawnAll()`. Fuer Fallen braucht er eine gezielte Variante:

- `DespawnMonster(MonsterController monster)`
- optional `DespawnMonsterAtIndex(int index)` intern

**Diese Methode muss immer alles zusammenraeumen:**

- `CellChanged`-Subscription loesen
- Monster aus `_activeMonsters` entfernen
- Index-/Cell-Listen aktualisieren
- `_stunOverlapMonsters` bereinigen
- Node freigeben

**Wichtiger Punkt:** Die aktuelle `_monsterIndices`-Struktur muss nach gezieltem Entfernen korrekt neu aufgebaut oder angepasst werden. Sonst laufen spaetere Zellupdates ins Leere. Das ist der Hauptgrund, warum Trap-Despawn eine eigene Manager-Methode braucht und kein direkter `QueueFree()`-Aufruf sein darf.

## Phase 6 — Spieler explizit immun halten

Ziel: Die Anforderung "Spieler wird nicht beeinflusst" muss als echte Designregel sichtbar sein und nicht nur zufaellig gelten.

**Klare Regel fuer Version 1:**

- Spieler kann Fallen optisch sehen.
- Spieler kann Fallen-Zellen betreten.
- Es gibt keine Verlangsamung, keinen Schaden, keinen Pushback, keinen Stun.
- Fallen reagieren nur auf Monster-Zellwechsel.

**Technische Folge:**

- keine Trap-Pruefung in `PlayerCharacter3D`
- keine Trap-Pruefung in `Main.UpdateMonsterStunCollision()`
- keine Player/Trap-Physics notwendig

So bleibt die Immunitaet nicht implizit, sondern als bewusste Architekturentscheidung verankert.

## Phase 7 — Sichtbarkeit und Lesbarkeit der Fallen

Ziel: Spieler muessen Fallen erkennen koennen, damit das System strategisch nutzbar ist.

**Visuelle Leitlinie fuer erste Version:**

- kleine, klar lesbare Bodenmarkierung pro Zelle
- keine hohe Geometrie, die den Weg blockiert oder Sichtlinien stoert
- im Dunkeln leicht leuchtend, aber schwächer als Monster-Glow

**Geeignete Darstellung:**

- flaches `CylinderMesh` oder `QuadMesh` knapp ueber dem Boden
- rote/orange Warnfarbe oder metallische Bodenplatte mit Glutkern
- Armed und Consumed visuell unterschiedlich

**Consumed-Zustand:**

- entweder sofort ausblenden
- oder kurz deaktiviert anzeigen und dann entfernen

Fuer die erste Version ist `kurzer Effekt -> ausblenden -> Zelle frei` wahrscheinlich am klarsten.

## Phase 8 — Save/Load und Reset integrieren

Ziel: Fallen muessen dieselbe Lebensdauer wie Maze und Monster haben.

**Beim neuen Spiel:**

- Fallen generieren
- in `GameSessionState` ablegen
- in `MazeSaveData.Traps` persistieren

**Beim Laden:**

- Fallen aus Save lesen
- `TrapManager.Configure(...)`
- aktive Szeneninstanzen neu aufbauen

**Beim Reset oder Rueckkehr ins Hauptmenue:**

- `TrapManager.Clear()`
- `GameSessionState.ActiveTrapCells.Clear()`

**Wichtige Entscheidung fuer Saves:**

Wenn Einweg-Fallen nach Aktivierung verschwinden, sollte der Save nur den **aktuellen Armed-Zustand** speichern, falls mitten im Lauf gespeichert werden soll. Falls Saves weiterhin nur Maze-Startzustaende sichern, reicht die statische Trap-Liste ohne Verbrauchszustand.

## Phase 9 — Balancing- und Platzierungsheuristiken

Ziel: Fallen sollen Monster nuetzlich reduzieren, aber nicht sofort alle neutralisieren.

**Sinnvolle Startheuristiken:**

- Fallen bevorzugt in mittleren bis tiefen Maze-Bereichen statt direkt am Start
- Fallen nicht direkt auf alle chokepoints legen
- Mindestabstand zwischen Fallen, z. B. Manhattan-Distanz `>= 3`
- keine Falle auf Zielzelle oder direkt daneben

**Pragmatischer Start fuer dieses Repo:**

1. Kandidatenliste aller erlaubten Zellen erzeugen.
2. Kandidaten nach BFS-Distanz vom Start filtern, z. B. nur Distanz `>= 6`.
3. Zufallsauswahl mit Mindestabstand anwenden.

Das ist konsistent mit der vorhandenen Monster-Spawnlogik, die bereits Distanzregeln nutzt.

## Phase 10 — Kleiner, testbarer Implementierungsweg

Ziel: Nicht alles gleichzeitig bauen, sondern in kleinen, validierbaren Schritten.

**Empfohlene Reihenfolge:**

1. Datenmodell erweitern: `TrapDefinition`, Session-State, Save-Daten.
2. `TrapManager` ohne Visuals bauen: nur Zelllisten und `TryConsumeTrapAtCell`.
3. `MonsterManager` um gezielten Despawn-Pfad erweitern.
4. Monster-Zellwechsel mit Trap-Abfrage verbinden.
5. Erst danach `MonsterTrap.tscn` und `TrapInstance` fuer Sichtbarkeit hinzufuegen.
6. Save/Load anschliessen.
7. Balancing anpassen.

**Warum diese Reihenfolge:** Erst muss die Spielregel korrekt sein, dann die Darstellung. Sonst wird leicht ein sichtbares, aber spielmechanisch unzuverlaessiges System gebaut.

## Konkrete Andockpunkte im aktuellen Repo

- `scripts/Main.cs`: Trap-Generierung/Laden orchestrieren, analog zu Monster-Spawnzellen
- `scripts/Game/GameSessionState.cs`: aktive und persistente Trap-Daten halten
- `scripts/Game/MazeSaveData.cs`: Trap-Save-Daten erweitern
- `scripts/Gameplay/Monster/MonsterManager.cs`: Zellwechsel mit Trap-Abfrage koppeln und gezielten Despawn einfuehren
- `scenes/MazeView3D.tscn`: `TrapManager` als Geschwister von `MonsterManager`

## Offene Entscheidungen

Diese Punkte sollten vor der Umsetzung bewusst festgelegt werden:

- Sind Fallen immer Einweg oder dauerhaft aktiv?
- Soll eine Falle beim Triggern sofort verschwinden oder kurz sichtbar deaktiviert bleiben?
- Sollen Fallen nur nachts relevant sein, weil Monster nur nachts aktiv sind, oder trotzdem immer geladen bleiben?
- Sollen Monster auf einer Fallen-Zelle spawnen duerfen? Empfehlung: nein.
- Soll der Spieler Fallen selbst platzieren koennen? Empfehlung fuer jetzt: nein, nur prozedural generiert.

## Empfehlung

Fuer dieses Projekt ist die beste erste Version:

- **prozedural generierte Einweg-Fallen**
- **zellbasiert statt physikbasiert**
- **Spieler komplett immun**
- **Monster-Despawn zentral im `MonsterManager`**
- **visuelle Bodenmarkierung in `TrapManager`/`TrapInstance`**

Damit bleibt das Feature klein, nachvollziehbar, save-kompatibel und passt sauber zu den bestehenden Maze-/Monster-Systemen.
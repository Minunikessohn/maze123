# Maze School Project — Gamification (Phasen 16–19) — Implementierungsplan

> **Für agentische Worker:** REQUIRED SUB-SKILL: Verwende `superpowers:subagent-driven-development` (empfohlen) oder `superpowers:executing-plans` zur task-by-task-Umsetzung. Schritte verwenden Checkbox-Syntax (`- [ ]`).
>
> **Für Schüler:** Du kannst die Phasen und Tasks einzeln nacheinander durcharbeiten. Jeder Task ist so aufgebaut, dass er für sich genommen lauffähigen Code produziert (`dotnet build` bricht nicht, das Spiel startet weiterhin). Kommentare im Code sind bewusst ausführlich.
>
> **Vorläufer-Plan A:** [`docs/2026-04-28-maze-school-project.md`](2026-04-28-maze-school-project.md) (Phasen 0–11 — Grundgerüst, Generatoren, Solver, 2D/3D-Views, HUD).
>
> **Vorläufer-Plan B:** [`docs/2026-04-29-maze-improvements.md`](2026-04-29-maze-improvements.md) (Phasen 12–15 — große Mazes, Tempo-frei, frei steuerbare 3D-Kamera, 2D-Pan/Zoom).

**Goal:** Vier Gamification-Erweiterungen am laufenden Maze-Projekt umsetzen — eine 3D-Spielfigur, die den vom Solver gefundenen Pfad sichtbar abläuft (mit optionaler Verfolger-Kamera), einen Selbstspiel-Modus mit WASD-Steuerung, Wandkollision, Stoppuhr und Sieg-Erkennung, einen Entdeckungs-Modus, der die Sicht des Spielers auf einen Lichtkreis um die Figur einschränkt, sowie einen Polish-Schritt, der den Capsule-Platzhalter durch eine animierte Lego-/Minecraft-aehnliche Figur ersetzt (sechs getexturierte Quader mit Sinus-Wellen-Animation). Der Plan baut bewusst aufeinander auf: die Figur aus Phase 16 wird in Phase 17 manuell steuerbar, die Beleuchtung aus Phase 18 setzt voraus, dass die Figur aus Phase 16 bereits existiert, und Phase 19 ersetzt nur das *Aussehen* — die Steuerung bleibt unveraendert.

**Didaktischer Bogen:** Phase 16 zeigt das Ergebnis des Solvers als Bewegung — der Algorithmus hat den Pfad nicht nur in Farben markiert, sondern eine Figur kann ihn ablaufen. Phase 17 dreht die Perspektive: der Schüler erlebt selbst, wie aufwändig es ist, einen Weg zu finden, wenn man nicht den ganzen Plan sieht. Phase 18 setzt diesen Kontrast spürbar in Szene — Wände in der Ferne verschwinden im Dunkel, der Solver hingegen "sieht" weiterhin alles. Daraus lässt sich im Unterricht direkt das Thema *Informationsasymmetrie* aufgreifen: Algorithmen profitieren davon, das ganze Suchraum-Modell im Speicher zu haben, der Mensch muss erkunden.

**Architecture:** Drei nacheinander umgesetzte Phasen.

- **Phase 16** fügt eine `PlayerCharacter3D`-Klasse als Kind der `MazeView3D`-Szene hinzu (CapsuleMesh + leichte Y-Anhebung). `Main` sammelt während des Solver-Laufs aus jedem `SolverStep` mit `NewState == Path` die Zelle in eine `List<Cell>` und übergibt diese — mit Start- und Goal-Zelle eingerahmt — nach `OnSolverFinished` der Figur. Die Figur hält eine Liste von Welt-Wegpunkten und linear interpoliert in `_Process` zwischen ihnen. Der `CameraController3D` aus Phase 14 bekommt einen optionalen `FollowTarget`-Modus, der bei aktivem Toggle die Free-Kamera-Logik überspringt und stattdessen einen Verfolgungs-Offset hinter dem Target hält.
- **Phase 17** erweitert `PlayerCharacter3D` um einen Manual-Modus mit cell-aligned Bewegung. Der Modus speichert eine Referenz auf das `Maze` und auf die aktuelle Zelle; pro Tastendruck (W/A/S/D = Nord/West/Süd/Ost) wird gegen `Cell.HasWall` geprüft, und falls offen, eine 1-Zellen-Lerp-Animation gestartet. Beim Erreichen der Goal-Zelle wird ein `GoalReached`-Signal mit der Spielzeit emittiert. `Main` startet den Modus auf "Selbst spielen"-Knopfdruck und unterdrückt im Manual-Modus die Free-Kamera-Tastenbelegung des `CameraController3D`, damit die WASD-Eingaben nicht doppelt wirken.
- **Phase 18** fügt im `MazeView3D` einen `OmniLight3D`-Knoten als Kind der Spielfigur und einen `WorldEnvironment`-Knoten mit einstellbarer Fog-Konfiguration hinzu. Eine HUD-Checkbox schaltet zwischen "Tageslicht" (DirectionalLight an, OmniLight aus, Fog aus) und "Entdeckungs-Modus" (DirectionalLight gedimmt, OmniLight an, Fog an) um. Die Übergangs-Lerps werden tween-basiert in `MazeView3D._Process` berechnet, damit das Umschalten weich wirkt.

**Tech Stack:**
- Godot 4.6.2 .NET (mono), C# 12 (`<TargetFramework>net8.0</TargetFramework>`)
- Forward+ Renderer mit D3D12
- Windows + PowerShell Workflow gemäß `.github/skills/godot-csharp-windows`
- Godot Executable: `$env:GODOT4` (Fallback `C:\temp\_godot\Godot_v4.6.2-stable_mono_win64.exe`)

**Konventionen (aus den Vorläuferplänen übernommen):**
- Klassendateiname == Klassenname (Godot-C#-Pflicht).
- `public partial class` und `using Godot;` für jedes Godot-Skript.
- Englische Identifier, deutsche Kommentare bei didaktisch wertvollen Stellen.
- `null!` Backing-Field-Pattern für Knoten, die in `_Ready()` aufgelöst werden.
- Nach jedem Hinzufügen oder Umbenennen eines C#-Skripts mit `[Export]` oder `[Signal]`: `& $env:GODOT4 --path $PWD --build-solutions` ausführen.
- Reine Codeänderungen: `dotnet build` reicht.
- Y-Achse im Grid (`Cell.Y`) entspricht der Z-Achse in 3D-Welt-Koordinaten — die Konvertierung `Cell (x,y)` → `Vector3(x*CellSize + CellSize/2, 0, y*CellSize + CellSize/2)` ist die feste Regel.

---

## Phase 16 — Solver-Bot mit Verfolger-Kamera

Ziel: Nach Abschluss eines Solvers wandert eine 3D-Figur (CapsuleMesh) sichtbar vom Start- zum Zielfeld, indem sie die markierten Path-Zellen in der vom Solver vorgegebenen Reihenfolge abläuft. Eine Verfolger-Kamera kann im HUD per Checkbox aktiviert werden — ist sie aus, behält der User die freie Kamera aus Phase 14, ist sie an, schwebt die Kamera weich aus Halbhöhe hinter der Figur.

**Didaktischer Punkt:** Der Solver-Algorithmus wirkt vorher abstrakt (Felder werden bunt eingefärbt). Mit der Figur sehen Schüler den Pfad als physische Bewegung — das macht den Unterschied zwischen "BFS findet kürzesten Weg" und "DFS macht Umwege" sofort intuitiv erlebbar.

### Task 16.1: `PlayerCharacter3D` als CapsuleMesh-Knoten anlegen

**Files:**
- Create: `scripts/Views/PlayerCharacter3D.cs`
- Modify: `scenes/MazeView3D.tscn` (neuer Kindknoten unterhalb von `MazeView3D`)

- [ ] **Step 1: `scripts/Views/PlayerCharacter3D.cs` anlegen**

```csharp
using System.Collections.Generic;
using Godot;
using Maze.Model;

namespace Maze.Views;

/// <summary>
/// Die im 3D-Maze sichtbare Spielfigur. Haelt eine Liste von Wegpunkten
/// (in Welt-Koordinaten) und interpoliert pro Frame zwischen ihnen.
///
/// Die Figur wird in Phase 16 nur passiv vom Solver-Bot benutzt; in Phase 17
/// kommt der Manual-Modus dazu, in Phase 18 die Lichtquelle als Kind.
/// </summary>
public partial class PlayerCharacter3D : Node3D
{
    [Signal] public delegate void GoalReachedEventHandler();

    /// <summary>Geschwindigkeit in Zellen pro Sekunde.</summary>
    [Export] public float MoveSpeed = 4f;

    /// <summary>Y-Anhebung der Figur. Capsule mit Hoehe 1.0 sitzt mit Mitte auf 0.5.</summary>
    [Export] public float StandHeight = 0.5f;

    private readonly List<Vector3> _waypoints = new();
    private int _currentIndex;
    private bool _isMoving;
    private float _cellSize = 1f;

    /// <summary>
    /// Beendet jede laufende Animation und versteckt die Figur.
    /// Wird gerufen, wenn ein neues Maze gebaut wird, bevor der naechste Solver startet.
    /// </summary>
    public void Hide()
    {
        _waypoints.Clear();
        _isMoving = false;
        Visible = false;
    }

    /// <summary>
    /// Setzt die Figur an die Startzelle und beginnt, der uebergebenen Cell-Liste zu folgen.
    /// Die Liste muss bereits Start- und Zielzelle einschliessen und in
    /// Reihenfolge des Pfades sortiert sein.
    /// </summary>
    public void StartFollowingPath(List<Cell> path, float cellSize)
    {
        _cellSize = cellSize;
        _waypoints.Clear();
        foreach (var cell in path)
            _waypoints.Add(CellToWorld(cell));

        if (_waypoints.Count == 0)
        {
            Visible = false;
            _isMoving = false;
            return;
        }

        Position = _waypoints[0];
        Visible = true;
        _currentIndex = 1;
        _isMoving = _waypoints.Count > 1;
    }

    public override void _Process(double delta)
    {
        if (!_isMoving) return;

        Vector3 target = _waypoints[_currentIndex];
        Vector3 toTarget = target - Position;
        float remaining = toTarget.Length();
        float step = MoveSpeed * _cellSize * (float)delta;

        if (step >= remaining)
        {
            // Zielwegpunkt mit kleinem Restschritt erreicht; an naechsten Wegpunkt weiterruecken.
            Position = target;
            _currentIndex++;
            if (_currentIndex >= _waypoints.Count)
            {
                _isMoving = false;
                EmitSignal(SignalName.GoalReached);
            }
        }
        else
        {
            Position += toTarget.Normalized() * step;
        }
    }

    /// <summary>Konvertiert Grid-Koordinaten in Welt-Koordinaten gemaess MazeView3D-Konvention.</summary>
    private Vector3 CellToWorld(Cell cell) =>
        new(cell.X * _cellSize + _cellSize / 2f, StandHeight, cell.Y * _cellSize + _cellSize / 2f);
}
```

> **Hinweis:** `MoveSpeed` ist in *Zellen pro Sekunde*, nicht Welt-Einheiten. Damit funktioniert die Geschwindigkeit fuer alle CellSize-Einstellungen unveraendert — das ist didaktisch wertvoller als ein Welt-Einheiten-Wert, der bei groesseren Mazes ploetzlich anders wirkt.

- [ ] **Step 2: `scenes/MazeView3D.tscn` — `Player`-Knoten hinzufuegen**

Oeffne `scenes/MazeView3D.tscn`. Du findest am Anfang einen `[ext_resource]`-Block fuer `MazeView3D.cs` und `CameraController3D.cs`. Erweitere den Header zuerst, sodass das neue Skript geladen wird:

```text
[ext_resource type="Script" path="res://scripts/Views/PlayerCharacter3D.cs" id="3_player"]
```

Vergroessere die Anzahl `load_steps` in der `[gd_scene]`-Zeile entsprechend (um 1 erhoehen, plus 1 fuer das CapsuleMesh-Sub-Resource, also +2 falls noch nicht vorhanden).

Direkt unterhalb der `[ext_resource]`-Bloecke fuege eine Sub-Resource fuer das CapsuleMesh ein:

```text
[sub_resource type="CapsuleMesh" id="CapsuleMesh_player"]
height = 1.0
radius = 0.25
```

Direkt unterhalb des `Camera3D`-Knotens (oder am Ende der Datei) fuege den Spielfigur-Knoten ein:

```text
[node name="Player" type="Node3D" parent="."]
script = ExtResource("3_player")
visible = false

[node name="Mesh" type="MeshInstance3D" parent="Player"]
mesh = SubResource("CapsuleMesh_player")
```

> **Wichtig:** `visible = false` ist kein Schreibfehler — die Figur soll erst beim Solver-Bot-Start sichtbar werden. `Hide()` und `Visible = true` aus dem Skript reichen, weil Kindknoten ohne `top_level` mit dem Parent ein-/ausgeblendet werden.

- [ ] **Step 3: Build pruefen**

```powershell
& $env:GODOT4 --path $PWD --build-solutions
dotnet build
```

Erwartet: `Build succeeded`. Die Szene laesst sich oeffnen, die Figur ist im Editor an Position (0,0,0) versteckt sichtbar (im Tree als `Player` zu sehen, im Viewport ausgeblendet).

- [ ] **Step 4: Commit**

```bash
git add scripts/Views/PlayerCharacter3D.cs scenes/MazeView3D.tscn
git commit -m "Task 16.1: PlayerCharacter3D-Skelett mit CapsuleMesh"
```

### Task 16.2: `Main` sammelt den Solver-Pfad und startet die Figur

**Files:**
- Modify: `scripts/Main.cs`

- [ ] **Step 1: Felder fuer Pfad-Sammlung und Player-Referenz ergaenzen**

In `scripts/Main.cs`, im Block der Knotenreferenzen (oberhalb von `_Ready`), zwei neue Felder einfuegen:

```csharp
private PlayerCharacter3D _player = null!;
private readonly List<Cell> _solverPath = new();
```

In `_Ready()`, nach der `_view3D`-Aufloesung, die Player-Referenz aufloesen:

```csharp
_player = GetNode<PlayerCharacter3D>("MazeView3D/Player");
_player.GoalReached += OnBotGoalReached;
```

> **Hinweis:** Wenn die Figur in Phase 17 selbst gesteuert wird, feuert dasselbe Signal — der Handler unterscheidet ueber einen Modus-Flag (kommt in 17.2). Hier reicht ein einfacher Log-Output.

- [ ] **Step 2: Pfad waehrend des Solver-Laufs sammeln**

Erweitere `OnSolveRequested` direkt nach dem `_runner.StopAll();`:

```csharp
_solverPath.Clear();
_player.Hide();
```

> **Bugfix-Nachtrag (Reset/Neugenerierung):** Es gab einen Laufzeitfehler, bei dem der Bot nach `Reset` oder nach einer Neugenerierung scheinbar durch Waende lief. Ursache war ein nicht vollstaendig zurueckgesetzter Bot-/Pfad-Zustand zwischen Runs. Die Loesung ist ein harter Reset von `_solverPath` + `_player.Hide()` **nicht nur** vor dem Solve, sondern auch in `OnGenerateRequested` und `OnResetRequested`, jeweils vor Start des naechsten Ablaufs.

Erweitere `OnSolverStepProduced` direkt nach `step.Cell.Distance = step.Distance;`:

```csharp
// Den finalen Pfad zellweise sammeln; die Reihenfolge entspricht der vom Solver
// emittierten Index-Reihenfolge (Distance == Pfad-Index).
if (step.NewState == CellState.Path)
    _solverPath.Add(step.Cell);
```

- [ ] **Step 3: Bei Solver-Ende den Bot mit dem Pfad starten**

Ersetze `OnSolverFinished` komplett:

```csharp
private void OnSolverFinished()
{
    _view2D.ForceRefresh();
    _tracker.Stop();
    _stats.UpdateStats(_tracker.Elapsed, _tracker.Steps, _tracker.VisitedCells, _tracker.PathLength, _tracker.ManagedMemoryDeltaBytes);
    GD.Print("[Main] Solver fertig.");

    // Pfad defensiv nach Distance sortieren - falls ein Solver Path-Schritte
    // nicht in Index-Reihenfolge yieldet, ist die Animation trotzdem korrekt.
    _solverPath.Sort((a, b) => a.Distance.CompareTo(b.Distance));

    // Vollstaendige Wegpunktliste aufbauen: Start, alle Path-Zellen, Goal.
    var fullPath = new List<Cell>(_solverPath.Count + 2) { _solverStart };
    fullPath.AddRange(_solverPath);
    fullPath.Add(_solverGoal);

    // Wenn keine Loesung gefunden wurde (Pfad leer und Start nicht direkt am Goal),
    // den Bot gar nicht erst starten - sonst wuerde er quer durchs Maze teleportieren.
    if (_solverPath.Count == 0 && !AreNeighbors(_solverStart, _solverGoal))
    {
        GD.Print("[Main] Kein Pfad zum Loesen vorhanden - Bot bleibt versteckt.");
        return;
    }

    _player.StartFollowingPath(fullPath, _view3D.CellSize);
}

private static bool AreNeighbors(Cell a, Cell b) =>
    System.Math.Abs(a.X - b.X) + System.Math.Abs(a.Y - b.Y) == 1;
```

- [ ] **Step 4: `OnBotGoalReached`-Handler ergaenzen**

Am Ende der Klasse, vor der schliessenden Klammer:

```csharp
private void OnBotGoalReached()
{
    GD.Print("[Main] Bot ist am Ziel angekommen.");
}
```

- [ ] **Step 5: Build und manueller Test**

```powershell
& $env:GODOT4 --path $PWD --build-solutions
& $env:GODOT4 --path $PWD
```

Im Editor (Play):
1. Maze 25x25, Recursive Backtracker erzeugen.
2. 3D-Ansicht aktivieren.
3. BFS-Solver starten.
4. Nach Abschluss erscheint die Capsule am Startfeld und wandert sichtbar entlang des markierten Pfades zum Zielfeld.
5. Konsolenausgabe `[Main] Bot ist am Ziel angekommen.` erscheint nach Erreichen.

> **Hinweis:** Falls die Capsule unsichtbar bleibt, ist meistens die `_view3D.CellSize`-Lookup falsch oder der `Player`-Knoten heisst anders. Pruefe `MazeView3D/Player` im Knotenpfad.

- [ ] **Step 6: Commit**

```bash
git add scripts/Main.cs
git commit -m "Task 16.2: Solver-Pfad an PlayerCharacter3D uebergeben"
```

#### Verifizierter Fix fuer Reset-/Neugenerierungs-Bug (Nachtrag)

**Problem:** Nach `Reset` oder neuer Maze-Generierung konnte der Solver-Bot alte Pfadreste verwenden. Das wirkte wie "durch Waende laufen", obwohl die neuen Wall-Daten korrekt waren.

**Ursache:** `_solverPath` und Player-Bewegungszustand wurden nicht in allen Startpunkten konsistent geloescht.

**Loesung (Code):**
- `OnSolveRequested`: `_solverPath.Clear(); _player.Hide(); _runner.StopAll();`
- `OnGenerateRequested`: `_solverPath.Clear(); _player.Hide(); _runner.StopAll();`
- `OnResetRequested`: `_runner.StopAll(); _solverPath.Clear(); _player.Hide();`
- `OnSolverFinished`: Diagnoselog mit `pathCells`/`fullWaypoints`, um inkonsistente Pfadlaengen sofort zu sehen.

**Regressionstest:**
1. Maze A erzeugen, Solver laufen lassen (Bot folgt korrekt).
2. `Reset` druecken.
3. Maze B mit anderem Generator erzeugen.
4. Solver erneut starten.
5. Erwartung: Bot bleibt auf dem neuen Solverpfad; keine alten Wegpunkte, keine Wanddurchdringung.

### Task 16.3: `CameraController3D` bekommt einen Verfolger-Modus

**Files:**
- Modify: `scripts/Views/CameraController3D.cs`

- [x] **Step 1: Felder und `[Export]`-Optionen fuer den Follow-Modus**

In `CameraController3D` direkt unter den bestehenden `[Export]`-Feldern erweitern:

```csharp
[Export] public float FollowDistance = 4.5f;     // Welt-Einheiten hinter dem Target
[Export] public float FollowHeight = 3.0f;       // Welt-Einheiten ueber dem Target
[Export] public float FollowSmoothing = 6.0f;    // hoeher = schnellere Annaeherung
```

Direkt unter den bestehenden privaten Feldern (`_yaw`, `_pitch`, `_mouseLook`) ergaenzen:

```csharp
private Node3D _followTarget;
public bool FollowMode { get; private set; }

// Orbit-Zustand fuer den Follow-Modus: sphaerische Koordinaten um das Target.
private float _followOrbitYaw;    // horizontaler Winkel (Bogen links/rechts)
private float _followOrbitPitch;  // vertikaler Winkel (Bogen rauf/runter)
private float _followOrbitRadius; // Abstand zum Target in Welt-Einheiten
```

- [x] **Step 2: Public API zum An- und Abschalten des Follow-Modus**

Direkt unter `FitToMaze` einfuegen:

```csharp
public void EnableFollow(Node3D target)
{
    _followTarget = target;
    FollowMode = true;

    // Orbit-Radius und -Winkel aus den Export-Feldern initialisieren.
    _followOrbitRadius = Mathf.Sqrt(FollowHeight * FollowHeight + FollowDistance * FollowDistance);
    _followOrbitPitch  = Mathf.Atan2(FollowHeight, FollowDistance);
    _followOrbitYaw    = 0f;

    // Maus-Look sicher abschalten, damit der Cursor nicht im Spielbereich klemmt.
    if (_mouseLook)
    {
        _mouseLook = false;
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }
}

public void DisableFollow()
{
    _followTarget = null;
    FollowMode = false;
}
```

- [x] **Step 3: `_Process`, `UpdateFollowCamera` und `_UnhandledInput` anpassen**

Ersetze `_Process` durch:

```csharp
public override void _Process(double delta)
{
    if (FollowMode && _followTarget != null)
    {
        UpdateFollowCamera(delta);
        return;
    }
    HandleMovement(delta);
    HandleKeyboardLook(delta);
    ApplyRotation();
}
```

`UpdateFollowCamera` berechnet die Kameraposition aus sphaerischen Koordinaten statt einem festen Offset — das ermoeglicht Orbit und Zoom:

```csharp
private void UpdateFollowCamera(double delta)
{
    Vector3 targetPos = _followTarget.GlobalPosition;

    // Sphaerische Koordinaten: Orbit-Position aus Radius, Yaw und Pitch berechnen.
    float cosP = Mathf.Cos(_followOrbitPitch);
    float sinP = Mathf.Sin(_followOrbitPitch);
    Vector3 orbitOffset = new Vector3(
        Mathf.Sin(_followOrbitYaw) * cosP,
        sinP,
        Mathf.Cos(_followOrbitYaw) * cosP
    ) * _followOrbitRadius;

    float lerpFactor = 1f - Mathf.Exp(-FollowSmoothing * (float)delta);
    GlobalPosition = GlobalPosition.Lerp(targetPos + orbitOffset, lerpFactor);
    LookAt(targetPos + new Vector3(0, 0.3f, 0), Vector3.Up);

    Vector3 euler = Basis.GetEuler();
    _pitch = euler.X;
    _yaw   = euler.Y;
}
```

In `_UnhandledInput` im Follow-Modus Zoom und Orbit erlauben (statt einfach `return`):

```csharp
public override void _UnhandledInput(InputEvent @event)
{
    if (FollowMode)
    {
        HandleFollowInput(@event);
        return;
    }
    // ... bisherige Free-Kamera-Eingaben ...
}

private void HandleFollowInput(InputEvent @event)
{
    if (@event is InputEventMouseButton mb)
    {
        // RMB schaltet Orbit-Look an/aus.
        if (mb.ButtonIndex == MouseButton.Right)
        {
            _mouseLook = mb.Pressed;
            Input.MouseMode = mb.Pressed ? Input.MouseModeEnum.Captured : Input.MouseModeEnum.Visible;
            return;
        }
        // Mausrad aendert den Orbit-Radius (Zoom).
        if (mb.Pressed && (mb.ButtonIndex == MouseButton.WheelUp || mb.ButtonIndex == MouseButton.WheelDown))
        {
            float step = ZoomStep;
            if (Input.IsPhysicalKeyPressed(Key.Shift)) step *= ZoomSprintMultiplier;
            _followOrbitRadius = Mathf.Clamp(
                _followOrbitRadius + (mb.ButtonIndex == MouseButton.WheelUp ? -step : step),
                1f, 200f);
        }
    }
    // RMB + Maus dreht die Kamera um das Target.
    if (@event is InputEventMouseMotion motion && _mouseLook)
    {
        _followOrbitYaw   -= motion.Relative.X * MouseSensitivity;
        _followOrbitPitch  = Mathf.Clamp(
            _followOrbitPitch - motion.Relative.Y * MouseSensitivity,
            0.05f, Mathf.Pi / 2f - 0.05f);
    }
}
```

> **Ergaenzung (nachtraeglich umgesetzt):** Im Follow-Modus stehen Zoom (Mausrad, +Shift schnell) und Orbit (RMB + Maus) zur Verfuegung. Die Steuerung ist dieselbe wie in der freien Kamera, wirkt aber auf die sphaerischen Orbit-Koordinaten statt auf den absoluten Transform.

- [x] **Step 4: Build und manueller Test (HUD-Toggle kommt in 16.4)**

```powershell
& $env:GODOT4 --path $PWD --build-solutions
dotnet build
```

Erwartet: `Build succeeded`. Verhalten zur Laufzeit ist noch unveraendert, weil noch niemand `EnableFollow` ruft.

- [x] **Step 5: Commit**

```bash
git add scripts/Views/CameraController3D.cs
git commit -m "Task 16.3: CameraController3D mit Follow-Modus (Orbit-Zoom + RMB-Rotate)"
```

#### Nachtraeglich umgesetzter Bugfix: 2D-Navigation durch CameraController3D blockiert

**Problem:** Nach Implementierung von Task 16.3 waren Mausrad-Zoom und RMB-Drag-Pan im 2D-View nicht mehr nutzbar.

**Ursache:** `CameraController3D._UnhandledInput` und `_Process` liefen auch dann, wenn `MazeView3D` auf `Visible = false` gesetzt war. `SetInputAsHandled()` konsumierte Mausrad- und RMB-Events bevor `CameraController2D` sie empfangen konnte. Ein erster Fix-Versuch mit `if (!Current)` war falsch, da `Camera3D.Current` unabhaengig von der Eltern-Sichtbarkeit `true` bleibt.

**Loesung (umgesetzt):** `IsVisibleInTree()` am Anfang beider Methoden:
```csharp
// In _Process und _UnhandledInput:
if (!IsVisibleInTree()) return;
```
`IsVisibleInTree()` traversiert rekursiv die gesamte Parent-Kette — wenn `MazeView3D.Visible = false`, gibt es zuverlaessig `false` zurueck.

**Commit:** `Fix: CameraController3D uses IsVisibleInTree() guard in _Process and _UnhandledInput`

### Task 16.4: HUD — "Verfolger-Kamera"-Checkbox

**Files:**
- Modify: `scenes/Hud.tscn` (Algos-Reihe um eine Checkbox erweitern)
- Modify: `scripts/Hud/Hud.cs` (Signal + Wiring)
- Modify: `scripts/Main.cs` (Handler ruft `EnableFollow`/`DisableFollow`)

- [ ] **Step 1: `scenes/Hud.tscn` — `FollowCamToggle` einfuegen**

In `scenes/Hud.tscn` finde den `Algos`-`HBoxContainer`-Block (Generator/Solver/View3D/Heatmap-Kinder). Direkt nach dem `HeatmapToggle`-Knoten einfuegen:

```text
[node name="FollowCamToggle" type="CheckBox" parent="Root/Margin/VBox/Algos"]
text = "Verfolger-Kamera"
```

- [ ] **Step 2: `scripts/Hud/Hud.cs` — Signal, Feld, Wiring**

Im `[Signal]`-Block oben:

```csharp
[Signal] public delegate void FollowCamToggleEventHandler(bool enabled);
```

Im Knotenreferenzen-Block:

```csharp
private CheckBox _followCamToggle = null!;
```

In `_Ready()` nach `_heatmapToggle = ...`:

```csharp
_followCamToggle = GetNode<CheckBox>("Root/Margin/VBox/Algos/FollowCamToggle");
```

Im Signal-Wiring-Block in `_Ready()`:

```csharp
_followCamToggle.Toggled += OnFollowCamToggled;
```

Am Ende der Klasse:

```csharp
private void OnFollowCamToggled(bool enabled) =>
    EmitSignal(SignalName.FollowCamToggle, enabled);
```

- [ ] **Step 3: `scripts/Main.cs` — Handler verbinden**

In `_Ready()` im Signal-Block des HUD ergaenzen:

```csharp
_hud.FollowCamToggle += OnFollowCamToggled;
```

Am Ende der Klasse:

```csharp
private void OnFollowCamToggled(bool enabled)
{
    var camera = _view3D.GetNode<CameraController3D>("Camera3D");
    if (enabled)
        camera.EnableFollow(_player);
    else
        camera.DisableFollow();
}
```

> **Hinweis:** `_view3D.GetNode<CameraController3D>("Camera3D")` liesse sich auch ueber eine Property oeffentlich machen — fuer dieses Schulprojekt reicht der direkte Lookup, weil Main bereits den Pfad kennt.

- [ ] **Step 4: Build und manueller Test**

```powershell
& $env:GODOT4 --path $PWD --build-solutions
& $env:GODOT4 --path $PWD
```

Pruefe:
1. Maze erzeugen, 3D-Ansicht aktivieren, BFS solven — Bot laeuft.
2. Waehrend der Bot laeuft, "Verfolger-Kamera" anhaken — die Kamera schwebt hinter den Bot.
3. Haken entfernen — Steuerung ist sofort wieder frei (WASD/Maus reagieren), die Kamera bleibt an der zuletzt erreichten Position.

- [ ] **Step 5: Commit**

```bash
git add scenes/Hud.tscn scripts/Hud/Hud.cs scripts/Main.cs
git commit -m "Task 16.4: HUD-Toggle fuer Verfolger-Kamera"
```

---

## Phase 17 — Selbststeuerung mit Wandkollision und Sieg-Erkennung

Ziel: Eine zweite Spielmodus-Variante, in der der User die Spielfigur per WASD selbst durch das Maze steuert. Pro Tastendruck bewegt sich die Figur eine Zelle, *falls* die Wand in der gewuenschten Richtung offen ist. Eine kurze Lerp-Animation glaettet den Wechsel zwischen Zellen. Beim Erreichen der Goal-Zelle erscheint ein Sieg-Label mit der gemessenen Zeit.

**Didaktischer Punkt:** Die Schueler erleben den Suchraum nicht mehr aus der Vogelperspektive, sondern lokal. Wer einmal selbst ein 50x50-Maze "blind" durchquert, versteht intuitiv, warum BFS so viele Zellen markieren *muss*, um sicher den kuerzesten Weg zu kennen.

### Task 17.1: `PlayerCharacter3D` um den Manual-Modus erweitern

**Files:**
- Modify: `scripts/Views/PlayerCharacter3D.cs`

- [ ] **Step 1: Mode-Enum, Felder, Cell-Lerp-State**

In `PlayerCharacter3D` direkt unter den `[Export]`-Feldern ergaenzen:

```csharp
public enum Mode
{
    Idle,
    FollowingPath,
    Manual
}

public Mode CurrentMode { get; private set; } = Mode.Idle;

private Model.Maze _manualMaze;
private Cell _manualCell;
private Cell _manualGoal;

// Cell-Lerp-Zustand: nur eine Zelle pro Tastendruck.
private bool _isAnimatingCell;
private Vector3 _animFrom;
private Vector3 _animTo;
private float _animElapsed;
private float _animDuration;
```

- [ ] **Step 2: `EnableManualMode` und `DisableManualMode`**

Direkt unter `StartFollowingPath` ergaenzen:

```csharp
/// <summary>
/// Aktiviert den Manual-Modus. Die Figur springt an die Startzelle und reagiert
/// ab sofort auf WASD-Eingaben in <see cref="_Process"/>. <paramref name="goal"/>
/// wird beim Erreichen mit dem GoalReached-Signal quittiert.
/// </summary>
public void EnableManualMode(Model.Maze maze, Cell start, Cell goal, float cellSize)
{
    _cellSize = cellSize;
    _manualMaze = maze;
    _manualCell = start;
    _manualGoal = goal;
    _isAnimatingCell = false;
    _waypoints.Clear();
    _isMoving = false;

    Position = CellToWorld(start);
    Visible = true;
    CurrentMode = Mode.Manual;
}

public void DisableManualMode()
{
    _manualMaze = null;
    _manualCell = null;
    _manualGoal = null;
    _isAnimatingCell = false;
    Visible = false;
    CurrentMode = Mode.Idle;
}
```

> **Hinweis:** `StartFollowingPath` muss kompatibel bleiben — ergaenze in dessen erstem Block `CurrentMode = Mode.FollowingPath;` direkt vor `if (_waypoints.Count == 0)`. Setze `Mode.Idle` in einem neuen Zweig direkt nach `_isMoving = false; EmitSignal(...);` am Ende von `_Process`.

- [ ] **Step 3: `_Process` um den Manual-Pfad erweitern**

Ersetze `_Process` durch:

```csharp
public override void _Process(double delta)
{
    switch (CurrentMode)
    {
        case Mode.FollowingPath:
            ProcessFollowPath(delta);
            break;
        case Mode.Manual:
            ProcessManual(delta);
            break;
    }
}

private void ProcessFollowPath(double delta)
{
    if (!_isMoving) return;

    Vector3 target = _waypoints[_currentIndex];
    Vector3 toTarget = target - Position;
    float remaining = toTarget.Length();
    float step = MoveSpeed * _cellSize * (float)delta;

    if (step >= remaining)
    {
        Position = target;
        _currentIndex++;
        if (_currentIndex >= _waypoints.Count)
        {
            _isMoving = false;
            CurrentMode = Mode.Idle;
            EmitSignal(SignalName.GoalReached);
        }
    }
    else
    {
        Position += toTarget.Normalized() * step;
    }
}

private void ProcessManual(double delta)
{
    if (_isAnimatingCell)
    {
        _animElapsed += (float)delta;
        float t = Mathf.Clamp(_animElapsed / _animDuration, 0f, 1f);
        Position = _animFrom.Lerp(_animTo, t);

        if (t >= 1f)
        {
            _isAnimatingCell = false;
            Position = _animTo;
            if (_manualCell == _manualGoal)
                EmitSignal(SignalName.GoalReached);
        }
        return;
    }

    // Eingabe einlesen. Vorrang: oben (W) > unten (S) > links (A) > rechts (D).
    // Damit gibt's bei zwei gleichzeitig gedrueckten Tasten ein deterministisches Verhalten.
    Direction? dir = null;
    if (Input.IsPhysicalKeyPressed(Key.W)) dir = Direction.North;
    else if (Input.IsPhysicalKeyPressed(Key.S)) dir = Direction.South;
    else if (Input.IsPhysicalKeyPressed(Key.A)) dir = Direction.West;
    else if (Input.IsPhysicalKeyPressed(Key.D)) dir = Direction.East;

    if (dir is null) return;

    if (_manualCell.HasWall(dir.Value))
        return; // Wand blockiert - Eingabe ignorieren.

    Cell next = _manualMaze.GetNeighbor(_manualCell, dir.Value);
    if (next == null) return;

    // Animation starten. Dauer = 1 / MoveSpeed (Sekunden pro Zelle).
    _animFrom = Position;
    _animTo = CellToWorld(next);
    _animElapsed = 0f;
    _animDuration = 1f / Mathf.Max(0.5f, MoveSpeed);
    _isAnimatingCell = true;
    _manualCell = next;
}
```

- [ ] **Step 4: Build pruefen**

```powershell
& $env:GODOT4 --path $PWD --build-solutions
dotnet build
```

Erwartet: `Build succeeded`. Verhalten zur Laufzeit ist unveraendert, weil noch niemand `EnableManualMode` ruft.

- [ ] **Step 5: Commit**

```bash
git add scripts/Views/PlayerCharacter3D.cs
git commit -m "Task 17.1: PlayerCharacter3D mit Manual-Modus und Wandkollision"
```

### Task 17.2: `Main` startet/stoppt den Manual-Modus, behandelt `GoalReached`

**Files:**
- Modify: `scripts/Main.cs`

- [ ] **Step 1: Felder fuer Manual-Modus-Zeitmessung**

In `Main` ergaenzen (oberhalb von `_Ready`):

```csharp
private bool _isManualMode;
private double _manualStartTimeSeconds;
```

> **Hinweis:** `Time.GetTicksMsec()` liefert ms seit Programmstart und reicht fuer die Spielzeit. Wir nutzen `Godot.Time` weiter unten.

- [ ] **Step 2: `OnPlayManualRequested` und `OnStopManualRequested` einfuehren**

Am Ende der Klasse:

```csharp
private void OnPlayManualRequested()
{
    if (_currentMaze == null)
    {
        GD.PrintErr("[Main] Kein Maze - bitte erst Erstellen.");
        return;
    }

    // Solver-Bot anhalten und Pfad-Markierung visuell zuruecksetzen,
    // damit der Spieler nicht entlang einer Loesungsspur gefuehrt wird.
    _runner.StopAll();
    _currentMaze.ResetSolverState();
    _solverStart = _currentMaze.GetCell(0, 0);
    _solverGoal = _currentMaze.GetCell(_currentMaze.Width - 1, _currentMaze.Height - 1);
    _solverStart.State = CellState.Start;
    _solverGoal.State = CellState.Goal;
    _view2D.ForceRefresh();
    if (_view3D.Visible)
        _view3D.SetMaze(_currentMaze);

    _player.EnableManualMode(_currentMaze, _solverStart, _solverGoal, _view3D.CellSize);
    _isManualMode = true;
    _manualStartTimeSeconds = Time.GetTicksMsec() / 1000.0;

    // Verfolger-Kamera ist im Manual-Modus zwingend an, sonst sieht der Spieler nichts.
    var camera = _view3D.GetNode<CameraController3D>("Camera3D");
    camera.EnableFollow(_player);

    GD.Print("[Main] Selbst spielen aktiviert.");
}

private void OnStopManualRequested()
{
    if (!_isManualMode) return;
    _player.DisableManualMode();
    _isManualMode = false;
    var camera = _view3D.GetNode<CameraController3D>("Camera3D");
    camera.DisableFollow();
    GD.Print("[Main] Selbst spielen beendet.");
}
```

- [ ] **Step 3: `OnBotGoalReached` umbauen, sodass es Manual- und Bot-Fall trennt**

Ersetze den Body von `OnBotGoalReached`:

```csharp
private void OnBotGoalReached()
{
    if (_isManualMode)
    {
        double elapsed = Time.GetTicksMsec() / 1000.0 - _manualStartTimeSeconds;
        _hud.ShowVictory(elapsed);
        OnStopManualRequested();
        return;
    }

    GD.Print("[Main] Bot ist am Ziel angekommen.");
}
```

> **Hinweis:** `_hud.ShowVictory` kommt in 17.3.

- [ ] **Step 4: Build pruefen (HUD-Bindung folgt in 17.3, deshalb Build noch erwartbar moeglich)**

```powershell
dotnet build
```

Erwartet: Es schlaegt mit `'Hud' does not contain a definition for 'ShowVictory'` fehl — das ist okay, Task 17.3 schliesst die Luecke.

- [ ] **Step 5: Commit (auch wenn build noch rot ist - Plan-Zeile)**

```bash
git add scripts/Main.cs
git commit -m "Task 17.2: Manual-Mode-Handler in Main (Build wird in 17.3 geschlossen)"
```

### Task 17.3: HUD — "Selbst spielen"-Knopf, Sieg-Label

**Files:**
- Modify: `scenes/Hud.tscn` (zwei neue Knoten)
- Modify: `scripts/Hud/Hud.cs`

- [ ] **Step 1: `scenes/Hud.tscn` — `PlayManualButton` und `VictoryLabel`**

Im `Buttons`-`HBoxContainer` (Generate/Solve/Pause/Step/Reset) direkt nach `ResetButton`:

```text
[node name="PlayManualButton" type="Button" parent="Root/Margin/VBox/Buttons"]
text = "Selbst spielen"
toggle_mode = true
```

Direkt am Ende der `VBox` (nach allen anderen Reihen) einen Sieg-Label-Knoten:

```text
[node name="VictoryLabel" type="Label" parent="Root/Margin/VBox"]
text = ""
modulate = Color(1, 0.85, 0.2, 1)
```

- [ ] **Step 2: `scripts/Hud/Hud.cs` — Signale, Felder, Wiring, Methode**

Im `[Signal]`-Block:

```csharp
[Signal] public delegate void PlayManualToggleEventHandler(bool active);
```

Im Knotenreferenzen-Block:

```csharp
private Button _playManualButton = null!;
private Label _victoryLabel = null!;
```

In `_Ready()`:

```csharp
_playManualButton = GetNode<Button>("Root/Margin/VBox/Buttons/PlayManualButton");
_victoryLabel = GetNode<Label>("Root/Margin/VBox/VictoryLabel");
_playManualButton.Toggled += OnPlayManualToggled;
```

Am Ende der Klasse:

```csharp
private void OnPlayManualToggled(bool active)
{
    _victoryLabel.Text = ""; // alte Sieg-Anzeige verbergen, sobald neu gestartet wird
    EmitSignal(SignalName.PlayManualToggle, active);
}

public void ShowVictory(double seconds)
{
    _victoryLabel.Text = $"Geschafft in {seconds:0.00} s!";
    // Den Button automatisch zurueckstellen, damit man direkt einen neuen Run starten kann.
    _playManualButton.SetPressedNoSignal(false);
}
```

- [ ] **Step 3: `scripts/Main.cs` — Signal verkabeln**

In `_Ready()`:

```csharp
_hud.PlayManualToggle += OnPlayManualToggle;
```

Am Ende der Klasse:

```csharp
private void OnPlayManualToggle(bool active)
{
    if (active) OnPlayManualRequested();
    else OnStopManualRequested();
}
```

- [ ] **Step 4: Build und manueller Test**

```powershell
& $env:GODOT4 --path $PWD --build-solutions
& $env:GODOT4 --path $PWD
```

Pruefe:
1. Maze 25x25 erzeugen, 3D-Ansicht aktivieren.
2. "Selbst spielen" druecken → Capsule erscheint am Startfeld, Kamera folgt.
3. WASD bewegt die Figur eine Zelle pro Druck (gedrueckt halten ergibt eine fluessige Reihe).
4. Vor einer Wand bleibt die Figur stehen, kein Durchgang.
5. Goal-Zelle erreichen → Label `Geschafft in X.XX s!` oben/unten erscheint.
6. Erneut "Selbst spielen" druecken → Sieg-Label verschwindet, neuer Run startet am Startfeld.

- [ ] **Step 5: Commit**

```bash
git add scenes/Hud.tscn scripts/Hud/Hud.cs scripts/Main.cs
git commit -m "Task 17.3: Selbst-spielen-Button, Sieg-Label, Zeitmessung"
```

### Task 17.4: Eingabe-Konflikt entschaerfen — Free-Kamera-Tasten im Manual-Modus deaktivieren

**Files:**
- Modify: `scripts/Views/CameraController3D.cs`

> **Hintergrund:** Im Manual-Modus haengt der `CameraController3D` zwar im Follow-Modus, aber die *frueheren* Eingaben (WASD/QE/Pfeiltasten) werden in `_Process` ausgewertet, weil `_Process` im Follow-Zweig zwar fruehzeitig zurueckkehrt, aber `_UnhandledInput` (Mausrad-Dolly) noch reagieren kann. Wir machen die Sperre an einer einzigen, deutlichen Stelle fest, damit Schueler verstehen, was passiert.

- [ ] **Step 1: Frueh-Returns sind bereits gesetzt — pruefen**

Lies `scripts/Views/CameraController3D.cs` und verifiziere:
- `public override void _Process(double delta)` beginnt mit `if (FollowMode && _followTarget != null) { UpdateFollowCamera(delta); return; }` (aus 16.3).
- `public override void _UnhandledInput(InputEvent @event)` beginnt mit `if (FollowMode) return;` (aus 16.3).

Wenn beide Stellen vorhanden sind, gibt es nichts zu aendern — der Schritt ist erfuellt.

- [ ] **Step 2: Vergewissere dich, dass im Manual-Modus tatsaechlich `EnableFollow` aktiv ist**

In `OnPlayManualRequested` (Main.cs aus 17.2) wird `camera.EnableFollow(_player)` gerufen. Damit ist `FollowMode == true`. Beide Frueh-Returns greifen — die WASD-Tastendruecke werden vom `CameraController3D` ignoriert und ausschliesslich von `PlayerCharacter3D.ProcessManual` ausgewertet.

- [ ] **Step 3: Manueller Test (Schluss-Smoke)**

```powershell
& $env:GODOT4 --path $PWD
```

1. Im Manual-Modus W druecken — Figur bewegt sich eine Zelle.
2. W gedrueckt halten — Figur bewegt sich Reihe nach Reihe, Kamera folgt; *die Kamera selbst* bewegt sich aber nicht zusaetzlich (kein doppelter Vorwaertsschub).
3. Mausrad drehen — keine Wirkung (Dolly nicht im Follow-Modus, das ist gewollt).

- [ ] **Step 4: Commit (Doku-Commit, falls keine Code-Aenderung noetig)**

```bash
git commit --allow-empty -m "Task 17.4: Eingabe-Konflikt-Doku - Frueh-Returns aus 16.3 sind ausreichend"
```

> **Hinweis:** Falls beim Test ein Problem auftaucht (z. B. weil ein Schueler die Frueh-Returns versehentlich entfernt hat), ist hier der Ort, sie wiederherzustellen. Im Plan-Standardfall ist 17.4 ein Verifikations-Task.

---

## Phase 18 — Entdeckungs-Modus (Lichtkreis um die Spielfigur)

Ziel: Im Selbstspiel-Modus optional die Sicht auf einen Lichtkreis um die Figur reduzieren. DirectionalLight (Tageslicht) wird gedimmt, ein OmniLight an der Spielfigur leuchtet die nahe Umgebung aus, distance-Fog laesst entfernte Waende im Dunkeln verschwinden. Die Wuerfe-Sicht aus der Vogelperspektive (3D-Free-Kamera, 2D-View) bleibt unveraendert — der Modus betrifft nur die *Beleuchtung* der 3D-Szene.

**Didaktischer Punkt:** Schueler erleben den Suchraum so, wie ein realer Agent ihn erleben wuerde — lokal. Algorithmen brauchen die Allwissenheit, die das volle Cell-Array bietet, um effizient zu sein. Solange der Mensch nur einen Lichtkreis sieht, ist *jede* gute Strategie schwer.

### Task 18.1: `OmniLight3D` als Kind der Spielfigur

**Files:**
- Modify: `scenes/MazeView3D.tscn` (Light-Knoten unterhalb von `Player`)

- [ ] **Step 1: `scenes/MazeView3D.tscn` — `PlayerLight` einfuegen**

In `scenes/MazeView3D.tscn` finde den `Player`-Knoten (aus 16.1). Direkt nach dem `Mesh`-Kindknoten innerhalb von `Player` einfuegen:

```text
[node name="PlayerLight" type="OmniLight3D" parent="Player"]
visible = false
omni_range = 6.5
light_energy = 1.6
light_color = Color(1, 0.92, 0.78, 1)
```

> **Hinweis:** `omni_range = 6.5` Welt-Einheiten ergibt bei `CellSize = 1` einen ~6-Zellen-Radius. Das ist gross genug, um Wegekreuzungen zu erkennen, aber klein genug, um Fernziele dunkel zu halten. `visible = false` ist Default, der Modus-Toggle aus 18.3 schaltet ihn ein.

- [ ] **Step 2: Build pruefen**

```powershell
& $env:GODOT4 --path $PWD --build-solutions
```

Erwartet: `Build succeeded`, keine Verhaltensaenderung.

- [ ] **Step 3: Commit**

```bash
git add scenes/MazeView3D.tscn
git commit -m "Task 18.1: OmniLight als Kind der Spielfigur"
```

### Task 18.2: `WorldEnvironment` mit dimmbarem Tageslicht und Fog

**Files:**
- Modify: `scenes/MazeView3D.tscn` (DirectionalLight + WorldEnvironment, falls nicht vorhanden)
- Modify: `scripts/Views/MazeView3D.cs` (Knoten-Lookups, `SetExploreMode`-Methode)

- [ ] **Step 1: `scenes/MazeView3D.tscn` — `Sun` und `WorldEnvironment` hinzufuegen, falls noch nicht vorhanden**

Pruefe, ob `MazeView3D` bereits einen `DirectionalLight3D`- und einen `WorldEnvironment`-Knoten hat. Falls *einer* fehlt, ergaenze die fehlenden — falls *beide* fehlen, fuege sie als direkte Kinder von `MazeView3D` ein:

Header-Sub-Resources (oberhalb der `[node]`-Bloecke):

```text
[sub_resource type="Environment" id="Env_explore"]
background_mode = 1
background_color = Color(0.02, 0.02, 0.04, 1)
ambient_light_source = 2
ambient_light_color = Color(0.4, 0.45, 0.55, 1)
ambient_light_energy = 0.4
fog_enabled = false
fog_light_color = Color(0.05, 0.05, 0.08, 1)
fog_density = 0.06
```

Knoten-Bloecke (am Ende der Datei):

```text
[node name="Sun" type="DirectionalLight3D" parent="."]
transform = Transform3D(0.866, -0.354, 0.354, 0, 0.707, 0.707, -0.5, -0.612, 0.612, 0, 4, 0)
light_energy = 1.0

[node name="WorldEnvironment" type="WorldEnvironment" parent="."]
environment = SubResource("Env_explore")
```

> **Hinweis:** `Env_explore`-Werte sind so gewaehlt, dass sie als *Tageslicht* funktionieren, wenn `fog_enabled = false` (default) und das Sun-Light voll an ist. Der Mode-Toggle aus 18.3 schaltet diese Felder per Code um. Der `background_color` ist bewusst sehr dunkel — bei aktiviertem Fog sieht der Hintergrund dann konsistent schwarz aus.

- [ ] **Step 2: `scripts/Views/MazeView3D.cs` — Light-Knoten aufloesen, `SetExploreMode`**

In `MazeView3D` ergaenze die Felder direkt unter den bestehenden `null!`-Feldern:

```csharp
private DirectionalLight3D _sun = null!;
private OmniLight3D _playerLight = null!;
private WorldEnvironment _worldEnv = null!;
```

In `_Ready()` direkt nach `_camera = ...`:

```csharp
_sun = GetNode<DirectionalLight3D>("Sun");
_playerLight = GetNode<OmniLight3D>("Player/PlayerLight");
_worldEnv = GetNode<WorldEnvironment>("WorldEnvironment");
```

Am Ende der Klasse:

```csharp
/// <summary>
/// Schaltet zwischen Tageslicht und Entdeckungs-Modus um.
/// Tageslicht: Sun = 1.0, AmbientLight = 0.4, Fog aus, OmniLight aus.
/// Entdeckungs-Modus: Sun = 0.05, AmbientLight = 0.05, Fog an, OmniLight an.
/// </summary>
public void SetExploreMode(bool enabled)
{
    var env = _worldEnv.Environment;

    if (enabled)
    {
        _sun.LightEnergy = 0.05f;
        env.AmbientLightEnergy = 0.05f;
        env.FogEnabled = true;
        _playerLight.Visible = true;
    }
    else
    {
        _sun.LightEnergy = 1.0f;
        env.AmbientLightEnergy = 0.4f;
        env.FogEnabled = false;
        _playerLight.Visible = false;
    }
}
```

> **Hinweis fuer Schueler:** Die `Environment`-Ressource ist *gesharet* (alle MazeView3D-Instanzen wuerden sich denselben Zustand teilen, falls man je mehrere haette). Fuer ein Schulprojekt mit genau einem 3D-View ist das egal. Wenn euch das stoert, koennt ihr in `_Ready()` mit `_worldEnv.Environment = (Godot.Environment)_worldEnv.Environment.Duplicate();` eine eigene Kopie ziehen.

- [ ] **Step 3: Build pruefen**

```powershell
& $env:GODOT4 --path $PWD --build-solutions
dotnet build
```

Erwartet: `Build succeeded`.

- [ ] **Step 4: Commit**

```bash
git add scenes/MazeView3D.tscn scripts/Views/MazeView3D.cs
git commit -m "Task 18.2: WorldEnvironment und SetExploreMode"
```

### Task 18.3: HUD — "Entdeckungs-Modus"-Checkbox

**Files:**
- Modify: `scenes/Hud.tscn`
- Modify: `scripts/Hud/Hud.cs`
- Modify: `scripts/Main.cs`

- [ ] **Step 1: `scenes/Hud.tscn` — Toggle einfuegen**

Im `Algos`-`HBoxContainer` (View3D / Heatmap / FollowCam) direkt nach `FollowCamToggle`:

```text
[node name="ExploreModeToggle" type="CheckBox" parent="Root/Margin/VBox/Algos"]
text = "Entdeckungs-Modus"
```

- [ ] **Step 2: `scripts/Hud/Hud.cs` — Signal, Feld, Wiring**

Im `[Signal]`-Block:

```csharp
[Signal] public delegate void ExploreModeToggleEventHandler(bool enabled);
```

Im Knotenreferenzen-Block:

```csharp
private CheckBox _exploreModeToggle = null!;
```

In `_Ready()`:

```csharp
_exploreModeToggle = GetNode<CheckBox>("Root/Margin/VBox/Algos/ExploreModeToggle");
_exploreModeToggle.Toggled += enabled => EmitSignal(SignalName.ExploreModeToggle, enabled);
```

- [ ] **Step 3: `scripts/Main.cs` — Handler**

In `_Ready()`:

```csharp
_hud.ExploreModeToggle += OnExploreModeToggled;
```

Am Ende der Klasse:

```csharp
private void OnExploreModeToggled(bool enabled)
{
    _view3D.SetExploreMode(enabled);
}
```

- [ ] **Step 4: Build und manueller Test**

```powershell
& $env:GODOT4 --path $PWD --build-solutions
& $env:GODOT4 --path $PWD
```

Pruefe:
1. Maze 25x25 erzeugen, 3D-Ansicht aktivieren, "Selbst spielen" starten.
2. "Entdeckungs-Modus" anhaken — die Szene wird dunkel, nur ein Lichtkreis um die Figur ist beleuchtet, ferne Waende verschwinden im Fog.
3. Haken entfernen — Tageslicht kehrt zurueck.
4. Mehrmals umschalten ist erlaubt; im Free-Kamera-Modus (ohne aktiven Selbst-Spielen-Run) wirkt der Modus genauso (die Figur ist dann unsichtbar; das OmniLight bleibt am Player-Knoten an dessen letzter Position).

- [ ] **Step 5: Commit**

```bash
git add scenes/Hud.tscn scripts/Hud/Hud.cs scripts/Main.cs
git commit -m "Task 18.3: HUD-Toggle fuer Entdeckungs-Modus"
```

### Task 18.4: Smoother Uebergang zwischen Tageslicht und Lichtkreis

**Files:**
- Modify: `scripts/Views/MazeView3D.cs`

> **Hintergrund:** Der harte Schalter aus 18.2 wirkt etwas abrupt — fuer das Schulprojekt ist eine 0.6-sekuendige Lerp-Blende deutlich angenehmer und zeigt den Schuelern, wie man Property-Animationen ohne `Tween`-Knoten in `_Process` umsetzt.

- [ ] **Step 1: Mode-Felder und `_Process`-Erweiterung**

In `MazeView3D` ergaenze direkt unter den Light-Feldern:

```csharp
private bool _exploreTarget;
private float _exploreFactor; // 0 = Tageslicht, 1 = volle Entdeckungs-Modus-Mischung
private const float ExploreLerpSpeed = 1.6f; // ~0.6 s fuer 0->1
```

Ersetze `SetExploreMode` durch eine reine Wunsch-Setter-Methode:

```csharp
public void SetExploreMode(bool enabled) => _exploreTarget = enabled;
```

Ergaenze `_Process` (oder lege sie an, falls sie noch nicht existiert):

```csharp
public override void _Process(double delta)
{
    float target = _exploreTarget ? 1f : 0f;
    if (Mathf.IsEqualApprox(_exploreFactor, target)) return;

    float lerpStep = ExploreLerpSpeed * (float)delta;
    _exploreFactor = Mathf.MoveToward(_exploreFactor, target, lerpStep);
    ApplyExploreFactor(_exploreFactor);
}

private void ApplyExploreFactor(float factor)
{
    // factor = 0 -> Tageslicht, factor = 1 -> Entdeckungs-Modus
    var env = _worldEnv.Environment;
    _sun.LightEnergy = Mathf.Lerp(1.0f, 0.05f, factor);
    env.AmbientLightEnergy = Mathf.Lerp(0.4f, 0.05f, factor);
    _playerLight.LightEnergy = Mathf.Lerp(0f, 1.6f, factor);
    _playerLight.Visible = factor > 0.01f;

    // Fog kann nicht "halb-an" gerendert werden; deshalb ueber die Density blenden.
    env.FogEnabled = factor > 0.01f;
    env.FogDensity = Mathf.Lerp(0f, 0.06f, factor);
}
```

> **Hinweis:** `Mathf.MoveToward` ist hier *richtiger* als ein klassischer Exp-Lerp, weil es deterministisch in fester Zeit auf 0 oder 1 trifft — das macht das Verhalten beim wiederholten Toggling sauber.

- [ ] **Step 2: Initialzustand absichern**

In `_Ready()` direkt nach den Light-Knoten-Lookups:

```csharp
ApplyExploreFactor(0f);
```

Damit liegt die Szene immer im Tageslicht-Zustand, wenn der Spieler die Szene zum ersten Mal sieht — auch wenn die `Environment`-Ressource im Editor anders eingestellt wurde.

- [ ] **Step 3: Build und manueller Test**

```powershell
& $env:GODOT4 --path $PWD --build-solutions
& $env:GODOT4 --path $PWD
```

Pruefe:
1. "Entdeckungs-Modus" anhaken — die Szene blendet ueber ~0.6 s zu dunkel-mit-Lichtkreis.
2. Haken entfernen — die Szene blendet ueber ~0.6 s zurueck zu Tageslicht.
3. Mehrfach schnell hintereinander toggeln — keine Sprung-Artefakte, der Lerp-Faktor bewegt sich linear in beide Richtungen.

- [ ] **Step 4: Commit**

```bash
git add scripts/Views/MazeView3D.cs
git commit -m "Task 18.4: Sanfter Lerp-Uebergang zwischen Tageslicht und Lichtkreis"
```

---

## Phase 19 — Lego-Spielfigur statt Capsule (animierter Quader-Charakter)

Ziel: Den `CapsuleMesh`-Platzhalter aus Phase 16.1 durch eine animierte Lego-/Minecraft-aehnliche Figur ersetzen, die aus sechs getexturierten Quadern (Kopf, Koerper, 2 Arme, 2 Beine) zusammengebaut wird. Beim Laufen schwingen Arme und Beine sinusfoermig, der Kopf bewegt sich leicht. Die Figur erbt die Umgebungsbeleuchtung aus Phase 18 (DirectionalLight + OmniLight) automatisch.

**Didaktischer Punkt:** Schueler sehen, dass eine "3D-Figur" in Godot nicht zwingend eine importierte Datei (FBX/GLTF) sein muss — sie kann komplett aus Code entstehen. Die Knoten-Hierarchie aus Pivot-`Node3D`s zeigt, wie man Skelett-Animationen "von Hand" macht: jeder Gelenkpunkt ist ein eigener Knoten, dessen Rotation sich pro Frame aendert.

**Autarkie-Hinweis:** Alle fuer Phase 19 benoetigten Daten (Geometrie, UV-Aufteilung, Pivot-Struktur, Animationsformeln) sind in diesem Abschnitt dokumentiert. Es ist kein Nachschlagen in einem zweiten Projekt erforderlich.

> **Textur fuer Phase 19:** Wir verwenden ausschliesslich `assets/devedse.png` aus diesem Repository (64x32 Pixel, Minecraft-Skin-Layout). Die UV-Rechtecke in Task 19.3 sind auf genau dieses Layout abgestimmt.

**Strategie:** Wir bauen jeden Quader als `ArrayMesh` mit 24 Vertices (4 pro Seite, jede Seite eigene UV-Koordinaten) und 36 Indices. Jedes Koerperteil sitzt unter einem Pivot-`Node3D`, dessen `Position` so gewaehlt ist, dass dort das Gelenk liegt — `Rotation` am Pivot = Animation am Gelenk. Die Hierarchie bildet die in diesem Plan definierte Gelenk-Logik direkt ab.

### Task 19.1: Texture-Asset uebernehmen

**Files:**
- Add/Track: `assets/devedse.png` (lokales Projekt-Asset)
- Modify: `project.godot` (optional — Filter auf Nearest fuer crispe Pixel-Optik)

- [ ] **Step 1: Asset-Verzeichnis und Datei-Pruefung**

```powershell
New-Item -ItemType Directory -Force -Path assets | Out-Null
if (-not (Test-Path "assets\devedse.png")) {
    throw "assets/devedse.png fehlt im Repository. Bitte Datei aus dem Kursmaterial in dieses Projekt legen."
}
```

> **Hinweis:** `devedse.png` muss 64x32 Pixel haben. Die UV-Koordinaten aus Task 19.3 sind auf diese Groesse normiert ($u=x/64$, $v=y/32$).

- [ ] **Step 2: Godot importiert die Textur automatisch beim naechsten Start. Filter auf Nearest setzen**

Beim ersten Editor-Start findet Godot die neue Datei und legt `assets/devedse.png.import` an. Im Editor: Inspector-Panel der `devedse.png`-Datei oeffnen → `Filter` auf `Nearest` setzen → `Reimport`. Damit bleiben die Pixel scharf, die Figur sieht aus wie aus Klotzbausteinen.

Alternativ direkt per Editor-Skript oder durch manuelle Anpassung der `.import`-Datei — fuer das Schulprojekt reicht der Editor-Klick.

- [ ] **Step 3: Build pruefen**

```powershell
& $env:GODOT4 --path $PWD --build-solutions
```

Erwartet: `Build succeeded`. Im FileSystem-Panel des Editors taucht `assets/devedse.png` auf.

- [ ] **Step 4: Commit**

```bash
git add assets/devedse.png assets/devedse.png.import
git commit -m "Task 19.1: devedse.png als Texture-Asset uebernommen"
```

### Task 19.2: `TexturedCuboid`-Helper — getexturierter Quader mit per-face UVs

**Files:**
- Create: `scripts/Views/TexturedCuboid.cs`

- [ ] **Step 1: `scripts/Views/TexturedCuboid.cs` anlegen**

```csharp
using Godot;
using GodotArray = Godot.Collections.Array;

namespace Maze.Views;

/// <summary>
/// Baut ein ArrayMesh fuer einen getexturierten Quader mit pro Seite eigenen UV-Koordinaten.
/// Alle benoetigten UV-Konventionen stehen in diesem Plan (Phase 19).
///
/// Jede Seite hat 4 Vertices in der Reihenfolge:
///   First (top-left), Second (top-right), Third (bottom-left), Fourth (bottom-right)
/// und 2 Dreiecke (0,1,2)+(1,3,2). Insgesamt: 24 Vertices, 36 Indices, 12 Triangles.
/// </summary>
public static class TexturedCuboid
{
    /// <summary>
    /// Beschreibt ein UV-Rechteck einer Seite in Pixel-Koordinaten der Atlas-Textur.
    /// Negative Width/Height kehrt die UV-Achse um (= horizontaler / vertikaler Flip),
    /// das wird fuer den Spiegel-Look des rechten Arms genutzt.
    /// </summary>
    public readonly record struct UvRect(int X, int Y, int Width, int Height);

    public readonly record struct FaceUvs(
        UvRect Front, UvRect Right, UvRect Rear, UvRect Left, UvRect Top, UvRect Bottom);

    private const float AtlasWidth = 64f;
    private const float AtlasHeight = 32f;

    public static ArrayMesh Build(float width, float height, float depth, FaceUvs uvs)
    {
        var verts = new Vector3[24];
        var norms = new Vector3[24];
        var uv = new Vector2[24];
        var idx = new int[36];

        // Front (+Z)
        SetFace(verts, norms, uv, 0, new Vector3(0, 0, 1),
            new Vector3(0, height, depth), new Vector3(width, height, depth),
            new Vector3(0, 0, depth),       new Vector3(width, 0, depth),
            uvs.Front);

        // Right (+X)
        SetFace(verts, norms, uv, 4, new Vector3(1, 0, 0),
            new Vector3(width, height, depth), new Vector3(width, height, 0),
            new Vector3(width, 0, depth),       new Vector3(width, 0, 0),
            uvs.Right);

        // Rear (-Z)
        SetFace(verts, norms, uv, 8, new Vector3(0, 0, -1),
            new Vector3(width, height, 0), new Vector3(0, height, 0),
            new Vector3(width, 0, 0),       new Vector3(0, 0, 0),
            uvs.Rear);

        // Left (-X)
        SetFace(verts, norms, uv, 12, new Vector3(-1, 0, 0),
            new Vector3(0, height, 0), new Vector3(0, height, depth),
            new Vector3(0, 0, 0),       new Vector3(0, 0, depth),
            uvs.Left);

        // Top (+Y)
        SetFace(verts, norms, uv, 16, new Vector3(0, 1, 0),
            new Vector3(0, height, 0),     new Vector3(width, height, 0),
            new Vector3(0, height, depth), new Vector3(width, height, depth),
            uvs.Top);

        // Bottom (-Y)
        SetFace(verts, norms, uv, 20, new Vector3(0, -1, 0),
            new Vector3(0, 0, depth),     new Vector3(width, 0, depth),
            new Vector3(0, 0, 0),         new Vector3(width, 0, 0),
            uvs.Bottom);

        // Indices: 6 Seiten * 6 Indices = 36
        int cur = 0;
        for (int i = 0; i < 24; i += 4)
        {
            idx[cur++] = 0 + i;
            idx[cur++] = 1 + i;
            idx[cur++] = 2 + i;
            idx[cur++] = 1 + i;
            idx[cur++] = 3 + i;
            idx[cur++] = 2 + i;
        }

        var arrays = new GodotArray();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = verts;
        arrays[(int)Mesh.ArrayType.Normal] = norms;
        arrays[(int)Mesh.ArrayType.TexUV] = uv;
        arrays[(int)Mesh.ArrayType.Index] = idx;

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }

    private static void SetFace(Vector3[] verts, Vector3[] norms, Vector2[] uv, int offset,
        Vector3 normal, Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3, UvRect rect)
    {
        verts[offset + 0] = v0;
        verts[offset + 1] = v1;
        verts[offset + 2] = v2;
        verts[offset + 3] = v3;

        norms[offset + 0] = normal;
        norms[offset + 1] = normal;
        norms[offset + 2] = normal;
        norms[offset + 3] = normal;

        // Negative Breite/Hoehe in UvRect = Spiegel-Flag (siehe ArmRight).
        float u0 = rect.X / AtlasWidth;
        float v0u = rect.Y / AtlasHeight;
        float u1 = (rect.X + rect.Width) / AtlasWidth;
        float v1u = (rect.Y + rect.Height) / AtlasHeight;

        uv[offset + 0] = new Vector2(u0, v0u);  // First (top-left)
        uv[offset + 1] = new Vector2(u1, v0u);  // Second (top-right)
        uv[offset + 2] = new Vector2(u0, v1u);  // Third (bottom-left)
        uv[offset + 3] = new Vector2(u1, v1u);  // Fourth (bottom-right)
    }
}
```

> **Hinweis fuer Schueler:** Eine `ArrayMesh` ist eine "rohe" Mesh-Form, bei der wir Vertex-Positionen, Normalen und UV-Koordinaten direkt schreiben. Die `Mesh.ArrayType`-Indizes sind Godots Konvention, in welcher Slot welche Daten liegen. Wer das einmal verstanden hat, kann *jede* prozedurale Geometrie in Godot bauen.

- [ ] **Step 2: Build pruefen**

```powershell
& $env:GODOT4 --path $PWD --build-solutions
dotnet build
```

Erwartet: `Build succeeded`.

- [ ] **Step 3: Commit**

```bash
git add scripts/Views/TexturedCuboid.cs
git commit -m "Task 19.2: TexturedCuboid-Helper fuer ArrayMesh mit per-face UVs"
```

### Task 19.3: `LegoFigure` — sechs Quader, sechs Pivots, statisch

**Files:**
- Create: `scripts/Views/LegoFigure.cs`

> **Geometrie-Spezifikation:** Kopf 8x8x8, Koerper 8x12x4, jeder Arm 4x12x4, jedes Bein 4x12x4. Der Anker liegt auf Hueft-Hoehe; in Godot nutzen wir dafuer einen `Hip`-Pivot bei `Y = 12` (Bein-Hoehe), damit die Figur-Wurzel auf den Boden zeigt.

- [ ] **Step 1: `scripts/Views/LegoFigure.cs` anlegen**

```csharp
using Godot;

namespace Maze.Views;

/// <summary>
/// Sechs-Quader-Spielfigur fuer Phase 19.
/// Wurzel-Y = 0 entspricht Fuessen auf dem Boden; die Figur ist 32 Einheiten hoch in
/// "Pixel-Koordinaten" und wird ueber den Skalierungsknoten in MazeView3D.tscn verkleinert.
///
/// Pivot-Hierarchie (alle als <see cref="Node3D"/>):
///   LegoFigure (root, feet at Y=0)
///   └─ Hip (Y=12) — entspricht der hier definierten Hueft-Ankerposition
///      ├─ BodyMesh
///      ├─ HeadPivot (am Hals, fuer Kopfbob/Drehung)
///      │  └─ HeadMesh
///      ├─ LeftShoulder (Schulter-Pivot fuer Arm-Schwung)
///      │  └─ LeftArmMesh
///      ├─ RightShoulder
///      │  └─ RightArmMesh
///      ├─ LeftHip (Hueft-Pivot fuer Bein-Schwung)
///      │  └─ LeftLegMesh
///      └─ RightHip
///         └─ RightLegMesh
/// </summary>
public partial class LegoFigure : Node3D
{
    [Export] public Texture2D AtlasTexture;

    public Node3D HeadPivot { get; private set; }
    public Node3D LeftShoulder { get; private set; }
    public Node3D RightShoulder { get; private set; }
    public Node3D LeftHip { get; private set; }
    public Node3D RightHip { get; private set; }

    public override void _Ready()
    {
        var material = new StandardMaterial3D
        {
            AlbedoTexture = AtlasTexture,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
        };

        var hip = new Node3D { Name = "Hip", Position = new Vector3(0, 12, 0) };
        AddChild(hip);

        // Body: sitzt mit linker-vorderer-unterer Ecke an Hip-Origin.
        // Fuer eine zentrierte Figur: -4 in X (Body breit 8) und -2 in Z (Body tief 4).
        AddCuboid(hip, "BodyMesh", new Vector3(-4, 0, -2),
            8, 12, 4, BodyUvs(), material);

        // Head: Pivot bei Hals-Position (Mitte oben am Body, leicht nach hinten versetzt
        // mit (0, 12, -2)). HeadMesh-Mitte (4, 0, 4) auf Pivot legen.
        HeadPivot = new Node3D { Name = "HeadPivot", Position = new Vector3(0, 12, 2) };
        hip.AddChild(HeadPivot);
        AddCuboid(HeadPivot, "HeadMesh", new Vector3(-4, 0, -4),
            8, 8, 8, HeadUvs(), material);

        // Schultern: links und rechts neben dem Body, auf Hoehe 10 (knapp unter Body-Top).
        LeftShoulder = new Node3D { Name = "LeftShoulder", Position = new Vector3(-6, 10, 0) };
        hip.AddChild(LeftShoulder);
        AddCuboid(LeftShoulder, "LeftArmMesh", new Vector3(-2, -10, -2),
            4, 12, 4, ArmLeftUvs(), material);

        RightShoulder = new Node3D { Name = "RightShoulder", Position = new Vector3(6, 10, 0) };
        hip.AddChild(RightShoulder);
        AddCuboid(RightShoulder, "RightArmMesh", new Vector3(-2, -10, -2),
            4, 12, 4, ArmRightUvs(), material);

        // Hueftgelenke: links und rechts unter dem Body. Pivot oben am Bein.
        LeftHip = new Node3D { Name = "LeftHip", Position = new Vector3(-2, 0, 0) };
        hip.AddChild(LeftHip);
        AddCuboid(LeftHip, "LeftLegMesh", new Vector3(-2, -12, -2),
            4, 12, 4, LegLeftUvs(), material);

        RightHip = new Node3D { Name = "RightHip", Position = new Vector3(2, 0, 0) };
        hip.AddChild(RightHip);
        AddCuboid(RightHip, "RightLegMesh", new Vector3(-2, -12, -2),
            4, 12, 4, LegRightUvs(), material);
    }

    private static void AddCuboid(Node3D parent, string name, Vector3 meshOffset,
        float w, float h, float d, TexturedCuboid.FaceUvs uvs, Material material)
    {
        var mesh = TexturedCuboid.Build(w, h, d, uvs);
        var instance = new MeshInstance3D
        {
            Name = name,
            Mesh = mesh,
            Position = meshOffset,
            MaterialOverride = material,
        };
        parent.AddChild(instance);
    }

    // UV-Rechtecke fuer den 64x32-Atlas assets/devedse.png.
    private static TexturedCuboid.FaceUvs HeadUvs() => new(
        Front:  new(8, 8, 8, 8),
        Right:  new(16, 8, 8, 8),
        Rear:   new(24, 8, 8, 8),
        Left:   new(0, 8, 8, 8),
        Top:    new(8, 0, 8, 8),
        Bottom: new(16, 0, 8, 8));

    private static TexturedCuboid.FaceUvs BodyUvs() => new(
        Front:  new(20, 20, 8, 12),
        Right:  new(28, 20, 4, 12),
        Rear:   new(32, 20, 8, 12),
        Left:   new(16, 20, 4, 12),
        Top:    new(20, 16, 8, 4),
        Bottom: new(28, 16, 8, 4));

    private static TexturedCuboid.FaceUvs ArmLeftUvs() => new(
        Front:  new(44, 20, 4, 12),
        Right:  new(48, 20, 4, 12),
        Rear:   new(52, 20, 4, 12),
        Left:   new(40, 20, 4, 12),
        Top:    new(44, 16, 4, 4),
        Bottom: new(48, 16, 4, 4));

    // Rechter Arm: gespiegelte UVs ueber negative Width im Rectangle.
    private static TexturedCuboid.FaceUvs ArmRightUvs() => new(
        Front:  new(48, 20, -4, 12),
        Left:   new(52, 20, -4, 12),
        Rear:   new(56, 20, -4, 12),
        Right:  new(44, 20, -4, 12),
        Top:    new(48, 16, -4, 4),
        Bottom: new(52, 16, -4, 4));

    private static TexturedCuboid.FaceUvs LegLeftUvs() => new(
        Front:  new(4, 20, 4, 12),
        Right:  new(8, 20, 4, 12),
        Rear:   new(12, 20, 4, 12),
        Left:   new(0, 20, 4, 12),
        Top:    new(4, 16, 4, 4),
        Bottom: new(8, 16, 4, 4));

    private static TexturedCuboid.FaceUvs LegRightUvs() => new(
        Front:  new(8, 20, -4, 12),
        Left:   new(12, 20, -4, 12),
        Rear:   new(16, 20, -4, 12),
        Right:  new(4, 20, -4, 12),
        Top:    new(8, 16, -4, 4),
        Bottom: new(12, 16, -4, 4));
}
```

> **Hinweis:** Wir nutzen bewusst Godots Knoten-Transformations-Vererbung statt manueller Matrix-Multiplikation. Der Effekt bleibt identisch: die Schulter sitzt am oberen Rand des Arm-Quaders, der Schwung erfolgt um diese Achse.

- [ ] **Step 2: Build pruefen**

```powershell
& $env:GODOT4 --path $PWD --build-solutions
dotnet build
```

Erwartet: `Build succeeded`.

- [ ] **Step 3: Commit**

```bash
git add scripts/Views/LegoFigure.cs
git commit -m "Task 19.3: LegoFigure mit Pivot-Hierarchie und Atlas-Texture"
```

### Task 19.4: Walk-Animation per Sinus-Wellen

**Files:**
- Modify: `scripts/Views/LegoFigure.cs`

- [ ] **Step 1: Animations-Felder und `_Process`**

In `LegoFigure` direkt unter `[Export] public Texture2D AtlasTexture;` ergaenzen:

```csharp
[Export] public float WalkSpeedScale = 8f;       // wie schnell der Phasen-Akkumulator laeuft
[Export] public float HeadTurn = 0f;             // optionaler Yaw, wird von aussen gesetzt

private float _walkPhase;
private bool _isWalking;
```

Am Ende der Klasse:

```csharp
/// <summary>
/// Schaltet die Lauf-Animation an oder aus. Im Idle-Modus stehen Arme/Beine still,
/// der Kopf wackelt nicht.
/// </summary>
public void SetWalking(bool walking) => _isWalking = walking;

public override void _Process(double delta)
{
    if (_isWalking)
        _walkPhase += (float)delta * WalkSpeedScale;

    float v = _walkPhase;

    // Ziel: deutlicher Gehschwung fuer Arme/Beine und leichtes Kopfwackeln.
    // Pitch = X-Achse, Yaw = Y-Achse, Roll = Z-Achse in Godot.
    HeadPivot.Rotation = new Vector3(
        Mathf.Sin(v) / 10f,   // _walkPhase = elapsed * WalkSpeedScale, daher ist bei WalkSpeedScale=8 der Kopfbob-Term sin(value*8)
        HeadTurn,
        0f);

    LeftShoulder.Rotation = new Vector3(
        Mathf.Sin(v * 5f / 8f) / 2f,                // sin(value*5)/2 — wir teilen durch 8/8 wegen WalkSpeedScale=8
        0f,
        Mathf.Sin(v * 9f / 8f) / 8f - 1f / 8f);

    RightShoulder.Rotation = new Vector3(
        Mathf.Sin(v * 5f / 8f - Mathf.Pi) / 2f,
        0f,
        Mathf.Sin(v * 9f / 8f - Mathf.Pi) / 8f + 1f / 8f);

    LeftHip.Rotation = new Vector3(
        Mathf.Sin(v * 7f / 8f),
        0f,
        0f);

    RightHip.Rotation = new Vector3(
        Mathf.Sin(v * 7f / 8f - Mathf.Pi),
        0f,
        0f);
}
```

> **Mathe-Hinweis:** Der Phasenakkumulator `_walkPhase` wird direkt als Eingabe verwendet, mit Faktoren 5, 7, 8, 9 fuer unterschiedliche Frequenzen. Wir setzen `WalkSpeedScale = 8`, sodass `_walkPhase` pro Sekunde um 8 steigt — passend fuer den Kopfbob-Term `sin(value*8)`. Fuer die anderen Glieder skalieren wir entsprechend (5/8, 7/8, 9/8). Wer das mathematisch sauberer haben moechte, kann `WalkSpeedScale = 1` setzen und alle inneren Faktoren auf 5, 7, 8, 9 belassen — das Ergebnis ist identisch.

- [ ] **Step 2: Build und visueller Test (Standalone, ohne Maze)**

```powershell
& $env:GODOT4 --path $PWD --build-solutions
dotnet build
```

Optional: eine kurze Test-Szene `scenes/LegoFigureTest.tscn` mit nur einer `LegoFigure` und einer Camera anlegen, um die Animation isoliert zu pruefen, *bevor* sie in MazeView3D landet. Fuer Schueler ist das didaktisch wertvoll — Fehlersuche im kleinen, isolierten System.

- [ ] **Step 3: Commit**

```bash
git add scripts/Views/LegoFigure.cs
git commit -m "Task 19.4: LegoFigure-Walk-Animation per Sinus-Wellen"
```

### Task 19.5: Capsule durch Lego-Figur ersetzen

**Files:**
- Modify: `scenes/MazeView3D.tscn` (Player-Knoten umbauen)
- Modify: `scripts/Views/PlayerCharacter3D.cs` (Walk-State an Lego-Figur durchreichen)

- [ ] **Step 1: `scenes/MazeView3D.tscn` — `Mesh`-Kind durch `LegoFigure` ersetzen**

In `scenes/MazeView3D.tscn` finde den Player-Block. Erweitere den Header um eine zusaetzliche `[ext_resource]`-Zeile fuer das Lego-Skript und eine `[ext_resource]`-Zeile fuer die Atlas-Textur:

```text
[ext_resource type="Script" path="res://scripts/Views/LegoFigure.cs" id="4_lego"]
[ext_resource type="Texture2D" path="res://assets/devedse.png" id="5_atlas"]
```

Erhoehe `load_steps` entsprechend.

Ersetze den `[node name="Mesh" type="MeshInstance3D" parent="Player"]`-Block (mit dem CapsuleMesh) komplett durch:

```text
[node name="Figure" type="Node3D" parent="Player"]
script = ExtResource("4_lego")
AtlasTexture = ExtResource("5_atlas")
transform = Transform3D(0.025, 0, 0, 0, 0.025, 0, 0, 0, 0.025, 0, 0, 0)
```

> **Hinweis zur Skalierung:** `Transform3D` mit Diagonal-0.025 = uniforme Skalierung um Faktor 0.025. Die Figur ist in Pixel-Koordinaten 32 Einheiten hoch — *32 × 0.025 = 0.8* Welt-Einheiten, passt knapp in eine 1.0-Zelle. `_camera`-Folge-Modus sieht weiterhin gut aus.

Du kannst auch das `CapsuleMesh_player`-Sub-Resource am Anfang der Datei loeschen — es wird nicht mehr referenziert.

- [ ] **Step 2: `scripts/Views/PlayerCharacter3D.cs` — Walk-State weitergeben**

In `PlayerCharacter3D` ergaenze ein neues Feld bei den Knotenreferenzen (oder loese in `_Ready` auf, falls noch nicht vorhanden):

```csharp
private LegoFigure _figure;

public override void _Ready()
{
    _figure = GetNodeOrNull<LegoFigure>("Figure");
}
```

Aktualisiere `StandHeight`-Default von 0.5 auf 0.0 (die Lego-Figur hat ihren eigenen Boden auf Y=0):

```csharp
[Export] public float StandHeight = 0.0f;
```

In `ProcessFollowPath` direkt nach `if (!_isMoving) return;`:

```csharp
_figure?.SetWalking(_isMoving);
```

Genauso in `ProcessManual` direkt vor dem `if (_isAnimatingCell)`-Block:

```csharp
_figure?.SetWalking(_isAnimatingCell);
```

> **Hinweis:** Wenn die Figur per Tastendruck-Lerp eine Zelle weit laeuft, ist `_isAnimatingCell == true` waehrend der Animation. Sobald sie die Zielzelle erreicht und stillsteht, gibt es einen kurzen Idle-Moment, bis der naechste Tastendruck kommt — perfekt fuer "stop & step"-Look.

Optional: in `ProcessManual`, wenn `dir` gesetzt ist und sich die Figur bewegt, `_figure.HeadTurn` so setzen, dass die Figur in die Bewegungsrichtung schaut. Schueler-Aufgabe als Erweiterung.

- [ ] **Step 3: Build und manueller Test**

```powershell
& $env:GODOT4 --path $PWD --build-solutions
& $env:GODOT4 --path $PWD
```

Pruefe:
1. Maze 25x25, BFS solven — die Lego-Figur erscheint am Startfeld und laeuft mit Arm- und Bein-Schwung den Pfad ab.
2. "Selbst spielen" + WASD — Figur laeuft Zelle fuer Zelle, Animation laeuft waehrend des Lerps, steht still im Idle.
3. "Verfolger-Kamera" + "Entdeckungs-Modus" gleichzeitig — die OmniLight aus Phase 18 leuchtet die Figur korrekt aus, die Texture sieht aus naher Distanz scharf aus (dank Nearest-Filter).

- [ ] **Step 4: Commit**

```bash
git add scenes/MazeView3D.tscn scripts/Views/PlayerCharacter3D.cs
git commit -m "Task 19.5: Capsule durch animierte Lego-Figur ersetzt"
```

### Optionale Polish-Schritte (nicht im Plan erzwungen)

- **Yaw der Figur:** In `PlayerCharacter3D.ProcessFollowPath` und `.ProcessManual` die Figur-`Rotation.Y` an die aktuelle Bewegungsrichtung anpassen (z. B. via `Mathf.Atan2(toTarget.X, toTarget.Z)`). Macht den Bot deutlich lebendiger.
- **Idle-Atem:** Wenn `_isWalking == false`, einen kleineren `_walkPhase += delta * 1.5f` weiterlaufen lassen und die Sinus-Amplituden mit `0.2f` multiplizieren — die Figur steht dann nicht ganz still, sondern atmet leicht.
- **Eigene Skin:** `devedse.png` durch ein Minecraft-Skin-PNG (Steve, Alex, etc.) ersetzen — die UV-Koordinaten in `LegoFigure` passen 1:1 fuers klassische Minecraft-64x32-Skin-Layout.

---

## Abschluss-Checkliste fuer alle vier Phasen

Nach Abschluss aller Tasks sollte folgendes gleichzeitig funktionieren:

- [ ] Maze 25x25 erstellen, BFS solven — Lego-Figur (Phase 19) laeuft sichtbar von Start zu Goal mit Arm-/Bein-Schwung.
- [ ] "Verfolger-Kamera" wahlweise an/aus — wechseln waehrend der Bot laeuft.
- [ ] "Selbst spielen" mit aktiviertem Verfolger — WASD steuert die Figur, Waende blockieren, Sieg-Label zeigt Zeit.
- [ ] "Entdeckungs-Modus" wahlweise an/aus — sanfter Uebergang, Lichtkreis folgt der Figur, OmniLight beleuchtet die Lego-Texture korrekt.
- [ ] Reset und neues Maze — Figur ist versteckt, Manual-Modus deaktiviert, Tageslicht zurueck.
- [ ] 1000x1000-Maze und "Ohne Tempolimit"-Modus aus Phasen 12/13 funktionieren weiterhin (Regression).

## Ideen fuer Folge-Phasen (nicht in diesem Plan)

Diese Liste ist bewusst *nicht* Teil der Phasen 16–19, soll aber Schuelern und Lehrern als Anregung dienen:

- **Phase 20 — Race-Mode:** Solver-Bot und Spieler starten gleichzeitig, beide laufen parallel. Zwei Lego-Figuren mit unterschiedlichen Skins. Wer ist zuerst am Ziel?
- **Phase 21 — Schatzsuche:** N zufaellig verteilte "Muenz"-Knoten im Maze, die der Spieler einsammeln muss, bevor das Goal zaehlt. Bonus-Zeit pro Muenze.
- **Phase 22 — Geist-Modus:** Der beste eigene Run wird als halbtransparente Geist-Figur aufgenommen und beim naechsten Versuch parallel abgespielt.
- **Phase 23 — Bestenliste:** Die fuenf besten Zeiten pro Maze-Groesse persistieren in einer JSON-Datei in `user://`.

# Maze Project — Horror-Umbau mit groesseren Zellen und freier Bewegung — Implementierungsplan

> Dieser Plan baut auf der aktuellen Architektur mit `Main`, `MazeView3D`, `PlayerCharacter3D`, `MonsterManager`, `TrapManager`, `DayNightController` und den vorhandenen Audio-Assets auf. Ziel ist kein kompletter Neustart, sondern ein kontrollierter Umbau vom "Maze-Spiel mit Horror-Elementen" zu einem deutlich staerkeren Horror-Erlebnis.

**Goal:** Das Spiel soll sich kuenftig wie ein Horror-Labyrinth anfuehlen: enger Blick, bedrohliche Audio-Layer, mehr Unsicherheit, groessere Zellen, freie Bewegung innerhalb der Zellen und staerkerer Druck durch Monster, Dunkelheit und Orientierungslosigkeit. Die aktuelle zellweise Spielerbewegung wird durch freie 3D-Lokomotion ersetzt, ohne die bestehende Maze-, Save-, Trap- und Monster-Logik wegzuwerfen.

**Zentrale Architektur-Entscheidung:** Die Maze-Topologie bleibt **zellenbasiert**, aber die Spielersteuerung wird **weltpositionsbasiert**. Das ist die risikoaermste Route. Der aktuelle Engpass sitzt in `scripts/Views/PlayerCharacter3D.cs`: Der Manual-Modus arbeitet dort mit `_isAnimatingCell`, `_manualCell` und genau einem Zell-Lerp pro Eingabe. Fuer den Horror-Umbau reicht es deshalb nicht, nur `CellSize` groesser zu machen. Die Bewegung muss auf kontinuierliche Weltpositionen umgestellt werden, waehrend Systeme wie Monster-Sicht, Fallen, Zielerkennung und Save-State weiterhin mit einer aus der Weltposition abgeleiteten `CurrentPlayerCell` arbeiten.

## Ausgangslage im aktuellen Projekt

**Bereits vorhanden und wiederverwendbar:**

- `MazeView3D` besitzt schon `CellSize`, Licht, Nebel, Proximity-Effekte und Nachtparameter.
- `PlayerCharacter3D` besitzt bereits Sprint/Stamina, First-Person-Unterstuetzung und ein `CellVisited`-Signal.
- `MonsterManager` und `MonsterController` arbeiten schon mit Zellpositionen, Sichtlogik und Chase-Verhalten.
- `TrapManager` verwendet bewusst Zellereignisse statt Physics-Overlaps.
- Das Projekt hat bereits eine Horror-Richtung durch Nacht, Sichtbegrenzung, Proximity-Overlay und Monster.

**Verfuegbare Horror-Audio-Dateien unter `assets/audio/horror`:**

- `data_pion-st1-footstep-sfx-323053.mp3`
- `data_pion-st2-footstep-sfx-323055.mp3`
- `data_pion-st3-footstep-sfx-323056.mp3`
- `soundreality-heart-beat-137135.mp3`
- `dragon-studio-ghost-whisper-351569.mp3`
- `freesound_community-whisper-trail-1-105420.mp3`
- `dogwolf123-flying-monster-screech-02-461220.mp3`
- `dragon-studio-deep-sea-monster-roar-329857.mp3`
- `chiiri-monster-15-337349.mp3`
- `freesound_community-monster-bite-44538.mp3`
- `freesound_community-running-14658.mp3`
- `freesound_community-metal-stretch-and-scrape-two-56192.mp3`
- `freesound_community-bear-trap-103800.mp3`
- `freesound_community-hear-race-and-give-out-78043.mp3`

## Leitbild fuer den Horror-Umbau

Das Spiel sollte nicht nur "dunkler" werden, sondern folgende Wirkung erzielen:

- Der Spieler weiss nie sicher, was hinter der naechsten Ecke wartet.
- Die Bewegung fuehlt sich koerperlich und verletzlich an, nicht abstrakt zellbasiert.
- Sound informiert frueher als Sicht.
- Monster duerfen nicht dauernd sichtbar sein, aber dauernd praesent wirken.
- Das Labyrinth soll eher wie ein begehbarer Ort wirken als wie ein abstraktes Grid.

Daraus folgen drei Designregeln:

- Freie Bewegung hat Vorrang vor neuer Mechanik-Menge.
- Audio und Licht muessen frueh im Umbau kommen, weil sie den Horror-Ton tragen.
- Zelllogik bleibt intern erhalten, aber fuer den Spieler unsichtbarer.

## Phase 1 — Bewegungsmodell von Zell-Lerp auf freie 3D-Lokomotion umstellen

Ziel: Der Spieler darf sich innerhalb einer Zelle frei bewegen und nicht nur die naechste Zelle anspringen.

**Empfohlene technische Route:**

- `PlayerCharacter3D` nicht weiter als reines `Node3D`-Interpolationsobjekt ausbauen.
- Stattdessen den spielbaren Pfad auf ein Physik-/Kollisionsmodell umstellen, am besten mit `CharacterBody3D` als owning node fuer die echte Laufbewegung.
- Den bisherigen Solver-/Bot-Pfad koennen entweder ein Child-Controller oder ein separater Bot-Modus behalten.

**Konkrete Architektur:**

- `PlayerCharacter3D` wird zu einem echten Beweger mit Weltposition, Velocity und Kollision.
- Aus `GlobalPosition` wird pro Frame oder bei Zellwechsel eine `CurrentPlayerCell` abgeleitet.
- `CellVisited` feuert nicht mehr pro Tastendruck, sondern nur dann, wenn sich die abgeleitete Zellkoordinate aendert.
- Sprint/Stamina bleiben erhalten, aber beeinflussen jetzt kontinuierliche Bewegung statt Zell-Lerp-Dauer.

**Wichtige Folge fuer bestehende Systeme:**

- Monster-Naehe, Fallen, Zielerreichung und Save-State lesen weiter die Zellkoordinate des Spielers.
- Nur die Art, wie diese Zellkoordinate entsteht, aendert sich.

**Warum das die richtige Schnitttiefe ist:**

- Kleine Risiko-Flaeche: Monster, Trap-Logik und Maze-Topologie muessen nicht neu erfunden werden.
- Spuerbarer Spielergewinn: Das Spiel fuehlt sich sofort weniger abstrakt an.
- Spaetere Horror-Features wie Herzschlag, Atmung, verzoegerte Schritte und physische Naehe funktionieren erst mit freier Bewegung wirklich gut.

## Phase 2 — Groessere Zellen und begehbare Raumwirkung herstellen

Ziel: Eine Maze-Zelle soll sich wie ein kleiner Raum oder Gangabschnitt anfuehlen, nicht wie ein abstrakter Schrittpunkt.

**Empfohlene Startwerte fuer einen ersten Playtest:**

- `MazeView3D.CellSize` von `1.0` auf etwa `2.6` bis `3.2` erhoehen.
- `WallHeight` auf etwa `2.3` bis `2.8` anheben.
- `WallThickness` leicht vergroessern, damit die Waende massiver wirken.
- Player-Kollisionskoerper bewusst schmal halten, damit enge Durchgaenge Spannung erzeugen, aber nicht unfair blockieren.

**Wichtig:** Zellgroesse allein reicht nicht. Mit groesseren Zellen muessen auch diese Dinge angepasst werden:

- Marker-Positionen in `MazeView3D`
- Trap-Positionierung in Weltkoordinaten
- Monster-Spawn-Visuals
- Kameraabstand und First-Person-Hoehe
- Lichtreichweite, damit grosse Zellen nicht ploetzlich zu hell oder zu leer wirken

**Gestalterische Regel:**

- Bodenflaechen nicht komplett leer lassen.
- Jede Zelle braucht mindestens leichte Atmosphaere: Schmutz, Nebel, Schatten, dunklere Ecken, Bodenvariation oder kleine Deko-Cluster.

Sonst fuehlen sich groessere Zellen nur "weiter auseinander" an, aber nicht bedrohlicher.

## Phase 3 — Kollisions- und Zellableitungs-Hybrid einfuehren

Ziel: Freie Bewegung und bestehende Zellmechaniken sauber verheiraten.

**Empfohlene Spielregel:**

- Der Spieler bewegt sich frei in Weltkoordinaten.
- Die Maze bestimmt weiter, wo Waende und offene Korridore liegen.
- Die aktuelle Spielerzelle wird aus `GlobalPosition` via `Floor(position / cellSize)` abgeleitet.
- Zellwechsel-Events entstehen nur beim Uebertritt in eine neue Zelle.

**Noetige Hilfsfunktionen:**

- `WorldToCell(Vector3 worldPosition)` zentralisieren
- `CellToWorldCenter(Vector2I cell)` zentralisieren
- optional `GetCellBounds(Vector2I cell)` fuer Debugging, Trigger und Audiozonen

**Warum kein kompletter Umstieg aller Systeme auf Weltkoordinaten:**

- Monster-AI, Sichtlinien und Fallen sind bereits robust im Zellmodell.
- Fuer Horror braucht ihr jetzt vor allem besseres Spielgefuehl, nicht sofort komplexere Navigation.
- Ein Vollumbau auf frei pathfindende Monster wuerde das Risiko massiv vergroessern.

## Phase 4 — Horror-Audio als eigenes System einziehen

Ziel: Audio wird vom Deko-Element zum wichtigsten Spannungstraeger.

**Empfohlene neue Runtime-Abstraktion:**

- `AudioDirector` oder `HorrorAudioController` als zentrales Laufzeitsystem unter `MazeView3D` oder `Main`

**Audio-Layer, die getrennt steuerbar sein sollten:**

- Ambient-Bett
- Spieler-Schritte
- Spieler-Atmung/Herzschlag
- Monster-Fernpraesenz
- Monster-Naehe/Chase
- Jumpscare- oder Sichtkontakt-Stinger
- Interaktionssounds fuer Trap, Ziel, Metall, Druckmomente

**Sinnvolle Zuordnung der vorhandenen Dateien:**

- Schritte: `data_pion-st1-footstep-sfx-323053.mp3`, `data_pion-st2-footstep-sfx-323055.mp3`, `data_pion-st3-footstep-sfx-323056.mp3`
- Herzschlag/Panik: `soundreality-heart-beat-137135.mp3`, `freesound_community-hear-race-and-give-out-78043.mp3`
- Whisper/Ambient-Paranoia: `dragon-studio-ghost-whisper-351569.mp3`, `freesound_community-whisper-trail-1-105420.mp3`
- Monster-Fernrufe/Stinger: `dogwolf123-flying-monster-screech-02-461220.mp3`, `dragon-studio-deep-sea-monster-roar-329857.mp3`, `chiiri-monster-15-337349.mp3`
- Nahkampf/Gefahr: `freesound_community-monster-bite-44538.mp3`
- Verfolgungsdruck: `freesound_community-running-14658.mp3`
- Metallische Spannungsmomente: `freesound_community-metal-stretch-and-scrape-two-56192.mp3`
- Fallen-Trigger oder Metallboden: `freesound_community-bear-trap-103800.mp3`

**Wichtige Horror-Regel:**

- Nicht alles immer abspielen.
- Wenige Sounds, aber kontextstark und mit Abstand, Lautstaerke und Timing.

**Erster Audio-MVP:**

- zufaellige ferne Whisper-Events
- dynamische Schrittgeraeusche beim Laufen/Sprinten
- Herzschlag ab niedriger Stamina oder hoher Monster-Naehe
- kurzer Monster-Stinger bei erstem Sichtkontakt oder Chase-Beginn

## Phase 5 — Licht, Blick und Sichtinformation schaerfen

Ziel: Sicht wird eingeschraenkt, ohne das Spiel unfair zu machen.

**Empfohlene Horror-Anpassungen in `MazeView3D`:**

- Sichtweite nachts weiter reduzieren
- Fog dichter und dunkler abstimmen
- Player-Light enger, waermer und unruhiger machen
- leichte Helligkeitsschwankung oder schwaches Flackern bei Stressmomenten
- Zielmarker nicht permanent wie ein klares Leuchtfeuer inszenieren

**Wichtige Regel fuer Horror statt Arcade:**

- Informationen lieber andeuten als voll offenlegen.
- Das Ziel darf lesbar sein, aber nicht den gesamten Spannungsbogen neutralisieren.

**Empfehlung fuer die Kamera:**

- First-Person als Standard fuer Horror beibehalten.
- FOV etwas enger abstimmen, damit Gassen klaustrophobischer wirken.
- Schnelle 180-Grad-Drehungen nicht zu weich machen, damit Flucht und Panik direkt bleiben.

## Phase 6 — Monster praesent, aber nicht permanent sichtbar machen

Ziel: Monster sollen den Spieler psychologisch unter Druck setzen, nicht nur mechanisch blockieren.

**Kurzfristig ohne AI-Grossumbau erreichbar:**

- Monster bleiben intern zellbasiert.
- Ihre Weltbewegung kann weiter interpoliert werden.
- Audio und Lichtreaktionen orientieren sich an Distanz zur Spieler-Weltposition.
- Chase startet weiterhin aus Zell-/Sichtregeln, fuehlt sich aber dank Sound und engerer Kamera deutlich bedrohlicher an.

**Konkrete Horror-Upgrades fuer das bestehende System:**

- Sichtkontakt-Stinger beim Wechsel in `Chase`
- Whisper oder entfernte Schrei-Laute, wenn ein Monster in benachbarten oder nahen Zellen patrouilliert
- staerkerer Heartbeat und Overlay-Intensitaet bei sinkender Distanz
- kurze Audio-Nachhallphase nach Sichtverlust, statt sofortiger Ruhe

**Mittelfristige Erweiterung:**

- Monster nicht nur auf Zellzentrum abprallen lassen, sondern ihre Bewegung optisch glatter und unvorhersehbarer machen
- einzelne Monster-Typen mit anderem Soundprofil einfuehren, bevor neue Kampfmechaniken kommen

## Phase 7 — Umgebung und Interaktion auf Horror trimmen

Ziel: Das Labyrinth soll nicht nur gross sein, sondern unangenehm praesent.

**Schnelle Atmosphaere-Gewinne:**

- dunklere Bodenmaterialien mit schwacher Variation
- vereinzelte Decals oder Bodenplatten in Zellenmitten und an Kreuzungen
- leichte Nebelinseln in tieferen Maze-Bereichen
- sparsame rote Warn- oder Restlichter an Fallen, aber nicht zu sauber oder futuristisch

**Interaktionsregeln, die gut zu Horror passen:**

- Sprinten kostet hoerbar Kraft und verraet den Spieler akustisch staerker
- Stillstehen reduziert Geraeuschkulisse, aber nicht die Angst
- Fallen und Monster duerfen mehr Druck erzeugen als direkten Schaden, solange das Spiel noch auf Orientierung basiert

## Phase 8 — Umsetzung in sicherer Reihenfolge

Ziel: Erst das Fundament, dann die Atmosphaere, dann das Feintuning.

**Empfohlene Reihenfolge fuer die echte Umsetzung:**

1. `PlayerCharacter3D` auf freie Bewegung umbauen und Zellableitung stabilisieren.
2. `MazeView3D.CellSize`, Kamera, Lichtreichweite und Kollisionsbreiten auf groessere Zellen abstimmen.
3. Audio-Director mit Schritt, Whisper, Herzschlag und Chase-Stinger einfuehren.
4. Horror-Licht/Fog/FOV feinjustieren.
5. Monster-Feedback und Naehedruck auf die neue freie Bewegung abstimmen.
6. Umgebungs-Polish und spaetere Sondermomente ergaenzen.

## Konkreter MVP fuer den ersten spielbaren Horror-Build

Wenn du schnell einen ersten starken Vorher/Nachher-Sprung sehen willst, sollte der erste echte Meilenstein nur diese Punkte enthalten:

- freie First-Person-Bewegung mit Wandkollision
- groessere Zellen
- leichtere Schritt-Sounds pro Untergrund/Laufzustand
- Herzschlag bei Sprint-Erschoepfung oder Monster-Naehe
- zufaellige Whisper in der Ferne
- dichteres Nacht-/Nebel-Setup

Schon dieser MVP wird das Spiel wesentlich staerker in Richtung Horror verschieben als mehrere neue Mechaniken ohne Bewegungsumbau.

## Risiken und Gegenmassnahmen

**Risiko 1: Freie Bewegung macht das Gameplay instabil.**

- Gegenmassnahme: Zelllogik intern behalten und nur den Spieler-Motor umstellen.

**Risiko 2: Groessere Zellen machen das Maze leer statt spannend.**

- Gegenmassnahme: Licht, Sound und kleine Umweltvariation zusammen mit `CellSize` anpassen.

**Risiko 3: Zu viele Sounds gleichzeitig wirken billig statt unheimlich.**

- Gegenmassnahme: frueh einen `AudioDirector` mit Cooldowns, Distanzregeln und Prioritaeten einfuehren.

**Risiko 4: Monster verhalten sich mit freier Bewegung des Spielers ploetzlich unfair.**

- Gegenmassnahme: Chase-Trigger und Sichtreichweite nach dem Bewegungsumbau neu balancieren, nicht vorher.

## Empfehlung fuer den naechsten Implementierungsschritt

Der sinnvollste erste Code-Schritt ist nicht Audio, sondern der Bewegungsumbau: `PlayerCharacter3D` und die Player-Kollision muessen zuerst weg vom Zell-Lerp. Danach lassen sich Zellgroesse, Sound und Horror-Feedback sauber darauf aufbauen.
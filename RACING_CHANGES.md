# Racing improvements

## October 11 bot, track and art upgrade (current configuration)

This section supersedes the earlier greyscale/110m description below.

Backup before this upgrade:
`C:\Users\Hp\AppData\Local\Temp\opencode\Gamify-before-bot-track-art-20261011.zip`

### Actual defects identified

- Old bot targets flipped between an inner lane and ±7m whenever player distance
  crossed 20m or an obstacle approached. No target commitment/hysteresis caused
  repeated lateral reversals.
- Emergency collision avoidance directly assigned X to the shoulder, producing
  visible teleports. Normal lateral movement was also an abrupt 10m/s MoveTowards.
- ±7m shoulders sat beyond the 13m road and coincided with raised shoulders;
  two bots shared each bypass, creating unnecessary bottlenecks.
- Same-side following treated separate lanes as one queue. Bots updated sequentially,
  so later bots saw already-moved positions while earlier bots saw old positions.
- Clamped position could stop a bot while speed/wheel animation still showed cruising.
  Paused speed was retained, so resume could jump straight back to cruising speed.
- No bot swept barrier constraint existed. Kinematic trigger bodies mean there was
  no dynamic solver pushing/jittering the cars, and no NavMesh or competing controller;
  the actual defects were transform/target/traffic coordination problems.

### Current traffic solution

The existing four player/answer lane centres remain -4.5, -1.5, 1.5, 4.5m. The
road is now 28m wide (previously 13m), with four dedicated outer bot lanes at
-11, -8, 8, 11m. These are part of the road, inside curbs at ±14.5m and rails at
±15.4m. Bot lanes pass outside the existing gates and barriers; scenery begins
outside ±17m. The player's existing controller, feel, camera, and gate layout
are preserved. This is the simplest compatible way to ensure bots never block
the player's complete answer-selection space, including reverse/steering.

No proximity-driven lane switching or recovery teleport remains. SmoothDamp
limits optional lane changes to 3m/s. A common position snapshot is collected,
every bot plans against it, then all positions are committed. Following uses
actual lane overlap/targets, 7m clearance, braking distance, and deterministic
priority for ties. Obstacles impose a swept longitudinal limit. Stopped cars
reset speed; wheels animate from actual travel. Questions hold all traffic and
resumption ramps from rest at 7m/s². Traffic delta is capped at 0.05s during
editor hitches; it slows on hitches instead of jumping. Automatic same-lane
overtaking is intentionally unnecessary with one opponent per outer lane.

Question spacing is now **275m**, previously **110m** (2.5×). The first question
remains at 45m; question 8 is at 1970m. Backend-sized track extension still works.
Question count/content, backend contracts and gameplay rules were not edited
in this upgrade. The straight track remains straight; no turns were added.

### Artwork and saved setup

- `Assets/RacingArt/`: eleven shared URP Lit materials (asphalt, meadow, foliage,
  wood, ivory, coral, blue, sunshine, orange, rubber, glass), `ToyTree.prefab`,
  eight saved batched environment meshes and Unity metadata.
- `RaceEnvironment.cs`: collider-free batched trees, hills, meadow, lane dashes,
  red/white curbs, blue rails, flags and start/finish chequers. Rebuilds only if
  backend question count changes the track length; no random placement per frame.
- Reuses the existing racing asset's Day Skybox; new soft sun, ambient sky/ground,
  distant atmospheric fog. Existing camera transform/FOV/follow behaviour unchanged.
- Colours existing player/bot meshes, gate frames/panels/arrows, and barriers.
  Adds non-colliding checkpoint header decorations. Existing door colour fields
  drive selection; question/answer text and UI layout are retained.
- `ArcadeTrackUpgrade.cs`: editor-only targeted upgrade utility; does not invoke
  SceneBuilder or recreate functional game objects.
- `RaceBot.cs`, `RaceDirector.cs`, `RacingSmokeChecks.cs`, SampleScene modified;
  `RaceEnvironment.cs` and `ArcadeTrackUpgrade.cs` new. Unity also refreshed
  `ProjectSettings/URPProjectSettings.asset`. Earlier unrelated user/config changes
  remain in the working tree.

All references are saved in SampleScene; no manual Inspector work is required.
Open SampleScene and click Game View after pressing Play. Other demo/backup
scenes were not upgraded. `ArcadeEnvironment` can be disabled to hide new scenery.

### Tests actually executed in this upgrade

- Compiled all new/modified scripts with no new compilation errors.
- Full existing live-scene smoke checks passed first at 110m with repaired bots,
  then again at 275m with the colourful environment.
- Passed no-input cruise, WASD steering/throttle/brake/reverse, bot advancement,
  four safe stopping gaps, question holds, all four answer-lane selections,
  wrong ram/recoil/explanation/Space retry, all eight correct answers with scoring,
  victory/results, restart with no duplicate gates/barriers, timeout life loss
  and out-of-lives results, rank HUD pause stability and barrier swept guards.
- 1800-step/30s synchronous bot stress simulation: same-lane following, no overlaps,
  road bounds respected, minimum gap 8m.
- 2400-step/40s bot stress simulation with an actual adjacent-to-same-lane merge
  and previously answered gate traversal: maximum lateral frame step 0.0475m,
  minimum shared-lane gap 8m, no gate contacts/overlaps/boundary violations.
  After hold, first resume step was 0.00195m at 0.1167m/s, not a teleport.
- Reviewed live Game View captures `Captures/arcade-track-review.png` and
  `Captures/arcade-question-review.png`. Scene validation: zero missing scripts
  or broken prefabs. Eight environment renderers during gameplay; no new scenery
  physics components. This is not a hardware performance benchmark.

No standalone player build, manual child playtest, or live backend success was
tested. Existing unrelated Unity AI NoSubscription errors and missing star/cross
glyph warnings on the pre-existing results title remain. The regression uses
editor injected input/private selection calls, as described below. Unity was
left in Edit Mode with the saved scene.

## Backup

Before edits, the project (including Assets, Packages, ProjectSettings,
UserSettings and local configuration) was archived to:

`C:\Users\Hp\AppData\Local\Temp\opencode\Gamify-before-racing-20261010.zip`

Regenerable Library/Temp/Logs/obj/.vs and Git history were excluded. The archive
contains local configuration; keep it private. Restore into a separate folder
and open in Unity to recover the pre-change project.

## Controls and compatibility

- No keys: original automatic cruise and checkpoint approach.
- W: accelerate to 22 m/s during the open racing segment.
- S: brake, then reverse up to 6 m/s; reverse cannot cross the segment start.
- A/D or left/right arrows: change lanes while driving (hold to repeat).
- At a question: existing A/D/Q/E/arrow selection and Space/Enter confirmation.
- The last 12 metres retain the original automatic checkpoint approach, so
  throttle/reverse cannot bypass the original stop/arrival rules.

CarController remains the sole player movement/camera/animation controller.
GameManager gains one optional RaceDirector reference and a preparation call
after the existing question loader finishes. Answer validation, scoring, lives,
timers, retries, ram/recoil, backend parsing/loading and results code are unchanged.

## Opponents and obstacles

Four opponents reuse copies of the existing car model, without CarController or
camera components. RaceDirector advances them only when BOTH GameManager says
DrivingToCheckpoint and the player says Cruising. All other states hold bots
in place, including wrong-answer explanations, ram/recoil, retries and results.

Bots slow toward stopping positions at least 12 m behind the player's waiting
line. Same-side bots retain 7 m following clearance and staggered stop targets.
They yield onto road shoulders near the player, checkpoint approach, barriers,
and when passing previously answered gates. The complete answer-lane corridor
stays reserved for the player. An emergency lateral yield avoids overlap if
relative car motion closes the safety envelope faster than normal yielding.

Cars have kinematic, gravity-free, trigger bounds with solver contacts disabled.
This intentionally preserves the original transform movement; adding dynamic
physics would change ram/recoil and answer alignment. RaceCar/RaceObstacle
layers identify the added objects. Forward/reverse swept bounds against the
four barriers are handled inside the existing player's Cruising movement only.
They cause no damage or life/score penalties. Adjacent lanes remain available.
Question movement is not obstacle-constrained. Obstacles are placed midway
between gates, only where the segment has at least 60 m clearance.

## Questions and track

The active scene uses Resources/questions.json; backend fetching was disabled
before this change and retains that setting. Three unique local questions were
added to BOTH existing JSON fallback sources (five to eight total). Remote
question content is never duplicated or supplemented. If backend fetching is
enabled, its returned list still replaces the fallback list exactly as before.
To increase the live backend quantity, the backend must return more questions;
no undocumented query parameter or repeated fetch was added.

Checkpoint distance is now 110 m (was 65 m), with the first gate retained at
45 m. Authored gates/road surfaces were adjusted in place. At load/restart,
RaceDirector creates extra copies of the authored gate only as needed for the
actual loaded question count, disables surplus gates, and extends existing
road/shoulder surfaces and lane markings. Questions still progress through the
existing sequential GameManager logic.

Rank uses world-Z distance, valid for the inspected straight +Z track. A 2 m
hysteresis prevents close-position flicker; ranks hold during question sequences.
Curved tracks would require replacing this metric with waypoint distance.

## Inspector setup

Already wired and saved in Assets/Scenes/SampleScene.unity:

- GameManager.raceDirector, CarController.raceDirector.
- RaceDirector gameManager, track, four bots, obstacle container, position text.
- Each bot's model, wheels, lane, and cruising speed.
- RaceCar and RaceObstacle layers; query-only car bodies/bounds.
- Compact greyscale position card inside existing HUD; existing controls hint.

No additional setup is needed for SampleScene. Other supplied/demo/backup scenes
were inspected as assets but not upgraded. Do not run the original Build Racer
Scene menu to retain this authored setup; that existing tool recreates objects.

## Verification actually completed

Unity 6000.4.3f1 compiled the changes without script errors. Existing SceneBuilder
obsolete-API warnings and unrelated Unity AI NoSubscription console errors remain.

Opt-in live-scene checks in Assets/Scripts/Editor/RacingSmokeChecks.cs passed:

- Eight local questions, gate capacity and all seven 110 m spacings.
- Original no-key cruise; A/D lane changes; S braking/reversing and reverse bound;
  W acceleration; opponent forward movement.
- All four opponent stopping gaps and frozen transforms during question wait.
- W/S cannot move the player at questions; all four answer lanes interpolate
  correctly with bots present; no added life/score penalties.
- Wrong answer through original ram, recoil, explanation, Space retry of same
  question; correct answers score without consuming lives.
- Forward/reverse swept barrier guard and adjacent-lane clearance.
- Entire eight-question sequence to victory/results; Space restart and no duplicate
  checkpoint/barrier objects; all three timeout lives to game-over/results.
- HUD shows five racers and holds rank during question pause.

Additional live checks: explicit rank changes 1st -> 2nd -> 1st with stable rank
inside the 2 m deadband; simulated 12-item parsed list expands gates/track then
disables surplus gates for a shorter list; unavailable-backend fetch falls back
to the eight local questions. A Game View screenshot was visually reviewed.

The smoke runner injects a temporary keyboard and invokes existing private Update/
selection methods for deterministic input windows. Outside those windows gameplay,
coroutines, movement, timers and progression run normally. This is an editor
integration harness, not a Unity Test Runner suite or a manual keyboard playtest.
Live backend success and a standalone player build were not tested.

Run checks in Play Mode using the Unity MCP execute-code command:
`Gamify.Racing.Editor.RacingSmokeChecks.Run();`
Inspect `RacingSmokeChecks.Results` / `Running`. It drives the current game through
results; do not run it during an ordinary play session.

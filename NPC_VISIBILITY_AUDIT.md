# NPC visibility audit and focused follow-up

## Backup and scope

The working tree already contained an NPC recovery-speed fix, brick wall assets,
vegetation improvements and an earlier report. These were inspected and tested
again rather than assumed correct. Existing uncommitted work was preserved.
Before changes, Assets, ProjectSettings, Packages and the prior report were
archived to `C:\Users\Hp\AppData\Local\Temp\opencode\Gamify-visibility-audit-20261011.zip`.

## Diagnosis from actual Play Mode

- SampleScene is the enabled build scene. Four authored NPC instances are
  referenced by RaceDirector; there is no runtime NPC spawner in this scene.
- Each NPC has six active imported car renderers with valid meshes/URP Lit
  materials. Inactive legacy primitive models are intentionally superseded.
- The existing architecture uses transform-driven, kinematic Rigidbody roots
  with trigger colliders and gravity disabled. This configuration was not changed.
  Root Y stayed at 0.3; road BoxCollider is enabled, road/NPC layer collisions
  are permitted, gravity is -9.81. No falling-through-ground evidence found.
- Camera mask is all layers; FOV 58, near/far 0.3/1000. Camera behavior was not changed.
- A baseline experiment disabled recovery pace **only on runtime instances**.
  At player Z=200.02, bots were Z=155.88/145.63/179.50/183.03, moving at
  15/16.5/18/19.5 m/s. All four had negative camera-space depth and failed
  frustum checks. The cars were behind the forward camera, not missing meshes.
- The already-present 28m/s recovery restored competing traffic, but the
  player-lane bot could remain trapped by the player-following limit. Overtake
  detection considered other bots and barriers but not the slower player;
  it also compared leaders against base pace rather than current recovery pace.

## Changes in this follow-up

- `Assets/Scripts/RaceBot.cs`: calculate recovery pace before lane decisions;
  treat the slower player as a leader requiring an existing safe adjacent-lane
  overtake; compare other leaders against that same pace. Existing LaneClear,
  following distances, braking, question holds and bypass lanes remain in use.
- `Assets/Scripts/RaceEnvironment.cs`: optional foliageAccent material and
  deterministic alternate tree-crown batches. Trunks and scenery colliders are
  unaffected; no animation, package, particles or gameplay component added.
- `Assets/RacingArt/LeafLime.mat`: compatible lightweight foliage material.
- Reapplied the existing `VisibilityVisualPolish.Apply()` editor utility to
  SampleScene: 32 wall renderers use the existing StylisedBrickWall material
  and 256x320 mipmapped StylisedBrick texture. Existing mesh UVs, geometry,
  transforms, tags, layers, colliders and highlight/break routines are retained.
  Saved refreshed `LivingEnvironment_*` batches and the accent reference.
- Existing bushes, tree size/rotation variation, meadow material, skybox and
  lighting were retained. Accent variation adds two batches across the authored
  long track; scenery has zero colliders.

## Tests actually performed

- Reproduced the all-behind-camera baseline in actual Play Mode.
- Final normal-timescale driving test used the original enabled collision guard.
  At player Z>245, all four imported model bounds intersected the camera frustum:
  bots Z=254.89/241.49/273.57/280.75, speeds 15/15.18/18/19.5 m/s, Y=0.3.
  All three lives remained. Game View screenshot visibly shows all four cars.
- Bots stayed stationary for a 0.6-second question hold and resumed after the
  existing answer/ram sequence. At checkpoint 2, player Z=688.5 and bots
  Z=661.51/668.5/676.5/668.5, all speeds zero; all three lives remained.
- Game View inspected for brick gates, coloured tree variation and clear road.
- Existing wrong-answer recoil/explanation and retry methods exercised; retry
  returned to WaitingForAnswer with the gate marked failed.
- Both changed scripts: standard validation, zero warnings/errors. Scene
  validation: zero missing scripts/broken prefabs. No new gameplay compile errors
  observed; existing Unity AI NoSubscription errors and MCP reconnect warning remain.
- Unity left in Edit Mode. No diagnostic source scripts were added.

Screenshots:
- `Captures/visibility-final-four-opponents.png`
- `Captures/visibility-final-brick-vegetation.png`

## Existing issues and verification limits

The current local JSON has five questions. QuestionParser interprets it as a
backend Quiz first, dropping local correctIndex values to -1. This was confirmed
by runtime correctIndex=-1 versus authored correctIndex=3 on question 2. Thus
the automated progression above used the current evaluation index (-1), not a
valid answer gate. Wrong local answers also deduct two lives through the existing
SubmitAnswer + RecordWrongAnswer paths. Neither unrelated quiz defect was edited.
The earlier report's eight-question/full-regression claims do not describe this
current baseline; no full valid-gate victory regression is claimed here.

Live backend/upload flow, standalone build, held-W endurance, complete restart/
results flow and hardware performance were not reverified. Forward-camera
followers and NPCs parked behind the question stop are naturally out of view;
the fix restores visible competitors during open racing, not perpetual visibility
of every car. No unrelated gameplay source or backend settings were edited.

# GAMIFY master pass

Backup before edits: `C:\Users\Hp\AppData\Local\Temp\opencode\Gamify-before-master-pass-20261011.zip`
(source/assets/settings/configuration; regenerable caches/Git history excluded).

## Audit and scope

Unity 6000.4.3f1, URP, SampleScene. Actual course is a straight world-Z road;
there are no authored curves, waypoints, splines or NavMesh. Player movement,
ram/recoil and camera explicitly depend on world Z. Adding curves would require
changing preserved mechanics/camera, so this pass does not invent turns. Turn
navigation is not implemented/tested. Uploaded-material UI, question IDs, quiz
IDs and answer-submission endpoints do not exist in the inspected Unity scripts;
the available integration fetches `/api/questions/latest` with existing JSON
fallbacks. No API schema or missing workflow was invented.

NPC issue: previous fix put opponents on dedicated outer lanes, isolated from
the player. They now use the player's four lane centres during ordinary racing,
smoothly choose safe adjacent lanes when a barrier/slower leader warrants it,
and commit decisions for two seconds. Near gates they take reserved outer
bypasses and return after clearing the previous gate. Following uses snapshots,
braking clearance, player spacing and same-lane occupancy. No normal-drive
teleport or Rigidbody steering was added.

Player: preserved WASD/lane controls, automatic cruise and checkpoint approach.
Acceleration 18 -> 24m/s², boosted speed 22 -> 25m/s, braking 32 -> 36m/s²;
frame-rate-independent racing lane response; 0.28s held-steering repeat;
subtle body pitch, existing engine pitch driven by speed, reverse wheel spin.
`UpdateCamera`, offsets, FOV, rotations, shake and tracking routines unchanged.

Track: 28 -> 32m wide. Spacing 275 -> 650m (2.36×). First gate still 45m;
question 8 at 4595m. No question-content/count or scoring-rule changes.

## Collision implementation

`RacingCollisionGuard` executes after traffic movement. Swept relative bounds
check actual NPC root positions and explicitly listed RaceDirector obstacle
colliders; harmless environment objects are not included. Cars remain kinematic
and transform-driven; solver callbacks alone cannot detect obstacles because
existing motion is clamped before physical contact. Swept bounds also avoid
tunnelling when transforms move between physics ticks.

Root-ID contact tracking, a 1.5-second global cooldown and separation-before-
reentry prevent duplicate/multiple-collider/continuous-contact deductions. A
contact first entered during cooldown remains suppressed until separation.
`QuestionManager.LoseLife()` shares the existing lives counter/events, without
marking a question retried. Existing wrong-answer code calls this same method.
GameManager updates HUD/audio, adds brief orange feedback, slows the car, and
calls existing ShowGameOver at zero. Guard only applies in normal Cruising /
DrivingToCheckpoint; question/ram/recoil/explanation/results are protected.
Watcher has a state guard so a collision game-over cannot be overwritten by
the arrival coroutine. Restart clears all collision bookkeeping.

## Asset audit (already imported folders; no new package/import)

### Assets/BEDRILL/Track_Environment_Free

14 FBX models/prefabs: arches (standard/banner/finish), two arrows, barrier,
cone, concrete fence, flag, light pole, sign, tire, traffic light, tribune.
One 64×64 shared palette texture, two materials, demo scene, Readme.txt.
Readme states Unity 2018.4+ and mobile-friendly. No scripts, audio, animation
clips or AnimatorControllers found. Selected prefabs have MeshColliders and
Standard materials; no Rigidbody/controller dependencies. Scale import is 1.
No explicit licence document is bundled; "Free"/compatibility is not a grant
of a particular licence. Existing project's asset-use rights remain applicable.

Selected: `Tribune_free_obs.prefab`, `Flag_free_obs.prefab`,
`Sign_free_obs.prefab`, `Tire_free_obs.prefab`, 16 sparse trackside instances.
Converted local material copy preserves palette texture in URP Lit; source
materials/prefabs are not overwritten. Decorative MeshColliders removed on
instances, outside road/answer corridor. Other meshes skipped as redundant
with current rails/gates/barriers or unnecessary clutter/dependencies.

### Assets/ARCADE - FREE Racing Car

Car and Road FBX models, six mesh-only variants (base/red/purple/gray/blue),
matching collider-prefab set, colour palette/emission/environment materials and
textures, Day/Night skyboxes and demo scenes. Separate Body, Spoiler and four
named wheel transforms found. Selected mesh-only car has no scripts, Rigidbody,
colliders or animator; no audio/animation clips found. Import scale 1; body
approximately 2.06m wide × 4.82m long, scaled 0.72 to existing collision envelope.
Standard source shaders required local URP Lit copies with original textures.
No bundled explicit licence document located.

Selected exact mesh-only prefabs: Blue Variant for player; Red, Purple, Gray,
Blue Variant for bots. Existing Day Skybox reused (already in scene). Original
primitive models are disabled, retained in hierarchy. Controller/camera/car
roots/collision bounds are retained; four wheel references point to new pivots.
Skipped collider-prefab set (unnecessary collider duplication), Road.fbx
(would replace existing course), demo scenes/Night skybox (no benefit to daytime
palette). Source assets remain intact; no packages or wholesale imports added.

## UX/presentation

Existing Canvas/GameUI retained. Corrected overflowing StatsBar child anchors
that placed lives/timer offscreen. Added numeric speed, readable life count,
course progress bar and state/collision messages; existing score/rank retained.
Improved question/explanation font hierarchy, dark question card, warm retry
heading, encouraging results text without missing star/cross font glyphs.
Answer controls/timer/ram/explanations/retries not redesigned. No new menus,
camera effects or forced transitions. Shared textured vehicle materials, selected
trackside stands/signs/flags/tire details complement existing batched scenery.

## Changed files/setup

Runtime: CarController.cs, RaceBot.cs, RaceDirector.cs, RacingCollisionGuard.cs
(new), QuestionManager.cs, GameManager.cs, GameUI.cs, RaceEnvironment.cs.
Editor: MasterRaceUpgrade.cs (new), RacingSmokeChecks.cs. Scene: SampleScene.unity.
Assets/RacingArt: Integrated_* URP materials, MasterEnvironment_* batched meshes,
ProgressSprite.asset and metadata. SelectedAssetScenery and ArcadeCarVisual
instances in scene. Reports and review screenshots under Captures.

All references saved for SampleScene. No Inspector setup required. Collision
cooldown/half-size tunable on GameManager's RacingCollisionGuard component.
Do not run the old destructive SceneBuilder menu to retain this scene setup.

## Verification

Core gameplay smoke regression passed at 275m with shared-line AI before asset
integration. Collision root-ID/cooldown/continuous-contact checks passed; actual
NPC overlap bounds lost one life across repeat samples. Actual obstacle contact
at the player's clamped stopping boundary lost one life; a final contact with
one remaining life reached existing ResultsScreen with zero lives.

Final controls/quiz regression is run with guard temporarily disabled to isolate
intentional test manoeuvres from traffic damage, then restored; collision checks
are separate with real bounds and guard enabled. The first combined run correctly
deducted a life during the test's reverse/lane manoeuvres, invalidating its old
"no penalty" assertion; it was not treated as a passed regression.

No standalone build, live backend success/upload/submission, child usability
study or hardware performance benchmark performed. The level has no turn to
test. Existing unrelated Unity AI NoSubscription errors remain.

Final 650m gameplay smoke run completed successfully: all eight questions through
victory, wrong-answer recoil/explanation/retry, correct scoring, all answer lanes,
restart reset/no duplicates, timeout lives to game-over, WASD/automatic cruise,
safe bot question holds, rank stability and swept barrier guards. Guard restored
enabled after isolated regression. Results-screen contact rejection checked.
30-second shared-lane traffic simulation found no overlaps/boundary violations;
maximum lateral step 0.0492m. That simulation did not produce same-lane following,
so its sentinel gap was not reported as a following clearance measurement.
Every integrated vehicle has four wheel references. Selected car/scenery materials
have zero missing shaders/materials and zero unconverted Standard shaders. Final
environment uses 14 batched renderers. Racing and question Game View screenshots
reviewed; final scene validation found zero missing scripts/broken prefabs. Unity
left in Edit Mode, scene saved/not dirty, collision guard enabled.

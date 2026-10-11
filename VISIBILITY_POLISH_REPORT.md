# NPC visibility and visual-only environment pass

Backup: `C:\Users\Hp\AppData\Local\Temp\opencode\Gamify-before-visibility-vegetation-20261011.zip`.

## Reproduced cause

SampleScene contains four authored opponents, all referenced by RaceDirector.
All four runtime roots/models are active, mesh renderers enabled, shaders valid,
and root Y remains 0.3m. Kinematic/gravity-free transform motion is the existing
architecture; it was not changed. Camera renders all layers, FOV 58°, clipping
0.3–1000m; it was not defective. No missing-model/null-reference cause found.

At first question player Z=38.5, bots Z=26.5/18.5. Camera follows 6.8m behind
player: the entire stopped field is behind it. During the next open segment,
player Z=207.76 at 16m/s, bot Z=159.15/142.12/181.83/184.68. All viewport depths
were negative (-38.55/-55.07/-16.55/-13.78): they were genuinely behind the camera,
not missing renderers. Initial acceleration and reserved stopping gaps put every
bot behind; 15/16.5m/s bots could not regain the player's view, and even faster
bots required a substantial catch-up interval. Holding W makes this more pronounced.

## Minimal motion fix

Only RaceBot pacing changed: offscreen recovery begins more than 12m behind,
drives up to configurable 28m/s with 12m/s² acceleration, and commits until 12m
ahead to avoid speed-threshold oscillation. No teleport, respawn, physics, camera,
lane/following/obstacle rules changed. Normal speed returns after recovery;
recovery is disabled in the final 80m approach. Existing braking limits and
question stops remain authoritative. All opponents cannot always be visible
with a forward camera: followers and safely parked checkpoint traffic remain
behind by design. Fix restores visible competing traffic on open racing segments.

## Visual-only updates

No existing brick texture found. Created 256×320 mipmapped stylised brick texture
asset and URP Lit material; assigned to 32 existing answer-wall renderers. Cube
UV mapping was inspected; one tile maps over the 2.45×3m panel, staggered rows
with mortar and restrained variation. Existing tint/highlight, break/reset code,
wall geometry, colliders, transforms, layers and question text are untouched.

RaceEnvironment now separates meadow material from foliage, deterministically
varies tree scale/rotation, and batches bushes outside the road. No wind shader,
moving tree roots, particles or scenery colliders added. Existing lighting/skybox
remain. Visual batches saved as LivingEnvironment_* mesh assets; runtime backend
length rebuild uses the same variation. Camera/player/UI/game rules unchanged.

Changed: RaceBot.cs, RaceEnvironment.cs, new editor-only VisibilityVisualPolish.cs,
SampleScene.unity, StylisedBrick.asset, StylisedBrickWall.mat, LivingEnvironment_*
assets and Unity metadata. Screenshots under Captures.

## Verification evidence

Actual Play Mode after the initial fix: four active bots, Y=0.3m; at player Z=218,
bot 3 Z=216.99 and bot 4 Z=224.08 passed camera-frustum checks. Game View capture
`npc-visibility-fixed.png` shows both competitors ahead. Later player reached
question 2 Z=688.5, bots held at Z=661.5/668.5/676.5/668.5 with speed zero.
`brick-gates-vegetation.png` shows textured gates and vegetation while answering.
Console compilation check contains only pre-existing Unity AI subscription errors.

No manual child playtest, standalone build, live-backend check or hardware
performance benchmark. First automated regression stopped on synthetic Space
retry input; no game code changed to address it. A subsequent rerun consumed
the input correctly and passed that check. Final checks recorded separately.

No Inspector work required for SampleScene; catch-up pace settings exposed on
RaceBot. Stop positions remain intentionally behind the forward camera during
questions. Track width and spacing remain 32m and 650m.

Final committed recovery pace verified in actual Game View again: live frustum
sample found visible competition during DrivingToCheckpoint; capture
`npc-final-recovery-gameview.png` shows two competitors racing ahead. Full
eight-question regression completed: WASD/cruise/reverse, question holds/lane
selection, wrong ram/recoil/explanation/Space retry, all correct scoring, victory,
restart/no duplicate objects, rank stability, timeout and out-of-lives results.
As before, quiz/control regression isolates collision damage, then restores the
guard; collision code itself is untouched in this pass. Scene validation found
zero missing scripts/broken prefabs. No new gameplay/compile errors observed;
existing Unity AI subscription errors remain. Scene saved; Unity left in Edit Mode.

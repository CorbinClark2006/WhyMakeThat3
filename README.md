# WhyMakeThat3

A Unity project built collaboratively through GitHub.

**Repository:** https://github.com/CorbinClark2006/WhyMakeThat3

## Get the project

Install:

- Git and [Git LFS](https://git-lfs.com/).
- [Unity Hub](https://unity.com/download) and **Unity 6000.4.1f1**. The exact editor version is recorded in [ProjectVersion.txt](ProjectSettings/ProjectVersion.txt).

Then run:

```sh
git lfs install
git clone https://github.com/CorbinClark2006/WhyMakeThat3.git
cd WhyMakeThat3
git lfs pull
```

In Unity Hub, choose **Add project from disk** and select the cloned folder. Open it with the editor version above and allow Unity to import assets and restore packages. Open [ConvoyPrototype](Assets/Scenes/ConvoyPrototype.unity) and press **Play** to try the game.

## Playable prototype: DUST // REARGUARD

The home screen's **Credits** button opens **The Crew**, a palette-matched
pixel panel crediting Corbin Clark (lead development/technology), Nick Rosen
(game design/environment art), Clark Washnock (environment/pixel art),
Addie Johnson (lead art/2D art/3D modeling), Carter Scott (2D art/3D modeling)
and Roman Keliiholokai (music). **Back** returns to the home screen without
starting a game.

### Two-player browser co-op

Both players open **https://corbinclark2006.github.io/WhyMakeThat3/** on a
desktop browser with keyboard and mouse. No Unity installation or port
forwarding is needed to play.

1. Choose **Co-op**.
2. One player chooses **Driver** and clicks **Create Room**.
3. Share the displayed room code. The other player chooses **Gunner**,
   enters that code, and clicks **Join Room**.
4. The match starts once both roles are occupied. Roles are exclusive; a player
   who chooses an occupied role can select the other role without reconnecting.

Either role can create the room; letting the driver create it normally keeps
steering on the initial world-authority peer.

The online match combines chasing vans **and** road rocks. Both players can
walk, carry separate batteries, reload the shared railgun, and repair the single
accelerator rock. Health, ammunition, enemies, obstacles, speed, and steering
are shared. Only the gunner operates the railgun; only the driver steers.

Escape releases **your** controls and opens the room menu; it does not pause
the other player or the world. Switching tabs releases your controls, and
world authority can transfer to the other peer. Falling starts only that
player's three-second respawn and fade; driving continues. Browsers can suspend
background tabs entirely, so keep both game tabs visible during play. An actual
disconnect stops the match until a replacement joins the same room and vacant
role.

After the van shuts down, choose **Play Again** without leaving the room. The
first player to press it chooses **Railgun** or **Driver**; their partner
automatically receives the remaining role. Both clients reset and acknowledge
the fresh round before play resumes automatically. Health, ammo, difficulty,
road obstacles, chasers, supplies, accelerator rock, player positions, and
crew-manual learning reset together. Either player can choose next round, and
role selection survives world-authority migration. **Leave room** remains
available instead of replaying.

Photon Fusion 2.1.4 Shared Mode supplies multiplayer over secure WebSockets;
GitHub Pages hosts only the static browser build. Rooms are private, limited to
two players, and use the same fixed US region and application version so players
in different locations can find each other. The configured Fusion App ID is a
public client identifier, not a password or administrative API key. The Photon
account's free concurrent-player allowance applies; GitHub Pages does not run a
game server.

For developers, open the authored scene and use **Tools > Convoy > Build Browser
Co-op**. Install Unity's **WebGL Build Support** module first. The build is saved
to `Build/WebGL`, with gzip decompression fallback for static hosts such as
Pages. Serve it over HTTP/HTTPS, not by opening its HTML file directly. Photon
must remain enabled for this App ID in the Photon dashboard.
The build command selects the custom [Convoy WebGL template](Assets/WebGLTemplates/Convoy/index.html),
including its matching pixel loading screen, progress indicator, and fullscreen
control. Refresh both browsers after an update; replay uses application version
`convoy-web-10` and cannot join rooms from an older cached build.
The `main` branch holds the editable Unity project; the `gh-pages` branch holds
the compiled site. Pushing source edits to `main` does not rebuild the website:
rebuild WebGL and publish the contents of `Build/WebGL` to `gh-pages` to release
an update. Never put Photon account credentials or Unity license files in either
branch.

The explicit PlayMode integration test
`ConvoyCoopTests.TwoPeersShareRolesSteeringAmmoDamageRockCombatAndAuthorityHandoff`
requires internet access and verifies real-cloud shared gameplay and authority
handoff. `FusionConnectionTests` separately checks bidirectional secure
WebSocket transport. Gameplay actions use player-owned network input objects
with bounded sequenced command queues and authority acknowledgments, not the
SDK's reliable-data callback sender argument or RPCs. The world owner validates
roles, interaction reach, ammunition, cooldown, and finite/bounded player poses.
This is cooperative prototype networking, not a competitive anti-cheat system.
`ConvoyCoopTests.ReplayKeepsRoomSwapsRolesResetsEverythingAndSurvivesAuthorityHandoff`
checks consecutive same-room rounds, competing replay requests, complementary
roles, full resets, and authority handoff during selection. Replay state remains
durable Fusion network state, but is marked `UnityNonSerialized`: it is
initialized for each room, not stored as scene/prefab defaults.

### Pixel interface

Menus, role cards, room-code entry, pause/run reports, replay selection, driving
dashboard, railgun HUD and physical charge digits, repair warnings, respawn,
and crew-manual cards share a custom point-filtered 5x7 pixel font and stepped
panel borders. The palette is amber `#fbb954`, pale yellow `#f2ec8b`, rust
`#993d41`, copper `#cd683d`, plum `#45293f`, and dark plum `#2e222f`.
Hover/selected/disabled states and warnings use the same design language.
The shared UI caches bounded caption textures rather than generating them
every frame.
The startup page now contains only the title, **Solo**, **Gunner**, **Driver**,
and **Co-op**. Menus omit slogans and button subtitles; HUD labels are compact,
and tutorial cards use one short action plus one necessary follow-up. Essential
room codes, connection errors, controls, replay choice, and repair warnings
remain visible.

### Sound effects

Gameplay uses only the six assigned clips in [sound_effects](Assets/Game%20Assets/sound_effects):

| Event | Clip |
| --- | --- |
| Van driving | `van_running.wav`, looped |
| Moving air | `background_wind.mp3`, looped |
| Successful gun shot | `crossbow_fire.mp3` |
| Chaser/road-obstacle collision | `van_hit.mp3` |
| Battery or accelerator-rock pickup | `pickup_soundeffect.wav` |
| Successful ammo insertion | `insert_amo.mp3` |

The synthesized shot/impact/explosion sounds are removed. No substitute
explosion effect or background song is played. Engine and wind have separate,
quiet 2D sources and stop during menus, local pause/focus release, van shutdown,
or a stopped accelerator; repair and replay restart them without duplicate
loops. Online respawn does not stop the moving world's loops. One-shot sounds
play only on confirmed actions; failed pickups/reloads and cooldown clicks are
silent. Both co-op peers hear the same confirmed shot, impact, pickup, and load,
using the existing sequenced event ring without adding network properties.
Paused clients mute their own audio without pausing their partner.

**Configure Browser Multiplayer** and **Build Browser Co-op** assign/preload the
six files without rebuilding scene geometry. The prototype scene builder also
assigns them. Missing clips are reported as configuration errors, not replaced
by generated audio. Clip references live in the dedicated
[ConvoySoundBank](Assets/ConvoyPrototype/Resources/ConvoySoundBank.asset)
resource, preserving the authored scene's serialization layout.

### Dirt, highways, and fire repairs

Each round repeats **30 seconds of dirt, then 15 seconds of highway**, using
shared survival time. Dirt retains the three off-road tracks and escalating
rocks/bushes. Highway switches to a continuous slate asphalt surface with pale
lane dashes and shoulders, gentler suspension, and **no road obstacles**:
existing hazards clear when the highway begins, and spawning resumes on dirt
without saving up a burst. Chasing vans and difficulty progression continue.
The driver can release right mouse to carry a battery or help with repairs
during the break. A compact road label/countdown shows when the surface changes.
Surface changes are physical stretches of road: the next surface appears ahead,
its boundary scrolls toward the van, and the old surface remains behind after
crossing. The same transition works in both directions, including partial tiles
and clipped lane markings; the whole landscape no longer changes in one frame.
The 30/15-second timer still determines when the van crosses each boundary.
Future stretches account for cruise-speed stages and current accelerator speed;
passed boundary distances are retained and replicated for co-op/rejoining peers.
Road meshes reuse materials and only rebuild tiles intersected by a changing
boundary, without rebuilding nature scenery.

Every nonfatal impact creates one small animated pixel fire, alternating
available repair spots inside the cargo area and on the hood. **Jump and land
on a flame to put it out and recover one van health point.** Walking through,
standing on, falling without jumping, mounting, or respawning does not repair.
Each fire can restore only its own damage point; repairs cannot revive a
destroyed van or replace the accelerator-rock mechanic. Either player can
stomp fires. Flame/scorch sprites use the supplied palette, point filtering,
and shared cached frames without particles or dynamic lights. Their bases are
placed on the actual authored van/cargo collision surfaces, not floating over
them or buried underneath cargo boxes. The hood remains reachable using the
existing jumping route across the center console and dashboard; no ladder or
teleport is added.
A first-use `SPACE` marker teaches the repair action and retires on success.

The authority validates jump rise and landing contact; the durable fire mask
and shared health replicate together and survive authority transfer. Replay
clears fires/jump tracking and starts on dirt with full health. New network
properties remain `UnityNonSerialized` to preserve authored scene defaults.

### First-game crew manual

Local practice and online co-op have a contextual, non-modal pixel-art crew
manual. Nearby batteries, the railgun, steering wheel, and fallen accelerator
rock get a small target bracket and a two-step instruction card with keycaps
or a mouse-button diagram. Carried batteries/rocks point you toward the loading
breech/pedal; **E** appears only when the same reach and collision checks used
by the interaction say it can succeed. Movement, looking, jumping, aiming,
firing, hood-view driving, steering, and road-hazard awareness are covered too.
The driving marker sits at the edge of the driver's standing box, not on the
wheel. When the gun first empties before you've learned reloading, guidance
points to an available battery even while still in the gun sight: release
right mouse, follow the marker, and press **E** when reachable. After pickup,
the marker moves to the railgun's actual loading breech. The route remains
active after the firing lesson retires, and disappears after your successful
load; partner reloads and failed interactions do not dismiss it.

Successfully performed steps retire for **that player during this game**, with
a brief "Got it" stamp. Unfinished lessons remain available after difficulty
increases; there is no forced tutorial pause or easier simulation. Walking and
looking require actual movement; failed interactions and someone else's ammo,
shots, or repairs never complete your lessons. Shared interactions are confirmed
through the existing authority-owned, targeted event stream. Each client's
learning survives authority migration; the shared world serialization layout
is unchanged. A replacement player starts with fresh lessons.

Pause/lobby/respawn/death hide the guide without erasing learning. A new game
resets it; nothing is saved to the browser or computer. The persistent accelerator
fault warning still appears after you've learned how to repair it.

### Endless survival difficulty

Every **30 seconds of active survival** raises the cruise/chaser difficulty
stage. Road pressure ramps separately every **15 seconds**:

- Cruise speed adds **10% of the starting speed per stage** (23, 25.3, 27.6 m/s,
  and so on). Speed continues increasing instead of reaching a fixed maximum.
- The chaser limit grows by one per stage, up to **eight simultaneous vans**.
  Arrival spacing shortens from 16 seconds using `16 / (1 + 0.25 * stage)`,
  with a 2.5-second minimum. Chasers' relative approach speed scales with cruise
  speed and stage pressure, so they still catch the accelerating van. Warnings
  and retreat times shorten, but keep 1.2-second and two-second minimums.
- Driver practice and co-op start with a center-lane rock and a seven-second
  travel interval between new obstacles. Every 15 seconds, this interval follows
  `7 / (1 + 1.25 * roadStage)`: about **3.11 seconds at 15s**, **2 seconds at
  30s**, **1.17 seconds at 60s**, and **0.85 seconds from 90s onward**.
  Distance spacing scales with cruise speed, so acceleration never undoes the
  increased density. After the first 15 seconds, rocks alternate with
  **1.8-meter bush/grass clumps** using the existing
  `funky_grass_2` pixel art. Both types damage the van and loosen the accelerator
  rock exactly once on impact. Spawned obstacles cycle lanes one at a time;
  there are never rows blocking every lane.
- Spawn visibility remains at least **6.5 seconds of cruise travel**, while
  up to **eight road obstacles** can be active for a much denser late-game road.
  New obstacles arrive in staggered lanes, never an all-lane wall. Missed
  spawns after a long hitch are skipped rather than replayed as a burst. These
  object/reaction-time limits protect smoothness while cruise speed and chaser
  pressure keep increasing.

Steering is four times snappier: **12 meters/second of lateral travel at starting
speed**, crossing one 4.8-meter lane in **0.4 seconds**. Opposite input reverses
immediately rather than waiting for a slow turn. Existing speed scaling,
road-edge limits, wheel/hood controls, suspension impacts, and stopped-van
behavior remain intact.

The HUD shows survival time, difficulty, and speed. Online players share one
authority-owned clock, including after an authority transfer; local pause/tab
switching and one player's respawn do not stop it. Local practice freezes its
clock during pause and respawn. A displaced accelerator rock stops road travel,
not the survival clock; repairing it restores the **current difficulty's**
cruise speed. Van shutdown freezes the score; a fresh round resets difficulty.
Local gunner practice still has no road obstacles and driver practice still
has no chasers.

### Local practice and controls

On Play, choose **Gunner** for the existing local chase or **Driver** for driving practice. Driver practice has no chasing vans and starts on the driver's seat. Hold **right-click** within reach of the steering wheel to switch to a driving view above and behind the hood at van-local `(0, 2.4, 4.35)`, looking down eight degrees so the hood remains visible across the lower foreground; hold **A/D** to steer smoothly across the three-lane road. The 48 scrolling road tiles extend more than 900 meters ahead, with a 1,000-meter camera clipping distance. Existing authored tiles are preserved and extended rather than rebuilding the van or cargo. Releasing right-click immediately restores the third-person camera and WASD walking. Forward speed stays automatic, and lateral steering stops when the van reaches either road edge. Escape/focus pauses release the wheel. The main imported truck remains intact; a separate graybox steering wheel marks the interaction point.

A third-person prototype with a tiny, 0.2-meter-tall sprite character inside a full-size van and a close, 0.6-meter follow camera. Jump across cargo boxes, explore the cabin, and defend the open rear doors with a mounted railgun. The van stays near the origin while three road lanes, dividers, and roadside scenery scroll backward.

Player 1 uses the Beaddy sprites in `Assets/main_characters/player1`; selecting Player 2 uses the Gleif sprites in `Assets/main_characters/player2`. Both share the same front/back idle, two-frame walking, jumping, and camera-facing behavior, with point-filtered unlit sprites and the same character size. The role-specific visual replacement does not change movement, collision, carrying, or driving controls.

Roadside scenery uses all nine images in `Assets/Game Assets/nature`: `Tree1`, three rocks, three grass clumps, and two mushrooms. Upright camera-facing sprites use point filtering, transparent edges, and an unlit material to preserve their pixel-art colors. Their pivots align to the lowest visible pixel row, excluding transparent bottom padding so every prop rests on the field surface. Each scrolling tile has 400 grass clumps, 32 trees, 12 rocks, and four mushrooms spread across both fields, outside the road and markers. Across 48 tiles this provides 19,200 grass clumps and 1,536 trees. Trees are 12-20 meters tall, rocks are 1-2.5 meters tall, and grass is 0.8-1.5 meters tall. The old primitive scrub trees and rocks are removed. **Tools > Convoy > Apply Roadside Nature Sprites** updates only scenery and sprite references without rebuilding the van, cargo, or road lanes.

For smooth scrolling, field props are baked into nine meshes per road tile (432 renderers instead of 21,504 individual sprite renderers). The billboard shader turns each sprite toward the current camera on the GPU, with alpha clipping and depth writes to avoid sorting thousands of overlapping transparent objects. All 21,504 placements, original textures, ground pivots, sizes, and the full road view remain. Placement data stays editable on each `ConvoyNatureBatch`; scene meshes are saved under `Assets/ConvoyPrototype/NatureMeshes`. Interactive road obstacles still use separate objects and the existing swept damage checks.

| Input | Action |
| --- | --- |
| WASD / mouse | Move relative to the camera / orbit around the character |
| Space | Jump onto cargo boxes and the gunner stool |
| Hold right mouse near the stool (Player 1) | Mount the rear railgun and enter its optic |
| Hold right mouse near the steering wheel (Player 2) | Use the driver camera; A/D steers while held |
| E near an ammo pack | Pick it up and keep carrying it without holding a button; carrying prevents mounting |
| E while carrying near the gun's loading socket | Automatically load four more shots; away from the gun, keep carrying |
| E near the fallen accelerator rock (either player) | Pick up the rock; carrying it prevents using the gun or wheel and picking up ammo |
| E while carrying the rock near the accelerator | Put it back, accelerate in one second, and outrun pursuers for eight seconds |
| Mouse while mounted | Aim within the rear gun's traverse limits |
| Left mouse while mounted | Fire one loaded shot; the railgun recharges for 0.8 seconds |
| Release right mouse | Leave the optic or wheel and return to walking |
| Escape | Pause local practice, or release only your controls and open the online room menu |
| Click Game view / Escape while paused locally | Resume local practice |
| Resume my controls / Escape online | Resume your online controls |
| R after the main van shuts down locally | Restart the scene with full health |

Player 2 driving practice and online co-op have road obstacles during dirt phases;
local Player 1's chase does not. The first 2.2-meter-tall rock appears 140 meters
ahead in the middle lane. Subsequent hazards cycle lanes at the escalating
road-pressure intervals described above, leaving other lanes open. Hold
right-click at the wheel and steer with A/D to avoid them. Rocks scroll at the
actual road speed and stop with the accelerator. Local practice freezes them
during pause/respawn; online play does not. A swept ground footprint prevents
fast frames from skipping contact. Each obstacle damages the van once and
loosens the accelerator, then disappears; avoided hazards disappear behind the
van. Highways clear all hazards for their full 15-second break. Five accumulated
**unrepaired** hits shut down the van; stomping a fire recovers one health point.

- The first 30 seconds of the chase start with one van 65 meters behind, allow at most two pursuers, and add another no sooner than every 16 seconds. Vans initially approach at 4.5 m/s, maintain a following gap, warn for 2.5 seconds before a slower 3 m/s ram, then retreat for six seconds before approaching again. Endless survival difficulty increases that pressure every 30 seconds.
- A ram must contact the main van's damage hitbox. It removes one of five health indicators, briefly flashes the screen, rocks the suspension, and pushes an unmounted occupant away from the struck side: a left-side hit pushes right, and a right-side hit pushes left. The larger health display drops into the top center on each hit, stays visible for five seconds after the latest hit, then fades out over 0.6 seconds. It starts hidden. The damage flash also fades instead of persisting. Falling below van-local y = -0.5 through the floor opening shows **Respawn** and a three-second countdown over black, then restores the scene's initial player position and rotation and fades the view in over 0.6 seconds. Fall velocity and impact motion reset, carried ammo drops without loading, and the chase pauses during the countdown so the van cannot take unattended damage. Escape/focus pauses also pause the countdown. Respawning does not reset van health or cannon ammo.
- A ram that reduces health to zero shuts down the main van: scrolling, walking, spawning, enemy movement, and combat stop, the gun dismounts, and the view darkens. Fire repairs can recover damage before shutdown, not afterward. Press **R** to reload the local scene and restore all five health indicators; co-op uses **Play Again**.
- The driver footwell has an accelerator and a decorative brake pedal (the brake has no gameplay behavior). A rock initially holds the accelerator down at normal road speed. Each ram shifts the rock toward the pedal edge; the third ram knocks it onto the nearby solid footwell floor and shows **! Rock off accelerator**. Road/scenery movement and distance travelled slow smoothly to a complete stop over **15 seconds**, but walking, interactions, and railgun combat remain available. Either selected role can approach with empty hands and a clear path, press **E** to carry the rock, then press **E** near the accelerator to seat it again. Successful placement accelerates to **165% of the current difficulty-stage cruise speed in one second** and starts an **eight-second escape**, measured from placement. Existing and newly spawned enemies fall back without teleporting, interrupt pending rams, and maintain their lane queues. When the window expires they resume chasing; main-van speed settles back to cruise over one second. The warning clears immediately and the rock's three-hit looseness resets. Dislodging it again cancels the boost; replay clears it. Van health and railgun ammo do not reset. Steering scales with current speed and cannot slide a stopped van sideways. Local pause and fall recovery freeze the transition and escape timer; co-op continues its shared simulation during individual pauses. The escape timer replicates separately and survives authority migration. Releasing a carried rock on pause, death, or respawn returns it to its recoverable footwell position. This is intentionally a bounded, animated prop rather than an unrestricted physics object that can become lost through the floor opening.
- Each enemy survives one railgun hit and explodes on the second. A surviving hit produces a strong, smooth backward slide without body shake or an instant position jump, interrupts its attack run, and produces an impact flash, sparks, and impact sound. For eight seconds after the hit it approaches at 1.5 m/s instead of 4.5 m/s and cannot start another ram. Backward movement respects the gap to its follower. Light armor indicators turn dark after damage; the optic flashes a hit marker on a confirmed hit.
- The cannon starts with four shots. A compact recessed counter on the gun itself shows the exact remaining count, including stacked reloads, only while using its aiming camera. Its screen sits in front of the housing to prevent surface flicker. There is no player-HUD ammo counter. Empty shots do not fire or start the cooldown. Every battery adds four shots with no magazine cap.
- Original-size graybox four-cell batteries sit on the front passenger-seat surface at van-local `(0.9, 0.9, 2.5)`. Get close with a clear path, press **E** to pick one up, and carry it behind the railgun or to the low loading socket beside the gunner stool. The battery is held 0.14 meters up and 0.16 meters forward, clear of the character's torso, with obstacle-aware carry clearance matched to its full size. Press **E** within reach with a clear path to either loading point: the carried battery disappears and the cannon gains exactly four shots. Pressing E elsewhere keeps the battery in your hands. Right-click is only for the gun optic, not carrying. The passenger supply instantly replaces a picked-up battery at the same position, rotation, and size. Waiting supply batteries do not block or shove the player; dropped batteries regain physical collision and can be picked up again. Pausing or dying releases carried ammo without loading it. Use the cargo platforms and the central opening to get past the front partition.
- Apart from the startup role selector, fall-recovery **Respawn** countdown, and accelerator-rock warning with nearby E prompts, there are no on-screen titles, impact labels, or pause messages. The aiming reticle, railgun recharge bar, numeric shot count, temporary top-center health display, and fading damage flash provide feedback. Controls are documented here instead.
- Enemy waves continue until the main van shuts down. This is a mechanics sandbox with local driver steering and accelerator-rock recovery, but no multiplayer, manual braking, progression, or final art yet.
- The chase pauses when the application loses focus and automatically resumes and recaptures the mouse when Game-view focus returns. An intentional Escape pause stays paused across focus changes; click the Game view or press Escape to resume it. PlayMode tests use the Input System's isolated `InputTestFixture`, not hardware-device disabling, so test input cannot replace or disable your real keyboard and mouse afterward.

The van uses the intact imported model; road and gameplay props use editable graybox geometry, alongside the player sprites. Tune road speed, spawn interval, and maximum pursuers on the **DUST - Rearguard Prototype** object. Movement speed and sensitivity are on **Third person gunner**. Mouse look responds directly without smoothing: walking sensitivity defaults to 0.2 degrees per mouse-delta unit, while the railgun uses 0.16 for finer aiming. The third-person camera pulls forward before the model or cargo obstruct it, then returns to its normal distance. The imported partition blocks both player and camera except at its original opening. Keep colliders enabled on new solid geometry; the camera ignores triggers. The main van's damage hitbox is a separate trigger, so it does not block interior movement or the camera. Mounting the railgun still switches to its aiming optic; releasing it restores the follow camera. **Tools > Convoy > Create Prototype Scene** rebuilds the scene from code; it replaces that scene's layout, so save custom variations separately first.

Player 1 uses the eight Beaddy sprites in [Assets/main_characters/player1](Assets/main_characters/player1), replacing the primitive avatar without changing the 0.2-meter collision capsule. Moving forward shows the back-facing two-frame walk, and moving backward shows the front-facing walk. Idle and jump poses retain the last facing direction; strafing uses that direction too. The upright sprite turns with the camera so it never becomes edge-on, respects world depth, and hides in the railgun optic or when the follow camera retracts too close. Frames use point filtering, uncompressed transparency, a bottom-center pivot, and 160 pixels per unit. The shared [Player1SpriteAvatar prefab](Assets/ConvoyPrototype/Player1SpriteAvatar.prefab) stores all frames; **Tools > Convoy > Apply Player 1 Sprites** refreshes only the avatar and its imports without rebuilding the cabin.

The main ship uses the original body mesh from
[Truck_v009.obj](Assets/Game%20Assets/in_van_assets/textures/Truck_v009.obj)
and its authored [in_van.mat](Assets/Game%20Assets/in_van_assets/textures/in_van.mat).
The export has matching dimensions but a different origin: its whole body is
uniformly scaled, rotated and translated to match the previous body within
5 mm. No source vertices are edited. The two interior pipe meshes and their
materials are retained from [truckgreybox_v008.fbx](Assets/main_van/truckgreybox_v008.fbx),
because the v009 export does not contain them. The stable
[PlayerVanV008.prefab](Assets/ConvoyPrototype/PlayerVanV008.prefab) path/GUID now
holds the v009 ship, keeping existing scene and test references compatible.
**Tools > Convoy > Replace Main Ship With v009** replaces only the main model,
preserving its transform and all separately authored gameplay objects; it
consumes the staged `Truck_v009 (1)` scene instance instead of leaving a duplicate
truck beside the road. It does not refit cargo or rebuild chasers.

Chasers still use the complete
[truckchaser_v001.fbx](Assets/Game%20Assets/truckchaser_v001.fbx) in
[PursuerVanV001.prefab](Assets/ConvoyPrototype/PursuerVanV001.prefab), with
[vann.mat](Assets/Game%20Assets/in_van_assets/textures/vann.mat) on every model
material slot, using [vanchaser_v001.png](Assets/Game%20Assets/in_van_assets/textures/vanchaser_v001.png).
Their model, hitbox, armor indicator and two-hit destruction are unchanged;
non-colliding emissive headlamps make approaching vans readable at night.
Cargo boxes, stools, railgun, lights, batteries, loading socket, driver station,
accelerator and fire-repair positions remain separate gameplay assets.

### Palette night atmosphere

The [pixel sky shader](Assets/ConvoyPrototype/Runtime/PixelNightSky.shader) uses
stepped `#2e222f`/`#45293f` bands, restrained stars and a cratered `#f2ec8b`
moon low behind the van, visible from the rear gun. Purple linear fog begins
at 55 m and reaches full density at 185 m; near threats stay clear. The sky
and fog account for the project's linear color space instead of brightening
the supplied palette into a daytime sky.

Sage moonlight and a readable ambient floor preserve textured models and
obstacle contrast. Warm cabin/cargo lights and ceiling strips illuminate
interaction areas; main headlights light the road. Every enemy has visible
headlamp faces, but only the nearest enemy in each lane within 35 m enables
its shadowless spotlight. Distant enemies use emissive lamps without extra
active lights. Nature keeps its baked batching and now receives palette fog
and a muted night tint. No bloom, volumetric fog or additional-light shadows
are required.

**Tools > Convoy > Apply Night Atmosphere** reapplies lighting/materials and
the enemy prefab without refitting cargo or changing the main ship collision.
The runtime applies the same atmosphere to solo, co-op and replay cameras.

The default spawn is at van-local `(-0.97, 0.14, -1.5)`, on solid floor beside the cracked opening. Keep space around the spawn for strafing and the 0.6-meter camera orbit; placing the tiny player beside a box can block movement and collapse the camera even without initial capsule penetration. Place edited spawns on solid model geometry, not over the opening. Player yaw initializes from the saved transform so movement and camera direction agree with the scene's spawn orientation. Collision regressions check actual character falls through the gap and support from the floor and both pipes.

The player wrapper retains the exact original transformed body/pipe collision
triangles, preserving the floor gap, pipes, partition, seat/footwell and hood
surfaces while rendering the aligned v009 body. No invisible floor or extra
collision hull is added. Cargo and gun props retain their own colliders, and the
main damage hitbox remains a non-blocking trigger. The older **Apply v008 Van
Models** menu now builds the current v009 wrapper but still performs its legacy
cargo fitting; use **Replace Main Ship With v009** for a model-only update.

Walking runs at 3.2 m/s with immediate starts, stops, and direction changes. Jumps have a short input buffer and ledge grace period, with enough height for the existing 0.3-meter box steps and 0.6-meter stacked-box rises. The smaller capsule fits tighter cargo gaps without changing your box layout. Walking and impact shoves use collision-checked substeps; cabin limits constrain the requested movement rather than teleporting the player afterward. Cargo, walls, the stool, and the gun's pedestal/body block movement.

### Prototype checks

Open **Window > General > Test Runner**, choose **EditMode**, and run **ConvoyPrototype.Tests**. The focused tests cover road recycling, fixed van position, impact direction, chase/ram transitions, damage-hitbox contact, fifth-hit shutdown, strong enemy slide/spacing, the exact eight-second slowdown, four-shot stackable reloads and blocked reload rejection, exactly-two-hit destruction, mounting/camera restoration, shot hit detection and cooldown, and pause behavior.

The **PlayMode** suite, **ConvoyPrototype.PlayModeTests**, checks that the saved scene starts clear of obstacles and remains non-penetrating during walking and repeated rams. It checks the intact model's partition from both sides, laser slide and effect cleanup, fading ram feedback, fifth-hit shutdown, keyboard restart, shot depletion/refill, and E pickup/carry/drop/recovery/loading. An authored-scene route test jumps through the model's partition opening, collects a passenger battery, and returns it to the loading socket; update its waypoints when intentionally changing this route. Other mechanics tests use a separate generated layout so collaborators can rearrange cargo without invalidating those timed jump routes. They also cover tiny-player scale, sprite animation, third-person camera obstruction and recovery, all three cargo-box heights and the stool, a direct 0.6-meter box jump, responsive starts/stops and mouse look, mounting, collisions against cargo/the model/the gun pedestal, live chase updates and rams, and two aimed shots across a recharge. EditMode checks assert that the main body uses the original v009 OBJ mesh and authored material, retained pipes use their original FBX meshes/materials, and every original collision vertex/triangle is preserved. They also verify body alignment within 5 mm, the authored enemy texture/material on all chaser model slots, fitted chaser hitboxes, uniform scaling and removal of legacy graybox shell parts. Night tests check shader compilation, palette fog, non-colliding headlamps and bounded active spotlights. Escape tests measure one-second acceleration, the eight-second boundary, ram interruption, lane spacing, pause, oversized steps and resets. A real-cloud PlayMode test performs remote E pickup/placement, verifies actual enemy retreat, transfers authority mid-escape and checks expiry and resumed pursuit. Test fixtures isolate virtual input from editor focus routing and restore the previous settings afterward. Do not edit scripts or interact with the Game view while these tests are running.

## Work together

Anyone can clone this public repository. To push branches directly, the owner must invite your GitHub account through **Settings > Collaborators**, and you must accept the invitation. Public visibility alone does not grant push access.

For each change, start from an up-to-date `main` and use a separate branch:

```sh
git switch main
git pull --ff-only origin main
git switch -c feature/describe-your-change
```

Make and save your changes in Unity, then inspect and commit them:

```sh
git status
git add Assets Packages ProjectSettings
git diff --cached --stat
git commit -m "Describe your change"
git push -u origin feature/describe-your-change
```

Stage any other intentional files, such as documentation, separately. Open a pull request on GitHub, review it together, and merge it into `main`. Everyone can then pull the merged changes. Without collaborator access, fork the repository, push your branch to your fork, and open a pull request back here.

### Unity collaboration rules

- Commit assets and their `.meta` files together; move and rename assets inside Unity to preserve references.
- Keep [Assets](Assets), [Packages](Packages), and [ProjectSettings](ProjectSettings) in version control. Commit package manifest and lockfile changes together.
- Do not commit generated caches, builds, local settings, credentials, or API keys. The [ignore rules](.gitignore) exclude Unity's generated directories.
- Save and close Unity before pulling changes that affect scenes, packages, or project settings, then reopen it afterward.
- Coordinate before editing the same scene or prefab. Prefer separate scenes and prefabs to reduce merge conflicts.
- Keep text serialization and visible metadata enabled. Do not resolve scene, prefab, or `.meta` conflicts by blindly taking one side.
- Git LFS is configured for binary assets through [.gitattributes](.gitattributes). Every contributor needs Git LFS installed before adding binary assets.
- Only upload assets and code you have permission to publish publicly.

## Optional: MCP for Unity

[MCP for Unity](https://github.com/CoplayDev/unity-mcp) is included in the Unity package dependencies. It is not required for normal editing or play mode.

To use it with VS Code:

1. Install [uv](https://docs.astral.sh/uv/getting-started/installation/) and ensure `uvx` is on your PATH.
2. Open the project in Unity and wait for packages to finish importing.
3. Open **Window > MCP for Unity**. Select **stdio** transport and start the Unity bridge if it is stopped.
4. Open this folder in VS Code. Use **MCP: List Servers**, select `unityMCP`, and start/trust it when prompted.

The workspace configuration is in [.vscode/mcp.json](.vscode/mcp.json). Both the Unity package and the server follow upstream `main`; the Unity lockfile records the resolved package revision. Avoid configuring a second user-level `unityMCP` server for the same workspace.

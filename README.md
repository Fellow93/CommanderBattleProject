# Commander Battle

Bannerlord multiplayer game mode in the style of Warband Napoleonic Wars commander battles:

- Attackers vs Defenders, stock cultures (`CultureTeam1` / `CultureTeam2`) and Commander Battle's own troop classes (see "Troop types"). Vlandia has its own classes so far; the other cultures still use the stock multiplayer classes.
- Each side is an army of 8 formations, around 400 troops. **How many formations of each class (infantry, ranged, cavalry, horse archers) a side has depends on its culture** (`CommanderBattleSettings`):

  | Culture | Infantry | Ranged | Cavalry | Horse archers |
  |---|---|---|---|---|
  | Vlandia | 3 | 2 | 3 | 0 |
  | Empire | 3 | 2 | 2 | 1 |
  | Khuzait | 2 | 2 | 2 | 2 |
  | Aserai | 3 | 3 | 1 | 1 |
  | Sturgia | 5 | 1 | 2 | 0 |
  | Battania | 5 | 2 | 1 | 0 |

  All six cultures are converted and use their row. A culture without Commander Battle classes (one added by another module) would keep 4 / 2 / 2 / 0, with a stock horse archer class counted as cavalry. The log lists each side's split when the mission opens ("Defender (vlandia) formations: ...").
- **Which formation is which.** Slot i is the team's formation i (the order bar's number). Each class takes its own formation indexes first (infantry 1 and 6, ranged 2 and 5, cavalry 3, 7 and 8, horse archers 4, counting from 1), and a class with more formations than that takes the lowest free ones. Vlandia: infantry 1, 4, 6; ranged 2, 5; cavalry 3, 7, 8. Khuzait: horse archers 4 and 8. The server and the clients derive it from the cultures alone.
- Each formation has a class (infantry, ranged...), a **weight** (light, medium or heavy: one multiplayer class each) and a **weapon setup** (a perk of that class). A commander picks them on Commander Battle's troop selection screen; the stock class loadout is gone. AI-led formations spread over the weights and setups, e.g. heavy spear and shield, medium sword and shield, light pike. The formation list (N) shows every formation's troops, weapon setup and count, a pick waiting for the next round, and the side's total.
- **The weight sets the formation's size; the weapon setup doesn't.** Each formation has a budget: 54 men for infantry and ranged, 44 for cavalry and horse archers (`CommanderBattleSettings.FootFormationBudget` / `MountedFormationBudget`). Mounted formations get less because every rider brings a horse, a second agent for the server. The budget is what a formation fields with a class of its role's average size; a heavier class fields fewer men, a lighter one more:
  - men = budget × stock count of the class / average stock count of the culture's classes of that role, rounded, at least 1;
  - the stock count is the class's Captain-mode troop count for 100 bots (`MPPerkObject.GetTroopCount`: `ceil(100 × multiplier)`, the multiplier from `Module/ModuleData/cb_mpclassdivisions_<culture>.xml`, or Native's `mpclassdivisions.xml` for the stock classes);
  - the average is taken over the role's classes, because cavalry multipliers sit far below infantry ones.

  A side's total therefore varies with its picks. With Vlandia's default AI picks it is 499: infantry 47 dismounted knights, 61 men-at-arms, 79 levy spearmen; ranged 51 crossbowmen-at-arms, 74 levy crossbowmen; cavalry 50 knights, 62 mounted sergeants, 75 light horsemen. All heavy gives Vlandia about 390, all light about 610. The formula lives in `CommanderRoles` (`GetTroopCount`), shared by the server and the selection screen's preview.
- **Counts are set per round.** The server works them out when preparation starts and again whenever a class or commander changes during preparation. They freeze when the armies spawn. Clients get the counts with the round's layout seed (`SpawnLayoutSeed`). The server's log lists every formation's class, weight, weapon setup, stock count, budget and count, and each side's total, once per round ("Troop counts for round N").
- Players take command of a formation on the troop screen's first step (by class) or from the formation list (N). The team AI leads every formation nobody commands, with a singleplayer-style field battle tactic.
- While the order bar is open, the third-person camera rises and pulls back from the commander (about 3 m up and 7 m back), so the formation and the ground ahead are in view. The mouse wheel brings it in or out, up to about 15 m up and 35 m back, and the choice is kept for the session. It falls back when the bar closes. This runs only on the client, and the commander keeps control of their soldier (`CommanderBattleOrderCameraView`, after RTS Camera's elevated camera by Li Zhenhuan, MIT). It does nothing in first person.
- While you hold Left Alt, a panel on the left shows what your side's AI is doing (`CommanderBattleAIStatusSync` on the server sends `SideAIStatus` to that side's players, `CommanderBattleAIStatusView` draws it). It shows the AI's tactic, and for every formation its troops, behaviour, order and the metres it still has to go. The formation list (N) shows each AI-led formation's behaviour as well. With Left Alt held, a marker also shows where each AI-led formation is heading: the move order's position, or the formation it is charging. The tactic's main formation is marked "(main)": the AI's archers and cavalry take their places from it. When every infantry formation is player-led, that can be a player's. A player who leads it gets a highlighted notice in the panel and a message, and another message when it stops being theirs.
- The round goes to the last side with soldiers standing. If time runs out with both armies still in the field, the defenders win: the attackers have to break them within the time limit.
- No flags, morale or gold. A dead commander can take over one of their own soldiers, the same way as in Captain mode.
- Giving orders counts as activity for the stock inactivity kick (`CommanderBattleActivityLogic`). The game kicks a player whose soldier hasn't moved or looked around for 3 minutes, which a commander leading from the order bar easily reaches. A change in the orders of the commander's formation (movement, facing, arrangement, form, firing, riding) resets the timer, so only a player who is really away is kicked.
- Horses run at 85% of the stock multiplayer top speed (`CommanderBattleSettings.HorseSpeedFactor`, applied by `CommanderBattleAgentStatCalculateModel` on the server and every client alike).
- Fleeing soldiers leave the battle once they come within 25 m of the map edge, or after fleeing for 2 minutes (`CommanderBattleFleeingLogic`). The stock rule only lets them go within a few metres, which on the campaign maps they often never reach, so a round could not end.

## Troop types

Commander Battle's troop classes replace the stock multiplayer classes (plan: `docs/plans/troop-types.md`).

- **Data.** One pair of files per culture in `Module/ModuleData/`:
  - `cb_mpclassdivisions_<culture>.xml`: one `MPClassDivision` per weight of a formation class;
  - `cb_mpcharacters_<culture>.xml`: a hero character for the commander and a troop character per class.

  A file per culture keeps one culture's bad node from silently dropping the others' entries. `SubModule.xml` merges each file into Native's lists for `MultiplayerGame`. The build copies every `cb_*.xml` to the game and the dedicated server. Gear comes only from Native's `mpitems.xml`, the only items multiplayer loads.
- **Override in place.** A culture's first 7 classes take over its 7 stock class ids (`_replaceWhileMerging="true"`), so they keep the stock class indexes; further classes get `cb_` ids and come after them. Every class of a culture is then one of ours. Up to 16 per culture (the troop index is 4 bits). This also changes the classes of every other multiplayer mode on a client that loads Commander Battle.
- **A converted culture** has three things:
  - its pair of files;
  - two `XmlNode`s in `SubModule.xml`;
  - an entry in `CommanderBattleSettings.ConvertedCultures`, which turns on its split.

  All six stock cultures are converted.
- **Load check.** A bad node makes the game drop the rest of that XML file without a word. The CommanderBattle log lists every culture's classes when a mission opens ("Classes for vlandia: 8: ..." with each class's role, weight, multiplier and number of weapon setups), on the server and on each client.
- **Weights.** Each class of a role is one weight. The label comes from the multipliers (`CommanderRoles.GetWeightLabel`): within a culture's classes of one role, sorted by multiplier, the one that fields the most men is light and the one that fields the fewest heavy; three classes are light, medium and heavy, two are light and heavy, a single one has no weight label. Gear tiers: light is the levy (padded or gambeson armour, a real helmet, a decent shield, a proper weapon), medium the common men-at-arms (mail, a good helmet), heavy the dismounted knights and knights (the culture's best armour and weapons).
- **Weapon setups** are the class's only perk list (`perk_list="1"`, list 0 in code), all `game_mode="all"` (the server's GameType is `CommanderBattle`, so stock `Captain` perks never apply). The first setup is the class's own loadout; the others swap weapon slots with `AlternativeEquipmentOnSpawn`. Every weight of a role offers the role's setups in the same order. They add no troops. A setup without a shield (pike, voulge, cavalry spear) puts a sidearm in the shield slot and clears the old sidearm slot (an `AlternativeEquipmentOnSpawn` without `item` empties the slot).
- **AI-led formations** take the role's weights heaviest first (3 infantry: heavy, medium, light, then round again) and a setup by their place in the role (`AiWeaponOrder`: infantry spear and shield, sword and shield, the long polearm, the shock setup; ranged the heavier setup first; cavalry lance and shield, the sidearm and shield setup, the spear). The AI spawner applies the setup's equipment itself (`IReadOnlyPerkObject.GetAlternativeEquipments`), with no perk handler, so no combat perks. A formation keeps its last class and setup when its commander leaves.

Vlandia (index = class index, `x` = multiplier):

| # | Id | Troops | Role, weight | x | Armour (troop rosters) | Weapon setups (first = own loadout) |
|---|---|---|---|---|---|---|
| 0 | `mp_light_infantry_vlandia` | Levy Spearman | Infantry, light | 0.85 | gambeson or padded coat, kettle or cervelliere over padding, boots | spear + kite shield + short sword / pike + short sword / arming sword + shield / voulge + short sword |
| 1 | `mp_shock_infantry_vlandia` | Man-at-Arms | Infantry, medium | 0.65 | mail shirt, hauberk or chainmail, kettle hat or nasal helmet over mail, mail chausses | spear + heater or kite shield + arming sword / pike / long sword + shield / voulge |
| 2 | `mp_heavy_infantry_vlandia` | Dismounted Knight | Infantry, heavy | 0.50 | coat of plates, white coat over mail or scale, full helm, visored or faceguard helmet, mail mittens | spear + tall heater shield + long sword / pike / long sword + shield / voulge |
| 3 | `mp_light_ranged_vlandia` | Levy Crossbowman | Ranged, light | 0.68 | aketon or padded coat, kettle helmet over padding | light crossbow + short sword / arbalest + pavise |
| 4 | `mp_heavy_ranged_vlandia` | Crossbowman-at-Arms | Ranged, heavy | 0.46 | mail shirt, hauberk or banded mail, kettle helmet over mail | crossbow + long sword / arbalest + pavise |
| 5 | `mp_light_cavalry_vlandia` | Light Horseman | Cavalry, light | 0.46 | gambeson or leather scale, nasal helmet, fast horse, light harness | lance + rider's shield + arming sword / cavalry spear + sword / cavalry mace + shield |
| 6 | `mp_heavy_cavalry_vlandia` | Knight | Cavalry, heavy | 0.30 | coat of plates, white coat over mail or scale, faceguard or visored helmet, warhorse, chain or mail-and-plate barding | heavy lance + horseman's kite shield + cavalry sword / cavalry spear + sword / mace + shield |
| 7 | `cb_medium_cavalry_vlandia` | Mounted Sergeant | Cavalry, medium | 0.38 | chainmail, hauberk or mail shirt, nasal or peaked helmet over mail, horse, half-chain barding | lance + horseman's kite shield + cavalry sword / cavalry spear + sword / mace + shield |

**The selection screen** (`CommanderBattle.View/TroopSelectionView.cs`, `TroopSelectionVM.cs`, `Module/GUI/Prefabs/CommanderBattleTroopSelection.xml`), in three steps with Back on the second and third:
1. **Formation class:** a card per class the culture fields (horse archers only where it has them), with its icon and "N of M free"; yours is marked. Picking another class asks the server for a free formation of it (`RequestRoleCommand`) and moves on once the server hands one over; a class with no free formation can't be picked. If the server refuses, the screen stops waiting after 3 seconds and the chat says why.
2. **Weight:** the class's weights as troop rows, light to heavy, each with the men it would field, its armour and speed, and marks for what is fielded now and next round.
3. **Weapon setup:** the chosen weight's setups with their perk icons and descriptions, then Confirm. Confirm sends `RequestTroopSelection` (class index and the setup index).

- It opens at step 1 when you join a side, on top of the formation list. The server has already put you in a free formation (preferring your last class), so you always lead one and spawn even if you close the screen; step 1 shows it as yours, and you keep it or switch class.
- It opens at step 2 when you take a formation from the formation list (once the server confirms it), and from the "Troops" button on your own row there.
- Esc or Cancel closes it.

**On the server** (`MissionMultiplayerCommanderBattle`):
- **Taking a formation** (from the list or by class) needs a free formation of your side, before you have spawned this round. By class, the server takes the lowest free formation of that class; you keep yours if it already is of that class. A commander who takes another formation leaves their old one to the AI. The new commander takes the formation over as it stands, class and weapon setup, so its size doesn't change until they pick.
- **A pick** is checked: the player commands a formation, the class is of the formation's role, and the setup belongs to that class.
- **Before this round's armies spawn** (preparation), the pick applies at once: the formation's class and setup, the commander's troop index (`UpdateSelectedTroopIndex`) and perks (`MissionPeer.SelectPerk`, then the stock `SyncPerksForCurrentlySelectedTroop` to every client). The commander's preparation visuals are rebuilt, and the counts and layout follow.
- **After that**, the pick is stored as the formation's pending selection, shown to every client (`FormationSlotPendingSelection`, also sent to players who join), and applied when the next round's preparation starts, before the counts are worked out.
- A pending pick stays with the formation if its commander leaves: the AI fields that class and setup next round.
- Every formation's class and setup reach the clients with `FormationSlotUpdate`. The log records each pick and each applied class ("... now fields ... (Pike)").

## How it works

| Piece | File |
|---|---|
| Game type registration (`CommanderBattle`) | `CommanderBattle/CommanderBattleMultiplayerGameMode.cs` |
| Mission (stock components + the ones below) | `CommanderBattle/CommanderBattleMissions.cs` |
| Server rules: teams, formation commanders, round end | `CommanderBattle/MissionMultiplayerCommanderBattle.cs` |
| Formation slots and who commands them | `CommanderBattle/FormationSlots.cs`, `CommanderBattle/Network/*.cs` |
| Per-culture split of the formations and the slot-to-formation mapping | `CommanderBattle/CommanderBattleSettings.cs` |
| Troop types: classes, characters, the role mapping, weight labels, weapon setups and the troop count formula | `Module/ModuleData/cb_mpclassdivisions_<culture>.xml`, `cb_mpcharacters_<culture>.xml`, `CommanderBattle/CommanderRole.cs` |
| Troop selection: taking a formation by class, the pick, pending pick, applying it, AI weights and setups | `CommanderBattle/Network/RequestRoleCommand.cs`, `RequestTroopSelection.cs`, `FormationSlotPendingSelection.cs`, `CommanderBattle/MissionMultiplayerCommanderBattle.cs` |
| Spawning: one army per side, laid out in formation | `CommanderBattle/CommanderBattleSpawningBehavior.cs`, `CommanderBattleSpawnLayout.cs` |
| Team AI for the formations nobody commands | `CommanderBattle/AI/*.cs` |
| Client: HUD strength bar, compass, scoreboard bot counts | `CommanderBattle/MissionMultiplayerCommanderBattleClient.cs` |
| Scoreboard (TAB): the stock Captain columns plus "Formation", the formation each player leads with its weight and weapon setup | `CommanderBattle/CommanderBattleScoreboardData.cs`, `Module/ModuleData/commander_battle_strings.xml` |
| Client views (HUD, order bar, lifted order camera, scoreboard, formation list, troop selection, attacker/defender banner and team labels, AI status panel and destination markers) | `CommanderBattle.View/*.cs` |

The client reports itself as `Captain` to the stock UI, so you get the bot counts on the scoreboard, the troop preloading and the order bar. The stock class loadout is left out of the view list: spawning doesn't depend on it, because the perk handshake the spawner waits on (`TeamInitialPerkInfoReady`) is sent by `MissionPeer` itself when its culture is set and handled by `MissionLobbyEquipmentNetworkComponent`, a mission behaviour.

The server marks the mission as a field battle (`Mission.MissionTeamAIType`), as singleplayer battles are, so the singleplayer formation behaviours the team AI uses, and mods built on them such as RBM's AI, behave as in a field battle.

## RBM (RBM.Multiplayer)

`RBM.Multiplayer` is a separate multiplayer module that brings RBM (Realistic Battle Mod) to Commander Battle. It is built from RBM's own source in the sibling `RealisticBattleProject` repo (`RbmSourceDir`), without changing that source.

What runs in Commander Battle:

- **RBM AI**: RBM's field-battle AI leads the formations nobody commands:
  - its own tactics (split infantry and archers, embolon, hold choke, reserve plug, hedgehog...) and RBM's tactic choice by culture and terrain, in place of `CommanderBattleTactic`;
  - RBM's own behaviours on every formation;
  - AI reactions;
  - the terrain read, done each round once both armies stand on the field;
  - the formation and agent AI patches (Frontline, formation movement and spacing, AI combat parameters).
- **Frontline** runs for AI-led and player-led infantry and ranged formations of more than 25 men on a charge: men step back, find an ally, flank or close up instead of all piling in.
  - Frontline holds soldiers in place with a server-only hold and releases them many times a second, partly from worker threads. On a server, the stock release sends every client a message, about 900 a second in melee, for holds the clients never had. `RBM.Multiplayer/AI/TargetFrameClearSync.cs` sends the release only for agents whose hold reached the clients (`SetTargetPositionSynched` / `SetTargetPositionAndDirectionSynched`), and off the main thread it waits for the next main-thread tick.
  - The 30 s formation log shows, for each infantry and ranged formation, how many men are deciding (attack, back step, find ally, flank, rest), closing up and held, or "too small for Frontline". A "Frontline this round" line counts obstacle probes, rejected steps and hold releases sent.
- **Archers defend themselves** (`RBM.Multiplayer/AI/ArcherCloseCombatLogic.cs`):
  - RBM's weapon scores for ranged formations keep the bow out at almost any range. Its formation movement also keeps every soldier on a move order in his slot.
  - So an AI archer on foot with an enemy within 4 m switches to vanilla's sword-versus-bow scores. He takes the nearest enemy as his target when the engine sticks to a far one, and leaves his slot.
  - RBM's behaviour returns 3 s after the last enemy is gone. The log's "Archer close combat" lines count them.
- **RBM Stance**: posture, stamina and AI kick/bash, simulated on the server. Players see RBM's posture/stamina bars and its crush-through and chamber-block posture-break messages (`StanceHudUpdate` / `StanceEvent`, `RBM.Multiplayer/Network`), and the first-person exhaustion effect with RBM's exertion grunt. RBM's stagger, tired and kick/bash fall animations are replayed on every client (`StanceAnimation`).
- **Settings**: RBM's defaults, the same on the server and every client. Your singleplayer RBM config is never read. RBM Combat is off (see below).
- **Damage curve** (`RBM.Multiplayer/DamageCurve.cs`): a light stand-in for RBM Combat. Every blow's final damage is first cut by 50% (`scaled = damage × 0.5`), then becomes `scaled / (1 + scaled / 300)`, so the harder a hit, the more it loses:

  | Damage | 25 | 50 | 75 | 100 | 150 | 200 |
  |---|---|---|---|---|---|---|
  | Becomes | 12 | 23 | 33 | 43 | 60 | 75 |

  `DamageCurve.Scale` (0.5) is the flat cut. `DamageCurve.Softness` (300) sets the strength of the curve: the scaled damage at which a blow is halved.

How it fits together:

| Piece | File |
|---|---|
| Entry point: settings, patching, mission behaviours | `RBM.Multiplayer/RbmMpSubModule.cs` |
| RBM's patches, one class at a time, on the dedicated server only | `RBM.Multiplayer/RbmMpPatcher.cs` |
| Per-round reset of RBM's state, tactics and terrain read; stat rebuild when a commander takes over a soldier | `RBM.Multiplayer/RbmMpServerLogic.cs` |
| RBM's tactics on Commander Battle's teams | `RBM.Multiplayer/RbmMpTactics.cs` |
| Keeps commanders' formations out of the AI's regrouping | `RBM.Multiplayer/FormationOwnershipGuard.cs` |
| Keeps RBM's stat changes off player-controlled agents | `RBM.Multiplayer/PlayerStatGuard.cs` |
| Posture HUD values and events, server and client | `RBM.Multiplayer/Stance/*.cs` |
| Stand-ins for the singleplayer types RBM's source mentions | `RBM.Multiplayer/Stubs/*.cs` |
| Posture bars and exhaustion effect (client only) | `RBM.Multiplayer.View/*.cs` |

Notes:

- **The AI regroups its own soldiers.** As in singleplayer, RBM's tactics merge the AI-led formations of a kind and split flanking parties off them. A formation a player commands is never touched: no soldier moves into or out of it, and its commander keeps exactly their own men. A formation whose commander leaves goes back to the AI and may be regrouped. Every round starts again from Commander Battle's formations.
- **Commander Battle without RBM** still uses its own `CommanderBattleTactic`. Commander Battle only leaves it out when the `RBM.Multiplayer` module is active.
- **Bots only for stat changes.** Driven properties (speeds, AI values) are computed separately on each machine and never synced. RBM's stat changes, including stamina's effect on speed, therefore apply only to the soldiers the AI controls. A player's agent keeps the stock values their client predicts with.
- **RBM Combat is not included yet.** RBM's damage and armour model expects RBM's item stats, and multiplayer uses its own `mp_` items. On those, RBM's formulas would leave arrows, bolts and horse charges doing almost no damage. Combat needs an RBM-style multiplayer item set first.
- **Stubs.** RBM's AI and Stance files compile against small stand-ins for the singleplayer types they reference. These are mostly campaign types (`Stubs/CampaignStubs.cs`), plus a few RBM UI classes left out of this module (`Stubs/RbmStubs.cs`). If RBM starts using another one, the build names it.
- **Log.** `C:\ProgramData\Mount and Blade II Bannerlord\logs\RBM.Multiplayer_<pid>.log` records the patch summary and anything that failed. With the detailed log on (see "Logs") it also records the per-round resets, tactics, the terrain read, the formation status and the Frontline and archer counters.

## Build

Requires the .NET SDK and the .NET Framework 4.7.2 targeting pack. RBM.Multiplayer also needs RBM's source at `<game>\Modules\RealisticBattleProject` (override with `-p:RbmSourceDir=X:\path\`).

```bash
dotnet build "CommanderBattle.sln" -c Release
```

The build installs:

- `<game>\Modules\CommanderBattle\`: `SubModule.xml`, `ModuleData\` (strings and the troop type XML), `GUI\Prefabs\`, `bin\Win64_Shipping_Client\CommanderBattle.dll` + `CommanderBattle.View.dll`, `bin\Win64_Shipping_Server\CommanderBattle.dll`
- `<game>\Modules\RBM.Multiplayer\`: `SubModule.xml`, `GUI\` (RBM's posture HUD), `bin\Win64_Shipping_Client\RBM.Multiplayer.dll` + `RBM.Multiplayer.View.dll` + `0Harmony.dll`, `bin\Win64_Shipping_Server\RBM.Multiplayer.dll` + `0Harmony.dll`
- the server builds of both modules into the dedicated server's `Modules` folder, when the dedicated server is installed next to the game.

The game folder is taken from the repo location. Override it with `-p:BannerlordDir=X:\path\to\game\`.

## Maps

The armies start where a singleplayer field battle of this size would place them on the same scene. `CommanderBattleSpawnLayout` runs the stock deployment planner on the scene's spawn path: the singleplayer distance between the armies for both armies' troops (up to the 1,000-troop reference), infantry in front, ranged behind, cavalry on the left and right flanks, horse archers on the right flank (the planner's HorseArcher class, as in singleplayer). It then shares each block out between the formations of that role, by their troop counts. The roles per formation follow the side's culture, which the server and clients both know, so they compute the same layout.

Each round, every army starts somewhere else in its spawn zone. The server picks a seed at the start of the round's preparation and sends it to every client with the round's troop counts (`SpawnLayoutSeed`; again when a count changes during preparation, and to players who join). All of them derive the same changes from it:
- The spawn path the armies stand on: one of the scene's `spawn_path_NN` paths, with the armies' meeting point 37–63% along it, as singleplayer picks it. The stock pick is random on each machine, so the server and the clients disagreed; it is made from the seed instead, and the log names the path ("Spawn path ...").
- The army moves up to 40 m sideways, 40 m back or 15 m forward, and turns up to 10 degrees.
- The formations of a role swap places.
- If that would put a formation outside the map boundary, a smaller move is used, down to none.

The amounts are in `CommanderBattleSettings` (`SpawnLateralJitter`, `SpawnBackJitter`, `SpawnForwardJitter`, `SpawnTurnJitterDegrees`), and the log records each side's move.

Multiplayer only loads scenes from multiplayer modules. `tools\Install-CampaignScenes.ps1` links the campaign's field-battle scenes into `Modules\CommanderBattle\SceneObj`, for both the game and the dedicated server. It also writes `Modules\Native\ds_config_commander_campaign.txt` for the server, with 2 rounds per map. After each match the players vote for the next map from these scenes (`enable_map_voting`; the game offers at most 100), and map-change polls are allowed (`AllowPollsToChangeMaps True`). The loading screen, map voting and scoreboard show each scene by a readable name. It is built from the scene's terrain, forest and features in `sp_battle_scenes.xml` ("Wooded Plains with a lake", "Steppe 7"), and the names live in `Module/ModuleData/commander_battle_strings.xml`. A scene added to the pool later needs a name there too, or it shows its id.

```bash
powershell -ExecutionPolicy Bypass -File tools/Install-CampaignScenes.ps1
```

## Run a test server

1. Install **Mount & Blade II: Dedicated Server** (Steam tool, or SteamCMD app `1863440`) next to the game, and build. The build installs both modules into it.
2. Run `tools\Install-CampaignScenes.ps1`, or write your own config in the server's `Modules\Native` folder with `GameType CommanderBattle`.
3. Start the server with `tools\Start-Server.cmd` (double-click), or from PowerShell:

   ```bash
   powershell -ExecutionPolicy Bypass -File tools/Start-Server.ps1 -Restart
   ```

   The script waits until the server hosts, then prints its process id, the map, the RBM patch line and the log files. Options:
   - `-Restart` stops a running server first; without it the script refuses to start a second one.
   - `-Build` rebuilds and installs the solution first.
   - `-NoRbm` starts without RBM Multiplayer.
   - `-Token <token>` (or the `CB_SERVER_TOKEN` environment variable) lists the server publicly.
   - `-Port`, `-TickRate` and `-Config` change the defaults (7210, 60, `ds_config_commander_campaign.txt`).
   - `-DryRun` shows what would run.

   Listing the server needs a token: run `customserver.gettoken` in the game's console (Alt+~ in the multiplayer lobby). By hand, the command is:

   ```bash
   DedicatedCustomServer.Starter.exe _MODULES_*Native*Multiplayer*CommanderBattle*RBM.Multiplayer*_MODULES_ /dedicatedcustomserverconfigfile ds_config_commander_campaign.txt /dedicatedcustomserverauthtoken <token> /tickrate 60
   ```

   Leave out `*RBM.Multiplayer` to play without RBM.
4. Each client enables **Commander Battle** (and **RBM Multiplayer**, if the server runs it) in the launcher's Multiplayer tab and joins through the custom server list. Players and the server must run the same modules.

If the server runs on your own PC or LAN and you can't join through your public address, add a rewrite to `Modules\CommanderBattle\ModuleData\server_address_overrides.txt`.

## Logs

The mod logs are in `C:\ProgramData\Mount and Blade II Bannerlord\logs\`, one file per process: `CommanderBattle_<pid>.log` and `RBM.Multiplayer_<pid>.log`. By default they hold only what matters:
- maps opening and round changes, and each round's result;
- players taking formations and picking troops;
- the RBM patch summary;
- anything that failed, and exceptions thrown in the mod's own code.

Diagnostics stay out unless the detailed log is on: spawn paths and layout, troop counts, class lists, team AI and RBM status, fleeing and Frontline counters. To turn it on, set `CommanderBattleLog.Detailed` or `RbmMpLog.Detailed` to `true` and rebuild. The test checklist items below that quote one of these lines need it.

## Test checklist

- [ ] The server starts with `GameType CommanderBattle` and appears in the custom server list.
- [ ] Both armies spawn in formation and the AI leads them into battle; the round ends when one side is wiped out.
- [ ] Taking command of a formation from the list gives you its orders; releasing it hands it back to the AI.
- [ ] The CommanderBattle log lists "Classes for vlandia: 8" with our ids (indexes 0-6 the stock ids, 7 `cb_medium_cavalry_vlandia`) and their weights (infantry light/medium/heavy, ranged light/heavy, cavalry light/heavy/medium), on the server and the client.
- [ ] The server log lists each side's split at mission open: Vlandia 3 infantry, 2 ranged, 3 cavalry (slots 0, 3, 5 infantry; 1, 4 ranged; 2, 6, 7 cavalry); an unconverted culture the 4/2/2 fallback.
- [ ] Joining a side opens the troop screen at step 1 over the formation list, with your auto-placed formation's class marked "yours". Picking another class moves you to a free formation of it and goes on to step 2; its card's free count drops. A class with no free formation can't be picked.
- [ ] Taking a formation from the list opens the screen at step 2; the "Troops" button on your row too. Back goes from step 3 to 2 and from 2 to 1. Esc and Cancel close it, Esc closes it before the formation list.
- [ ] Picking another weight during preparation changes the formation's count in the formation list (yours and other players') and spawns that many of that class. The preview count on the screen matches. A weapon setup doesn't change the count and swaps the weapons for the commander and the troops.
- [ ] A pick mid-round shows "(next: ...)" in the formation list and on the screen, and the next round spawns it (log: "... now fields ..."). The log's "Troop counts for round N" lines match the spawned formations.
- [ ] Pikemen (any weight) spawn without shields, with a sword, and their pike and brace stances work. Voulge and cavalry spear setups have no shield either. Arbalest + pavise carries a pavise.
- [ ] AI-led formations field varied weights and setups (log "Troop counts" lines and the AI formation spawn lines name them), e.g. Vlandian infantry heavy spear + shield, medium sword + shield, light pike.
- [ ] Khuzait field two horse archer formations, and Empire and Aserai one each. They deploy on the right flank, and the class screen shows a Horse archers card only for those cultures.
- [ ] A dead commander can take over a soldier in their formation.
- [ ] Opening the order bar in third person lifts the camera up and back. The mouse wheel changes the height, closing the bar brings it down, and move orders land where the screen centre points.
- [ ] Time-out with both armies still in the field: the defenders win the round.
- [ ] Routed soldiers run off the map and the round ends when the last fighting soldier of a side is gone (the log's "Fleeing" lines count them).
- [ ] Server tick rate holds with 1000-1250 soldiers. CPU is the main risk, more so with RBM's AI.
- [ ] With RBM.Multiplayer, the RBM.Multiplayer log lists the patch classes, with none failed.
- [ ] With RBM.Multiplayer, AI infantry keeps Frontline spacing (stepping in and out of the line) and AI soldiers kick and bash. In a charge, the formation log shows men deciding and held, and "Frontline this round" counts hold releases sent; no network errors or client disconnects in the melee.
- [ ] With RBM.Multiplayer, the log shows "RBM tactics registered" and "Terrain read started" each round, and the AI-led formations regroup into RBM's tactic shapes.
- [ ] With RBM.Multiplayer, the formation you command keeps exactly your soldiers while the AI regroups the others.
- [ ] With RBM.Multiplayer, your posture and stamina bars show and move when you block, and the target's bars show after an exchange.
- [ ] With RBM.Multiplayer, a soldier you take over moves at normal speed (no stamina slowdown, no rubber-banding).
- [ ] With RBM.Multiplayer, the second round starts cleanly (the log shows a reset per round).

## Known limitations

- **Our troop types reach other modes.** A client that loads Commander Battle sees our Vlandian classes in every multiplayer mode, since they replace the stock ones by id. Players and servers must run the same module version, or the class indexes disagree.
- **Saved stock perk choices.** When a player joins a side, the client sends its saved perk choices for each class id (stock behaviour). Our classes reuse the stock ids, so a saved stock pick can switch the commander to another weapon setup until they pick on the troop screen. The formation then shows and fields that setup (`SyncCommanderClasses` follows the commander's perk).
- **Formation names.** The formation list names a formation by its class and its formation number (the order bar's), so Vlandia's infantry are "Infantry 1", "Infantry 4" and "Infantry 6", and the order bar may call a Vlandian infantry formation by a cavalry or horse archer formation name.

- **RBM: terrain read hitch.** RBM builds its battlefield grid when it reads the terrain, at the start of each round. That can stall the server briefly while it does.
- **Agent cap.** The engine caps a mission at about 2047 agents, and horses count. Two armies of up to about 620 soldiers each plus the commanders fit, but cavalry-heavy armies eat into the margin.
- **RBM: remote AI agents on clients.** Clients compute stock stats for the AI soldiers they see, while the server applies RBM's. Movement and outcomes come from the server, but an AI soldier's swing may look slightly faster or slower than it really is.
- **RBM: no listen server.** RBM's AI and Stance only run on the dedicated server.

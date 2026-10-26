# Commander Battle

Bannerlord multiplayer game mode in the style of Warband Napoleonic Wars commander battles:

- Attackers vs Defenders, stock cultures (`CultureTeam1` / `CultureTeam2`) and Commander Battle's own troop classes (see "Troop types"). Vlandia has its own classes so far; the other cultures still use the stock multiplayer classes.
- Each side is an army of 8 formations, around 400 troops, plus its general and his 6 riders. **How many formations of each class (infantry, ranged, cavalry, horse archers) a side has depends on its culture** (`CommanderBattleSettings`):

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
- **The general.** On top of the 8 formations, each side has a general: a mounted hero in the culture's finest gear, leading a bodyguard of 6 mounted elite riders (`CommanderBattleSettings.GeneralBodyguardCount`). See "The general" below.
- Each formation has a class (infantry, ranged...), a **weight** (light, medium or heavy: one multiplayer class each) and a **weapon setup** (a perk of that class). A commander picks them on Commander Battle's troop selection screen; the stock class loadout is gone. AI-led formations spread over the weights and setups, e.g. heavy spear and shield, medium sword and shield, light pike. The formation list (N) shows every formation's troops, weapon setup and count, a pick waiting for the next round, and the side's total.
- **The weight sets the formation's size; the weapon setup doesn't.** Each formation has a budget: 54 men for infantry and ranged, 44 for cavalry and horse archers (`CommanderBattleSettings.FootFormationBudget` / `MountedFormationBudget`). Mounted formations get less because every rider brings a horse, a second agent for the server. The budget is what a formation fields with a class of its role's average size; a heavier class fields fewer men, a lighter one more:
  - men = budget × stock count of the class / average stock count of the culture's classes of that role, rounded, at least 1;
  - the stock count is the class's Captain-mode troop count for 100 bots (`MPPerkObject.GetTroopCount`: `ceil(100 × multiplier)`, the multiplier from `Module/ModuleData/cb_mpclassdivisions_<culture>.xml`, or Native's `mpclassdivisions.xml` for the stock classes);
  - the average is taken over the role's classes, because cavalry multipliers sit far below infantry ones.

  A side's total therefore varies with its picks. With Vlandia's default AI picks it is 499: infantry 47 dismounted knights, 61 men-at-arms, 79 levy spearmen; ranged 51 crossbowmen-at-arms, 74 levy crossbowmen; cavalry 50 knights, 62 mounted sergeants, 75 light horsemen. All heavy gives Vlandia about 390, all light about 610. The formula lives in `CommanderRoles` (`GetTroopCount`), shared by the server and the selection screen's preview.
- **Counts are set per round.** The server works them out when preparation starts and again whenever a class or commander changes during preparation. They freeze when the armies spawn. Clients get the counts with the round's layout seed (`SpawnLayoutSeed`). The server's log lists every formation's class, weight, weapon setup, stock count, budget and count, and each side's total, once per round ("Troop counts for round N").
- Players take command of a formation on the troop screen's first step (by class) or from the formation list (N). The team AI leads every formation nobody commands, with a singleplayer-style field battle tactic.
- While the order bar is open, the third-person camera rises and pulls back from the commander (about 3 m up and 7 m back), so the formation and the ground ahead are in view. The mouse wheel brings it in or out, up to about 15 m up and 35 m back, and the choice is kept for the session. It falls back when the bar closes. This runs only on the client, and the commander keeps control of their soldier (`CommanderBattleOrderCameraView`, after RTS Camera's elevated camera by Li Zhenhuan, MIT). It does nothing in first person.
- While you hold Left Alt, a panel on the left shows what your side's AI is doing (`CommanderBattleAIStatusSync` on the server sends `SideAIStatus` to that side's players, `CommanderBattleAIStatusView` draws it). It shows the AI's tactic, and for every formation its troops, behaviour, order and the metres it still has to go. The formation list (N) shows each AI-led formation's behaviour as well. With Left Alt held, a marker also shows where each AI-led formation is heading: the move order's position, or the formation it is charging. The tactic's main formation is marked "(main)": the AI's archers and cavalry take their places from it. When every infantry formation is player-led, that can be a player's. A player who leads it gets a highlighted notice in the panel and a message, and another message when it stops being theirs. The panel ends with a reminder that N opens the formation list, where formation and troops are changed.
- The round goes to the last side with soldiers standing. If time runs out with both armies still in the field, the defenders win: the attackers have to break them within the time limit.
- No flags, morale or gold. A dead commander can take over one of their own soldiers, the same way as in Captain mode: the soldier the camera follows, with the action key (F). Once the 3 s respawn period is over, the spectator card says so.
- Giving orders counts as activity for the stock inactivity kick (`CommanderBattleActivityLogic`). The game kicks a player whose soldier hasn't moved or looked around for 3 minutes, which a commander leading from the order bar easily reaches. A change in the orders of the commander's formation (movement, facing, arrangement, form, firing, riding) resets the timer, so only a player who is really away is kicked.
- **Cheer with your men** (plan: `docs/plans/cheer-with-your-men.md`). When a commander cheers (the stock cheer wheel), the AI soldiers of their own formation cheer too, each 0.1 to 1.5 s later, so it rolls through the ranks. One in three also shouts. The general's cheer reaches his riders only. A soldier joins in only if no enemy is within 20 m and he isn't attacking, blocking, aiming, reloading or switching weapons. Once a commander has cheered, a cheer within the next 10 s gets no echo. The server does it all (`MissionMultiplayerCommanderBattle.Cheer.cs`, a second handler for the stock `TauntSelected`); the engine carries the actions and voices to the clients, as it does the stock cheer. The detailed log records each one ("<name> cheered: N of <formation> join in").
- Horses run at 85% of the stock multiplayer top speed (`CommanderBattleSettings.HorseSpeedFactor`, applied by `CommanderBattleAgentStatCalculateModel` on the server and every client alike).
- Bows and crossbows fire slower: their draw (or raise) and reload run at 75% of the stock speed (`CommanderBattleSettings.BowFireSpeedFactor`, `CrossbowFireSpeedFactor`). This holds for players and AI soldiers, foot and mounted, and is applied by the same stats model on every machine.
- Fleeing soldiers leave the battle once they come within 25 m of the map edge, or after fleeing for 2 minutes (`CommanderBattleFleeingLogic`). The stock rule only lets them go within a few metres, which on the campaign maps they often never reach, so a round could not end.

## The general

Plan: `docs/plans/general-role.md` (phase 0: the general as a unit; steering the team AI comes later).

- **One per side, on top of the army.** The general and his bodyguard are the team's formation 8 (`FormationClass.General`), a 9th slot after the 8 regular ones (`CommanderBattleSettings.GeneralSlot`, `SlotCount`). Formation 8 is outside `Team.FormationsIncludingEmpty` (0-7), so stock and RBM tactics, their regrouping and the order bar leave it alone.
- **The class.** Each culture has one general's class, `cb_general_<culture>` (hero `cb_general_<culture>_hero`, troop `cb_bodyguard_<culture>_troop`), with one weapon setup. `CommanderRoles.GetRole` knows it by the `cb_general_` id prefix and gives it the role `CommanderRole.General`. Its characters stay in the stock Cavalry class group (`default_group="Cavalry"`): see "Stock class groups" below. The class is its role's only one, so it is its own reference in the troop count formula and always fields the 10 riders, whatever its multiplier (0.10). A culture without a general's class fields no general; the log says so once per mission ("... fields no general").
- **Becoming the general.** Only by choice: the formation list (N) shows the general as the last row, and the troop screen has a General card. A player who joins a side is never placed there automatically. If all 8 formations are taken and the general is free, the player stays on the side without a formation and is told to take the general from the list. The troop screen skips the weight step for the general, who has a single class.
- **A player general rides at the tip of his bodyguard's wedge.** The bodyguard uses the stock Skein arrangement: one V-shaped rank, one file per agent, three riders to a side. The general's slot is the reserved front middle, the tip. The order bar can't give formation 8 orders (stock MP order code only knows 0-7), so the server (`MissionMultiplayerCommanderBattle.Bodyguard.cs`) gives the bodyguard a Move order that puts the tip on the general. The order is renewed when he has moved 2 m or turned about 15°. The wedge points where he rides; when he stands, it keeps its last heading. The stock `Follow` order isn't used: on the move it aims a mounted formation 2 s ahead of him, across his path. A dead player general takes over one of his riders (F), who becomes the new tip.
- **The AI general.** When no player is the general, the formation spawns the class's hero once, 5 m in front of his 6 riders, and the stock `BehaviorGeneral` leads it (`CommanderBattleTeamAI.LeadAsGeneral`: the default weights plus `BehaviorGeneral`, as singleplayer sets them), which keeps it behind the main formation. A player general's formation that goes to the AI (disconnect, team change) gets the same weights.
- **Team fields** (server only): `Team.GeneralsFormation` is formation 8 and `Team.GeneralAgent` the general, player or AI, so RBM's reactions leave the general's formation out. They are cleared each round. `Team.BodyGuardFormation` stays empty: the stock team AI makes a set one charge every tick.
- **Where he stands.** On campaign scenes the general is the deployment planner's General block, which it places behind the line's centre column (`CommanderBattleSpawnLayout`); in the fixed fallback layout, 40 m behind the centre.
- **With RBM.Multiplayer**, `FormationOwnershipGuard` keeps every agent in and out of formation 8 where it is, so RBM's regrouping never pulls the general and his riders into the cavalry.
- **Stock class groups.** The stock code only knows the 4 class groups (Infantry, Ranged, Cavalry, HorseArcher). A class in any other group crashes the client when the multiplayer lobby opens (its class filter, `MPLobbyClassFilterFactionItemVM`, looks the group up in a dictionary of the 4), and overflows the mission's class restrictions (`bool[8]`). So the general's characters are in the Cavalry group. A general's class picker keeps the Cavalry group open, because `SpawnComponent.AllowEarlyAgentVisualsDespawning` refuses a commander whose group is restricted.

## General mode

Plan: `docs/plans/general-mode.md` (v1: path orders).

- **F1 enters and leaves it, for a player general only,** while his soldier is alive and the round is being fought. For everyone else F1 stays the stock order key. The general has no use for the order bar (his formation 8 only follows him), so the order UI ignores F1-F9 for him (`CommanderBattleOrderUIHandler`, the stock `MissionGauntletMultiplayerOrderUIHandler` minus its key handling for the general).
- **Camera.** The commander's order camera, lifted about three times as far: by default about 45 m up and 105 m back from the general, along the way he looks. The mouse turns it as usual (look down to see the field); it stays on the general, so there is no panning. The mouse wheel zooms (lift 50 to 300, kept for the session); it rises and falls with the same easing as the order camera. General mode switches to third person, where the lift applies, and back on leaving. The pull back stops short of the terrain, so looking up or a hill behind him brings the camera in rather than under the ground. `CommanderBattleOrderCameraView` writes the camera offsets for both views.
- **Cursor: hold Left Ctrl.** While it is held, the view stops turning (except at the screen's edge, as in the stock order view), a cursor shows, and the menu buttons, drawing and the labels take the mouse. Letting go hides the cursor and the mouse turns the view again. Letting go mid-drag ends the path as releasing the button would (the label popup comes up).
- **His soldier stands still:** his controls are off (turned off again every frame, because the stock camera turns them back on) and his movement is zeroed every frame. The bodyguard wedge stays with him. The cheer, equip and drop keys are silenced.
- **Leaving.** F1 or Esc (Esc closes the formation list or troop screen first, if open). General mode also closes, with a message, when he or his horse is hit, and silently when he dies, stops being the general, or the round ends.
- **The menu** at the top of the screen has two tools and a hint line:
  - **Path:** with Ctrl held, hold the left mouse button and drag along the ground, then type a label (1-40 characters) in the popup. Cancel throws the path away. A press shorter than 5 m is ignored. The path is sampled every few metres and thinned to 64 points.
  - **Delete:** with Ctrl held, click a path's label to remove it.
- **Who sees the orders:** the general's side only. In general mode they always show; otherwise every player of the side sees them while holding Left Alt (with the AI status panel). Each path is a yellow line on the ground with its label at the midpoint. They are screen overlays drawn over the terrain and units.
- **Lifetime:** an order lasts until the round ends or the general deletes it. A side keeps at most 10; drawing an 11th removes the oldest. A player who joins the side, or the battle, mid-round gets the side's orders.
- **Server checks** (`MissionMultiplayerCommanderBattle.GeneralOrders.cs`): the sender is the side's general, the round is in progress, at most 64 points, points outside the battlefield are dropped and fewer than 2 left is refused, and the label is cut to 40 characters with `<`, `>` and control characters taken out. The log records each order drawn and deleted ("Fellow93 (Attacker general) drew path 3 'Flank left' (17 points)", "... deleted path 3 'Flank left'").
- **Network** (`CommanderBattle/Network`): `RequestGeneralOrder` and `RequestDeleteGeneralOrder` from the general; `GeneralOrderAdded` and `GeneralOrderRemoved` to each player of his side, never broadcast. Points travel as 2D; each client takes the height from its terrain.
- **Client side:** `MissionMultiplayerCommanderBattleClient` keeps the orders by side (`GetGeneralOrders`, `OnGeneralOrdersChanged`); `CommanderBattleGeneralModeView` runs general mode and draws the orders (`GeneralModeVM.cs`, `Module/GUI/Prefabs/CommanderBattleGeneralMode.xml` and `CommanderBattleGeneralModeInput.xml`).

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
- **Weapon setups** are the class's only perk list (`perk_list="1"`, list 0 in code), all `game_mode="all"` (the server's GameType is `CommanderBattle`, so stock `Captain` perks never apply). The first setup is the class's own loadout; the others swap weapon slots with `AlternativeEquipmentOnSpawn`. Every weight of a role offers the role's setups in the same order. They add no troops. A setup without a shield (pike, two-handers) clears the shield slot (an `AlternativeEquipmentOnSpawn` without `item` empties the slot).
- **Sidearms.** Spears, pikes, the menavlion, lances and throwing weapons carry the culture's sidearm (Item2), since they fight poorly up close: Vlandian swords, the Empire's gladius or spatha (maces for its cavalry), Khuzait sabres (maces for the lancers), Aserai swords, Battanian swords, and for Sturgia a raider axe (levy), a northern sword (men-at-arms) or a two-handed maul (riders). One-handed swords, axes and maces, and swung two-handers (two-handed axes, tabar, bardiche, falx, rhomphaia, glaive, bill, voulge), carry none: their setups clear Item2. Archers and horse archers keep theirs.
- **Throwing weapons** are only Javelin-template items (Imperial javelins, jereeds, Battanian javelins), which throw by default, on foot and mounted. Not pila: `mp_pilum` is wielded as a spear (X switches to throwing), holds one per slot and can't be thrown from a horse.
- **AI-led formations** take the role's weights heaviest first (3 infantry: heavy, medium, light, then round again) and a setup by their place in the role (`AiWeaponOrder`: infantry spear and shield, sword and shield, the long polearm, the shock setup; ranged the heavier setup first; cavalry lance and shield, the sidearm and shield setup, the spear). The AI spawner applies the setup's equipment itself (`IReadOnlyPerkObject.GetAlternativeEquipments`), with no perk handler, so no combat perks. A formation keeps its last class and setup when its commander leaves.

Vlandia (index = class index, `x` = multiplier):

| # | Id | Troops | Role, weight | x | Armour (troop rosters) | Weapon setups (first = own loadout) |
|---|---|---|---|---|---|---|
| 0 | `mp_light_infantry_vlandia` | Levy Spearman | Infantry, light | 0.85 | gambeson or padded coat, kettle or cervelliere over padding, boots | spear + kite shield + short sword / pike + short sword / mace + kite shield |
| 1 | `mp_shock_infantry_vlandia` | Man-at-Arms | Infantry, medium | 0.65 | mail shirt, hauberk or chainmail, kettle hat or nasal helmet over mail, mail chausses | spear + iron-rimmed kite shield + arming sword / pike + arming sword / long sword + kite shield / bill |
| 2 | `mp_heavy_infantry_vlandia` | Dismounted Knight | Infantry, heavy | 0.50 | coat of plates, white coat over mail or scale, full helm, visored or faceguard helmet, mail mittens | spear + reinforced kite shield + long sword / pike + long sword / mace + kite shield / voulge |
| 3 | `mp_light_ranged_vlandia` | Levy Crossbowman | Ranged, light | 0.68 | aketon or padded coat, kettle helmet over padding | light crossbow + pavise + short sword / arbalest + pavise + short sword |
| 4 | `mp_heavy_ranged_vlandia` | Crossbowman-at-Arms | Ranged, heavy | 0.46 | mail shirt, hauberk or banded mail, kettle helmet over mail | crossbow + pavise + long sword / arbalest + heavy bolts + pavise + long sword |
| 5 | `mp_light_cavalry_vlandia` | Light Horseman | Cavalry, light | 0.46 | gambeson or leather scale, nasal helmet, fast horse, light harness | lance + light tall heater + arming sword / cavalry mace + shield |
| 6 | `mp_heavy_cavalry_vlandia` | Knight | Cavalry, heavy | 0.30 | coat of plates, white coat over mail or scale, faceguard or visored helmet, warhorse, chain or mail-and-plate barding | heavy lance + heavy heater + cavalry sword / mace + shield |
| 7 | `cb_medium_cavalry_vlandia` | Mounted Sergeant | Cavalry, medium | 0.38 | chainmail, hauberk or mail shirt, nasal or peaked helmet over mail, horse, half-chain barding | long lance + tall heater + cavalry sword / mace + shield |

**The selection screen** (`CommanderBattle.View/TroopSelectionView.cs`, `TroopSelectionVM.cs`, `Module/GUI/Prefabs/CommanderBattleTroopSelection.xml`), in three steps with Back on the second and third:
1. **Formation class:** a card per class the culture fields (horse archers only where it has them, and the General), with its icon and "N of M free"; yours is marked. A class with a single weight (the General) goes straight to step 3, and Back from there returns to step 1. Picking another class asks the server for a free formation of it (`RequestRoleCommand`) and moves on once the server hands one over; a class with no free formation can't be picked. If the server refuses, the screen stops waiting after 3 seconds and the chat says why.
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
| Spawning: one army per side, laid out in formation; the AI general | `CommanderBattle/CommanderBattleSpawningBehavior.cs`, `CommanderBattleSpawnLayout.cs` |
| The general: slot, role, team fields, AI weights | `CommanderBattle/CommanderBattleSettings.cs` (`GeneralSlot`), `CommanderRole.cs`, `CommanderBattleTeams.cs` (`SetGeneral`), `AI/CommanderBattleTeamAI.cs` (`LeadAsGeneral`) |
| The general's drawn orders: server checks, lifetime, sending to his side | `CommanderBattle/MissionMultiplayerCommanderBattle.GeneralOrders.cs`, `CommanderBattle/GeneralOrders.cs`, `CommanderBattle/Network/*GeneralOrder*.cs` |
| General mode: drawing, the orders on screen, the camera (lifted by the order camera view); the order UI without F1 for the general | `CommanderBattle.View/CommanderBattleGeneralModeView.cs`, `GeneralModeVM.cs`, `CommanderBattleOrderCameraView.cs`, `CommanderBattleOrderUIHandler.cs` |
| Team AI for the formations nobody commands | `CommanderBattle/AI/*.cs` |
| Client: HUD strength bar, compass, scoreboard bot counts | `CommanderBattle/MissionMultiplayerCommanderBattleClient.cs` |
| Scoreboard (TAB): the stock Captain columns plus "Formation", the formation each player leads with its weight and weapon setup | `CommanderBattle/CommanderBattleScoreboardData.cs`, `Module/ModuleData/commander_battle_strings.xml` |
| Client views (HUD, order bar, lifted order camera, scoreboard, formation list, troop selection, attacker/defender banner and team labels, AI status panel and destination markers) | `CommanderBattle.View/*.cs` |

The client reports itself as `Captain` to the stock UI, so you get the bot counts on the scoreboard, the troop preloading and the order bar. The stock class loadout is left out of the view list: spawning doesn't depend on it, because the perk handshake the spawner waits on (`TeamInitialPerkInfoReady`) is sent by `MissionPeer` itself when its culture is set and handled by `MissionLobbyEquipmentNetworkComponent`, a mission behaviour.

The server marks the mission as a field battle (`Mission.MissionTeamAIType`), as singleplayer battles are, so the singleplayer formation behaviours the team AI uses, and mods built on them such as RBM's AI, behave as in a field battle.

## Admin panel

An admin opens it from the escape menu ("Admin Panel"). To join as an admin, right-click the server in the custom server list, choose **Join as Admin**, and enter the server's admin password (`AdminPassword` in the server config). A normal join never asks for it. `tools\Install-CampaignScenes.ps1 -AdminPassword <password>` writes it. A rerun without the parameter keeps the password already there, and `-AdminPassword ''` removes it. Restart the server after changing it: the server hands the password to the lobby when it registers.

The panel shows Commander Battle's own groups in place of the stock ones. The stock groups had options this mode doesn't use, such as warmup, class toggles and formation markers, and their map list filtered out the campaign scenes.
- **Next Map:** the map (the vote pool and the usable maps, by their readable names) and both cultures.
  - **Change Map Now** ends the match and opens that map with those cultures, with no vote. It needs automated battle switching, which the server config turns on.
  - **Apply Changes** keeps the match and uses the map after it. A map picked this way turns the vote off for later matches. Applying again without changing the map turns it back on.
- **Match:** number of rounds and round time limit, which apply to this match too; map time limit, which applies from the next map.
- **Players:** the stock kick, ban, mute and unmute.
- **Restart Round** (Commander Battle group) ends the round being fought with no winner, and the same round number is played again, so a restart doesn't use up one of the match's rounds. It is greyed out outside a fight.
- **Swap Sides** moves every player to the other side. During a fight the round restarts and the sides swap as the next preparation starts; during preparation they swap at once. Each player keeps their formation number if the other side's formation of that number has the same role, and otherwise takes a free formation of the role. Round scores stay with the sides (attackers, defenders), not with the players.
- **Remove Commander** lists the players who command a formation. The one you pick goes to the spectators and their formation goes to the AI. They stay on the server and can join a side again.

The client sends `AdminCommand` (`CommanderBattle/Network/AdminCommand.cs`). The server runs it only for a peer the game marks as admin (`NetworkCommunicator.IsAdmin`), and tells every player what was done. The log records each command and who sent it ("Admin <name>: RestartRound ..."), and also each refused one. Map, culture and match settings go through the stock `AdminUpdateMultiplayerOptions`, which the server also applies for admins only. The code is in `CommanderBattle/MissionMultiplayerCommanderBattle.Admin.cs` (server) and `CommanderBattle.View/CommanderBattleAdminPanel.cs`, the provider that replaces the stock one in the stock panel.

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
  - The front rank is more aggressive than in singleplayer: the attack score is weighted 1.3 and the back-step score 0.75 (`RbmMpSettings`), so men in contact press in rather than step back. The decisions are made on the server only.
  - The 30 s formation log shows, for each infantry and ranged formation, how many men are deciding (attack, back step, find ally, flank, rest), closing up and held, or "too small for Frontline". A "Frontline this round" line counts obstacle probes, rejected steps and hold releases sent.
- **Archers defend themselves** (`RBM.Multiplayer/AI/ArcherCloseCombatLogic.cs`):
  - RBM's weapon scores for ranged formations keep the bow out at almost any range. Its formation movement also keeps every soldier on a move order in his slot.
  - So an AI archer on foot with an enemy within 4 m switches to vanilla's sword-versus-bow scores. He takes the nearest enemy as his target when the engine sticks to a far one, and leaves his slot.
  - RBM's behaviour returns 3 s after the last enemy is gone. The log's "Archer close combat" lines count them.
- **RBM Stance**: posture, stamina and AI kick/bash, simulated on the server. Players see RBM's posture/stamina bars and its crush-through and chamber-block posture-break messages (`StanceHudUpdate` / `StanceEvent`, `RBM.Multiplayer/Network`), and the first-person exhaustion effect with RBM's exertion grunt. RBM's stagger, tired and kick/bash fall animations are replayed on every client (`StanceAnimation`).
- **Settings**: RBM's defaults, the same on the server and every client. Your singleplayer RBM config is never read. RBM Combat is off (see below), and the Frontline attack and back-step weights differ (see above).
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
| Per-round reset of RBM's state, tactics and terrain read; stat rebuild and Frontline hold release when a commander takes over a soldier | `RBM.Multiplayer/RbmMpServerLogic.cs` |
| RBM's tactics on Commander Battle's teams | `RBM.Multiplayer/RbmMpTactics.cs` |
| Keeps commanders' formations out of the AI's regrouping | `RBM.Multiplayer/FormationOwnershipGuard.cs` |
| Keeps RBM's stat changes off player-controlled agents | `RBM.Multiplayer/PlayerStatGuard.cs` |
| Posture HUD values and events, server and client | `RBM.Multiplayer/Stance/*.cs` |
| Stand-ins for the singleplayer types RBM's source mentions | `RBM.Multiplayer/Stubs/*.cs` |
| Posture bars and exhaustion effect (client only) | `RBM.Multiplayer.View/*.cs` |

Notes:

- **The AI regroups its own soldiers.** As in singleplayer, RBM's tactics merge the AI-led formations of a kind and split flanking parties off them. A formation a player commands is never touched: no soldier moves into or out of it, and its commander keeps exactly their own men. Neither is the general's formation (8), whoever leads it. A formation whose commander leaves goes back to the AI and may be regrouped. Every round starts again from Commander Battle's formations.
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

The armies start where a singleplayer field battle of this size would place them on the same scene. `CommanderBattleSpawnLayout` runs the stock deployment planner on the scene's spawn path: the singleplayer distance between the armies for both armies' troops (up to the 1,000-troop reference), infantry in front, ranged behind, cavalry on the left and right flanks, horse archers on the right flank (the planner's HorseArcher class, as in singleplayer), and the general's riders as the planner's General block, behind the line's centre. It then shares each block out between the formations of that role, by their troop counts. The roles per formation follow the side's culture, which the server and clients both know, so they compute the same layout.

Each round, every army starts somewhere else in its spawn zone. The server picks a seed at the start of the round's preparation and sends it to every client with the round's troop counts (`SpawnLayoutSeed`; again when a count changes during preparation, and to players who join). All of them derive the same changes from it:
- The spawn path the armies stand on: one of the scene's `spawn_path_NN` paths, with the armies' meeting point 37–63% along it, as singleplayer picks it. The stock pick is random on each machine, so the server and the clients disagreed; it is made from the seed instead, and the log names the path ("Spawn path ...").
- The army moves up to 40 m sideways, 40 m back or 15 m forward, and turns up to 10 degrees.
- The formations of a role swap places.
- If that would put a formation outside the map boundary, a smaller move is used, down to none.

The amounts are in `CommanderBattleSettings` (`SpawnLateralJitter`, `SpawnBackJitter`, `SpawnForwardJitter`, `SpawnTurnJitterDegrees`), and the log records each side's move.

Multiplayer only loads scenes from multiplayer modules. `tools\Install-CampaignScenes.ps1` links the campaign's field-battle scenes into `Modules\CommanderBattle\SceneObj`, for both the game and the dedicated server. Players don't need the script: the client links any missing scene itself when the game loads (`CampaignSceneLinker`, junctions from the player's own `SandBoxCore`), and logs "Campaign scenes: N of M available". It also writes `Modules\Native\ds_config_commander_campaign.txt` for the server, with 2 rounds per map. After each match the players vote for the next map from these scenes (`enable_map_voting`; the game offers at most 100), and map-change polls are allowed (`AllowPollsToChangeMaps True`). The loading screen, map voting and scoreboard show each scene by a readable name. It is built from the scene's terrain, forest and features in `sp_battle_scenes.xml` ("Wooded Plains with a lake", "Steppe 7"), and the names live in `Module/ModuleData/commander_battle_strings.xml`. A scene added to the pool later needs a name there too, or it shows its id.

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
- [ ] With nobody as general, each side spawns an AI general (the class's hero, mounted) and 6 mounted bodyguards behind its line; the detailed log shows "<side> AI general ... with 6 bodyguards" and "Troop counts" lists "general (formation 8)". The AI general stays behind the main formation (Left Alt panel: "General (7): General, Move"), and with RBM his riders stay with him instead of joining the cavalry.
- [ ] Joining a side never makes you the general. Taking the General row (N) or card opens the troop screen at the weapon step; the log shows "<name> commands <side> general (formation 8)". You spawn mounted in the general's gear with 6 riders who form a wedge behind you, you at its tip, and keep it as you ride and turn; pressing F during preparation causes no server error.
- [ ] A dead player general takes over a rider (F) and the others follow him. A player general who leaves mid-round hands the riders to the AI, who then lead them as the AI general.
- [ ] A culture without a General class: no General row or card, nothing spawns in formation 8, and the log says "... fields no general" once.
- [ ] Swap Sides keeps the general as the general on the other side.
- [ ] General mode, as the general mid-round: F1 lifts the camera well above the order camera's height, with the menu and no "There isn't any unit under command" message. The map renders properly up there (no missing terrain or objects). The mouse turns the view, the wheel zooms, F1 and Esc leave, and the normal camera comes back. Looking up or towards a hill doesn't put the camera under the ground. Starting in first person, general mode shows third person and leaving returns to first person. His soldier and horse stand still in general mode, and WASD does nothing.
- [ ] General mode, Left Ctrl: holding it stops the view turning and shows the cursor; the menu buttons work only then. Releasing it hides the cursor and the view turns again. Releasing Ctrl mid-drag brings up the label popup.
- [ ] General mode, Path: with Ctrl held, drag a line on the ground, type a label, and the path shows with its label in the middle; Cancel throws it away. Typing the label doesn't move the camera or the soldier, and Enter doesn't open the chat. The log shows "<name> (<side> general) drew path N '<label>' (M points)".
- [ ] General mode, Delete: with Ctrl held, clicking a label removes the path for the whole side; the log shows "... deleted path N ...". An 11th path removes the oldest.
- [ ] Being hit (or his horse being hit) in general mode closes it with "You were hit: general mode closed."
- [ ] Another player of the general's side sees the paths and labels while holding Left Alt; a player of the other side and a spectator never do. A player who joins the side mid-round sees the paths already drawn. They are gone when the next round starts.
- [ ] For a player who isn't the general, F1-F9 still work the order bar.
- [ ] A dead commander can take over a soldier in their formation. About 3 s after dying, the spectator card on one of their own soldiers shows "Press F to take control"; it doesn't show on another formation's soldier.
- [ ] Opening the order bar in third person lifts the camera up and back. The mouse wheel changes the height, closing the bar brings it down, and move orders land where the screen centre points. Looking up with the bar open brings the camera in rather than under the ground.
- [ ] After a map change (match end, vote, new map) the order bar still opens, F1-F9 pick orders, and attacking gives no orders. The client's rgl log shows "InitializeInADisgustingManner is called" once per map.
- [ ] Admin panel (join with the admin password): the Commander Battle group shows under the stock groups. Restart Round mid-fight ends the round with no winner and replays the same round number. Swap Sides mid-fight restarts the round and every player starts the next one on the other side, at the same formation number where the roles match. Remove Commander sends the player to the spectators and the AI leads the formation. The log shows "Admin <name>: ..." for each.
- [ ] Admin panel: only the Next Map, Match, Commander Battle and Players groups show. The Map list holds the campaign scenes by name. Change Map Now opens the picked map with the picked cultures and no vote. Changing Number of Rounds and pressing Apply Changes changes the round total shown in game.
- [ ] A player who isn't an admin can't run the commands (the log shows "Refused admin command").
- [ ] Cheering (the cheer wheel) makes your formation's soldiers cheer in a rolling wave, and another player sees and hears it. The general's cheer reaches only his riders. Soldiers near an enemy, fighting, aiming or reloading don't join in. A second cheer within 10 s gets no echo. The detailed log shows "<name> cheered: N of <formation> join in".
- [ ] Time-out with both armies still in the field: the defenders win the round.
- [ ] Routed soldiers run off the map and the round ends when the last fighting soldier of a side is gone (the log's "Fleeing" lines count them).
- [ ] Server tick rate holds with 1000-1250 soldiers. CPU is the main risk, more so with RBM's AI.
- [ ] With RBM.Multiplayer, the RBM.Multiplayer log lists the patch classes, with none failed.
- [ ] With RBM.Multiplayer, AI infantry keeps Frontline spacing (stepping in and out of the line) and AI soldiers kick and bash. In a charge, the formation log shows men deciding and held, and "Frontline this round" counts hold releases sent; no network errors or client disconnects in the melee.
- [ ] With RBM.Multiplayer, the log shows "RBM tactics registered" and "Terrain read started" each round, and the AI-led formations regroup into RBM's tactic shapes.
- [ ] With RBM.Multiplayer, the formation you command keeps exactly your soldiers while the AI regroups the others.
- [ ] With RBM.Multiplayer, your posture and stamina bars show and move when you block, and the target's bars show after an exchange.
- [ ] With RBM.Multiplayer, a soldier you take over moves at normal speed (no stamina slowdown, no rubber-banding).
- [ ] A front-rank soldier taken over in melee (F) can walk away at once; the server doesn't keep him where Frontline held him.
- [ ] With RBM.Multiplayer, the second round starts cleanly (the log shows a reset per round).

## Known limitations

- **Our troop types reach other modes.** A client that loads Commander Battle sees our Vlandian classes in every multiplayer mode, since they replace the stock ones by id. Players and servers must run the same module version, or the class indexes disagree.
- **Saved stock perk choices.** When a player joins a side, the client sends its saved perk choices for each class id (stock behaviour). Our classes reuse the stock ids, so a saved stock pick can switch the commander to another weapon setup until they pick on the troop screen. The formation then shows and fields that setup (`SyncCommanderClasses` follows the commander's perk).
- **Formation names.** The formation list names a formation by its class and its formation number (the order bar's), so Vlandia's infantry are "Infantry 1", "Infantry 4" and "Infantry 6", and the order bar may call a Vlandian infantry formation by a cavalry or horse archer formation name.
- **The general's riders take no orders.** The stock order bar and the server's order handlers only know formations 0-7, so a player general can't order the bodyguard; it follows him.

- **RBM: terrain read hitch.** RBM builds its battlefield grid when it reads the terrain, at the start of each round. That can stall the server briefly while it does.
- **Agent cap.** The engine caps a mission at about 2047 agents, and horses count. Two armies of up to about 620 soldiers each plus the commanders fit, but cavalry-heavy armies eat into the margin.
- **RBM: remote AI agents on clients.** Clients compute stock stats for the AI soldiers they see, while the server applies RBM's. Movement and outcomes come from the server, but an AI soldier's swing may look slightly faster or slower than it really is.
- **RBM: no listen server.** RBM's AI and Stance only run on the dedicated server.

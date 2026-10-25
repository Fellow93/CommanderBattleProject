# Troop types and the troop selection screen

**Status:** in progress (v2 built for all six cultures, and the culture variance pass applied and compile-checked 2026-10-25. Not tested in game; phase 4 balance after testing) · **Size:** L

Follows `choosing-troops.md` (options 1–3 built: the class sets the formation's size).

## Goal

A commander picks their troops on Commander Battle's own screen, in three steps:
1. the **formation class** (infantry, ranged, cavalry, horse archers), showing how many formations of it are free, which takes one;
2. the **weight** (heavy, medium, light);
3. the **weapon setup** (e.g. spear + shield, pike, weapon + sidearm, two-handed shock, skirmisher).

Each culture differs: in how many formations of each class it has, which weights and which weapon setups it offers, and with its own weapons.

## Decisions

### v2 (2026-10-25, replaces the v1 type lists and perk shape)

- **Each culture has its own split of the 8 formations**, horse archers included as a fourth class (table below). This replaces the fixed 4/2/2.
- **Picking a class on the first screen takes a free formation of it.** Switching class moves you to a free formation of the new class, under the same rules as taking one from the list (N) today. The formation list stays as an overview.
- **Weight is the class; the weapon setup is a perk choice on it.** The game allows 16 classes per culture, too few for every weight × weapon combination.
- **The "more men / better armour" perk list is dropped.** The weapon setup is the only perk choice.
- **Only the weight sets the troop count.** Weapon setups don't change it (no `TroopCountOnSpawn` on them).
- The split, weights and weapon setups below are approved, with Aserai changed to 3 / 3 / 1 / 1.
- **What the weights mean (gear tiers):**
  - **Light = levy:** properly equipped, not rabble. Padded or gambeson armour, a real helmet, a decent shield and a proper weapon.
  - **Medium = common men-at-arms:** mail and a good helmet.
  - **Heavy = knights:** dismounted knights on foot and knights on horseback, in the culture's best armour (heavy mail or lamellar and plate pieces, full helms) and its best weapons.
  - Each culture uses its own items of that tier (e.g. Khuzait lamellar, Battanian highland gear).
  - Ranged and horse archers follow the same tiers: levy archers, men-at-arms archers, elite archers.

### Kept from v1

- Our own classes replace the stock ones, built from multiplayer items only (`Native/ModuleData/mpitems.xml`, `mp_crafting_pieces.xml`).
- **Override the stock classes.**
  - The 7 stock class ids of each culture are overridden in place (`_replaceWhileMerging="true"`), and the rest are appended.
  - Every class of a culture is ours, so each culture needs at least 7.
  - Accepted cost: any MP mode on a client that loads Commander Battle sees our classes.
- **Own screen; the stock class loadout is gone.**
- **A pick applies next round,** or at once during preparation before the armies spawn.
- **A pending pick stays with the formation** when its commander leaves, weapon setup included: the AI fields it next round.
- **RBM's regroup stays as it is.**

## Formation slots per culture

8 per side. Each formation has a budget: the men it fields at its class's role-average size. Since 2026-10-25 16:10 it is 54 for foot formations and 44 for mounted ones (was 500 / 8 = 62–63 for all), cut for server load; a side lands around 400 men.

| Culture | Infantry | Ranged | Cavalry | Horse archers |
|---|---|---|---|---|
| Vlandia | 3 | 2 | 3 | 0 |
| Empire | 3 | 2 | 2 | 1 |
| Khuzait | 2 | 2 | 2 | 2 |
| Aserai | 3 | 3 | 1 | 1 |
| Sturgia | 5 | 1 | 2 | 0 |
| Battania | 5 | 2 | 1 | 0 |

## Weights (one class each)

| Culture | Infantry | Ranged | Cavalry | Horse archers | Classes |
|---|---|---|---|---|---|
| Vlandia | heavy, medium, light | light, heavy | heavy, medium, light | – | 8 |
| Empire | heavy, medium, light | light, heavy | heavy, medium, light | light | 9 |
| Khuzait | medium, light | light, heavy | light, heavy | light, heavy | 8 |
| Aserai | heavy, medium, light | light, heavy | light, heavy | light | 8 |
| Sturgia | heavy, medium, light | light, heavy | light, heavy | – | 7 |
| Battania | heavy, medium, light | light, heavy | light, medium | – | 7 |

**Weight labels come from multiplier bands per role** (`CommanderRoles.WeightBands`; changed 2026-10-25 15:20). Labelling by place alone called a role's second class "heavy", which put medium gear (Khuzait's Darkhan, Battania's Highland Horseman) under the wrong label.

| Role | Light | Medium | Heavy |
|---|---|---|---|
| Infantry | ≥0.75 | ≥0.58 | below |
| Ranged | ≥0.60 | ≥0.52 | below |
| Cavalry | ≥0.42 | ≥0.34 | below |
| Horse archers | ≥0.40 | ≥0.36 | below |

- All six XMLs use this scale: infantry 0.85 / 0.65 / 0.50, ranged 0.68 / 0.46, cavalry 0.46 / 0.38 / 0.30, horse archers 0.43 / 0.32.
- A role with a single class has no weight step.
- The light → heavy sort order (`GetWeightClassIndexes`) is still by multiplier.

## Culture variance pass (approved 2026-10-25, replaces the weapon setups table below)

Inspired by RBM's troop overhaul (`..\RealisticBattleProject\RBMXML\RBMCombat_unit_overhaul.xml`), adapted to multiplayer items.

**User decisions:**
- Throwing weapons appear **only in skirmisher setups**, never as an extra on other setups.
- No Sturgian mounted crossbowmen.
- Pikes stay shieldless (the MP pike stance requires no shield).

**Conventions:**
- **Levies** always have a proper weapon + shield + sidearm.
- **Two-handed setups may keep a shield,** carried on the back (user, 2026-10-25). The variance pass cleared it on most of them; giving it back is a balance-pass choice per culture. Pikes stay shieldless, because the MP pike stance requires no shield.
- **Cavalry** carry shields; **horse archers** don't (Aserai mamluke HA: a targe is allowed).
- **Archers** carry a sidearm.
- **Setup order is the AI order.** Index 0 is the culture's signature setup. The k-th AI formation of a role fields setup k, wrapping (`AssignDefaultClasses`).

| Culture | Strength | Gap | Weapon setups, in order (default first) |
|---|---|---|---|
| **Vlandia** | Heaviest knights; armoured pike and polearm infantry; crossbows with pavises | No throwing weapons, no bows, no horse archers. Slow | **Inf:** spear + kite shield · pike · mace or sword + kite shield · bill or voulge (medium and heavy only). **Ranged:** crossbow + pavise · heavy crossbow + pavise. **Cav:** lance + tall heater · mace + shield |
| **Empire** | Legion infantry with large kite shields on every weight; shielded archers; cataphracts | No two-handers; one horse archer class; middling bows | **Inf:** spear + kite shield · sword + kite shield · menavlion (medium and heavy only) · skirmisher (pila + kite shield + gladius). **Ranged:** bow + targe + sidearm · crossbow + pavise. **Cav:** lance + targe + mace · mounted skirmisher (pila + shield). **HA:** bow + sword |
| **Khuzait** | Horse-archer doctrine with the best mounted bows; lancers with tough small shields | Weakest infantry (light and medium only, no big shields, no pikes, no skirmishers); lightest archers | **Inf:** spear + small wicker shield · glaive (two-handed) · sabre + small shield. **Ranged:** bow + sabre (no shield). **Cav:** lance + small shield + mace · spear. **HA:** bow + sabre · bow + glaive (no shields) |
| **Aserai** | Javelin skirmishers on foot and horseback; two-handed tabar axemen; strong bows | Lighter armour throughout; no pikes, no crossbows | **Inf:** spear + oval shield · mace or sword + oval shield · tabar (medium and heavy only) · skirmisher (jereed + oval shield + sidearm). **Ranged:** bow + mace · strong recurve. **Cav:** lance + adarga · mounted skirmisher (jereed + round shield). **HA:** bow + mace + targe |
| **Sturgia** | Heaviest shield walls: big round shields, the heaviest infantry armour; two-handed axe berserkers; heavy cavalry with two-handed maces | Weak ranged (composite bow only); no pikes, no skirmishers; slowest infantry | **Inf:** spear + big round shield · axe + big round shield · two-handed axe (berserker). **Ranged:** composite bow + two-handed axe sidearm. **Cav:** lance + big round shield + two-handed mace · axe + big round shield |
| **Battania** | Best longbows; javelin skirmishers; pikes and falx; fastest infantry | No heavy cavalry, no crossbows; lightest heavy armour | **Inf:** spear + round shield · pike · sword or axe + big round shield · falx (two-handed) · skirmisher (javelins + small shield). **Ranged:** longbow + two-handed sidearm · heavy longbow. **Cav:** spear + big round shield · mounted skirmisher (javelins + falx + shield) |

**Armour and speed:**
- **Sturgian infantry and Vlandian knights** are the heaviest at their weight.
- **Khuzait, Battania and Aserai** are a step lighter at each weight, with infantry speed a little higher.
- **Sturgian infantry** are the slowest.
- The weight multipliers stay on the label scale (`CommanderRoles.WeightBands`).

## Weapon setups (v2 original, superseded by the variance pass above)

| Culture | Infantry | Ranged | Cavalry | Horse archers |
|---|---|---|---|---|
| Vlandia | spear + shield, pike, sword + shield, two-handed (voulge) | crossbow, heavy crossbow + pavise | lance + shield, spear, mace + shield | – |
| Empire | spear + shield, long menavlion, sword + shield, skirmisher (pila) | bow, crossbow | lance + shield, spear, sword + shield | bow + sword |
| Khuzait | spear + shield, pike, axe/mace + shield, two-handed glaive | bow, strong bow | lance + shield, spear, mace + shield | bow + sword, bow + lance |
| Aserai | spear + shield, pike, sword + shield, two-handed, skirmisher (jereed) | bow, recurve | lance + shield, sword + shield, mounted skirmisher (jereed) | bow + sword |
| Sturgia | spear + shield, great spear, axe + shield, two-handed axe (no skirmishers) | longbow | lance + shield, axe + shield | – |
| Battania | spear + shield, pike, sword + shield, two-handed (falx), skirmisher (javelins) | longbow, heavy longbow | spear + shield, mounted skirmisher | – |

- Every weight of a role offers that role's setups, swapped onto the weight's armour. The first setup is index 0.
- **Pike:** the pike goes in the weapon slot and **the shield slot gets a sidearm**, so no shield remains. The same goes for every setup without a shield (voulge, cavalry spear).
  - Verified in phase 2: an `AlternativeEquipmentOnSpawn` without `item` does clear its slot (`AlternativeEquipmentEffect.Deserialize` in `TaleWorlds.MountAndBlade.Multiplayer.dll` leaves the element empty, and the spawner assigns it). Vlandia's no-shield setups use that to clear the old sidearm slot (Item2), so the sidearm isn't carried twice.

## What exists (facts from the code and the game data)

**From v1, built:**
- `cb_mpcharacters.xml` and `cb_mpclassdivisions.xml` (Vlandia so far).
- `RequestTroopSelection` with pending picks: `FormationSlotPendingSelection`, applied in `NewSpawnLayout`, or at once before the counts freeze.
- The screen (`TroopSelectionView`/`VM`, `CommanderBattleTroopSelection.xml`, `Module/GUI/Brushes/CommanderBattle.xml`), restyled on the stock loadout's art.
- The stock loadout is removed.
- The shared count formula and the class log, in `CommanderRoles`.

**Code:**
- **Class index.** A formation's class is an index into `MultiplayerClassDivisions.GetMPHeroClasses(culture)`, the same index as `MissionPeer.SelectedTroopIndex`.
- **Roles in v1** (replaced in phase 2, see "Phase 2 as built").
  - `CommanderRole` was Infantry/Ranged/Cavalry; the stock "HorseArcher" class group mapped to Cavalry (`CommanderRoles.GetRole`).
  - `CommanderBattleSettings.FormationRoles` was one fixed array; slot i is the team's formation `(FormationClass)i`.
  - The layout (`CommanderBattleSpawnLayout`, `GetPlanClass`, slot widths), `AssignDefaultClasses`, the role averages, the AI status and the view all read it. (The AI status panel classifies formations by their troops, `FormationQuerySystem`, so it needed no change.)
- **Perks** are stored per peer per troop index, 3 lists (`RequestPerkChange(list, index)`: list 0..3, index 0..15). A perk applies when its `game_mode` is `all` or the server's GameType (`CommanderBattle`).
- **AI-led formations have no perks** (`SpawnAiFormation` uses the bare troop character). With weapon setups as perks, the AI applies them itself (built in phase 2).
- **Spawning** gets the commander's perk equipment from `MPPerkObject.GetOnSpawnPerkHandler(peer).GetAlternativeEquipments(isPlayer)`. `IReadOnlyPerkObject.GetAlternativeEquipments(isWarmup, isPlayer, list)` does the same for one perk object, so the AI path can use it.

**Game data:**
- **Limit: 16 classes per culture** (`MissionPeer.MaxNumberOfTroopTypesPerCulture`, 4-bit troop index).
- **A bad XML node silently drops every later entry of that file** (`MBObjectManager.LoadXML` swallows the exception). The class log at mission open shows it.
- **Never put `_replaceWhileMerging` on a new id.** With no id match, the merger replaces the file's first class.
- **Rosters vary looks only.** The spawner's seeded `Equipment.GetRandomEquipmentElements` rolls the roster per slot, so a slot holds the same kind of item in every roster.
- **Weapons:**
  - pikes: `mp_vlandian_pike`, `mp_aserai_pike`, `mp_khuzait_pike`, `mp_battania_pike` (pike and brace stances, no shield and no mount);
  - long spears ~190–230 cm (`mp_sturgia_great_spear`, `mp_empire_long_menavlion`);
  - lances per culture (the Empire lances may lack the couch usage);
  - crossbows and bolts, 24 bows, javelins, thrown spears, throwing axes.

## Design (v2)

### Roles and slots
- `CommanderRole` gains **HorseArcher**, and `GetRole` maps the "HorseArcher" class group to it.
- `CommanderBattleSettings` gets the split table and `GetFormationRoles(culture)`, which returns the culture's 8 roles. Infantry comes first, then ranged, cavalry and horse archers, mapped onto formation indexes in a fixed, documented order.
- The slot table is built per side from its culture. Both cultures are known on server and clients (`CultureTeam1`/`2`), so slots and layout stay deterministic on both sides. Rounds reuse the mission; the cultures don't change within a mission.
- **Layout:** horse archer slots use the deployment planner's HorseArcher class. Everything that read `FormationRoles` reads the side's roles instead.
- **Fallback:** a culture whose XML isn't converted yet keeps 4/2/2/0, so the stock classes keep working until its XML lands.

### Weight and weapons
- **Classes:** one per weight (see the Weights table), with `multiplier` set by weight. The role average and the count formula are unchanged; HorseArcher gets its own role average.
- **Perk list 1 ("Weapons"):** the weapon setups. Each is an `AlternativeEquipmentOnSpawn` set, `target` Any, swapping the weapon slots onto the class's armour. Index 0 is the class's own loadout. There is no other list.
- **AI-led formations get a weapon setup too.** `FormationSlot` stores the AI's perk indexes. `AssignDefaultClasses` spreads weights and setups across a side's AI formations, e.g. infantry: heavy spear + shield, medium sword + shield, light pike. `SpawnAiFormation` applies the chosen perk's alternative equipment, which is still no perk handler and no combat perks.

### Selection
- **Taking a class:** the first screen sends a request for "a free formation of role R".
  - New `RequestRoleCommand(role)`, or `RequestFormationCommand` with a client-picked free slot. The server validates either way.
  - The server takes the lowest free slot of that role, under today's "take a formation" rules (side, free, before the next round).
  - A commander of another formation moves.
- **Weight and weapons:** `RequestTroopSelection(classIndex, weaponIndex, 0, 0)` as built. Server checks: class of the slot's role, weapon index valid.

### The screen
1. **Formation class:** four cards (only the culture's classes; horse archers are hidden when it has none), each with its icon and "N of M free". Your own class is marked. A class with no free formation can't be picked unless it is yours.
2. **Weight:** light / medium / heavy cards for that class (the culture's ones), each with its troop count, armour and speed.
3. **Weapon setup:** the setups with perk icons and short descriptions.
4. **Confirm**, with Back on steps 2 and 3. The status line says "applies now" or "applies next round".

- It opens on joining a side (step 1), on taking a formation from the list (step 2), and from the list's "Troops" button (step 2).
  - As built, the auto-placement stays: the server still puts a joining player in a free formation, so they always lead one and spawn even if they close the screen. Step 1 opens on top and shows that formation's class as theirs.
- The formation list (N) stays as an overview, and you can still take a specific formation there.

## Phase 2 as built (2026-10-25)

- **Split:** `CommanderBattleSettings.CultureSplits` (the table above) and `ConvertedCultures` (only `vlandia`). `GetFormationRoles(culture)` builds the 8 slot roles, cached per culture. `FormationSlot.Role` reads it from the side's culture each time, so the slot table can be built before the culture options arrive.
- **Slot-to-formation mapping:** each role takes its preferred formation indexes first (infantry 0, 5; ranged 1, 4; cavalry 2, 6, 7; horse archers 3), and a role with more formations takes the lowest free indexes, roles in the order infantry, ranged, cavalry, horse archers. Results:
  - Vlandia 3/2/3/0: infantry 0, 3, 5; ranged 1, 4; cavalry 2, 6, 7.
  - Empire 3/2/2/1: infantry 0, 5, 7; ranged 1, 4; cavalry 2, 6; horse archers 3.
  - Khuzait 2/2/2/2: infantry 0, 5; ranged 1, 4; cavalry 2, 6; horse archers 3, 7.
  - Aserai 3/3/1/1: infantry 0, 5, 6; ranged 1, 4, 7; cavalry 2; horse archers 3.
  - Sturgia 5/1/2/0: infantry 0, 3, 4, 5, 7; ranged 1; cavalry 2, 6.
  - Battania 5/2/1/0: infantry 0, 3, 5, 6, 7; ranged 1, 4; cavalry 2.
  - Fallback 4/2/2/0: infantry 0, 3, 5, 7; ranged 1, 4; cavalry 2, 6 (v1 had infantry 0, 5, 6, 7 and cavalry 2, 3).
- **Unconverted cultures:** `CommanderRoles.GetRole` maps the stock "HorseArcher" class group to Cavalry for them, so their stock horse archer class still fills a cavalry formation.
- **Layout:** horse archer slots are cut from the planner's HorseArcher block (singleplayer puts it on the right flank with the light cavalry); the fixed fallback puts them outside the cavalry. Untested in game: no converted culture has horse archers yet.
- **Weapon setup on the slot:** `FormationSlot.WeaponIndex` (synced in `FormationSlotUpdate`) is the commander's weapon perk, or the AI's pick. `SyncCommanderClasses` follows the commander's perk (a stock saved-perk message can change it).
- **AI picks** (`AssignDefaultClasses`, once per mission): weights heaviest first, then round again; setup by the formation's place in its role (`AiWeaponOrder`: infantry 0, 2, 1, 3; ranged 1, 0; cavalry 0, 2, 1; horse archers 0, 1). This relies on each culture's XML listing setups in this plan's table order. `SpawnAiFormation` applies the setup's `GetAlternativeEquipments(isWarmup: false, isPlayer: false, null)`.
- **Taking a class:** new `RequestRoleCommand(role)` (shared dll). The server keeps the player's formation if it is already of that role, else calls `RequestFormationCommandFrom` with the lowest free slot of the role (today's rules). The screen waits for the slot update and gives up after 3 s (a refusal changes no slot; the chat says why).
- **Taking a formation adopts it:** the new commander gets the formation's class and weapon setup (troop index and perks), instead of keeping their own class as v1 did, so taking never changes a formation's size.
- `RequestTroopSelection` keeps its 3 perk slots: the weapon index and two zeros.

## Phases

1. **v1, Vlandia types and first screen.** Built and installed 2026-10-25; the screen was restyled after the first look in game.
2. **v2 core:** built 2026-10-25 (compile-checked, not installed or tested in game; see "Phase 2 as built").
   - HorseArcher role, the per-culture split with the fallback, slots, layout, counts, AI weights and setups;
   - weight labels, the weapon perk list, and taking a class;
   - the three-step screen;
   - Vlandia's XML reshaped to v2 (infantry H/M/L, ranged L/H, cavalry H/M/L, weapon setups).
3. **v2 cultures:** the other five cultures' XML. Each turns on its split. Independent per culture, so it can run in parallel.
   - **Built 2026-10-25,** one pair of files per culture (`cb_mpclassdivisions_<culture>.xml`, `cb_mpcharacters_<culture>.xml`); Vlandia's moved there too.
   - Class counts: Vlandia 8, Empire 9, Khuzait 8, Aserai 8, Sturgia 7, Battania 7.
   - Cross-file check: no duplicate ids, and `_replaceWhileMerging` sits exactly on the 42 stock ids.
   - Notes from the writers:
     - The Empire lances can't be couched, so all Empire cavalry use `mp_western_lance`.
     - Stock ids were repurposed where a culture lacked one: e.g. `mp_skirmisher_battania` is the medium cavalry, `mp_skirmisher_sturgia` the heavy archers, and `mp_skirmisher_aserai` the medium infantry.
     - Some cultures add Cape-slot shoulder pieces.
   - `AssignDefaultClasses` now keeps only the setup indexes a class has, so the AI uses both of a two-setup class's setups.
4. **Balance pass:** multipliers, speeds, weapon setups. Check in game with RBM's AI.

## Files it touches (v2)

- `CommanderBattle/CommanderRole.cs` (HorseArcher, weight labels), `CommanderBattleSettings.cs` (split table), `FormationSlots.cs` (per-side roles, AI perks).
- `CommanderBattleSpawnLayout.cs`, `MissionMultiplayerCommanderBattle.cs` (defaults, take-by-role, selection checks), `CommanderBattleSpawningBehavior.cs` (AI weapon setups).
- The AI status and formation list views (roles per side). A new network message if taking by role needs one.
- `TroopSelectionVM.cs`, `TroopSelectionView.cs`, `CommanderBattleTroopSelection.xml`, `CommanderBattle.xml` brushes.
- `cb_mpclassdivisions.xml` and `cb_mpcharacters.xml`, `README.md`.

## Open questions for the user

None open (settled 2026-10-25).

## Done when

- The server log lists each culture's classes (counts match the XML) and each side's split.
- In game:
  - pick class → weight → weapons. The class card's free count drops when you take one;
  - the next round spawns that weight with that weapon setup and the logged count;
  - AI formations field varied weights and setups;
  - Khuzait show two horse archer formations, Vlandia none.
- Pikemen spawn without shields, and their pike and brace stances work.

# RBM Combat in Commander Battle

## Goal

The host picks the combat rules:
- **Light** (today): stock MP items, `DamageCurve`, Commander Battle's slower ranged fire and horses.
- **RBM:** full RBM Combat on RBM's own items.

## What exists today

- `RbmMpSettings` sets `rbmCombatEnabled = false`. RBM.Multiplayer compiles none of `RealisticBattleCombatModule`.
- `DamageCurve` postfixes `AgentApplyDamageModel.CalculateDamage`: final damage × 0.5, then a soft cap on big hits.
- `CommanderBattleAgentStatCalculateModel` (both sides) slows bow and crossbow fire and reload and horse speed.
- Server-side settings precedent: `ServerAddressOverrides` reads a text file from `Modules\CommanderBattle\ModuleData\`. Commander Battle has its own admin panel (`CommanderBattleAdminPanel`).

## What the audit found (2026-10-27)

### RBM's hooks fire in MP

MP registers `MultiplayerAgentApplyDamageModel`, `MultiplayerStrikeMagnitudeModel` and `MultiplayerAgentStatCalculateModel`. These forward to the static helpers RBM patches: `MissionCombatMechanicsHelper`, `CombatStatCalculator` and the base `CalculateDamage`. So RBM's blow, armour and magnitude patches would run on the server as they are.

Patches on `SandboxAgentStatCalculateModel`, `CustomBattleAgentStatCalculateModel` and `BattleAgentLogic` never fire in MP. RBM's swing and missile magnitude prefixes replace the MP strike model, so MP class-perk bonuses are lost. Commander Battle doesn't use those perks.

### RBM's formulas don't fit the stock MP items

RBM's item XMLs only cover SP item IDs and are never loaded in MP. MP items have their own `mp_` IDs and crafting pieces. On MP items the numbers go wrong:

| Input | RBM | MP | Effect on MP items |
|---|---|---|---|
| Body armour | e.g. imperial_scale 135 | 44 | about 1/3 the protection |
| Helmet | head 90–133, plus body/arm values (used as face and neck armour) | head only, 20–51 | face and neck armour are 0 |
| Blade damage factor | 0.8–1.2 | 2.5–3.9 | `weaponDamageFactor = sqrt(factor)` (`DamageRework.Core.cs:549`) divides the armour threshold by about 1.8 more |
| Ammo damage | arrows carry the damage; launcher is a flat 100 | arrows 0–4; the bow carries 30–84 | `MagnitudeChanges.Missile.cs:47-55,187-209` drops the launcher, so **arrows and bolts do about 0 damage** |
| `missile_speed` | read as draw weight in lb: bows 100–200, crossbows 120–330 | bows 60–85, crossbows 77–110 | — |
| Shield armour | ×10 in `Core.cs:820`, RBM-tuned | different scale | pilum shield penetration near zero |
| Horse charge | `MountChargeDamage` around 600 (horse + rider weight, set by RBM's SP stat patch) | item charge 2–10 | charges near harmless |
| Harness | head/arm/leg armour give the horse's head, neck and legs | body only | horse head and legs unarmoured |

RBM's rules therefore need RBM's items, or a shim that maps MP stats onto RBM's scale. The shim is the alternative at the end of this plan.

### RBM's items in MP

- **SP items aren't loaded in MP.** They live in `SandBoxCore\ModuleData\items\`. The dedicated server has no SandBoxCore, and Native loads `crafting_pieces.xml` (the SP pieces) only for SP game types. The server does have the file.
- **Commander Battle's troops mostly have RBM twins.** They use 393 `mp_` items. Without the `mp_`, 292 name an SP item and 291 have an RBM version.
  - Armour maps almost one-to-one.
  - About 100 items have no twin and need a hand-made mapping: the MP-only crafted weapons (e.g. `mp_vlandian_long_sword`, `mp_sturgia_spear`), bows and crossbows, arrows and bolts, the agile/war horse variants, and a few shields.
- **Equipment reaches clients from the server.** A server-side swap of each item for its RBM twin at spawn reaches clients through the agent's spawn message. One troop set serves both modes. Clients only need the RBM items loaded.

### What can run where

- **Server only:**
  - `DamageRework.Core`, `.Blows`, `.HitSound`, `.Entities`
  - `ArmorRework.Dispatch`, `.Human`, `.Horse`
  - `MagnitudeChanges.Melee`, `.Thrust`, `.Missile`, `.Perks`
  - `RealisticWeaponCollision`
  - `RangedRework.Collision`, `.ArmorDeflection`, `.ShieldPenetration`
  - `HorseChanges.MountedCombat`

  Blows and missile reactions are decided and broadcast by the server.
- **Both sides (RBM mode needs some of these):**
  - **`RangedRework.MissileSpeed`:** RBM's `missile_speed` values are draw weights, not m/s. The server's launch speed reaches clients, but the shooter's own predicted shot uses the stock speed.
  - **`RangedRework.EquipWield`:** attack and draw speeds, shared weapon flags, `BipedalRadius`. A high desync risk.
  - **`RangedRework.Reload`, `HorseChanges.MountStats`:** better written into `CommanderBattleAgentStatCalculateModel`, which runs on both sides.
  - **`RangedRework.StuckMissiles`:** needs a network message. Leave it out, or set its timers to 0.
- **Not applicable, must be excluded:**
  - `Campaign\**`, `Items\**`, `TroopPerks\**`, `Magnitude\Tooltips\**`, `UI\**`
  - `RangedRework.Siege`, `.FirePotSplash`, `DamageRework.HitReaction`, `RBMCombatPatcher.cs`

  `CampaignChanges.Spawn` patches `CommonAIComponent.InitializeMorale` and `Equipment.GetRandomizedEquipment`, which do run in MP.

## Design

### 1. The combat mode setting

- **A server setting, `CombatMode = Light | RBM`,** read at server start from a text file in `Modules\CommanderBattle\ModuleData\` (as `ServerAddressOverrides` does). Default Light. Logged with the RBM patch line.
- **Clients learn the mode** from a network message on join and at each round (as `SpawnLayoutSeed` does). They need it for the client-side parts and the HUD.
- **v1: a restart switches modes.** RBM Combat's patches are applied only when the server starts in RBM mode. `DamageCurve` is applied only in Light.
- **v2 (optional):** an admin panel choice that applies at the next map. The server unpatches and repatches `com.rbmcombat` and the shim between maps (Harmony `Unpatch`), and the clients do the same on the message. Harmony supports this, but the JIT-inlining rule then has to hold on every repatch.

### 2. RBM's items, loaded everywhere

- **RBM.Multiplayer ships an MP item set:** SandBoxCore's SP items, Native's SP crafting pieces, and RBM's item XMLs, registered for `MultiplayerGame` in its `SubModule.xml`. The build target copies them from the game install and RBM's repo, never by hand.
- **The set loads in both modes,** so server and clients always have the same item objects. MP references items by object index.
- **Load blockers:**
  - RBM's XMLs are gated by RBM's own loader (`RBM\XmlLoadingPatches.cs`, `RBM_COMBAT_XML_TAG`), and RBM's later files override earlier SP definitions by ID. The merge has to give the same result on both sides.
  - SP items may reference SP-only cultures, modifier groups or holsters.
  - Meshes: SandBoxCore has no asset packages, so SP item meshes should come from Native. This needs checking.

### Spike result (2026-10-27): RBM's items load in MP

The server loads them all; the client is not checked yet.

- **Files:** `RBM.Multiplayer/Module/SubModule.xml` registers for `MultiplayerGame`:
  - SandBoxCore's items (as `sp_*`);
  - Native's SP `crafting_pieces.xml` (as `sp_crafting_pieces`);
  - RBM's piece, item, weapon description and crafting template files.

  The csproj copies them into both installs.
- **Result:** 2158 items (848 `mp_`, 1310 others), 442 crafted, 1241 crafting pieces. Every node of the merged items document loads. No crafted item is broken, and no piece is rejected.
- **RBM's values win:**
  - `imperial_scale_armor` body 135 / leg 122 / arm 67;
  - `empire_sword_1_t2`'s blade swing ×0.6, thrust ×0.7;
  - `nomad_bow` thrust 100, missile 170;
  - `empire_horse` 473 kg, charge 10;
  - `gladius_b` (RBM-only) present.
- **`mp_` items unchanged,** with one exception: RBM's weapon descriptions take the one-handed grip off five `mp_` two-handed axes (`mp_empire_battle_axe`, `mp_empire_long_twohandedaxe`, `mp_empire_short_twohandedaxe`, `mp_test_twohandedaxe`, `mp_vlandian_bastard_axe`). None is a Commander Battle item. Found by diffing the 180 `mp_` crafted weapons' usages before and after.

What broke on the way, and the fixes:

1. **The stock merge folds same-id items field by field.** RBM's loader replaces them whole. RBM rebuilt `eastern_throwing_spear_1_t3` as a two-piece `Javelin`, so the merged item kept the SP guard (`spear_banner_16`), which `Javelin` rejects. `GenerateCraftedItem` returned null and `ItemObject.Deserialize` threw a NullReferenceException.
   - `MBObjectManager.LoadXML` swallows that exception, and every item after it in the merged document is lost: 356 of 2158.
   - **Fix:** `RbmXmlMerge`, a prefix on `MBObjectManager.MergeTwoXmls`. For files with RBM's `RBM_XML_TAG`, it removes same-id `Item` and `CraftedItem` nodes before appending, as RBM's `MergeTwoXmlsPatch` does. It is applied in `OnSubModuleLoad` on the server and on every client: the first patch clients run.
2. **RBM's items need RBM's templates.** `battania_polearm_1_t5` uses a sword guard on `TwoHandedPolearm`, which only RBM's templates allow. **Fix:** load RBM's weapon descriptions and crafting templates. Without them, the last 85 items were lost.
3. **33 SP items name a culture MP lacks** (`looters`, `nord`): bandit clothes and dummy armours. They load, with an unresolved culture. Harmless unless equipped.

**Client check (first join, client pid 8960): not identical to the server.**
- **Same:** counts, every probe value, and 0 items not loaded.
- **Different:**
  - The five two-handed axes keep their one-handed grip. The client's `_mp_weapons.txt` matches the server's dump from before RBM's templates line for line, and differs from the final server's in those five.
  - Item order: the client's first unknown-culture items match the server's runs from before `RbmXmlMerge`.
- **What it suggests:** on the client, RBM's templates and items were merged the stock way, i.e. `RbmXmlMerge` didn't act there. But nothing broke on the client, as it did on the server without the patch, so that isn't proven.
- **Next run:** `RbmXmlMerge.Describe()` now logs ("Item merge:") whether the prefix is registered and how many RBM files it merged, by type. On the server: 19 (5 CraftingPieces, 1 WeaponDescriptions, 1 CraftingTemplates, 12 Items).
- **Why it matters:** MP sends items over the network by object index, and the order differs. A client whose item order differs from the server's would show soldiers the wrong items.

**Second join (client pid 37156): the patch never ran on the client.** The "Item merge" line said the prefix was registered, but `0 merges seen`, even for a merge made after patching. The server saw 47.
- **Why:** the .NET Framework client compiles `CreateMergedXmlFile` before mods load and inlines the small `MergeTwoXmls` into it, so the detour is bypassed. The .NET 6 server called it.
- **What the client built:** everything merged field by field. That loaded without breaking there, because RBM's templates were also field-merged, as unions. But RBM-rebuilt items differ from the server's, and so does the order.
- **Fix:** `RbmXmlMerge` now prefixes `CreateMergedXmlFile`, too large to inline, and reproduces its loop with RBM's files merged RBM's way. The server is unchanged under it.
- **For the next client check,** `RbmItemSetCheck` logs fingerprints of the item order and of every item's stats. The server shows order `b51dff12`, stats `99db25f6`.

**Third join (client pid 2508, server pid 5252): identical.**
- **Fingerprints:** order `b51dff12` and stats `99db25f6` on both sides.
- **Merge patch:** the same 19 RBM files merged on both sides. The client merges 53 files in all, the server 47; the extra 6 are client-only XML.
- **`mp_` weapon dumps:** no difference.

Still open from the spike:
- **Meshes:** not seen in game yet. No soldier wears these items.
- **Left out:** RBM's item modifiers, its holsters (RBM's loader injects them into Native's holster XML in SP), the troop overhaul and the siege items.

### 3. Equipment swap in RBM mode

- **At spawn, the server swaps each `mp_` item for its RBM twin:** by name, plus a hand-made table for the ~100 items without one.
- **Gaps:**
  - The class selection screen still previews the `mp_` items.
  - An item missing from the table keeps its MP version, and its numbers are then off. Log every unmapped item.

### 4. RBM Combat itself (RBM mode)

- **csproj:** glob `RealisticBattleCombatModule\**` with the exclusions above.
- **Stubs:**
  - namespace placeholders for `TaleWorlds.CampaignSystem.ViewModelCollection.*` and `TaleWorlds.Core.ViewModelCollection.Information`;
  - about 15 `DefaultPerks` members.
- **Patcher:**
  - `RbmMpPatcher` maps the `RBMCombat` namespace to `com.rbmcombat`, which RBMStance's `[HarmonyAfter]` refers to;
  - patch small helpers early (the JIT-inlining rule).
- **Settings:**
  - `rbmCombatEnabled = true` in RBM mode. RBM AI then uses RBM-scale missile speeds in `RangedReachGate` and `AgentStats`, which is right with RBM's items.
  - Fill `weaponTypesFactors`. Only `LoadConfig` fills it, so in MP it is empty today, and RBM Stance may already read the empty table.
- **The missile speed conversion runs on both sides.** That ends "RBM patches on the server only" for this one piece: clients patch it too, gated by the mode message. `EquipWield` stays out unless a play-test shows RBM's lighter blades swinging far too fast without it.
- **Overlaps with Commander Battle's own stat tweaks:**
  - the ranged fire and reload factors and the horse speed factor;
  - in RBM mode, use RBM's reload and mount stats instead, written into `CommanderBattleAgentStatCalculateModel`.

### Alternative: RBM's rules on the MP items (a shim, no RBM items)

- **The shim:** Harmony patches on RBM's helpers scale MP armour (about ×2.5, with face and neck values from the helmet), the blade factors (÷3.3), ammo damage (the bow's damage moved onto the arrow), shields and horse charge.
- **What it saves:** no item loading and no swap, and RBM Combat stays server-only.
- **Its cost:** every factor is a guess to tune against RBM's own feel. It becomes a third mode to maintain. Only worth it if loading RBM's items in MP fails.

## Files it touches

- **`RBM.Multiplayer`:**
  - `RBM.Multiplayer.csproj` (Combat glob, item XMLs in the deploy target)
  - `Module\SubModule.xml` (MP item set)
  - `RbmMpPatcher.cs`, `RbmMpSettings.cs`
  - `Stubs\`
  - a new combat mode reader and its network message
  - the equipment swap
  - `DamageCurve.cs` (Light only)
- **`CommanderBattle`:** the spawn path, for the swap; `CommanderBattleAgentStatCalculateModel`; `README.md`.

## Implementation (branch `rbm-combat`, 2026-10-27)

- **Combat mode:**
  - `CommanderBattle/CombatModeSetting.cs` holds the mode. The server's .NET 6 build reads `Modules\CommanderBattle\ModuleData\combat_mode.txt` ("RBM" or "Light"; no file means Light) in `OnSubModuleLoad`.
  - `Network/CombatModeInfo` tells each joining client, first of all messages.
  - `RBM.Multiplayer/RbmMpCombatMode.cs` reads it by name.
- **Engine parameters:** RBM's native, spear combat and managed core parameter files ship in both modes.
  - The first two go through `Module/ModuleData/project.mbproj`, the core parameters through `SubModule.xml`. `RbmXmlMerge` leaves core parameters to the stock merge, as RBM does.
  - The spear combat file triggers ten engine "collision_damage_starting_percent" assertions at start, about 18 s on the dedicated server. Not yet seen on a client.
- **RBM loadouts:**
  - `CommanderBattle/RbmLoadouts.cs` reads `Module/ModuleData/cb_rbm_loadouts_<culture>.xml` (format in its header).
  - `CommanderBattleSpawningBehavior` takes RBM-mode gear at all five sites: preview visuals, AI formations, AI general, and the stock spawner's queued player agents in `PrepareQueuedAgents`, formerly `ApplyCommanderBannersToQueuedAgents`. Light mode is unchanged.
  - The server logs coverage when an RBM-mode mission opens.
  - The selection screen shows the RBM troops' armour, and the setup's RBM weapons.
- **RBM Combat:**
  - Compiled into RBM.Multiplayer, minus campaign, items, perks, UI, siege, fire pots, `HitReaction`, `MountStats` and RBM's patcher. Stubs cover perks, `Hero.GetPerkValue`, `MapEvent.IsHideoutBattle` and `MathF.Floor`.
  - Patched under `com.rbmcombat`, on the server, in RBM mode only. `rbmCombatEnabled` follows the mode there, and `DamageCurve` is off in RBM mode. `weaponTypesFactors` is now filled in both modes.
  - Left out (`RbmMpPatcher.Skipped`):
    - the client-only arrow visuals and crosshair;
    - `EquipWield`'s bow unload (`OnWieldedItemIndexChangePatch`, `ProcessPendingBowUnloadsPatch`): it moves ammo by hand on each machine, outside the server's ammo messages;
    - `MissionLoadChangeParameters` (`BipedalRadius` 0.48, stick threshold 12.5): a client finishes loading before it learns the mode, and the radius must match everywhere. Since 2026-11-03 the radius 0.48 loads as data in both modes (`RBM.Multiplayer/Module/ModuleData/rbm_mp_core_parameters.xml`);
    - `Reload`'s patch: its formula is in `CommanderBattleAgentStatCalculateModel` instead (below).
  - Light: 96 applied, 0 failed. RBM: 121 applied, 0 failed; 123 after 2026-10-27 evening (the horse charge and the battle log folder).
- **Added 2026-10-27 evening:**
  - **`RbmMpEquipWield`:** RBM's `OverrideWeaponEquipped` prefix, called from a wrapper on `Agent.WeaponEquipped` on the server and every client (under `com.rbmcombat`, so RBM AI's signature grip still runs after it). It acts on non-`mp_` items only, a rule both sides apply without the mode message, which a client gets after the agents on the field are built. RBM's launchers keep their XML draw weight in `MissileSpeed` (RBM no longer overwrites it), so both sides compute the same launch speed with no cache.
  - **`RbmMpClientMissileSpeed`:** a client prefix on `Mission.OnAgentShootMissile` for its own predicted shot (`forcedMissileIndex` -1), in RBM mode: the launch speed from the draw weight, ammo weight and modifier, as the server's prefix computes it, and the realistic-arc lift if it is on. Shots the server sends are left alone.
  - **Reload:** in RBM mode `CommanderBattleAgentStatCalculateModel` sets RBM's AI reload formula for everyone (bow `0.25 × (1 + 0.016 s)`, `crossbow_fast` `0.3 × (1 + 0.0045 s)`, `crossbow` `0.2 × (1 + 0.0045 s)`, s = skill with RBM's diminishing returns) in place of the reload factor. The draw factor (×0.75) stays.
  - **Horse charge:** in RBM mode the same model sets `MountChargeDamage` to the horse, harness, rider, rider's armour and weapons' weight, and `HorseChanges.ChangeHorseDamageCalculation` is applied on the server.
  - **Battle log:** `RbmMpLog.BattleHits` (off) adds RBM's `BattleHitLogic` on the server in RBM mode. `RbmMpBattleLog` moves its file from the SP RBM folder (where RBM prunes to ten logs) to `logs\RBM.Multiplayer_battles\`.
  - **`RbmItemSetCheck`:** the probes, merge counts, core parameters and `_mp_weapons.txt` dump only with `RbmMpLog.Detailed`; the document walk logs only a failure.
- **Loadout data:** written for all six cultures (53 classes, 72 setups), no load problems.
- **Blocked:** the game client is on Steam's v1.5.6 beta, which TaleWorlds' MP lobby doesn't serve, and no beta dedicated server exists. RBM mode can't be played until v1.5.6 is released for MP. RBM is ported (`Stubs/CampaignStubs.cs` has the v1.5.6 `PerkHelper` signatures).
- **Added 2026-11-03 evening:**
  - **Light-only tweaks:** in RBM mode `CommanderBattleAgentStatCalculateModel` applies no horse speed, draw or reload factor, and `RbmMpPatcher` skips `DamageCurve`'s kick/bash minimum with the curve.
  - **RBM's horse speed** (`HorseChanges.MountStats`, the custom battle variant) in the same model, for a ridden horse: mastery, weight lerp, the 10/8 base speed. No banner or perk bonus. Rain from the round's weather pick (`CommanderBattleWeather.GetPick`, same on every machine), since the dedicated server's scene has no weather; Commander Battle has no night.
  - **RBM's shield catch box** (`AttributeShieldMissileCollisionBodySizeAdder` 0.01) for humans, same model.
  - **Arrow meshes:** `RbmXmlMerge` sets `flying_mesh = mesh` on RBM's Arrows and Bolts items (RBM's `betterArrowVisuals`), on every machine. Not in the stats fingerprint.
  - **`RbmMpStuckMissiles`:** RBM's `StuckMissileLogic` rules for MP. A prefix on RBM's `StuckMissileLogic.Register` feeds it on the server. RBM's own loose copy goes through `Mission.SpawnWeaponAux`, which sends nothing to clients, so the server sends `StuckMissileDrop` (agent, body or shield slot, attachment index, item, the copy's id) and each machine removes the attachment and spawns its copy under that id, as the engine's `SpawnAttachedWeaponOnCorpse` does.
  - **Missiles too strong (user, play-test):** RBM's `ArmorRework.ApplyDrivenArmorBonus` scales RBM's armour by driven armour / gear sum. MP's `FillAgentStatsFromData` sets the driven armour to the class `armor` value (8–56) for agents with a `MissionPeer` or `OwningMissionPeer`, so player-led formations had a third or so of their armour; AI-led ones (no peer, driven 0) kept it. `CommanderBattleAgentStatCalculateModel.InitializeAgentStats` now sets the gear sums in RBM mode, as custom battle does.
  - **Face and under-shoulder hit messages:** `HitMessageSync` sends RBM's lines to the player as a `StanceEvent` (they were server-side `InformationManager` lines).
- **Still open:** a play-test of RBM mode.

## Decisions (2026-10-27)

1. **Mode switch:** at a restart only. A server file names the mode, and the server reads it at start. No admin panel switch.
2. **No `mp_`→SP mapping table:** each Commander Battle class gets an RBM-mode loadout made of SP items. It is seeded from the matching SP troop's gear in RBM's troop overhaul (levy → recruit, legionary → legionary, cataphract → elite cataphract), then reviewed. Light mode keeps today's `mp_` loadouts. This replaces design section 3 (equipment swap).
3. **The class selection screen previews the RBM loadouts in RBM mode.** The client learns the mode from the server.
4. **RBM's engine parameter XMLs ship,** on both sides. They load at game start, before the client knows the mode, so they apply in Light mode too.
5. **Reload (2026-10-27 evening):** RBM mode takes RBM's AI reload formula for players and AI, on both sides, instead of Commander Battle's reload factor. Commander Battle's draw factor stays.
6. **Horse charge:** RBM's charge formula in RBM mode, with the charge weight computed on both sides.
7. **`EquipWield`:** runs on both sides, minus the hand-made bow unload and `BipedalRadius` (see "Left out").
8. **Commander Battle's own combat tweaks only in Light mode (2026-11-03):** RBM mode drops the horse speed, draw and reload factors, the damage curve and the kick/bash minimum, and takes RBM's horse speed, shield catch box, arrow meshes and stuck missiles instead.
9. **`BipedalRadius` 0.48 in both modes (2026-11-03),** as data.

## Next steps (as of 2026-11-03 20:15)

Everything above is built on branch `rbm-combat`, in the worktree `Modules\CommanderBattleProject-rbm-combat`. It is uncommitted: the user said not to commit yet. Master, in the main checkout, has none of it.

**Multiplayer works again (2026-11-03):** the server is on the v1.5.6 beta and starts through `TaleWorlds.Starter.DotNetCore.exe` (`tools\Start-Server.ps1`). The user joined it at 17:21.

**Fixed 2026-11-03:**
- The first round's armies never spawned: the Aserai horse archers' `battania_targe_b` is on RBM's `left_forearm_shield` holster, which MP doesn't load. The engine threw in `GetItemHolsterIndex` at the first AI spawn. Now `battania_shield_targe_a`. It was the only loadout item on an RBM holster.
- The server and clients built different thrust damage for 304 crafted weapons (RBM Combat's thrust magnitude patch was on while the server loaded the items). Those two patches now go on after the items load; both sides show `b51dff12` / `88120b5a`.

**Fixed: shield hit points over 1023 (2026-11-03).** RBM's `steel_round_shield`, `noyans_shield` and `steppe_guardian_shield` have 1100–2000 HP; the engine sends a shield's HP after each hit in 10 bits, and each hit froze the server about 5 s on an assertion. `ShieldHitPointsSync` caps the sent value at 1023 (server keeps the real HP). The same "Max: 1023" warning appears for the missile index below, so check the warning's stack before blaming either. Since then `NetworkRangeGuard` clamps every int and float the server writes into its range and logs the first of each kind ("Network value out of range"), so neither freezes the server any more; the log names the message.

**Open: missile pile-up.** In one round (`battle_terrain_biome_063`, thick fog) about 1,100 missiles stayed alive on the server; past 1023 the 10-bit missile index makes every shot freeze the server 4–8 s (huge lag and desync). Not reproduced on `battle_terrain_z`. When it recurs, read the "Missiles alive" lines in `RBM.Multiplayer_<pid>.log` (written while 512 or more are alive) to see which missiles linger.

**Next:**
1. Rebuild from the worktree with `tools\Start-Server.ps1 -Restart -Build`; the build installs both sides, and both checkouts install into the same module folders. A server from this build runs in RBM mode as pid 39312.
2. **Server checks:**
   - "Combat mode: Rbm" (the host file `<dedicated server>\Modules\CommanderBattle\ModuleData\combat_mode.txt` says RBM; it was written for testing);
   - "Patches: 120 classes applied, 0 failed, 12 skipped, 2 after item load" and "Patches after item load: 2 applied, 0 failed";
   - "RBM loadouts: 53 classes, 72 setups" with no problems line;
   - no exception when a round starts and the armies spawn, and no "RBM weapon stats failed" or "Client missile speed failed" line.
3. **Client check:** the client's "Item fingerprints" must equal the server's (`b51dff12`, `88120b5a`). No "RBM weapon stats" failure line on the client either.
4. **Play-test:**
   - soldiers in RBM gear: preparation visuals, AI formations, a player and their troops, the AI general;
   - the selection screen shows the RBM armour and setup weapons;
   - kills per blow, arrows against armour and shields, rearing and dismounts;
   - no attack or animation desync, now that RBM's weapon speeds run on both sides: swings, bow draws and reloads of the player and of soldiers next to them; a player joining mid-round;
   - a player's own arrow flies straight from the bow, with no jump when the server's copy arrives;
   - bow and crossbow fire rate with RBM's reload and no draw factor (crossbows reload much slower than in Light);
   - horse charges hurt again (RBM's formula on the horse and rider weight);
   - horse speed: a player's horse doesn't rubber-band (RBM's speed on both sides), skilled riders on light horses fastest; slower in rain;
   - arrows hit shields less often from the side (the 0.01 catch box), and fly as real arrows;
   - javelins drop out of shields and bodies after a few seconds and arrows out of shields after about 45 s, for the player as for the server: the stuck one vanishes, a loose one falls and can be picked up; no more than 8 arrows in a shield; a shield dropped or a soldier killed later shows the right missiles;
   - a sheathed bow: what happens to the nocked arrow of a low-skill archer (the engine's unload-when-sheathed flag) and of a skilled one (no flag), and that player and server agree on the arrow count;
   - whether the spear-parameter engine assertions (ten "collision_damage_starting_percent" warnings, about 18 s at server start) show up as dialogs on a client.
5. For tuning numbers, turn on `RbmMpLog.BattleHits`, rebuild, and read `logs\RBM.Multiplayer_battles\` after a map.
6. Light mode: delete the host file, restart, and play as before.

**Open work:**
- **Damage feedback from the user** is still to come.
- **Loadout review by the user:**
  - Sturgian bows no longer match their setup names (RBM's line: levy `steppe_bow`, veteran `nordic_shortbow`).
  - The Battanian couched lance is the Sturgian lance.
  - The Aserai cavalry and general share `aserai_lance_1_t5`.
  - The Aserai large quiver is two RBM quivers of 30.
  - Each `cb_rbm_loadouts_<culture>.xml` header names its seeds.
  - The ids the loadout agents were unsure of (all valid; the doubt is the choice):
    - Empire: `gladius_b` (Dagger template); the bodyguard's and strategos' lord helmets `empire_guarded_lord_helmet` and `empire_battle_crown_west`.
    - Vlandia: `northern_plated_boots` and `mail_shoulders` (Sturgian, but RBM's vlandian_sergeant wears them); `sturgian_fortified_armor` (culture vlandia).
    - RBM's ammo: `GRE_hardened_100_gr_arrows`, `GRE_hardened_120_gr_bolts`, `GRE_hardened_150_gr_bolts`, `GRE_hardened_125_gr_arrows`, `GRE_vlandic_arrows`.
    - Battania: `battania_2hsword_6` (light falx); `battania_polearm_1_t5` (rhomphaia, a polearm in RBM); `tribal_bow`; `bearhead` (a hood); `northern_light_harness`.
    - Sturgia: `sturgia_cavalry_armor` (culture aserai).
    - Khuzait: `khuzait_heavy_armor`; `curved_round_shield`; `light_mace_t3`; `steppe_harness`.
    - Aserai: `steel_round_shield`; `battania_targe_b`.
    - Not checked: `eastern_spear_3_t3` and `eastern_spear_5_t5` used one-handed with a shield.
- **RBM repo, the user's:** `RBMCombat_sword_blades.xml` defines `dagger_blade_13_blunt` twice (a schema error at load).
- **Build caveat:** the SP item files are copied from the game install into both installs. While the game is on a different build from the server, the server gets the game's files.
- **Finally:** commit when the user says, then merge `rbm-combat` into master.

## Size

L. In order:
1. Load RBM's items in MP on both sides. This is the riskiest step: try it first, as a spike. **Done.**
2. The mode setting and its message. **Done.**
3. RBM Combat compiled and patched. **Done.**
4. RBM loadouts per class, replacing the swap table. **Done.**
5. Missile speed on both sides. **Done** (a player's own shot; the weapon stats with `EquipWield`).
6. Tuning rounds. First decisions made (reload, horse charge, `EquipWield`); the rest waits for a play-test.

## Done when

- **Light mode:** plays exactly as today.
- **RBM mode, server log:**
  - the patch line shows the `RBMCombat` classes, "0 failed";
  - the combat mode is logged;
  - no unmapped items, or a short known list;
  - no exceptions on hits.
- **RBM mode, in game:**
  - soldiers wear their class's RBM loadout;
  - light troops die in 2–3 blows, heavy ones in more;
  - arrows fly at RBM's speeds for the shooter as well as for onlookers;
  - no attack or animation desync on players.

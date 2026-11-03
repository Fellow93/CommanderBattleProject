# Handoff: RBM Combat on MP items, feasibility

Session of 2026-10-27, written 09:48.

## What was done

- **Audit (read-only)** of `..\RealisticBattleProject\RealisticBattleCombatModule`:
  - which patches fire in MP, where each would have to run, and which depend on RBM's item stats;
  - stock MP item stats (`Native\ModuleData\mpitems.xml`, `mp_crafting_pieces.xml`) compared with RBM's item XMLs.
- **New plan,** `docs/plans/rbm-combat-mp-items.md`:
  - RBM Combat's server-side rules, plus an RBM.Multiplayer shim that maps MP armour, blade factors, ammo damage, shields and horse charge onto RBM's scale;
  - the open questions for the user.

  Indexed in `docs/plans/README.md`.
- **Plan reworked** after the user asked about RBM's own items and a host setting. The plan is now two modes the host picks, Light (today) and RBM:
  - RBM mode loads RBM's items in MP on both sides and swaps each troop's `mp_` items for their RBM twins at spawn.
  - 291 of the 393 `mp_` items Commander Battle uses have an RBM twin; the ~100 without one are the MP-only weapons, bows, ammo and horses.
  - The MP-item shim is kept as an alternative.
- **Spike: RBM's items in MP** (14:20). The server loads all 2158 items; the client is not checked yet. Details are in the plan's "Spike result".
  - `RBM.Multiplayer/Module/SubModule.xml`: SandBoxCore's items, Native's SP crafting pieces and RBM's piece, item, weapon description and crafting template files, all for `MultiplayerGame`.
  - `RBM.Multiplayer/RBM.Multiplayer.csproj`: copies those files into both installs; the SP files are renamed `sp_*`.
  - `RBM.Multiplayer/RbmXmlMerge.cs` (new): RBM's files replace same-id items whole, as RBM's SP loader does. Patched in `OnSubModuleLoad` on server and clients.
  - `RBM.Multiplayer/RbmItemSetCheck.cs` (new), called from `RbmMpSubModule.OnGameInitializationFinished`. `MultiplayerGame` never calls `OnAfterGameInitializationFinished`. It logs counts, probes and the first item that failed to load, and writes `RBM.Multiplayer_<pid>_mp_weapons.txt`.
  - `README.md`: the "RBM's items are loaded, unused" note.
  - The server was restarted several times with the user's OK. It started as pid 33060 (not this session's); now it is pid 8452.
- **Client check** (14:20–14:35). The client loaded the same counts and probes, but a different item order and five `mp_` axes with a one-handed grip.
  - Instrumented, the client showed the merge patch registered but never called ("0 merges seen"). The .NET Framework client had compiled `CreateMergedXmlFile` with `MergeTwoXmls` inlined before mods load.
  - `RbmXmlMerge` now prefixes `CreateMergedXmlFile`, reproducing its loop with RBM's files merged its way. The server is unchanged under it.
  - `RbmItemSetCheck` logs an "Item merge" line and "Item fingerprints" (item order and stats); the server shows order `b51dff12`, stats `99db25f6`.
  - `CLAUDE.md`: the client-inlining trap.
  - Server pid 21060. The client is installed with this build but not re-checked.
- **Moved to branch `rbm-combat`** (worktree `Modules\CommanderBattleProject-rbm-combat`), uncommitted. Master stays clean for the other sessions.
- **RBM mode implemented** (15:00–15:55). Decisions and status are in the plan's "Decisions" and "Implementation" sections.
  - **Combat mode:** `CommanderBattle/CombatModeSetting.cs` and `Network/CombatModeInfo.cs` (new). `CommanderBattleSubModule` reads the server file; `MissionMultiplayerCommanderBattle` sends the mode on join; `MissionMultiplayerCommanderBattleClient` handles it. `RBM.Multiplayer/RbmMpCombatMode.cs` (new) reads it.
  - **Loadouts:**
    - `CommanderBattle/RbmLoadouts.cs` (new).
    - `CommanderBattleSpawningBehavior`: `HeroEquipment` / `TroopEquipment` at four sites, plus `PrepareQueuedAgents`, renamed from `ApplyCommanderBannersToQueuedAgents`, with `ApplyRbmLoadout`. `MissionMultiplayerCommanderBattle.Banners.cs` calls it by the new name.
    - `MissionMultiplayerCommanderBattle.AfterStart` logs coverage.
    - `CommanderBattle.View/TroopSelectionVM.cs` shows RBM armour and weapons.
    - Data: `Module/ModuleData/cb_rbm_loadouts_{empire,vlandia,khuzait,aserai,sturgia,battania}.xml`, written by three agents seeded from RBM's troop overhaul. RBM's `_shoulder` and `_kalkan_shoulder` shields were swapped for their plain ids, because their holsters are RBM's own.
  - **RBM Combat:**
    - `RBM.Multiplayer.csproj` globs `RealisticBattleCombatModule`.
    - Stubs: `Stubs/CampaignStubs.cs` (perks, `Hero.GetPerkValue`, `MapEvent.IsHideoutBattle`), `Stubs/Namespaces.cs` (three view model namespaces), `Stubs/ServerCompat.cs` (`MathF.Floor`).
    - `RbmMpPatcher`: `com.rbmcombat` in RBM mode, the skip list, `DamageCurve` off in RBM mode.
    - `RbmMpSettings`: `rbmCombatEnabled` from the mode on the server; `weaponTypesFactors` filled.
  - **Engine parameters:** `RBM.Multiplayer/Module/ModuleData/project.mbproj` (native and spear combat files), `Module/SubModule.xml` (core parameters), `RbmXmlMerge` (core parameters go to the stock merge), `RbmItemSetCheck` (a core parameter probe line).
  - **Docs:** `README.md` (combat mode), `CLAUDE.md` (RBM.Multiplayer rules), the plan, `docs/plans/commander-banners.md` (the method name).
  - **Server checks:**
    - Light: 96 patch classes, 0 failed.
    - RBM: 121, 0 failed; "RBM loadouts: 53 classes, 72 setups", no problems.
    - The host file `<dedicated server>\Modules\CommanderBattle\ModuleData\combat_mode.txt` (RBM) was written for the test. The server runs in RBM mode (pid 31688). Not played.
- **Game v1.5.6** (16:23–17:40):
  - The game client updated to build 125053 (v1.5.6), and the dedicated server is still on 124119. Steam shows it up to date: `buildid 25805354`, target the same. So the user's client could not log in to the server.
  - RBM was ported to v1.5.6 (`d54dd4f1`). Its new perk helper calls needed the v1.5.6 `PerkHelper` signatures in `Stubs/CampaignStubs.cs`.
  - Rebuilt and installed both sides. Server pid 38508 (old build), RBM mode: 121 patch classes, 0 failed.
  - Item fingerprints: order `b51dff12`, stats now `61ad8004` (RBM's ported item files changed). The client is not checked on this build.
  - **The client can't log in to MP at all:** v1.5.6 is Steam's `beta` branch of the game (`BetaKey "beta"`). TaleWorlds' service discovery returns 404 for it, so the lobby's `ConnectMessage` fails. The dedicated server has no beta. A vanilla launch (Native and Multiplayer only) fails the same way (user), so TaleWorlds' services don't serve this build; the v1.5.5 beta had logged in fine.
  - **The user stays on the beta; MP testing waits for TaleWorlds' release.** RBM mode is not played yet. Server pid 28752 in RBM mode, on the old build.
  - **RBM bug, left for the user's RBM repo:** `RBMCombat_sword_blades.xml` defines `dagger_blade_13_blunt` twice (a schema error at load; every item still loads).
  - **While the game is on the beta,** the build copies the beta's SP item files into the server too.
- **Remaining work** is in the plan's "Next steps" section, written 18:00 for the next session.
- **Client re-check** (14:42): identical. Client pid 2508 joined server pid 5252, a restart by another session at 14:41 with the same RBM.Multiplayer code. Fingerprints order `b51dff12`, stats `99db25f6` on both; the same 19 RBM files merged; the `mp_` weapon dumps are equal.

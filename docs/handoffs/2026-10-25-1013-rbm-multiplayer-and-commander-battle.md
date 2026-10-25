# Handoff: RBM in Commander Battle, and Commander Battle polish

Session of 2026-10-24/25 ("RBM integration"), written 2026-10-25 10:13. Read `CLAUDE.md` first: it covers the build, the MP rules learned the hard way, the logs and how to start the server (from PowerShell, never Git Bash). `README.md` describes the current behaviour in full. This file says what changed, what is verified and what is open.

## State

- **Nothing is committed.** Everything below sits in the working tree on `master`, on top of `a19c4b6` (see `git status`). There are no commits from this session. When committing, add no co-author line.
- **RBM's source (`..\RealisticBattleProject`) is untouched.** It holds the user's own uncommitted work. RBM.Multiplayer links RBM's files and patches around them.
- **Installed and running.** The latest build is installed for the game and for the dedicated server, and the two match. A dedicated server was running at handoff (pid 27440, battle_terrain_k, 91 patch classes, 0 failed). Stop it before building again: a running server or game locks the DLLs.
- `CommanderBattle.View/CommanderBattleOrderCameraView.cs` (the lifted order camera) came from **another session**, not this one.

## What was done

### RBM.Multiplayer (new module: `RBM.Multiplayer/`, `RBM.Multiplayer.View/`)

- **RBM AI and Stance in Commander Battle,** on the dedicated server, built from RBM's own source.
  - Stubs stand in for the campaign types the server lacks.
  - Patching goes class by class (`RbmMpPatcher`).
  - Settings are RBM's defaults with RBM Combat off (`RbmMpSettings`).
  - Each round resets RBM's state, re-registers RBM's tactics and re-reads the terrain (`RbmMpServerLogic`, `RbmMpTactics`).
- **Two MP guards.**
  - Players' formations keep exactly their own soldiers (`FormationOwnershipGuard`).
  - RBM's stat changes never touch player agents, whose stats are not synced (`PlayerStatGuard`).
- **Damage curve** (`DamageCurve`, a stand-in for RBM Combat): damage is first scaled by **0.67** (a 33% cut), then a curve cuts big hits harder (Softness 300).
- **Posture HUD over the network** (`Stance/StanceNetworkSync`, `StanceHudView`).
  - The bars are resent every 1 s, because the updates are unreliable.
  - The HUD is cleared when the player's agent changes.
  - Crush-through and chamber-block posture-break messages reach players.
  - The first-person exhaustion effect works, with a server-side grunt.
- **Posture animations on clients** (`Stance/StanceAnimationSync`, `Network/StanceAnimation`).
  - A server postfix on `Agent.SetActionChannel` broadcasts RBM's reaction animations: stagger, tired, kick/bash falls.
  - It only runs while a Commander Battle mission runs RBM's server logic.
  - RBM's `RBM_action_sets.xml` is shipped through `Module/ModuleData/project.mbproj`, so the tired clip exists in MP.
- **Archers fight back at close range** (`AI/ArcherCloseCombatLogic`).
  - The cause: RBM's weapon curves for ranged formations keep the bow out at almost any range, and RBM's Frontline movement pins every AI soldier on a move order to his slot.
  - The fix applies to AI foot archers with an enemy within 4 m:
    - they get vanilla's DefaultMove melee/ranged curves;
    - if their target is far, they retarget, and then are given the nearest enemy as their target;
    - they leave their slot.
  - It is undone 3 s after the last threat.
  - **Verified in a real round:** hundreds of engagements, with archers drawing swords.

### Commander Battle

- **Spawn layout** (`CommanderBattleSpawnLayout`).
  - It uses singleplayer's deployment planner on the scene's spawn path.
  - **New:** a per-round seed (`Network/SpawnLayoutSeed`), sent to all clients and to players who join, moves each army up to 40 m sideways, 40 m back or 15 m forward and turns it up to 10°, clamped to the map boundary. The formations of a role also swap places. It uses its own xorshift generator, so .NET Framework (the client) and .NET 6 (the server) draw the same numbers.
- **Fleeing soldiers leave** at 25 m from the edge or after 2 min (`CommanderBattleFleeingLogic`). Before this, rounds could not end.
- **Map voting and polls** are on (`tools/Install-CampaignScenes.ps1`).
- **Attacker/defender labels** (`CommanderBattleSideBannerView`):
  - a round-start banner, then a small reminder;
  - labels on the team selection screen;
  - the formation list title.
- **AI status** (`CommanderBattleAIStatusSync`, `Network/SideAIStatus`, `CommanderBattleAIStatusView`).
  - While Left Alt is held, players see their side's tactic and, per formation, its behaviour, order and distance still to go. Yellow markers show where each AI formation is heading.
  - The formation list shows "AI: <behaviour>".
  - A player who leads the tactic's main formation, the one AI archers and cavalry follow, gets a notice and a message.
- **Horses** run at 85% of the stock MP top speed (`CommanderBattleAgentStatCalculateModel`, registered as the game's stats model on both sides).
- **Inactivity kick.** A change in the orders of the commander's formation counts as activity (`CommanderBattleActivityLogic`).
- **Round time-out:** defenders win if both armies still stand (`MissionMultiplayerCommanderBattle.OnPreRoundEnding`).
- **Texts** (`Module/ModuleData/commander_battle_strings.xml`, registered in `Module/SubModule.xml`):
  - the game mode's name and explainer for the loading screen;
  - readable names for all 84 pool maps ("Wooded Plains with a lake", "Steppe 7").

### Docs

- `README.md` was rewritten and is kept current.
- `CLAUDE.md` is new: build, conventions, RBM.Multiplayer rules, logs, the Git Bash pitfall.

## Verified vs not

| Change | Status |
|---|---|
| RBM patches apply (91 classes, 0 failed) | Verified in the server log |
| Tactics, terrain read, round resets | Verified in the server log |
| Fleeing exit | Verified in the server log |
| Archer close combat | Verified in the server log |
| Posture animation sync, on the player and on bots | Not seen in game |
| AI panel, markers and main-formation notice | Not seen in game |
| Side banner and team labels | Not seen in game |
| Loading screen and map names | Not seen in game |
| Spawn randomisation | Not seen in game (the seed is logged; the "spawn moved" lines appear when a layout is computed) |
| Defenders win on time-out | Not seen in game |
| Inactivity rule | Not seen in game |
| Horse speed | Not seen in game |

## Open items, with suggestions

1. **Archer engagement flapping.** The counts run far above the number of archers, because archers flip in and out around the 4 m line, and some at 4.1-4.3 m are never treated. Proposed: engage at 5 m, release only after 3 s with no enemy within 7 m (`ArcherCloseCombatLogic`). If archers still keep the bow with a near target, log whether the close-combat curves are still in place (some RBM code may override them outside `SetBehaviorValueSet`).
2. **Morale is off.** The server never adds `AgentCommonAILogic`, so soldiers have no morale component, nothing routs, and RBM's rout, rally and panic logic is inert. The "Fleeing" counts are mostly riderless horses. The user chose to keep it off for now.
3. **AI tactic vs player formations.** Tactic weights count player-led troops, and tactic roles (main infantry, archers) fall back to player-led formations. Proposed: leave player-led formations out of role picks and weigh their troops less, and re-pick the tactic when a formation changes hands. Not started.
4. **Gap list from the read-only audit session** (beyond what was fixed):
   - S2: check in game that the animation sync fires for players, and that it doesn't double-start clips on bots;
   - S5: target bars for missiles, shooters and mounts;
   - S7/S8: RBM formation shouts and tactical horns (they need messages and RBM's sounds);
   - S9: RBM rewrites player formations' orders (ChargeGuard, MovementOrderPatches), so the order bar disagrees with what the men do;
   - S10: tired AI soldiers' attack speed changes only on the server;
   - A2: RBM's deep-water blocker (it can't be server-only as it is);
   - A3: the `CharacterObject` stub, so heavy javelin infantry count as skirmishers.
5. **Possible reliable-message load.** Big RBM regroups move hundreds of soldiers in one go, and the new reliable messages (AI status every 2 s, stance animations) add to that. Not a confirmed problem: the one drop seen was the inactivity kick, now handled. If players get dropped during big regroups, send `StanceAnimation` unreliable first.
6. **RBM Combat port.** It needs an RBM-style MP item set first; the user deferred it.
7. **Map names.** They are generic, from sparse terrain data. Better names, by region or hand-picked, are an easy edit in the strings file.
8. **A trap: compiling the client writes into the game folder.** `CommanderBattle.csproj`'s output path *is* the game's module folder. A `-t:Compile;CopyFilesToOutputDirectory` "compile check" therefore installs the client DLL by itself, which leaves client and server mismatched. For compile-only checks use `dotnet msbuild <proj> -t:Compile -p:TargetFramework=<tf>`. Install with a full `dotnet build CommanderBattle.sln -c Release` while the game and server are stopped.

## Run and check

```
dotnet build "CommanderBattle.sln" -c Release
```

Start the server from PowerShell in `<dedicated server>\bin\Win64_Shipping_Server\`:

```
.\DedicatedCustomServer.Starter.exe "_MODULES_*Native*Multiplayer*CommanderBattle*RBM.Multiplayer*_MODULES_" /dedicatedcustomserverconfigfile ds_config_commander_campaign.txt /tickrate 60 /port 7210
```

The logs are in `C:\ProgramData\Mount and Blade II Bannerlord\logs\`:
- `CommanderBattle_<pid>.log`: rounds, spawn seed and moves, fleeing.
- `RBM.Multiplayer_<pid>.log`: patches, tactics, formations, "Archer close combat".
- `rgl_log_<pid>.txt`: engine messages, including player connects and disconnects.

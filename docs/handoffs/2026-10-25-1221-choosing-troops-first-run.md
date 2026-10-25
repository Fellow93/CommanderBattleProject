# Handoff: choosing troops, first server run, and the obj\ build fix

Session of 2026-10-25, written 12:21. Follows `2026-10-25-1202-choosing-troops.md`, which describes the feature.

## State

- **Nothing is committed.** The working tree on `master` sits on `755390d`: the feature (see the 12:02 handoff) and the `Directory.Build.props` fix below.
- **Running:** dedicated server pid 3092 (battle_terrain_biome_046, 91 patch classes, 0 failed), with the user's client connected and round 1 in progress.
- **Installed:**
  - The server DLLs are from the 11:59 solution build.
  - The client's `RBM.Multiplayer.dll` was rebuilt for net472 at 12:14 (`dotnet build RBM.Multiplayer\RBM.Multiplayer.csproj -c Release -f net472 --no-incremental`).
  - The other client DLLs are from the 12:01 solution build.
  - Everything comes from the same source.

## What happened

1. **The client could not load RBM.Multiplayer.** The rgl log said "Could not load ... System.Runtime, Version=6.0.0.0". The installed client `RBM.Multiplayer.dll` was the net6.0 build.
   - **Cause:** `Directory.Build.props` sets `AppendTargetFrameworkToOutputPath=false`, which also drops the framework from `obj\`. So net472 and net6.0 compiled into the same `obj\Release\<Assembly>.dll`. In a parallel solution build the net6.0 compile overwrote it before the net472 build copied it to the client folder.
   - Later builds skipped the copy, because the client file looked newer.
   - **Fix:** `Directory.Build.props` sets `IntermediateOutputPath` to `obj\$(Configuration)\$(TargetFramework)\`, defaulting `Configuration` to Debug. Evaluated with `-getProperty`, it is correct for both frameworks of RBM.Multiplayer and CommanderBattle, and the bin paths are unchanged.
   - **Not yet run as a full solution build**, because the server was running.
2. **A dedicated server with no player sits in round 0 preparation.** The mission doesn't tick, so the log has no "Status:" lines. Every earlier run without players looks the same; it isn't a fault.

## Verified

Round 1, Battania (defender) vs Aserai (attacker); the user led Defender formation 7:
- The "Troop counts for round 1" lines match the formula by hand. Examples: Battania light infantry 63 × 88 / 71.3 → 78; Aserai heavy cavalry 63 × 30 / 38 → 50; Aserai light cavalry → 76.
- Logged totals were Defender 539 and Attacker 507. RBM's tactic count showed 540 (539 + the player) and 507.
- The player-led formation has 78 men: 77 troops plus the commander.

## Not verified

- The formation list showing the counts and the side total on the client, and counts changing live during preparation when a class changes.
- Count perks.
- A full solution build with the obj\ fix. Run `dotnet build "CommanderBattle.sln" -c Release` with the game and server stopped. Then check that the client DLLs are net472: an assembly's metadata names `.NETFramework,Version=v4.7.2`.

## Open items

1. **RBM's regroup evens out AI formations.** After RBM's tactic regroups, AI formations hold 62–63 men each and infantry merge into f0 (256 men attacker, 211 defender). So class-based sizes survive only in the side totals and in player-led formations. This was already RBM's behaviour, not something new. If per-formation sizes should hold for AI formations too, look at RBM's regroup (`BehaviorRegroup`) and Commander Battle's formation ownership. Ask the user first.
2. **Battania has only one cavalry class in the role mapping** (role average 50.0 = its single class), so both of its cavalry formations get the same class. This comes from `CommanderRoles.GetRole`, not this feature.
3. The rest is as in the 12:02 handoff: the in-game checklist item, the minor edges, the agent cap (1,299 agents on the field this round, horses included).

## Traps

- The obj\ collision above. It is fixed in `Directory.Build.props`, so `CLAUDE.md` needs no new rule. If a client ever fails with "System.Runtime, Version=6.0.0.0", the net6.0 DLL got into the client folder.

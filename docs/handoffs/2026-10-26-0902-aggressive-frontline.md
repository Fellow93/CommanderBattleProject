# Handoff: a more aggressive Frontline front rank in MP

Session of 2026-10-26, written 09:02. It follows `2026-10-26-0840-review-defect-fixes.md`, whose five fixes are still uncommitted in the same tree.

## State

- **Not committed:** `RBM.Multiplayer/RbmMpSettings.cs`, `README.md`, this handoff, and everything the 08:40 handoff lists as uncommitted.
- **Installed:** both sides, built at 09:02. The client DLLs copied too.
- **Running:** server pid 5996, started with `tools\Start-Server.ps1 -Build -Restart` (it stopped pid 8120). Map `battle_terrain_biome_106`. Patch line: "94 classes applied, 0 failed, 3 skipped". Both mod logs are clean.

## What was done

- The user asked for frontline soldiers to be more aggressive, in MP only.
- RBM's Frontline scores Attack, Back step, Find ally and Flank for each man and picks the highest (`FrontlinePositioning.cs:690-694` in RBM). Each finished score is multiplied by an `RBMConfig` weight, and the weights default to 1.
- `RbmMpSettings.Apply` now sets `frontlineAttackWeight = 1.3` and `frontlineBackStepWeight = 0.75`. Its Detail line logs both.
- Frontline decides on the server only, so the weights don't need to match on the clients, and nothing in the network messages changes.
- The README's Frontline and Settings entries say so.

## Verified vs not

- **Verified:** it builds with 0 errors (the one warning comes from RBM source). The server starts with 0 failed patches and clean mod logs.
- **Not verified in play:** that a front rank in an infantry clash holds and pushes in, instead of stepping back and swapping out. To check the numbers, turn on `RbmMpLog.Detailed` and rebuild. The formation log's "Frontline N deciding (attack, back step, ...)" share should move toward attack.

## Open items

- The second lever, which the user did not pick for now: a `com.rbm.multiplayer` postfix after RBM's `AgentStats` postfix (Priority.Low), multiplying `AIAttackOnDecideChance` for AI agents. With posture on, RBM sets it to 0.10-0.15 for infantry on foot. Since AI combat runs on the server only and players are skipped, it would not desync.
- Tune 1.3 / 0.75 after a played round.

## Traps

- Lowering the back step weight also cuts the posture-driven rotation: the back step score is multiplied by `2 - postureModifier`. Tired men stay in the front rank longer, so posture breaks may get more common.

# Handoff: kick and bash minimum damage

Session of 2026-10-27, written 14:39.

## What was done

- **Report:** AI weapon bashes play, but the enemy shows no reaction.
- **Engine reading:** `Mission.MeleeHitCallback` registers no blow for a blocked, parried or chamber-blocked collision. The user found the other cause: a kick or bash that does 0 damage is not applied either. With the damage curve, a kick or bash's few points often round to 0.
- **Fix,** `RBM.Multiplayer/DamageCurve.cs`: `AlternativeAttackMinimumPatch`, a postfix on `Mission.GetAttackCollisionResults`. It applies to a kick, shield bash or weapon bash on an enemy that isn't cancelled, blocked, parried or chamber-blocked. If such a hit ends below 1 damage, it gets 1. Server only, like every patch.
- **A server diagnostic was added and then removed at the user's request.** It was `Stance/KickBashDiagnostics.cs` with hooks in `RbmMpServerLogic`, plus `RbmMpLog.Detailed` turned on. All of it is reverted.
- `README.md`: a note on the minimum under the damage curve.
- **State:** installed on both sides. The server was restarted with `-Build` as pid 5252: 96 applied, 0 failed (one more than before, the new patch). Not played, committed.

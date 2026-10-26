# Handoff: play test, the last day's work verified

Session of 2026-10-26, written 12:48. It follows `2026-10-26-1234-thrown-weapons.md`.

## State

- **Committed:** everything up to `66aea48` (sidearms, Empire javelins). This commit holds only this handoff and the plan statuses. No code changed.
- **Running:** server pid 25088, map `battle_terrain_026`. It runs the `66aea48` build ("94 classes applied, 0 failed, 3 skipped").
- **Client:** the same build (log `CommanderBattle_33912.log`).

## What was done

The user played on server 25088 with one client and reported that everything they tested worked. The logs agree:
- the client joined at 12:38;
- Fellow93 took Attacker infantry 0, then cavalry 2, then infantry 0 again;
- Round 1 started at 12:38:53;
- neither the server nor the client logged an exception.

## Verified vs not

**Verified in play (the user's report, 2026-10-26):**
- **Troops and weapons:**
  - the troop selection screen (class → weight → weapons), and the pick spawning next round;
  - the sidearm rule;
  - Empire javelins thrown on foot and from the saddle.
  - This covers `2026-10-25-1516` (troop types v2), `2026-10-25-1553` (variance pass), `2026-10-26-1220` and `2026-10-26-1234`.
- **Admin panel:** Next Map, Restart Round, Swap Sides, Remove Commander and Players (`2026-10-26-1009`).
- **Hints and order bar:**
  - F to take control of the spectated soldier, and the N line in the Alt panel (`2026-10-26-1158`);
  - the order bar and F keys after a map change (`2026-10-25-2353`).
- **Combat and AI:**
  - slower bow and crossbow fire (`2026-10-25-2233`);
  - the more aggressive Frontline (`2026-10-26-0902`);
  - the posture bars;
  - no stutter from hold releases (`2026-10-26-0020`).

**Not verified:**
- The user didn't list per-culture checks, so these remain open:
  - all six cultures' class counts against the XML;
  - Khuzait's two horse archer formations;
  - pikemen without shields.
- The "Classes for" line is `Detail` only.
- Nothing was tested with more than one human player.

## Open items

- **Troop types phase 4, the balance pass:** multipliers, speeds and weapon setups. This is next on that plan.
- **Admin commands, items 3–5:**
  - army size for the next round;
  - toggles (horse speed, spawn randomisation, damage curve);
  - forcing a tactic.
  - These need runtime settings applied at a round boundary on both sides.
- **Choosing troops, option 4:** each side picks its own role split. Deferred, with an open question for the user (who decides it, and how often).
- **Ideas not started:**
  - deployment phase;
  - general role;
  - end-of-round stats;
  - spectator camera.

## Traps

None new.

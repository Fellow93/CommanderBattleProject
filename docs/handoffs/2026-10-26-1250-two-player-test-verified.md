# Handoff: two-player test, the rest verified

Session of 2026-10-26, written 12:50. It follows `2026-10-26-1248-play-test-verified.md` (same session).

## State

- **Committed:** everything up to `e8b9a26`. This commit holds only this handoff and the plan statuses. No code changed.
- **Running:** server pid 25088 on the `66aea48` build.

## What was done

The user played with a second human player and reported that the items the 12:48 handoff left open all work:
- each culture's classes match its XML;
- Khuzait field two horse archer formations;
- pikemen spawn without shields;
- the game with more than one human player.

The general role was talked through, and nothing was built. The design is below under open items.

## Verified vs not

- **Verified in play (the user's report):** the items above. Together with the 12:48 handoff, every "not played yet" item since `2026-10-25-1516` is now played.
- **Not in the local logs.** No local server log today shows a second player. In every one, Fellow93 is the only player who takes a formation. Server 25088's log stops at "Round 1: InProgress" (12:38:53), with no round end since. Maybe the two-player game ran on another host. If it ran on 25088, check that the server isn't stuck mid-round.
- The troop types plan's "done when" is met, except for the balance pass (phase 4).

## Open items

- **Troop types phase 4, the balance pass.**
- **Admin commands, items 3–5:** army size, the toggles, forcing a tactic.
- **General role.** The design from this session:
  - Forcing a tactic means cutting the team's option list down to the chosen tactic, then `ResetTactic(false)`. `TeamAIComponent.MakeDecision` takes the best-weighted option every 5 s, so a single option sticks. Register it through `RbmMpTactics`' `Add` so RBM's `TacticSelection` knows it. "AI chooses" restores the full list.
  - The admin "force tactic" uses the same mechanism.
  - The stance filters that list (offensive tactics only, or defensive only). Unchecked: whether RBM's defensive tactics work on the attacking side.
  - Network messages: `RequestGeneral`, `SetSideTactic`, `SetSideStance`. The UI is a General row in the formation list (N), plus tactic and stance buttons in the Alt AI panel.
  - Check first that `FormationOwnershipGuard` already keeps tactics from leaning on player formations.
  - The plan's three open questions are still with the user. The suggestion was: first claim, the general may also lead a formation, tactic + stance first.

## Traps

None new.

# End-of-round stats per formation

**Status:** idea · **Size:** M

## Goal

At the end of each round, and for the whole match, everyone sees how each formation did: who commanded it, its troops, kills, losses and survivors. It gives commanders feedback and a reason to come back.

## What exists today

- The scoreboard is the stock one with `CaptainScoreboardData` (`CommanderBattleMissions`), which shows players and bot counts, not formations.
- The server already sees every death (`MissionMultiplayerCommanderBattle.OnAgentRemoved`), and the team AI keeps a `CasualtyHandler`.
- Slots know their commander, role, class and troop count (`FormationSlot`).
- Fleeing soldiers leave through `CommanderBattleFleeingLogic`, which logs them.

## Design

**Counting, on the server only:**
- Per formation slot, per round: starting soldiers, killed (by enemy), fled, survivors at round end, enemy soldiers killed, damage dealt (optional), commander(s) during the round.
- Credit a kill to the formation of the killer (`affectorAgent.Formation`). Mounts and horse kills don't count.
- If the AI regroups soldiers between formations (RBM does), count by the agent's formation at spawn, not the current one. Keep a map agent → spawn slot, filled at spawn.
- Keep match totals per commander (player), so a commander who changes formation keeps their score.

**Sending:** one message per side at round end, `RoundStats`, with a row per slot (small: 16 rows of a few ints). Send it to all players. Players who join get the match totals.

**Showing**, simplest first:
1. A panel at round end, during the stock end-of-round screen, with a row per formation: commander, troops, kills, losses, survivors. Highlight the best formation on each side.
2. The formation list (N) shows the match totals.
3. Write the table to `CommanderBattle_<pid>.log` as well, for balancing.

## Things to work out

- When the stock end-of-round UI shows, and whether a Gauntlet layer can sit on top of it. Read `..\RealisticBattleProject\docs\BannerlordGauntletUI.md` first.
- Whether the stock scoreboard's player rows can carry formation kills (they show player score from `MissionScoreboardComponent`). Feeding commanders' formation kills into their player score could be enough for version 1.
- Friendly fire counts as a loss, not a kill.

## Files it touches

- New `CommanderBattle/CommanderBattleRoundStats.cs` (server counting, a mission behaviour added in `CommanderBattleMissions`).
- `CommanderBattle/Network/RoundStats.cs` (shared).
- New `CommanderBattle.View/CommanderBattleRoundStatsView.cs` + VM + prefab.
- `CommanderBattle.View/FormationSelectionVM.cs` for match totals.

## Open questions for the user

1. Which columns matter most: kills and losses only, or also damage, survivors, time held?
2. Per round only, or also match totals per player?
3. Should a commander's formation kills count toward their stock scoreboard score?

## Done when

- In game: the round-end panel shows all 16 formations with numbers that add up (losses on one side ≈ kills by the other, minus fleeing and friendly fire).
- Logs: the same table per round.

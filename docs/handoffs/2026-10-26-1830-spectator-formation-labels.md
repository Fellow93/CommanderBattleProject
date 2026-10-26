# Handoff: spectators see the formation labels

Session of 2026-10-26, written 18:30. It follows `2026-10-26-1828-enemy-formation-labels.md` (same session). The state there still holds:
- not committed;
- the client is installed and the server isn't needed;
- server pid 21560 belongs to another session.

## What was done

At the user's request, a spectator on neither side (`MySide == None`) now sees both sides' formation labels on Left Alt. Before, they saw none. In `CommanderBattleAllyPlayerMarkersView.cs`:
- `GetLabelSide` gives such a spectator defenders in the first block and attackers in the second.
- The out-of-sight check now asks the handler for every formation, not only enemy ones. The handler evaluates only teams that aren't the player's allies: for a player, the enemy, as before; for a spectator, both sides. That spectator's labels then hide with the stock icons out of sight.

A dead player still on a side was already covered: their side stays `MySide`.

The player name markers are unchanged: allies only, so a spectator off both sides gets none.

`README.md`: the Alt paragraph and the checklist item.

## Verified vs not

- **Verified:** the build (0 errors) and the client install.
- **Not played.** Join as a spectator with players on both sides and hold Alt.
  - **Open question:** does the stock icon (and so the label) hide formations out of a spectator camera's sight? That depends on the server's `FormationTargetingVisibility` option.

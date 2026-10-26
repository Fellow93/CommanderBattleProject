# Handoff: the general and his wedge, played and approved

Session of 2026-10-26, written 14:18. It follows `2026-10-26-1409-bodyguard-wedge.md` (same session).

## State

- **Not committed:** all of the general role work (13:40, 13:51, 13:59, 14:09 handoffs) and this handoff. No code changed since 14:09.
- **Running:** server pid 32248, on the 14:09 build.

## Verified

The user played the 14:09 build. The general with 6 bodyguards riding in a wedge, the player at its tip, "looks good now".

The lobby crash fix (13:51) is verified by the same session, since the client reached the game.

## Still open

- The parts of the README's general checklist the user didn't mention:
  - the AI general staying behind the line;
  - the formation list fitting 9 rows;
  - taking over a rider after the general dies;
  - gear looks: the Vlandian crowned helmet, the Khuzait bodyguard without a bow, possible cape and harness clipping.
- Next phases of `docs/plans/general-role.md`: the general steers the team AI (tactic, then stance).
- From 13:40: our layout never calls `SetSpawnWithHorses`, so cavalry may stand behind the infantry rather than on the flanks as the README says. Check in game.

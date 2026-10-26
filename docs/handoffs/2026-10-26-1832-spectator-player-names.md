# Handoff: spectators see player names

Session of 2026-10-26, written 18:32. It follows `2026-10-26-1830-spectator-formation-labels.md` (same session). The state there still holds:
- not committed;
- the client is installed and the server isn't needed;
- server pid 21560 belongs to another session.

## What was done

At the user's request, a spectator on neither side (`MySide == None`) now sees the name markers of players on both sides within 100 m of the camera. A player on a side still sees only allies.

In `CommanderBattleAllyPlayerMarkersView.cs`:
- `Markers` holds 2 × `SlotCount` and uses the same `GetLabelSide` mapping as the formation labels.
- The enemy block stays empty for a player on a side.
- `TryGetAlly` is renamed `TryGetPlayer`.

`README.md`: the Alt paragraph and the checklist item.

## Verified vs not

- **Verified:** the build (0 errors) and the client install.
- **Not played.**

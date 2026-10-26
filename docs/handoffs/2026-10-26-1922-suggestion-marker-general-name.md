# Handoff: suggestion markers signed with the general's name

Session of 2026-10-26, written 19:22. It follows `2026-10-26-1802-general-orders-v1.md` (general orders session).

**Another session is working in this checkout at the same time:** ally markers, formation labels, the cheer, and the takeover hold fix; see the 18:21–19:17 handoffs. It restarted the server (now pid 30484).

## What was done

The user asked that a commander's Alt marker for a suggestion reads like "Fellow: move here", not "General: move here".

- **`CommanderBattle.View/CommanderBattleAIStatusView.cs`:**
  - `RebuildSuggestions` uses the new `GetGeneralName(side)`: the general slot commander's `UserName`, or "General" if the slot is empty.
  - The general's own markers are unchanged ("Infantry 3: move here").
  - The pop-up text in `MissionMultiplayerCommanderBattleClient.GeneralCommand.cs` still reads "General: Infantry 1, move here"; the user asked only about the map.

## State

- **Not committed:** this change, along with all the uncommitted general orders and Ctrl+C work, and the other session's work.
- **Installed:** the client View project (built alone; 0 errors). It is client-only, so the server doesn't need a restart.
- **Not played.**

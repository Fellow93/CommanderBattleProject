# Handoff: formation column on the scoreboard

Session of 2026-10-25, written 16:40. Runs alongside `2026-10-25-1638-posture-animation-sync-fix.md`, which owns the uncommitted `RBM.Multiplayer` changes and the running server.

## State

- **Not committed:** `CommanderBattle/CommanderBattleScoreboardData.cs` (new), `CommanderBattle/CommanderBattleMissions.cs`, `Module/ModuleData/commander_battle_strings.xml`, `README.md`.
- **Installed (client only):** only `CommanderBattle.csproj` was built (Release, both frameworks compile). The game's client `CommanderBattle.dll` matches `CommanderBattle\obj\Release\net472\`, and the strings file is installed in both module folders.
- **Not installed on the dedicated server:** its `CommanderBattle.dll` is locked by the running server (pid 26700, from the 16:38 session), so the copy failed (MSB3027) and it is still the 16:17 build. The feature is client-side and adds no network message, so the old server still works with the new client. The next restart with `-Build` installs it.

## What was done

- `CommanderBattleScoreboardData` (`IScoreboardData`) takes the stock `CaptainScoreboardData` headers and inserts a `formation` column after `name`. For each player it finds their slot (`FormationSlotTable.FindSlotOf`) and shows "Heavy Infantry 3 · Spear and shield". The weight label is left out when the role has a single class, and the weapons when there's no setup. A player without a formation gets "-"; the bots' row is empty, since it stands for all of a side's AI formations at once.
- Both mission lists use it in place of `CaptainScoreboardData` (`CommanderBattleMissions.cs`).
- Column title: `str_scoreboard_header.formation` → "Formation" in `commander_battle_strings.xml`. `ScoreboardHeader` looks its title up by id there.
- The stock prefab (`Multiplayer/GUI/Prefabs/Scoreboard/MultiplayerScoreboardSideInner.xml`) draws any column id other than badge/avatar as a `ScrollingRichTextWidget`, so no prefab change was needed.

## Verified

- Both frameworks compile; the client DLL is installed.

## Not verified

- Not seen in game: the column title, the text, and whether the stock scoreboard refreshes the value when a player takes another formation or their pick applies at the round start.
- The column is narrow (the stock width ratio for an unknown column is 1, name has 3), so long texts scroll. If that reads badly, shorten the text (e.g. "Hvy Inf 3 · Spear"); avoid overriding the stock prefab, which other MP modes share.

## Open items

- Restart the server with `-Build` when the 16:38 session is done with it, then check the column in game.

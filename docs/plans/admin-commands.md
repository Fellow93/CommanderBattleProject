# Admin commands

**Status:** idea · **Size:** M

## Goal

A server admin can control a match without restarting the server: restart the round, swap sides, change army size and settings, force a map. It makes testing with friends and running events much easier.

## What exists today

- The stock **`MultiplayerAdminComponent`** is in the mission (`CommanderBattleMissions`), so the stock admin panel works: kick, ban, change map, and the stock options.
- **Polls** are on (`MultiplayerPollComponent`, map-change polls allowed).
- Commander Battle's own settings are **constants** (`CommanderBattleSettings`: `FootFormationBudget` / `MountedFormationBudget`, the culture splits, `HorseSpeedFactor`, the spawn jitter), so none of them can change while the server runs.

## Design

**How admins send commands.** Check first what the game already offers:
- the stock admin panel (in the escape menu for admins): can a mod add buttons or options to it?
- chat commands: do the current game versions have a chat-command hook for mods?
- the dedicated server's console: it accepts commands, and mods can register their own (`[CommandLineFunctionality.CommandLineArgumentFunction]`). That works from the server window and the web panel, but not for an admin playing in game.

Without a usable hook, add a client message (`AdminCommand`, shared dll) that the server only accepts from peers the stock admin system marks as admin (check how `MultiplayerAdminComponent` knows a peer is admin), plus a small admin panel in `CommanderBattle.View`.

**Commands,** in order of value:
1. **Restart round** — end the current round with no winner and start the next one.
2. **Swap sides** — move every player to the other side, keeping their formation index if free.
3. **Army size** — the formation budgets (`FootFormationBudget` / `MountedFormationBudget`) for the next round (e.g. 200–800, under the engine's agent cap). It needs to be a runtime value on the server only: the classes turn each formation's budget into its troop count (`MissionMultiplayerCommanderBattle.ComputeTroopCounts`), and `SpawnLayoutSeed` already sends the resulting counts to the clients.
4. **Toggles** for the next round: horse speed factor, spawn randomisation on/off, damage curve on/off (RBM.Multiplayer).
5. **Force tactic** for a side, or freeze the AI (for testing). Overlaps with `general-role.md`.
6. **Kick a commander** from their formation (back to the AI) without kicking them from the server.

Every command is logged in `CommanderBattle_<pid>.log` with who sent it.

## Things to work out

- Settings that both sides compute (army size, horse speed, jitter) must change on the server and every client at the same moment, at a round boundary, never mid-round.
- `RoundController` has no public restart. Check what the stock admin "end round"/"restart" does, if anything, in `TaleWorlds.MountAndBlade.Multiplayer` (not in the decompiled folder; decompile with `ilspycmd`).
- Reset per-round state the same way a normal round end does (`OnClearScene`).

## Files it touches

- `CommanderBattle/CommanderBattleSettings.cs`: the values that become runtime settings.
- A new `CommanderBattle/CommanderBattleAdminLogic.cs` (server) and `CommanderBattle/Network/AdminCommand.cs` + a settings message (shared).
- `CommanderBattle/MissionMultiplayerCommanderBattle.cs`: restart, swap, kick from formation.
- `CommanderBattle.View/`: an admin panel, if no stock hook fits.

## Open questions for the user

1. Where should admins type commands: in game (panel or chat) or the server console is enough?
2. Which settings should be changeable without a restart?

## Done when

- In game: an admin restarts a round, swaps sides and changes army size for the next round. A non-admin's request is refused.
- Logs: every command, who sent it, and the settings each round started with.

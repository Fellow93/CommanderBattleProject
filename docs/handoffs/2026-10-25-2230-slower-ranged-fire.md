# Handoff: bows and crossbows fire slower

Session of 2026-10-25, written 22:30. Separate from the spawn-path session (`2026-10-25-2208-spawn-path-sync.md`), whose uncommitted work is still in the checkout.

## State

- **Not committed:** this pass's `CommanderBattle/CommanderBattleAgentStatCalculateModel.cs`, `CommanderBattle/CommanderBattleSettings.cs`, `README.md` (one line), this handoff and its index row. The 22:08 handoff's uncommitted files are untouched.
- **Not installed.** Both frameworks compile (no CS errors), but every DLL copy failed: the dedicated server (pid 35176, started 22:08:52) and the Bannerlord launcher (pid 27456, started 22:17:19) hold the installed DLLs. Neither was started by this session, so neither was stopped.
- **Running:** that server and launcher, as above.

## What was done

- `CommanderBattleAgentStatCalculateModel.UpdateAgentStats`: when a human's wielded weapon is a bow or a crossbow, `ThrustOrRangedReadySpeedMultiplier` (draw, or crossbow raise) and `ReloadSpeed` (taking the next arrow or bolt) are multiplied by the factor. Only in Commander Battle missions, the same check as the horse speed.
- `CommanderBattleSettings.BowFireSpeedFactor` and `CrossbowFireSpeedFactor`, both 0.75.
- The model runs on the server and every client, so players' and AI agents' values agree on both sides. Players keep the change: `PlayerStatGuard` snapshots inside `SetAiRelatedProperties`, which runs inside `base.UpdateAgentStats`, before this multiply. RBM Stance's stamina scaling is multiplicative and commutes with it (its in-place rescale too).
- Throwing weapons and slings are unchanged.

## Verified

- Compiles on net472 and net6.0.

## Not verified

- Not installed or run. After closing the game and the server: `tools\Start-Server.ps1 -Build -Restart`, join, and watch an archer and a crossbowman reload; check that a player's own bow feels smooth (no snapping back of the draw) and the server log shows no exceptions.

## Open items

- Tune the factors after a playtest. The AI's fire rate also depends on aim time (`WeaponBestAccuracyWaitTime`, `AiWaitBeforeShootFactor`), which RBM AI sets; this pass leaves those alone.

## Traps

- The first build in this pass failed only on copying: a server and launcher started after the 22:08 handoff hold the DLLs. Check `Get-Process` before assuming nothing runs.

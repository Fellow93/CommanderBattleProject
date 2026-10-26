# Handoff: client test zip; campaign scenes linked automatically

Session of 2026-10-25, written 22:45. Other sessions share this checkout (see the 22:33 and 22:36 handoffs).

## State

- **Not committed:** `CommanderBattle/CampaignSceneLinker.cs` (new), `CommanderBattle/CommanderBattleSubModule.cs` (one call, `#if !SERVER`), `README.md` (scene paragraph), this handoff and its index row.
- **Installed:** client `CommanderBattle.dll` at 22:44. It carries every working-tree change in the checkout at that time, the other sessions' too. Server copy failed (MSB3027, locked by pid 34388); this change is client-only.
- **Running:** server pid 34388 (another session's), started 22:33 with the 22:33 `CommanderBattle.dll`.
- **Zip:** `artifacts\CommanderBattle-client-2026-10-25.zip` (gitignored), 1.6 MB: `Modules\CommanderBattle` + `Modules\RBM.Multiplayer`, client bins only, no `SceneObj`, plus `README.txt` for the player. Its `server_address_overrides.txt` has the user's `91.127.98.139 = 127.0.0.1` rewrite removed: on a remote player's PC it would send the join to their own machine.

## What was done

- `CampaignSceneLinker.LinkMissingScenes`, called in `OnSubModuleLoad` on clients. It reads `SandBox\ModuleData\sp_battle_scenes.xml`, then junctions every missing scene from `<game>\Modules\SandBoxCore\SceneObj` into the module's own `SceneObj`. The module folder comes from the dll's location, so a Workshop install works too. It uses one temporary `.cmd` running `mklink /J`, since .NET Framework has no junction API. Then it calls `Utilities.PairSceneNameToModuleName(id, "CommanderBattle")` for each scene, in case the engine indexed the module's scenes before this ran. It logs "Campaign scenes: N of M available, X of Y missing ones linked now".
- The dedicated server still uses `tools\Install-CampaignScenes.ps1`: its install has no SandBoxCore of its own.

## Verified vs not

- Builds (net472 and net6.0). **Not run yet.** This PC already has all the links, so nothing gets created here.
- Unknown: whether the engine finds a scene linked during `OnSubModuleLoad` in the same session, or only after a restart. The pairing call should cover it. The player README says to restart once if a map fails on the first start.

## Verify next

- On a fresh client, or with `Modules\CommanderBattle\SceneObj` renamed: start the game. The `CommanderBattle_<pid>.log` should show "M of M available, M of M missing ones linked now". Then join and load a campaign map without restarting.
- Before a remote player joins: restart the server with `-Build` so it runs the same build as the zip (its dll is from 22:33, the zip's from 22:44).

## Traps

- `Path` is ambiguous between `System.IO` and `TaleWorlds.Engine` once both are imported: alias it.
- PowerShell 5.1 `Compress-Archive` writes backslash entry names. Windows Explorer and 7-Zip handle them; some non-Windows unzippers don't.

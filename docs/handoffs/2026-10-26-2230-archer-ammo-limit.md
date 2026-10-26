# Handoff: archers save their ammo

Session of 2026-10-26, written 22:30. Separate from the commander banners session (`2026-10-26-2227-commander-banners.md`), whose uncommitted work is still in the checkout.

## State

- **Not committed:** `RBM.Multiplayer/AI/AmmoLimitGuard.cs` (new), `RBM.Multiplayer/RbmMpPatcher.cs`, `README.md` (one bullet), this handoff and its index row. The banners session's uncommitted files are untouched.
- **Not installed on the server.** `RBM.Multiplayer` compiles on net472 and net6.0 (no CS errors); the server copy failed because the running server (pid 23668, started 22:25 by the banners session) holds the DLL. The client copy is irrelevant: the patch only runs on the server.
- **Running:** server pid 23668, without this change. Not stopped by this session.

## What was done

- RBM AI's `HumanAIComponent.OnTick` postfix (`RealisticBattleAiModule/AiModule/Agents/TickPatches.cs:243`) sets `Agent.AISpecialCombatModeFlags.IgnoreAmmoLimitForRangeCalculation` on every ranged agent, every tick. Vanilla sets that flag only in `Formation.AddUnit`, for agents `AmmoSupplyLogic` resupplies (siege defenders). Without it, the engine shortens an AI archer's shooting range as his ammo runs low.
- `AmmoLimitGuard`: a `com.rbm.multiplayer` prefix on `Agent.SetScriptedCombatFlags` that strips the flag on the server. It is stripped at the setter, rather than cleared after RBM's postfix, because RBM sets it again every tick.
- `RbmMpPatcher.SmallAgentMethods` now includes `SetScriptedCombatFlags`, so the guard is patched before RBM's `OnTick` replacement can inline the setter (the same trap as `SetActionChannel`).
- README: an "Archers save their ammo" bullet in the RBM section.

## Verified

- Compiles on both frameworks.

## Not verified

- Not installed or run. `tools\Start-Server.ps1 -Build -Restart` (this stops pid 23668, so check with whoever is using it first). Look for "0 failed" in the patch line, then watch whether archers hold fire longer and still have arrows when the lines meet.
- The engine's exact rule for the shortened range is native and wasn't read; the vanilla use of the flag is the only evidence of what it does.

## Open items

- If archers now hold fire too long, the middle ground is to keep the flag until a quiver falls below a fraction (for example half). That would replace the blanket strip with a check on `agent.Equipment.GetAmmoAmount`.
- RBM re-sets the flag every tick, so each ranged agent now costs one native get and one native set per tick (before: one get). Negligible but measurable if ranged-heavy rounds ever show tick-time trouble.

## Traps

- Any new Harmony patch on a small `Agent` method must go into `RbmMpPatcher.SmallAgentMethods`, or calls from RBM replacements compiled earlier skip it.

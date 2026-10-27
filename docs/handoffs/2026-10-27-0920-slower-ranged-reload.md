# Handoff: bows and crossbows reload slower

Session of 2026-10-27, written 09:20.

## What was done

- The bow and crossbow reload used to share the fire factor, 0.75. It now has its own factors, `CommanderBattleSettings.BowReloadSpeedFactor` and `CrossbowReloadSpeedFactor`, set to 0.67. The draw / crossbow raise (`BowFireSpeedFactor`, `CrossbowFireSpeedFactor`) stays at 0.75.
- `CommanderBattleAgentStatCalculateModel.SlowRangedFire`: `ThrustOrRangedReadySpeedMultiplier` uses the fire factor and `ReloadSpeed` the reload factor.
- `CommanderBattleSubModule`: the Detail log line includes the reload factors.
- `README.md`: the ranged fire bullet.
- The handoff format is now "what was done" only: `docs/handoffs/README.md` and `CLAUDE.md`.

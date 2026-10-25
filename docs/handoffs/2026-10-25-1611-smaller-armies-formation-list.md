# Handoff: smaller armies, restyled formation list

Session of 2026-10-25, written 16:11. Follows `2026-10-25-1553-culture-variance-pass.md`.

## State

- **Nothing is committed.**
- **Installed:** full Release build at about 16:04, 0 errors and 0 warnings. Client DLLs are net472 and server DLLs net6.0, all hash-identical to `obj\Release\<tfm>\`. The new formation-list prefab is installed.
- **Running:** dedicated server pid 9684, battle_terrain_m, Aserai (defender) vs Battania (attacker). The user is playing round 1 (they lead Attacker formation 0).

## What changed

- **Lag diagnosis (round on pid 26872):** server FPS went from 60 to 18–35 once ~1,300 agents (1,006 soldiers plus horses) engaged, under RBM's per-agent AI at that scale.
- **Smaller armies.** The user asked for lower counts overall, with mounted formations cut more.
  - `CommanderBattleSettings.FootFormationBudget` = 54 and `MountedFormationBudget` = 44, replacing `TroopBudgetPerSide` 500 / 8 = 62–63.
  - `FormationSlot.Budget` is read from the slot's role. `TroopCount` falls back to the budget until counts are set.
  - The spawn layout's fallback uses the role budget.
  - README and plans are updated.
- **Formation list restyled** in the troop screen's style (`Module/GUI/Prefabs/CommanderBattleFormationSelection.xml`, `FormationSelectionVM`):
  - dark panel, frame lines, notched divider, and a summary line ("3 infantry · 2 ranged · ... · N men");
  - rows grouped by class, with the compass icon via `MultiplayerClassLoadoutTroopSubclassButtonWidget`;
  - each row shows troop name, "weight · weapons · N men", a pending line and the commander;
  - your own row is highlighted and has a Troops button;
  - hint and Done in the footer.

## Verified (pid 9684, round 1)

- Troop counts: Aserai 394, Battania 405.
  - Foot formations at budget 54 by weight: light 64–69, medium 53, heavy 41–44.
  - Mounted at 44: Aserai heavy cavalry 35, horse archers 44, Battanian medium cavalry 40.
- AI setups vary: spear + shield, sword + shield, axe + shield, pike, falx, skirmishers, both bows.
- **Agents on the field: 919 (was 1,307).**
- **Server FPS during the fight:** 68 drops below 60 with an average of 55. Lowest 28.8; 1 drop below 30, none below 20. The last round's drops were mostly 18–35.

## Not verified

- The restyled formation list in game; the user has not commented on it yet.
- The rest of the in-game checklist from the 15:16 and 15:53 handoffs.

## Open items

- If the lag is still noticeable: run the server at `/tickrate 30`, or measure a round with RBM's AI off.
- The balance-pass items from the 15:53 handoff (shields on two-handers' backs, the Aserai adarga, Palatine Guard armour, Khuzait bows).

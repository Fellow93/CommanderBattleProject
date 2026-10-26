# Choosing your troops

**Status:** in progress (options 1–3 built and played 2026-10-26; option 4, the role split, deferred) · **Size:** M

## Goal

A commander chooses what kind of troops their formation fields, and that choice matters and is visible to everyone.

## What exists today

More than the README says:
- Each formation slot has a **class** (`FormationSlot.ClassIndex`, an index into the culture's `MultiplayerClassDivisions` hero classes).
- A commander picks it in the **stock class loadout**. The server limits the picker to the formation's role (`MissionMultiplayerCommanderBattle.UpdateClassRestrictions`, through `ChangeClassRestrictions`). It follows the commander's pick and forces it back if a class of another role slips through (`SyncCommanderClasses`).
- **AI-led formations** cycle through the role's classes, so a side gets e.g. light, heavy and shock infantry (`AssignDefaultClasses`).
- The formation list shows each formation's troop name (`FormationSelectionVM`).
- The troops spawn as that class (`CommanderBattleSpawningBehavior`), with the troop count fixed by the slot (`GetTroopNumberMultiplierForMissingPlayer`), whatever the class costs in stock Captain mode. (Before option 3. `GetTroopNumberMultiplierForMissingPlayer` and `SpawnAiFormations` already spawn `FormationSlot.TroopCount`, so option 3 only had to set that count per round.)
- The AI's weights need no fixed counts: `CommanderBattlePowerLogic`, `CommanderBattleTactic` and RBM's tactics read the live formations. The strength bar measured against the most soldiers seen.

## What's missing, and options

1. **Document it and check it in game** (S). Confirm the pick works, perks apply to the troops, and the class sticks across rounds. Add it to the README.
2. **Perks as part of the choice** (S). Stock Captain perks change the troops (more shields, banner bearer...). Check which make sense here. Some change troop counts, which `GetTroopNumberMultiplierForMissingPlayer` already overrides. The banner bearer is already accounted for.
3. **Cost and size** (M). Heavier classes field fewer men, light ones more, so the choice is a real trade-off. For example, use the stock class's troop count for 100 bots (`MPPerkObject.GetTroopCount`) as a weight and share the side's 500 out by it. The spawn layout, the strength bar and the AI's weights must all use the new counts, on server and client alike.
4. **Army composition per side** (M–L). Let the side, or the general (see `general-role.md`), choose the 4/2/2 role split before the match, within limits (e.g. 3–5 infantry). `CommanderBattleSettings.FormationRoles` is a constant today, and the layout, slots and network messages assume it. That makes this the most work.

## Files it touches

- 1–2: `README.md`, maybe `CommanderBattle/MissionMultiplayerCommanderBattle.cs`.
- 3 (as built): `MissionMultiplayerCommanderBattle` (`ComputeTroopCounts`, `UpdateTroopCounts` during preparation, `FreezeTroopCounts` at round start, the log), `FormationSlots.cs` (`Budget`, per-round `TroopCount`, `GetTroopCounts`/`SetTroopCounts`), `CommanderBattleSettings` (`GetTroopBudget`), `CommanderRole.cs` (`GetAverageStockTroopCount`), `Network/SpawnLayoutSeed` (carries the counts), `CommanderBattleSpawnLayout` (plans with the counts), `MissionMultiplayerCommanderBattleClient` (receives them; strength bar), `CommanderBattle.View/FormationSelectionVM.cs` (side total).
- 4: also `CommanderBattleSettings.FormationRoles`, `FormationSlotUpdate`, `CommanderBattle.View/FormationSelectionVM.cs`.

## Decisions (2026-10-25)

- **Scope: options 1–3.** The role split (4) is left for later.
- **A budget, not a fixed 500.** Each slot keeps its budget (`GetTroopBudget`, 62–63 men at the reference class) and the class sets how many men that buys. A side's total varies with its picks.
- **Weight: the stock Captain troop count,** `MPPerkObject.GetTroopCount(heroClass, 100, perks)`, i.e. `ceil(100 × multiplier)` plus perk extras. The stock multipliers (`Native/ModuleData/mpclassdivisions.xml`) run 0.30–0.92, and cavalry sits far below infantry, so each class is compared within its role.
- **Reference: the role's average.** men = budget × stock count(class, perks) / average stock count of the culture's classes of that role (no perks). Light classes get about 75–80, heavy ones about 50. Vlandia: 517 with the default classes, 410 all heavy, 618 all light.
- **Perks: count perks change the share.** A commander's perk extras feed the stock count. AI-led formations have no perks.
- **RBM's regroup stays as it is.** RBM's tactics regroup AI-led formations into even sizes (62–63 men, with the infantry merged), so class-based sizes show up only in the side totals and in player-led formations. The user decided that is enough.

## Open questions for the user

1. Should sides pick their own role split (4)? If so, who decides, and before each match or each round? (Deferred.)

## Done when

- In game: a commander's class pick spawns that troop type and survives the next round; other players see it in the formation list.
- For 3: the counts in the log match the class weights and the spawned formations. The side total varies with the picks (see Decisions).

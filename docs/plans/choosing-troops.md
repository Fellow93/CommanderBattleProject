# Choosing your troops

**Status:** idea · **Size:** S–M (depends on how far it goes)

## Goal

A commander chooses what kind of troops their formation fields, and that choice matters and is visible to everyone.

## What exists today

More than the README says:
- Each formation slot has a **class** (`FormationSlot.ClassIndex`, an index into the culture's `MultiplayerClassDivisions` hero classes).
- A commander picks it in the **stock class loadout**. The server limits the picker to the formation's role (`MissionMultiplayerCommanderBattle.UpdateClassRestrictions`, through `ChangeClassRestrictions`). It follows the commander's pick and forces it back if a class of another role slips through (`SyncCommanderClasses`).
- **AI-led formations** cycle through the role's classes, so a side gets e.g. light, heavy and shock infantry (`AssignDefaultClasses`).
- The formation list shows each formation's troop name (`FormationSelectionVM`).
- The troops spawn as that class (`CommanderBattleSpawningBehavior`), with the troop count fixed by the slot (`GetTroopNumberMultiplierForMissingPlayer`), whatever the class costs in stock Captain mode.

## What's missing, and options

1. **Document it and check it in game** (S). Confirm the pick works, perks apply to the troops, and the class sticks across rounds. Add it to the README.
2. **Perks as part of the choice** (S). Stock Captain perks change the troops (more shields, banner bearer...). Check which make sense here. Some change troop counts, which `GetTroopNumberMultiplierForMissingPlayer` already overrides. The banner bearer is already accounted for.
3. **Cost and size** (M). Heavier classes field fewer men, light ones more, so the choice is a real trade-off. For example, use the stock class's troop count for 100 bots (`MPPerkObject.GetTroopCount`) as a weight and share the side's 500 out by it. The spawn layout, the strength bar and the AI's weights must all use the new counts, on server and client alike.
4. **Army composition per side** (M–L). Let the side, or the general (see `general-role.md`), choose the 4/2/2 role split before the match, within limits (e.g. 3–5 infantry). `CommanderBattleSettings.FormationRoles` is a constant today, and the layout, slots and network messages assume it. That makes this the most work.

## Files it touches

- 1–2: `README.md`, maybe `CommanderBattle/MissionMultiplayerCommanderBattle.cs`.
- 3: `FormationSlots.cs` (`TroopCount` becomes per class), `MissionMultiplayerCommanderBattle.GetTroopNumberMultiplierForMissingPlayer`, `CommanderBattleSpawningBehavior.SpawnAiFormations`, `CommanderBattleSpawnLayout`, the client strength bar.
- 4: also `CommanderBattleSettings.FormationRoles`, `FormationSlotUpdate`, `CommanderBattle.View/FormationSelectionVM.cs`.

## Open questions for the user

1. Is option 1 (confirm and document) enough for now, or should the choice change troop counts (3)?
2. Should sides pick their own role split (4)? If so, who decides, and before each match or each round?

## Done when

- In game: a commander's class pick spawns that troop type and survives the next round; other players see it in the formation list.
- For 3: the counts in the log match the class weights, and the side total stays 500.

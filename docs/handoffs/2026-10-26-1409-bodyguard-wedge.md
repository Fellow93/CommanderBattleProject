# Handoff: 6 bodyguards in a wedge, the general at its tip

Session of 2026-10-26, written 14:09. It follows `2026-10-26-1359-bodyguard-behind-general.md` (same session), whose "10 m behind in a deep block" this replaces.

## State

- **Not committed:** all of the general role work (13:40, 13:51, 13:59) plus this change.
- **Installed:** both sides; the build is clean.
- **Running:** server pid 32248, map `battle_terrain_biome_148`. Patch line: "94 classes applied, 0 failed, 3 skipped".
- **Client:** restart the game.

## What was done

The user asked for 6 bodyguards, following in a wedge with the player at its tip.

- **`CommanderBattleSettings.GeneralBodyguardCount` = 6** (was 10). The count comes only from this constant: a single-class role fields its budget. The class comments in the six `cb_mpclassdivisions_<culture>.xml` wrongly said "10 (ceil(100 x 0.10))"; they now point at the constant.
- **`CommanderBattle/MissionMultiplayerCommanderBattle.Bodyguard.cs`, rewritten.** For a side whose `Team.GeneralAgent` is a living player:
  - Formation 8 gets `ArrangementOrder.ArrangementOrderSkein`: the stock `SkeinFormation`, a `LineFormation` whose files step back by half a spacing away from the middle file.
  - It also gets a custom form width of `LineFormation.CalculateWidth(Interval, UnitDiameter, CountOfUnits)`, i.e. one file per agent, a single V.
  - The heading is `FacingOrderLookAtDirection`: his average velocity above 2 m/s, else the last heading.
  - A Move order puts the middle front slot on him, then `Arrangement.ReserveMiddleFrontUnitPosition(general)`.
  - **How the tip slot stays his:** the middle front slot is `GetOrderedUnitPositionIndex(0)`, the tip. The player's own unit is moved to the back-most slot (`Formation.TryRelocatePlayerUnit`), so the 6 riders fill the V.
  - **Renewal:** when he moves 2 m or turns about 15°.
  - **Resizing:** the arrangement and width are set again when the unit count changes (a rider dies). A new arrangement object drops the reservation, so it is reserved on every renewal.
  - AI generals are untouched.
- **Docs:** the README general section and checklist (6 riders, the wedge, "General (7)"), and `docs/plans/general-role.md` (the user's decisions: 6 riders and the wedge).

## Verified vs not

- **Verified:** the build (0 warnings, 0 errors); the server started with 0 failed patches.
- **Not verified in play:**
  - the V shape and the general at its tip;
  - that no rider takes the tip slot;
  - how the wedge turns;
  - the spacing (the stock mounted interval);
  - behaviour when riders die (uneven V, then 5 files and so on).

## Open items

- **Play it.** If the V is too shallow, Skein's file step is fixed at half a spacing. A deeper wedge would need our own arrangement, or per-rider positions.
- **If a rider sits at the tip,** the reservation isn't holding: check the order of `SetArrangementOrder`, `SetFormOrder`, `SetMovementOrder` and `ReserveMiddleFrontUnitPosition`.
- Commit the whole general role work after the user's look.

## Traps

- **Stock Skein is one V-shaped rank per file count.** With one file per agent, its middle front slot (ordered index 0) is the tip, at local position (0, 0), so `GetMiddleFrontUnitPositionOffset` is zero there.
- **`Formation.SetArrangementOrder` builds a new arrangement object,** which drops a `ReserveMiddleFrontUnitPosition`.

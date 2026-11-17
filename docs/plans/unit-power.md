# Unit power in the unit editor

**Status:** in progress (built; the node tests pass, with hand-computed powers on made-up items and the fixture's men; the page and "Level this class" checked in the browser pane on a copy of the unit files; the men are not yet compared with a server's class log) · **Size:** S–M

## Goal

The unit editor shows how strong each unit's formation is next to the others of its formation class, and lets the user level them: formations of one class should come out roughly equal in strength. A soldier's strength is priced the way RBM prices a troop in singleplayer.

Not a goal: predicting who wins a matchup (cavalry against spears), balance between formation classes or between cultures (shown, not adjusted), the player's hero.

## What exists today

- The page ([unit-editor.md](unit-editor.md)) has every soldier's gear with the catalogue's item data, his skills, and `multiplier`.
- **Men per formation** (`CommanderRole.GetTroopCount`): stock count = `ceil(100 × multiplier − 0.00001)` (`MPPerkObject.GetTroopCount`, in float); men = `round(budget × stock count / the class's average stock count in the culture)`, 1 to 1023. The budget is `CommanderBattleSettings.GetTroopBudget` (foot, mounted, the general's bodyguard). So inside a class only the ratios of the multipliers matter.
- **RBM's singleplayer troop power** is `StrategicTroopPower` (`..\RealisticBattleProject\RBMCampaign\Power\StrategicTroopPower.cs`, `Measure` and `PowerOfSet`). It prices a troop from his equipment and skills only; level and tier don't enter. Nothing in RBM's `tools\` computes it in JavaScript.

## Approaches

1. **A formula in the page (picked).** Reacts while editing and shows why a unit scores as it does. After the user's "similar to how power of unit is calculated in SP RBM", the formula is a port of `StrategicTroopPower`, not one of our own.
2. **Simulated duels in the page** with RBM Combat's damage rules: closer to the game, a large build, stale whenever RBM changes.
3. **Measured in game** by test battles: the truth, but slow and nothing updates while editing.

## Design

**One soldier: `power(unit)` in `unit-editor-core.js`,** a port of `PowerOfSet`, averaged over the troop's rosters with setup 0's gear (a roster without a priced weapon is skipped, as in RBM):

- **Melee:** per melee weapon, a ceiling by weapon class and blunt or not (RBM's `ClassCeiling` table) × `(1 + skill / 250)`; then `0.7 × best + 0.3 × mean`. Thrown weapons are not priced. Damage numbers, speed and length don't enter.
- **Ranged:** the best launcher (thrown weapons included, by energy) with matching ammunition: draw energy from the launcher's missile speed (RBM stores the draw weight there), a sling flat 110, crossbows ÷ 2.5; `× 0.7 × (1 + skill / 250)`. Only for Ranged and Horse archer units (RBM's `IsRangedTroop`) that have a priced launcher: `offense = max(melee, 0.7 × ranged + 0.3 × melee) × 1.35`; otherwise offense is melee, without the 1.35.
- **Charge:** `× (1 + 0.004 × horse charge)`.
- **Parry:** `(1 / (1 − a))^0.4`, `a = 0.45 + 0.30 × s` with a shield, `0.20 + 0.18 × s` without (at most 0.75), `s` the best melee skill / 250.
- **Armour:** RBM's six zones (head 0.16, neck 0.03, torso 0.44, shoulder 0.12, arm 0.14, leg 0.11, composed from the pieces as `GetArmorZonesRbm` does) + `4 × shield tier` (RBM's shield tier, 0 to 6.5); `1 + weighted / 50`.
- **Mount:** adds `product × (horse hit points + 2 × harness body armour) / 440 × 0.43`.
- **Scale:** `/ 272`, RBM's own, so the numbers read like RBM's.

**One formation:** men (the game's formula above, ported) × power.

**The page:**

```
Infantry                     power   men   formation
  Tribal Spearman             0.71    78     102%
  Veteran Infantry            1.00    62     100%
  Heavy Guard                 1.48    50     119%
  [Level this class]   multipliers 0.85 / 0.62 / 0.50 → 0.88 / 0.62 / 0.42
```

- The unit list shows power, men and the formation's strength as a percentage of its class's average.
- **Level this class** sets every multiplier of the class to `k / power`, with `k` keeping the class's average multiplier, so the men of an average unit stay as they are; when the weakest unit would pass 1, all come down together (only the ratios count in the game). Values are written with two decimals, between 0.01 and 1; units with power 0 keep theirs. It shows the new values first and applies them as one edit. Weight labels (Light/Medium/Heavy) follow the multiplier and may change.
- A **men** field next to `multiplier` shows the unit's men; typing a count sets the multiplier that gives it (`setMen`), or the nearest reachable count. Fixed for the general's guard and the only unit of a class.
- The unit's detail shows the parts of its power (melee, ranged, parry, armour, mount), so a change of gear or skill explains itself.
- The general's guard shows its power and men, without a percentage or levelling.

## Calls made here

- **Setup 0 only.** A unit has one `multiplier`, so the other weapon setups are not priced.
- **Left out of RBM's formula:** the weapon quality term (`Penetration`, a few percent; the catalogue has no damage factors), the long bow's lower efficiency (the catalogue has no item usage: every bow is priced at 0.90, a long bow's 0.835 would be 7% less), item modifiers, and everything per party (commander perks, morale, context).
- **Taken from the game, not the catalogue:** a weapon's skill (`GetRelevantSkillFromWeaponClass`) and a launcher's ammunition class (bow arrows, crossbow bolts, sling stones) follow from its weapon class; shield, ranged and ammo from its flags.
- **The troop's skills,** not the hero's: the formation is troops.
- **`IsRangedTroop`** is RBM's `troop.IsRanged` (formation class Ranged or Horse archer) or a sling carrier; here only the formation class, so a sling in an infantry unit's kit is not blended in.
- **Armour constants** are RBM's defaults (`armorMultiplier` 2, effectiveness 1, `RBMConfig.Combat.cs`); `RbmMpSettings` doesn't change them.
- **No tuning knobs.** The constants are RBM's; changing them is an edit of the core.

## Files it touches

- `tools/UnitEditor/unit-editor-core.js`: `power`, `menOf`, `levelClass`.
- `tools/UnitEditor/unit-editor-test.js`: tests on the frozen empire files.
- `tools/UnitEditor/index.html`: the three columns, the breakdown, the button.
- `tools/UnitEditor/README.md`, `README.md`.

## Open questions

None.

## Done when

- The tests pass: a hand-computed power for one infantry, one ranged and one cavalry soldier (on made-up items: the catalogue is gitignored, so the tests can't price the fixture's real gear); men equal to the game's count (compare with the server's class log); levelling brings a class's formation strengths within a few percent and keeps the average multiplier.
- In the page: changing a body armour or a skill moves the unit's power and percentage at once; "Level this class" changes only `multiplier` lines in `cb_mpclassdivisions_<culture>.xml`.

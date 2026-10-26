# Handoff: Empire pila replaced by Imperial javelins

Session of 2026-10-26, written 12:34. It follows `2026-10-26-1220-sidearm-rule.md` (same session).

## State

- **Not committed:** everything from the 12:20 handoff; `cb_mpclassdivisions_empire.xml`, `cb_mpcharacters_empire.xml` (comments only), `README.md`, `docs/plans/troop-types.md` and this handoff.
- **Installed:** the build is clean (0 warnings, 0 errors), and the XMLs on both sides match the project.
- **Running:** server pid 25088, map `battle_terrain_026`. Patch line: "94 classes applied, 0 failed, 3 skipped". Round 0 opened with no exceptions.
  - It includes the 12:20 sidearm rule. Server 13252 (12:22) ran that too and was then replaced.
- **Client:** restart the game to load the new XML.

## What was done

The user reported that pila could not be thrown. A read-only Opus investigation found the cause, and I checked its key points in the game data:
- **Wielded as a spear.** `mp_pilum` uses the `TwoHandedPolearm` craft template, so its thrown usage (`TwoHandedPolearm_Thrown`) comes last. A drawn pilum is a one-handed spear, and only a player pressing X switches it to throwing. Bots don't switch from managed code.
- **One per slot.** A thrown usage that is an alternative gets a stack of 1 (`Crafting.cs:160`, `isAlternative ? 1 : StackAmount`). The "two bundles of pila" setup was really two single pila.
- **Not from a horse.** `polearm_thrown` has `requires_no_mount` (`item_usage_sets.xml:11354`), so all three Empire mounted skirmisher setups could never throw.
- **Invisible to RBM.** RBM's skirmisher checks look for the Javelin weapon class or the Thrown item type, so they treated pila troops as plain infantry.

**Fix:** every Empire skirmisher and mounted skirmisher setup now carries `mp_empire_javelin_captain` (stack 5) in Item0 and `mp_empire_javelin` (stack 3) in Item3. These Javelin-template items throw by default, on foot and mounted. The descriptions were updated and the icon is now `PerkJavelins`.

**Other thrown setups checked:** Aserai jereeds (`mp_jereed_extra`, 6) and Battanian javelins (`mp_battania_javelin_extra` 7, `_captain` 10) already use the Javelin template, so they are unchanged. Khuzait, Sturgia and Vlandia have no throwing weapons.

**RBM ruled out:** `RangedReachGate` (off with RBM Combat), `SignatureWeaponGrip` (singleplayer ids only), `WeaponPreference` (skips throwables) and `AgentStats` (shoot frequency never 0).

## Verified vs not

- **Verified:** the data facts above, the build, the installed XML hashes, and a clean server start.
- **Not verified in play:**
  - Empire skirmishers throw at once, both a player and the AI;
  - the mounted ones throw from the saddle;
  - after Item0 runs out, Item3's bundle is used.
- The class counts per culture are still unchecked: the "Classes for" line is `Detail` only (`CommanderBattleLog.Detailed`).

## Open items

- Play an Empire round and watch the skirmishers.
- Commit both changes (sidearms and javelins) after the user's look.

## Traps

- A crafted throwable is throwable by default only with the `Javelin` (or throwing axe or knife) template. A `TwoHandedPolearm` craft with a thrown usage, such as the pilum, is a spear first, holds one per slot and may not be thrown mounted. Check `crafting_template` before using an item as a thrown weapon.

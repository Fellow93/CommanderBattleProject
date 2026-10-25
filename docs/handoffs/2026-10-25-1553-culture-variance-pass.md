# Handoff: culture variance pass installed

Session of 2026-10-25, written 15:53. Follows `2026-10-25-1516-troop-types-v2-all-cultures.md`. Plan: `docs/plans/troop-types.md`, section "Culture variance pass (approved 2026-10-25)".

## State

- **Nothing is committed** (same tree as the 15:16 handoff, plus this pass).
- **Installed:** full Release build at about 15:50, 0 errors and 0 warnings.
  - Client DLLs are net472 and server DLLs net6.0, all hash-identical to `obj\Release\<tfm>\`.
  - All 12 `cb_*.xml` files are identical in source, game and server, and the game and server have the same `SubModule.xml`.
- **Running:** dedicated server pid 26872, battle_terrain_biome_044, Vlandia (defender) vs Aserai (attacker), 91 patch classes, 0 failed. No client connected at handoff.

## What was done since 15:16

- **Weight labels:** they follow multiplier bands per role (`CommanderRoles.WeightBands`), so Khuzait's Darkhan and Battania's Highland Horseman read "medium". Installed at 15:19 and checked in the log.
- **Variance pass,** inspired by RBM's troop overhaul (`RBMXML/RBMCombat_unit_overhaul.xml`, summarised in the plan).
  - User decisions: throwing weapons only in skirmisher setups; no Sturgian mounted crossbows; two-handed setups may keep a shield on the back (said after the pass, recorded in the plan).
  - Six agents rewrote the six cultures' XML in parallel.
  - Sturgian archers were then switched to Sturgia's weaker bows (Nordic short bow 45, hunting longbow 55), so Sturgia's ranged is a real gap.
- **AI setup order comes from the data:** the k-th AI formation of a role fields setup k, wrapping. Each culture lists its setups signature first. `AiWeaponOrder` is gone (`MissionMultiplayerCommanderBattle.AssignDefaultClasses`).

## Verified

- Server log at mission open:
  - all six cultures' classes with their weights and setup counts. A light infantry class has one fewer setup where a setup is medium/heavy only; Battania infantry have 5; Sturgian archers 1;
  - splits: Vlandia 3 / 2 / 3, Aserai 3 / 3 / 1 / 1.
- Cross-file check: no duplicate ids; all item and character references resolve; `_replaceWhileMerging` is exactly on the 42 stock ids.

## Not verified

The whole in-game test, as listed in the 15:16 handoff: the screen, spawns, gear, setups, AI variety and horse archer layout. In addition:
- mounted pila and jereed throwing;
- big round shields on horseback;
- the Sturgian maul on horseback;
- pavises carried by crossbowmen.

## Open items (balance pass)

1. **Two-handed setups and back shields.** The user allows a shield on the back with a two-hander. Most cultures' two-handed setups currently clear the shield; Battania's mounted skirmisher keeps it. Decide per culture whether to give it back. Pikes stay shieldless (MP pike stance).
2. **Aserai heavy cavalry** default to the light adarga (105 hp); the steel round shield is only in their skirmisher setup.
3. **Empire Palatine Guard** is slightly heavier than Vlandia's dismounted knight; the plan names Sturgian infantry and Vlandian knights as the heaviest.
4. **Khuzait:** the Marksman's second bow is the same guard's bow as the Khan's Guard's default.
5. **Multipliers, speeds and displayed armour** are hand-set estimates.

## Traps

- Parallel agents shared the scratchpad and overwrote each other's helper files (`items.py`). Give parallel agents their own scratch subfolders.

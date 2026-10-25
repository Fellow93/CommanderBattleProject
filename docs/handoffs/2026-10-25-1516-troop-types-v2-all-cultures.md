# Handoff: troop types v2, all six cultures installed

Session of 2026-10-25, written 15:16. Follows `2026-10-25-1358-troop-types-phase-1.md`. Plan: `docs/plans/troop-types.md` (v2 decisions; phases 2 and 3 built). `README.md` describes the behaviour.

## State

- **Nothing is committed.** The working tree on `master` sits on `755390d`. It holds:
  - choosing troops;
  - the `Directory.Build.props` per-framework obj fix;
  - troop types v1 + v2, all six cultures;
  - the restyled troop screen;
  - plans and handoffs.
- **Installed:** full `dotnet build "CommanderBattle.sln" -c Release` at 15:14, 0 errors and 0 warnings.
  - Client DLLs are net472 and server DLLs net6.0. Every installed DLL is hash-identical to `obj\Release\<tfm>\` and newer than the last source edit.
  - All 12 `cb_*_<culture>.xml` files are in the game and the server, and both have the same `SubModule.xml`.
  - The old `cb_mpcharacters.xml` / `cb_mpclassdivisions.xml` are still in the installed ModuleData folders but are no longer referenced (harmless).
- **Running:** dedicated server pid 29164, battle_terrain_biome_094, Empire (defender) vs Khuzait (attacker), 91 patch classes, 0 failed. No client connected at handoff.

## What was done this session (since 13:58)

- **Troop screen restyle (v1):**
  - The labels overflowed fixed-height boxes, so every text cell is now CoverChildren.
  - It reuses the stock class loadout's art: `MultiplayerClassLoadoutTroopSubclassButtonWidget` banner, culture colour, compass icons, perk icons `General\Perks\<IconId>`.
  - New brushes are in `Module/GUI/Brushes/CommanderBattle.xml`.
  - `ui_mpmission` stays loaded through the stock HUD extension.
- **v2 design** (user decisions; see the plan):
  - per-culture formation split with horse archers;
  - screen steps class → weight → weapon setup, where picking a class takes a free formation;
  - weight is the class and the weapon setup is the only perk list;
  - weight alone sets the count;
  - gear tiers: light = levy, medium = men-at-arms, heavy = knights.
- **v2 core (agent):** HorseArcher role; `CommanderBattleSettings.CultureSplits` / `ConvertedCultures`; slot roles per side; layout; weight labels; weapon perk list; AI weights and setups (`AssignDefaultClasses`, applied in `SpawnAiFormation`); `RequestRoleCommand`; the three-step screen; the formation list follows.
- **Per-culture XML:** Vlandia moved to `cb_*_vlandia.xml`. Five agents wrote Empire, Khuzait, Aserai, Sturgia and Battania in parallel. `SubModule.xml` registers 12 files, and the csproj copies `cb_*.xml`.
- **Fix:** the AI setup choice now keeps only the indexes a class has. Before, `{0, 2, 1}` modulo 2 gave lance + shield twice for two-setup cavalry.

## Verified

- Server log at mission open:
  - classes per culture: Sturgia 7, Vlandia 8, Khuzait 8, Empire 9, Battania 7, Aserai 8, with weights, multipliers and setup counts as written;
  - splits: Empire 3 / 2 / 2 / 1 and Khuzait 2 / 2 / 2 / 2, with horse archer slots 3 (and 7).
- No XML load errors (errors file 4,931 bytes, the usual noise).
- Cross-file check: no duplicate ids, and `_replaceWhileMerging` sits exactly on the 42 stock ids.

## Not verified (in-game test next)

- The three-step screen: class cards with free counts, taking a class, weight and weapons, Back, and the join flow.
- Spawns:
  - each weight and setup's gear;
  - pikes and two-handers spawn without shields;
  - horse archers deploy on the right flank;
  - the AI fields varied setups.
- Troop counts in the round log.

## Open items

1. **Weight labels with two classes.** The rule labels two classes light / heavy, so Khuzait's Darkhan (medium gear) and Battania's Highland Horseman (medium cavalry) show as "heavy", against the plan's table. Suggested fix: label by the multiplier bands all six XMLs use instead of by count:
   - infantry ≥0.75 light, ≥0.58 medium, else heavy;
   - cavalry ≥0.42 light, ≥0.34 medium, else heavy;
   - ranged 0.68 light / 0.46 heavy;
   - horse archers 0.43 light / 0.32 heavy.
2. **The in-game test** above.
3. **Phase 4 balance:** multipliers, speeds, setup contents; some cultures add Cape-slot shoulder pieces.
4. **Notes from the XML writers:**
   - Empire lances aren't couchable, so Empire cavalry use `mp_western_lance`.
   - Stock ids were repurposed: `mp_skirmisher_battania` is the medium cavalry, `mp_skirmisher_sturgia` the heavy archers, `mp_skirmisher_aserai` the medium infantry.
   - Sturgian heavy archers carry a back shield.
   - Khuzait "Bow and lance" keeps the sabre and adds the lance in Item3.
5. **Order-bar naming:** some formations sit on an engine slot of another name (e.g. a third infantry formation on the "Horse Archers" slot 3 for Vlandia).

## Traps

- `H` is a built-in PowerShell alias (`Get-History`); don't name a helper function `H`.
- A `cd` inside a Bash call moves the session's working directory; use absolute paths afterwards.

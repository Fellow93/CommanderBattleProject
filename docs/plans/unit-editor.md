# Unit editor for RBM mode

**Status:** in progress (built; `serve.js` serves the page and saves, checked in the browser pane; a new unit in game not yet checked by the user) · **Size:** M–L

## Goal

Make and change RBM mode's units without hunting for item ids and without guessing stats, and add new units under the four formation classes (infantry, ranged, cavalry, horse archers). Today a unit is hand-edited across three XML files per culture, item ids are looked up in RBM's item files, and the stats only show in game. The two costs to remove are finding the items and seeing their stats while picking.

A power number per unit comes later, on top of this tool. It is not part of this plan.

**Not goals:**
- showing the unit dressed;
- editing Light mode's `mp_` gear (a new unit keeps the `mp_` gear of the unit it was copied from);
- a formation class beyond the four: those are fixed in code (`CommanderRole`, the per-culture split, the layout, the selection screen) and in the stock lobby;
- the per-culture split of the 8 formations (`CommanderBattleSettings.CultureSplits`);
- any change to how the game reads these files.

## What exists today

A unit is three entries, in three files of its culture under `Module\ModuleData`:

| File | Entry | Holds |
|---|---|---|
| `cb_mpclassdivisions_<culture>.xml` | `MPClassDivision` | id, hero and troop character ids, `multiplier` (formation size and the weight label), `cost`, `icon`, AI and speed values, and the weapon setups as `Perk`s of `perk_list="1"` (name, description, icon, `mp_` slot swaps) |
| `cb_mpcharacters_<culture>.xml` | two `NPCCharacter`s (hero, troop) | name, `default_group` (the formation class), level, skills, face, `mp_` gear |
| `cb_rbm_loadouts_<culture>.xml` | `Class` | RBM mode's gear: `Hero`, `Troop` rosters, `Setup index="N"` for the N-th perk |

- **The formation class is the character's `default_group`** (`CommanderRoles.GetRole`). The general is known by his class id (`cb_general_<culture>`).
- **The weight label comes from the multiplier**, by bands per formation class (`CommanderRoles.WeightBands`).
- **16 units per culture at most** (`MissionPeer.MaxNumberOfTroopTypesPerCulture`). The cultures have 7 to 9.
- **The 7 stock class ids of each culture are overridden in place** (`_replaceWhileMerging="true"`); further ones have `cb_` ids and are appended. `_replaceWhileMerging` on a new id replaces the file's first class.
- **Setup order is the AI order**: the k-th AI formation of a formation class fields setup k.
- The files carry long comments (doctrine, slot rules, where each unit was seeded from), tabs and CRLF. They must survive an edit.
- Real stats exist only inside the game: crafted weapons get damage, reach and speed from their pieces, and RBM's XML merges change items further. `RBM.Multiplayer\RbmItemSetCheck.cs` already walks the loaded items after `OnGameInitializationFinished`, on server and client.
- Items are loaded before the lobby appears, and the client's item fingerprints equal the server's. A client started in multiplayer is enough to read the real item values; no server is needed.
- **RBM's repo already has editor pages of this kind** (`..\RealisticBattleProject\tools\`, see its `README.md`): `TroopLoadoutEditor`, `ItemEditor`, `CraftingEditor`, `TroopPerkEditor`. They share a shape this tool follows:
  - a folder with `index.html`, a `*-core.js` without DOM (the model and the export, runnable under node) and a generated, gitignored `*-data.js` ("contains TaleWorlds text");
  - an item picker per slot with search, filters and sortable stat columns (`TroopLoadoutEditor\index.html`);
  - an export that splices text, so everything outside the edit stays byte-identical; its helpers are exported from `TroopLoadoutEditor\troop-loadout-core.js` and the item editor already reuses them.
  - Their item data comes from the XML, so crafted weapons show no stats there, and the campaign's item set is not multiplayer's. That is why this tool takes its items from the game.

## Approaches considered

1. **Catalogue only.** The game writes the items as a CSV; filter it in Excel, keep editing XML by hand. Smallest, but ids are still copied across and nothing checks that an item fits its slot.
2. **Catalogue plus an editor page (picked).** One HTML file in `tools\`, opened in Edge or Chrome. It writes the repo's XML directly, needs no build, and changes to it show on a page reload.
3. **A screen inside the game.** Live stats with no export, but about 1,100–1,700 lines of C# and a prefab, a search box and a column list the repo has no precedent for, a save that has to guess the repo's folder, and a game restart per code change. Its one advantage, seeing the unit, is not wanted.

Picked 2 by the user: the pain is finding items and seeing stats, and the page solves both at about a fifth of the work.

## Design

### 1. Item catalogue (game side)

New `RBM.Multiplayer\RbmItemCatalogue.cs`, called next to `RbmItemSetCheck.Log()`.

- Writes `tools\UnitEditor\unit-editor-data.js` in the repo: `window.UNIT_EDITOR_DATA = { written: "...", fingerprint: "...", items: [...] };`. A `.js` file, not JSON, so the page can load it with a script tag when opened from disk.
- **Only on a development machine:** it writes when the repo's `tools\UnitEditor\` folder exists (looked for under the game's `Modules`, from the client and from the dedicated server next to it), and does nothing otherwise. Players never get the file and no setting is needed. One `Write` log line: "Item catalogue: N items written".
- Every item except `mp_` ones (the loadouts use singleplayer items only), **in the item shape RBM's troop loadout editor uses**, so its slot check and its picker columns work on it unchanged:
  - `id`, `name`, `type` (`ItemType`), `culture`, `tier`, `weight`, `flags`. As built: the tier comes from the item id (`_tN`, marked `tierFromId`), and there is no `value`, because multiplayer has no item value model (`ItemObject.Tier` throws there and `Value` is a placeholder).
  - `armor`: `head`, `body`, `arm`, `leg`, `mat`
  - `horse`: `spd`, `man`, `chg`, `hp`
  - `weapons`, one entry per usage: `cls`, `sw`/`swt` and `th`/`tht` (damage and its type), `spd`, `tspd`, `len`, `ms`, `stack`, `acc`, `hand`, `arm` and `hp` (shields), `bal`
- Numbers in the invariant culture. The stats fingerprint from `RbmItemSetCheck` goes into the file, so a stale catalogue can be told from the log.
- The generated file is gitignored, as RBM's are: it contains TaleWorlds text. Make or refresh it by starting the game in multiplayer (or the dedicated server) once.

### 2. Editor page

`tools\UnitEditor\`, shaped like RBM's editors: `index.html` (the page), `unit-editor-core.js` (reading and splicing the three files, no DOM, tested under node) and the generated data. Plain JavaScript, no libraries, no build. It loads RBM's `troop-loadout-core.js` from the sibling repo for the slot check and the XML text helpers, and its picker is adapted from that editor's. Edge or Chrome (it uses the browser's folder picker to write the files back in place).

```
[Open ModuleData folder]  culture [Empire ▾]   catalogue: 1310 items, 16 Nov   9 of 16 units   [Save]
┌ Units ──────────────┐┌ Levy Spearman · mp_skirmisher_empire ────────┐┌ Pick: Item0 ───────────────────────────┐
│ INFANTRY            ││ name [Levy Spearman]  multiplier [0.85] Light ││ search [spear   ] culture [empire ▾]   │
│  Levy Spearman   L  ││ speed [0.83] combat [0.9]  skills …           ││ name       tier swing thrust speed len │
│  Legionary       M  ││ ───────────────────────────────────────────── ││ ▸ imperial_spear_t2 (current) …        │
│  Palatine Guard  H  ││ Hero | Roster 1 | Roster 2 | Setup 1 | Setup 2││   imperial_spear_t3           …        │
│  [+ unit]           ││ Item0  imperial_spear_t2   th 31  205 cm      ││   …                                    │
│ RANGED              ││ Item1  worn_kite_shield    hp 380             │└────────────────────────────────────────┘
│  Levy Archer     L  ││ Head   spiked_kettle…      head 38            │
│  …  [+ unit]        ││ …            totals: head 38 body 61 …        │
│ CAVALRY …           │└───────────────────────────────────────────────┘
│ HORSE ARCHERS …     │
│ GENERAL             │
└─────────────────────┘
```

**Opening.** Pick the `Module\ModuleData` folder once, then a culture. The page reads that culture's three files and joins them by class id and character id.

**Unit list.** Units grouped under the four formation classes and the general, light to heavy, each with its weight label. A counter shows how many of the 16 are used.

**Adding a unit.** "+ unit" under a formation class copies an existing unit of that class, picked from a list, under a new id and name:
- a new `MPClassDivision` appended to the class divisions file, with a `cb_` id and never `_replaceWhileMerging`;
- its hero and troop characters, copied with new ids, keeping `default_group`, face and `mp_` gear;
- its loadout `Class`, copied.

Refused at 16 units, and for an id already in use.

**Deleting a unit.** Only units with `cb_` ids. The stock ids stay: every culture needs its 7. As built, deleting removes the unit's three entries and keeps the comments above them: a comment there may be a hand-written section note, not just the unit's label.

**Unit properties** (the class division and the characters): `multiplier` with the weight label it gives, `cost`, `icon`, `melee_ai`, `ranged_ai`, `armor`, the speed values, name, level and skills for hero and troop. As built, the hero and the troop are named separately: the general's are a commander and his guards (Strategos and Elite Cataphract). Moving a unit to another formation class sets `default_group` on both characters; as built, it is refused for the last unit of a formation class, whose AI formations would otherwise get class index 0.

**RBM gear.** Tabs: Hero, each roster, each setup. Each tab is a slot table with the item's name and its key stats, and armour totals underneath.
- **Click a slot** to open the picker: only catalogue items that fit that slot, a search box over id and name, a culture filter, columns that match the slot (armour values, weapon stats or horse stats), sortable by clicking a header. The slot's current item stays pinned on top for comparison. One entry empties the slot.
- **In all rosters:** a tick box in the picker sets the item in every roster of the unit, since weapons are the same in every roster by the files' own rule.
- **Rosters:** copy or delete.
- **Setups:** add, delete or reorder (setup 0 is the unit's own loadout and stays first). A setup is a `Perk` in the class divisions file (name, description, icon) and a `Setup` of the same index in the loadouts; the page keeps the two in step. A new setup's `mp_` slot swaps for Light mode are copied from the setup it was made from. As built, a unit has at most 16 setups, its own loadout included: the perk index goes over the network in 4 bits.

**Checks shown in red:**
- an item id that is not in the catalogue;
- an id used twice, or a class division whose hero, troop or loadout is missing;
- a slot holding different kinds of item across rosters;
- a loadout `Setup` index with no perk, or the other way round (as built, both are warnings);
- a loadout `Setup index="0"` (the game refuses it: setup 0 is the unit's own loadout);
- more than 16 units;
- hero and troop characters with different `default_group`s.

**Save** writes back only the files that changed. Every edit is a text splice, as in RBM's editors, so only changed attributes and added or removed nodes differ; comments, tabs and CRLF are kept.
- Before writing, the page reads each file again and refuses to save one that changed on disk since it was opened.
- Edits live in the page until saved; closing it with unsaved edits asks first.

### 3. Getting changes into the game

Unchanged: build as today. `DeployModule` copies the files to the client and the dedicated server. A new unit shows in the class log (`CommanderBattleLog.Detailed`) and on the selection screen; the AI fields it by the existing rules (`AssignDefaultClasses`).

## Calls made here (correct them if wrong)

- **A new unit is always a copy of an existing one.** Faces, idle animations and Light mode's `mp_` gear come with the copy and are not editable in the page.
- **Light mode gets the new unit too**, in the copied `mp_` gear and under the new name and multiplier. The classes are shared by both combat modes, so a unit cannot exist in RBM mode only.
- **The weight bands are repeated in the page** to show the label. If `CommanderRoles.WeightBands` changes, the page's copy has to follow.
- **The page depends on RBM's repo** being next to this one, for `troop-loadout-core.js`. The build already depends on it (`RbmSourceDir`). Nothing in RBM's repo is edited.
- **The page writes the files in place** through the browser's folder picker, where RBM's editors offer a download. Three files per culture make the download round too slow.
- **The catalogue is written on every start on a development machine**, by client or server, whichever starts. It costs a few milliseconds.
- **The numbers are item data, not battle results.** RBM Combat's armour scaling and skill-based weapon speeds are applied per agent on the server and are not shown. The power calculation will have to deal with that.
- **Built in three steps,** each usable alone: the catalogue; the page editing RBM gear of existing units; adding units and editing their properties.

## Files it touches

- New `RBM.Multiplayer\RbmItemCatalogue.cs`; one call in `RbmItemSetCheck.Log()` or `RbmMpSubModule`.
- New `tools\UnitEditor\index.html`, `unit-editor-core.js`, `unit-editor-test.js` and its fixture `test-data\` (a copy of the empire files); generated `unit-editor-data.js`, added to `.gitignore`.
- `README.md`: a short "Editing RBM units" section. `CLAUDE.md`: a line on the tool.

The steps are in [unit-editor-implementation.md](unit-editor-implementation.md).

## Things for the builder to check

- That the item set is the same in Light and RBM combat mode on a client (the merge runs at module load in both), so the catalogue does not depend on the host's mode.
- Whether anything else keys on a class id or a class count: the README's class tables, `AiWeaponOrder`, the release script.

## Done when

- Starting the game in multiplayer writes the catalogue, and its fingerprint equals the "Item fingerprints" stats value in the log.
- `node tools\UnitEditor\unit-editor-test.js` passes.
- Saving without a change leaves `git diff` empty.
- Changing one slot through the picker gives a one-line `git diff`; after a build the unit carries the item in game.
- A unit added in the page appears in the class log under its formation class with the expected weight label, can be picked on the selection screen, and spawns in its RBM gear; `RbmLoadouts` logs no problems.
- A roster and a setup added in the page spawn in game.

# Unit Editor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A local page that edits RBM mode's units across a culture's three XML files and adds new ones, picking gear from an item catalogue the game writes with real stats.

**Architecture:** The game writes the loaded items to a generated `.js` file. A DOM-free core holds the three files as text; every edit is a function from texts to texts that splices only what changes, and the page re-reads the model from the texts after each edit. The page is the UI over that core, with the picker adapted from RBM's troop loadout editor.

**Tech Stack:** C# (`RBM.Multiplayer`, net472 + net6.0), plain JavaScript (no libraries), node for the core's tests, the File System Access API (Edge, Chrome).

**Spec:** [unit-editor.md](unit-editor.md)

## Global Constraints

- **Never edit anything under `..\RealisticBattleProject`.** Read it, load `tools\TroopLoadoutEditor\troop-loadout-core.js` from it, and copy from its pages into this repo.
- **No commits unless the user asks.** No co-author trailer. Where a step says "Commit", do it only on the user's word; otherwise leave the files changed.
- Edit files only with the Edit/Write tools. Never copy files into a module output folder by hand.
- JavaScript in the style of RBM's editors: an IIFE, `'use strict'`, `var`/`function`, no build, comments that say why.
- Files written back stay byte-identical outside the edit: BOM, CRLF, tabs, comments, attribute layout.
- At most 16 units per culture. New units get `cb_` ids and never `_replaceWhileMerging`. Stock ids and the general (`cb_general_<culture>`) cannot be deleted.
- C#: no `using MathF = ...` alias inside `RBM.Multiplayer`. Numbers in files use `CultureInfo.InvariantCulture`. `RbmMpLog.Write` only for the one catalogue line.
- Generated `tools\UnitEditor\unit-editor-data.js` is gitignored.

## Review Focus

1. **The catalogue is missing or older than the game's items.** The page says how to make it and still opens the files; an item id not in the catalogue is flagged and kept, never dropped. (Task 5)
2. **A file changed on disk after it was opened** (a hand edit, a git checkout). Save re-reads it and refuses to overwrite. (Task 5)
3. **Names and descriptions with `&`, `"`, `<` or an apostrophe.** They are escaped on write and read back unchanged. (Tasks 3, 4)
4. **Entries that do not join:** a class division without a loadout, a loadout without a class division, a missing character, the general. Listed with a problem; nothing throws. (Task 2)
5. **Setup numbering:** deleting or moving a setup renumbers the loadout's `Setup index` values with the perks, including a class whose later setup has no loadout entry. (Task 3)

## File Structure

| File | Responsibility |
|---|---|
| `RBM.Multiplayer\RbmItemCatalogue.cs` (new) | Writes the loaded items to `unit-editor-data.js` on a development machine |
| `RBM.Multiplayer\RbmItemSetCheck.cs` | Calls it with the stats fingerprint |
| `tools\UnitEditor\unit-editor-core.js` (new) | Reads the three texts into a model; edit functions texts → texts; problems; weight label. No DOM |
| `tools\UnitEditor\unit-editor-test.js` (new) | Node tests of the core against the repo's six cultures |
| `tools\UnitEditor\index.html` (new) | The page: open folder, unit list, properties, gear tabs, picker, save |
| `tools\UnitEditor\README.md` (new) | How to make the data, use the page, run the tests |
| `.gitignore`, `README.md`, `CLAUDE.md`, `docs\plans\README.md`, handoff | Housekeeping |

---

### Task 1: Item catalogue from the game

**Files:**
- Create: `RBM.Multiplayer\RbmItemCatalogue.cs`
- Modify: `RBM.Multiplayer\RbmItemSetCheck.cs` (`Log`, after the "Item fingerprints" line)
- Modify: `.gitignore` (add `/tools/UnitEditor/unit-editor-data.js` with a comment in the style of RBM's entries)
- Create: `tools\UnitEditor\README.md` (so the folder exists in git; filled in Task 6)

**Interfaces:**
- Produces: `tools\UnitEditor\unit-editor-data.js`:
  `window.UNIT_EDITOR_DATA = { written: "<ISO local time>", fingerprint: "<8 hex, the stats fingerprint>", items: [ ... ] };`
- Item shape (omit a field that is 0, empty or absent, except `id`, `name`, `type`):
  - `id`, `name` (`item.Name.ToString()`), `type` (`item.ItemType.ToString()`), `culture` (the culture's `StringId`, `""` without one), `tier` (`(int)item.Tier`), `weight`, `value`, `flags` (array of the set `ItemFlags` names)
  - `armor: { head, body, arm, leg, mat }` (`mat` = `MaterialType.ToString()`)
  - `horse: { spd, man, chg, hp }` (`hp`: check `HorseComponent` in the decompiled `TaleWorlds.Core` for the hit point property the game uses)
  - `weapons: [ { cls, sw, swt, th, tht, spd, tspd, len, ms, stack, acc, hand, arm, hp, bal, flags } ]`, one per `item.Weapons` entry: `cls` = `WeaponClass`, `sw`/`th` = `SwingDamage`/`ThrustDamage`, `swt`/`tht` = the damage type names, `spd` = `SwingSpeed`, `tspd` = `ThrustSpeed`, `len` = `WeaponLength`, `ms` = `MissileSpeed`, `acc` = `Accuracy`, `hand` = `Handling`, `arm` = `BodyArmor`, `bal` = `WeaponBalance`, `flags` = the set `WeaponFlags` names. `MaxDataValue` goes to `hp` when `IsShield`, else to `stack`.

- [ ] **Step 1: Implement `internal static class RbmItemCatalogue` with `public static void Write(MBReadOnlyList<ItemObject> items, string statsFingerprint)`**
  - Target folder: the first that exists of `Path.Combine(BasePath.Name, "Modules", "CommanderBattleProject", "tools", "UnitEditor")` and `Path.Combine(BasePath.Name, "..", "Mount & Blade II Bannerlord", "Modules", "CommanderBattleProject", "tools", "UnitEditor")` (the dedicated server sits next to the game, as `Directory.Build.props` assumes). None exists: return without a log line.
  - Skip items whose id starts with `mp_`.
  - Write the JSON by hand with a `StringBuilder` (the project has no JSON dependency): strings escaped for `\`, `"` and control characters, numbers with `InvariantCulture`.
  - Wrap in `try`/`catch`; on failure `RbmMpLog.Write($"Item catalogue failed: {e.Message}")`. On success `RbmMpLog.Write($"Item catalogue: {n} items written")`.
- [ ] **Step 2: Call it from `RbmItemSetCheck.Log`**: compute the stats fingerprint once into a local, use it in the existing log line and pass it as `x8` to `RbmItemCatalogue.Write(items, ...)`.
- [ ] **Step 3: Build and run the server**

Run: `powershell -ExecutionPolicy Bypass -File tools\Start-Server.ps1 -Build -Restart`
Expected: the build succeeds for both targets; the patch line shows "0 failed"; `RBM.Multiplayer_<pid>.log` has "Item catalogue: N items written" with N around 1300.

- [ ] **Step 4: Check the file**

Run: `node -e "global.window={};require('./tools/UnitEditor/unit-editor-data.js');const d=window.UNIT_EDITOR_DATA;const by=Object.fromEntries(d.items.map(i=>[i.id,i]));console.log(d.items.length,d.fingerprint,JSON.stringify(by.imperial_spear_t2),JSON.stringify(by.imperial_scale_armor),JSON.stringify(by.empire_horse))"`
Expected: the fingerprint equals the log's "stats" value; `imperial_spear_t2` has a `weapons` entry with `th` and `len` above 0 (a crafted weapon with real stats); `imperial_scale_armor` has `armor.body` 135; `empire_horse` has `horse.spd`; no id starts with `mp_`. `git status` does not list the data file.

- [ ] **Step 5: Commit** (`Item catalogue for the unit editor`)

---

### Task 2: Core — read the three files

**Files:**
- Create: `tools\UnitEditor\unit-editor-core.js`
- Create: `tools\UnitEditor\unit-editor-test.js`

**Interfaces:**
- Consumes: `TroopLoadoutCore` (`parseChildren`, `rootRange`, `getAttr`, `tokens`, `fits`, `SLOTS`). Under node: `require('../../../RealisticBattleProject/tools/TroopLoadoutEditor/troop-loadout-core.js')`; in the page: the global.
- Produces, as `UnitEditorCore` (global, or `module.exports` under node):
  - `texts`: `{ divisions: string, characters: string, loadouts: string }`, each the file's full text without its BOM.
  - `ROLES = ['Infantry', 'Ranged', 'Cavalry', 'HorseArcher', 'General']`, `MAX_UNITS = 16`.
  - `parse(texts) -> { units: Unit[], problems: Problem[] }`
  - `Unit = { id, role, name, stock: boolean, general: boolean, weight: string, division: { <attribute>: string }, perks: [{ name, description, icon }], hero: Character|null, troop: Character|null, loadout: Loadout|null }`
    - `stock` = the division has `_replaceWhileMerging`; `general` = the id starts with `cb_general_`; `role` = `'General'` for the general, else the hero's `default_group`.
    - `perks` = the division's `Perk`s of `perk_list="1"`, in order; index = setup index.
  - `Character = { id, name, group, level, skills: { <skill id>: number } }`
  - `Loadout = { hero: Slots|null, rosters: Slots[], setups: { <index>: { name, slots: [{ slot, id, target }] } } }`, `Slots = { <slot>: <item id without "Item."> }`. A setup slot without an id has `id: ''`.
  - `weightLabel(role, multiplier, unitsInRole) -> 'Light'|'Medium'|'Heavy'|''`: the bands of `CommanderRoles.WeightBands` (Infantry 0.75/0.58, Ranged 0.60/0.52, Cavalry 0.42/0.34, HorseArcher 0.40/0.36); `''` for the general or a role with one unit. A comment names `CommanderRole.cs` as the source to keep in step.
  - `Problem = { unit: string|null, cls: 'bad'|'warn', text: string }`. `parse` reports: an id used twice in a file; a division whose hero, troop or loadout is missing; a loadout `Class` without a division; hero and troop with different `default_group`s; a loadout `Setup` index with no perk; more than `MAX_UNITS` divisions.
  - `itemProblems(parsed, itemsById) -> Problem[]`: an item id not in the catalogue; an item that does not fit its slot (`TroopLoadoutCore.fits`); a slot holding different `type`s across a unit's rosters.

- [ ] **Step 1: Write the failing tests** in `unit-editor-test.js`: a plain node script using `assert`, with a `test(name, fn)` helper that prints each name and exits 1 on the first failure. `load(culture)` reads the three files of `Module\ModuleData` (strip the BOM).

```js
test('parse joins the three files for every culture', function () {
  var counts = { vlandia: 9, empire: 10, khuzait: 9, aserai: 9, sturgia: 8, battania: 8 }; // the classes plus the general
  Object.keys(counts).forEach(function (c) {
    var p = Core.parse(load(c));
    assert.strictEqual(p.units.length, counts[c], c);
    assert.deepStrictEqual(p.problems, [], c);
    p.units.forEach(function (u) { assert.ok(u.hero && u.troop && u.loadout, c + ' ' + u.id); });
  });
});
test('empire light infantry reads as the files have it', function () {
  var u = Core.parse(load('empire')).units.filter(function (x) { return x.id === 'mp_skirmisher_empire'; })[0];
  assert.strictEqual(u.role, 'Infantry'); assert.strictEqual(u.name, 'Levy Spearman');
  assert.strictEqual(u.stock, true); assert.strictEqual(u.weight, 'Light');
  assert.strictEqual(u.division.multiplier, '0.85');
  assert.deepStrictEqual(u.perks.map(function (p) { return p.name; }), ['Spear and shield', 'Sword and shield', 'Skirmisher']);
  assert.strictEqual(u.hero.skills.Polearm, 80);
  assert.strictEqual(u.loadout.hero.Item0, 'imperial_spear_t2');
  assert.strictEqual(u.loadout.setups[1].name, 'Sword and shield');
});
test('the general has role General and no weight', function () {
  var g = Core.parse(load('empire')).units.filter(function (x) { return x.general; })[0];
  assert.strictEqual(g.id, 'cb_general_empire'); assert.strictEqual(g.role, 'General'); assert.strictEqual(g.weight, '');
});
test('entries that do not join are problems, not exceptions', function () {
  var t = load('empire');
  t.loadouts = t.loadouts.replace('<Class id="mp_skirmisher_empire">', '<Class id="nobody">');
  var texts = Core.parse(t).problems.map(function (p) { return p.text; }).join('\n');
  assert.ok(/mp_skirmisher_empire.*no loadout/.test(texts)); assert.ok(/nobody.*no class division/.test(texts));
});
test('weightLabel follows the bands', function () {
  assert.strictEqual(Core.weightLabel('Infantry', 0.65, 3), 'Medium');
  assert.strictEqual(Core.weightLabel('Cavalry', 0.30, 3), 'Heavy');
  assert.strictEqual(Core.weightLabel('HorseArcher', 0.43, 1), '');
});
```

If a count is wrong because the files differ from `docs\plans\troop-types.md`, fix the number in the test from the files, not the parser.

- [ ] **Step 2: Run** `node tools\UnitEditor\unit-editor-test.js`. Expected: FAIL, the core does not exist.
- [ ] **Step 3: Implement `parse`, `weightLabel`, `itemProblems`** with `TroopLoadoutCore.parseChildren` over each file's root range. The core keeps, for every element it reads, nothing but values: positions are found again from the text by each edit.
- [ ] **Step 4: Run the tests.** Expected: PASS.
- [ ] **Step 5: Commit** (`Unit editor core: read the unit files`)

---

### Task 3: Core — gear edits

**Files:**
- Modify: `tools\UnitEditor\unit-editor-core.js`, `tools\UnitEditor\unit-editor-test.js`

**Interfaces:**
- Consumes: Task 2's `parse`; `TroopLoadoutCore` (`setAttr`, `removeAttr`, `indentAt`, `reindent`, `escAttr`, `tokens`).
- Produces (each returns new `texts`, leaves its input alone, and throws an `Error` with a readable message when the target does not exist):
  - `where`: `{ kind: 'hero' } | { kind: 'roster', index } | { kind: 'setup', index }`
  - `setSlot(texts, unitId, where, slot, itemId)`: `itemId` a string sets `id="Item.<itemId>"`, adding the `<equipment>` entry in slot order (`TroopLoadoutCore.SLOTS`) shaped like a sibling when the slot has none; `null` removes the entry; `''` (setups only) writes the entry without an `id`, which empties the slot in game.
  - `setSlotInAllRosters(texts, unitId, slot, itemId)`
  - `copyRoster(texts, unitId, index)` (the copy goes right after it), `deleteRoster(texts, unitId, index)` (refuses the last roster)
  - `addSetup(texts, unitId, fromIndex, name)`: appends a `Perk` to the division, a copy of perk `fromIndex` with the new name (copying index 0 gives a perk without effects), and a `Setup index="<new>" name="<name>"` to the loadout with the slots of setup `fromIndex` (none for 0).
  - `deleteSetup(texts, unitId, index)`: index ≥ 1; removes the perk and the `Setup`, and lowers the `index` of later `Setup`s by one.
  - `moveSetup(texts, unitId, index, delta)`: index and target ≥ 1; swaps the two perks' texts and the two `Setup` indexes.
  - `setPerk(texts, unitId, index, { name, description, icon })`: sets the given attributes on the perk; a new name also goes to the loadout's `Setup` of that index.

- [ ] **Step 1: Write the failing tests.** A helper `diffLines(a, b)` returns the lines that differ between two texts.

```js
test('an edit that sets the value a slot already has changes nothing', function () {
  var t = load('empire');
  assert.deepStrictEqual(Core.setSlot(t, 'mp_skirmisher_empire', { kind: 'hero' }, 'Item0', 'imperial_spear_t2'), t);
});
test('setSlot changes one line and keeps CRLF', function () {
  var t = load('empire'), r = Core.setSlot(t, 'mp_skirmisher_empire', { kind: 'roster', index: 0 }, 'Item0', 'imperial_spear_t3');
  assert.strictEqual(diffLines(t.loadouts, r.loadouts).length, 1);
  assert.strictEqual(r.divisions, t.divisions); assert.strictEqual(r.characters, t.characters);
  assert.ok(r.loadouts.indexOf('\r\n') > 0 && !/[^\r]\n/.test(r.loadouts));
  assert.strictEqual(Core.parse(r).units[0].loadout.rosters[0].Item0, 'imperial_spear_t3');
});
test('setSlot adds a missing slot in slot order and removes one', function () { /* add Item3 to a hero without it: one line more, placed after Item2; null removes it: back to the original text */ });
test('a setup slot can be emptied', function () { /* '' on setup 1 writes <equipment slot="Item2" /> */ });
test('setSlotInAllRosters sets every roster', function () { /* every roster of the unit has the item; the hero does not */ });
test('copyRoster and deleteRoster are inverse', function () { /* copy 0 then delete 1 gives the original text; deleting the only roster throws */ });
test('addSetup writes the perk and the Setup, deleteSetup removes both', function () {
  var t = load('empire'), r = Core.addSetup(t, 'mp_skirmisher_empire', 1, 'Mace & shield');
  var u = Core.parse(r).units[0];
  assert.strictEqual(u.perks.length, 4); assert.strictEqual(u.perks[3].name, 'Mace & shield');
  assert.ok(r.divisions.indexOf('name="Mace &amp; shield"') > 0);
  assert.deepStrictEqual(u.loadout.setups[3].slots, u.loadout.setups[1].slots);
  assert.deepStrictEqual(Core.deleteSetup(r, 'mp_skirmisher_empire', 3), t);
});
test('deleteSetup renumbers the later setups', function () { /* delete 1 of 3: old setup 2 is now index 1 with its name and slots, parse has no problems */ });
test('moveSetup swaps perks and Setup indexes; setup 0 does not move', function () { /* move 1 by +1 twice gives the original model; move(…, 1, -1) throws */ });
test('setPerk renames the perk and its Setup', function () { /* both files change, one line each */ });
```

Write the bodies marked with a comment from that comment; each asserts on `parse` of the result and on `diffLines`.

- [ ] **Step 2: Run the tests.** Expected: FAIL, the functions are missing.
- [ ] **Step 3: Implement the functions.** Each finds its element's span in the current text (`parseChildren` from the root), builds the element's new text with the `TroopLoadoutCore` helpers, and splices it in. New entries copy the whitespace of a sibling; where there is none, the parent's indent plus one tab and the file's line ending.
- [ ] **Step 4: Run the tests.** Expected: PASS.
- [ ] **Step 5: Commit** (`Unit editor core: gear edits`)

---

### Task 4: Core — unit properties, adding and deleting units

**Files:**
- Modify: `tools\UnitEditor\unit-editor-core.js`, `tools\UnitEditor\unit-editor-test.js`

**Interfaces:**
- Produces (texts → texts, throwing as in Task 3):
  - `setDivisionAttr(texts, unitId, name, value)`: for `multiplier`, `cost`, `icon`, `melee_ai`, `ranged_ai`, `armor`, `movement_speed`, `combat_movement_speed`, `acceleration`.
  - `setName(texts, unitId, name)`: the `name` of the hero and the troop character.
  - `setRole(texts, unitId, group)`: `default_group` of both characters; `group` one of `ROLES` without `'General'`; refuses the general.
  - `setLevel(texts, characterId, level)`, `setSkill(texts, characterId, skillId, value)` (an existing `<skill>` entry gets the value; a missing one is added after the last).
  - `addUnit(texts, fromUnitId, newId, newName) -> texts`: `newId` must match `/^cb_[a-z0-9_]+$/`, be unused, and not start with `cb_general_`; refuses at `MAX_UNITS` and refuses to copy the general. Appends before each root's closing tag, each after a blank-line-free line break in the file's indentation:
    - the division copied, with `id="<newId>"`, `hero="<newId>_hero"`, `troop="<newId>_troop"` and `_replaceWhileMerging` removed;
    - both characters copied, with the new ids and `name="<newName>"`;
    - the loadout `Class` copied, with `id="<newId>"`.
    A comment directly above a copied element is not copied.
  - `deleteUnit(texts, unitId)`: refuses a stock unit and the general. Removes the division, both characters and the loadout `Class`, each with the whitespace before it and a comment directly above it.

- [ ] **Step 1: Write the failing tests**

```js
test('setDivisionAttr changes one line of the divisions file', function () {
  var t = load('empire'), r = Core.setDivisionAttr(t, 'mp_skirmisher_empire', 'multiplier', '0.80');
  assert.strictEqual(diffLines(t.divisions, r.divisions).length, 1);
  assert.strictEqual(r.characters, t.characters); assert.strictEqual(r.loadouts, t.loadouts);
});
test('setName escapes and reads back', function () {
  var r = Core.setName(load('empire'), 'mp_skirmisher_empire', 'Levy "Spear" & <Shield>');
  var u = Core.parse(r).units[0];
  assert.strictEqual(u.name, 'Levy "Spear" & <Shield>'); assert.strictEqual(u.troop.name, u.name);
});
test('setRole moves both characters and the weight label follows', function () { /* Infantry -> Ranged: role Ranged on the unit, no group-mismatch problem */ });
test('setSkill sets an entry and adds a missing one', function () { /* Polearm 80 -> 90 one line; a skill id the character lacks is added, parse reads it */ });
test('addUnit writes all three entries and deleteUnit restores the files', function () {
  var t = load('empire'), r = Core.addUnit(t, 'mp_skirmisher_empire', 'cb_pikeman_empire', 'Pikeman');
  var p = Core.parse(r), u = p.units.filter(function (x) { return x.id === 'cb_pikeman_empire'; })[0];
  assert.deepStrictEqual(p.problems, []);
  assert.strictEqual(u.stock, false); assert.strictEqual(u.name, 'Pikeman'); assert.strictEqual(u.role, 'Infantry');
  assert.strictEqual(u.hero.id, 'cb_pikeman_empire_hero'); assert.strictEqual(u.troop.id, 'cb_pikeman_empire_troop');
  assert.ok(!/_replaceWhileMerging[^>]*id="cb_pikeman_empire"|id="cb_pikeman_empire"[^>]*_replaceWhileMerging/.test(r.divisions));
  var src = Core.parse(t).units[0];
  assert.deepStrictEqual(u.loadout, src.loadout); assert.deepStrictEqual(u.perks, src.perks);
  assert.deepStrictEqual(Core.deleteUnit(r, 'cb_pikeman_empire'), t);
});
test('addUnit refuses a used id, a bad id, the general and the 17th unit', function () { /* each throws; fill a culture to 16 with addUnit, the next throws */ });
test('deleteUnit refuses stock units and the general', function () { /* both throw */ });
```

- [ ] **Step 2: Run the tests.** Expected: FAIL.
- [ ] **Step 3: Implement the functions.**
- [ ] **Step 4: Run the tests.** Expected: PASS.
- [ ] **Step 5: Commit** (`Unit editor core: properties, add and delete units`)

---

### Task 5: Page — open, list, gear, picker, save

**Files:**
- Create: `tools\UnitEditor\index.html`
- Create: `.claude\launch.json` (a static server for checking the page in the browser pane)

**Interfaces:**
- Consumes: `UnitEditorCore` (Tasks 2–3), `TroopLoadoutCore`, `window.UNIT_EDITOR_DATA` (Task 1).
- Script tags, in this order: `unit-editor-data.js`, `../../../RealisticBattleProject/tools/TroopLoadoutEditor/troop-loadout-core.js`, `unit-editor-core.js`.
- Page state: `dir` (the folder handle), `culture`, `handles` and `opened` (the three file handles and the texts as read), `texts` (current), `parsed`, `selected` (unit id), `tab` (`where`).
- One function applies every edit: `apply(fn)` runs `texts = fn(texts)` in a `try`, shows the error's message as a toast on failure, then re-parses and re-renders. Later tasks use it.

- [ ] **Step 1: Build the page**, taking the stylesheet, the modal markup, `esc`, `fmt`, `toast` and the picker (`COLS`, `COLSETS`, `openPicker`, `renderPicker`, `statLine`) from `..\RealisticBattleProject\tools\TroopLoadoutEditor\index.html` and dropping what this page has no use for (troop list filters, bulk edits, skill hints, local storage, the export modal, the "not in campaign" toggle and the crafted-weapon notes).
  - **Header:** "Open ModuleData folder" (`showDirectoryPicker({ mode: 'readwrite' })`), a culture select filled from the folder's `cb_rbm_loadouts_<culture>.xml` names, the catalogue line (item count and `written`, or how to make it: start the game in multiplayer once), "N of 16 units", a Problems count that opens the list (`parsed.problems` plus `itemProblems`), Save.
  - **Left:** units grouped by `ROLES`, light to heavy (`multiplier` descending), each with name, weight label and id.
  - **Middle:** the selected unit's tabs: Hero, Roster 1…n, Setup 1…n (by perk name). A tab is a table of the twelve slots: slot, item name and id, a stat line; for a setup also the `target` and whether the slot is unset, emptied or set. Under hero and roster tabs: armour totals (head, body, arm, leg summed over the armour slots) and gear weight. Roster tabs have Copy and Delete.
  - **Picker:** opens on a slot click with the items that fit, the current one pinned first, "Clear" (removes the entry; on a setup tab a second button "Empty the slot" writes `''`), and for roster tabs a tick box "in all rosters". A pick calls `apply` with `setSlot` or `setSlotInAllRosters`. Items unknown to the catalogue show their id in red.
  - **Save:** for each file whose text differs from `opened`, read the handle again; if that text differs from `opened`, name the file and stop without writing any. Otherwise write the text with the BOM the file had, set `opened = texts`, and toast the file names. Disabled without changes. `beforeunload` asks when there are unsaved changes.
  - **Checking without the picker:** when the page is served over http and the query has `dev=<culture>`, it loads the three files with `fetch` from `../../Module/ModuleData/` and Save is replaced by a line "dev: N file(s) changed". This exists so the page can be checked in a browser that cannot answer a folder dialog; say so in a comment.
- [ ] **Step 2: Add `.claude\launch.json`** with one configuration `unit-editor`: `python -m http.server 8123 --directory ..` (the `Modules` folder, so the RBM script resolves), port 8123.
- [ ] **Step 3: Check in the browser pane**

Start `unit-editor`, open `http://localhost:8123/CommanderBattleProject/tools/UnitEditor/index.html?dev=empire`.
Expected, read from the page text and the console:
- no console errors; "10 of 16 units"; Infantry lists Levy Spearman (Light), Legionary (Medium), Palatine Guard (Heavy);
- Levy Spearman's Hero tab shows `imperial_spear_t2` with a thrust damage and a length, and armour totals;
- clicking Item0 lists only weapons, the current one first; sorting by Length changes the order; searching "spear" narrows it;
- picking another spear updates the row and the header shows "dev: 1 file(s) changed"; Problems stays at 0;
- at `?dev=empire&nodata=1` (in dev mode `nodata=1` makes the page ignore `UNIT_EDITOR_DATA`, to show the state without a catalogue): the catalogue line explains how to make it, every item shows its id in red, nothing throws.
- [ ] **Step 4: Check saving by hand** (needs the folder dialog, so the user does this one): open the page from disk in Edge, pick `Module\ModuleData`, change one slot, Save. `git diff --stat` shows one file, one line. Change it back and save: `git diff` is empty. With the page open, edit the same file in an editor, change a slot in the page and Save: the page refuses and names the file.
- [ ] **Step 5: Commit** (`Unit editor page: gear and picker`)

---

### Task 6: Page — properties, units, setups; documentation

**Files:**
- Modify: `tools\UnitEditor\index.html`
- Modify: `tools\UnitEditor\README.md`, `README.md`, `CLAUDE.md`, `docs\plans\README.md`, `docs\plans\unit-editor.md` (status)
- Create: `docs\handoffs\<date>-<time>-unit-editor.md` and its index row

**Interfaces:**
- Consumes: Task 4's functions and Task 3's setup functions through `apply`.

- [ ] **Step 1: Unit properties** above the tabs: name; formation class (a select, disabled for the general); `multiplier` with the weight label it gives next to it; `cost`, `icon`, `melee_ai`, `ranged_ai`, `armor`, `movement_speed`, `combat_movement_speed`, `acceleration`; level and skills of the hero and of the troop side by side. A field applies on change. Numbers are validated before `apply` (a number, and for `multiplier` above 0 and at most 1); a bad value is marked and not written.
- [ ] **Step 2: Units.** "+ unit" under each formation class but General opens a small dialog: the unit to copy (that class's units), a name, and an id prefilled as `cb_<name in lower case, non-alphanumerics to _>_<culture>`; it calls `addUnit` and selects the new unit. Disabled at 16. "Delete unit" shows only for units that are neither stock nor the general, and asks first.
- [ ] **Step 3: Setups.** On a setup tab: name, description and icon (`setPerk`), Move left / Move right (`moveSetup`, disabled at the ends), Delete (`deleteSetup`, asks first). "+ setup" after the last tab asks for a name and the setup to copy (0 = the unit's own loadout) and calls `addSetup`. A line under the tabs says that setup order is the AI's order.
- [ ] **Step 4: Check in the browser pane** at `?dev=empire`:
- changing the multiplier of Levy Spearman to 0.60 moves it between Legionary and Palatine Guard in the list and its label reads Medium;
- "+ unit" under Infantry, copying Legionary as "Pikeman": the list shows it, the counter reads "11 of 16", Problems is 0, the header reads "dev: 3 file(s) changed"; deleting it brings the counter back and "dev: 0 file(s) changed";
- "+ setup" on a unit adds a tab; renaming it renames the tab; Delete removes it;
- no console errors throughout. Run `node tools\UnitEditor\unit-editor-test.js`: PASS.
- [ ] **Step 5: Check in the game** (the user's part, as the project tests by hand): add a unit in the page, save, build, start the server and join. The class log lists the unit under its formation class with the expected weight; it can be picked on the selection screen and spawns in its RBM gear; `RbmLoadouts` logs no problems. Then remove the unit again unless the user wants to keep it.
- [ ] **Step 6: Documentation.**
  - `tools\UnitEditor\README.md`: making the data, opening the page, what each part does, saving, the tests, the dependency on RBM's repo, the limits (item data, not battle results; a new unit keeps the copied `mp_` gear for Light mode).
  - `README.md`: a short "Editing RBM units" section pointing there.
  - `CLAUDE.md`: one line under the `tools\` paragraph.
  - `docs\plans\unit-editor.md` and the index: status to what was built and played.
  - The handoff, per `docs\handoffs\README.md`.
- [ ] **Step 7: Commit** (`Unit editor page: units, properties and setups`)

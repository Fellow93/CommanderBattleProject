# Unit editor

A local page for editing Commander Battle's units in RBM mode: their gear, their properties and weapon setups, and new units under the four formation classes. It shows the stats of the items the game actually builds.

A unit is three entries in three files of its culture under `Module\ModuleData`:

| File | Entry |
|---|---|
| `cb_mpclassdivisions_<culture>.xml` | the `MPClassDivision`: multiplier, cost, icon, AI and speed values, and the weapon setups as `Perk`s |
| `cb_mpcharacters_<culture>.xml` | the hero and troop `NPCCharacter`s: name, formation class (`default_group`), level, skills |
| `cb_rbm_loadouts_<culture>.xml` | the `Class`: RBM mode's gear (Hero, Troop rosters, a `Setup` per weapon setup) |

## Making the data

The item catalogue, `unit-editor-data.js`, is generated and gitignored (it contains TaleWorlds text). Start the game in multiplayer, or the dedicated server, once: `RBM.Multiplayer` (`RbmItemCatalogue`) writes it into this folder when the folder exists, so only on a development machine. The log says "Item catalogue: N items written", and the file carries the stats fingerprint of `RbmItemSetCheck`'s "Item fingerprints" line.

It lists every item but the `mp_` ones. The tier comes from the item id (`_tN`, shown with a "?"); there is no value, because multiplayer has no item value model.

Without the catalogue the page still opens, but the picker has nothing to pick from.

## Opening the page

Start the page's server from the repository and open the address it prints (`http://localhost:8123/`) in any browser:

```
node tools\UnitEditor\serve.js
```

The server reads and writes the repository's `Module\ModuleData` itself, so there is no folder to pick: the page opens the first culture at once. It listens on this machine only, and reads and writes only existing `cb_*.xml` files of that folder. Another folder and port can be given: `node tools\UnitEditor\serve.js <folder> <port>`.

Without the server, open `index.html` from disk in Edge or Chrome (it needs the browser's folder picker), click **Open ModuleData folder** and pick the repository's `Module\ModuleData`.

The page needs RBM's repository next to this one: it loads `..\RealisticBattleProject\tools\TroopLoadoutEditor\troop-loadout-core.js` for the slot check and the XML text helpers. Nothing in RBM's repository is changed.

## What each part does

- **Header:** the culture, the catalogue's date, the unit counter (at most 16 per culture), **Problems** (click to list them; click a row to open its unit) and **Save**.
- **Unit list:** the units under their formation class, light to heavy, with their weight label.
  - **+ unit** under a class copies one of that class's units under a new name and id (`cb_<name>_<culture>`, editable). The copy goes at the end of each file.
  - **Delete unit** shows only for units that are neither stock nor the general. It removes the unit's three entries; the comments above them stay.
- **Properties,** above the tabs: formation class (fixed for the general), `multiplier` with the weight label it gives, `cost`, `icon`, `melee_ai`, `ranged_ai`, `armor`, the speed values and `acceleration`, and the hero's and troop's name, level and skills side by side. The hero and the troop have names of their own (the general's: Strategos and Elite Cataphract); the unit list shows the hero's, and the heading adds the troop's when it differs. A field applies when you leave it, a list at once; the focus stays where you moved it, but the first click on a redrawn control of the pane (a tab, a button) right after a field edit applies the edit and only focuses the control, so click it again (Save and Open are not affected). A bad number is marked red and not written: `multiplier` is above 0 and at most 1, the speeds and `acceleration` above 0, the others whole numbers. The last unit of a formation class can't move to another one: move another unit in first.
- **Gear tabs:** Hero, each roster, each setup. Click a slot to pick an item: only items that fit the slot, with search, filters and sortable stat columns. "In all rosters" sets a roster slot in every roster. Rosters can be copied and deleted.
- **Setups:** a setup tab edits the setup's name, description and icon, moves it left or right, or deletes it. **+ setup** adds one, copied from another (setup 0 is the unit's own loadout), up to 16 setups per unit with setup 0: the setup index goes over the network in 4 bits. Setup order is the AI's order: the k-th AI formation of a class fields setup k.
- **Problems:** besides missing or doubled entries, a loadout `Setup` with no perk and a setup perk with no `Setup` are warnings (in RBM mode such a perk changes nothing), and a `Setup index="0"` is an error (the game refuses it).

Every edit splices the file text, so comments, tabs and CRLF are kept and only the changed parts differ.

## Power

With the catalogue, each unit in the list shows its **power** per man, its **men** and its formation's strength (men × power) as a percentage of its formation class's average; the general shows power and men only. The unit's properties end with the parts of its power.

- **Power** is RBM's singleplayer troop power (`StrategicTroopPower` in `..\RealisticBattleProject\RBMCampaign\Power`), ported in `unit-editor-core.js`: offense (melee by weapon class and skill, the bow blended in for Ranged and Horse archer units, the horse's charge) × parry × armour, plus the mount's share, / 272. It is averaged over the troop's rosters (setup 0) with the troop's skills; a roster with no priced weapon is skipped. Armour uses RBM's defaults (`armorMultiplier` 2), which multiplayer keeps.
- **Left out:** the weapon quality term (the catalogue has no damage factors), the long bow's lower efficiency (every bow counts as a composite one), item modifiers, the other weapon setups, and everything per party.
- **Men** are the server's count (`CommanderRoles.GetTroopCount`): the formation budget (54 on foot, 44 mounted, the general's guard 6) × `ceil(100 × multiplier)` / the class's average of that.
- **What the numbers mean** is written in the page: a legend above the unit list (power, men, %), a line under every property field saying what it does in the game, and a sentence under the power's parts. `cost`, `melee_ai` and `ranged_ai` are stock values Commander Battle doesn't use (no code was found reading the two AI values); `armor` counts in Light mode only.
- **men,** next to `multiplier` in the unit's properties, shows the men the multiplier gives. Typing a count sets the multiplier (two decimals) that gives it, or the nearest count a multiplier up to 1 can reach. The class's other units get fewer or more men for it, since the class shares one average. It is fixed for the general's guard and for the only unit of a class.
- **Level this class** sets each multiplier of the class to `k / power`, `k` keeping the class's average multiplier (lower when the weakest unit would pass 1: only the ratios count in the game), so its formations come out about equally strong. It lists the multipliers and men old → new first, then writes only `multiplier` attributes (two decimals, 0.01 to 1). Weight labels may change. Units with no power keep theirs.

It is a model, not a battle: matchups (spears against horse) and balance between classes or cultures are not in it.

## Saving

**Save** writes the changed files in place. It first reads them again and writes nothing if one changed on disk since it was opened. If a write fails partway, the message names the files written and the ones not. Closing the page with unsaved edits asks first.

Build as usual afterwards: `DeployModule` copies the files to the game and the dedicated server.

To try the page without touching the real files, copy the `cb_*.xml` files to another folder and start the server on it.

## Tests

```
node tools\UnitEditor\unit-editor-test.js
```

They cover `unit-editor-core.js`, which holds all reading and editing; the page only wires it up.

The checks of particular values (unit counts, item ids, perk names, changed lines) run on `test-data\`, a frozen copy of the empire files, so saving an edit to the live files doesn't break them. Only generic checks run on the live `Module\ModuleData` files of every culture: they parse without problems, every unit has its hero, troop and loadout, and adding then deleting a unit gives back the same texts. To refresh the fixture, copy the three `cb_*_empire.xml` files there again (a plain file copy keeps the bytes) and fix the tests the new values break.

## Limits

- The numbers are item data, not battle results. RBM Combat's armour scaling and skill-based weapon speeds are applied per agent on the server.
- A new unit keeps the copied unit's faces and its `mp_` gear for Light mode; the page edits RBM mode's gear only.
- The formation classes are the four the game has; the general's is fixed.

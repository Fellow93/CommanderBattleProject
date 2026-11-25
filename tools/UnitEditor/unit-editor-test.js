// Tests of the unit editor core: node tools\UnitEditor\unit-editor-test.js
// The value checks run on a frozen copy of the empire files (test-data/), so saving an edit to the live files doesn't
// break them; only generic checks run on the live Module/ModuleData files.
'use strict';
var assert = require('assert');
var fs = require('fs');
var path = require('path');
var Core = require('./unit-editor-core.js');

var DATA = path.join(__dirname, '..', '..', 'Module', 'ModuleData');
var FIXTURE = path.join(__dirname, 'test-data');
var CULTURES = ['vlandia', 'empire', 'khuzait', 'aserai', 'sturgia', 'battania'];

function read(dir, file) { return fs.readFileSync(path.join(dir, file), 'utf8').replace(/^﻿/, ''); }
function loadFrom(dir, culture) {
  return {
    divisions: read(dir, 'cb_mpclassdivisions_' + culture + '.xml'),
    characters: read(dir, 'cb_mpcharacters_' + culture + '.xml'),
    loadouts: read(dir, 'cb_rbm_loadouts_' + culture + '.xml')
  };
}
// The live files of a culture.
function load(culture) { return loadFrom(DATA, culture); }
// The frozen empire files.
function empire() { return loadFrom(FIXTURE, 'empire'); }
// The lines between the common head and tail of two texts: the longer side of the change.
function diffLines(a, b) {
  var x = a.split('\n'), y = b.split('\n'), h = 0, t = 0;
  while (h < x.length && h < y.length && x[h] === y[h]) h++;
  while (t < x.length - h && t < y.length - h && x[x.length - 1 - t] === y[y.length - 1 - t]) t++;
  var xm = x.slice(h, x.length - t), ym = y.slice(h, y.length - t);
  return ym.length >= xm.length ? ym : xm;
}
function unit(texts, id) { return Core.parse(texts).units.filter(function (u) { return u.id === id; })[0]; }
function problemTexts(texts) { return Core.parse(texts).problems.map(function (p) { return p.text; }).join('\n'); }
function test(name, fn) {
  try { fn(); } catch (e) { console.log('FAIL ' + name); console.log(e && e.stack || e); process.exit(1); }
  console.log('ok   ' + name);
}

// ---------------------------------------------------------------- the live files (generic checks only)

test('the live files of every culture parse without problems and every unit joins', function () {
  CULTURES.forEach(function (c) {
    var p = Core.parse(load(c));
    assert.deepStrictEqual(p.problems, [], c);
    assert.ok(p.units.length > 0, c);
    p.units.forEach(function (u) { assert.ok(u.hero && u.troop && u.loadout, c + ' ' + u.id); });
  });
});
test('addUnit and deleteUnit round-trip in every live culture, with a name to escape', function () {
  CULTURES.forEach(function (c) {
    var t = load(c), from = Core.parse(t).units.filter(function (u) { return !u.general; })[0], name = 'Kern\'s "best" & <co>';
    var r = Core.addUnit(t, from.id, 'cb_new_' + c, name), u = unit(r, 'cb_new_' + c);
    assert.strictEqual(u.name, name, c); assert.strictEqual(u.troop.name, name, c);
    assert.strictEqual(u.role, from.role, c); assert.deepStrictEqual(u.hero.skills, from.hero.skills, c);
    assert.deepStrictEqual(Core.deleteUnit(r, 'cb_new_' + c), t, c);
  });
});

// ---------------------------------------------------------------- reading (fixture)

test('parse joins the three files', function () {
  var p = Core.parse(empire());
  assert.strictEqual(p.units.length, 10); // the classes plus the general
  assert.deepStrictEqual(p.problems, []);
  p.units.forEach(function (u) { assert.ok(u.hero && u.troop && u.loadout, u.id); });
});
test('empire light infantry reads as the files have it', function () {
  var u = Core.parse(empire()).units.filter(function (x) { return x.id === 'mp_skirmisher_empire'; })[0];
  assert.strictEqual(u.role, 'Infantry'); assert.strictEqual(u.name, 'Levy Spearman');
  assert.strictEqual(u.stock, true); assert.strictEqual(u.weight, 'Light');
  assert.strictEqual(u.division.multiplier, '0.85');
  assert.deepStrictEqual(u.perks.map(function (p) { return p.name; }), ['Spear and shield', 'Sword and shield', 'Skirmisher']);
  assert.strictEqual(u.hero.skills.Polearm, 80);
  assert.strictEqual(u.loadout.hero.Item0, 'imperial_spear_t2');
  assert.strictEqual(u.loadout.setups[1].name, 'Sword and shield');
  assert.deepStrictEqual(u.loadout.setups[1].slots, [{ slot: 'Item0', id: 'empire_sword_1_t2', target: '' }, { slot: 'Item2', id: '', target: '' }]);
});
test('the general has role General and no weight', function () {
  var g = Core.parse(empire()).units.filter(function (x) { return x.general; })[0];
  assert.strictEqual(g.id, 'cb_general_empire'); assert.strictEqual(g.role, 'General'); assert.strictEqual(g.weight, '');
});
test('entries that do not join are problems, not exceptions', function () {
  var t = empire();
  t.loadouts = t.loadouts.replace('<Class id="mp_skirmisher_empire">', '<Class id="nobody">');
  var texts = problemTexts(t);
  assert.ok(/mp_skirmisher_empire.*no loadout/.test(texts)); assert.ok(/nobody.*no class division/.test(texts));
});
test('duplicate ids, split groups and setups without a perk are problems', function () {
  var t = empire();
  t.divisions = t.divisions.replace('id="mp_shock_infantry_empire"', 'id="mp_skirmisher_empire"');
  t.characters = t.characters.replace('id="cb_levy_spearman_empire_troop" default_group="Infantry"', 'id="cb_levy_spearman_empire_troop" default_group="Ranged"');
  t.loadouts = t.loadouts.replace('<Setup index="2" name="Skirmisher">', '<Setup index="7" name="Skirmisher">');
  var texts = problemTexts(t);
  assert.ok(/mp_skirmisher_empire.*twice/.test(texts), texts);
  assert.ok(/Infantry.*Ranged/.test(texts), texts);
  assert.ok(/setup 7 has no perk/.test(texts), texts);
});
test('a Setup index 0 is a bad problem', function () {
  var t = empire();
  t.loadouts = t.loadouts.replace('<Setup index="1" name="Sword and shield">', '<Setup index="0" name="Sword and shield">');
  var p = Core.parse(t).problems.filter(function (x) { return /setup index "0"/.test(x.text); });
  assert.strictEqual(p.length, 1, problemTexts(t));
  assert.strictEqual(p[0].cls, 'bad'); assert.strictEqual(p[0].unit, 'mp_skirmisher_empire');
});
test('a setup perk without a loadout Setup is a warning naming the perk', function () {
  var t = empire();
  t.loadouts = t.loadouts.replace(/\r\n\t\t<Setup index="2" name="Skirmisher">[\s\S]*?<\/Setup>/, '');
  var p = Core.parse(t).problems;
  assert.strictEqual(p.length, 1, problemTexts(t));
  assert.strictEqual(p[0].cls, 'warn'); assert.strictEqual(p[0].unit, 'mp_skirmisher_empire');
  assert.ok(/setup 2.*Skirmisher.*no loadout Setup/.test(p[0].text), p[0].text);
});
test('attribute values are unescaped', function () {
  var t = empire();
  t.characters = t.characters.replace('name="Levy Spearman"', 'name="Levy &amp; &quot;Spear&quot; &lt;man&gt; &apos;"');
  var u = Core.parse(t).units[0];
  assert.strictEqual(u.name, 'Levy & "Spear" <man> \'');
});
test('weightLabel follows the bands', function () {
  assert.strictEqual(Core.weightLabel('Infantry', 0.65, 3), 'Medium');
  assert.strictEqual(Core.weightLabel('Cavalry', 0.30, 3), 'Heavy');
  assert.strictEqual(Core.weightLabel('HorseArcher', 0.43, 1), '');
});
test('itemProblems names unknown items, misfits and mixed roster types', function () {
  var items = {
    sword: { id: 'sword', type: 'OneHandedWeapon' }, helm: { id: 'helm', type: 'HeadArmor' },
    mail: { id: 'mail', type: 'BodyArmor' }, cape: { id: 'cape', type: 'Cape' }
  };
  var parsed = { units: [{ id: 'u', loadout: {
    hero: { Item0: 'sword', Head: 'gone' },
    rosters: [{ Body: 'mail' }, { Body: 'cape' }],
    setups: { 1: { name: 's', slots: [{ slot: 'Item0', id: 'helm', target: '' }, { slot: 'Item2', id: '', target: '' }] } }
  } }] };
  var p = Core.itemProblems(parsed, items);
  var texts = p.map(function (x) { return x.text; }).join('\n');
  assert.ok(/gone.*not in the catalogue/.test(texts), texts);
  assert.ok(/helm.*does not fit Item0/.test(texts), texts);
  assert.ok(/Body.*BodyArmor.*Cape/.test(texts), texts);
  assert.ok(p.every(function (x) { return x.unit === 'u'; }));
  assert.ok(Core.itemProblems(Core.parse(empire()), {}).length > 0, 'the empire files against an empty catalogue');
});

// ---------------------------------------------------------------- gear edits

var SK = 'mp_skirmisher_empire', SH = 'mp_shock_infantry_empire';
test('an edit that sets the value a slot already has changes nothing', function () {
  var t = empire();
  assert.deepStrictEqual(Core.setSlot(t, 'mp_skirmisher_empire', { kind: 'hero' }, 'Item0', 'imperial_spear_t2'), t);
});
test('setSlot changes one line and keeps CRLF', function () {
  var t = empire(), r = Core.setSlot(t, 'mp_skirmisher_empire', { kind: 'roster', index: 0 }, 'Item0', 'imperial_spear_t3');
  assert.strictEqual(diffLines(t.loadouts, r.loadouts).length, 1);
  assert.strictEqual(r.divisions, t.divisions); assert.strictEqual(r.characters, t.characters);
  assert.ok(r.loadouts.indexOf('\r\n') > 0 && !/[^\r]\n/.test(r.loadouts));
  assert.strictEqual(Core.parse(r).units[0].loadout.rosters[0].Item0, 'imperial_spear_t3');
});
test('setSlot adds a missing slot in slot order and removes one', function () {
  var t = empire(), r = Core.setSlot(t, SK, { kind: 'hero' }, 'Item3', 'northern_javelin_1_t2');
  assert.deepStrictEqual(diffLines(t.loadouts, r.loadouts), ['\t\t\t<equipment slot="Item3" id="Item.northern_javelin_1_t2" />\r']);
  assert.strictEqual(r.loadouts.split('\n').length, t.loadouts.split('\n').length + 1);
  assert.ok(r.loadouts.indexOf('id="Item.gladius_b" />\r\n\t\t\t<equipment slot="Item3"') > 0, 'right after Item2');
  assert.strictEqual(unit(r, SK).loadout.hero.Item3, 'northern_javelin_1_t2');
  assert.deepStrictEqual(Core.setSlot(r, SK, { kind: 'hero' }, 'Item3', null), t);
  assert.throws(function () { Core.setSlot(t, SK, { kind: 'hero' }, 'Item9', 'x'); }, /slot/);
  assert.throws(function () { Core.setSlot(t, 'nobody', { kind: 'hero' }, 'Item0', 'x'); }, /nobody/);
  assert.throws(function () { Core.setSlot(t, SK, { kind: 'roster', index: 9 }, 'Item0', 'x'); }, /roster/);
});
test('a setup slot can be emptied', function () {
  var t = empire(), r = Core.setSlot(t, SK, { kind: 'setup', index: 2 }, 'Item2', '');
  assert.deepStrictEqual(diffLines(t.loadouts, r.loadouts), ['\t\t\t<equipment slot="Item2" />\r']);
  assert.deepStrictEqual(unit(r, SK).loadout.setups[2].slots.map(function (e) { return e.slot + '=' + e.id; }), ['Item0=northern_javelin_1_t2', 'Item2=', 'Item3=northern_javelin_1_t2']);
  var r2 = Core.setSlot(t, SK, { kind: 'setup', index: 1 }, 'Item0', '');
  assert.deepStrictEqual(diffLines(t.loadouts, r2.loadouts), ['\t\t\t<equipment slot="Item0" />\r']);
  assert.throws(function () { Core.setSlot(t, SK, { kind: 'hero' }, 'Item0', ''); }, /setup/);
  assert.throws(function () { Core.setSlot(t, SK, { kind: 'setup', index: 3 }, 'Item0', 'x'); }, /setup/);
});
test('setSlot keeps a setup entry\'s target and writes a missing Setup', function () {
  var t = empire();
  t.loadouts = t.loadouts.replace('<equipment slot="Item2" />', '<equipment slot="Item2" target="Troops" />');
  var r = Core.setSlot(t, SK, { kind: 'setup', index: 1 }, 'Item2', 'gladius_b');
  assert.deepStrictEqual(diffLines(t.loadouts, r.loadouts), ['\t\t\t<equipment slot="Item2" target="Troops" id="Item.gladius_b" />\r']);
  t.loadouts = t.loadouts.replace(/\r\n\t\t<Setup index="2" name="Skirmisher">[\s\S]*?<\/Setup>/, '');
  r = Core.setSlot(t, SK, { kind: 'setup', index: 2 }, 'Item0', 'gladius_b');
  assert.deepStrictEqual(unit(r, SK).loadout.setups[2], { name: 'Skirmisher', slots: [{ slot: 'Item0', id: 'gladius_b', target: '' }] });
  assert.deepStrictEqual(Core.parse(r).problems, []);
});
test('the first entry of an entry-less Setup or roster keeps the comment inside', function () {
  var t = empire();
  t.loadouts = t.loadouts.replace(/(<Setup index="2" name="Skirmisher">)[\s\S]*?(<\/Setup>)/, '$1\r\n\t\t\t<!-- javelins later -->\r\n\t\t$2');
  var r = Core.setSlot(t, SK, { kind: 'setup', index: 2 }, 'Item0', 'gladius_b');
  assert.ok(r.loadouts.indexOf('<Setup index="2" name="Skirmisher">\r\n\t\t\t<!-- javelins later -->\r\n\t\t\t<equipment slot="Item0" id="Item.gladius_b" />\r\n\t\t</Setup>') > 0, 'after the comment');
  assert.deepStrictEqual(diffLines(t.loadouts, r.loadouts), ['\t\t\t<equipment slot="Item0" id="Item.gladius_b" />\r']);
  t = empire();
  var roster = /(<Troop>\r\n\t\t\t<EquipmentRoster>)[\s\S]*?(<\/EquipmentRoster>)/;
  t.loadouts = t.loadouts.replace(roster, '$1<!-- to fill --></EquipmentRoster>');
  assert.deepStrictEqual(unit(t, SK).loadout.rosters[0], {}, 'the skirmisher\'s first roster');
  r = Core.setSlot(t, SK, { kind: 'roster', index: 0 }, 'Item0', 'gladius_b');
  assert.ok(r.loadouts.indexOf('<EquipmentRoster><!-- to fill -->\r\n\t\t\t\t<equipment slot="Item0" id="Item.gladius_b" />\r\n\t\t\t</EquipmentRoster>') > 0, 'after the comment');
});
test('setSlotInAllRosters sets every roster', function () {
  var t = empire(), r = Core.setSlotInAllRosters(t, SK, 'Item0', 'imperial_spear_t3');
  var l = unit(r, SK).loadout;
  assert.ok(l.rosters.length > 1 && l.rosters.every(function (x) { return x.Item0 === 'imperial_spear_t3'; }));
  assert.strictEqual(l.hero.Item0, 'imperial_spear_t2');
  assert.strictEqual(diffLines(t.loadouts, r.loadouts).filter(function (x) { return /imperial_spear_t3/.test(x); }).length, l.rosters.length);
});
test('copyRoster and deleteRoster are inverse', function () {
  var t = empire(), r = Core.copyRoster(t, SK, 0), n = unit(t, SK).loadout.rosters.length;
  assert.strictEqual(unit(r, SK).loadout.rosters.length, n + 1);
  assert.deepStrictEqual(unit(r, SK).loadout.rosters[1], unit(t, SK).loadout.rosters[0]);
  assert.deepStrictEqual(Core.deleteRoster(r, SK, 1), t);
  for (var i = 1; i < n; i++) t = Core.deleteRoster(t, SK, 0);
  assert.strictEqual(unit(t, SK).loadout.rosters.length, 1);
  assert.throws(function () { Core.deleteRoster(t, SK, 0); }, /last roster/);
});
test('addSetup writes the perk and the Setup, deleteSetup removes both', function () {
  var t = empire(), r = Core.addSetup(t, 'mp_skirmisher_empire', 1, 'Mace & shield');
  var u = Core.parse(r).units[0];
  assert.strictEqual(u.perks.length, 4); assert.strictEqual(u.perks[3].name, 'Mace & shield');
  assert.ok(r.divisions.indexOf('name="Mace &amp; shield"') > 0);
  assert.deepStrictEqual(u.loadout.setups[3].slots, u.loadout.setups[1].slots);
  assert.deepStrictEqual(Core.deleteSetup(r, 'mp_skirmisher_empire', 3), t);
});
test('addSetup from setup 0 has no effects and no slots', function () {
  var t = empire(), r = Core.addSetup(t, SK, 0, 'Plain');
  var u = unit(r, SK);
  assert.deepStrictEqual(u.loadout.setups[3], { name: 'Plain', slots: [] });
  assert.ok(r.divisions.indexOf('name="Plain" description="Spear, large kite shield and a gladius." icon="PerkSpears" perk_list="1" />') > 0);
  assert.deepStrictEqual(Core.parse(r).problems, []);
  assert.deepStrictEqual(Core.deleteSetup(r, SK, 3), t);
});
test('addSetup refuses a setup past ' + Core.MAX_SETUPS + ' (the perk index goes over the network in 4 bits)', function () {
  var t = empire();
  for (var i = unit(t, SK).perks.length; i < Core.MAX_SETUPS; i++) t = Core.addSetup(t, SK, 1, 'Extra ' + i);
  assert.strictEqual(unit(t, SK).perks.length, Core.MAX_SETUPS);
  assert.throws(function () { Core.addSetup(t, SK, 1, 'One more'); }, /16/);
});
test('deleteSetup renumbers the later setups', function () {
  var t = empire(), old = unit(t, SH), r = Core.deleteSetup(t, SH, 1), u = unit(r, SH);
  assert.deepStrictEqual(u.perks.map(function (p) { return p.name; }), [old.perks[0].name, old.perks[2].name, old.perks[3].name]);
  assert.deepStrictEqual(u.loadout.setups, { 1: old.loadout.setups[2], 2: old.loadout.setups[3] });
  assert.deepStrictEqual(Core.parse(r).problems, []);
  assert.throws(function () { Core.deleteSetup(t, SH, 0); }, /setup/);
});
test('deleteSetup keeps the Setup indexes in step when a setup has no loadout entry', function () {
  var t = empire();
  t.loadouts = t.loadouts.replace(/\r\n\t\t<Setup index="2" name="Menavlion">[\s\S]*?<\/Setup>/, '');
  var old = unit(t, SH).loadout.setups;
  var u = unit(Core.deleteSetup(t, SH, 1), SH);
  assert.deepStrictEqual(u.loadout.setups, { 2: old[3] });
  assert.strictEqual(u.perks[2].name, old[3].name);
  u = unit(Core.deleteSetup(t, SH, 2), SH);
  assert.deepStrictEqual(u.loadout.setups, { 1: old[1], 2: old[3] });
  assert.strictEqual(u.perks[2].name, old[3].name);
});
test('moveSetup swaps perks and Setup indexes; setup 0 does not move', function () {
  var t = empire(), old = unit(t, SH), r = Core.moveSetup(t, SH, 1, 1), u = unit(r, SH);
  assert.deepStrictEqual(u.perks, [old.perks[0], old.perks[2], old.perks[1], old.perks[3]]);
  assert.deepStrictEqual(u.loadout.setups, { 1: old.loadout.setups[2], 2: old.loadout.setups[1], 3: old.loadout.setups[3] });
  assert.deepStrictEqual(Core.parse(r).problems, []);
  assert.deepStrictEqual(Core.parse(Core.moveSetup(r, SH, 1, 1)), Core.parse(t));
  assert.throws(function () { Core.moveSetup(t, SH, 1, -1); }, /setup/);
  assert.throws(function () { Core.moveSetup(t, SH, 3, 1); }, /setup/);
});
test('setPerk renames the perk and its Setup', function () {
  var t = empire(), name = 'Sword & "shield" <x>';
  var r = Core.setPerk(t, SK, 1, { name: name, icon: 'PerkMace' });
  assert.strictEqual(diffLines(t.divisions, r.divisions).length, 1);
  assert.strictEqual(diffLines(t.loadouts, r.loadouts).length, 1);
  var u = unit(r, SK);
  assert.strictEqual(u.perks[1].name, name); assert.strictEqual(u.perks[1].icon, 'PerkMace');
  assert.strictEqual(u.perks[1].description, 'Spatha and large kite shield.');
  assert.strictEqual(u.loadout.setups[1].name, name);
  assert.ok(r.loadouts.indexOf('name="Sword &amp; &quot;shield&quot; &lt;x&gt;"') > 0);
  assert.throws(function () { Core.setPerk(t, SK, 5, { name: 'x' }); }, /setup/);
});

// ---------------------------------------------------------------- unit properties, adding and deleting units

var SK_HERO = 'cb_levy_spearman_empire_hero';
test('setDivisionAttr changes one line of the divisions file', function () {
  var t = empire(), r = Core.setDivisionAttr(t, 'mp_skirmisher_empire', 'multiplier', '0.80');
  assert.strictEqual(diffLines(t.divisions, r.divisions).length, 1);
  assert.strictEqual(r.characters, t.characters); assert.strictEqual(r.loadouts, t.loadouts);
  assert.strictEqual(unit(r, SK).division.multiplier, '0.80');
  assert.throws(function () { Core.setDivisionAttr(t, SK, 'id', 'x'); }, /id/);
  assert.throws(function () { Core.setDivisionAttr(t, 'nobody', 'cost', '1'); }, /nobody/);
});
test('setCharacterName renames one character, escapes and reads back', function () {
  var t = empire(), g = unit(t, 'cb_general_empire');
  assert.notStrictEqual(g.hero.name, g.troop.name, 'the general\'s hero and troop have their own names');
  var r = Core.setCharacterName(t, g.hero.id, 'Strategos "Old" & <Wise>'), u = unit(r, 'cb_general_empire');
  assert.strictEqual(u.hero.name, 'Strategos "Old" & <Wise>'); assert.strictEqual(u.name, u.hero.name);
  assert.strictEqual(u.troop.name, g.troop.name);
  assert.strictEqual(diffLines(t.characters, r.characters).length, 1);
  assert.strictEqual(r.divisions, t.divisions); assert.strictEqual(r.loadouts, t.loadouts);
  assert.strictEqual(unit(Core.setCharacterName(t, unit(t, SK).troop.id, 'Kern\'s men'), SK).troop.name, 'Kern\'s men');
  assert.throws(function () { Core.setCharacterName(t, 'nobody', 'x'); }, /nobody/);
});
test('setRole moves both characters and the weight label follows', function () {
  var t = empire(), r = Core.setRole(t, SK, 'Ranged'), u = unit(r, SK);
  assert.strictEqual(u.role, 'Ranged'); assert.strictEqual(u.troop.group, 'Ranged');
  assert.strictEqual(u.weight, Core.weightLabel('Ranged', 0.85, 3));
  assert.deepStrictEqual(Core.parse(r).problems, []);
  // Line by line: only the hero's and the troop's start tags change.
  var a = t.characters.split('\n'), b = r.characters.split('\n');
  assert.strictEqual(a.length, b.length);
  var changed = b.filter(function (l, i) { return l !== a[i]; });
  assert.strictEqual(changed.length, 2);
  assert.ok(changed.every(function (l) { return /default_group="Ranged"/.test(l); }), changed.join('\n'));
  assert.throws(function () { Core.setRole(t, SK, 'General'); }, /General/);
  assert.throws(function () { Core.setRole(t, 'cb_general_empire', 'Infantry'); }, /general/);
});
test('setRole refuses to move the last unit out of its formation class', function () {
  var t = empire(), inf = Core.parse(t).units.filter(function (u) { return u.role === 'Infantry'; });
  assert.ok(inf.length > 1);
  for (var i = 0; i < inf.length - 1; i++) t = Core.setRole(t, inf[i].id, 'Ranged');
  var last = inf[inf.length - 1].id;
  assert.throws(function () { Core.setRole(t, last, 'Ranged'); }, /last Infantry.*move another unit in first/);
  assert.deepStrictEqual(Core.setRole(t, last, 'Infantry'), t, 'its own role is no move');
});
test('setSkill sets an entry and adds a missing one', function () {
  var t = empire(), r = Core.setSkill(t, SK_HERO, 'Polearm', 90);
  assert.deepStrictEqual(diffLines(t.characters, r.characters), ['\t\t\t<skill id="Polearm" value="90" />']);
  assert.strictEqual(unit(r, SK).hero.skills.Polearm, 90);
  r = Core.setSkill(t, SK_HERO, 'Athletics', 50);
  assert.deepStrictEqual(diffLines(t.characters, r.characters), ['\t\t\t<skill id="Athletics" value="50" />']);
  assert.ok(r.characters.indexOf('<skill id="Throwing" value="80" />\n\t\t\t<skill id="Athletics" value="50" />') > 0, 'after the last');
  assert.strictEqual(unit(r, SK).hero.skills.Athletics, 50);
  assert.throws(function () { Core.setSkill(t, SK_HERO, 'Polearm', 1.5); }, /integer/);
  assert.throws(function () { Core.setSkill(t, 'nobody', 'Polearm', 1); }, /nobody/);
});
test('setLevel writes an integer level', function () {
  var t = empire(), r = Core.setLevel(t, SK_HERO, 12);
  assert.strictEqual(diffLines(t.characters, r.characters).length, 1);
  assert.strictEqual(unit(r, SK).hero.level, 12); assert.strictEqual(unit(r, SK).troop.level, 10);
  assert.throws(function () { Core.setLevel(t, SK_HERO, -1); }, /integer/);
  assert.throws(function () { Core.setLevel(t, SK_HERO, 'x'); }, /integer/);
});
test('addUnit writes all three entries and deleteUnit restores the files', function () {
  var t = empire(), r = Core.addUnit(t, 'mp_skirmisher_empire', 'cb_pikeman_empire', 'Pikeman');
  var p = Core.parse(r), u = p.units.filter(function (x) { return x.id === 'cb_pikeman_empire'; })[0];
  assert.deepStrictEqual(p.problems, []);
  assert.strictEqual(u.stock, false); assert.strictEqual(u.name, 'Pikeman'); assert.strictEqual(u.troop.name, 'Pikeman');
  assert.strictEqual(u.role, 'Infantry');
  assert.strictEqual(u.hero.id, 'cb_pikeman_empire_hero'); assert.strictEqual(u.troop.id, 'cb_pikeman_empire_troop');
  assert.ok(!/_replaceWhileMerging[^>]*id="cb_pikeman_empire"|id="cb_pikeman_empire"[^>]*_replaceWhileMerging/.test(r.divisions));
  assert.ok(/<\/MPClassDivision>\n\t<MPClassDivision\n\t\tid="cb_pikeman_empire"/.test(r.divisions), 'no blank line, the file\'s indent');
  var src = Core.parse(t).units[0];
  assert.deepStrictEqual(u.loadout, src.loadout); assert.deepStrictEqual(u.perks, src.perks);
  assert.deepStrictEqual(Core.deleteUnit(r, 'cb_pikeman_empire'), t);
});
test('addUnit refuses a used id, a bad id, the general and the 17th unit', function () {
  var t = empire();
  assert.throws(function () { Core.addUnit(t, SK, 'cb_medium_cavalry_empire', 'x'); }, /used/);
  assert.throws(function () { Core.addUnit(t, SK, 'cb_levy_spearman_empire', 'x'); }, /used/);
  assert.throws(function () { Core.addUnit(t, SK, 'cb_Bad-id', 'x'); }, /cb_/);
  assert.throws(function () { Core.addUnit(t, SK, 'pikeman', 'x'); }, /cb_/);
  assert.throws(function () { Core.addUnit(t, SK, 'cb_general_x', 'x'); }, /general/);
  assert.throws(function () { Core.addUnit(t, 'cb_general_empire', 'cb_x', 'x'); }, /general/);
  assert.throws(function () { Core.addUnit(t, 'nobody', 'cb_x', 'x'); }, /nobody/);
  for (var i = Core.parse(t).units.length; i < Core.MAX_UNITS; i++) t = Core.addUnit(t, SK, 'cb_extra_' + i, 'Extra');
  assert.strictEqual(Core.parse(t).units.length, Core.MAX_UNITS);
  assert.throws(function () { Core.addUnit(t, SK, 'cb_one_more', 'x'); }, /16/);
});
test('deleteUnit refuses stock units and the general', function () {
  var t = empire();
  assert.throws(function () { Core.deleteUnit(t, SK); }, /stock/);
  assert.throws(function () { Core.deleteUnit(t, 'cb_general_empire'); }, /general/);
  assert.throws(function () { Core.deleteUnit(t, 'nobody'); }, /nobody/);
});
test('deleteUnit keeps the comment above and leaves out a missing part', function () {
  var t = empire(), note = '<!-- New Empire classes: appended after every stock class';
  assert.ok(/<!-- New Empire classes:[^>]*-->\n\t<MPClassDivision\n\t\tid="cb_medium_cavalry_empire"/.test(t.divisions), 'a comment directly above');
  var r = Core.deleteUnit(t, 'cb_medium_cavalry_empire');
  assert.ok(!unit(r, 'cb_medium_cavalry_empire')); assert.deepStrictEqual(Core.parse(r).problems, []);
  assert.ok(r.divisions.indexOf(note) > 0);
  assert.ok(r.loadouts.indexOf('<!-- Cavalry, medium: equite -->') > 0 && r.characters.indexOf('<!-- Cavalry, medium: equite -->') > 0);
  t = empire(); r = Core.deleteUnit(t, 'cb_light_horse_archer_empire');
  assert.strictEqual(Core.parse(r).units.length, 9);
  t.loadouts = Core.deleteUnit(t, 'cb_light_horse_archer_empire').loadouts;
  assert.deepStrictEqual(Core.deleteUnit(t, 'cb_light_horse_archer_empire'), r);
});
// ---------------------------------------------------------------- power, men, levelling
// The catalogue is gitignored: these items are made up, in its shape (zero fields left out, as RbmItemCatalogue writes).

var ITEMS = {};
[
  { id: 'spear', type: 'Polearm', weapons: [{ cls: 'TwoHandedPolearm', th: 30, tht: 'Pierce', flags: ['MeleeWeapon'] }] },
  { id: 'sword', type: 'OneHandedWeapon', weapons: [{ cls: 'OneHandedSword', sw: 30, swt: 'Cut', th: 20, tht: 'Pierce', flags: ['MeleeWeapon'] }] },
  { id: 'shield', type: 'Shield', weapons: [{ cls: 'LargeShield', hp: 500, arm: 10, len: 90, flags: ['HasHitPoints', 'CanBlockRanged'] }] },
  { id: 'bow', type: 'Bow', weapons: [{ cls: 'Bow', th: 100, tht: 'Pierce', ms: 100, flags: ['RangedWeapon'] }] },
  { id: 'arrows', type: 'Arrows', weapons: [{ cls: 'Arrow', th: 10, tht: 'Pierce', stack: 30, flags: ['Consumable'] }] },
  { id: 'javelin', type: 'Thrown', weapons: [{ cls: 'Javelin', th: 20, tht: 'Pierce', ms: 30, stack: 4, flags: ['RangedWeapon', 'Consumable'] }] },
  { id: 'helm', type: 'HeadArmor', armor: { head: 40, arm: 10 } },
  { id: 'mail', type: 'BodyArmor', armor: { body: 40, arm: 20, leg: 10 } },
  { id: 'boots', type: 'LegArmor', armor: { leg: 20 } },
  { id: 'gloves', type: 'HandArmor', armor: { arm: 10 } },
  { id: 'cape', type: 'Cape', armor: { body: 6, arm: 4 } },
  { id: 'horse', type: 'Horse', horse: { spd: 40, man: 40, chg: 20, hp: 300 } },
  { id: 'barding', type: 'HorseHarness', armor: { body: 50 } }
].forEach(function (it) { ITEMS[it.id] = it; });
function soldier(role, skills, rosters) { return { role: role, troop: { skills: skills }, loadout: { hero: null, rosters: rosters, setups: {} } }; }
function near(a, b, what) { assert.ok(Math.abs(a - b) < 1e-6, what + ': ' + a + ' is not ' + b); }

test('power of an infantryman, by hand from StrategicTroopPower', function () {
  var kit = { Item0: 'spear', Item1: 'shield', Item2: 'sword', Head: 'helm', Body: 'mail', Leg: 'boots', Gloves: 'gloves', Cape: 'cape' };
  // A roster with no priced weapon is skipped; an id the catalogue lacks is left out.
  var u = soldier('Infantry', { Polearm: 125, OneHanded: 100 }, [kit, { Body: 'mail', Item3: 'nope' }]);
  // Melee: spear 28×4 × (1 + 125/250) = 168, sword 15×4.6 × (1 + 100/250) = 96.6; 0.7 × 168 + 0.3 × (168 + 96.6) / 2.
  var melee = 0.7 * 168 + 0.3 * (168 + 96.6) / 2;
  // Parry: shield, best melee skill 125: a = 0.45 + 0.30 × 0.5 = 0.6.
  var parry = Math.pow(1 / (1 - 0.6), 0.4);
  // Zones: head 40, neck 10 + 20, torso 40, shoulder 6 + 4 + 20, arm 10, leg 10/2 + 20/2; shield tier
  // ((500 - 400) × 0.005 + 10 × 0.2) × 90/60 = 3.75.
  var weighted = 0.16 * 40 + 0.03 * 30 + 0.44 * 40 + 0.12 * 30 + 0.14 * 10 + 0.11 * 15 + 4 * 3.75;
  var r = Core.power(u, ITEMS);
  near(r.power, melee * parry * (1 + weighted / 50) / 272, 'power');
  assert.strictEqual(r.parts.sets, 1); assert.strictEqual(r.parts.of, 2);
  near(r.parts.melee, melee, 'melee'); near(r.parts.mount, 0, 'mount'); near(r.parts.charge, 1, 'charge');
});
test('power of an archer, by hand; the blend only for a Ranged or Horse archer unit', function () {
  var kit = { Item0: 'bow', Item1: 'arrows', Item2: 'sword', Body: 'mail' };
  // Bow: 0.5 × (100 lb × 4.448) × 25 × 0.0254 m × 0.9 J, × 0.7 × (1 + 125/250). Sword: 69 × (1 + 50/250) = 82.8.
  var ranged = 0.5 * 100 * 4.448 * 25 * 0.0254 * 0.9 * 0.7 * 1.5, melee = 82.8;
  var parry = Math.pow(1 / (1 - (0.20 + 0.18 * 50 / 250)), 0.4);
  var armour = 1 + (0.03 * 20 + 0.44 * 40 + 0.12 * 20 + 0.11 * 5) / 50;
  var r = Core.power(soldier('Ranged', { Bow: 125, OneHanded: 50 }, [kit]), ITEMS);
  near(r.power, (0.7 * ranged + 0.3 * melee) * 1.35 * parry * armour / 272, 'ranged power');
  near(r.parts.ranged, ranged, 'ranged part');
  near(Core.power(soldier('Infantry', { Bow: 125, OneHanded: 50 }, [kit]), ITEMS).power, melee * parry * armour / 272, 'as infantry');
  // A bow without arrows, or javelins alone (thrown weapons are not priced), shoot nothing.
  near(Core.power(soldier('Ranged', { Bow: 125, OneHanded: 50 }, [{ Item0: 'bow', Item2: 'sword', Body: 'mail' }]), ITEMS).power, melee * parry * armour / 272, 'no arrows');
  assert.strictEqual(Core.power(soldier('Ranged', { Throwing: 200 }, [{ Item0: 'javelin', Body: 'mail' }]), ITEMS).power, 0);
});
test('power of a cavalryman, by hand: charge and mount', function () {
  var kit = { Item0: 'spear', Body: 'mail', Horse: 'horse', HorseHarness: 'barding' };
  // Spear 112 × (1 + 250/250) = 224, charge × (1 + 0.004 × 20); parry 0.20 + 0.18; mount 0.25 for riding +
  // (300 + 2 × 50) / 440 × 0.43.
  var product = 224 * 1.08 * Math.pow(1 / (1 - 0.38), 0.4) * (1 + 21.15 / 50), mount = 0.25 + 400 / 440 * 0.43;
  var r = Core.power(soldier('Cavalry', { Polearm: 250 }, [kit]), ITEMS);
  near(r.power, product * (1 + mount) / 272, 'power');
  near(r.parts.mount, mount, 'mount'); near(r.parts.charge, 1.08, 'charge');
});
test('power does not throw on missing items, rosters or a catalogue', function () {
  var u = Core.parse(empire()).units[0];
  assert.deepStrictEqual(Core.power(u, {}), { power: 0, parts: null });
  assert.deepStrictEqual(Core.power(u), { power: 0, parts: null });
  assert.deepStrictEqual(Core.power({ role: 'Infantry', troop: null, hero: null, loadout: null }, ITEMS), { power: 0, parts: null });
});
test('menOf is the game\'s count on the fixture', function () {
  // Stock = ceil(100 × multiplier); men = round(budget × stock / the role's average stock).
  // Infantry 85/65/50 (average 66.67, budget 54): 68.85, 52.65, 40.5. Ranged 68/46 (57): 64.42, 43.58.
  // Cavalry 46/30/38 (38, budget 25): 30.26, 19.74, 25. Horse archers alone: 25. The general: his guard of 6.
  assert.deepStrictEqual(Core.menOf(Core.parse(empire())), {
    mp_skirmisher_empire: 69, mp_shock_infantry_empire: 53, mp_heavy_infantry_empire: 41,
    mp_light_ranged_empire: 64, mp_heavy_ranged_empire: 44,
    mp_light_cavalry_empire: 30, mp_heavy_cavalry_empire: 20, cb_medium_cavalry_empire: 25,
    cb_light_horse_archer_empire: 25, cb_general_empire: 6
  });
});
test('setMen finds the multiplier for a men count', function () {
  // Heavy infantry to 50 men: the others' stock is 85 + 65, so men = 54 × s × 3 / (150 + s). s = 66 gives 49.5 and
  // s = 67 gives 50.02, both 50 men: the one nearer the present 0.50 is taken.
  var t = empire(), r = Core.setMen(t, 'mp_heavy_infantry_empire', 50), p = Core.parse(r);
  assert.strictEqual(p.units.filter(function (u) { return u.id === 'mp_heavy_infantry_empire'; })[0].division.multiplier, '0.66');
  assert.strictEqual(Core.menOf(p).mp_heavy_infantry_empire, 50);
  var a = t.divisions.split('\n'), b = r.divisions.split('\n');
  assert.strictEqual(b.filter(function (l, i) { return l !== a[i]; }).length, 1);
  assert.strictEqual(r.characters, t.characters); assert.strictEqual(r.loadouts, t.loadouts);
  // Out of reach (a multiplier ends at 1): the nearest count, 54 × 100 × 3 / 250 = 64.8.
  assert.strictEqual(Core.menOf(Core.parse(Core.setMen(t, 'mp_heavy_infantry_empire', 500))).mp_heavy_infantry_empire, 65);
  assert.throws(function () { Core.setMen(t, 'cb_general_empire', 10); }, /general/);
  assert.throws(function () { Core.setMen(t, 'cb_light_horse_archer_empire', 10); }, /only/);
});
test('levelClass changes only multipliers, keeps the average and levels men × power', function () {
  var t = empire(), inf = Core.parse(t).units.filter(function (u) { return u.role === 'Infantry'; });
  // Every infantry item is one of a few made-up ones: the units differ by their troops' skills and their body armour.
  var items = Object.assign({}, ITEMS), BODY = { mp_skirmisher_empire: 10, mp_shock_infantry_empire: 40, mp_heavy_infantry_empire: 70 };
  inf.forEach(function (u) {
    u.loadout.rosters.forEach(function (r) {
      Object.keys(r).forEach(function (s) {
        items[r[s]] = s === 'Body' ? { id: r[s], type: 'BodyArmor', armor: { body: BODY[u.id] } } :
          s === 'Item1' ? ITEMS.shield : /^Item/.test(s) ? ITEMS.spear : { id: r[s], type: 'Goods' };
      });
    });
  });
  var r = Core.levelClass(t, 'Infantry', items);
  assert.strictEqual(r.characters, t.characters); assert.strictEqual(r.loadouts, t.loadouts);
  var a = t.divisions.split('\n'), b = r.divisions.split('\n'), blank = function (l) { return l.replace(/multiplier="[^"]*"/, 'multiplier=""'); };
  assert.strictEqual(a.length, b.length);
  var changed = b.filter(function (l, i) { return l !== a[i]; });
  assert.ok(changed.length > 0);
  b.forEach(function (l, i) { assert.strictEqual(blank(l), blank(a[i]), 'line ' + (i + 1)); });
  var after = Core.parse(r), men = Core.menOf(after), lvl = after.units.filter(function (u) { return u.role === 'Infantry'; });
  var avg = function (us) { return us.reduce(function (s, u) { return s + parseFloat(u.division.multiplier); }, 0) / us.length; };
  assert.ok(Math.abs(avg(lvl) - avg(inf)) <= 0.01, avg(lvl) + ' vs ' + avg(inf));
  var strength = lvl.map(function (u) { return men[u.id] * Core.power(u, items).power; });
  var mean = strength.reduce(function (s, x) { return s + x; }, 0) / strength.length;
  strength.forEach(function (s) { assert.ok(Math.abs(s / mean - 1) < 0.05, strength.join(', ')); });
  assert.throws(function () { Core.levelClass(t, 'General', items); }, /general/);
  assert.throws(function () { Core.levelClass(t, 'Infantry', {}); }, /power/);
});
test('the core exports the edits, not the text helpers', function () {
  ['attr', 'attrs', 'unescape', 'topElements', 'childElements', 'setName'].forEach(function (k) { assert.ok(!(k in Core), k); });
});

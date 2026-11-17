// Unit editor: reads a culture's units from its three files under Module/ModuleData (cb_mpclassdivisions_<culture>,
// cb_mpcharacters_<culture>, cb_rbm_loadouts_<culture>). No DOM; index.html uses it, and it runs under node
// (module.exports) for unit-editor-test.js. The model holds values only: an edit finds its element in the text again.
(function (root) {
  'use strict';

  var X = typeof module !== 'undefined' && module.exports ?
    require(require('path').join(__dirname, '../../../RealisticBattleProject/tools/TroopLoadoutEditor/troop-loadout-core.js')) :
    root.TroopLoadoutCore;

  var ROLES = ['Infantry', 'Ranged', 'Cavalry', 'HorseArcher', 'General'];
  // More classes than the class menu and the AI's formation picks are laid out for.
  var MAX_UNITS = 16;
  // Weapon setup perks per unit, setup 0 included: the perk index goes over the network in 4 bits.
  var MAX_SETUPS = 16;

  // CommanderRoles.WeightBands in CommanderBattle/CommanderRole.cs: keep in step. [light from, medium from].
  var WEIGHT_BANDS = { Infantry: [0.75, 0.58], Ranged: [0.60, 0.52], Cavalry: [0.42, 0.34], HorseArcher: [0.40, 0.36] };

  // CommanderRoles.GetWeightLabel: a role with a single class (and the general) has no weight step.
  function weightLabel(role, multiplier, unitsInRole) {
    var b = WEIGHT_BANDS[role];
    if (!b || unitsInRole < 2) return '';
    if (multiplier >= b[0]) return 'Light';
    return multiplier >= b[1] ? 'Medium' : 'Heavy';
  }

  // ---------------------------------------------------------------- text helpers (the edits locate elements with these)

  function unescape(s) {
    return s == null ? s : s.replace(/&(amp|quot|lt|gt|apos);/g, function (m, e) {
      return { amp: '&', quot: '"', lt: '<', gt: '>', apos: '\'' }[e];
    });
  }
  function attr(tag, name) { return unescape(X.getAttr(tag, name)); }
  // Every attribute of a start tag, unescaped, in order.
  function attrs(tag) {
    var re = /\s([\w.:-]+)\s*=\s*(?:"([^"]*)"|'([^']*)')/g, out = {}, m;
    while ((m = re.exec(tag))) out[m[1]] = unescape(m[2] != null ? m[2] : m[3]);
    return out;
  }
  // The child elements named name (any name when null) of the range [from, to), each with its start tag text.
  function elements(text, from, to, name) {
    return X.parseChildren(text, from, to).filter(function (n) {
      if (n.kind !== 'el' || (name && n.name !== name)) return false;
      n.tag = text.slice(n.start, n.tagEnd);
      return true;
    });
  }
  // The root's children named name.
  function topElements(text, name) {
    var r = X.rootRange(text);
    return elements(text, r.innerStart, r.innerEnd, name);
  }
  // An element's children named name (none for a self-closing element).
  function childElements(text, el, name) { return el.self ? [] : elements(text, el.tagEnd, el.closeStart, name); }

  // ---------------------------------------------------------------- reading

  function itemId(raw) { return raw ? raw.replace(/^Item\./, '') : ''; }
  // <equipment slot id/> entries -> { slot: item id }.
  function slotsOf(text, el) {
    var s = {};
    childElements(text, el, 'equipment').forEach(function (e) {
      var slot = attr(e.tag, 'slot'), id = itemId(attr(e.tag, 'id'));
      if (slot && id) s[slot] = id;
    });
    return s;
  }

  function readCharacter(text, el) {
    var skills = {};
    childElements(text, el, 'skills').forEach(function (sk) {
      childElements(text, sk, 'skill').forEach(function (s) { skills[attr(s.tag, 'id')] = parseInt(attr(s.tag, 'value'), 10) || 0; });
    });
    return {
      id: attr(el.tag, 'id'), name: attr(el.tag, 'name') || '', group: attr(el.tag, 'default_group') || '',
      level: parseInt(attr(el.tag, 'level'), 10) || 0, skills: skills
    };
  }

  function readLoadout(text, el) {
    var hero = childElements(text, el, 'Hero')[0];
    var rosters = [];
    childElements(text, el, 'Troop').forEach(function (t) {
      childElements(text, t, 'EquipmentRoster').forEach(function (r) { rosters.push(slotsOf(text, r)); });
    });
    var setups = {};
    childElements(text, el, 'Setup').forEach(function (s) {
      setups[attr(s.tag, 'index')] = {
        name: attr(s.tag, 'name') || '',
        slots: childElements(text, s, 'equipment').map(function (e) {
          return { slot: attr(e.tag, 'slot') || '', id: itemId(attr(e.tag, 'id')), target: attr(e.tag, 'target') || '' };
        })
      };
    });
    return { hero: hero ? slotsOf(text, hero) : null, rosters: rosters, setups: setups };
  }

  // id -> value of the root's elements named name; an id used twice is a problem (the last one wins, as the loader
  // deserializes it last).
  function byId(text, name, file, read, problems) {
    var map = {}, seen = {};
    topElements(text, name).forEach(function (el) {
      var id = attr(el.tag, 'id');
      if (id == null) return;
      if (seen[id] === 1) problems.push({ unit: id, cls: 'bad', text: id + ': id used twice in ' + file });
      seen[id] = (seen[id] || 0) + 1;
      map[id] = read(text, el);
    });
    return map;
  }

  function parse(texts) {
    var problems = [];
    var divs = topElements(texts.divisions, 'MPClassDivision');
    byId(texts.divisions, 'MPClassDivision', 'the class divisions', function () {}, problems);
    var chars = byId(texts.characters, 'NPCCharacter', 'the characters', readCharacter, problems);
    var loads = byId(texts.loadouts, 'Class', 'the loadouts', readLoadout, problems);
    if (divs.length > MAX_UNITS) problems.push({ unit: null, cls: 'bad', text: divs.length + ' class divisions, more than ' + MAX_UNITS });

    var units = divs.map(function (el) {
      var d = attrs(el.tag), id = d.id || '';
      var perks = [];
      childElements(texts.divisions, el, 'Perks').forEach(function (ps) {
        childElements(texts.divisions, ps, 'Perk').forEach(function (p) {
          if (attr(p.tag, 'perk_list') === '1') perks.push({ name: attr(p.tag, 'name') || '', description: attr(p.tag, 'description') || '', icon: attr(p.tag, 'icon') || '' });
        });
      });
      var hero = chars[d.hero] || null, troop = chars[d.troop] || null, loadout = loads[id] || null;
      if (!hero) problems.push({ unit: id, cls: 'bad', text: id + ': hero "' + d.hero + '" is not in the characters' });
      if (!troop) problems.push({ unit: id, cls: 'bad', text: id + ': troop "' + d.troop + '" is not in the characters' });
      if (!loadout) problems.push({ unit: id, cls: 'bad', text: id + ': no loadout class' });
      if (hero && troop && hero.group !== troop.group) {
        problems.push({ unit: id, cls: 'bad', text: id + ': the hero is ' + hero.group + ', the troop ' + troop.group });
      }
      if (loadout) {
        Object.keys(loadout.setups).forEach(function (i) {
          // RbmLoadouts.ReadClass refuses index 0: setup 0 is the unit's own loadout (Hero and rosters).
          if (/^\d+$/.test(i) && +i === 0) problems.push({ unit: id, cls: 'bad', text: id + ': loadout setup index "' + i + '" is not 1 or more' });
          else if (!(/^\d+$/.test(i) && +i < perks.length)) problems.push({ unit: id, cls: 'warn', text: id + ': loadout setup ' + i + ' has no perk' });
        });
        // Without a Setup, choosing the perk changes nothing in RBM mode.
        perks.forEach(function (p, i) {
          if (i > 0 && !loadout.setups[String(i)]) problems.push({ unit: id, cls: 'warn', text: id + ': setup ' + i + ' (' + p.name + ') has no loadout Setup' });
        });
      }
      var general = id.indexOf('cb_general_') === 0;
      return {
        id: id, role: general ? 'General' : (hero ? hero.group : ''), name: hero ? hero.name : (troop ? troop.name : ''),
        stock: '_replaceWhileMerging' in d, general: general, weight: '', division: d, perks: perks,
        hero: hero, troop: troop, loadout: loadout
      };
    });
    units.forEach(function (u) {
      var n = units.filter(function (v) { return v.role === u.role; }).length;
      u.weight = u.general ? '' : weightLabel(u.role, parseFloat(u.division.multiplier), n);
    });
    Object.keys(loads).forEach(function (id) {
      if (!divs.some(function (el) { return attr(el.tag, 'id') === id; })) problems.push({ unit: id, cls: 'warn', text: id + ': loadout class with no class division' });
    });
    return { units: units, problems: problems };
  }

  // The loadouts checked against the item catalogue (unit-editor-data.js).
  function itemProblems(parsed, itemsById) {
    var out = [];
    parsed.units.forEach(function (u) {
      if (!u.loadout) return;
      var seen = {};
      var add = function (cls, text) { if (!seen[text]) { seen[text] = true; out.push({ unit: u.id, cls: cls, text: u.id + ': ' + text }); } };
      var check = function (where, slot, id) {
        if (!id) return;
        var item = itemsById[id];
        if (!item) add('bad', where + ' ' + slot + ': ' + id + ' is not in the catalogue');
        else if (!X.fits(slot, item)) add('bad', where + ' ' + slot + ': ' + id + ' (' + item.type + ') does not fit ' + slot);
      };
      var l = u.loadout;
      if (l.hero) Object.keys(l.hero).forEach(function (s) { check('hero', s, l.hero[s]); });
      l.rosters.forEach(function (r, i) { Object.keys(r).forEach(function (s) { check('roster ' + (i + 1), s, r[s]); }); });
      Object.keys(l.setups).forEach(function (k) { l.setups[k].slots.forEach(function (e) { check('setup ' + k, e.slot, e.id); }); });
      // A bot draws every slot from a random roster, so a slot of different kinds mixes the loadout.
      X.SLOTS.forEach(function (s) {
        var types = [];
        l.rosters.forEach(function (r) {
          var it = r[s] && itemsById[r[s]];
          if (it && types.indexOf(it.type) < 0) types.push(it.type);
        });
        if (types.length > 1) add('warn', 'rosters hold different kinds in ' + s + ': ' + types.join(', '));
      });
    });
    return out;
  }

  // ---------------------------------------------------------------- gear edits
  // Each edit takes texts and returns new texts, splicing only the changed elements so the rest stays byte-identical.

  function eolOf(text) { return text.indexOf('\r\n') >= 0 ? '\r\n' : '\n'; }
  function withText(texts, key, text) { var o = Object.assign({}, texts); o[key] = text; return o; }
  // edits: [{ start, end, text }], applied from the end so the earlier positions stay valid.
  function applyEdits(text, edits) {
    edits.slice().sort(function (a, b) { return b.start - a.start; }).forEach(function (e) {
      text = text.slice(0, e.start) + e.text + text.slice(e.end);
    });
    return text;
  }
  function replaceEl(el, s) { return { start: el.start, end: el.end, text: s }; }
  // An element's removal takes its line with it: from the end of the previous line.
  function removeEl(text, el) {
    var s = el.start;
    while (s > 0 && (text[s - 1] === ' ' || text[s - 1] === '\t')) s--;
    if (text[s - 1] === '\n') s -= text[s - 2] === '\r' ? 2 : 1;
    return { start: s, end: el.end, text: '' };
  }
  // A new element on its own line after a sibling, at the sibling's indent.
  function insertAfter(text, el, s) { return { start: el.end, end: el.end, text: eolOf(text) + X.indentAt(text, el.start) + s }; }
  // The first child element of an element without one: the parent's indent plus a tab.
  function fillEmpty(text, el, s) {
    var eol = eolOf(text), pi = X.indentAt(text, el.start), line = pi + '\t' + s, inner = eol + line + eol + pi;
    if (el.self) return replaceEl(el, el.tag.replace(/\s*\/>$/, '>') + inner + '</' + el.name + '>');
    if (!text.slice(el.tagEnd, el.closeStart).trim()) return { start: el.tagEnd, end: el.closeStart, text: inner };
    // A comment inside stays: the entry goes after it, on its own line before the closing tag.
    var ls = text.lastIndexOf('\n', el.closeStart - 1) + 1;
    if (ls > el.tagEnd && !text.slice(ls, el.closeStart).trim()) return { start: ls, end: ls, text: line + eol };
    return { start: el.closeStart, end: el.closeStart, text: inner };
  }
  function elText(text, el) { return text.slice(el.start, el.end); }
  // An element's text with one attribute of its start tag set.
  function withAttr(text, el, name, value) { return X.setAttr(el.tag, name, value) + text.slice(el.tagEnd, el.end); }

  function lastById(els, id) { return els.filter(function (e) { return attr(e.tag, 'id') === id; }).pop(); }
  function divisionOf(texts, unitId) {
    var d = lastById(topElements(texts.divisions, 'MPClassDivision'), unitId);
    if (!d) throw new Error('No class division "' + unitId + '"');
    return d;
  }
  function classOf(text, unitId) {
    var c = lastById(topElements(text, 'Class'), unitId);
    if (!c) throw new Error('No loadout class "' + unitId + '"');
    return c;
  }
  // The weapon setup perks (perk_list="1"), as parse reads them.
  function perksOf(text, div) {
    var out = [];
    childElements(text, div, 'Perks').forEach(function (ps) {
      childElements(text, ps, 'Perk').forEach(function (p) { if (attr(p.tag, 'perk_list') === '1') out.push(p); });
    });
    return out;
  }
  function rostersOf(text, cls) {
    var out = [];
    childElements(text, cls, 'Troop').forEach(function (t) { out = out.concat(childElements(text, t, 'EquipmentRoster')); });
    return out;
  }
  function setupOf(text, cls, index) {
    return childElements(text, cls, 'Setup').filter(function (s) { return attr(s.tag, 'index') === String(index); }).pop();
  }
  // Setup 0 is the unit's own loadout: only 1..perks-1 are editable setups (from 0 where allowZero).
  function checkSetup(perks, index, allowZero) {
    if (!(index === +index && index % 1 === 0 && index >= (allowZero ? 0 : 1) && index < perks.length)) {
      throw new Error('No setup ' + index + (allowZero ? '' : ' to edit (setup 0 is the unit\'s own loadout)'));
    }
  }
  // A new Setup goes after the class's last Setup, else after its last child.
  function insertSetup(text, cls, s) {
    var after = childElements(text, cls, 'Setup').pop() || childElements(text, cls).pop();
    return after ? insertAfter(text, after, s) : fillEmpty(text, cls, s);
  }
  function emptySetup(index, name) { return '<Setup index="' + index + '" name="' + X.escAttr(name) + '" />'; }

  // The equipment entry of slot in a Hero, EquipmentRoster or Setup: itemId sets it, null removes it, '' writes it
  // without an id. A new entry copies a sibling's shape (minus id and target) and goes in slot order.
  function slotEdit(text, el, slot, itemId) {
    var entries = childElements(text, el, 'equipment');
    var mine = entries.filter(function (e) { return attr(e.tag, 'slot') === slot; }).pop();
    if (itemId === null) return mine ? removeEl(text, mine) : null;
    var set = function (tag) { return itemId === '' ? X.removeAttr(tag, 'id') : X.setAttr(tag, 'id', 'Item.' + itemId); };
    if (mine) return replaceEl(mine, set(mine.tag));
    var tag = set(entries.length ? X.setAttr(X.removeAttr(X.removeAttr(entries[0].tag, 'id'), 'target'), 'slot', slot) : '<equipment slot="' + slot + '" />');
    var order = function (e) { var o = X.SLOT_ORDER[attr(e.tag, 'slot')]; return o == null ? 99 : o; };
    var anchor = entries.filter(function (e) { return order(e) < X.SLOT_ORDER[slot]; }).pop();
    if (anchor) return insertAfter(text, anchor, tag);
    if (entries.length) return { start: entries[0].start, end: entries[0].start, text: tag + eolOf(text) + X.indentAt(text, entries[0].start) };
    return fillEmpty(text, el, tag);
  }

  // where: { kind: 'hero' } | { kind: 'roster', index } | { kind: 'setup', index }
  function setSlot(texts, unitId, where, slot, itemId) {
    if (X.SLOTS.indexOf(slot) < 0) throw new Error('No slot "' + slot + '"');
    if (itemId === '' && where.kind !== 'setup') throw new Error('Only a setup can empty a slot');
    var perks = perksOf(texts.divisions, divisionOf(texts, unitId));
    var text = texts.loadouts, cls = classOf(text, unitId), el;
    if (where.kind === 'hero') {
      el = childElements(text, cls, 'Hero')[0];
      if (!el) throw new Error(unitId + ' has no Hero');
    } else if (where.kind === 'roster') {
      el = rostersOf(text, cls)[where.index];
      if (!el) throw new Error(unitId + ' has no roster ' + where.index);
    } else if (where.kind === 'setup') {
      checkSetup(perks, where.index);
      el = setupOf(text, cls, where.index);
      // A setup perk without a Setup entry gets one, named as the perk.
      if (!el && itemId !== null) {
        text = applyEdits(text, [insertSetup(text, cls, emptySetup(where.index, attr(perks[where.index].tag, 'name') || ''))]);
        el = setupOf(text, classOf(text, unitId), where.index);
      }
    } else throw new Error('Unknown place "' + where.kind + '"');
    var e = el && slotEdit(text, el, slot, itemId);
    return withText(texts, 'loadouts', e ? applyEdits(text, [e]) : text);
  }

  function setSlotInAllRosters(texts, unitId, slot, itemId) {
    var n = rostersOf(texts.loadouts, classOf(texts.loadouts, unitId)).length;
    for (var i = 0; i < n; i++) texts = setSlot(texts, unitId, { kind: 'roster', index: i }, slot, itemId);
    return texts;
  }

  function rosterAt(text, unitId, index) {
    var rs = rostersOf(text, classOf(text, unitId));
    if (!rs[index]) throw new Error(unitId + ' has no roster ' + index);
    return rs;
  }
  function copyRoster(texts, unitId, index) {
    var t = texts.loadouts, r = rosterAt(t, unitId, index)[index];
    return withText(texts, 'loadouts', applyEdits(t, [insertAfter(t, r, elText(t, r))]));
  }
  function deleteRoster(texts, unitId, index) {
    var t = texts.loadouts, rs = rosterAt(t, unitId, index);
    if (rs.length < 2) throw new Error(unitId + ': the last roster can\'t be deleted');
    return withText(texts, 'loadouts', applyEdits(t, [removeEl(t, rs[index])]));
  }

  // A new setup copied from setup fromIndex: from 0, a perk without effects and a Setup without slots.
  function addSetup(texts, unitId, fromIndex, name) {
    var d = texts.divisions, perks = perksOf(d, divisionOf(texts, unitId));
    checkSetup(perks, fromIndex, true);
    if (perks.length >= MAX_SETUPS) throw new Error(unitId + ' has ' + perks.length + ' setups: a unit has at most ' + MAX_SETUPS + ', its own loadout included');
    var src = perks[fromIndex], n = perks.length;
    var perk = fromIndex === 0 ? X.setAttr(src.tag, 'name', name).replace(/\s*\/?>$/, ' />') : withAttr(d, src, 'name', name);
    var l = texts.loadouts, cls = classOf(l, unitId), from = fromIndex && setupOf(l, cls, fromIndex);
    var setup = from ? X.setAttr(X.setAttr(from.tag, 'index', String(n)), 'name', name) + l.slice(from.tagEnd, from.end) : emptySetup(n, name);
    return {
      divisions: applyEdits(d, [insertAfter(d, perks[n - 1], perk)]), characters: texts.characters,
      loadouts: applyEdits(l, [insertSetup(l, cls, setup)])
    };
  }

  // Drops setup index; the later Setup entries move down one to stay in step with their perks.
  function deleteSetup(texts, unitId, index) {
    var d = texts.divisions, perks = perksOf(d, divisionOf(texts, unitId));
    checkSetup(perks, index);
    var l = texts.loadouts, edits = [];
    childElements(l, classOf(l, unitId), 'Setup').forEach(function (s) {
      var i = +attr(s.tag, 'index');
      if (i === index) edits.push(removeEl(l, s));
      else if (i > index) edits.push(replaceEl(s, withAttr(l, s, 'index', String(i - 1))));
    });
    return { divisions: applyEdits(d, [removeEl(d, perks[index])]), characters: texts.characters, loadouts: applyEdits(l, edits) };
  }

  // Swaps setup index with index + delta: the perks' texts, and the Setup entries (their contents trade places so
  // the file keeps its order, each keeping the index of its place).
  function moveSetup(texts, unitId, index, delta) {
    var d = texts.divisions, perks = perksOf(d, divisionOf(texts, unitId)), to = index + delta;
    checkSetup(perks, index); checkSetup(perks, to);
    var l = texts.loadouts, cls = classOf(l, unitId), a = setupOf(l, cls, index), b = setupOf(l, cls, to), edits = [];
    var moved = function (el, i) { return withAttr(l, el, 'index', String(i)); };
    if (a) edits.push(replaceEl(b || a, moved(a, to)));
    if (b) edits.push(replaceEl(a || b, moved(b, index)));
    return {
      divisions: applyEdits(d, [replaceEl(perks[index], elText(d, perks[to])), replaceEl(perks[to], elText(d, perks[index]))]),
      characters: texts.characters, loadouts: applyEdits(l, edits)
    };
  }

  // fields: { name, description, icon }, each optional. A new name also goes to the Setup of that index.
  function setPerk(texts, unitId, index, fields) {
    var d = texts.divisions, perks = perksOf(d, divisionOf(texts, unitId));
    checkSetup(perks, index, true);
    var p = perks[index], tag = p.tag;
    ['name', 'description', 'icon'].forEach(function (k) { if (fields[k] != null) tag = X.setAttr(tag, k, fields[k]); });
    var l = texts.loadouts, s = fields.name != null && index > 0 && setupOf(l, classOf(l, unitId), index);
    return {
      divisions: applyEdits(d, [{ start: p.start, end: p.tagEnd, text: tag }]), characters: texts.characters,
      loadouts: s ? applyEdits(l, [replaceEl(s, withAttr(l, s, 'name', fields.name))]) : l
    };
  }

  // ---------------------------------------------------------------- unit edits

  var DIVISION_ATTRS = ['multiplier', 'cost', 'icon', 'melee_ai', 'ranged_ai', 'armor', 'movement_speed', 'combat_movement_speed', 'acceleration'];

  function characterOf(text, id) {
    var c = lastById(topElements(text, 'NPCCharacter'), id);
    if (!c) throw new Error('No character "' + id + '"');
    return c;
  }
  // The start tag of el with one attribute set, as an edit.
  function tagEdit(el, name, value) { return { start: el.start, end: el.tagEnd, text: X.setAttr(el.tag, name, value) }; }
  function wholeNumber(v) {
    var n = Number(v);
    if (v == null || String(v).trim() === '' || n % 1 !== 0 || n < 0) throw new Error('"' + v + '" is not an integer of 0 or more');
    return String(n);
  }
  function isGeneral(unitId) { return unitId.indexOf('cb_general_') === 0; }

  function setDivisionAttr(texts, unitId, name, value) {
    if (DIVISION_ATTRS.indexOf(name) < 0) throw new Error('"' + name + '" is not an editable class division attribute');
    return withText(texts, 'divisions', applyEdits(texts.divisions, [tagEdit(divisionOf(texts, unitId), name, value)]));
  }

  // The same attribute on the unit's hero and troop.
  function setCharactersAttr(texts, unitId, name, value) {
    var div = divisionOf(texts, unitId), c = texts.characters;
    var edits = ['hero', 'troop'].map(function (k) { return tagEdit(characterOf(c, attr(div.tag, k)), name, value); });
    return withText(texts, 'characters', applyEdits(c, edits));
  }
  // The hero and the troop have names of their own (the general's: Strategos and Elite Cataphract).
  function setCharacterName(texts, characterId, name) {
    var c = texts.characters;
    return withText(texts, 'characters', applyEdits(c, [tagEdit(characterOf(c, characterId), 'name', name)]));
  }
  function setRole(texts, unitId, group) {
    if (ROLES.indexOf(group) < 0 || group === 'General') throw new Error('"' + group + '" is not a role a class can take');
    if (isGeneral(unitId)) throw new Error('The general\'s role can\'t change');
    // A class the culture fields with no unit left gives its AI formations class index 0.
    var units = parse(texts).units, role = units.filter(function (u) { return u.id === unitId; }).map(function (u) { return u.role; })[0];
    if (role && role !== group && units.filter(function (u) { return u.role === role; }).length === 1) {
      throw new Error(unitId + ' is the last ' + role + ' unit: move another unit in first');
    }
    return setCharactersAttr(texts, unitId, 'default_group', group);
  }

  function setLevel(texts, characterId, level) {
    var c = texts.characters;
    return withText(texts, 'characters', applyEdits(c, [tagEdit(characterOf(c, characterId), 'level', wholeNumber(level))]));
  }
  function setSkill(texts, characterId, skillId, value) {
    var c = texts.characters, ch = characterOf(c, characterId), v = wholeNumber(value);
    var sk = childElements(c, ch, 'skills')[0];
    if (!sk) throw new Error(characterId + ' has no skills');
    var entries = childElements(c, sk, 'skill'), mine = entries.filter(function (s) { return attr(s.tag, 'id') === skillId; }).pop();
    var tag = '<skill id="' + X.escAttr(skillId) + '" value="' + v + '" />';
    var e = mine ? tagEdit(mine, 'value', v) : entries.length ? insertAfter(c, entries[entries.length - 1], tag) : fillEmpty(c, sk, tag);
    return withText(texts, 'characters', applyEdits(c, [e]));
  }

  // A new unit copied from fromUnitId. Each copy goes after the root's last element, so deleteUnit takes off exactly
  // what was added.
  function addUnit(texts, fromUnitId, newId, newName) {
    if (!/^cb_[a-z0-9_]+$/.test(newId)) throw new Error('"' + newId + '": a new id is cb_ and then lower-case letters, digits and _');
    if (isGeneral(newId)) throw new Error('"' + newId + '": cb_general_ ids are the generals\'');
    var d = texts.divisions, c = texts.characters, l = texts.loadouts, divs = topElements(d, 'MPClassDivision');
    var heroId = newId + '_hero', troopId = newId + '_troop';
    var used = lastById(divs, newId) || lastById(topElements(l, 'Class'), newId) ||
      lastById(topElements(c, 'NPCCharacter'), heroId) || lastById(topElements(c, 'NPCCharacter'), troopId);
    if (used) throw new Error('"' + newId + '" is already used');
    if (divs.length >= MAX_UNITS) throw new Error('A culture has at most ' + MAX_UNITS + ' classes');
    if (isGeneral(fromUnitId)) throw new Error('The general can\'t be copied');
    var div = divisionOf(texts, fromUnitId), cls = classOf(l, fromUnitId);
    var hero = characterOf(c, attr(div.tag, 'hero')), troop = characterOf(c, attr(div.tag, 'troop'));
    var divTag = X.removeAttr(div.tag, '_replaceWhileMerging');
    divTag = X.setAttr(X.setAttr(X.setAttr(divTag, 'id', newId), 'hero', heroId), 'troop', troopId);
    var charCopy = function (el, id) { return X.setAttr(X.setAttr(el.tag, 'id', id), 'name', newName) + c.slice(el.tagEnd, el.end); };
    var after = function (text, s) { return applyEdits(text, [insertAfter(text, topElements(text).pop(), s)]); };
    return {
      divisions: after(d, divTag + d.slice(div.tagEnd, div.end)),
      characters: after(c, charCopy(hero, heroId) + eolOf(c) + X.indentAt(c, hero.start) + charCopy(troop, troopId)),
      loadouts: after(l, withAttr(l, cls, 'id', newId))
    };
  }

  // Comments stay: one above a unit may be a hand-written section note, not just the unit's label.
  function deleteUnit(texts, unitId) {
    var div = divisionOf(texts, unitId);
    if (isGeneral(unitId)) throw new Error('The general can\'t be deleted');
    if (X.getAttr(div.tag, '_replaceWhileMerging') != null) throw new Error(unitId + ' is a stock class: it can\'t be deleted');
    var c = texts.characters, l = texts.loadouts, chars = topElements(c, 'NPCCharacter');
    var mine = [lastById(chars, attr(div.tag, 'hero')), lastById(chars, attr(div.tag, 'troop'))].filter(Boolean);
    var cls = lastById(topElements(l, 'Class'), unitId);
    return {
      divisions: applyEdits(texts.divisions, [removeEl(texts.divisions, div)]),
      characters: applyEdits(c, mine.map(function (el) { return removeEl(c, el); })),
      loadouts: cls ? applyEdits(l, [removeEl(l, cls)]) : l
    };
  }

  // ---------------------------------------------------------------- power: RBM's singleplayer troop power
  // A port of StrategicTroopPower (RBM repo, RBMCampaign/Power/StrategicTroopPower.cs) on the catalogue's item data.
  // Left out: Penetration (the catalogue has no damage factors), the long bow's efficiency (no item usage), item
  // modifiers and everything per party (perks, morale, context).

  var PowerScale = 272, BestWeaponWeight = 0.7, RangedShare = 0.7, RangedOffenseWeight = 1.35, SkillOffenseSpread = 1,
    SkillSaturation = 250, ChargeWeight = 0.004, PoundsToNewtons = 4.448, BowPowerstroke = 25 * 0.0254,
    CrossbowPowerstroke = 20 * 0.0254, BowEfficiency = 0.90, CrossbowEfficiency = 0.88, CrossbowReloadDivisor = 2.5,
    RangedEnergyScale = 0.7, MaxItemTier = 6.5, SlingEnergy = 110, ShieldPassiveWeight = 4, ActiveDefenseDamping = 0.4,
    ShieldDefenseBase = 0.45, ShieldDefenseSkillCoeff = 0.30, WeaponDefenseFloor = 0.20, WeaponDefenseSkillCoeff = 0.18,
    DefenseChanceCap = 0.75, ReferenceMountSurvival = 440, MountBonusAtReference = 0.43, BardingToHealth = 2;
  // ArmorConstant 100 / (armorMultiplier 2 × armorEffectivenessMultiplier 1): RBM's defaults, which RbmMpSettings keeps.
  var ARMOR_DIVISOR = 100 / (2 * 1);
  var ZONE = { head: 0.16, neck: 0.03, torso: 0.44, shoulder: 0.12, arm: 0.14, leg: 0.11 };
  // EquipmentIndex.WeaponItemBeginSlot..NumAllWeaponSlots: the four weapons and the extra slot.
  var WEAPON_SLOTS = ['Item0', 'Item1', 'Item2', 'Item3', 'Item4'];
  // WeaponComponentData.GetRelevantSkillFromWeaponClass (decompiled TaleWorlds.Core), as the character files name skills.
  var RELEVANT_SKILL = {
    Arrow: 'Bow', Bow: 'Bow', Bolt: 'Crossbow', Crossbow: 'Crossbow', SlingStone: 'Throwing', Sling: 'Throwing', Stone: 'Throwing',
    Boulder: 'Throwing', ThrowingAxe: 'Throwing', ThrowingKnife: 'Throwing', Javelin: 'Throwing', BallistaBoulder: 'Throwing',
    BallistaStone: 'Throwing', Dagger: 'OneHanded', OneHandedSword: 'OneHanded', OneHandedAxe: 'OneHanded', Mace: 'OneHanded',
    TwoHandedSword: 'TwoHanded', TwoHandedAxe: 'TwoHanded', TwoHandedMace: 'TwoHanded', OneHandedPolearm: 'Polearm',
    TwoHandedPolearm: 'Polearm', LowGripPolearm: 'Polearm', SmallShield: 'OneHanded', LargeShield: 'OneHanded'
  };
  // A launcher's AmmoClass, which the catalogue doesn't carry. A thrown weapon has none here: no ammo item (IsAmmo)
  // can be of its class in the game either, so it never finds ammo.
  var AMMO_CLASS = { Bow: 'Arrow', Crossbow: 'Bolt', Sling: 'SlingStone' };

  function clamp(v, lo, hi) { return v < lo ? lo : v > hi ? hi : v; }
  function flag(w, name) { return !!(w.flags && w.flags.indexOf(name) >= 0); }
  // WeaponComponentData.IsShield / IsRangedWeapon / IsAmmo, from the flag names the catalogue writes (WeaponMask is
  // MeleeWeapon | RangedWeapon).
  function inWeaponMask(w) { return flag(w, 'MeleeWeapon') || flag(w, 'RangedWeapon'); }
  function isShield(w) { return !inWeaponMask(w) && flag(w, 'HasHitPoints') && flag(w, 'CanBlockRanged'); }
  function isRangedWeapon(w) { return flag(w, 'RangedWeapon'); }
  function isAmmo(w) { return !inWeaponMask(w) && flag(w, 'Consumable'); }
  // StrategicTroopPower.SkillFracFor.
  function skillFracFor(skills, skill) { return skill ? clamp((skills[skill] || 0) / SkillSaturation, 0, 1) : 0; }
  // StrategicTroopPower.MeleeSkillOf.
  function meleeSkillOf(skills) { return Math.max(skills.OneHanded || 0, skills.TwoHanded || 0, skills.Polearm || 0); }
  // A set's slots as items; a slot whose item the catalogue lacks is left out.
  function itemsOf(set, slots, itemById) {
    return slots.map(function (s) { return itemById[set[s]]; }).filter(Boolean);
  }
  function primaryWeapon(item) { return item.weapons && item.weapons[0]; }

  // TierfOf (StrategicTroopPower) of a shield: RBM's CalculateShieldTierPatch (RealisticBattleCombatModule,
  // Items/ItemValuesTiers.Tiers.cs), clamped to 0..MaxItemTier.
  function shieldTierOf(w) {
    return clamp((((w.hp || 0) - 400) * 0.005 + (w.arm || 0) * 0.2) * ((w.len || 0) / 60), 0, MaxItemTier);
  }

  // StrategicTroopPower.ClassCeiling.
  function classCeiling(cls, blunt) {
    switch (cls) {
      case 'Dagger': case 'OneHandedSword': return blunt ? 20 * 1.6 : 15 * 4.6;
      case 'TwoHandedSword': return blunt ? 26 * 1.6 : 20 * 4.6;
      case 'OneHandedAxe': return blunt ? 20 * 1.2 : 18 * 4.6;
      case 'TwoHandedAxe': return blunt ? 26 * 1.2 : 24 * 4.6;
      case 'Mace': return 15 * 4.6;
      case 'TwoHandedMace': return 22 * 4.6;
      case 'OneHandedPolearm': return blunt ? 20 * 1.2 : 24 * 4;
      case 'TwoHandedPolearm': case 'LowGripPolearm': return blunt ? 26 * 1.2 : 28 * 4;
      default: return 0;
    }
  }

  // StrategicTroopPower.MeleeWeaponScore, without Penetration (taken as 1).
  function meleeWeaponScore(skills, w) {
    var blunt = ((w.sw || 0) >= (w.th || 0) ? w.swt : w.tht) === 'Blunt';   // BestDamageTypeOf
    var ceiling = classCeiling(w.cls, blunt);
    return ceiling <= 0 ? 0 : ceiling * (1 + SkillOffenseSpread * skillFracFor(skills, RELEVANT_SKILL[w.cls]));
  }

  // StrategicTroopPower.MeleeOffense: { melee, hasShield, shieldTier }.
  function meleeOffense(skills, items) {
    var best = 0, sum = 0, count = 0, out = { melee: 0, hasShield: false, shieldTier: 0 };
    items.forEach(function (it) {
      var w = primaryWeapon(it);
      if (!w) return;
      if (isShield(w)) { out.hasShield = true; out.shieldTier = Math.max(out.shieldTier, shieldTierOf(w)); return; }
      if (isRangedWeapon(w) || isAmmo(w)) return;
      var score = meleeWeaponScore(skills, w);
      if (score <= 0) return;
      best = Math.max(best, score); sum += score; count++;
    });
    if (count) out.melee = BestWeaponWeight * best + (1 - BestWeaponWeight) * (sum / count);
    return out;
  }

  // StrategicTroopPower.LauncherEnergyOf, every bow at BowEfficiency (the catalogue can't tell a long bow).
  function launcherEnergyOf(item, w) {
    if (w.cls === 'Sling' || item.type === 'Sling') return SlingEnergy;
    var drawWeight = w.ms || 0;
    if (drawWeight <= 0) return 0;
    var crossbow = w.cls === 'Crossbow' || item.type === 'Crossbow';
    var energy = 0.5 * (drawWeight * PoundsToNewtons) * (crossbow ? CrossbowPowerstroke : BowPowerstroke) *
      (crossbow ? CrossbowEfficiency : BowEfficiency);
    return crossbow ? energy / CrossbowReloadDivisor : energy;
  }

  // StrategicTroopPower.RangedOffense: the best launcher, if something of its ammo class is carried.
  function rangedOffense(skills, items) {
    var launcher = null, energy = 0;
    items.forEach(function (it) {
      var w = primaryWeapon(it);
      if (!w || !isRangedWeapon(w) || isAmmo(w)) return;
      var e = launcherEnergyOf(it, w);
      if (!launcher || e > energy) { launcher = w; energy = e; }
    });
    if (!launcher) return 0;
    var ammo = items.some(function (it) { var w = primaryWeapon(it); return w && isAmmo(w) && w.cls === AMMO_CLASS[launcher.cls]; });
    if (!ammo) return 0;
    return RangedEnergyScale * energy * (1 + SkillOffenseSpread * skillFracFor(skills, RELEVANT_SKILL[launcher.cls]));
  }

  // SimulationEquipmentPower.GetArmorZonesRbm (RBM repo, RBMAutoResolve/Simulation), weighted by StrategicTroopPower's zones.
  function weightedArmorOf(items) {
    var head = 0, headArm = 0, bodyArm = 0, bodyOn = 0, capeOn = 0, gloves = 0, leg = 0;
    items.forEach(function (it) {
      var a = it.armor || {};
      if (it.type === 'HeadArmor') { head += a.head || 0; headArm += a.arm || 0; }
      else if (it.type === 'BodyArmor') { bodyOn += a.body || 0; bodyArm += a.arm || 0; leg += (a.leg || 0) * 0.5; }
      else if (it.type === 'LegArmor') leg += (a.leg || 0) * 0.5;
      else if (it.type === 'HandArmor') gloves += a.arm || 0;
      else if (it.type === 'Cape') capeOn += (a.body || 0) + (a.arm || 0);
    });
    return head * ZONE.head + (headArm + bodyArm) * ZONE.neck + bodyOn * ZONE.torso + (capeOn + bodyArm) * ZONE.shoulder +
      gloves * ZONE.arm + leg * ZONE.leg;
  }

  // StrategicTroopPower.PowerOfSet: { power, parts }, power 0 for a set with no offense. Raw scale (not / PowerScale).
  // shooter: SimulationEquipmentPower.IsRangedTroop, here the unit's formation class.
  function powerOfSet(skills, shooter, set, itemById) {
    var weapons = itemsOf(set, WEAPON_SLOTS, itemById);
    var m = meleeOffense(skills, weapons), ranged = rangedOffense(skills, weapons), offense = m.melee;
    if (shooter && ranged > 0) offense = Math.max(offense, RangedShare * ranged + (1 - RangedShare) * m.melee) * RangedOffenseWeight;
    if (offense <= 0) return { power: 0 };
    var horse = itemById[set.Horse], harness = itemById[set.HorseHarness];
    horse = horse && horse.horse;
    var charge = 1 + ChargeWeight * (horse ? horse.chg || 0 : 0);   // ChargeDamageOf
    var s = clamp(meleeSkillOf(skills) / SkillSaturation, 0, 1);
    var active = m.hasShield ? clamp(ShieldDefenseBase + ShieldDefenseSkillCoeff * s, 0, DefenseChanceCap) :
      clamp(WeaponDefenseFloor + WeaponDefenseSkillCoeff * s, 0, DefenseChanceCap);
    var parry = Math.pow(1 / (1 - active), ActiveDefenseDamping);
    var armour = 1 + (weightedArmorOf(itemsOf(set, ['Head', 'Body', 'Leg', 'Gloves', 'Cape'], itemById)) + ShieldPassiveWeight * m.shieldTier) / ARMOR_DIVISOR;
    // MountFractionOf: horse.hp is the monster's hit points plus the item's bonus; barding is the harness's body armour.
    var mount = horse ? ((horse.hp || 0) + BardingToHealth * (harness && harness.armor ? harness.armor.body || 0 : 0)) / ReferenceMountSurvival * MountBonusAtReference : 0;
    var product = offense * charge * parry * armour;
    return { power: product * (1 + mount), parts: { melee: m.melee, ranged: ranged, charge: charge, parry: parry, armour: armour, mount: mount } };
  }

  // StrategicTroopPower.Measure for a parsed unit: PowerOfSet averaged over the troop's rosters (setup 0), the sets
  // without offense skipped, over PowerScale. parts are averaged the same way (sets: how many were priced).
  function power(unit, itemById) {
    var l = unit.loadout, c = unit.troop || unit.hero, skills = c ? c.skills : {};
    var sets = l ? (l.rosters.length ? l.rosters : l.hero ? [l.hero] : []) : [];
    var shooter = unit.role === 'Ranged' || unit.role === 'HorseArcher';
    var sum = 0, n = 0, parts = { melee: 0, ranged: 0, charge: 0, parry: 0, armour: 0, mount: 0 };
    sets.forEach(function (set) {
      var r = powerOfSet(skills, shooter, set, itemById || {});
      if (r.power <= 0) return;
      sum += r.power; n++;
      Object.keys(parts).forEach(function (k) { parts[k] += r.parts[k]; });
    });
    if (!n) return { power: 0, parts: null };
    Object.keys(parts).forEach(function (k) { parts[k] /= n; });
    parts.sets = n; parts.of = sets.length;
    return { power: sum / n / PowerScale, parts: parts };
  }

  // ---------------------------------------------------------------- men per formation (CommanderBattle, server)

  // CommanderBattleSettings.GetTroopBudget.
  var TROOP_BUDGET = { Infantry: 54, Ranged: 54, Cavalry: 44, HorseArcher: 44, General: 6 };
  var BOTS_PER_FORMATION = 100, MAX_TROOPS_PER_FORMATION = 1023;
  var f32 = Math.fround;   // the game computes these in float
  // MPPerkObject.GetTroopCount without perks: the class files have no TroopCountOnSpawn.
  function stockTroopCount(multiplier) { return Math.max(Math.ceil(f32(f32(BOTS_PER_FORMATION * f32(multiplier)) - f32(1e-5))), 1); }
  // CommanderRoles.GetTroopCount.
  function troopCount(budget, stock, reference) {
    var t = stock > 0 && reference > 0 ? Math.trunc(f32(f32(f32(budget * stock) / reference) + 0.5)) : budget;
    return clamp(t, 1, MAX_TROOPS_PER_FORMATION);
  }
  // Men per unit id: the formation's budget × the class's stock count / its role's average stock count
  // (CommanderRoles.GetAverageStockTroopCount). The general's class is its own reference: his bodyguard budget.
  function menOf(parsed) {
    var units = parsed.units.filter(function (u) { return TROOP_BUDGET[u.role] && !isNaN(parseFloat(u.division.multiplier)); });
    var stock = {}, sum = {}, n = {}, men = {};
    units.forEach(function (u) {
      stock[u.id] = stockTroopCount(parseFloat(u.division.multiplier));
      sum[u.role] = (sum[u.role] || 0) + stock[u.id]; n[u.role] = (n[u.role] || 0) + 1;
    });
    units.forEach(function (u) { men[u.id] = troopCount(TROOP_BUDGET[u.role], stock[u.id], f32(sum[u.role] / n[u.role])); });
    return men;
  }

  // Sets the unit's multiplier so its formation gets men soldiers, or the nearest count a multiplier of two decimals
  // can give. The class's other units get fewer or more men for it: the class shares one average.
  function setMen(texts, unitId, men) {
    var parsed = parse(texts), u = parsed.units.filter(function (x) { return x.id === unitId; })[0];
    if (!u || u.general) throw new Error('The general\'s guard has a fixed size');
    var others = parsed.units.filter(function (x) { return x.role === u.role && x.id !== unitId && !isNaN(parseFloat(x.division.multiplier)); });
    if (!others.length) throw new Error('The only ' + u.role + ' unit always gets the class\'s ' + TROOP_BUDGET[u.role] + ' men');
    var sum = 0, cur = stockTroopCount(parseFloat(u.division.multiplier) || 0), best = 0, bestOff = Infinity;
    others.forEach(function (x) { sum += stockTroopCount(parseFloat(x.division.multiplier)); });
    for (var s = 1; s <= BOTS_PER_FORMATION; s++) {
      var off = Math.abs(troopCount(TROOP_BUDGET[u.role], s, f32((sum + s) / (others.length + 1))) - men);
      // Among equally near counts, the one that moves the multiplier least.
      if (off < bestOff || (off === bestOff && Math.abs(s - cur) < Math.abs(best - cur))) { best = s; bestOff = off; }
    }
    return setDivisionAttr(texts, unitId, 'multiplier', (best / BOTS_PER_FORMATION).toFixed(2));
  }

  // Sets every multiplier of the class to k / power, k keeping the class's average multiplier (lower when a unit
  // would pass 1): men × power then comes out level. Units with power 0 keep theirs.
  function levelClass(texts, role, itemById) {
    if (role === 'General') throw new Error('The general\'s guard is not levelled');
    var rows = parse(texts).units.filter(function (u) { return u.role === role && !u.general; }).map(function (u) {
      return { id: u.id, m: parseFloat(u.division.multiplier), p: power(u, itemById).power };
    }).filter(function (r) { return r.p > 0 && r.m > 0; });
    if (!rows.length) throw new Error('No ' + role + ' unit has a power to level by');
    var sumM = 0, sumInv = 0;
    rows.forEach(function (r) { sumM += r.m; sumInv += 1 / r.p; });
    var k = sumM / sumInv, top = 0;
    // Only the ratios count in the game, so when the weakest unit would pass 1 all come down together.
    rows.forEach(function (r) { top = Math.max(top, k / r.p); });
    if (top > 1) k /= top;
    rows.forEach(function (r) {
      var v = clamp(Math.round(k / r.p * 100) / 100, 0.01, 1).toFixed(2);
      if (+v !== r.m) texts = setDivisionAttr(texts, r.id, 'multiplier', v);
    });
    return texts;
  }

  var api = {
    ROLES: ROLES, MAX_UNITS: MAX_UNITS, MAX_SETUPS: MAX_SETUPS, parse: parse, weightLabel: weightLabel, itemProblems: itemProblems,
    power: power, menOf: menOf, setMen: setMen, levelClass: levelClass,
    setSlot: setSlot, setSlotInAllRosters: setSlotInAllRosters, copyRoster: copyRoster, deleteRoster: deleteRoster,
    addSetup: addSetup, deleteSetup: deleteSetup, moveSetup: moveSetup, setPerk: setPerk,
    setDivisionAttr: setDivisionAttr, setCharacterName: setCharacterName, setRole: setRole, setLevel: setLevel, setSkill: setSkill,
    addUnit: addUnit, deleteUnit: deleteUnit
  };
  if (typeof module !== 'undefined' && module.exports) module.exports = api;
  else root.UnitEditorCore = api;
})(this);

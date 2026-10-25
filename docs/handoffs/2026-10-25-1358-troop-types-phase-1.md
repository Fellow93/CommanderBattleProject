# Handoff: troop types, phase 1 (Vlandia) installed

Session of 2026-10-25, written 13:58. Follows `2026-10-25-1221-choosing-troops-first-run.md`. Plan: `docs/plans/troop-types.md` (decisions settled; phase 1 built). `README.md` describes the behaviour.

## State

- **Nothing is committed.** The working tree on `master` sits on `755390d`. It holds:
  - choosing troops (class-weighted counts);
  - the `Directory.Build.props` per-framework obj fix;
  - troop types phase 1;
  - the plans and handoffs.
- **Installed:** a full `dotnet build "CommanderBattle.sln" -c Release` at 13:56 (0 errors, 1 old RBM warning).
  - Client DLLs are net472 and the server's are net6.0 (checked in the metadata).
  - The client `CommanderBattle.dll` is the net472 build of 13:30. The 13:56 build found it up to date, and it is byte-identical to `obj\Release\net472\` and newer than every source edit.
  - `cb_mpcharacters.xml` and `cb_mpclassdivisions.xml` are copied to the game and to the server.
- **Running:** dedicated server pid 31288, battle_terrain_033, Vlandia (CultureTeam1) vs Battania, 91 patch classes, 0 failed. No client was connected at handoff.

## What phase 1 built

The agent's report, reviewed in this session:
- **Data:** Vlandia's 8 types. The 7 stock ids are overridden in place (`_replaceWhileMerging`), and `cb_pikeman_vlandia` is appended at index 7. Each type has its own hero and troop characters and 2 perk lists:
  - list 1: Balanced, More men or Better armour;
  - list 2: three weapon options.
- **Server:**
  - `RequestTroopSelection` (from the client) is checked for slot, role and perks.
  - Before the armies spawn it applies at once. After that it becomes a pending pick, sent to everyone as `FormationSlotPendingSelection`, and applied in `NewSpawnLayout` before the counts are computed.
  - Perks go through `SelectPerk(list, index, classIndex)` and the stock `SyncPerksForCurrentlySelectedTroop`.
  - Late joiners get the pending picks.
- **Client:**
  - The stock class loadout is removed. The new `CommanderBattleTroopSelectionView` (role tabs, then types with a count preview, then perks) opens when you take a formation from the list, or from the new "Troops" button on your own row.
  - The formation list shows "(next: X)".
  - The troop count formula is shared in `CommanderRoles`.

## Verified

- Server log at mission open: "Classes for vlandia: 8: …" with our multipliers (0.85, 0.62, 0.55, 0.68, 0.46, 0.46, 0.30, 0.70; stock light infantry was 0.92) and `7 cb_pikeman_vlandia`. The other cultures still have their 7 stock classes. The in-place override and the append both load.
- No XML load errors in the engine log; the errors file is the usual 4,931 bytes of noise.

## Not verified (in-game test next)

- Joining works with the overridden classes, on client and server.
- The screen: layout, picking, perks, "applies next round" vs at once during preparation.
- The troops spawn with the type's gear. Pikemen have no shield, and their pike and brace stances work.
- The logged counts match.

## Open items

1. The in-game test above, with a Vlandian formation.
2. **Phase 2:** the other five cultures' XML. Watch the trap: `_replaceWhileMerging` only on stock ids, never on a new id (with no match it replaces the file's first class).
3. **Phase 3:** screen polish. **Phase 4:** balance. The multipliers and perk values are first guesses.
4. **Minor, from the agent:**
   - Saved stock perk choices can preselect a perk on our types until the player picks on the screen.
   - A rejected take request leaves a stale "open when commanding" flag.
   - Pending picks are visible to the enemy.
   - The screen doesn't open on automatic placement after joining a side.

## Traps

- A build can find the client DLL "up to date" when an earlier compile check already installed it. Compare it with `obj\Release\net472\` before trusting a timestamp.

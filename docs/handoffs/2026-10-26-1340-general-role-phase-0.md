# Handoff: the general as a pickable role (general role, phase 0)

Session of 2026-10-26, written 13:40. It follows `2026-10-26-1250-two-player-test-verified.md` (same session).

## State

- **Not committed:** all of the work below (36 files), plus `docs/plans/general-role.md`, `docs/plans/README.md` and this handoff.
- **Installed:** both sides; the server module's DLLs and XML hashes match the build.
- **Running:** server pid 10324, map `battle_terrain_biome_063`. Patch line: "94 classes applied, 0 failed, 3 skipped". Round 0 opened, with no exceptions and no "fields no general" line.
- **Restart the client to play.** `SpawnLayoutSeed`, `RequestRoleCommand` and `SideAIStatus` changed on the wire, so an old client doesn't match the new server.

## What was done

The design and the user's decisions are in `docs/plans/general-role.md`, "Phase 0". In short:
- one general per side, mounted and in the culture's best gear, leading 10 mounted elite bodyguards;
- one fixed class per culture;
- an AI general when no player takes the role;
- the general's group comes on top of the 8 formations.

**Where it lives.** The general's group is slot 8, the game's `FormationClass.General`.
- Stock and RBM tactics, regrouping and the order bar only see formations 0–7, so they leave it alone.
- The cost: a player general can't order his bodyguard from the order bar. It follows him instead.

**Code** (written by an Opus agent; I read the core):
- **Settings and roles.**
  - `CommanderBattleSettings.cs`: `FormationCount` (8) now means the regular formations. New `GeneralSlot = 8`, `SlotCount = 9`, `GeneralBodyguardCount = 10`.
  - `CommanderRole.cs`: `CommanderRole.General`, mapped from the class group "General" (the hero's `default_group`). `HasGeneral(culture)`.
  - `FormationSlots.cs`: `IsGeneral`, `IsFielded`, `DisplayName`.
- **Spawning.**
  - `CommanderBattleSpawningBehavior.cs`: `SpawnAiGeneral` spawns the hero 5 m in front of his riders.
  - `CommanderBattleTeams.SetGeneral`/`ClearGeneral` set `Team.GeneralsFormation`/`GeneralAgent`, cleared in `OnClearScene`. `Team.BodyGuardFormation` stays null (stock `TeamAIComponent.Tick` would force a set one to charge).
- **AI.** `CommanderBattleTeamAI.cs` adds `BehaviorGeneral` to formation 8. `LeadAsGeneral` sets the weights as singleplayer does. It runs when an AI general spawns and when a player general's formation goes to the AI.
- **Layout.** `CommanderBattleSpawnLayout.cs` gives the stock planner the general as its own `FormationClass.General` block, mounted, so it stands behind the centre of the line.
- **Server rules.** `MissionMultiplayerCommanderBattle.cs`:
  - joining players are never placed as general automatically;
  - after a take-over (F), the general fields and the bodyguard's Follow order move to the new rider;
  - claim log: "X commands Attacker general (formation 8)".
  - `.Admin.cs` `SwapSides` maps general to general.
- **RBM.** `RBM.Multiplayer/FormationOwnershipGuard.cs`: no agent moves into or out of formation 8. Otherwise RBM's `ManageFormationCounts` would sort the AI general's riders into the cavalry.
- **Stock overflow fixed.** `MissionLobbyComponent._classRestrictions` is a `bool[8]` indexed by class group, and "General" is group 8. `MissionMultiplayerCommanderBattleClient.WidenClassRestrictions()` widens it to 11 by reflection in `AfterStart`, on both sides. Otherwise it overflowed in `SpawnComponent.AllowEarlyAgentVisualsDespawning` when a player general broke spawn invulnerability.
- **UI.**
  - Formation list: the general is the last row.
  - Troop screen: a single-class role goes from step 1 straight to step 3.
  - AI status panel: a "General" kind.
  - Scoreboard and admin panel labels.
- **Text.** `commander_battle_strings.xml`, and the README's new "The general" section, checklist items and limitation.

**Troop data** (written by an Opus agent).
- Each `cb_mpclassdivisions_<culture>.xml` and `cb_mpcharacters_<culture>.xml` gets `cb_general_<culture>` (multiplier 0.10, icon `Special_RoyalCavalry`, one setup), with hero `cb_general_<culture>_hero` and troop `cb_bodyguard_<culture>_troop`, both `default_group="General"`.
- Names: Vlandian Lord / Household Knight, Strategos / Elite Cataphract, Noyan / Torguud, Emir / Ghulam, Voivode / Druzhina Guard, Clan Chieftain / Sworn Rider.
- Every item id was checked against Native's `mpitems.xml`, and lances are the couchable ones.
- Class counts: Vlandia 9, Empire 10, Khuzait 9, Aserai 9, Sturgia 8, Battania 8.

## Verified vs not

- **Verified:**
  - 0 warnings and 0 errors in the build;
  - the server installed and started, with 0 failed patches;
  - the round opened with no exceptions and nothing about the new classes in `rgl_log`;
  - both sides found a general class.
- **Not played.** Nothing is tested in game:
  - the AI general's spawn and position, and whether `BehaviorGeneral` keeps him behind the line;
  - taking the general, the selection screen skipping to step 3, and the preparation visuals;
  - taking over a bodyguard after the general dies;
  - the order bar for a player general (expected to show nothing);
  - whether 9 rows fit the formation list at 1080p (estimated ~945 px);
  - the gear's looks.

## Open items

- **Play it:** the README checklist's general items.
- **Look at three gear choices in game:**
  - the Vlandian general's crowned helmet (armour 39, weaker than his guards' 45–48; `mp_full_helm_over_padded_cloth` is the swap);
  - the Khuzait bodyguard has no bow (that would cost the shield);
  - capes and harnesses that may clip.
- **Commit** after the user's look.
- **Next phases of the plan:** the general steers the team AI (tactic, then stance).
- **Found, not caused:** our layout never calls `SetSpawnWithHorses`, so the stock planner puts the cavalry plan class in the front column, behind the infantry. The README says cavalry deploys on the flanks, which looks unverified. Check in game.

## Traps

- `MissionLobbyComponent._classRestrictions` holds only 8 class groups. A class group past the stock ones (like "General") overflows `IsClassAvailable` unless the array is widened.
- `Team.Reset` doesn't clear `GeneralAgent`, `GeneralsFormation` or `BodyGuardFormation`, so a mode that reuses teams must clear them itself.

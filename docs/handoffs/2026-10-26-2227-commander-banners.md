# Handoff: the commander's banner

Session of 2026-10-26, written 22:27. It builds the plan `docs/plans/commander-banners.md` (now "in progress (built, not played)").

**Other sessions are working in this checkout.** A weather session (`CommanderBattleWeather.cs`, `CommanderBattleSpawnLayout.cs`, `docs/plans/round-weather.md`) edited `MissionMultiplayerCommanderBattle.cs` and `MissionMultiplayerCommanderBattleClient.cs` while I worked. My hooks there are intact, and the tree with both sets of changes compiles (scratch build, 22:27).

## State

- **Not committed.**
- **Installed** on both sides at 22:25 with `tools\Start-Server.ps1 -Build -Restart`. The user approved stopping the running server (pid 21728). The installed server DLL is from my 22:13 compile, so the weather session's later edits are probably not in it.
- **Running:** server pid 23668, on battle_terrain_biome_096. "Patches: 94 classes applied, 0 failed, 3 skipped". No exceptions in the CommanderBattle, RBM or rgl logs. The rgl error log has the same 47 "Cannot load" startup lines as the previous server.
- **Not played.** Nobody has joined, so no round has run on this build.

## The user's decisions

- **Question 2:** only the general's riders carry his banner. AI formations keep the side's banner.
- **Question 3:** accept plain shields where a shield can't take a banner.
- **New:** shades come from the **whole palette**. Only 8 of 229 colours are flagged for backgrounds, all dark, which would have left most sides with one shade or none.

## What was done

See the plan's "What the check found" and "As built", and the README's "The commander's banner" bullet.

**The check, done by reading the source instead of flipping the flag:**
- `AllowCustomPlayerBanners` doesn't affect shields. True would only let clients set an unchecked banner code, so it stays false.
- Banners travel inside each weapon of `CreateAgent`, so the server's build-data banner is what every client paints.
- Two shields our classes carry have no tableau: `mp_strapped_round_shield` and `mp_steel_round_shield`.

**New files:**
- `CommanderBattle/CommanderBanners.cs`: build, shades, sigil checks, profile sigil.
- `CommanderBattle/CommanderBattleClientConfig.cs`: the client's saved file.
- `CommanderBattle/MissionMultiplayerCommanderBattle.Banners.cs`: the server's state and checks, `OnAgentCreated`.
- `CommanderBattle/MissionMultiplayerCommanderBattleClient.Banners.cs`.
- `CommanderBattle/Network/RequestCommanderBanner.cs` and `CommanderBanner.cs`.
- `CommanderBattle.View/CommanderBattleBannerPickerView.cs` and `BannerPickerVM.cs`.
- `Module/GUI/Prefabs/CommanderBattleBannerPicker.xml`.

**Edited files:**
- `CommanderBattleSpawningBehavior.cs`: banners onto the stock spawner's queued build data; the side banner on AI formations and the AI general; formation banner codes.
- `MissionMultiplayerCommanderBattle.cs`: handler, pending picks applied at preparation, sent to joiners, forgotten on disconnect.
- `MissionMultiplayerCommanderBattleClient.cs`: handler, team-change hook, `UpdateFormationBanners` in the tick.
- `FormationSelectionVM.cs` and `CommanderBattleFormationSelectionView.cs`: the Banner button, each commander's banner image, the pending line.
- `CommanderBattleMissionViews.cs`.
- `CommanderBattle.View.csproj`: references to `TaleWorlds.Core.ViewModelCollection` and `TaleWorlds.TwoDimension`.
- `Module/GUI/Brushes/CommanderBattle.xml`: `CommanderBattle.Banner.Cell` and `.Swatch`.
- `CommanderBattleFormationSelection.xml`.
- `README.md`: a rule bullet, a "How it works" row and three checklist lines.
- The plan and the plans index.

## Verified vs not

- **Verified:**
  - Both targets build.
  - The server is installed and runs with clean logs.
  - The prefabs and brushes are well-formed XML.
  - A Python simulation of the shade rule on the real palette gives Vlandia 3 shades and Empire, Aserai, Sturgia and Khuzait 5. That covers the cultures' first colours only, not swapped colours.
- **Not verified (needs play):**
  1. **Shields actually show the commander's banner.** This depends on the `_agentsToBeSpawnedCache` hook. If the hook fails, the log says "Stock spawner has no _agentsToBeSpawnedCache".
  2. Another client sees the same shields.
  3. AI shields show the side banner (before this change they had none).
  4. The picker grid shows sprites (`ui_bannericons` loads), and the banner preview renders.
  5. The formation list row images render.
  6. "Next round" works mid-round.
  7. The file is written and restored after a restart.
  8. A bad sigil in the file is refused.

## Open items and next steps

- Play the three banner lines of the README checklist.
- **If the shields still show the profile banner**, check whether `OnAgentCreated` reaches the game mode during the stock spawner's tick. Turn on `CommanderBattleLog.Detailed`, or add a Detail line in `ApplyCommanderBannersToQueuedAgents`.
- **If a shade looks too close to another**, tune `ShadeHueWindow`, `DistinctValue` and `EnemyDistance` in `CommanderBanners`.
- **Not done:**
  - The kill feed and the scoreboard still show the profile banner. The scoreboard takes it from the peer's `BannerCode`.
  - Clothing doesn't follow the shade. That is still an open idea in the plan.
- **Named formations** (`docs/plans/named-formations.md`) can add a `<FormationName>` element beside `<Banner>` in `CommanderBattleClientConfig`. Saving replaces only its own element.

## Traps

- **`culture.Banner` is a `Banner` object,** not a code. `new Banner(culture.Banner)` is the copy constructor.
- **`new Banner(code, c1, c2)` throws on a malformed or empty code** (an empty layer list). `GetProfileSigil` parses with `TryGetBannerDataFromCode` instead.
- **The stock spawner overwrites build-data banners itself** (`new Banner(Peer.BannerCode, side colours)`). The only window to change them is between its queue and the first weapon being made, which is the first `OnAgentCreated`.
- **`Team.AssignPlayerAsSergeantOfFormation` resets `Formation.BannerCode`** to the profile code. The setter broadcasts `InitializeFormation` on every set, so it is guarded with a check that the code changed.
- **Scratch compile:** `-p:ModuleOutputDir=<scratch>\ -p:DedicatedServerModuleDir=<scratch>\` compiles without installing. Don't also move `BaseIntermediateOutputPath`: the stale `obj\` files then compile as sources.

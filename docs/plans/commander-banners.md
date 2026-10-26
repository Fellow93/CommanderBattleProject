# The commander's banner

**Status:** in progress (built, not played) · **Size:** M

## Goal

A player's formation carries their banner on its shields (a sigil they pick, in a shade of their side's colour, or their multiplayer profile banner until they pick one), and the general's riders carry his. AI formations carry the side's banner. You can tell on the field whose men those are, and a commander's banner becomes part of their identity, like a [formation name](named-formations.md).

## What exists today

- `MissionMultiplayerCommanderBattle.AllowCustomPlayerBanners()` returns false, so the stock code paints the team's banner, not a player's.
- Each side has a culture banner in its colours (`Banner` built from `culture.Banner` and the battle colours, `MissionMultiplayerCommanderBattle`, team setup).
- A player's formation already gets their banner code: `formation.BannerCode = commander.Peer.BannerCode` when the commander's visuals spawn (`CommanderBattleSpawningBehavior`). The client uses it only for the compass icon (`MissionMultiplayerCommanderBattleClient`: `new Banner(formation.BannerCode, team colours)`).
- AI formations get `formation.Banner = team.Banner` (`SpawnAiFormation`).
- The general's riders and the AI general are spawned by `SpawnAiFormation` / `SpawnAiGeneral`; a player general's riders by the stock spawner from his visuals.
- `MissionMultiplayerCommanderBattle.OnAgentBuild(Agent, Banner)` is overridden; it receives the banner the stock code chose.

## Design

Simplest first, stopping at the first step that works well:

1. **Turn the stock path on.** Return true from `AllowCustomPlayerBanners()` and see what the stock code then does: which agents get the peer's banner (the commander, their troops from the visuals, nobody?), on which items, and whether every client paints it the same. In stock modes this setting is what puts players' own banners on shields.
2. **Fill the gaps on the server.** Where step 1 leaves troops on the team banner, pass the commander's banner in the agent build data (the build data takes a banner) for the commander's troops, and the general's for his riders. AI formations keep `team.Banner`.
3. **Colours.** Paint the player's banner in their side's colours, not their profile colours, so the sides stay readable: blue and red shields matter more than the exact banner colours. Same as the compass does now.

**Picking a banner** (the user's decisions):
- **A sigil picker,** not the full banner editor. The stock editor (`BannerEditorVM`) belongs to the campaign UI: its screen is in the SandBox module, which multiplayer doesn't load, and it's a full-screen editor with a 3D character. A grid of the game's banner icons (`BannerManager`), on the formation list or the troop screen, is enough because the colours come from the side. Read `..\RealisticBattleProject\docs\BannerlordGauntletUI.md` before building it.
- **Applies from the next round.** A shield is painted when the agent is built, and `Agent` has no call to repaint it after (only `SetFormationBanner`, the carried banner item). A pick mid-round shows "(next: ...)" in the formation list, as a troop pick does; before the commander's formation has spawned, it applies at once.
- **Saved on the client,** in the same per-player file as the [formation name](named-formations.md) (`Configs\CommanderBattle_<playerId>.xml`), and sent when the player joins a side. With nothing saved, the profile banner (`Peer.BannerCode`) is used.
- **Server check.** The banner goes to every client, so the server parses it and refuses icon or colour ids the game doesn't have, more layers than one sigil, and oversized codes, before passing it on. A bad code could otherwise break shield painting for everyone.

**Shade of the side's colour.** A commander can pick a shade of their side's colour for their banner's background.
- Banner colours are palette ids, not free colours: `Banner.ChangePrimaryColor(uint)` looks the colour up with `BannerManager.GetColorId` and silently does nothing when it isn't in the palette. So shades come from the palette.
- Each machine builds the same list from the palette (`BannerManager.ColorPalette`): the colours with `PlayerCanChooseForBackground` closest in hue to the side's banner background colour, sorted light to dark, up to five. Colours too close to the other side's are left out, for cultures whose colours are near each other.
- The pick is a shade index, checked by range on the server, applied from the next round and saved in the same client file. Index 0 is the side's own colour.
- The sigil keeps the side's second colour, so the two sides still read apart.

**Taking over.** A soldier a dead commander takes over keeps his shield. A formation that goes to the AI mid-round keeps its banner until the next round, then takes the side's.

## Things to work out

- **Do banner-painted shields work on the `mp_` items?** Check first, before anything else: spawn with `AllowCustomPlayerBanners` true and look at every shield type our classes carry (kite, heater, round, pavise, the Khuzait and Aserai shields). Some multiplayer shields may lack a banner mesh and stay plain.
- Where the stock code applies the banner (agent build, the agent visuals, the shield's mesh), and whether it happens on the server and every client alike or is sent. The multiplayer game mode base is in the multiplayer assemblies, which aren't in `decompiled/`: decompile it with `ilspycmd`.
- What `AllowCustomPlayerBanners` changes beyond shields: banners in the scoreboard, the kill feed, the loading screen.
- A peer with no banner code (new account) and no pick: fall back to the side's banner.
- How many usable shades the palette has near each side colour. Some hues may have only two or three.
- Whether the soldiers' clothing should follow the shade too. Clothing colours are free colour values (`ClothingColor`/`ClothingColor2` in the build data, set from the side's colours in `CommanderBattleSpawningBehavior`), so any shade works there, but it changes the whole formation's look, not just the shields.

## Files it touches

- `CommanderBattle/MissionMultiplayerCommanderBattle.cs` (`AllowCustomPlayerBanners`, `OnAgentBuild`).
- `CommanderBattle/CommanderBattleSpawningBehavior.cs` (banner in the build data for the commander's troops and the general's riders).
- New `CommanderBattle/Network/RequestCommanderBanner.cs`, `CommanderBanner.cs` (shared: sigil, shade index), the server's handler and join sync, and the client's saved file (shared with the formation name).
- `CommanderBattle.View`: the sigil and shade picker, and "(next: ...)" in `FormationSelectionVM`.
- `README.md`.

## Open questions for the user

1. Banner colours: the side's colours (readable at a distance) or the player's own (more personal)? **Decided: the side's colours, with a shade pick.**
2. Should the general's whole side carry his banner, or only his riders? **Decided: only his riders.** AI formations keep the side's banner; other players' formations carry their own.
3. If some shields can't take a banner: accept plain shields there, or drop the feature? **Decided: accept plain shields,** and the README names them.

## What the check found (2026-10-26)

The stock path was read in the decompiled sources, client and dedicated server (the multiplayer dlls with `ilspycmd`), instead of flipping the flag in game:
- **`AllowCustomPlayerBanners` doesn't touch shields.** Its only caller that matters is the server's handler for the client's `CreateBanner` message: true lets a client set its banner code, unchecked, and the server passes it to everyone. So it stays false. Step 1 of the design was wrong.
- **The banner travels in the weapons.** `Mission.SpawnAgent` gives the build data's banner to every `MissionWeapon` (`InitializeMissionEquipment`), and `CreateAgent` writes each weapon's banner code. The client builds the agent from those weapons and ignores its own banner choice for them. So a banner set on the server is what every client paints, and no new message is needed for the shields. The shield's tableau is applied on the client only (`ItemCollectionElementViewExtensions.GetTableauMaterial`, for items with `IsUsingTableau`).
- **Players' formations already carried banners.** The stock spawner (`SpawningBehaviorBase.OnTick`) turns preparation visuals into agents and gives the commander and their troops `new Banner(Peer.BannerCode, side colours)`. Our AI formations carried none.
- **Shields.** All 53 `mp_` shields are in Native's `mpitems.xml`. Only `mp_strapped_round_shield` (Sturgian horse raider) and `mp_steel_round_shield` (Aserai general, one Aserai setup) have no `using_tableau`. Whether a tableau shield's mesh shows the banner well is only visible in game.
- **Palette.** Only 8 of 229 colours are flagged `player_can_choose_for_background`, all dark. The user decided to take shades from the whole palette instead. With the main cultures' first colours, Vlandia gets 3 shades, the others 5.

## As built

- **Banner:** `CommanderBanners.Build(side, sigil, shade)`. Its background is the side banner's (culture banner in battle colours, as `CreateTeams` makes the team banner) in the shade's palette colour. The sigil sits at size 512 in the centre, in the side banner's sigil colour. Sigil -1 keeps the side's own icons.
- **Shades:** `CommanderBanners.GetShades`. Index 0 is the side's colour. Then palette colours within 20° of hue and 0.35 of saturation (greys with greys), closest first, skipping near-duplicates and any colour nearer the other side's or within 60 RGB of it. The rest are sorted light to dark, up to 5 in all.
- **Server** (`MissionMultiplayerCommanderBattle.Banners.cs`): a sigil and shade per peer, and a pending pair.
  - `RequestCommanderBanner` is checked: the icon must exist and not be a pattern, the shade in range. A refusal is logged and messaged.
  - Sigil -1 becomes the profile banner's sigil (`GetProfileSigil`: parsed, at most 400 characters, layer 1, checked).
  - A pick applies at once unless the commander spawned this round, else at the next preparation (`ApplyPendingBanners` in `NewSpawnLayout`).
  - `CommanderBanner` is broadcast on change and sent to joiners. The state is forgotten on disconnect.
- **Shields:** `MissionMultiplayerCommanderBattle.OnAgentCreated` calls `CommanderBattleSpawningBehavior.ApplyCommanderBannersToQueuedAgents`.
  - While `base.OnTick` spawns from visuals, the first agent created means the queue is full and no weapon has been made yet.
  - It sets every queued build data's banner from its commander (`AgentMissionPeer ?? OwningAgentMissionPeer`), read from the private `_agentsToBeSpawnedCache` (it logs once if that field goes). It also sets `ControlledFormation.Banner`.
  - AI formations and the AI general get `.Banner(team.Banner)`.
  - `Formation.BannerCode` is set to the commander's banner, or the side's for AI formations, again after `AssignPlayerAsSergeantOfFormation`, which resets it to the profile code.
- **Client** (`MissionMultiplayerCommanderBattleClient.Banners.cs`):
  - It keeps each commander's ids.
  - On joining a side it sends the saved pick, with a shade out of the new side's range sent as 0.
  - Each tick it sets every slot's `Formation.Banner` (commander's or side's) for the compass and the agent labels. The client otherwise builds those from `BannerCode` in team colours and caches the first one.
- **Saved file:** `CommanderBattleClientConfig`, `Configs\CommanderBattle_<playerId>.xml` with `<CommanderBattle><Banners><Banner culture="vlandia" sigil=".." shade=".." /></Banners></CommanderBattle>`.
  - One banner per culture (the user's decision after the first test): joining a side sends the one saved for its culture.
  - The player id is `NetworkMain.GameClient.PlayerID`, as the perk file uses.
  - Saving replaces only the `Banners` element (and drops the first build's root-level `Banner`), so the formation name can be added beside it.
- **Soldiers' label banners (Alt).** The first test showed them still carrying the profile banner.
  - The cause: `Mission.SpawnAgent` writes `CreateAgent` before `Agent.Build` sets the agent's formation, so a new agent's formation index is -1.
  - The client then takes `new Banner(owner's Peer.BannerCode, team colours)`, never the formation's banner.
  - The fix, client only: `UpdateFormationBanners` sets each commander's local `VirtualPlayer.BannerCode` to their commander banner, keeping the original for the picker (`GetProfileBannerCode`). `OnAgentBuild` turns that fresh per-agent banner back into the shaded one before `MissionAgentLabelView` (a view, later in the behaviour list) draws it.
  - Side effect: the scoreboard and kill feed show the commander's banner too.
- **UI:**
  - A "Banner" button on your own row of the formation list.
  - Each commander's banner image beside their name.
  - "Next round: your new banner" in the pending line, for you only.
  - `CommanderBattleBannerPickerView` (layer 26, Esc priority 22) shows `BannerPickerVM` / `CommanderBattleBannerPicker.xml`. It has a preview, shade swatches, "Profile banner", and a 9-column scrolling grid of the available icons that have a `ui_bannericons` sprite (the category is loaded on open and never unloaded).

## Done when

- In game: a player's formation carries their banner on its shields, seen the same by every player; the general's riders carry his; AI formations carry the side's. The sides still read as two colours.
- A player with no banner falls back to the side's banner.
- Picking a sigil and a shade mid-round shows "(next: ...)" and the next round's shields carry them; restarting the game and joining again keeps them.
- A hand-made bad banner code from a client is refused by the server and reaches no other client.
- No new warnings in the rgl log when the armies spawn.

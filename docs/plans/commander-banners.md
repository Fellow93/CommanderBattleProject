# The commander's banner

**Status:** idea · **Size:** M

## Goal

A player's formation carries their multiplayer profile banner on its shields, and the general's riders carry his. AI formations carry the side's banner. You can tell on the field whose men those are, and a commander's banner becomes part of their identity, like a [formation name](named-formations.md).

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

**Taking over.** A soldier a dead commander takes over keeps his shield. A formation that goes to the AI mid-round keeps its banner until the next round, then takes the side's.

## Things to work out

- **Do banner-painted shields work on the `mp_` items?** Check first, before anything else: spawn with `AllowCustomPlayerBanners` true and look at every shield type our classes carry (kite, heater, round, pavise, the Khuzait and Aserai shields). Some multiplayer shields may lack a banner mesh and stay plain.
- Where the stock code applies the banner (agent build, the agent visuals, the shield's mesh), and whether it happens on the server and every client alike or is sent. The multiplayer game mode base is in the multiplayer assemblies, which aren't in `decompiled/`: decompile it with `ilspycmd`.
- What `AllowCustomPlayerBanners` changes beyond shields: banners in the scoreboard, the kill feed, the loading screen.
- A peer with no banner code (new account): fall back to the side's banner.

## Files it touches

- `CommanderBattle/MissionMultiplayerCommanderBattle.cs` (`AllowCustomPlayerBanners`, `OnAgentBuild`).
- `CommanderBattle/CommanderBattleSpawningBehavior.cs` (banner in the build data for the commander's troops and the general's riders).
- `README.md`.

## Open questions for the user

1. Banner colours: the side's colours (readable at a distance) or the player's own (more personal)?
2. Should the general's whole side carry his banner, or only his riders?
3. If some shields can't take a banner: accept plain shields there, or drop the feature?

## Done when

- In game: a player's formation carries their banner on its shields, seen the same by every player; the general's riders carry his; AI formations carry the side's. The sides still read as two colours.
- A player with no banner falls back to the side's banner.
- No new warnings in the rgl log when the armies spawn.

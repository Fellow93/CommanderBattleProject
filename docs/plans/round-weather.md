# Weather and time of day per round

**Status:** idea · **Size:** M

## Goal

Each round is fought in different light and weather: morning, noon, afternoon or dusk, clear, overcast, fog or rain. Two rounds on the same map look and feel different, and fog turns an archer duel into something else. **No night battles:** the latest it gets is dusk.

## What exists today

- The server picks a seed when each round's preparation starts and sends it to every client (`SpawnLayoutSeed`, `MissionMultiplayerCommanderBattle`; again when a count changes, and to players who join). Server and clients derive the spawn layout from it (`CommanderBattleSpawnLayout`).
- Each scene's terrain is known from `sp_battle_scenes.xml` (the readable map names are built from it: "Steppe 7", "Wooded Plains with a lake").
- The engine has the calls (`TaleWorlds.Engine/Scene.cs`): `SetAtmosphereWithName(name)`, `SetRainDensity`, `SetSnowDensity`, `SetFog(density, colour, falloff)`, `SetFogAdvanced`, `SetEnvironmentMultiplier`.
- Singleplayer picks an atmosphere by time of day (`BannerlordMissions.CreateAtmosphereInfoForMission`): 6 → `TOD_06_00_SemiCloudy`, 12 → `TOD_12_00_SemiCloudy`, 15 → `TOD_04_00_SemiCloudy`, 18 → `TOD_03_00_SemiCloudy`, 22 → `TOD_01_00_SemiCloudy`. The 22 one is night and is left out.
- Singleplayer weather has rule effects (`CustomBattleApplyWeatherEffectsModel`): rain or snow slows bow and crossbow missiles to 90%, fog cuts missile range to 80%. Commander Battle doesn't use that model.

## Design

**The pick.** From the round's seed, through its own random stream (seed mixed with a constant), so a change to the layout code never changes the weather. Server and clients get the same answer and need no new message.
- **Time:** morning, noon, afternoon or dusk (the 6, 12, 15 and 18 atmospheres), never the night one.
- **Weather:** clear, overcast, light fog, thick fog, rain. Weights by terrain: little rain on steppe and desert scenes, more fog on forest, lake and river scenes, snow instead of rain on snowy scenes.
- A scene can keep its own atmosphere ("as the map maker made it") as one more outcome, so the original look still comes up.

**Applying it**, on clients only (a dedicated server draws nothing): when the seed arrives and the scene is ready, set the atmosphere, then rain or snow, then fog. Rounds reuse the mission, so it is set again each preparation, and a player who joins gets it with the seed.

**Telling players.** A line when preparation starts: "Dusk, fog over the field." The log records the pick per round.

**Rules stay the same.** Weather is visual only: no missile speed or range changes, unless the user decides otherwise (open question 2).

## Things to work out

- **Which calls work on a multiplayer scene, mid-mission.** Multiplayer opens the scene with its own atmosphere; check that `SetAtmosphereWithName` changes the sky, sun and light after load, and again on the next round. If it only works at scene load, the fallback is fog, rain and `SetEnvironmentMultiplier` on the scene's own atmosphere.
- Which of the `TOD_*` atmospheres the campaign scenes have, and what each looks like. The singleplayer table's names don't match their hours (15 → `TOD_04_00`), so look at each in game rather than trusting the names. Make sure "dusk" still has enough light to fight by.
- Rain: whether `SetRainDensity` alone shows rain, or the scene needs the dynamic rain texture (`CreateDynamicRainTexture`) set up first.
- Fog values that look good from a soldier's eyes and from the general-mode camera, which sits about 100 m up.
- Frame rate on the client with rain and 1,000 agents.

## Files it touches

- New `CommanderBattle/CommanderBattleWeather.cs`: the pick from the seed (shared) and applying it (client).
- `CommanderBattle/MissionMultiplayerCommanderBattleClient.cs`: apply on `SpawnLayoutSeed`.
- `CommanderBattle/MissionMultiplayerCommanderBattle.cs`: log the pick.
- `CommanderBattle/CommanderBattleSettings.cs`: the weights.
- `README.md`: under "Maps".

## Open questions for the user

1. The weights: how often fog and rain should come up (for example 50% clear or overcast, 25% fog, 25% rain)?
2. Weather effects on the rules: keep it visual only, or use singleplayer's effects (rain slows missiles to 90%, fog cuts range to 80%)? Those would need to be applied on the server and every client.
3. Snow on snowy maps, or leave those as they are?
4. An admin option to fix the weather (always clear noon) for tournaments?

## Done when

- In game: different rounds on the same map show different times of day and weather, the same for every player and for a player who joins mid-round. Never night.
- Fog, rain and dusk look right from the soldier's view and from general mode.
- Log: the pick per round ("Round 2 weather: dusk, fog").
- Client frame rate stays playable in rain with both armies on the field.

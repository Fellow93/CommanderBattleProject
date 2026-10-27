# Weather and time of day per round

**Status:** in progress (v1 installed, not played; admin Time of Day / Weather built) · **Size:** M

## Goal

Each round is fought in different light and weather: morning, noon, afternoon or dusk, clear, overcast, fog or rain. Two rounds on the same map look and feel different, and fog turns an archer duel into something else. **No night battles:** the latest it gets is dusk, and dusk still has enough light to fight by.

## What exists today

- The server picks a seed when each round's preparation starts and sends it to every client (`SpawnLayoutSeed`). It is sent:
  - from `MissionMultiplayerCommanderBattle.NewSpawnLayout` when preparation starts;
  - again with the same seed when a troop count changes (`UpdateTroopCounts`, `FreezeTroopCounts`);
  - to players who join (`HandleNewClientAfterSynchronized`, `MissionMultiplayerCommanderBattle.cs:728`).
- The client handles it in `MissionMultiplayerCommanderBattleClient.HandleServerEventSpawnLayoutSeed`. It already reads `mission.Scene` there (the spawn path pick, `CommanderBattleSpawnLayout.SetRound` → `SelectSpawnPath`), so the scene is ready when the message arrives.
- Server and clients derive the spawn layout from the seed with `CommanderBattleSpawnLayout.LayoutRandom`, an xorshift32 that gives the same numbers on net472 and net6. Streams are told apart by the second constructor argument: sides use 0 and 1, the spawn path 2.
- Commander Battle opens its mission with a bare `new MissionInitializerRecord(scene)` (`CommanderBattleMissions.cs:16-20`), as every stock MP mode does (`MultiplayerMissions.OpenBattleMission` etc., decompiled from `Modules\Multiplayer\bin\Win64_Shipping_Client\TaleWorlds.MountAndBlade.Multiplayer.dll`). Its atmosphere is "invalid" (empty name, `MissionInitializerRecord` constructor), so each scene keeps the atmosphere it was saved with.
- Nothing in the multiplayer assemblies touches atmosphere, time of day, rain, snow or fog after load. The only MP atmosphere code is `MultiplayerPracticeMissions`, which Commander Battle doesn't use.

## What the game code shows

### How atmosphere is set at load, and changed later

- **At load, native code applies it.** `Mission.Initialize` passes the whole `MissionInitializerRecord` to the engine (`MBAPI.IMBMission.InitializeMission`, `Mission.cs:1802-1811`). Its `AtmosphereOnCampaign` (an `AtmosphereInfo`) is never read by managed code.
  - Custom battle fills only the atmosphere name and season (`BannerlordMissions.CreateAtmosphereInfoForMission`, `BannerlordMissions.cs:83-106`).
  - Campaign battles fill every value, rain, snow and fog included, from the map weather model (`DefaultMapWeatherModel.GetAtmosphereModel`; `SandBoxMissions.cs:51`).
  - This path only exists at mission load. Commander Battle could use it for round 1 only, so it doesn't help.
- **Mid-mission, the game uses `Scene.SetAtmosphereWithName(name)`.** Evidence that it works on a running battle scene:
  - `Quest5SetPieceBattleMissionController` (NavalDLC story) switches atmosphere four times inside one mission, from `OnMissionTick` at story-phase changes: `TOD_02_00_SemiCloudy` → `TOD_01_00_SemiCloudy` → `TOD_naval_03_00_sunset` → `TOD_naval_05_30_sunset` (lines 1144, 2610, 2655, 3116, 4155).
  - `FloatingFortressSetPieceBattleMissionController.AfterStart` sets `TOD_naval_09_00_Overcast` (line 655).
  - Tableaus do the same on live scenes: `BrightnessDemoTableau`, `ItemTableau`, `MapConversationTableau`.
  - It is the only atmosphere change any shipped code makes on a live scene.
- **Nothing in the game calls `SetRainDensity`, `SetSnowDensity`, `SetFog`, `SetFogAdvanced`, `SetEnvironmentMultiplier`, `SetSun*` or `SetSkyBrightness`.** They exist only as wrappers (`TaleWorlds.Engine/Scene.cs:783-885`). Rain, snow and fog in battles always come from the atmosphere, either a named preset or the campaign's `AtmosphereInfo`. Managed code only reads them back (`GetRainDensity`, `GetSnowDensity`, `GetFog`).
- **`CreateDynamicRainTexture` is for the campaign map only.** Its one caller is `MapScene.SetSnowAndRainDataWithDimension` (`SandBox/MapScene.cs:206-214`). Campaign battles get rain from `RainInfo.Density` alone, so a battle scene doesn't need it.
- **`Mission.ResetMission` doesn't touch the atmosphere** (`Mission.cs:1752-1791`), and `MultiplayerRoundController.BeginNewRound` calls it every round. A change stays until it is set again, so each round must set its own atmosphere.
- **Signatures:**
  - `SetAtmosphereWithName(string name)`
  - `SetRainDensity(float)`, `SetSnowDensity(float)`
  - `SetFog(float fogDensity, ref Vec3 fogColor, float fogFalloff)`
  - `SetFogAdvanced(float fogFalloffOffset, float fogFalloffMinFog, float fogFalloffStartDist)`
  - `SetEnvironmentMultiplier(bool useMultiplier, float multiplier)`
  - `TimeOfDay { get; set; }`
- There is no getter for a scene's atmosphere name.

### The atmosphere presets and their real hours

- **The files.** Plain XML in `Modules\Native\Atmospheres\`, one per preset: 25 `TOD_NN_*`, 24 `TOD_photo_*` (photo mode), plus `scene_atmosphere`, `default_*` and the conversation ones.
  - The campaign's 24-hour curves are in `Native\Atmospheres\Interpolated\`.
  - NavalDLC has its own `TOD_naval_*` in `Modules\NavalDLC\Atmospheres\`. Commander Battle doesn't load that module, so they are out.
- **`TOD_NN` is the morning clock hour, and the curve is symmetric around noon,** so the same preset is also hour 24 − NN.
  - Proof: `Interpolated\semicloudy_field_battle.xml`, the campaign's own field-battle day, has the same sun altitude, fog falloff, exposure and sky brightness:
    - at 15:00 as `TOD_09`;
    - at 18:00 as `TOD_06`;
    - at 19:00 as `TOD_05`;
    - at 20:00 as `TOD_04`.
  - The `time_of_day` value inside a file is often wrong (`TOD_07`, `TOD_09` and `TOD_06_00_Overcast` all say 8.0). Trust the sun values.
- **The singleplayer table is wrong after noon.** Its hour 15 is `TOD_04`, the 20:00 look. Its hour 18 is `TOD_03`, about 21:00 and nearly black. Hours 6 and 12 are right.
- **The presets that matter**, from `Native\Atmospheres\*.xml`:

| Preset | Real look | Sun altitude / intensity / sky | Fog density, falloff | Rain | Use |
|---|---|---|---|---|---|
| `TOD_01`, `TOD_02`, `TOD_01_00_HeavyRain` | night (moon, sun_rotation 270) | int 0.5–2 | 1.0 | 0 | **never** |
| `TOD_03_00_SemiCloudy` | ~21:00, nearly black | 1.5° / 8 / 2 | 1.0, 0.11 | 0 | **never** |
| `TOD_04_00_SemiCloudy` | ~20:00, last light | 5° / 275 / 10 | 1.0, 0.19 | 0 | too dark? check in game |
| `TOD_05_00_SemiCloudy` | ~19:00, sunset | 9° / 601 / 37 | 1.0, 0.30 | 0 | **dusk** |
| `TOD_06_00_SemiCloudy` | 06:00 / 18:00, low gold sun | 13.8° / 1026 / 120 | 1.0, 0.36 | 0 | **morning** |
| `TOD_09_00_SemiCloudy` | 09:00 / 15:00 | 45.9° / 3153 / 664 | 1.0, 0.415 | 0 | **afternoon** |
| `TOD_12_00_SemiCloudy` | noon | 82° / 8000 / 1000 | 1.0, 0.423 | 0 | **noon** |
| `TOD_06_00_Overcast` | grey morning | 6.5° / 150 / 100 | 1.0, 0.156 | 0 | overcast, morning |
| `TOD_08_00_Overcast` | grey day | 20° / 665 / 217 | 1.0, 0.156 | 0 | overcast, day |
| `TOD_12_00_Overcast` | grey noon | 90° / 700 / 500 | 2.0, 0.176 | 0 | overcast, noon |
| `TOD_06_00_Foggy` | misty morning | 12° / 300 / 200 | **3.0**, 0.34, grey | 0 | fog |
| `TOD_12_00_HeavyRain` | rain at noon (same light as Overcast) | 90° / 700 / 500 | 2.0, 0.176 | **1.0** | rain |
| `TOD_photo_09_00_rain` | rain, darker | 8 h / 200 / 150 | 5.0, 0.21 | **1.0** | rain (photo preset) |

- **No preset has snow** (`snow_density` is 0 in every file). The interpolated `dense_snowy` / `semi_snowy` curves carry none either; the campaign's snow comes from `SnowInfo.Density`, which `DefaultMapWeatherModel` computes (0.55–0.85 for Snowy and Blizzard). Snow mid-mission can only come from `SetSnowDensity`.
- **What the scenes use now.**
  - Most campaign field-battle scenes save `scene_atmosphere` (`SandBoxCore\SceneObj\battle_terrain_*\scene.xscene`): sun 30°, intensity 11139, daylight.
  - A few save a `TOD_*` preset: `_e` and `_016` use `TOD_09`, `biome_076` uses `TOD_06`, `biome_030` and `093b` use `TOD_12`.
  - Each scene's `atmosphere.xml` snapshot varies widely; some are night-dark (`battle_terrain_e`: intensity 1.5).
  - The MP scenes (`Native\SceneObj\mp_*`) also use `scene_atmosphere`.

### Rule effects

- **Singleplayer's weather rules come from two places,** both reading the live scene:
  - `ApplyWeatherEffectsModel.ApplyWeatherEffects()` sets three mission-wide engine modifiers (`CustomBattleApplyWeatherEffectsModel.cs:9-23`; the Sandbox one is identical):
    - bow and crossbow missile speed ×0.9 if rain or snow > 0;
    - missile range ×0.8 if fog > 0.
  - It runs once, in `Mission.AfterStart` (`Mission.cs:3840`), never per round.
  - The agent stat models also slow agents by 10% in rain and raise the AI shooter error with rain and fog (`AgentStatCalculateModel.GetEnvironmentSpeedFactor`; `CustomBattleAgentStatCalculateModel.cs:323, 346-360`).
- **Multiplayer has neither.** The MP assemblies register no `ApplyWeatherEffectsModel`, so `MissionGameModels.Current.ApplyWeatherEffectsModel` is null and the call is skipped. Their stat model never reads rain, fog or the hour.
- **RBM switches the missile part off in singleplayer** (`RealisticBattleAiModule\AiModule\Agents\Weather.cs`: a prefix sets all three modifiers to 1). The only other RBM reader is its horse stats in RBM Combat (`HorseChanges.MountStats.cs:38, 149, 153`), which Commander Battle has off.
- **So the weather changes no rule in Commander Battle today,** whatever the scene shows.
- **AI bots don't see weather.** Their targeting ignores fog, so in thick fog AI archers keep shooting at full range while players see little. The 0.8 range modifier is the only thing that would narrow that gap.

### Where it must run

- **Visual only: clients only.** The dedicated server draws nothing. Its `TaleWorlds.Engine.Scene` has the same methods (net6 dll, checked with `ilspycmd`). They would only matter on the server for gameplay reads (`IsDayTime`, `GetRainDensity`), and nothing in Commander Battle or the RBM parts it uses reads them.
- **The server still computes the pick,** a pure function of the seed with no scene calls, to log it and to name it in the round's chat line.
- **With rule effects, the missile modifiers must be set on the server,** which simulates the missiles, and on every client, for prediction. They must also be set again each round, since the stock call runs only once.

## Design

**The pick.** Its own stream, `LayoutRandom(seed, 3)`, so the layout code and the weather never shift each other. `LayoutRandom` is private to `CommanderBattleSpawnLayout`: make it `internal` (a one-word change) rather than copying the formula. Server and clients get the same answer and need no new message.
- **Time:** morning `TOD_06`, afternoon `TOD_09`, noon `TOD_12`, dusk `TOD_05`. Never `TOD_01`–`TOD_03`. `TOD_04` only if it passes the in-game light check.
- **Weather:** clear, overcast, light fog, thick fog, rain. Each outcome is an atmosphere name plus optional overrides:

| Outcome | Atmosphere | Then |
|---|---|---|
| clear | the time's `SemiCloudy` | – |
| overcast | `TOD_06_00_Overcast` (morning, dusk), `TOD_08_00_Overcast` (afternoon), `TOD_12_00_Overcast` (noon) | – |
| light fog | the time's `SemiCloudy` | `SetFog` density ~2.0, falloff ~0.25, grey-blue |
| thick fog | `TOD_06_00_Foggy` (morning), else the time's `Overcast` | `SetFog` density ~3.5, falloff ~0.18, grey |
| rain | `TOD_12_00_HeavyRain` (noon, afternoon), `TOD_06_00_Overcast` + `SetRainDensity(0.85)` (morning, dusk) | – |

  - The fog numbers are starting points from the presets (`TOD_06_00_Foggy` is 3.0 / 0.34; `TOD_12_00_HeavyRain` is 2.0 / 0.176; the photo fog is 5.0).
  - A **lower falloff** keeps the fog thicker higher up, which is what hides the field from the general-mode camera 100 m up.
  - Tune them in game.
- **Terrain weights** (dropped in v1, see "As built": the dedicated server can't read `sp_battle_scenes.xml`):
  - from the scene's terrain in that file;
  - little rain on steppe and desert;
  - more fog on forest, lake and river.
- **Snow** dropped too (decision 3).
- **No "as the map maker made it" outcome.** There is no getter for the scene's atmosphere name, and once a round changes it, the original can't be restored. `SetAtmosphereWithName("scene_atmosphere")` loads the global preset, not the scene's own snapshot (check in game). Some scenes' own snapshots are also night-dark (`battle_terrain_e`), which would break the no-night rule. Every round sets an atmosphere, round 1 included.

**Applying it, on clients only,** in `HandleServerEventSpawnLayoutSeed`:
1. `SetAtmosphereWithName`.
2. `SetRainDensity` or `SetSnowDensity`, if any.
3. `SetFog`, if any.

- The overrides go after the atmosphere, which brings its own rain and fog values.
- Remember the last applied seed: the message comes again on every count change during preparation, and the atmosphere shouldn't be re-set then.
- Reset that memory in `OnClearScene`, so a new round always applies, even with the same seed.
- A player who joins gets the seed and so the weather.
- The change lands at preparation start, while armies are not yet on the field, so a visible pop doesn't matter.

**Telling players.**
- A line when preparation starts: "Dusk, fog over the field."
- The server log records the pick per round ("Round 2 weather: dusk, thick fog").

**Rules stay the same,** unless the user decides otherwise (open question 2). Commander Battle has no weather model, so nothing needs switching off.

## Things to work out (only in game)

Everything below is settled by the code except what only a running client can show. Check it with a client on a local server, two or three rounds on one campaign map:

1. **`SetAtmosphereWithName` on an MP client mid-mission.** Sky, sun, light and shadows change at preparation start and again next round. Expected to work, since the NavalDLC story missions do the same on a running battle scene. If a name doesn't resolve, the scene keeps its look; log it with `Detail`.
2. **Rain.** Does `TOD_12_00_HeavyRain` (or `SetRainDensity` after an overcast preset) actually draw rain particles on a scene loaded without rain? Campaign battles only ever get rain at load. If it doesn't show, rain is dropped, or applied only at round 1 of a map, which doesn't fit a per-round pick.
3. **Fog.** Do `SetFog` values stick after `SetAtmosphereWithName`, or does the atmosphere's own fog win over them? Does `SetFog` take the same units as the files' `fog_density` / `fog_falloff`? If not, the round after a fog round looks wrong too, since it sets the preset's file values. Tune the density and falloff from a soldier's eyes and from general mode (Ctrl+C, ~100 m up).
4. **Dusk light.** Is `TOD_05` bright enough to tell friend from foe at 50 m? Is `TOD_04` too dark? The user decides.
5. **Snow** (a follow-up): what do `SetForcedSnow(true)` and `SetSnowDensity` draw on a summer scene?
6. **`scene_atmosphere`.** Out of interest: does setting it bring back the map's own look or the global preset?
7. **Frame rate** on the client in rain with both armies on the field (~900 agents).

## Files it touches

- New `CommanderBattle/CommanderBattleWeather.cs` (shared):
  - the outcomes table, the weights and the pick from the seed;
  - the readable line;
  - `Apply(mission, seed)`, which never runs on a dedicated server.
- `CommanderBattle/CommanderBattleSpawnLayout.cs`: `LayoutRandom` becomes `internal`.
- `CommanderBattle/MissionMultiplayerCommanderBattleClient.cs`: call `CommanderBattleWeather.Apply` in `HandleServerEventSpawnLayoutSeed`, with the last-seed check and its reset in `OnClearScene`.
- `CommanderBattle/MissionMultiplayerCommanderBattle.cs`: log the pick at preparation start, and send the chat line the same way the other round messages go.
- With rule effects (open question 2): set `Mission.SetBowMissileSpeedModifier`, `SetCrossbowMissileSpeedModifier` and `SetMissileRangeModifier` each round on server and clients, in the same place.
- With an admin fixed-weather option (open question 4): a new message (the clients must know), a server setting, and an entry in the admin panel (`MissionMultiplayerCommanderBattle.Admin.cs`, `Network/AdminCommand.cs`).
- `README.md`: a rule under "Maps", a checklist line.

## The user's decisions (2026-10-26)

1. **Weights: rare weather.** Clear 70%, overcast 15%, fog 10% (light 6, thick 4), rain 5%. The four times come up equally often.
2. **Visual only.** No rule effects.
3. **Snow: try it, drop it if broken.** Dropped before building, for a data reason:
   - `sp_battle_scenes.xml` marks no scene snowy (its terrains are Plain, Steppe, Desert and Swamp).
   - The campaign's snow comes from winter (`TimeInfo.Season`, `WinterTimeFactor` in the load-time `AtmosphereInfo`), not from the scene.
   - Falling snow from `SetSnowDensity` would land on summer grass.
   - Snow would need a test of `Scene.SetForcedSnow(bool)` (no caller in the game; it may be the snow ground cover) first.
4. **Admin fixed weather: later,** as a follow-up. It needs its own message. Built on 2026-10-27, see "As built (admin)". The user asked for both the time of day and the weather, each with a random choice that is the weighted draw. A new map reverts to random.

## As built (v1)

- **`CommanderBattle/CommanderBattleWeather.cs`** (new, shared):
  - `GetPick(seed)` draws from `CommanderBattleSpawnLayout.LayoutRandom(seed, 3)`, now `internal`.
  - `GetAtmosphere(pick)` gives the preset by the table in "Design".
  - `Apply(mission, seed)` runs on any machine but the dedicated server. It sets the atmosphere, sets the rain every round (0 unless raining; 1.0 for `TOD_12_00_HeavyRain`, 0.85 laid over `TOD_06_00_Overcast` at morning and dusk), then the fog.
  - `Describe` / `GetLine` give the log and chat words.
- **Changes from the design:**
  - **No terrain weights.** The dedicated server has no SandBox module, so no `sp_battle_scenes.xml`, and a pick weighted by terrain on clients only would make the server log disagree. With rain at 5%, it matters little.
  - **The weights are constants in `CommanderBattleWeather`,** not `CommanderBattleSettings`.
  - **Dusk overcast and dusk rain** use `TOD_06_00_Overcast`. `TOD_06_30_Cloudy_sunset2` would suit them, but its internal name differs from its file name (`TOD_06_30_cloudy_sunset2`), so which one `SetAtmosphereWithName` matches is a guess.
  - **Thick fog in the morning** uses `TOD_06_00_Foggy` with the same override as the other thick fogs (3.5, falloff 0.2). The fog colour is always the preset's own.
  - **Fog never outlasts its round.** Whether `SetAtmosphereWithName` replaces fog set by hand is engine code. So the round after a fog round sets the new preset's own fog values explicitly, copied from its file into `CommanderBattleWeather.Presets`. Clear rounds after clear rounds never call `SetFog`.
- **Client:** `MissionMultiplayerCommanderBattleClient.HandleServerEventSpawnLayoutSeed` applies when the seed differs from `_weatherSeed`, and shows the chat line. `OnClearScene` resets `_weatherSeed`.
- **Server:** `MissionMultiplayerCommanderBattle.NewSpawnLayout` logs "Round N weather: ..." (a `Write`, not a `Detail`), and calls `Apply`, which returns at once on a dedicated server. A host who plays sees the weather too.
- The detailed log has "Weather applied: ..." with the preset, rain and fog on each client.

## Six times, weighted to midday (2026-10-27)

- **The user asked** for the hours around noon to come up more often, with a later morning and an earlier afternoon.
- **`RoundTime`** is in the day's order:

| Time | Preset | Weight |
|---|---|---|
| Morning | `TOD_06` | 10 |
| Late morning | `TOD_11` | 20 |
| Noon | `TOD_12` | 25 |
| Early afternoon | `TOD_10` (14:00) | 20 |
| Afternoon | `TOD_09` (15:00) | 15 |
| Dusk | `TOD_05` (19:00) | 10 |

- **Late morning is 11:00,** not 10:00: 10:00 would share `TOD_10` with 14:00 and look the same.
- **Overcast:** late morning, noon and early afternoon take `TOD_12_00_Overcast`, since only three overcast presets exist.
- **Rain:** these times take `TOD_12_00_HeavyRain`.

## As built (admin)

- **Admin panel, Commander Battle group:**
  - **Time of Day:** Random (weighted), Morning, Late morning, Noon, Early afternoon, Afternoon, Dusk.
  - **Weather:** Random (weighted), Clear, Overcast, Light fog, Thick fog, Rain.
  - Apply Changes sends `RequestWeatherOverride` if either changed (`CommanderBattleAdminPanel.SendOptions`).
- **Server** (`MissionMultiplayerCommanderBattle.Admin.cs`, `SetWeatherOverrideFrom`):
  - checks the admin (`CheckAdmin`, shared with the other commands);
  - sets `CommanderBattleWeather.ForcedTime` / `ForcedWeather`;
  - logs the setting and the round's new pick, broadcasts `WeatherOverride` and announces it.
  - Joining players get `WeatherOverride` before their `SpawnLayoutSeed`.
- **The pick:** `GetPick` draws both parts from the seed every time, then puts the fixed parts in. Fixing the time never changes the drawn weather, and Random is the same weighted draw as before.
- **Client:** `UpdateWeather` reapplies the look whenever the round's pick changes, from a new seed or a `WeatherOverride`. So an admin's change shows at once, mid-round too.
- **A new map reverts to random.** `MissionMultiplayerCommanderBattleClient.OnBehaviorInitialize` resets the setting on every machine, the server included, since that behaviour runs there too.

## Follow-ups

- Snow, after testing `SetForcedSnow` (decision 3).
- Terrain weights, if the server ever has the scene list: ship the terrain per scene in our own module data, readable on both sides.

## Done when

- In game: different rounds on the same map show different times of day and weather, the same for every player and for a player who joins mid-round. Never night.
- Fog, rain and dusk look right from the soldier's view and from general mode.
- Log: the pick per round ("Round 2 weather: dusk, fog").
- Client frame rate stays playable in rain with both armies on the field.

# Weather: summer presets only, dimmer rain (2026-10-27 14:32)

## What was done

- **Grass and trees vanished** in every weather but clear (tested at dusk). The overcast, rain and thick fog presets used there (`TOD_06_00_Overcast`, also `TOD_12_00_Overcast` and the photo rain storms) have `season` fall, and the campaign scenes' flora is summer.
- **Shiny wet ground** in rain: `TOD_12_00_HeavyRain` (the only summer rain preset) has the full noon overcast sun and sky.
- `CommanderBattle/CommanderBattleWeather.cs` now uses summer presets only:
  - overcast: `TOD_08_00_Overcast` at every time;
  - thick fog: `TOD_06_00_Foggy` in the morning, else `TOD_08_00_Overcast`;
  - rain: `TOD_photo_11_00_overcast` + rain 1.0 at midday, `TOD_08_00_Overcast` + 0.85 otherwise.
- `docs/plans/round-weather.md`: "Summer presets only".
- Client installed. The server copy failed (locked by server pid 13288); the weather is client-applied, so it only matters for the server's detail log. Restart the server to install it.
- The user checked in game: grass and trees stay in every weather. Committed.

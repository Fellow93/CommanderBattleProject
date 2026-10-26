using System;
using System.Collections.Generic;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace CommanderBattle
{
    // Each round's time of day and weather, drawn from the round's layout seed (SpawnLayoutSeed) through a stream of
    // its own, so the server and every client get the same pick with no message of its own, and a change to the layout
    // code never changes the weather. The pick depends on the seed alone: the dedicated server has no SandBox module,
    // so it can't read the scenes' terrain (sp_battle_scenes.xml) to weight it.
    //
    // Visual only. Multiplayer registers no ApplyWeatherEffectsModel and its stat model never reads rain, fog or the
    // hour, so the rules are the same in any weather. Only clients apply it; the dedicated server draws nothing and
    // only logs the pick.
    //
    // The looks are the engine's named atmosphere presets (Modules\Native\Atmospheres\*.xml), which bring their own
    // sun, sky, exposure, fog and rain. TOD_NN is the morning clock hour and the day is symmetric around noon, so
    // TOD_05 is also 19:00 (the campaign's own field-battle day, Interpolated\semicloudy_field_battle.xml, matches it
    // there). Never TOD_01 to TOD_04: night or too dark to fight by. SetAtmosphereWithName is how the game itself
    // changes a running battle scene (NavalDLC's Quest5SetPieceBattleMissionController), and ResetMission never
    // touches the atmosphere, so each round sets its own.
    public static class CommanderBattleWeather
    {
        public enum RoundTime
        {
            Morning,
            Afternoon,
            Noon,
            Dusk
        }

        public enum RoundWeather
        {
            Clear,
            Overcast,
            LightFog,
            ThickFog,
            Rain
        }

        public struct Pick
        {
            public RoundTime Time;

            public RoundWeather Weather;
        }

        private const int RandomStream = 3;

        // Out of 100, by RoundWeather: fair weather most rounds (the user's choice).
        private static readonly int[] WeatherWeights = { 70, 15, 6, 4, 5 };

        // Fog laid over the preset's own; a lower falloff keeps it thick higher up, so it hides the field from the
        // general's camera too. Starting values, between TOD_12_00_HeavyRain (2.0, 0.176) and the photo-mode fog (5.0).
        private const float LightFogDensity = 2f;

        private const float LightFogFalloff = 0.25f;

        private const float ThickFogDensity = 3.5f;

        private const float ThickFogFalloff = 0.2f;

        // TOD_12_00_HeavyRain rains at 1.0; the morning and dusk rain is laid over an overcast preset.
        private const float RainDensity = 0.85f;

        private struct Preset
        {
            public float FogDensity;

            public Vec3 FogColor;

            public float FogFalloff;

            public Preset(float fogDensity, Vec3 fogColor, float fogFalloff)
            {
                FogDensity = fogDensity;
                FogColor = fogColor;
                FogFalloff = fogFalloff;
            }
        }

        // Each preset's own fog, from its file, to put back after a round that laid fog over another.
        private static readonly Dictionary<string, Preset> Presets = new Dictionary<string, Preset>
        {
            ["TOD_06_00_SemiCloudy"] = new Preset(1f, new Vec3(0.557f, 0.762f, 0.890f), 0.360f),
            ["TOD_09_00_SemiCloudy"] = new Preset(1f, new Vec3(0.589f, 0.837f, 1f), 0.415f),
            ["TOD_12_00_SemiCloudy"] = new Preset(1f, new Vec3(0.502f, 0.785f, 1f), 0.423f),
            ["TOD_05_00_SemiCloudy"] = new Preset(1f, new Vec3(0.461f, 0.650f, 0.780f), 0.300f),
            ["TOD_06_00_Overcast"] = new Preset(1f, new Vec3(0.620f, 0.856f, 1f), 0.156f),
            ["TOD_08_00_Overcast"] = new Preset(1f, new Vec3(0.620f, 0.856f, 1f), 0.156f),
            ["TOD_12_00_Overcast"] = new Preset(2f, new Vec3(0.789f, 0.941f, 1f), 0.176f),
            ["TOD_06_00_Foggy"] = new Preset(3f, new Vec3(0.235f, 0.235f, 0.235f), 0.340f),
            ["TOD_12_00_HeavyRain"] = new Preset(2f, new Vec3(0.789f, 0.941f, 1f), 0.176f)
        };

        // The mission whose scene last had fog laid over its preset. Whether SetAtmosphereWithName replaces fog set by
        // hand is engine code, so the next round puts the preset's own back itself.
        private static Mission _fogChangedMission;

        public static Pick GetPick(int seed)
        {
            CommanderBattleSpawnLayout.LayoutRandom random = new CommanderBattleSpawnLayout.LayoutRandom(seed, RandomStream);
            Pick pick = new Pick { Time = (RoundTime)random.Next(4) };
            int total = 0;
            foreach (int weight in WeatherWeights)
            {
                total += weight;
            }
            int roll = random.Next(total);
            for (int i = 0; i < WeatherWeights.Length; i++)
            {
                roll -= WeatherWeights[i];
                if (roll < 0)
                {
                    pick.Weather = (RoundWeather)i;
                    break;
                }
            }
            return pick;
        }

        private static string GetClearAtmosphere(RoundTime time)
        {
            switch (time)
            {
                case RoundTime.Morning:
                    return "TOD_06_00_SemiCloudy";
                case RoundTime.Afternoon:
                    return "TOD_09_00_SemiCloudy";
                case RoundTime.Noon:
                    return "TOD_12_00_SemiCloudy";
                default:
                    return "TOD_05_00_SemiCloudy";
            }
        }

        private static string GetOvercastAtmosphere(RoundTime time)
        {
            switch (time)
            {
                case RoundTime.Afternoon:
                    return "TOD_08_00_Overcast";
                case RoundTime.Noon:
                    return "TOD_12_00_Overcast";
                default:
                    return "TOD_06_00_Overcast";
            }
        }

        public static string GetAtmosphere(Pick pick)
        {
            switch (pick.Weather)
            {
                case RoundWeather.Overcast:
                    return GetOvercastAtmosphere(pick.Time);
                case RoundWeather.ThickFog:
                    return pick.Time == RoundTime.Morning ? "TOD_06_00_Foggy" : GetOvercastAtmosphere(pick.Time);
                case RoundWeather.Rain:
                    return pick.Time == RoundTime.Morning || pick.Time == RoundTime.Dusk ? "TOD_06_00_Overcast" : "TOD_12_00_HeavyRain";
                default:
                    return GetClearAtmosphere(pick.Time);
            }
        }

        // Sets the round's look on this machine's scene. Seed 0 (no round set up yet) leaves the scene as it is.
        public static void Apply(Mission mission, int seed)
        {
            if (seed == 0 || GameNetwork.IsDedicatedServer || mission?.Scene == null)
            {
                return;
            }
            Pick pick = GetPick(seed);
            string atmosphere = GetAtmosphere(pick);
            try
            {
                Scene scene = mission.Scene;
                // The overrides come after the atmosphere, which brings its own rain and fog.
                scene.SetAtmosphereWithName(atmosphere);
                // Set every round, so the last round's rain never carries over. No preset here has snow.
                float rain = pick.Weather != RoundWeather.Rain ? 0f : atmosphere == "TOD_12_00_HeavyRain" ? 1f : RainDensity;
                scene.SetRainDensity(rain);
                Preset preset = Presets[atmosphere];
                float fogDensity = preset.FogDensity;
                float fogFalloff = preset.FogFalloff;
                if (pick.Weather == RoundWeather.LightFog)
                {
                    fogDensity = LightFogDensity;
                    fogFalloff = LightFogFalloff;
                }
                else if (pick.Weather == RoundWeather.ThickFog)
                {
                    fogDensity = ThickFogDensity;
                    fogFalloff = ThickFogFalloff;
                }
                bool fogLaidOver = pick.Weather == RoundWeather.LightFog || pick.Weather == RoundWeather.ThickFog;
                if (fogLaidOver || _fogChangedMission == mission)
                {
                    Vec3 fogColor = preset.FogColor;
                    scene.SetFog(fogDensity, ref fogColor, fogFalloff);
                    _fogChangedMission = fogLaidOver ? mission : null;
                }
                CommanderBattleLog.Detail($"Weather applied: {Describe(pick)} ({atmosphere}, rain {rain:0.00}, fog {fogDensity:0.0}/{fogFalloff:0.00}, seed {seed})");
            }
            catch (Exception e)
            {
                CommanderBattleLog.Write($"Weather not applied ({atmosphere}): {e}");
            }
        }

        // For the log: "dusk, thick fog".
        public static string Describe(Pick pick)
        {
            return $"{GetTimeName(pick.Time).ToLowerInvariant()}, {GetWeatherName(pick.Weather)}";
        }

        // What players read as preparation starts: "Dusk, thick fog over the field."
        public static string GetLine(Pick pick)
        {
            return $"{GetTimeName(pick.Time)}, {GetWeatherLine(pick.Weather)}.";
        }

        private static string GetTimeName(RoundTime time)
        {
            switch (time)
            {
                case RoundTime.Morning:
                    return "Morning";
                case RoundTime.Afternoon:
                    return "Afternoon";
                case RoundTime.Noon:
                    return "Noon";
                default:
                    return "Dusk";
            }
        }

        private static string GetWeatherName(RoundWeather weather)
        {
            switch (weather)
            {
                case RoundWeather.Overcast:
                    return "overcast";
                case RoundWeather.LightFog:
                    return "light fog";
                case RoundWeather.ThickFog:
                    return "thick fog";
                case RoundWeather.Rain:
                    return "rain";
                default:
                    return "clear";
            }
        }

        private static string GetWeatherLine(RoundWeather weather)
        {
            switch (weather)
            {
                case RoundWeather.Overcast:
                    return "overcast";
                case RoundWeather.LightFog:
                    return "mist over the field";
                case RoundWeather.ThickFog:
                    return "thick fog over the field";
                case RoundWeather.Rain:
                    return "rain";
                default:
                    return "clear skies";
            }
        }
    }
}

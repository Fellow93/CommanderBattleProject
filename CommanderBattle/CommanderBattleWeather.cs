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
    // so it can't read the scenes' terrain (sp_battle_scenes.xml) to weight it. An admin can fix the time, the weather
    // or both from the admin panel (ForcedTime, ForcedWeather); the server sends that setting (WeatherOverride).
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
        // In the day's order, as the admin panel lists them.
        public enum RoundTime
        {
            Morning,
            LateMorning,
            Noon,
            EarlyAfternoon,
            Afternoon,
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

        // Out of 100, by RoundTime: the hours around noon most rounds (the user's choice).
        private static readonly int[] TimeWeights = { 10, 20, 25, 20, 15, 10 };

        // Out of 100, by RoundWeather: fair weather most rounds (the user's choice).
        private static readonly int[] WeatherWeights = { 70, 15, 6, 4, 5 };

        // Fog laid over the preset's own; a lower falloff keeps it thick higher up, so it hides the field from the
        // general's camera too. Starting values, between TOD_12_00_HeavyRain (2.0, 0.176) and the photo-mode fog (5.0).
        private const float LightFogDensity = 2f;

        private const float LightFogFalloff = 0.25f;

        private const float ThickFogDensity = 3.5f;

        private const float ThickFogFalloff = 0.2f;

        // Rain laid over the overcast preset in the morning, afternoon and dusk; midday rain is laid on at 1.0.
        private const float RainDensity = 0.85f;

        // Every preset here must be a summer one. A preset's season is applied to the running scene, and the campaign
        // scenes' grass and trees are summer flora: a fall preset (TOD_06_00_Overcast, TOD_12_00_Overcast, every rain
        // preset but TOD_12_00_HeavyRain) took them all away. TOD_08_00_Overcast is the only summer overcast.
        private const string OvercastAtmosphere = "TOD_08_00_Overcast";

        // Not TOD_12_00_HeavyRain (summer, but the noon overcast's full sun and sky, which made the wet ground shine).
        // Rain is laid over this dim photo-mode overcast instead (sun 50, sky 200).
        private const string MiddayRainAtmosphere = "TOD_photo_11_00_overcast";

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
            ["TOD_10_00_SemiCloudy"] = new Preset(1f, new Vec3(0.557f, 0.818f, 1f), 0.420f),
            ["TOD_11_00_SemiCloudy"] = new Preset(1f, new Vec3(0.525f, 0.799f, 1f), 0.422f),
            ["TOD_09_00_SemiCloudy"] = new Preset(1f, new Vec3(0.589f, 0.837f, 1f), 0.415f),
            ["TOD_12_00_SemiCloudy"] = new Preset(1f, new Vec3(0.502f, 0.785f, 1f), 0.423f),
            ["TOD_05_00_SemiCloudy"] = new Preset(1f, new Vec3(0.461f, 0.650f, 0.780f), 0.300f),
            [OvercastAtmosphere] = new Preset(1f, new Vec3(0.620f, 0.856f, 1f), 0.156f),
            ["TOD_06_00_Foggy"] = new Preset(3f, new Vec3(0.235f, 0.235f, 0.235f), 0.340f),
            [MiddayRainAtmosphere] = new Preset(3f, new Vec3(0.177f, 0.205f, 0.223f), 0.400f)
        };

        // The mission whose scene last had fog laid over its preset. Whether SetAtmosphereWithName replaces fog set by
        // hand is engine code, so the next round puts the preset's own back itself.
        private static Mission _fogChangedMission;

        public const int TimeCount = 6;

        public const int WeatherCount = 5;

        // An admin's fixed time of day and weather (RoundTime / RoundWeather), or -1: drawn from the seed. On the server
        // it is the admin panel's setting; on a client it is what the server last sent (WeatherOverride, also on
        // joining). It lasts for the map: every machine resets it as a mission starts
        // (MissionMultiplayerCommanderBattleClient.OnBehaviorInitialize).
        public static int ForcedTime { get; private set; } = -1;

        public static int ForcedWeather { get; private set; } = -1;

        public static void SetForced(int time, int weather)
        {
            ForcedTime = time >= 0 && time < TimeCount ? time : -1;
            ForcedWeather = weather >= 0 && weather < WeatherCount ? weather : -1;
        }

        // The round's pick: drawn from the seed, then the admin's fixed parts in place of the drawn ones. Both are drawn
        // every time, so fixing one never changes what the other draws.
        public static Pick GetPick(int seed)
        {
            Pick pick = DrawPick(seed);
            if (ForcedTime >= 0)
            {
                pick.Time = (RoundTime)ForcedTime;
            }
            if (ForcedWeather >= 0)
            {
                pick.Weather = (RoundWeather)ForcedWeather;
            }
            return pick;
        }

        private static Pick DrawPick(int seed)
        {
            CommanderBattleSpawnLayout.LayoutRandom random = new CommanderBattleSpawnLayout.LayoutRandom(seed, RandomStream);
            return new Pick
            {
                Time = (RoundTime)DrawWeighted(ref random, TimeWeights),
                Weather = (RoundWeather)DrawWeighted(ref random, WeatherWeights)
            };
        }

        private static int DrawWeighted(ref CommanderBattleSpawnLayout.LayoutRandom random, int[] weights)
        {
            int total = 0;
            foreach (int weight in weights)
            {
                total += weight;
            }
            int roll = random.Next(total);
            for (int i = 0; i < weights.Length; i++)
            {
                roll -= weights[i];
                if (roll < 0)
                {
                    return i;
                }
            }
            return weights.Length - 1;
        }

        // TOD_NN is the morning hour and the day is symmetric about noon: 14:00 is TOD_10, 15:00 TOD_09, 19:00 TOD_05.
        private static string GetClearAtmosphere(RoundTime time)
        {
            switch (time)
            {
                case RoundTime.Morning:
                    return "TOD_06_00_SemiCloudy";
                case RoundTime.LateMorning:
                    return "TOD_11_00_SemiCloudy";
                case RoundTime.Noon:
                    return "TOD_12_00_SemiCloudy";
                case RoundTime.EarlyAfternoon:
                    return "TOD_10_00_SemiCloudy";
                case RoundTime.Afternoon:
                    return "TOD_09_00_SemiCloudy";
                default:
                    return "TOD_05_00_SemiCloudy";
            }
        }

        private static bool IsMidday(RoundTime time)
        {
            return time == RoundTime.LateMorning || time == RoundTime.Noon || time == RoundTime.EarlyAfternoon;
        }

        // Overcast and thick fog take the one summer overcast at every time: under full cloud the sun's height barely
        // shows.
        public static string GetAtmosphere(Pick pick)
        {
            switch (pick.Weather)
            {
                case RoundWeather.Overcast:
                    return OvercastAtmosphere;
                case RoundWeather.ThickFog:
                    return pick.Time == RoundTime.Morning ? "TOD_06_00_Foggy" : OvercastAtmosphere;
                case RoundWeather.Rain:
                    return IsMidday(pick.Time) ? MiddayRainAtmosphere : OvercastAtmosphere;
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
                float rain = pick.Weather != RoundWeather.Rain ? 0f : atmosphere == MiddayRainAtmosphere ? 1f : RainDensity;
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

        // The admin's setting, for the announcement: "dusk, random weather".
        public static string DescribeForced()
        {
            if (ForcedTime < 0 && ForcedWeather < 0)
            {
                return "random time and weather";
            }
            string time = ForcedTime < 0 ? "random time" : GetTimeName((RoundTime)ForcedTime).ToLowerInvariant();
            string weather = ForcedWeather < 0 ? "random weather" : GetWeatherName((RoundWeather)ForcedWeather);
            return $"{time}, {weather}";
        }

        // For the admin panel's choices: "Light fog".
        public static string GetWeatherDisplayName(RoundWeather weather)
        {
            string name = GetWeatherName(weather);
            return char.ToUpperInvariant(name[0]) + name.Substring(1);
        }

        public static string GetTimeName(RoundTime time)
        {
            switch (time)
            {
                case RoundTime.Morning:
                    return "Morning";
                case RoundTime.LateMorning:
                    return "Late morning";
                case RoundTime.Noon:
                    return "Noon";
                case RoundTime.EarlyAfternoon:
                    return "Early afternoon";
                case RoundTime.Afternoon:
                    return "Afternoon";
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

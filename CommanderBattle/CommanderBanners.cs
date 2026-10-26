using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.Missions.Multiplayer;

namespace CommanderBattle
{
    // A commander's banner: their sigil on a shade of their side's banner colour, the sigil in the side's second colour,
    // so the two sides still read apart. Built the same way on the server and every client from (side, sigil, shade):
    // the server paints shields with it (the banner travels inside each weapon of the CreateAgent message), clients use
    // it for the formation list, the compass and the labels.
    //
    // Banner colours are palette ids, not free colours: Banner.ChangePrimaryColor looks the colour up and silently does
    // nothing for one that isn't in the palette, so shades are palette entries.
    public static class CommanderBanners
    {
        public const int MaxShades = 5;

        // Sigil -1: the side's own sigil (a commander with no banner of their own).
        public const int SideSigil = -1;

        // Longest profile banner code the server reads a sigil from. A stock MP banner (background and one sigil) is
        // about 60 characters.
        private const int MaxProfileCodeLength = 400;

        // Placement of a sigil on the banner, as the stock single-icon banners place theirs (Banner.CreateOneColored...).
        private const float SigilSize = 512f;

        private const float BannerCentre = 764f;

        // A shade's hue may differ this much from the side's colour (degrees), its saturation this much.
        private const float ShadeHueWindow = 20f;

        private const float ShadeSaturationWindow = 0.35f;

        // Colours below this saturation are greys: their shades are other greys.
        private const float GreySaturation = 0.2f;

        // Two shades closer than this in brightness (and hue) look the same on a shield; the second is left out.
        private const float DistinctValue = 0.08f;

        // A shade this close to the other side's colour (RGB distance, 0-441) is left out.
        private const float EnemyDistance = 60f;

        private static string _shadeCultures;

        private static readonly List<int>[] _shades = { new List<int>(), new List<int>() };

        private static List<int> _availableSigils;

        // The side's banner: its culture's banner in the side's battle colours. The team banner (CreateTeams) is this.
        public static Banner GetSideBanner(BattleSideEnum side)
        {
            BasicCultureObject attackerCulture = CommanderBattleTeams.GetCulture(BattleSideEnum.Attacker);
            BasicCultureObject defenderCulture = CommanderBattleTeams.GetCulture(BattleSideEnum.Defender);
            BasicCultureObject culture = side == BattleSideEnum.Attacker ? attackerCulture : defenderCulture;
            if (culture?.Banner == null)
            {
                return null;
            }
            MultiplayerBattleColors colors = MultiplayerBattleColors.CreateWith(attackerCulture, defenderCulture);
            MultiplayerBattleColors.MultiplayerCultureColorInfo sideColors = side == BattleSideEnum.Attacker ? colors.AttackerColors : colors.DefenderColors;
            Banner banner = new Banner(culture.Banner);
            if (banner.BannerDataList.Count == 0)
            {
                return null;
            }
            banner.ChangePrimaryColor(sideColors.BannerBackgroundColorUint);
            banner.ChangeIconColors(sideColors.BannerForegroundColorUint);
            return banner;
        }

        // Palette ids of the side's shades: index 0 is the side's own colour, then up to MaxShades - 1 palette colours
        // closest in hue to it, light to dark. Colours nearer the other side's colour are left out, for cultures whose
        // colours are close. Every machine has the same palette and cultures, so every machine gets the same list.
        public static IReadOnlyList<int> GetShades(BattleSideEnum side)
        {
            if (side != BattleSideEnum.Attacker && side != BattleSideEnum.Defender)
            {
                return Array.Empty<int>();
            }
            string cultures = $"{CommanderBattleTeams.GetCulture(BattleSideEnum.Attacker)?.StringId}|{CommanderBattleTeams.GetCulture(BattleSideEnum.Defender)?.StringId}";
            if (cultures != _shadeCultures)
            {
                _shadeCultures = cultures;
                foreach (BattleSideEnum s in CommanderBattleTeams.Sides)
                {
                    _shades[(int)s].Clear();
                    _shades[(int)s].AddRange(ComputeShades(s));
                }
            }
            return _shades[(int)side];
        }

        private static List<int> ComputeShades(BattleSideEnum side)
        {
            List<int> shades = new List<int>();
            Banner own = GetSideBanner(side);
            Banner enemy = GetSideBanner(side == BattleSideEnum.Attacker ? BattleSideEnum.Defender : BattleSideEnum.Attacker);
            if (own == null || BannerManager.Instance == null)
            {
                return shades;
            }
            int ownId = own.GetPrimaryColorId();
            shades.Add(ownId);
            uint ownColor = BannerManager.GetColor(ownId);
            uint enemyColor = enemy != null ? BannerManager.GetColor(enemy.GetPrimaryColorId()) : 0u;
            ToHsv(ownColor, out float ownHue, out float ownSaturation, out _);
            bool ownGrey = ownSaturation < GreySaturation;
            List<(int Id, uint Color, float Closeness, float Value, float Hue)> candidates = new List<(int, uint, float, float, float)>();
            HashSet<uint> seen = new HashSet<uint> { ownColor & 0xFFFFFF };
            List<KeyValuePair<int, BannerColor>> palette = new List<KeyValuePair<int, BannerColor>>(BannerManager.Instance.ReadOnlyColorPalette);
            // Lowest id first, so a colour listed twice is always the same id on every machine.
            palette.Sort((a, b) => a.Key.CompareTo(b.Key));
            foreach (KeyValuePair<int, BannerColor> entry in palette)
            {
                uint color = entry.Value.Color;
                if (!seen.Add(color & 0xFFFFFF))
                {
                    continue;
                }
                ToHsv(color, out float hue, out float saturation, out float value);
                float hueDistance = HueDistance(hue, ownHue);
                bool grey = saturation < GreySaturation;
                if (ownGrey != grey || (!grey && (hueDistance > ShadeHueWindow || Math.Abs(saturation - ownSaturation) > ShadeSaturationWindow)))
                {
                    continue;
                }
                if (enemy != null && (RgbDistance(color, enemyColor) < EnemyDistance || RgbDistance(color, enemyColor) < RgbDistance(color, ownColor)))
                {
                    continue;
                }
                float closeness = grey ? Math.Abs(saturation - ownSaturation) : hueDistance / ShadeHueWindow + Math.Abs(saturation - ownSaturation);
                candidates.Add((entry.Key, color, closeness, value, hue));
            }
            candidates.Sort((a, b) => a.Closeness != b.Closeness ? a.Closeness.CompareTo(b.Closeness) : a.Id.CompareTo(b.Id));
            ToHsv(ownColor, out _, out _, out float ownValue);
            List<(int Id, float Value, float Hue)> picked = new List<(int, float, float)> { (ownId, ownValue, ownHue) };
            foreach ((int id, uint _, float _, float value, float hue) in candidates)
            {
                if (picked.Count >= MaxShades)
                {
                    break;
                }
                if (picked.Exists(p => Math.Abs(p.Value - value) < DistinctValue && HueDistance(p.Hue, hue) < ShadeHueWindow / 2f))
                {
                    continue;
                }
                picked.Add((id, value, hue));
            }
            List<(int Id, float Value, float Hue)> others = picked.GetRange(1, picked.Count - 1);
            others.Sort((a, b) => a.Value != b.Value ? b.Value.CompareTo(a.Value) : a.Id.CompareTo(b.Id));
            foreach ((int id, float _, float _) in others)
            {
                shades.Add(id);
            }
            return shades;
        }

        // A commander's banner. sigil: a BannerManager icon id, or SideSigil; a shade out of the side's range is the
        // side's own colour.
        public static Banner Build(BattleSideEnum side, int sigil, int shade)
        {
            Banner sideBanner = GetSideBanner(side);
            if (sideBanner == null)
            {
                return null;
            }
            IReadOnlyList<int> shades = GetShades(side);
            int colorId = shade >= 0 && shade < shades.Count ? shades[shade] : sideBanner.GetPrimaryColorId();
            BannerData background = sideBanner.BannerDataList[0];
            Banner banner = new Banner();
            banner.AddIconData(new BannerData(background.MeshId, colorId, colorId, background.Size, background.Position, background.DrawStroke, background.Mirror, background.RotationValue));
            if (sigil == SideSigil || !IsValidSigil(sigil))
            {
                for (int i = 1; i < sideBanner.BannerDataList.Count; i++)
                {
                    banner.AddIconData(new BannerData(sideBanner.BannerDataList[i]));
                }
                return banner;
            }
            int sigilColorId = sideBanner.BannerDataList.Count > 1 ? sideBanner.BannerDataList[1].ColorId : colorId;
            banner.AddIconData(new BannerData(sigil, sigilColorId, sigilColorId, new Vec2(SigilSize, SigilSize), new Vec2(BannerCentre, BannerCentre), drawStroke: true, mirror: false, 0f));
            return banner;
        }

        // An icon the game has (BannerManager, not a background pattern).
        public static bool IsValidSigil(int sigil)
        {
            if (sigil < 0 || BannerManager.Instance == null)
            {
                return false;
            }
            foreach (BannerIconGroup group in BannerManager.Instance.BannerIconGroups)
            {
                if (!group.IsPattern && group.AllIcons.ContainsKey(sigil))
                {
                    return true;
                }
            }
            return false;
        }

        // The sigils a player may pick: every icon the game offers (not reserved), in the game's group order.
        public static IReadOnlyList<int> GetAvailableSigils()
        {
            if (_availableSigils == null && BannerManager.Instance != null)
            {
                _availableSigils = new List<int>();
                foreach (BannerIconGroup group in BannerManager.Instance.BannerIconGroups)
                {
                    if (group.IsPattern)
                    {
                        continue;
                    }
                    List<int> ids = new List<int>(group.AvailableIcons.Keys);
                    ids.Sort();
                    _availableSigils.AddRange(ids);
                }
            }
            return (IReadOnlyList<int>)_availableSigils ?? Array.Empty<int>();
        }

        // The sigil of a profile banner (the first icon above its background), or SideSigil when the code is missing,
        // malformed, oversized or names an icon the game doesn't have. Only the sigil is kept: what reaches the other
        // players is a banner built from it.
        public static int GetProfileSigil(string bannerCode)
        {
            if (string.IsNullOrEmpty(bannerCode) || bannerCode.Length > MaxProfileCodeLength || !Banner.TryGetBannerDataFromCode(bannerCode, out List<BannerData> layers) || layers.Count < 2)
            {
                return SideSigil;
            }
            int sigil = layers[1].MeshId;
            return IsValidSigil(sigil) ? sigil : SideSigil;
        }

        // "#RRGGBBFF", for the shade swatches.
        public static string ToColorText(int colorId)
        {
            uint color = BannerManager.GetColor(colorId);
            return $"#{(color >> 16) & 0xFF:X2}{(color >> 8) & 0xFF:X2}{color & 0xFF:X2}FF";
        }

        // Palette colours are 0xAARRGGBB.
        private static void ToHsv(uint color, out float hue, out float saturation, out float value)
        {
            float r = ((color >> 16) & 0xFF) / 255f;
            float g = ((color >> 8) & 0xFF) / 255f;
            float b = (color & 0xFF) / 255f;
            float max = Math.Max(r, Math.Max(g, b));
            float min = Math.Min(r, Math.Min(g, b));
            float delta = max - min;
            value = max;
            saturation = max > 0f ? delta / max : 0f;
            if (delta <= 0f)
            {
                hue = 0f;
            }
            else if (max == r)
            {
                hue = 60f * (((g - b) / delta % 6f + 6f) % 6f);
            }
            else if (max == g)
            {
                hue = 60f * ((b - r) / delta + 2f);
            }
            else
            {
                hue = 60f * ((r - g) / delta + 4f);
            }
        }

        private static float HueDistance(float a, float b)
        {
            float distance = Math.Abs(a - b) % 360f;
            return distance > 180f ? 360f - distance : distance;
        }

        private static float RgbDistance(uint a, uint b)
        {
            float dr = ((a >> 16) & 0xFF) - (float)((b >> 16) & 0xFF);
            float dg = ((a >> 8) & 0xFF) - (float)((b >> 8) & 0xFF);
            float db = (a & 0xFF) - (float)(b & 0xFF);
            return (float)Math.Sqrt(dr * dr + dg * dg + db * db);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Xml;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.PlayerServices;

namespace CommanderBattle
{
    // What a player keeps between sessions, on their own machine: Configs\CommanderBattle_<playerId>.xml in the game's
    // per-user folder (Documents\Mount and Blade II Bannerlord\Configs on Windows), as the stock MPPerkSelectionManager
    // keeps MPDefaultPerks_<playerId>.xml. Keyed by player id so two accounts on one machine stay apart. The server
    // stores nothing: the client sends its picks each time it joins a side, so they follow the player to any server.
    //
    // <CommanderBattle>
    //   <Banners>
    //     <Banner culture="vlandia" sigil="512" shade="2" />
    //   </Banners>
    // </CommanderBattle>
    //
    // A banner per culture: a shade is an index into that culture's shades, and a player may want a different sigil for
    // each army. One element per setting under the root; saving replaces only its own element, so settings added later
    // (the planned formation name) sit beside it.
    public static class CommanderBattleClientConfig
    {
        private const string RootElement = "CommanderBattle";

        private const string BannersElement = "Banners";

        private const string BannerElement = "Banner";

        private static PlayerId _loadedFor;

        private static bool _loaded;

        // By culture id: the picked sigil (a BannerManager icon id, or -1 for the profile banner's) and shade.
        private static readonly Dictionary<string, (int Sigil, int Shade)> _banners = new Dictionary<string, (int, int)>();

        // The banner saved for the culture; false when none is (the profile banner's sigil, the side's own colour).
        public static bool TryGetBanner(PlayerId playerId, string cultureId, out int sigil, out int shade)
        {
            EnsureLoaded(playerId);
            if (cultureId != null && _banners.TryGetValue(cultureId, out (int Sigil, int Shade) banner))
            {
                sigil = banner.Sigil;
                shade = banner.Shade;
                return true;
            }
            sigil = CommanderBanners.SideSigil;
            shade = 0;
            return false;
        }

        public static void SaveBanner(PlayerId playerId, string cultureId, int sigil, int shade)
        {
            if (string.IsNullOrEmpty(cultureId))
            {
                return;
            }
            EnsureLoaded(playerId);
            _banners[cultureId] = (sigil, shade);
            try
            {
                PlatformFilePath path = GetPath(playerId);
                XmlDocument document = new XmlDocument();
                if (FileHelper.FileExists(path))
                {
                    try
                    {
                        document.Load(path);
                    }
                    catch (XmlException)
                    {
                        document = new XmlDocument();
                    }
                }
                XmlElement root = document.DocumentElement;
                if (root == null || root.Name != RootElement)
                {
                    document = new XmlDocument();
                    document.AppendChild(document.CreateXmlDeclaration("1.0", "UTF-8", null));
                    root = document.CreateElement(RootElement);
                    document.AppendChild(root);
                }
                XmlElement banners = root[BannersElement];
                if (banners != null)
                {
                    root.RemoveChild(banners);
                }
                // The first test build kept one banner for every culture, directly under the root.
                XmlElement oneForAll = root[BannerElement];
                if (oneForAll != null)
                {
                    root.RemoveChild(oneForAll);
                }
                banners = document.CreateElement(BannersElement);
                root.AppendChild(banners);
                List<string> cultures = new List<string>(_banners.Keys);
                cultures.Sort(StringComparer.Ordinal);
                foreach (string culture in cultures)
                {
                    XmlElement banner = document.CreateElement(BannerElement);
                    banner.SetAttribute("culture", culture);
                    banner.SetAttribute("sigil", _banners[culture].Sigil.ToString());
                    banner.SetAttribute("shade", _banners[culture].Shade.ToString());
                    banners.AppendChild(banner);
                }
                document.Save(path);
            }
            catch (Exception exception)
            {
                CommanderBattleLog.Write($"Could not save the banner: {exception.Message}");
            }
        }

        private static void EnsureLoaded(PlayerId playerId)
        {
            if (_loaded && _loadedFor == playerId)
            {
                return;
            }
            _loaded = true;
            _loadedFor = playerId;
            _banners.Clear();
            try
            {
                PlatformFilePath path = GetPath(playerId);
                if (!FileHelper.FileExists(path))
                {
                    return;
                }
                XmlDocument document = new XmlDocument();
                document.Load(path);
                XmlElement banners = document.DocumentElement?[BannersElement];
                if (banners == null)
                {
                    return;
                }
                foreach (XmlNode node in banners.ChildNodes)
                {
                    if (node is XmlElement banner && banner.Name == BannerElement && banner.GetAttribute("culture").Length > 0)
                    {
                        _banners[banner.GetAttribute("culture")] = (ReadInt(banner, "sigil", CommanderBanners.SideSigil), Math.Max(0, ReadInt(banner, "shade", 0)));
                    }
                }
            }
            catch (Exception exception)
            {
                CommanderBattleLog.Write($"Could not read the saved banners: {exception.Message}");
            }
        }

        private static PlatformFilePath GetPath(PlayerId playerId)
        {
            return new PlatformFilePath(EngineFilePaths.ConfigsPath, $"CommanderBattle_{playerId}.xml");
        }

        private static int ReadInt(XmlElement element, string attribute, int fallback)
        {
            return int.TryParse(element.GetAttribute(attribute), out int value) ? value : fallback;
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Multiplayer;

namespace CommanderBattle
{
    // The stock Captain scoreboard plus a "formation" column after the name: the formation each player leads, with
    // its weight and weapon setup ("Heavy Infantry 3 · Spear and shield"). Read from the slot table, which the
    // server keeps and clients copy, so it needs no network message of its own. The stock prefab draws an unknown
    // column id as scrolling text; its title is str_scoreboard_header.formation in commander_battle_strings.xml.
    public class CommanderBattleScoreboardData : IScoreboardData
    {
        public const string FormationHeaderId = "formation";

        private readonly CaptainScoreboardData _captain = new CaptainScoreboardData();

        private MissionScoreboardComponent.ScoreboardHeader[] _headers;

        // MissionScoreboardComponent.Headers asks for the headers on every read.
        public MissionScoreboardComponent.ScoreboardHeader[] GetScoreboardHeaders()
        {
            if (_headers == null)
            {
                List<MissionScoreboardComponent.ScoreboardHeader> headers = _captain.GetScoreboardHeaders().ToList();
                int name = headers.FindIndex(header => header.Id == "name");
                // The bots' row stands for every AI-led formation of the side at once, so it gets no formation.
                headers.Insert(name + 1, new MissionScoreboardComponent.ScoreboardHeader(FormationHeaderId, DescribeFormationOf, bot => ""));
                _headers = headers.ToArray();
            }
            return _headers;
        }

        private static string DescribeFormationOf(MissionPeer missionPeer)
        {
            // The server's game mode shares this table (MissionMultiplayerCommanderBattle.Slots).
            FormationSlotTable slots = Mission.Current?.GetMissionBehavior<MissionMultiplayerCommanderBattleClient>()?.Slots;
            FormationSlot slot = slots?.FindSlotOf(missionPeer.GetNetworkPeer());
            if (slot == null)
            {
                return "-";
            }
            // Named by its formation number, as the order bar and the formation list number it; the general by name.
            string name = slot.DisplayName;
            MultiplayerClassDivisions.MPHeroClass heroClass = CommanderRoles.GetClass(CommanderBattleTeams.GetCulture(slot.Side), slot.ClassIndex);
            if (heroClass == null)
            {
                return name;
            }
            string weight = CommanderRoles.GetWeightLabel(heroClass);
            string weapons = CommanderRoles.GetWeaponName(heroClass, slot.WeaponIndex);
            string troops = weight.Length > 0 ? $"{weight} {name}" : name;
            return weapons.Length > 0 ? $"{troops} · {weapons}" : troops;
        }
    }
}

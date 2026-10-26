using System.Linq;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Multiplayer.ViewModelCollection.TeamSelection;

namespace CommanderBattle.View
{
    // Makes clear which side the player is on:
    //   - a banner, large for a few seconds when the player joins a side and when a round starts, then small at
    //     the top of the screen;
    //   - "Attackers" / "Defenders" next to the culture names on the stock team selection screen, which shows only
    //     the cultures.
    public class CommanderBattleSideBannerView : CommanderBattleLayerView<SideBannerVM>
    {
        private const string MovieName = "CommanderBattleSideBanner";

        private const float AnnounceSeconds = 6f;

        // The stock team selection view keeps its view model private; it is read by name to relabel the teams.
        private const string TeamSelectionViewName = "MissionGauntletTeamSelection";

        private const string TeamSelectionDataSourceField = "_dataSource";

        private BattleSideEnum _shownSide = BattleSideEnum.None;

        private bool _wasRoundInProgress;

        private float _announceTimer;

        private MissionBehavior _teamSelectionView;

        private FieldInfo _teamSelectionDataSource;

        public CommanderBattleSideBannerView()
        {
            // Below the team selection (22) and the formation list (24).
            ViewOrderPriority = 15;
        }

        public static string GetSideName(BattleSideEnum side)
        {
            return side == BattleSideEnum.Attacker ? "Attackers" : "Defenders";
        }

        public override void OnMissionScreenInitialize()
        {
            base.OnMissionScreenInitialize();
            OpenLayer(new SideBannerVM(), MovieName, ViewOrderPriority, captureMouse: false);
            _teamSelectionView = Mission.MissionBehaviors.FirstOrDefault(behavior => behavior.GetType().Name == TeamSelectionViewName);
            _teamSelectionDataSource = _teamSelectionView?.GetType().GetField(TeamSelectionDataSourceField, BindingFlags.Instance | BindingFlags.NonPublic);
        }

        public override void OnMissionScreenTick(float dt)
        {
            base.OnMissionScreenTick(dt);
            if (!IsOpen)
            {
                return;
            }
            BattleSideEnum side = CommanderBattleTeams.MySide;
            bool roundInProgress = Client != null && Client.IsRoundInProgress;
            if (side != _shownSide)
            {
                _shownSide = side;
                ShowSide(side);
            }
            else if (roundInProgress && !_wasRoundInProgress && side != BattleSideEnum.None)
            {
                Announce();
            }
            _wasRoundInProgress = roundInProgress;
            if (_announceTimer > 0f)
            {
                _announceTimer -= dt;
                if (_announceTimer <= 0f)
                {
                    DataSource.IsAnnouncing = false;
                }
            }
            LabelTeamSelection();
        }

        private void ShowSide(BattleSideEnum side)
        {
            if (side == BattleSideEnum.None)
            {
                DataSource.IsVisible = false;
                DataSource.IsAnnouncing = false;
                _announceTimer = 0f;
                return;
            }
            string culture = CommanderBattleTeams.GetCulture(side)?.Name?.ToString();
            string role = side == BattleSideEnum.Attacker ? "You are attacking" : "You are defending";
            DataSource.SideText = string.IsNullOrEmpty(culture) ? role : $"{role} ({culture})";
            DataSource.TaskText = side == BattleSideEnum.Attacker
                ? "Break the enemy army before time runs out."
                : "Hold out until time runs out, or break the enemy army.";
            DataSource.IsVisible = true;
            Announce();
        }

        private void Announce()
        {
            DataSource.IsAnnouncing = true;
            _announceTimer = AnnounceSeconds;
        }

        // The stock screen names each team only by its culture; put the side in front of it.
        private void LabelTeamSelection()
        {
            if (_teamSelectionDataSource?.GetValue(_teamSelectionView) is MultiplayerTeamSelectVM teamSelect)
            {
                LabelTeam(teamSelect.Team1);
                LabelTeam(teamSelect.Team2);
            }
        }

        private static void LabelTeam(TeamSelectTeamInstanceVM team)
        {
            if (team?.Team == null || team.Team.Side == BattleSideEnum.None)
            {
                return;
            }
            string culture = CommanderBattleTeams.GetCulture(team.Team.Side)?.Name?.ToString();
            string label = string.IsNullOrEmpty(culture) ? GetSideName(team.Team.Side) : $"{GetSideName(team.Team.Side)}: {culture}";
            team.DisplayedPrimary = label;
        }
    }
}

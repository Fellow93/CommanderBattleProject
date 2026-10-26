using System;
using System.Collections.Generic;
using System.Linq;
using CommanderBattle.Network;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Network.Messages;

namespace CommanderBattle
{
    // The general's side of the order bar (docs/plans/general-orders.md), on his client only:
    //   - a local mirror makes his team's formations 0-7 his own (PlayerOwner = his agent), so the stock bar opens,
    //     lists them and lets him select (MissionOrderVM.CheckCanBeOpened, OrderController.IsFormationSelectable). The
    //     server never does this: there they stay un-owned. Their IsAIControlled follows the server's SideAIStatus, so
    //     the Delegate toggle shows who leads them;
    //   - he never has more than one formation selected;
    //   - every order the bar gives goes to the server as a RequestGeneralCommand. The bar also applies it here,
    //     which is only cosmetic, and sends the stock order messages, which the server ignores for him.
    // Every player also keeps the suggestions the server sends them (theirs as a commander, or the general's echo).
    public partial class MissionMultiplayerCommanderBattleClient
    {
        // Until the server's next status says so, a formation he just ordered shows the leader he gave it.
        private const float LeaderOverrideSeconds = 3f;

        private const string SuggestionSound = "event:/ui/notification/alert";

        private static readonly Color SuggestionColor = Color.FromUint(0xFF8CC8FFu);

        private Team _mirrorTeam;

        private OrderController _hookedController;

        private bool _isTrimmingSelection;

        private readonly List<Formation> _lastSelection = new List<Formation>();

        private Formation _keptFormation;

        // Each formation's unit spacing before this frame's orders (the views give orders after the mission tick), so a
        // move line's spacing change can be sent along with its width.
        private readonly int[] _spacingBeforeOrders = new int[CommanderBattleSettings.FormationCount];

        private readonly AIFormationLeader?[] _leaderOverride = new AIFormationLeader?[CommanderBattleSettings.FormationCount];

        private readonly float[] _leaderOverrideUntil = new float[CommanderBattleSettings.FormationCount];

        // A move line fires its facing and width right after its move, in the same frame.
        private Formation _lastMoveFormation;

        private float _lastMoveTime = -1f;

        private bool _toldNotSuggested;

        // The latest suggestion per side and formation; only the formation's commander and the general get them.
        private readonly Dictionary<int, GeneralSuggestion> _generalSuggestions = new Dictionary<int, GeneralSuggestion>();

        public event Action OnGeneralSuggestionsChanged;

        public IEnumerable<GeneralSuggestion> GeneralSuggestions => _generalSuggestions.Values;

        // We are our side's general (our soldier may be dead).
        public bool IsGeneralHere => MySlot is FormationSlot slot && slot.IsGeneral && slot.Side == CommanderBattleTeams.MySide
            && Mission.PlayerTeam != null && Mission.PlayerTeam.Side == slot.Side;

        // A suggestion we should see: to the formation we lead, or one we gave as our side's general.
        public bool IsSuggestionForMe(GeneralSuggestion suggestion)
        {
            FormationSlot slot = Slots.GetSlot(suggestion.Side, suggestion.FormationIndex);
            if (slot == null || suggestion.Side != CommanderBattleTeams.MySide)
            {
                return false;
            }
            return (slot.Commander != null && slot.Commander.IsMine) || IsGeneralHere;
        }

        private void TickGeneralCommand()
        {
            Team team = IsGeneralHere ? Mission.PlayerTeam : null;
            if (_mirrorTeam != null && _mirrorTeam != team)
            {
                RestoreMirror(_mirrorTeam);
            }
            _mirrorTeam = team;
            if (team != null)
            {
                ApplyMirror(team);
            }
            HookOrderController(team?.PlayerOrderController);
            if (!Mission.IsOrderMenuOpen)
            {
                _toldNotSuggested = false;
            }
            if (_generalSuggestions.Count > 0 && !IsRoundInProgress)
            {
                ClearGeneralSuggestions();
            }
        }

        // The PlayerOwner setter calls SetControlledByAI(value == null), so it is only assigned when it differs.
        private void ApplyMirror(Team team)
        {
            Agent owner = Agent.Main != null && Agent.Main.IsActive() ? Agent.Main : null;
            foreach (Formation formation in team.FormationsIncludingEmpty)
            {
                int index = (int)formation.FormationIndex;
                if (index < 0 || index >= CommanderBattleSettings.FormationCount)
                {
                    continue;
                }
                if (formation.PlayerOwner != owner)
                {
                    formation.PlayerOwner = owner;
                }
                if (owner != null)
                {
                    // No team AI runs on a client, so this only sets the flag the Delegate toggle reads.
                    bool aiLed = GetLeader(team.Side, index) == AIFormationLeader.AI;
                    if (formation.IsAIControlled != aiLed)
                    {
                        formation.SetControlledByAI(aiLed);
                    }
                }
                _spacingBeforeOrders[index] = formation.UnitSpacing;
            }
        }

        // Only his own formation (the general's 8, or one he leads from now on) is really his on this client.
        private void RestoreMirror(Team team)
        {
            Formation own = GameNetwork.IsMyPeerReady ? GameNetwork.MyPeer.GetComponent<MissionPeer>()?.ControlledFormation : null;
            foreach (Formation formation in team.FormationsIncludingEmpty)
            {
                if (formation.PlayerOwner != null && formation != own)
                {
                    formation.PlayerOwner = null;
                }
            }
            for (int i = 0; i < _leaderOverride.Length; i++)
            {
                _leaderOverride[i] = null;
            }
        }

        private AIFormationLeader GetLeader(BattleSideEnum side, int formationIndex)
        {
            AIFormationLeader fromServer = GetServerLeader(side, formationIndex);
            AIFormationLeader? local = _leaderOverride[formationIndex];
            if (local == null)
            {
                return fromServer;
            }
            if (local.Value == fromServer || Mission.CurrentTime >= _leaderOverrideUntil[formationIndex])
            {
                _leaderOverride[formationIndex] = null;
                return fromServer;
            }
            return local.Value;
        }

        // The slot table knows a commander at once; the AI status tells AI from the general.
        private AIFormationLeader GetServerLeader(BattleSideEnum side, int formationIndex)
        {
            if (Slots.GetSlot(side, formationIndex)?.Commander != null)
            {
                return AIFormationLeader.Player;
            }
            SideAIStatus status = AIStatus;
            if (status != null && status.Side == side)
            {
                foreach (AIFormationStatus formation in status.Formations)
                {
                    if (formation.FormationIndex == formationIndex)
                    {
                        return formation.Leader;
                    }
                }
            }
            return AIFormationLeader.AI;
        }

        private void SetLeaderOverride(int formationIndex, AIFormationLeader leader)
        {
            _leaderOverride[formationIndex] = leader;
            _leaderOverrideUntil[formationIndex] = Mission.CurrentTime + LeaderOverrideSeconds;
        }

        private void HookOrderController(OrderController controller)
        {
            if (controller == _hookedController)
            {
                return;
            }
            if (_hookedController != null)
            {
                _hookedController.OnOrderIssued -= OnGeneralOrderIssued;
                _hookedController.OnSelectedFormationsChanged -= OnGeneralSelectionChanged;
            }
            _hookedController = controller;
            _lastSelection.Clear();
            _keptFormation = null;
            if (controller != null)
            {
                controller.OnOrderIssued += OnGeneralOrderIssued;
                controller.OnSelectedFormationsChanged += OnGeneralSelectionChanged;
                OnGeneralSelectionChanged();
            }
        }

        // One formation at a time (the user's decision): the newest one picked. The bar selects every formation when it
        // opens with none selected (MissionOrderVM.OpenToggleOrder) or on a number with no formation; then the one he
        // had stays, else the lowest. Deselecting raises this event again, hence the guard.
        private void OnGeneralSelectionChanged()
        {
            OrderController controller = _hookedController;
            if (_isTrimmingSelection || controller == null)
            {
                return;
            }
            List<Formation> selected = controller.SelectedFormations.ToList();
            if (selected.Count > 1)
            {
                List<Formation> added = selected.Where(formation => !_lastSelection.Contains(formation)).ToList();
                Formation keep = added.Count == 1 ? added[0]
                    : _keptFormation != null && selected.Contains(_keptFormation) ? _keptFormation
                    : selected.OrderBy(formation => (int)formation.FormationIndex).First();
                _isTrimmingSelection = true;
                try
                {
                    foreach (Formation formation in selected)
                    {
                        if (formation != keep)
                        {
                            controller.DeselectFormation(formation);
                        }
                    }
                }
                finally
                {
                    _isTrimmingSelection = false;
                }
                selected = controller.SelectedFormations.ToList();
            }
            _lastSelection.Clear();
            _lastSelection.AddRange(selected);
            if (selected.Count == 1)
            {
                _keptFormation = selected[0];
            }
        }

        // The stock line order (MoveToLineSegment) is left out: it fires a Move, a facing and a width per formation
        // (OrderController.MoveToLineSegment), which carry what the server needs.
        private void OnGeneralOrderIssued(OrderType orderType, MBReadOnlyList<Formation> appliedFormations, OrderController orderController, params object[] delegateParams)
        {
            if (orderType == OrderType.MoveToLineSegment || orderType == OrderType.MoveToLineSegmentWithHorizontalLayout || appliedFormations == null || !IsGeneralHere)
            {
                return;
            }
            object parameter = delegateParams != null && delegateParams.Length > 0 ? delegateParams[0] : null;
            foreach (Formation formation in appliedFormations.ToList())
            {
                SendGeneralCommand(orderType, formation, orderController.Team, parameter);
            }
        }

        private void SendGeneralCommand(OrderType orderType, Formation formation, Team team, object parameter)
        {
            int index = (int)formation.FormationIndex;
            if (team == null || formation.Team != team || index < 0 || index >= CommanderBattleSettings.FormationCount)
            {
                return;
            }
            RequestGeneralCommand command = new RequestGeneralCommand(orderType, index);
            switch (parameter)
            {
                case null:
                    break;
                case WorldPosition position:
                    if (orderType == OrderType.FormCustom)
                    {
                        // The stock width (OrderController.GetOrderFormCustomWidth) is from the general to the flag; the
                        // server has no Agent.Main to measure from.
                        Agent main = Agent.Main;
                        if (main == null)
                        {
                            return;
                        }
                        command.WithWidth((main.Position - position.GetGroundVec3()).Length, 0);
                    }
                    else
                    {
                        command.WithPosition(position.GetGroundVec3());
                    }
                    break;
                case Vec2 direction:
                    command.WithDirection(direction);
                    break;
                case float width:
                    command.WithWidth(width, formation.UnitSpacing - _spacingBeforeOrders[index]);
                    break;
                case Formation target:
                    if (target.Team == null)
                    {
                        return;
                    }
                    command.WithFormation(target.Team.Side, (int)target.FormationIndex);
                    break;
                case Agent agent:
                    command.WithAgent(agent.Index);
                    break;
                default:
                    return;
            }
            if (!GeneralCommands.IsAllowed(orderType, command.Payload))
            {
                return;
            }
            bool partOfMove = formation == _lastMoveFormation && _lastMoveTime == Mission.CurrentTime;
            if (orderType == OrderType.Move)
            {
                _lastMoveFormation = formation;
                _lastMoveTime = Mission.CurrentTime;
            }
            if (Slots.GetSlot(team.Side, index)?.Commander != null)
            {
                // A player leads it: only movement orders become a suggestion, and a move line's facing and width join
                // its move. The bar has already shown the order on this client only.
                if (!partOfMove && !GeneralCommands.TryGetSuggestionKind(orderType, out _))
                {
                    TellNotSuggested();
                    return;
                }
                if (orderType == OrderType.LookAtEnemy)
                {
                    return;
                }
            }
            else
            {
                SetLeaderOverride(index, orderType == OrderType.AIControlOn ? AIFormationLeader.AI : AIFormationLeader.General);
            }
            Request(command, gameMode => gameMode.RequestGeneralCommandFrom(GameNetwork.MyPeer, command));
        }

        // Once per opening of the bar.
        private void TellNotSuggested()
        {
            if (_toldNotSuggested)
            {
                return;
            }
            _toldNotSuggested = true;
            InformationManager.DisplayMessage(new InformationMessage(GeneralCommands.NotSuggestedNotice, SuggestionColor));
        }

        // --- Suggestions ---

        private void HandleServerEventGeneralSuggestion(GameNetworkMessage baseMessage)
        {
            AddGeneralSuggestion((GeneralSuggestion)baseMessage);
        }

        internal void AddGeneralSuggestion(GeneralSuggestion suggestion)
        {
            if (suggestion == null || (suggestion.Side != BattleSideEnum.Attacker && suggestion.Side != BattleSideEnum.Defender))
            {
                return;
            }
            _generalSuggestions[(int)suggestion.Side * 16 + suggestion.FormationIndex] = suggestion;
            OnGeneralSuggestionsChanged?.Invoke();
            AnnounceSuggestion(suggestion);
        }

        // "Fellow, move here" (GeneralCommands.GetSuggestionLine), to the commander with a sound and echoed to the
        // general. A centre pop-up and a line in the message log.
        private void AnnounceSuggestion(GeneralSuggestion suggestion)
        {
            FormationSlot slot = Slots.GetSlot(suggestion.Side, suggestion.FormationIndex);
            if (slot == null)
            {
                return;
            }
            bool isMine = slot.Commander != null && slot.Commander.IsMine;
            if (!isMine && !(IsGeneralHere && suggestion.Side == CommanderBattleTeams.MySide))
            {
                return;
            }
            string text = GeneralCommands.GetSuggestionLine(slot, suggestion.Kind, suggestion.HasTarget);
            // Player names may hold braces; as a variable they are never parsed as markup.
            MBInformationManager.AddQuickInformation(new TextObject("{=!}{TEXT}").SetTextVariable("TEXT", text));
            InformationManager.DisplayMessage(new InformationMessage(text, SuggestionColor));
            if (isMine)
            {
                SoundEvent.PlaySound2D(SuggestionSound);
            }
        }

        private void ClearGeneralSuggestions()
        {
            if (_generalSuggestions.Count == 0)
            {
                return;
            }
            _generalSuggestions.Clear();
            OnGeneralSuggestionsChanged?.Invoke();
        }

        // Rounds reuse the formations; Formation.Reset has cleared every PlayerOwner, and the mirror sets them again.
        private void ClearGeneralCommandState()
        {
            _lastSelection.Clear();
            _keptFormation = null;
            _lastMoveFormation = null;
            for (int i = 0; i < _leaderOverride.Length; i++)
            {
                _leaderOverride[i] = null;
            }
            ClearGeneralSuggestions();
        }
    }
}

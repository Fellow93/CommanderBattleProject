using System;
using System.Collections.Generic;
using CommanderBattle.Network;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Network.Messages;

namespace CommanderBattle
{
    // The general orders formations with the stock order bar (docs/plans/general-orders.md). His client sends each
    // order it gives (RequestGeneralCommand); the stock order messages it also sends do nothing here, because no
    // formation is selectable for him on the server.
    //
    // A formation the AI leads obeys: the order takes it over (IsAIControlled off, PlayerOwner stays null), and the
    // stock Delegate order hands it back. PlayerOwner is never set for him: bot scoring (MissionLobbyComponent) and the
    // spectator card look for the peer whose ControlledFormation the formation is, and RBM's guard keys on it.
    // A formation a player leads only gets a suggestion, sent to its commander and the general.
    public partial class MissionMultiplayerCommanderBattle
    {
        private sealed class CommandRate
        {
            public float WindowStart;

            public int Count;
        }

        // Per side, by (int)BattleSideEnum: an owner-less order controller that applies the general's orders. Owner-less,
        // so every formation with men is selectable and no gesture plays. Teams survive rounds, so these do too.
        private readonly OrderController[] _generalOrderControllers = new OrderController[2];

        private readonly Dictionary<NetworkCommunicator, CommandRate> _generalCommandRates = new Dictionary<NetworkCommunicator, CommandRate>();

        // Suggestions collected this tick, by side and formation: a move line's move, facing and width become one.
        private readonly Dictionary<int, GeneralSuggestion> _pendingSuggestions = new Dictionary<int, GeneralSuggestion>();

        private bool HandleClientEventRequestGeneralCommand(NetworkCommunicator peer, GameNetworkMessage baseMessage)
        {
            RequestGeneralCommandFrom(peer, (RequestGeneralCommand)baseMessage);
            return true;
        }

        public void RequestGeneralCommandFrom(NetworkCommunicator peer, RequestGeneralCommand command)
        {
            BattleSideEnum side = GetGeneralSide(peer);
            MissionPeer missionPeer = peer.GetComponent<MissionPeer>();
            Agent general = missionPeer?.ControlledAgent;
            // Stale orders from someone who just stopped being the general, or died, are dropped quietly.
            if (side == BattleSideEnum.None || general == null || !general.IsActive() || !RoundController.IsRoundInProgress)
            {
                return;
            }
            if (!TakeCommandToken(peer))
            {
                return;
            }
            Team team = CommanderBattleTeams.GetTeam(Mission, side);
            if (team == null || command.FormationIndex < 0 || command.FormationIndex >= CommanderBattleSettings.FormationCount)
            {
                return;
            }
            Formation formation = team.GetFormation((FormationClass)command.FormationIndex);
            if (formation == null || formation.CountOfUnits == 0 || !GeneralCommands.IsAllowed(command.OrderType, command.Payload) || !IsPayloadValid(command, team, general))
            {
                return;
            }
            CommanderBattleActivityLogic.MarkActive(missionPeer);
            if (formation.PlayerOwner != null)
            {
                CollectSuggestion(side, formation, command);
                return;
            }
            ApplyGeneralCommand(peer, side, team, formation, command, general);
        }

        // At most GeneralCommands.MaxCommandsPerSecond per general, counted in one-second windows.
        private bool TakeCommandToken(NetworkCommunicator peer)
        {
            float now = Mission.CurrentTime;
            if (!_generalCommandRates.TryGetValue(peer, out CommandRate rate))
            {
                rate = new CommandRate { WindowStart = now };
                _generalCommandRates[peer] = rate;
            }
            if (now - rate.WindowStart >= 1f)
            {
                rate.WindowStart = now;
                rate.Count = 0;
            }
            if (rate.Count >= GeneralCommands.MaxCommandsPerSecond)
            {
                return false;
            }
            rate.Count++;
            return true;
        }

        private bool IsPayloadValid(RequestGeneralCommand command, Team team, Agent general)
        {
            switch (command.Payload)
            {
                case GeneralCommandPayload.Position:
                    return command.Position.IsValid && Mission.IsPositionInsideBoundaries(command.Position.AsVec2);
                case GeneralCommandPayload.Direction:
                    return command.Direction.IsValid && command.Direction.LengthSquared > 0.01f;
                case GeneralCommandPayload.Width:
                    return !float.IsNaN(command.Width) && command.Width > 0f;
                case GeneralCommandPayload.Formation:
                    Formation target = GetTargetFormation(command);
                    return target != null && target.CountOfUnits > 0 && target.Team != null && target.Team.IsEnemyOf(team);
                case GeneralCommandPayload.Agent:
                    // Follow: only the general himself.
                    return command.AgentIndex == general.Index;
                default:
                    return true;
            }
        }

        private Formation GetTargetFormation(RequestGeneralCommand command)
        {
            Team team = CommanderBattleTeams.GetTeam(Mission, command.TargetSide);
            return team != null && command.TargetFormationIndex >= 0 && command.TargetFormationIndex < (int)FormationClass.NumberOfAllFormations
                ? team.GetFormation((FormationClass)command.TargetFormationIndex)
                : null;
        }

        // --- Formations the AI leads ---

        private void ApplyGeneralCommand(NetworkCommunicator peer, BattleSideEnum side, Team team, Formation formation, RequestGeneralCommand command, Agent general)
        {
            FormationSlot slot = Slots.GetSlot(side, command.FormationIndex);
            string name = slot?.DisplayName ?? $"formation {command.FormationIndex}";
            if (command.OrderType == OrderType.AIControlOn)
            {
                if (!formation.IsAIControlled)
                {
                    formation.SetControlledByAI(isControlledByAI: true);
                    CommanderBattleLog.Write($"{peer.UserName} ({side} general) delegated {name} to the AI");
                }
                return;
            }
            if (formation.IsAIControlled)
            {
                // The stock BeforeSetOrder only does this for a formation with a PlayerOwner.
                formation.SetControlledByAI(isControlledByAI: false);
                CommanderBattleLog.Write($"{peer.UserName} ({side} general) took over {name}");
            }
            switch (command.Payload)
            {
                case GeneralCommandPayload.Direction:
                    // A move line's facing: the general's client worked out the line, as the stock MoveToLineSegment
                    // would have here.
                    formation.SetFacingOrder(FacingOrder.FacingOrderLookAtDirection(command.Direction.Normalized()));
                    return;
                case GeneralCommandPayload.Width:
                    ApplyWidth(formation, command.Width, command.SpacingChange);
                    return;
            }
            if (command.OrderType == OrderType.AIControlOff)
            {
                return;
            }
            OrderController controller = GetGeneralOrderController(side, team);
            controller.ClearSelectedFormations();
            controller.SelectFormation(formation);
            try
            {
                switch (command.Payload)
                {
                    case GeneralCommandPayload.Position:
                        // Move, or a face-direction order (the controller turns the point into a direction from the
                        // formation's own order position, which only the server knows for an AI formation).
                        controller.SetOrderWithPosition(command.OrderType, new WorldPosition(Mission.Scene, UIntPtr.Zero, command.Position, hasValidZ: false));
                        break;
                    case GeneralCommandPayload.Formation:
                        controller.SetOrderWithFormation(command.OrderType, GetTargetFormation(command));
                        break;
                    case GeneralCommandPayload.Agent:
                        controller.SetOrderWithAgent(command.OrderType, general);
                        break;
                    default:
                        controller.SetOrder(command.OrderType);
                        break;
                }
            }
            finally
            {
                controller.ClearSelectedFormations();
            }
        }

        // A form order's width (the stock FormCustom reads Agent.Main, which a server doesn't have), and the unit spacing
        // a move line had to give up so the men fit it.
        private static void ApplyWidth(Formation formation, float width, int spacingChange)
        {
            if (spacingChange != 0)
            {
                int spacing = formation.UnitSpacing + spacingChange;
                formation.SetPositioning(null, null, spacing < 0 ? 0 : spacing);
            }
            formation.SetFormOrder(FormOrder.FormOrderCustom(width));
        }

        private OrderController GetGeneralOrderController(BattleSideEnum side, Team team)
        {
            OrderController controller = _generalOrderControllers[(int)side];
            if (controller == null || controller.Team != team)
            {
                controller = new OrderController(Mission, team, null);
                controller.OnOrderIssued += OnOrderIssuedForBattleCry;
                _generalOrderControllers[(int)side] = controller;
            }
            return controller;
        }

        // Every formation the general took over goes back to the AI at once: he died, left, stopped being the general,
        // or the round is over. A formation without a PlayerOwner that the AI doesn't control is one he took over.
        private void ReleaseGeneralCommand(BattleSideEnum side, string reason)
        {
            Team team = CommanderBattleTeams.GetTeam(Mission, side);
            if (team == null)
            {
                return;
            }
            int released = 0;
            foreach (Formation formation in team.FormationsIncludingEmpty)
            {
                if (formation.PlayerOwner == null && !formation.IsAIControlled)
                {
                    formation.SetControlledByAI(isControlledByAI: true);
                    released++;
                }
            }
            if (released > 0)
            {
                CommanderBattleLog.Write($"{side}: {released} formation{(released == 1 ? "" : "s")} the general led went back to the AI ({reason})");
            }
        }

        private void ReleaseAllGeneralCommand(string reason)
        {
            foreach (BattleSideEnum side in CommanderBattleTeams.Sides)
            {
                ReleaseGeneralCommand(side, reason);
            }
        }

        // Called from OnAgentRemoved: the general's own soldier is gone.
        private void ReleaseGeneralCommandOnDeath(Agent affectedAgent)
        {
            NetworkCommunicator peer = affectedAgent.MissionPeer?.GetNetworkPeer();
            if (peer == null)
            {
                return;
            }
            BattleSideEnum side = GetGeneralSide(peer);
            if (side != BattleSideEnum.None)
            {
                ReleaseGeneralCommand(side, "the general fell");
            }
        }

        private void ClearGeneralCommandState()
        {
            _generalCommandRates.Clear();
            _pendingSuggestions.Clear();
        }

        // --- Formations a player leads: suggestions ---

        private static int SuggestionKey(BattleSideEnum side, int formationIndex)
        {
            return (int)side * 16 + formationIndex;
        }

        private void CollectSuggestion(BattleSideEnum side, Formation formation, RequestGeneralCommand command)
        {
            int formationIndex = (int)formation.FormationIndex;
            int key = SuggestionKey(side, formationIndex);
            _pendingSuggestions.TryGetValue(key, out GeneralSuggestion pending);
            switch (command.Payload)
            {
                case GeneralCommandPayload.Direction:
                    // A move line's facing joins its move; a facing order alone is not suggested.
                    if (pending != null && pending.Kind == GeneralSuggestionKind.Move)
                    {
                        pending.SetDirection(command.Direction.Normalized());
                    }
                    return;
                case GeneralCommandPayload.Width:
                    if (pending != null && pending.Kind == GeneralSuggestionKind.Move)
                    {
                        pending.SetWidth(command.Width);
                    }
                    return;
            }
            if (command.OrderType == OrderType.LookAtDirection || !GeneralCommands.TryGetSuggestionKind(command.OrderType, out GeneralSuggestionKind kind))
            {
                return;
            }
            GeneralSuggestion suggestion = new GeneralSuggestion(side, formationIndex, kind);
            if (command.Payload == GeneralCommandPayload.Position)
            {
                suggestion.SetPosition(command.Position.AsVec2);
            }
            else if (command.Payload == GeneralCommandPayload.Formation)
            {
                suggestion.SetTarget(command.TargetSide, command.TargetFormationIndex);
            }
            _pendingSuggestions[key] = suggestion;
        }

        // Once a tick, after the general's messages of the tick were handled. Never a broadcast: only the formation's
        // commander and the general hear of it.
        private void SendPendingSuggestions()
        {
            if (_pendingSuggestions.Count == 0)
            {
                return;
            }
            foreach (GeneralSuggestion suggestion in _pendingSuggestions.Values)
            {
                FormationSlot slot = Slots.GetSlot(suggestion.Side, suggestion.FormationIndex);
                NetworkCommunicator commander = slot?.Commander;
                NetworkCommunicator general = Slots.GetSlot(suggestion.Side, CommanderBattleSettings.GeneralSlot)?.Commander;
                if (commander != null && commander.IsConnectionActive)
                {
                    SendSuggestionTo(commander, suggestion);
                }
                if (general != null && general != commander && general.IsConnectionActive)
                {
                    SendSuggestionTo(general, suggestion);
                }
                CommanderBattleLog.Detail($"{general?.UserName} ({suggestion.Side} general) suggested {suggestion.Kind} to {commander?.UserName} ({slot?.DisplayName})");
            }
            _pendingSuggestions.Clear();
        }

        // A listen server's own player has no connection to send to; its client half takes the suggestion directly.
        private void SendSuggestionTo(NetworkCommunicator peer, GeneralSuggestion suggestion)
        {
            if (!peer.IsServerPeer)
            {
                CommanderBattleNetwork.SendTo(peer, suggestion);
                return;
            }
            Mission.GetMissionBehavior<MissionMultiplayerCommanderBattleClient>()?.AddGeneralSuggestion(suggestion);
        }
    }
}

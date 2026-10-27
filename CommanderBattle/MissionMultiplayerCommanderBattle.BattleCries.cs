using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace CommanderBattle
{
    // A commander's charge or advance order sets their soldiers shouting in a wave, a few men repeat every other
    // order down the ranks, and routing men cry out as they break (docs/plans/battle-cries.md). Server only: a voice made here with NoPrediction reaches every client
    // through the engine, as the stock multiplayer barks do (Agent.HandleBark makes the voice on the server only; the
    // BarkAgent message the clients get only prints the chat line).
    public partial class MissionMultiplayerCommanderBattle
    {
        // One cry per formation in this window; orders given in quick succession (charge, then charge a target)
        // would otherwise stack.
        private const float BattleCryCooldown = 10f;

        // Each soldier shouts somewhere in this window, so the cry rolls through the ranks.
        private const float BattleCryDelayMin = 0.2f;
        private const float BattleCryDelayMax = 2f;

        // How many soldiers shout: one in this many. All of them at once is a wall of noise.
        private const int ChargeCryOneIn = 2;
        private const int AdvanceCryOneIn = 4;

        // Of those shouting, one in this many calls the order ("Charge!", "Advance!") rather than a wordless yell.
        private const int CryWordOneIn = 4;

        // Every other order is passed down the ranks by a few men repeating its word, as file closers would.
        private const int EchoVoices = 3;
        private const float EchoDelayMin = 0.4f;
        private const float EchoDelayMax = 1.8f;

        // A commander dragging move lines gives an order a second; one echo per formation in this window.
        private const float EchoCooldown = 3f;

        // A man breaking cries out soon after; a whole formation routs in one tick, so spread them.
        private const float FearDelayMax = 1.2f;
        private const int FearOneIn = 2;

        private readonly struct QueuedVoice
        {
            public QueuedVoice(Agent agent, float time, SkinVoiceManager.SkinVoiceType voice, bool isBattleCry)
            {
                Agent = agent;
                Time = time;
                Voice = voice;
                IsBattleCry = isBattleCry;
            }

            public Agent Agent { get; }

            public float Time { get; }

            public SkinVoiceManager.SkinVoiceType Voice { get; }

            public bool IsBattleCry { get; }
        }

        private readonly List<QueuedVoice> _queuedVoices = new List<QueuedVoice>();

        private readonly Dictionary<Formation, float> _lastBattleCryTimes = new Dictionary<Formation, float>();

        private readonly Dictionary<Formation, float> _lastEchoTimes = new Dictionary<Formation, float>();

        private readonly List<Agent> _echoCandidates = new List<Agent>();

        // Team.OnOrderIssued covers every player's order controller (Team.GetOrderControllerOf); the general's own
        // controller for the AI's formations isn't one of the team's, so it is hooked when made (GetGeneralOrderController).
        private void HookBattleCries()
        {
            foreach (BattleSideEnum side in CommanderBattleTeams.Sides)
            {
                Team team = CommanderBattleTeams.GetTeam(Mission, side);
                if (team != null)
                {
                    team.OnOrderIssued += OnOrderIssuedForBattleCry;
                }
            }
        }

        private void UnhookBattleCries()
        {
            foreach (BattleSideEnum side in CommanderBattleTeams.Sides)
            {
                Team team = CommanderBattleTeams.GetTeam(Mission, side);
                if (team != null)
                {
                    team.OnOrderIssued -= OnOrderIssuedForBattleCry;
                }
            }
        }

        private void OnOrderIssuedForBattleCry(OrderType orderType, MBReadOnlyList<Formation> appliedFormations, OrderController orderController, params object[] delegateParams)
        {
            SkinVoiceManager.SkinVoiceType? word = GetOrderWord(orderType);
            if (word == null)
            {
                return;
            }
            bool charge = orderType == OrderType.Charge || orderType == OrderType.ChargeWithTarget;
            bool cry = charge || orderType == OrderType.Advance;
            // A commander's order: a player's controller, or the general's for the AI's formations. The team AI's
            // own orders don't come through here, but a master controller used by anything else stays quiet.
            bool fromGeneral = Array.IndexOf(_generalOrderControllers, orderController) >= 0;
            Agent commander = orderController.Owner;
            if (!fromGeneral && commander?.MissionPeer == null)
            {
                return;
            }
            float now = Mission.CurrentTime;
            string by = commander?.MissionPeer?.GetNetworkPeer()?.UserName ?? "the general";
            foreach (Formation formation in appliedFormations)
            {
                string name = Slots.GetSlot(formation.Team.Side, (int)formation.FormationIndex)?.DisplayName ?? formation.FormationIndex.ToString();
                if (cry && (!_lastBattleCryTimes.TryGetValue(formation, out float lastCry) || now - lastCry >= BattleCryCooldown))
                {
                    _lastBattleCryTimes[formation] = now;
                    _lastEchoTimes[formation] = now;
                    int shouting = QueueBattleCry(formation, commander, charge, word.Value, now);
                    CommanderBattleLog.Detail($"{by} ordered {orderType}: {shouting} of {name} shout");
                    continue;
                }
                // A cry still on cooldown falls back to the echo: a second charge order is still passed on.
                if (_lastEchoTimes.TryGetValue(formation, out float lastEcho) && now - lastEcho < EchoCooldown)
                {
                    continue;
                }
                _lastEchoTimes[formation] = now;
                int echoing = QueueEcho(formation, commander, word.Value, now);
                CommanderBattleLog.Detail($"{by} ordered {orderType}: {echoing} of {name} repeat it");
            }
        }

        private int QueueBattleCry(Formation formation, Agent commander, bool charge, SkinVoiceManager.SkinVoiceType word, float now)
        {
            int shouting = 0;
            formation.ApplyActionOnEachUnit(agent =>
            {
                if (agent == commander || !agent.IsHuman || !agent.IsAIControlled || MBRandom.RandomInt(charge ? ChargeCryOneIn : AdvanceCryOneIn) != 0)
                {
                    return;
                }
                _queuedVoices.Add(new QueuedVoice(agent, now + MBRandom.RandomFloatRanged(BattleCryDelayMin, BattleCryDelayMax), PickBattleCry(agent, word), isBattleCry: true));
                shouting++;
            });
            return shouting;
        }

        // A few men picked at random through the formation, so the order is heard along the line, not from one spot.
        private int QueueEcho(Formation formation, Agent commander, SkinVoiceManager.SkinVoiceType word, float now)
        {
            _echoCandidates.Clear();
            formation.ApplyActionOnEachUnit(agent =>
            {
                if (agent != commander && agent.IsHuman && agent.IsAIControlled)
                {
                    _echoCandidates.Add(agent);
                }
            });
            int echoing = Math.Min(EchoVoices, _echoCandidates.Count);
            for (int i = 0; i < echoing; i++)
            {
                int pick = i + MBRandom.RandomInt(_echoCandidates.Count - i);
                Agent agent = _echoCandidates[pick];
                _echoCandidates[pick] = _echoCandidates[i];
                _queuedVoices.Add(new QueuedVoice(agent, now + MBRandom.RandomFloatRanged(EchoDelayMin, EchoDelayMax), word, isBattleCry: true));
            }
            _echoCandidates.Clear();
            return echoing;
        }

        // Riders yell to their horses as the stock charge yell does (Agent.Tick); on foot, some call the order.
        private static SkinVoiceManager.SkinVoiceType PickBattleCry(Agent agent, SkinVoiceManager.SkinVoiceType word)
        {
            if (agent.MountAgent != null)
            {
                return SkinVoiceManager.VoiceType.HorseRally;
            }
            return MBRandom.RandomInt(CryWordOneIn) == 0 ? word : SkinVoiceManager.VoiceType.Yell;
        }

        // The word the commander calls for each order (the stock OrderController.PlayOrderGestures). Handing a
        // formation to the AI or back isn't an order to the men, and this mode has no naval battles.
        private static SkinVoiceManager.SkinVoiceType? GetOrderWord(OrderType orderType)
        {
            switch (orderType)
            {
                case OrderType.FireAtWill: return SkinVoiceManager.VoiceType.FireAtWill;
                case OrderType.HoldFire: return SkinVoiceManager.VoiceType.HoldFire;
                case OrderType.Mount: return SkinVoiceManager.VoiceType.Mount;
                case OrderType.Dismount: return SkinVoiceManager.VoiceType.Dismount;
                case OrderType.Move:
                case OrderType.MoveToLineSegment:
                case OrderType.MoveToLineSegmentWithHorizontalLayout:
                    return SkinVoiceManager.VoiceType.Move;
                case OrderType.Charge:
                case OrderType.ChargeWithTarget:
                    return SkinVoiceManager.VoiceType.Charge;
                case OrderType.FollowMe: return SkinVoiceManager.VoiceType.Follow;
                case OrderType.Retreat: return SkinVoiceManager.VoiceType.Retreat;
                case OrderType.AdvanceTenPaces:
                case OrderType.Advance:
                    return SkinVoiceManager.VoiceType.Advance;
                case OrderType.FallBackTenPaces:
                case OrderType.FallBack:
                    return SkinVoiceManager.VoiceType.FallBack;
                case OrderType.StandYourGround: return SkinVoiceManager.VoiceType.Stop;
                case OrderType.ArrangementLine: return SkinVoiceManager.VoiceType.FormLine;
                case OrderType.ArrangementCloseOrder: return SkinVoiceManager.VoiceType.FormShieldWall;
                case OrderType.ArrangementLoose: return SkinVoiceManager.VoiceType.FormLoose;
                case OrderType.ArrangementCircular: return SkinVoiceManager.VoiceType.FormCircle;
                case OrderType.ArrangementSchiltron: return SkinVoiceManager.VoiceType.FormSquare;
                case OrderType.ArrangementVee: return SkinVoiceManager.VoiceType.FormSkein;
                case OrderType.ArrangementColumn: return SkinVoiceManager.VoiceType.FormColumn;
                case OrderType.ArrangementScatter: return SkinVoiceManager.VoiceType.FormScatter;
                case OrderType.LookAtEnemy: return SkinVoiceManager.VoiceType.FaceEnemy;
                case OrderType.LookAtDirection: return SkinVoiceManager.VoiceType.FaceDirection;
                default: return null;
            }
        }

        // Raised by the stock MissionAgentPanicHandler when an agent starts to rout. The stock Debacle cry comes from
        // AgentMoraleInteractionLogic, which this mode doesn't run.
        public override void OnAgentFleeing(Agent affectedAgent)
        {
            base.OnAgentFleeing(affectedAgent);
            if (affectedAgent != null && affectedAgent.IsHuman && affectedAgent.IsAIControlled && MBRandom.RandomInt(FearOneIn) == 0)
            {
                _queuedVoices.Add(new QueuedVoice(affectedAgent, Mission.CurrentTime + MBRandom.RandomFloat * FearDelayMax, SkinVoiceManager.VoiceType.Fear, isBattleCry: false));
            }
        }

        private void PlayQueuedVoices()
        {
            if (_queuedVoices.Count == 0)
            {
                return;
            }
            float now = Mission.CurrentTime;
            for (int i = _queuedVoices.Count - 1; i >= 0; i--)
            {
                QueuedVoice voice = _queuedVoices[i];
                if (voice.Time > now)
                {
                    continue;
                }
                _queuedVoices.RemoveAt(i);
                Agent agent = voice.Agent;
                if (!agent.IsActive())
                {
                    continue;
                }
                // A man who broke between the order and his turn doesn't cheer the charge on.
                if (voice.IsBattleCry && (agent.IsRunningAway || (agent.CommonAIComponent != null && agent.CommonAIComponent.IsRetreating)))
                {
                    continue;
                }
                agent.MakeVoice(voice.Voice, SkinVoiceManager.CombatVoiceNetworkPredictionType.NoPrediction);
            }
        }

        private void ClearBattleCries()
        {
            _queuedVoices.Clear();
            _lastBattleCryTimes.Clear();
            _lastEchoTimes.Clear();
        }
    }
}

using System.Collections.Generic;
using NetworkMessages.FromClient;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Network.Messages;

namespace CommanderBattle
{
    // A commander's cheer rolls through their own formation (docs/plans/cheer-with-your-men.md). The stock handler
    // (MissionNetworkComponent) plays the commander's own taunt; this one runs after it and queues a cheer for each
    // of their AI soldiers. Actions and voices set here reach the clients through the engine, as the stock
    // multiplayer taunt and AgentVictoryLogic's cheers do.
    public partial class MissionMultiplayerCommanderBattle
    {
        // The stock cooldown is the client's only (4 s); this one keeps a commander from flooding the field.
        private const float CheerCooldown = 10f;

        // Each soldier joins in somewhere in this window, so the cheer rolls through the ranks.
        private const float CheerDelayMin = 0.1f;
        private const float CheerDelayMax = 1.5f;

        // A soldier with an enemy this close has better things to do.
        private const float CheerEnemyRange = 20f;

        // One soldier in this many also shouts; all of them at once is a wall of noise.
        private const int CheerVoiceOneIn = 3;

        // The stock victory cheers that also work mounted (AgentVictoryLogic's mid set).
        private static readonly ActionIndexCache[] CheerActions =
        {
            ActionIndexCache.act_cheer_1,
            ActionIndexCache.act_cheer_2,
            ActionIndexCache.act_cheer_3,
            ActionIndexCache.act_cheer_4,
        };

        private readonly struct QueuedCheer
        {
            public QueuedCheer(Agent agent, float time)
            {
                Agent = agent;
                Time = time;
            }

            public Agent Agent { get; }

            public float Time { get; }
        }

        private readonly List<QueuedCheer> _queuedCheers = new List<QueuedCheer>();

        private readonly Dictionary<NetworkCommunicator, float> _lastCheerTimes = new Dictionary<NetworkCommunicator, float>();

        // Always true: a false return stops the handlers after this one and marks the packet as invalid.
        private bool HandleClientEventTauntSelected(NetworkCommunicator peer, GameNetworkMessage baseMessage)
        {
            MissionPeer missionPeer = peer.GetComponent<MissionPeer>();
            Agent commander = missionPeer?.ControlledAgent;
            Formation formation = missionPeer?.ControlledFormation;
            if (commander == null || !commander.IsActive() || formation == null || commander.Formation != formation)
            {
                return true;
            }
            float now = Mission.CurrentTime;
            if (_lastCheerTimes.TryGetValue(peer, out float last) && now - last < CheerCooldown)
            {
                return true;
            }
            _lastCheerTimes[peer] = now;
            int joining = 0;
            formation.ApplyActionOnEachUnit(agent =>
            {
                if (agent != commander && agent.IsHuman && agent.IsAIControlled)
                {
                    _queuedCheers.Add(new QueuedCheer(agent, now + MBRandom.RandomFloatRanged(CheerDelayMin, CheerDelayMax)));
                    joining++;
                }
            });
            CommanderBattleLog.Detail($"{peer.UserName} cheered: {joining} of {Slots.FindSlotOf(peer)?.DisplayName ?? formation.FormationIndex.ToString()} join in");
            return true;
        }

        private void PlayQueuedCheers()
        {
            if (_queuedCheers.Count == 0)
            {
                return;
            }
            float now = Mission.CurrentTime;
            for (int i = _queuedCheers.Count - 1; i >= 0; i--)
            {
                QueuedCheer cheer = _queuedCheers[i];
                if (cheer.Time > now)
                {
                    continue;
                }
                _queuedCheers.RemoveAt(i);
                if (CanCheer(cheer.Agent))
                {
                    cheer.Agent.SetActionChannel(1, in CheerActions[MBRandom.RandomInt(CheerActions.Length)], ignorePriority: false, (AnimFlags)0uL);
                    if (MBRandom.RandomInt(CheerVoiceOneIn) == 0)
                    {
                        cheer.Agent.MakeVoice(SkinVoiceManager.VoiceType.Victory, SkinVoiceManager.CombatVoiceNetworkPredictionType.NoPrediction);
                    }
                }
            }
        }

        private bool CanCheer(Agent agent)
        {
            if (!agent.IsActive() || agent.Team == null || agent.IsUsingGameObject || agent.IsInWater())
            {
                return false;
            }
            // Idle on the upper body: not attacking, blocking, aiming, reloading or switching weapons.
            Agent.ActionCodeType action = agent.GetCurrentActionType(1);
            if (action != Agent.ActionCodeType.Idle && action != Agent.ActionCodeType.Other)
            {
                return false;
            }
            // Cheering with such an item in hand drops it (AgentVictoryLogic avoids it the same way).
            EquipmentIndex wielded = agent.GetPrimaryWieldedItemIndex();
            if (wielded != EquipmentIndex.None && agent.Equipment[wielded].Item.ItemFlags.HasAnyFlag(ItemFlags.DropOnAnyAction))
            {
                return false;
            }
            return !Mission.HasAnyAgentsOfSideInRange(agent.Position, CheerEnemyRange, agent.Team.Side.GetOppositeSide());
        }

        private void ClearCheers()
        {
            _queuedCheers.Clear();
            _lastCheerTimes.Clear();
        }
    }
}

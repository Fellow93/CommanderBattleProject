using System.Collections.Generic;
using RBM.Multiplayer.Network;
using RBMStance;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;

namespace RBM.Multiplayer.Stance
{
    // Server: feeds each player's posture HUD. RBM keeps posture and stamina in AgentStances.values on the server only,
    // and draws its singleplayer HUD straight from them; here each player gets their own values, and those of the last
    // agent they exchanged blows with, in a StanceHudUpdate sent when something changed (at most every 0.1 s) and
    // again every second when nothing did.
    public class StanceNetworkSync : MissionLogic
    {
        private const float SendInterval = 0.1f;

        // Unchanged values are sent again this often: the updates are unreliable, and a lost last one (bars back to
        // full, target gone) would otherwise stay lost.
        private const float ResendInterval = 1f;

        // True while a Commander Battle mission runs RBM's server behaviours (StanceAnimationSync broadcasts only then).
        internal static bool IsActive { get; private set; }

        // How long the target's bars stay up after the last exchange (RBM: 600 frames, about 10 s).
        private const float TargetDisplaySeconds = 10f;

        // A dead target stays up briefly (RBM: 200 frames).
        private const float DeadTargetDisplaySeconds = 3f;

        private sealed class PeerHud
        {
            public Agent Target;

            public float TargetUntil;

            public StanceHudUpdate LastSent;

            public float LastSentTime;
        }

        private readonly Dictionary<NetworkCommunicator, PeerHud> _huds = new Dictionary<NetworkCommunicator, PeerHud>();

        private float _sendTimer;

        public override void OnBehaviorInitialize()
        {
            base.OnBehaviorInitialize();
            IsActive = true;
        }

        public override void OnRemoveBehavior()
        {
            IsActive = false;
            base.OnRemoveBehavior();
        }

        public override void OnClearScene()
        {
            base.OnClearScene();
            _huds.Clear();
        }

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);
            _sendTimer += dt;
            if (_sendTimer < SendInterval || !RBMConfig.RBMConfig.postureEnabled || !RBMConfig.RBMConfig.postureGUIEnabled)
            {
                return;
            }
            _sendTimer = 0f;
            float now = Mission.CurrentTime;
            foreach (NetworkCommunicator peer in GameNetwork.NetworkPeers)
            {
                if (!peer.IsSynchronized || peer.IsServerPeer)
                {
                    continue;
                }
                Agent agent = peer.ControlledAgent;
                if (agent == null || !agent.IsActive() || !AgentStances.values.TryGetValue(agent, out RBMStance.Stance own))
                {
                    continue;
                }
                PeerHud hud = GetHud(peer);
                StanceHudUpdate message = Build(own, hud, now);
                if (hud.LastSent != null && SameValues(hud.LastSent, message) && now - hud.LastSentTime < ResendInterval)
                {
                    continue;
                }
                hud.LastSent = message;
                hud.LastSentTime = now;
                GameNetwork.BeginModuleEventAsServerUnreliable(peer);
                GameNetwork.WriteMessage(message);
                GameNetwork.EndModuleEventAsServerUnreliable();
            }
        }

        private static StanceHudUpdate Build(RBMStance.Stance own, PeerHud hud, float now)
        {
            Agent target = hud.Target;
            RBMStance.Stance targetStance = null;
            if (target != null && (now > hud.TargetUntil || target.Mission == null || !AgentStances.values.TryGetValue(target, out targetStance)))
            {
                hud.Target = null;
                target = null;
            }
            if (target == null || targetStance == null)
            {
                return new StanceHudUpdate((int)own.posture, (int)own.maxPosture, (int)own.stamina, (int)own.maxStamina, -1, 0, 0, 0, 0, 0, 0);
            }
            return new StanceHudUpdate((int)own.posture, (int)own.maxPosture, (int)own.stamina, (int)own.maxStamina,
                target.Index, (int)targetStance.posture, (int)targetStance.maxPosture, (int)targetStance.stamina, (int)targetStance.maxStamina,
                (int)target.Health, (int)target.HealthLimit);
        }

        private static bool SameValues(StanceHudUpdate a, StanceHudUpdate b)
        {
            return a.Posture == b.Posture && a.PostureMax == b.PostureMax && a.Stamina == b.Stamina && a.StaminaMax == b.StaminaMax
                && a.TargetAgentIndex == b.TargetAgentIndex && a.TargetPosture == b.TargetPosture && a.TargetPostureMax == b.TargetPostureMax
                && a.TargetStamina == b.TargetStamina && a.TargetStaminaMax == b.TargetStaminaMax
                && a.TargetHealth == b.TargetHealth && a.TargetHealthMax == b.TargetHealthMax;
        }

        private PeerHud GetHud(NetworkCommunicator peer)
        {
            if (!_huds.TryGetValue(peer, out PeerHud hud))
            {
                hud = new PeerHud();
                _huds[peer] = hud;
            }
            return hud;
        }

        // The player controlling this agent, or riding this horse.
        private static NetworkCommunicator GetPlayer(Agent agent)
        {
            if (agent == null)
            {
                return null;
            }
            MissionPeer peer = agent.MissionPeer ?? (agent.IsMount ? agent.RiderAgent?.MissionPeer : null);
            NetworkCommunicator communicator = peer?.GetNetworkPeer();
            return communicator != null && !communicator.IsServerPeer ? communicator : null;
        }

        private void ShowTarget(Agent player, Agent target, float seconds)
        {
            NetworkCommunicator peer = GetPlayer(player);
            if (peer == null || target == null || target == player)
            {
                return;
            }
            PeerHud hud = GetHud(peer);
            hud.Target = target;
            hud.TargetUntil = Mission.CurrentTime + seconds;
        }

        // As RBM's HUD: the agent a player hits, and both sides of every melee exchange (blocks included), which is
        // when posture moves.
        public override void OnAgentHit(Agent affectedAgent, Agent affectorAgent, in MissionWeapon affectorWeapon, in Blow blow, in AttackCollisionData attackCollisionData)
        {
            if (affectedAgent != null && affectorAgent != null)
            {
                ShowTarget(affectorAgent, affectedAgent, affectedAgent.Health <= 0f ? DeadTargetDisplaySeconds : TargetDisplaySeconds);
            }
        }

        public override void OnMeleeHit(Agent attacker, Agent victim, bool isCanceled, AttackCollisionData collisionData)
        {
            if (isCanceled || attacker == null || victim == null)
            {
                return;
            }
            ShowTarget(attacker, victim, TargetDisplaySeconds);
            ShowTarget(victim, attacker, TargetDisplaySeconds);
        }

        // RBM's posture crush-through: a blow that was blocked but still lands damage (its CrushThroughBlow builds it
        // from the blocked collision). RBM tells the player in a message, which on a server reaches nobody.
        public override void OnRegisterBlow(Agent attacker, Agent victim, WeakGameEntity realHitEntity, Blow b, ref AttackCollisionData collisionData, in MissionWeapon attackerWeapon)
        {
            if (victim == null || b.InflictedDamage <= 0 || !IsBlocked(collisionData.CollisionResult))
            {
                return;
            }
            Send(GetPlayer(victim), new StanceEvent(StanceEventType.OwnCrushThrough, b.InflictedDamage, b.VictimBodyPart));
            Send(GetPlayer(attacker), new StanceEvent(StanceEventType.EnemyCrushThrough, b.InflictedDamage, b.VictimBodyPart));
        }

        private static bool IsBlocked(CombatCollisionResult result)
        {
            return result == CombatCollisionResult.Blocked || result == CombatCollisionResult.Parried || result == CombatCollisionResult.ChamberBlocked;
        }

        public static void SendExhausted(Agent agent)
        {
            Send(GetPlayer(agent), new StanceEvent(StanceEventType.Exhausted, 0, BoneBodyPartType.None));
        }

        public static void SendChamberBlockPostureBreak(Agent agent)
        {
            Send(GetPlayer(agent), new StanceEvent(StanceEventType.ChamberBlockPostureBreak, 0, BoneBodyPartType.None));
        }

        private static void Send(NetworkCommunicator peer, StanceEvent message)
        {
            if (peer == null || !peer.IsSynchronized)
            {
                return;
            }
            GameNetwork.BeginModuleEventAsServer(peer);
            GameNetwork.WriteMessage(message);
            GameNetwork.EndModuleEventAsServer();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace RBM.Multiplayer
{
    // Server side of RBM in a Commander Battle mission: the housekeeping RBM's singleplayer SubModule does per mission,
    // done per round here, since rounds reuse the mission, its teams and formations.
    public class RbmMpServerLogic : MissionLogic
    {
        // RBM reads the battlefield once both armies stand on it (BattleTerrainLogic, singleplayer: after deployment).
        // A round has no deployment: the read starts once both sides have men and nobody has spawned for this long.
        private const float ArmiesSettledSeconds = 2f;

        // MissionBehavior.Mission is set by Mission.AddMissionBehavior; the terrain logic is driven from here instead
        // (see TickTerrain), so it is set by hand.
        private static readonly MethodInfo MissionSetter = AccessTools.PropertySetter(typeof(MissionBehavior), nameof(MissionBehavior.Mission));

        // Agents of players whose stats were last rebuilt while a player controlled them.
        private readonly HashSet<Agent> _playerAgents = new HashSet<Agent>();

        private readonly List<Agent> _rebuild = new List<Agent>();

        private bool _tacticsDue = true;

        // This round's terrain reader; null until the armies are on the field.
        private RBMAI.BattleTerrainLogic _terrain;

        private bool _terrainDue = true;

        private bool _terrainPublished;

        private float _terrainStartedAt;

        private const float TacticLogInterval = 10f;

        private float _tacticLogTimer;

        private string _lastTactics;

        private const float FormationLogInterval = 30f;

        private float _formationLogTimer;

        private int _agentCount = -1;

        private float _agentCountSince;

        // Main thread, at the top of Mission.AfterStart (RBM's SubModule.OnBeforeMissionBehaviorInitialize) and
        // when a round's scene is cleared: drops every RBM static holding the last battle's agents and formations.
        public static void ResetRbmState(string reason)
        {
            try
            {
                RBMAI.MissionStartReset.Reset();
                RBMStance.StanceReset.Reset();
                RbmMpLog.Detail("RBM state reset: " + reason);
            }
            catch (Exception e)
            {
                RbmMpLog.Write($"RBM state reset ({reason}) failed: {e}");
            }
        }

        public override void OnClearScene()
        {
            base.OnClearScene();
            _playerAgents.Clear();
            AI.TargetFrameClearSync.Reset();
            EndTerrain();
            ResetRbmState("round reset");
            // Not here: the scene is mid-reset (agents deleted, teams not reset yet). Next tick.
            _tacticsDue = true;
            _terrainDue = true;
        }

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);
            if (_tacticsDue && RBMConfig.RBMConfig.rbmAiEnabled)
            {
                _tacticsDue = false;
                Guarded("tactics", () => RbmMpTactics.Register(Mission));
            }
            TickTerrain(dt);
            LogTactics(dt);
            LogFormations(dt);
            // Writes what RBM's AI tick guard caught on any thread (RBM's RBMAIPatchLogic does the same).
            RBMAI.AiTickGuard.Flush();
            AI.TargetFrameClearSync.Flush();
            RebuildTakenOverAgents();
        }

        protected override void OnEndMission()
        {
            EndTerrain();
            base.OnEndMission();
        }

        public override void OnRemoveBehavior()
        {
            EndTerrain();
            base.OnRemoveBehavior();
        }

        // RBM's terrain reader, one per round. Not a behaviour of the mission: its read is due as soon as deployment is
        // over, which a multiplayer mission is from the start, before any soldier stands on the field. So it is made
        // once the armies have settled, and ticked from here.
        private void TickTerrain(float dt)
        {
            if (!RBMConfig.RBMConfig.rbmAiEnabled)
            {
                return;
            }
            if (_terrain != null)
            {
                RBMAI.BattleTerrainLogic terrain = _terrain;
                Guarded("terrain tick", () => terrain.OnMissionTick(dt));
                if (!_terrainPublished && RBMAI.BattleTerrain.Current != null)
                {
                    _terrainPublished = true;
                    RbmMpLog.Detail($"Terrain read published after {Mission.CurrentTime - _terrainStartedAt:0.0} s");
                }
                return;
            }
            if (!_terrainDue || !Mission.AllowAiTicking || Mission.AttackerTeam == null || Mission.DefenderTeam == null
                || Mission.AttackerTeam.ActiveAgents.Count == 0 || Mission.DefenderTeam.ActiveAgents.Count == 0)
            {
                _agentCount = -1;
                return;
            }
            int count = Mission.Agents.Count;
            if (count != _agentCount)
            {
                _agentCount = count;
                _agentCountSince = Mission.CurrentTime;
                return;
            }
            if (Mission.CurrentTime - _agentCountSince < ArmiesSettledSeconds)
            {
                return;
            }
            _terrainDue = false;
            Guarded("terrain start", () =>
            {
                RBMAI.BattleTerrainLogic terrain = new RBMAI.BattleTerrainLogic();
                MissionSetter.Invoke(terrain, new object[] { Mission });
                long startTicks = DateTime.UtcNow.Ticks;
                terrain.AfterStart();
                _terrain = terrain;
                _terrainPublished = false;
                _terrainStartedAt = Mission.CurrentTime;
                RbmMpLog.Detail($"Terrain read started ({count} agents on the field), grid built in {(DateTime.UtcNow.Ticks - startTicks) / TimeSpan.TicksPerMillisecond} ms");
            });
        }

        // Which tactic each side's AI is on, when it changes (RBM's TacticSelection reads the team AI's current one).
        private void LogTactics(float dt)
        {
            _tacticLogTimer += dt;
            if (_tacticLogTimer < TacticLogInterval || !RBMConfig.RBMConfig.rbmAiEnabled)
            {
                return;
            }
            _tacticLogTimer = 0f;
            string tactics = "";
            string line = "";
            foreach (Team team in Mission.Teams)
            {
                if (team.HasTeamAi)
                {
                    TacticComponent tactic = RBMAI.TacticSelection.CurrentTacticRef(team.TeamAI);
                    tactics += $"{team.Side} {tactic?.GetType().Name ?? "none"}; ";
                    line += $"{team.Side} {tactic?.GetType().Name ?? "none"} ({team.ActiveAgents.Count} agents); ";
                }
            }
            if (tactics != _lastTactics)
            {
                _lastTactics = tactics;
                RbmMpLog.Detail("Tactics: " + line);
            }
        }

        // Every formation with men in it, so a formation the AI leaves idle shows up: who leads it, what it is, the
        // behaviour and order it is on, and how many of its men stand still (and are not fighting in melee).
        private void LogFormations(float dt)
        {
            _formationLogTimer += dt;
            if (_formationLogTimer < FormationLogInterval || !Mission.AllowAiTicking)
            {
                return;
            }
            _formationLogTimer = 0f;
            foreach (Team team in Mission.Teams)
            {
                if (!team.HasTeamAi)
                {
                    continue;
                }
                System.Text.StringBuilder line = new System.Text.StringBuilder();
                foreach (Formation formation in team.FormationsIncludingEmpty)
                {
                    if (formation.CountOfUnits == 0)
                    {
                        continue;
                    }
                    int standing = 0;
                    List<Agent> idle = new List<Agent>();
                    formation.ApplyActionOnEachUnit(agent =>
                    {
                        if (agent.IsActive() && agent.Velocity.AsVec2.LengthSquared < 0.04f && !agent.IsRunningAway
                            && (agent.GetTargetAgent() == null || agent.Position.DistanceSquared(agent.GetTargetAgent().Position) > 9f))
                        {
                            standing++;
                            if (idle.Count < IdleSamplesPerFormation && NearestEnemyDistance(agent) < IdleEnemyDistance)
                            {
                                idle.Add(agent);
                            }
                        }
                    });
                    FormationQuerySystem query = formation.QuerySystem;
                    string kind = query.IsRangedCavalryFormation ? "horse archers" : query.IsCavalryFormation ? "cavalry" : query.IsRangedFormation ? "ranged" : "infantry";
                    string leader = formation.IsAIControlled ? "AI" : "player";
                    BehaviorComponent behavior = formation.AI.ActiveBehavior;
                    line.Append($"\n  f{(int)formation.FormationIndex} {kind}, {formation.CountOfUnits} men, {leader}, {behavior?.GetType().Name ?? "no behaviour"}, order {formation.GetReadonlyMovementOrderReference().OrderEnum}, {standing} standing");
                    if (RBMConfig.RBMConfig.frontlineEnabled && (kind == "infantry" || kind == "ranged"))
                    {
                        line.Append(", ").Append(DescribeFrontline(formation));
                    }
                    foreach (Agent agent in idle)
                    {
                        line.Append("\n    idle next to an enemy: ").Append(DescribeAgent(agent));
                    }
                }
                RbmMpLog.Detail($"Formations {team.Side}:{line}");
            }
            if (RBMConfig.RBMConfig.frontlineEnabled)
            {
                RbmMpLog.Detail($"Frontline this round: {System.Threading.Interlocked.Read(ref RBMAI.Frontline.ObstacleProbeQueries)} obstacle probes, "
                    + $"{System.Threading.Interlocked.Read(ref RBMAI.Frontline.ObstacleUnreachableRejects)} steps rejected as unreachable, "
                    + $"{AI.TargetFrameClearSync.SentThisRound} hold releases sent to clients, {AI.TargetFrameClearSync.SkippedThisRound} not sent (clients never had the hold)");
            }
        }

        // What RBM's Frontline does with an infantry or ranged formation's men: it only runs on a charge, and only for a
        // formation larger than frontlineMinFormationSize. "Deciding" counts men with a live decision timer, which is set
        // on the charge and not cleared when the charge ends, so it can lag a change of order. "Held" is the live sign:
        // men held in place this moment, which in a charge are Frontline's pins. Decisions are written on the workers; a
        // stale read is fine here.
        private static string DescribeFrontline(Formation formation)
        {
            if (formation.CountOfUnitsWithoutDetachedOnes <= RBMConfig.RBMConfig.frontlineMinFormationSize)
            {
                return $"too small for Frontline (at most {RBMConfig.RBMConfig.frontlineMinFormationSize})";
            }
            int[] decisions = new int[Enum.GetValues(typeof(RBMAI.Frontline.AIMindset.AIDecision)).Length];
            int deciding = 0;
            int closingUp = 0;
            int held = 0;
            formation.ApplyActionOnEachUnit(agent =>
            {
                if (!agent.IsActive())
                {
                    return;
                }
                if (agent.MovementLockedState != AgentMovementLockedState.None)
                {
                    held++;
                }
                if (RBMAI.Frontline.aiDecisionCooldownDict.TryGetValue(agent, out RBMAI.Frontline.AIDecisionState state) && state != null
                    && state.AIMindset.AIDecisionTimer != null)
                {
                    RBMAI.Frontline.AIMindset mindset = state.AIMindset;
                    deciding++;
                    int decision = (int)mindset.currentDecision;
                    if (decision >= 0 && decision < decisions.Length)
                    {
                        decisions[decision]++;
                    }
                    if (mindset.currentDecision == RBMAI.Frontline.AIMindset.AIDecision.Attack && mindset.aheadAlly != null)
                    {
                        closingUp++;
                    }
                }
            });
            return $"Frontline {deciding} deciding (attack {decisions[(int)RBMAI.Frontline.AIMindset.AIDecision.Attack]}, "
                + $"back step {decisions[(int)RBMAI.Frontline.AIMindset.AIDecision.BackStep]}, "
                + $"find ally {decisions[(int)RBMAI.Frontline.AIMindset.AIDecision.FindAlly]}, "
                + $"flank {decisions[(int)RBMAI.Frontline.AIMindset.AIDecision.FlankAllyLeft]}/{decisions[(int)RBMAI.Frontline.AIMindset.AIDecision.FlankAllyRight]}, "
                + $"rest {decisions[(int)RBMAI.Frontline.AIMindset.AIDecision.Rest]}), {closingUp} closing up, {held} held";
        }

        private const int IdleSamplesPerFormation = 3;

        // An enemy this close (metres) and still standing: not fighting back.
        private const float IdleEnemyDistance = 5f;

        private float NearestEnemyDistance(Agent agent)
        {
            float nearest = float.MaxValue;
            foreach (Agent other in Mission.Agents)
            {
                if (other.IsActive() && other.IsHuman && other.Team != null && other.Team.IsEnemyOf(agent.Team))
                {
                    nearest = Math.Min(nearest, other.Position.Distance(agent.Position));
                }
            }
            return nearest;
        }

        // What an idle soldier is doing, for the formation log.
        private string DescribeAgent(Agent agent)
        {
            MissionWeapon weapon = agent.WieldedWeapon;
            string wielded = weapon.IsEmpty ? "nothing" : weapon.CurrentUsageItem?.WeaponClass.ToString() ?? "?";
            EquipmentIndex index = agent.GetPrimaryWieldedItemIndex();
            string ammo = index != EquipmentIndex.None && weapon.CurrentUsageItem != null && weapon.CurrentUsageItem.IsRangedWeapon ? $", ammo {agent.Equipment.GetAmmoAmount(index)}" : "";
            Agent target = agent.GetTargetAgent();
            string stance = RBMStance.AgentStances.values.TryGetValue(agent, out RBMStance.Stance s) ? $"posture {s.posture:0}/{s.maxPosture:0}, stamina {s.stamina:0}/{s.maxStamina:0}" : "no stance";
            AgentDrivenProperties properties = agent.AgentDrivenProperties;
            return $"#{agent.Index} {agent.Character?.StringId}, wields {wielded}{ammo}, controller {agent.Controller}, AI {agent.IsAIControlled}, "
                + $"target {(target == null ? "none" : target.Index + " at " + target.Position.Distance(agent.Position).ToString("0.0") + " m")}, "
                + $"enemy at {NearestEnemyDistance(agent):0.0} m, actions {agent.GetCurrentActionType(0)}/{agent.GetCurrentActionType(1)}, "
                + $"{(agent.IsDetachedFromFormation ? "detached, " : "")}{(agent.IsUsingGameObject ? "using object, " : "")}{stance}, "
                + $"attack chance {properties.AIDecideOnAttackChance:0.00}, shoot freq {properties.AiShootFreq:0.00}, block {properties.AIBlockOnDecideAbility:0.00}";
        }

        private void EndTerrain()
        {
            RBMAI.BattleTerrainLogic terrain = _terrain;
            _terrain = null;
            if (terrain != null)
            {
                Guarded("terrain end", terrain.OnRemoveBehavior);
            }
        }

        private static void Guarded(string step, Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                RbmMpLog.Write($"{step} failed: {e}");
            }
        }

        // A commander taking over one of their soldiers (Mission.ReplaceBotWithPlayer) keeps the soldier's stats, which
        // RBM built for an AI. Rebuilt once a player controls the agent, so PlayerStatGuard puts back the stock values
        // the player's client predicts with.
        private void RebuildTakenOverAgents()
        {
            _rebuild.Clear();
            foreach (NetworkCommunicator peer in GameNetwork.NetworkPeers)
            {
                Agent agent = peer.ControlledAgent;
                if (agent != null && agent.IsActive() && agent.IsHuman && agent.MissionPeer != null && !_playerAgents.Contains(agent))
                {
                    _rebuild.Add(agent);
                }
            }
            if (_rebuild.Count == 0)
            {
                return;
            }
            _playerAgents.RemoveWhere(agent => !agent.IsActive() || agent.MissionPeer == null);
            foreach (Agent agent in _rebuild)
            {
                _playerAgents.Add(agent);
                agent.UpdateAgentStats();
            }
        }
    }
}

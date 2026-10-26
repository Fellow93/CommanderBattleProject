using System.Collections.Generic;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using MathF = TaleWorlds.Library.MathF;

namespace CommanderBattle
{
    // Fleeing soldiers leave the battle at the edge of the map, or after fleeing for two minutes. The stock rule
    // (CommonAIComponent) fades a fleeing agent out only within a few metres of its flee point or of the boundary;
    // on the campaign battle maps they often end up milling near the edge without getting that close, and the round,
    // which waits for a side to have nobody left, never ends. Server only.
    public class CommanderBattleFleeingLogic : MissionLogic
    {
        // A fleeing agent this close to the boundary (or past it) has left the battle.
        private const float EdgeDistance = 25f;

        // ...and so has one that has been fleeing this long without getting there (stuck behind terrain, or
        // running along the edge without reaching it).
        private const float MaxFleeingSeconds = 120f;

        private const float CheckInterval = 1f;

        private const float StatusInterval = 30f;

        private readonly Dictionary<Agent, float> _fleeingSince = new Dictionary<Agent, float>();

        private readonly List<Agent> _gone = new List<Agent>();

        private float _checkTimer;

        private float _statusTimer;

        private int _leftThisRound;

        public override void OnClearScene()
        {
            base.OnClearScene();
            _fleeingSince.Clear();
            _leftThisRound = 0;
        }

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);
            _checkTimer += dt;
            _statusTimer += dt;
            if (_checkTimer < CheckInterval)
            {
                return;
            }
            _checkTimer = 0f;
            float now = Mission.CurrentTime;
            int fleeing = 0;
            float nearestEdge = float.MaxValue;
            float longest = 0f;
            foreach (Agent agent in Mission.Agents)
            {
                if (!IsFleeing(agent))
                {
                    continue;
                }
                fleeing++;
                if (!_fleeingSince.TryGetValue(agent, out float since))
                {
                    since = now;
                    _fleeingSince[agent] = now;
                }
                longest = MathF.Max(longest, now - since);
                Vec2 position = agent.Position.AsVec2;
                float edge = Mission.IsPositionInsideBoundaries(position) ? position.Distance(Mission.GetClosestBoundaryPosition(position)) : 0f;
                nearestEdge = MathF.Min(nearestEdge, edge);
                if (edge < EdgeDistance || now - since > MaxFleeingSeconds)
                {
                    agent.StartFadingOut();
                    _leftThisRound++;
                }
            }
            _gone.Clear();
            foreach (Agent agent in _fleeingSince.Keys)
            {
                if (!agent.IsActive() || agent.IsFadingOut())
                {
                    _gone.Add(agent);
                }
            }
            foreach (Agent agent in _gone)
            {
                _fleeingSince.Remove(agent);
            }
            if (_statusTimer >= StatusInterval)
            {
                _statusTimer = 0f;
                if (fleeing > 0 || _leftThisRound > 0)
                {
                    CommanderBattleLog.Detail($"Fleeing: {fleeing} now (nearest {(fleeing > 0 ? nearestEdge.ToString("0") : "-")} m from the edge, longest {longest:0} s), {_leftThisRound} left the field this round");
                }
            }
        }

        // A soldier or a riderless horse running from the battle (the stock CanAgentRout test), not yet fading out.
        private static bool IsFleeing(Agent agent)
        {
            return agent.IsActive() && agent.IsAIControlled && agent.RiderAgent == null && !agent.IsFadingOut()
                && (agent.IsRunningAway || (agent.CommonAIComponent != null && agent.CommonAIComponent.IsRetreating));
        }
    }
}

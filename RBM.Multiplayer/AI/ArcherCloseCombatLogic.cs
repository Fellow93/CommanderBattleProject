using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace RBM.Multiplayer.AI
{
    // Archers on foot defend themselves when an enemy closes in. RBM AI keeps them shooting instead:
    //  - its behaviour curves for a ranged formation on a move or charge order (AgentSpeedAndParams,
    //    SetBehaviorValueSet postfix) score the bow above the sword at any distance beyond ~2.5 m, while vanilla
    //    switches to the sword within ~19 m of the target; and the AI keeps whatever target it had, often 30-90 m
    //    away, with an enemy standing next to it;
    //  - its Frontline formation movement (FormationMovement, ParallelUpdateFormationMovement postfix) tells every AI
    //    soldier on a move order to keep to his formation slot, so he never steps to the man attacking him.
    // Here, an AI archer with a non-routing enemy within ThreatRadius gets vanilla's weapon curves, a fresh target
    // (the nearest enemy, if the engine picks a far one again) and leaves his slot, until no enemy has been that
    // close for CalmSeconds; then RBM's values come back. Server only.
    public class ArcherCloseCombatLogic : MissionLogic
    {
        private const float CheckInterval = 0.5f;

        private const float ThreatRadius = 4f;

        // A target this far away while an enemy is within ThreatRadius is the wrong one.
        private const float FarTargetDistance = 8f;

        private const float CalmSeconds = 3f;

        private const float StatusInterval = 30f;

        // Agent.Index -> in close combat. Read by the formation-movement patch on the worker threads; written on the
        // main thread only.
        internal static readonly bool[] InCloseCombat = new bool[4096];

        private sealed class State
        {
            public bool CarriesMelee;

            public float NextCheck;

            public bool Active;

            public float LastThreat;

            // The target was invalidated once this engagement; if the engine picks a far one again, the nearest
            // enemy is set as a scripted target.
            public bool Retargeted;

            public bool ForcedTarget;
        }

        private readonly Dictionary<Agent, State> _states = new Dictionary<Agent, State>();

        private readonly List<Agent> _stale = new List<Agent>();

        private readonly MBList<Agent> _nearby = new MBList<Agent>();

        private float _statusTimer;

        private int _engagedThisRound;

        private int _forcedThisRound;

        public override void OnClearScene()
        {
            base.OnClearScene();
            _states.Clear();
            Array.Clear(InCloseCombat, 0, InCloseCombat.Length);
            _engagedThisRound = 0;
            _forcedThisRound = 0;
        }

        public override void OnRemoveBehavior()
        {
            Array.Clear(InCloseCombat, 0, InCloseCombat.Length);
            base.OnRemoveBehavior();
        }

        public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
        {
            base.OnAgentRemoved(affectedAgent, affectorAgent, agentState, blow);
            if (_states.Remove(affectedAgent))
            {
                SetFlag(affectedAgent, false);
            }
        }

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);
            if (!RBMConfig.RBMConfig.rbmAiEnabled)
            {
                return;
            }
            float now = Mission.CurrentTime;
            foreach (Agent agent in Mission.Agents)
            {
                if (!IsCandidate(agent))
                {
                    continue;
                }
                if (!_states.TryGetValue(agent, out State state))
                {
                    state = new State
                    {
                        CarriesMelee = CarriesMeleeWeapon(agent),
                        // Spread over the interval: an army spawns in one frame.
                        NextCheck = now + CheckInterval * MBRandom.RandomFloat
                    };
                    _states[agent] = state;
                }
                if (!state.CarriesMelee || now < state.NextCheck)
                {
                    continue;
                }
                state.NextCheck = now + CheckInterval;
                Tick(agent, state, now);
            }
            ReleaseLeavers();
            _statusTimer += dt;
            if (_statusTimer >= StatusInterval)
            {
                _statusTimer = 0f;
                LogStatus();
            }
        }

        // An AI soldier on foot in a ranged formation, not a player's agent.
        private static bool IsCandidate(Agent agent)
        {
            return agent.IsActive() && agent.IsHuman && agent.IsAIControlled && agent.MissionPeer == null && !agent.HasMount
                && agent.HumanAIComponent != null && agent.Formation != null && agent.Formation.QuerySystem.IsRangedFormation
                && !agent.IsRetreating();
        }

        private void Tick(Agent agent, State state, float now)
        {
            Agent nearest = FindNearestThreat(agent);
            if (nearest != null)
            {
                state.LastThreat = now;
                if (!state.Active)
                {
                    Engage(agent, state);
                }
                CheckTarget(agent, state, nearest);
            }
            else if (state.Active && now - state.LastThreat >= CalmSeconds)
            {
                Release(agent, state);
            }
        }

        private Agent FindNearestThreat(Agent agent)
        {
            Vec2 position = agent.Position.AsVec2;
            Mission.GetNearbyEnemyAgents(position, ThreatRadius, agent.Team, _nearby);
            Agent nearest = null;
            float nearestDistanceSquared = float.MaxValue;
            for (int i = 0; i < _nearby.Count; i++)
            {
                Agent enemy = _nearby[i];
                if (!enemy.IsActive() || enemy.IsRunningAway)
                {
                    continue;
                }
                float distanceSquared = enemy.Position.AsVec2.DistanceSquared(position);
                if (distanceSquared < nearestDistanceSquared)
                {
                    nearestDistanceSquared = distanceSquared;
                    nearest = enemy;
                }
            }
            _nearby.Clear();
            return nearest;
        }

        private void Engage(Agent agent, State state)
        {
            state.Active = true;
            state.Retargeted = false;
            SetFlag(agent, true);
            ApplyCloseCombatValues(agent.HumanAIComponent);
            _engagedThisRound++;
        }

        // A far target (or none) while an enemy is at hand: let the engine choose again first; if it picks a far one
        // again, give it the nearest enemy.
        private void CheckTarget(Agent agent, State state, Agent nearest)
        {
            Agent target = agent.GetTargetAgent();
            bool targetIsFar = target == null || !target.IsActive()
                || target.Position.AsVec2.DistanceSquared(agent.Position.AsVec2) > FarTargetDistance * FarTargetDistance;
            if (!targetIsFar)
            {
                return;
            }
            if (!state.Retargeted)
            {
                state.Retargeted = true;
                agent.InvalidateTargetAgent();
                agent.ResetEnemyCaches();
                return;
            }
            if (!state.ForcedTarget)
            {
                state.ForcedTarget = true;
                agent.SetAutomaticTargetSelection(false);
                _forcedThisRound++;
            }
            agent.SetTargetAgent(nearest);
        }

        private void Release(Agent agent, State state)
        {
            state.Active = false;
            SetFlag(agent, false);
            if (state.ForcedTarget)
            {
                state.ForcedTarget = false;
                agent.SetAutomaticTargetSelection(true);
                agent.InvalidateTargetAgent();
            }
            RestoreValues(agent);
        }

        // Soldiers that left the ranged formations, mounted or became a player's while in close combat.
        private void ReleaseLeavers()
        {
            _stale.Clear();
            foreach (KeyValuePair<Agent, State> entry in _states)
            {
                if (!IsCandidate(entry.Key))
                {
                    _stale.Add(entry.Key);
                }
            }
            foreach (Agent agent in _stale)
            {
                State state = _states[agent];
                _states.Remove(agent);
                if (state.Active && agent.IsActive())
                {
                    Release(agent, state);
                }
                else
                {
                    SetFlag(agent, false);
                }
            }
        }

        private void LogStatus()
        {
            int engaged = 0;
            int forced = 0;
            foreach (State state in _states.Values)
            {
                if (state.Active)
                {
                    engaged++;
                }
                if (state.ForcedTarget)
                {
                    forced++;
                }
            }
            if (engaged > 0 || _engagedThisRound > 0)
            {
                RbmMpLog.Write($"Archer close combat: {engaged} fighting at close range now ({forced} on a given target), {_engagedThisRound} engaged this round, {_forcedThisRound} given the nearest enemy");
            }
        }

        private static bool CarriesMeleeWeapon(Agent agent)
        {
            for (EquipmentIndex i = EquipmentIndex.WeaponItemBeginSlot; i < EquipmentIndex.ExtraWeaponSlot; i++)
            {
                MissionWeapon weapon = agent.Equipment[i];
                if (weapon.IsEmpty || weapon.Item == null || weapon.Item.ItemType == ItemObject.ItemTypeEnum.Shield)
                {
                    continue;
                }
                foreach (WeaponComponentData usage in weapon.Item.Weapons)
                {
                    if (usage.IsMeleeWeapon && !usage.IsConsumable && !usage.IsRangedWeapon)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static void SetFlag(Agent agent, bool value)
        {
            if (agent.Index >= 0 && agent.Index < InCloseCombat.Length)
            {
                InCloseCombat[agent.Index] = value;
            }
        }

        internal static bool IsInCloseCombat(Agent agent)
        {
            return agent != null && agent.Index >= 0 && agent.Index < InCloseCombat.Length && InCloseCombat[agent.Index];
        }

        // Vanilla's DefaultMove weapon curves: the sword wins within ~19 m of the target.
        internal static void ApplyCloseCombatValues(HumanAIComponent component)
        {
            component.OverrideBehaviorParams(HumanAIComponent.AISimpleBehaviorKind.Melee, 8f, 7f, 5f, 20f, 0.01f);
            component.OverrideBehaviorParams(HumanAIComponent.AISimpleBehaviorKind.Ranged, 0.02f, 7f, 0.04f, 20f, 0.03f);
        }

        // Re-runs the order's value set (vanilla's values plus RBM's overrides). The last set is Overriden after any
        // OverrideBehaviorParams, so vanilla applies it again rather than skipping an unchanged set.
        private static void RestoreValues(Agent agent)
        {
            Formation formation = agent.Formation;
            if (formation == null || agent.HumanAIComponent == null)
            {
                return;
            }
            agent.HumanAIComponent.RefreshBehaviorValues(formation.GetReadonlyMovementOrderReference().OrderEnum, formation.ArrangementOrder.OrderEnum);
        }

        // RBM and vanilla re-apply the order's values when the order or arrangement changes; keep the close-combat
        // curves on while the fight lasts.
        [HarmonyPatch(typeof(HumanAIComponent), nameof(HumanAIComponent.SetBehaviorValueSet))]
        [HarmonyAfter("com.rbmai")]
        private static class KeepCloseCombatValuesPatch
        {
            private static void Postfix(HumanAIComponent __instance, Agent ___Agent)
            {
                if (IsInCloseCombat(___Agent))
                {
                    ApplyCloseCombatValues(__instance);
                }
            }
        }

        // A man in close combat leaves his slot, as vanilla lets a soldier of a broken-up formation do. Runs after
        // RBM's Frontline movement, which pins every AI soldier on a move order to his slot. Worker thread: reads
        // InCloseCombat only.
        [HarmonyPatch(typeof(HumanAIComponent), nameof(HumanAIComponent.ParallelUpdateFormationMovement))]
        [HarmonyAfter("com.rbmai")]
        private static class LeaveSlotPatch
        {
            private static readonly Action<HumanAIComponent, bool> SetShouldCatchUpWithFormation =
                AccessTools.MethodDelegate<Action<HumanAIComponent, bool>>(AccessTools.PropertySetter(typeof(HumanAIComponent), nameof(HumanAIComponent.ShouldCatchUpWithFormation)));

            private static void Postfix(HumanAIComponent __instance, Agent ___Agent)
            {
                if (!IsInCloseCombat(___Agent) || ___Agent.Controller != AgentControllerType.AI)
                {
                    return;
                }
                SetShouldCatchUpWithFormation?.Invoke(__instance, false);
                ___Agent.SetFormationIntegrityData(Vec2.Zero, Vec2.Zero, Vec2.Zero, 0f, 0f, shouldKeepWithFormationInsteadOfMovingToAgent: false);
            }
        }
    }
}

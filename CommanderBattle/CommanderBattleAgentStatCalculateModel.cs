using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace CommanderBattle
{
    // The stock multiplayer stats in Commander Battle missions, with Light mode's slower horses (HorseSpeedFactor) and
    // slower-firing bows and crossbows (BowFireSpeedFactor, CrossbowFireSpeedFactor, and the ReloadSpeedFactor pair).
    // Agent stats are computed on every machine and never synced, so this is registered on the server and every client
    // alike; a change on one side only would make riders rubber-band and shots mistimed. Other game modes keep the
    // stock values.
    // RBM mode (CombatModeSetting) has none of Light's factors but RBM Combat's own stats: its reload speed, its
    // horse speed, a horse's charge damage as its weight with the rider's (RBM Combat's charge formula expects it,
    // applied on the server), and its small shield catch box.
    public class CommanderBattleAgentStatCalculateModel : MultiplayerAgentStatCalculateModel
    {
        private Mission _checkedMission;

        private bool _isCommanderBattle;

        // RBM Combat scales its per-part armour by the driven armour over the gear's armour sum, to carry singleplayer
        // perks over (ArmorRework.ApplyDrivenArmorBonus). The stock MP model sets the driven armour to the class's flat
        // armor value (8 to 56) for a player and their troops, far below RBM gear's sums, which cut their armour to a
        // third or so and let arrows through. In RBM mode it is the gear's sum, as in custom battle, so the ratio is 1.
        // Only the server's blows read armour.
        public override void InitializeAgentStats(Agent agent, Equipment spawnEquipment, AgentDrivenProperties agentDrivenProperties, AgentBuildData agentBuildData)
        {
            base.InitializeAgentStats(agent, spawnEquipment, agentDrivenProperties, agentBuildData);
            if (agent.IsHuman && CombatModeSetting.IsRbm && IsCommanderBattle(agent.Mission))
            {
                agentDrivenProperties.ArmorHead = spawnEquipment.GetHeadArmorSum();
                agentDrivenProperties.ArmorTorso = spawnEquipment.GetHumanBodyArmorSum();
                agentDrivenProperties.ArmorLegs = spawnEquipment.GetLegArmorSum();
                agentDrivenProperties.ArmorArms = spawnEquipment.GetArmArmorSum();
            }
        }

        public override void UpdateAgentStats(Agent agent, AgentDrivenProperties agentDrivenProperties)
        {
            base.UpdateAgentStats(agent, agentDrivenProperties);
            if (!IsCommanderBattle(agent.Mission))
            {
                return;
            }
            bool rbm = CombatModeSetting.IsRbm;
            if (agent.IsMount)
            {
                if (!rbm)
                {
                    agentDrivenProperties.MountSpeed *= CommanderBattleSettings.HorseSpeedFactor;
                    return;
                }
                if (agent.RiderAgent != null)
                {
                    agentDrivenProperties.MountSpeed = RbmMountSpeed(agent);
                }
                agentDrivenProperties.MountChargeDamage = ChargeWeight(agent);
            }
            else if (agent.IsHuman)
            {
                if (!rbm)
                {
                    SlowRangedFire(agent, agentDrivenProperties);
                    return;
                }
                RbmReload(agent, agentDrivenProperties);
                // RBM Combat (DamageRework.HitReaction): stock gives a raised shield a 0.3 missile catch box, the
                // "magnet shield".
                agentDrivenProperties.AttributeShieldMissileCollisionBodySizeAdder = 0.01f;
            }
        }

        // The draw (bow) or raise (crossbow) runs at ThrustOrRangedReadySpeedMultiplier and taking the next bolt or
        // arrow at ReloadSpeed. Both follow the wielded weapon: the stats are rebuilt whenever the agent wields
        // another, so a melee weapon keeps its thrust speed.
        private void SlowRangedFire(Agent agent, AgentDrivenProperties agentDrivenProperties)
        {
            EquipmentIndex index = agent.GetPrimaryWieldedItemIndex();
            if (index == EquipmentIndex.None)
            {
                return;
            }
            WeaponComponentData weapon = agent.Equipment[index].CurrentUsageItem;
            float fireFactor;
            float reloadFactor;
            if (weapon?.WeaponClass == WeaponClass.Bow)
            {
                fireFactor = CommanderBattleSettings.BowFireSpeedFactor;
                reloadFactor = CommanderBattleSettings.BowReloadSpeedFactor;
            }
            else if (weapon?.WeaponClass == WeaponClass.Crossbow)
            {
                fireFactor = CommanderBattleSettings.CrossbowFireSpeedFactor;
                reloadFactor = CommanderBattleSettings.CrossbowReloadSpeedFactor;
            }
            else
            {
                return;
            }
            agentDrivenProperties.ThrustOrRangedReadySpeedMultiplier *= fireFactor;
            agentDrivenProperties.ReloadSpeed *= reloadFactor;
        }

        private void RbmReload(Agent agent, AgentDrivenProperties agentDrivenProperties)
        {
            EquipmentIndex index = agent.GetPrimaryWieldedItemIndex();
            WeaponComponentData weapon = index == EquipmentIndex.None ? null : agent.Equipment[index].CurrentUsageItem;
            float reload = weapon == null ? 0f : RbmReloadSpeed(agent, weapon);
            if (reload > 0f)
            {
                agentDrivenProperties.ReloadSpeed = reload;
            }
        }

        // RBM Combat's reload speed for an AI archer (RangedRework.OverrideSetAiRelatedProperties), here for players
        // too: by launcher type and the skill with RBM's diminishing returns. 0 for a launcher type RBM leaves stock.
        private float RbmReloadSpeed(Agent agent, WeaponComponentData weapon)
        {
            int skill = weapon.RelevantSkill != null ? GetEffectiveSkill(agent, weapon.RelevantSkill) : 0;
            float skillWithDR = 600f / (600f + skill) * skill;
            switch (weapon.ItemUsage)
            {
                case "bow":
                case "long_bow":
                    return 0.25f * (1f + 0.016f * skillWithDR);
                case "crossbow_fast":
                    return 0.3f * (1f + 0.0045f * skillWithDR);
                case "crossbow":
                    return 0.2f * (1f + 0.0045f * skillWithDR);
                default:
                    return 0f;
            }
        }

        // RBM Combat's mount speed for a ridden horse (HorseChanges.MountStats, the custom battle variant), without the
        // banner bonus. The rider's mastery (riding skill over the horse's difficulty) scales the horse's speed 0.9 to
        // 1.1; above the base speed, the load (horse, harness, rider, armour, weapons) pulls it back towards the base.
        // The weather is the round's pick, which every machine draws from the round's seed: the dedicated server shows
        // no weather, so its scene has no rain to read. Commander Battle has no night, so RBM's night penalty never
        // applies. shortcut: an admin's weather change mid-round reaches a horse at its next stat update, which may
        // come at a different moment on each machine; recompute the mounts on that message if riders rubber-band.
        private float RbmMountSpeed(Agent mount)
        {
            Agent rider = mount.RiderAgent;
            Equipment equipment = mount.SpawnEquipment;
            EquipmentElement horse = equipment[EquipmentIndex.ArmorItemEndSlot];
            EquipmentElement harness = equipment[EquipmentIndex.HorseHarness];
            const int masteryThreshold = 75;
            int mastery = GetEffectiveSkill(rider, DefaultSkills.Riding) - horse.Item.Difficulty;
            mastery = mastery < 0 ? 0 : mastery > masteryThreshold ? masteryThreshold : mastery;
            float masteryModifier = 0.9f + 0.2f * mastery / masteryThreshold;
            float sceneModifier = IsRoundRaining(mount.Mission) ? 0.9f : 1f;
            float speedStat = horse.GetModifiedMountSpeed(in harness) + 1;
            if (harness.Item == null)
            {
                speedStat *= 0.9f;
            }
            float speed = sceneModifier * 0.22f * (1f + speedStat) * masteryModifier;
            float baseSpeed = horse.Weight > 500f ? 8f : 10f;
            if (speed > baseSpeed)
            {
                float load = horse.Weight + harness.Weight + GetEffectiveArmorEncumbrance(rider, rider.SpawnEquipment) + rider.SpawnEquipment.GetTotalWeightOfWeapons() + rider.Monster.Weight;
                float weightModifier = 475f * 475f / (load * load);
                speed = baseSpeed + (speed - baseSpeed) * weightModifier;
            }
            return speed;
        }

        private static bool IsRoundRaining(Mission mission)
        {
            int seed = CommanderBattleSpawnLayout.GetRoundSeed(mission);
            return seed != 0 && CommanderBattleWeather.GetPick(seed).Weather == CommanderBattleWeather.RoundWeather.Rain;
        }

        // RBM Combat's charge weight (HorseChanges.MountStats): the horse with its harness, and the rider with their
        // armour and weapons.
        private float ChargeWeight(Agent mount)
        {
            EquipmentElement horse = mount.SpawnEquipment[EquipmentIndex.ArmorItemEndSlot];
            float weight = (horse.Weight > 0f ? horse.Weight : mount.Monster.Weight) + mount.SpawnEquipment.GetTotalWeightOfArmor(forHuman: false);
            Agent rider = mount.RiderAgent;
            if (rider != null)
            {
                weight += rider.Monster.Weight + GetEffectiveArmorEncumbrance(rider, rider.SpawnEquipment) + rider.Equipment.GetTotalWeightOfWeapons();
            }
            return weight;
        }

        private bool IsCommanderBattle(Mission mission)
        {
            if (mission != _checkedMission)
            {
                _checkedMission = mission;
                _isCommanderBattle = mission?.GetMissionBehavior<MissionMultiplayerCommanderBattleClient>() != null;
            }
            return _isCommanderBattle;
        }
    }
}

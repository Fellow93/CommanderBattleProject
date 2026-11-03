using System;
using System.Reflection;
using HarmonyLib;
using RBMCombat;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace RBM.Multiplayer
{
    // RBM's per-agent weapon stats (RangedRework.OverrideWeaponEquipped), on the server and every client alike: the
    // swing, thrust and handling speeds of RBM's weapons, the bow draw speed, the bow's unload-when-sheathed flag, and
    // the launch speeds the engine aims and throws with. Every machine simulates these, so all must compute the same.
    //
    // Applied to RBM's items only, never to an mp_ item: Light mode equips only mp_ items, RBM mode's loadouts only
    // RBM's. The rule needs no word from the server, which a client gets only after it has built the agents already on
    // the field when it joins.
    //
    // Patched by hand on both sides, under RBM Combat's Harmony id: RBM AI's signature grip prefix on the same method
    // runs after it (Priority.Low), as in singleplayer.
    internal static class RbmMpEquipWield
    {
        private delegate bool RbmPrefix(ref Agent __instance, EquipmentIndex equipmentSlot, ref WeaponData weaponData, ref WeaponStatsData[] weaponStatsData,
            in WeaponData ammoWeaponData, ref WeaponStatsData[] ammoWeaponStatsData, bool removeOldWeaponFromScene, bool isWieldedOnSpawn);

        private static RbmPrefix _rbmPrefix;

        private static bool _patched;

        private static bool _failed;

        public static void Patch()
        {
            if (_patched)
            {
                return;
            }
            _patched = true;
            try
            {
                MethodInfo rbm = AccessTools.Method(AccessTools.Inner(typeof(RangedRework), "OverrideWeaponEquipped"), "Prefix");
                _rbmPrefix = (RbmPrefix)Delegate.CreateDelegate(typeof(RbmPrefix), rbm);
                new Harmony(RbmMpPatcher.CombatHarmonyId).Patch(AccessTools.Method(typeof(Agent), "WeaponEquipped"),
                    prefix: new HarmonyMethod(typeof(RbmMpEquipWield), nameof(Prefix)));
            }
            catch (Exception e)
            {
                RbmMpLog.Write($"RBM weapon stats not patched: {e}");
            }
        }

        private static void Prefix(Agent __instance, EquipmentIndex equipmentSlot, ref WeaponData weaponData, ref WeaponStatsData[] weaponStatsData,
            in WeaponData ammoWeaponData, ref WeaponStatsData[] ammoWeaponStatsData, bool removeOldWeaponFromScene, bool isWieldedOnSpawn)
        {
            MissionWeapon weapon = __instance.Equipment[equipmentSlot];
            if (weapon.IsEmpty || weapon.Item == null || weapon.Item.StringId.StartsWith("mp_", StringComparison.Ordinal))
            {
                return;
            }
            try
            {
                Agent agent = __instance;
                _rbmPrefix(ref agent, equipmentSlot, ref weaponData, ref weaponStatsData, in ammoWeaponData, ref ammoWeaponStatsData, removeOldWeaponFromScene, isWieldedOnSpawn);
            }
            catch (Exception e)
            {
                // Once: this runs on every equip.
                if (!_failed)
                {
                    _failed = true;
                    RbmMpLog.Write($"RBM weapon stats failed for {weapon.Item.StringId}: {e}");
                }
            }
        }
    }
}

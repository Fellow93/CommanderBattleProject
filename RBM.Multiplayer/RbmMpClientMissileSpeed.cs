using System;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace RBM.Multiplayer
{
    // RBM mode, client side: a player's own bow, crossbow or sling shot, as the client predicts it, flies at the speed
    // the server gives it. RBM's items hold a draw weight in missile_speed (100-330); the server's
    // RangedRework.OverrideOnAgentShootMissile turns it into a launch speed, but a client predicts its own shot from
    // the raw value and only the server's CreateMissile corrects it.
    // Only the prediction is touched (forcedMissileIndex -1): every shot the server sends comes with its speed already
    // set, and changing it again would aim it twice, which is why RBM's own prefix is not run on clients.
    // Patched by hand on clients, not by attribute, so RbmMpPatcher doesn't apply it on the server.
    internal static class RbmMpClientMissileSpeed
    {
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
                new Harmony(RbmMpPatcher.MultiplayerHarmonyId).Patch(AccessTools.Method(typeof(Mission), "OnAgentShootMissile"),
                    prefix: new HarmonyMethod(typeof(RbmMpClientMissileSpeed), nameof(Prefix)));
            }
            catch (Exception e)
            {
                RbmMpLog.Write($"Client missile speed not patched: {e}");
            }
        }

        // The server's shot prefix: the launcher's MissileSpeed is the draw weight, RBM never overwrites it.
        private static void Prefix(Agent shooterAgent, EquipmentIndex weaponIndex, ref Vec3 velocity, int forcedMissileIndex)
        {
            if (forcedMissileIndex != -1 || !GameNetwork.IsClient || shooterAgent == null || !RbmMpCombatMode.IsRbm)
            {
                return;
            }
            try
            {
                MissionWeapon launcher = shooterAgent.Equipment[weaponIndex];
                WeaponComponentData usage = launcher.CurrentUsageItem;
                WeaponClass weaponClass = launcher.GetWeaponComponentDataForUsage(0).WeaponClass;
                if (usage == null || (weaponClass != WeaponClass.Bow && weaponClass != WeaponClass.Crossbow && weaponClass != WeaponClass.Sling))
                {
                    return;
                }
                int speed;
                if (weaponClass == WeaponClass.Sling)
                {
                    speed = RBMConfig.MissileBallistics.GetSlingSpeed(shooterAgent, launcher, usage);
                }
                else
                {
                    MissionWeapon ammo = launcher.AmmoWeapon;
                    float ammoWeight = !ammo.IsEmpty && ammo.Item != null && ammo.Amount > 0 ? ammo.GetWeight() / ammo.Amount : 0.07f;
                    speed = RBMConfig.MissileBallistics.CalculateMissileSpeed(ammoWeight, usage.ItemUsage,
                        usage.MissileSpeed + RBMConfig.MissileBallistics.GetLauncherModifierBonus(launcher));
                }
                if (speed <= 0)
                {
                    return;
                }
                Vec3 engineVelocity = velocity;
                Vec3 direction = new Vec3(velocity, -1);
                direction.Normalize();
                float shooterSpeed = Vec3.DotProduct(new Vec3(shooterAgent.Velocity, -1), direction);
                velocity = direction * (speed + shooterSpeed);
                // The server lifts a player's bow and crossbow shots when RBM's realistic arc is on.
                if (RBMConfig.RBMConfig.realisticArrowArc && weaponClass != WeaponClass.Sling)
                {
                    double currentRad = Math.Acos(MBMath.ClampFloat(velocity.z / velocity.Length, -1f, 1f));
                    velocity.z = velocity.Length * (float)Math.Cos(currentRad - 0.083141f);
                }
                if (!velocity.IsValid || velocity.LengthSquared < 1f)
                {
                    velocity = engineVelocity;
                }
            }
            catch (Exception e)
            {
                // Once: this runs on every shot.
                if (!_failed)
                {
                    _failed = true;
                    RbmMpLog.Write($"Client missile speed failed, the engine's shot is kept: {e}");
                }
            }
        }
    }
}

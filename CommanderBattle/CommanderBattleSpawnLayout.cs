using System;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using MathF = TaleWorlds.Library.MathF;

namespace CommanderBattle
{
    // Where each side's formations start on campaign field-battle scenes. Those scenes have no multiplayer
    // spawn points, only the singleplayer spawn path (Mission.GetSpawnPathFrame), so the army is laid out
    // around the point where that path places the side, facing the enemy: infantry in the front line,
    // ranged behind it, cavalry on the flanks. Used by the server to place troops and by clients to place
    // the preparation-phase visuals, so both agree.
    public static class CommanderBattleSpawnLayout
    {
        private const float InfantrySpacing = 28f;

        private const float RangedSpacing = 40f;

        private const float RangedDepth = -22f;

        private const float CavalryDepth = -12f;

        private const float CavalryFlankGap = 35f;

        // How far each side's line starts back from the point where the two spawn paths meet.
        // Singleplayer scales this with army size; a fixed distance keeps server and clients in agreement.
        private const float SideDistanceFromCenter = 150f;

        // Multiplayer maps place sides with tagged spawn points; campaign scenes only have spawn paths.
        public static bool UsesSpawnPaths(Mission mission)
        {
            if (!mission.HasSpawnPath)
            {
                return false;
            }
            foreach (GameEntity entity in mission.Scene.FindEntitiesWithTag("spawnpoint"))
            {
                if (entity.HasTag("attacker") || entity.HasTag("defender"))
                {
                    return false;
                }
            }
            return true;
        }

        public static MatrixFrame GetSideFrame(Mission mission, BattleSideEnum side)
        {
            return GetFrame(mission, side, 0f, 0f);
        }

        public static MatrixFrame GetFormationFrame(Mission mission, BattleSideEnum side, int slotIndex)
        {
            GetOffset(slotIndex, out float lateral, out float depth);
            return GetFrame(mission, side, lateral, depth);
        }

        // lateral: metres to the right of the path, depth: metres towards the enemy. On the ground.
        public static MatrixFrame GetFrame(Mission mission, BattleSideEnum side, float lateral, float depth)
        {
            // Negative path offsets move back along the side's own end of the path; the frame faces the centre.
            float pathOffset = -MathF.Min(SideDistanceFromCenter, mission.GetInitialSpawnPath().GetTotalLength() / 2f - 1f);
            WorldFrame pathFrame = mission.GetSpawnPathFrame(side, pathOffset);
            Vec3 center = pathFrame.Origin.GetGroundVec3();
            Vec2 forward = pathFrame.Rotation.f.AsVec2.Normalized();
            Vec2 right = new Vec2(forward.y, -forward.x);
            Vec2 position = center.AsVec2 + right * lateral + forward * depth;
            WorldPosition ground = new WorldPosition(mission.Scene, UIntPtr.Zero, new Vec3(position.x, position.y, center.z), hasValidZ: false);
            Mat3 rotation = Mat3.Identity;
            rotation.RotateAboutUp(forward.RotationInRadians);
            return new MatrixFrame(in rotation, ground.GetGroundVec3());
        }

        // The slot's place in the army: lateral is metres right of the centre line, depth metres towards the
        // enemy. The AI keeps infantry formations at their lateral offset while advancing.
        public static void GetOffset(int slotIndex, out float lateral, out float depth)
        {
            CommanderRole[] roles = CommanderBattleSettings.FormationRoles;
            CommanderRole role = roles[slotIndex];
            int indexInRole = 0;
            int roleCount = 0;
            int infantryCount = 0;
            for (int i = 0; i < roles.Length; i++)
            {
                if (roles[i] == role)
                {
                    if (i < slotIndex)
                    {
                        indexInRole++;
                    }
                    roleCount++;
                }
                if (roles[i] == CommanderRole.Infantry)
                {
                    infantryCount++;
                }
            }
            float centered = indexInRole - (roleCount - 1) / 2f;
            switch (role)
            {
                case CommanderRole.Ranged:
                    lateral = centered * RangedSpacing;
                    depth = RangedDepth;
                    break;
                case CommanderRole.Cavalry:
                    // Alternate left and right flank, further out for each extra formation.
                    float side = indexInRole % 2 == 0 ? -1f : 1f;
                    float flank = (infantryCount - 1) / 2f * InfantrySpacing + CavalryFlankGap * (1 + indexInRole / 2);
                    lateral = side * flank;
                    depth = CavalryDepth;
                    break;
                default:
                    lateral = centered * InfantrySpacing;
                    depth = 0f;
                    break;
            }
        }
    }
}

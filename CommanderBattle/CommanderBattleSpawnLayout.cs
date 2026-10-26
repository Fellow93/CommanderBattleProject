using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using MathF = TaleWorlds.Library.MathF;

namespace CommanderBattle
{
    // Where each side's formations start on campaign field-battle scenes. Those scenes have no multiplayer
    // spawn points, only the singleplayer spawn path, so the armies are placed the way a singleplayer field
    // battle places them: the stock deployment planner (DefaultTeamDeploymentPlan) puts the side on its spawn
    // path at the singleplayer distance for this many troops, infantry in front, ranged behind, cavalry on the
    // left and right flanks, horse archers on the right flank (the planner's HorseArcher class). Each of those
    // blocks is then shared out between this mode's formations of that role, side by side, as wide as its share of
    // the block's troops. The roles per slot follow the side's culture. Used by the server to place troops and by
    // clients to place the preparation-phase visuals; both compute it from the same inputs (the round's seed and
    // troop counts, SpawnLayoutSeed, and the spawn path picked from that seed), so they agree.
    // Each round the server picks a seed (SpawnLayoutSeed, sent to every client) that moves each army within its
    // spawn zone: sideways, back or forward and slightly turned, as far as the map boundary allows, and swaps the
    // places of the formations of a role. Seed 0 is the plain layout. The random numbers come from LayoutRandom,
    // not System.Random, so the .NET Framework client and the .NET 6 server draw the same ones.
    public static class CommanderBattleSpawnLayout
    {
        private const float InfantrySpacing = 28f;

        private const float RangedSpacing = 40f;

        private const float CavalryFlankGap = 35f;

        // Space left between neighbouring formations cut from the same singleplayer block.
        private const float GapBetweenFormations = 4f;

        // Singleplayer spaces the armies by the battle size against the player's battle-size setting
        // (Mission.ComputeSpawnPathDeploymentOffset). A fixed reference keeps every machine in agreement.
        private const int ReferenceMaxBattleSize = 1000;

        // As DefaultBattleMissionAgentSpawnLogic.ComputeTeamDeploymentOffsets is called in singleplayer.
        private const float InterTeamGap = 20f;

        private sealed class SideLayout
        {
            public MatrixFrame SideFrame;

            public MatrixFrame[] SlotFrames;

            public float[] SlotWidths;
        }

        private static Mission _layoutMission;

        private static readonly SideLayout[] Layouts = new SideLayout[2];

        private static int _roundSeed;

        // This round's troop count per slot, FormationSlotTable.GetTroopCounts' order; null until the server sets
        // the first round up (the budgets stand in).
        private static int[] _troopCounts;

        public static int RoundSeed => _roundSeed;

        // The stock spawn path pick (BattleSpawnPathSelector.FindBestInitialPath) takes a random path and pivot with
        // MBRandom, on each machine at mission start, so the server and every client planned on different paths
        // (opposite ends of the map, even). The path is picked again each round from the round's seed instead.
        private static readonly FieldInfo SpawnPathSelectorField = typeof(Mission).GetField("_battleSpawnPathSelector", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo InitialPathField = typeof(BattleSpawnPathSelector).GetField("_initialPath", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo SideSelectorsField = typeof(BattleSpawnPathSelector).GetField("_battleSideSelectors", BindingFlags.Instance | BindingFlags.NonPublic);

        private static Mission _spawnPathMission;

        private static int _spawnPathSeed;

        // A new round's seed and troop counts, or new counts for this round's seed: the layout is planned again.
        // The server and the clients pass the same values (SpawnLayoutSeed), so they plan the same layout.
        public static void SetRound(Mission mission, int seed, int[] troopCounts)
        {
            _roundSeed = seed;
            _troopCounts = troopCounts != null && troopCounts.Length == 2 * CommanderBattleSettings.FormationCount ? (int[])troopCounts.Clone() : null;
            _layoutMission = mission;
            Layouts[0] = null;
            Layouts[1] = null;
            if (mission != _spawnPathMission || seed != _spawnPathSeed)
            {
                _spawnPathMission = mission;
                _spawnPathSeed = seed;
                try
                {
                    SelectSpawnPath(mission, seed);
                }
                catch (Exception e)
                {
                    CommanderBattleLog.Write($"Spawn path not picked from the seed, the stock random one stays: {e}");
                }
            }
        }

        // As FindBestInitialPath on a scene without a map patch (a spawn_path_NN and a pivot 37-63% along it), and as
        // BattleSpawnPathSelector.Initialize builds the two sides from it, but drawn from the seed.
        private static void SelectSpawnPath(Mission mission, int seed)
        {
            // With a map patch the stock pick follows the encounter position, the same everywhere.
            if (!mission.HasSpawnPath || mission.HasSceneMapPatch())
            {
                return;
            }
            object selector = SpawnPathSelectorField?.GetValue(mission);
            if (selector == null || InitialPathField == null || SideSelectorsField == null)
            {
                CommanderBattleLog.Write("Spawn path not picked from the seed: the stock spawn path selector's fields were not found");
                return;
            }
            MBList<Path> paths = MBSceneUtilities.GetAllSpawnPaths(mission.Scene);
            if (paths.Count == 0)
            {
                return;
            }
            LayoutRandom random = new LayoutRandom(seed, 2);
            Path path = seed == 0 ? paths[0] : paths[random.Next(paths.Count)];
            float length = path.GetTotalLength();
            float pivot = MathF.Clamp(length * (seed == 0 ? 0.5f : random.Range(0.37f, 0.63f)), 1f, length - 1f);
            // A side's path snaps to the ground by Mission.IsFieldBattle. The stock pick ran before the server set
            // FieldBattle, which clients never do, so every machine builds the sides as without a team AI type.
            Mission.MissionTeamAITypeEnum teamAIType = mission.MissionTeamAIType;
            mission.MissionTeamAIType = Mission.MissionTeamAITypeEnum.NoTeamAI;
            try
            {
                BattleSideSpawnPathSelector[] sides =
                {
                    new BattleSideSpawnPathSelector(mission, path, pivot, false),
                    new BattleSideSpawnPathSelector(mission, path, MathF.Clamp(length - pivot, 1f, length - 1f), true)
                };
                InitialPathField.SetValue(selector, path);
                SideSelectorsField.SetValue(selector, sides);
            }
            finally
            {
                mission.MissionTeamAIType = teamAIType;
            }
            CommanderBattleLog.Detail($"Spawn path {path.GetName()} ({paths.Count} on the scene), pivot {pivot:0} m of {length:0} m (seed {seed})");
        }

        private static int GetTroopCount(BattleSideEnum side, int slotIndex)
        {
            if (_troopCounts == null)
            {
                return CommanderBattleSettings.GetTroopBudget(CommanderBattleSettings.GetFormationRoles(CommanderBattleTeams.GetCulture(side))[slotIndex]);
            }
            return _troopCounts[(side == BattleSideEnum.Attacker ? CommanderBattleSettings.FormationCount : 0) + slotIndex];
        }

        private static int GetSideTroopCount(BattleSideEnum side)
        {
            int total = 0;
            for (int i = 0; i < CommanderBattleSettings.FormationCount; i++)
            {
                total += GetTroopCount(side, i);
            }
            return total;
        }

        // xorshift32: the same numbers on every machine for the same seed.
        private struct LayoutRandom
        {
            private uint _state;

            public LayoutRandom(int seed, int sideIndex)
            {
                _state = unchecked((uint)seed * 2654435761u ^ (uint)(sideIndex + 1) * 40503u);
                if (_state == 0)
                {
                    _state = 1;
                }
            }

            public float NextFloat()
            {
                _state ^= _state << 13;
                _state ^= _state >> 17;
                _state ^= _state << 5;
                return (_state >> 8) / 16777216f;
            }

            public float Range(float min, float max)
            {
                return min + (max - min) * NextFloat();
            }

            public int Next(int count)
            {
                return Math.Min((int)(NextFloat() * count), count - 1);
            }
        }

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

        // The middle of the side's front line, facing the enemy.
        public static MatrixFrame GetSideFrame(Mission mission, BattleSideEnum side)
        {
            return GetLayout(mission, side).SideFrame;
        }

        public static MatrixFrame GetFormationFrame(Mission mission, BattleSideEnum side, int slotIndex)
        {
            return GetFormationFrame(mission, side, slotIndex, out _);
        }

        // width: how wide the formation is planned, metres.
        public static MatrixFrame GetFormationFrame(Mission mission, BattleSideEnum side, int slotIndex, out float width)
        {
            SideLayout layout = GetLayout(mission, side);
            width = layout.SlotWidths[slotIndex];
            return layout.SlotFrames[slotIndex];
        }

        // lateral: metres to the right of the side frame, depth: metres towards the enemy. On the ground.
        public static MatrixFrame GetFrame(Mission mission, BattleSideEnum side, float lateral, float depth)
        {
            return Offset(mission, GetSideFrame(mission, side), lateral, depth);
        }

        // The side's slot roles: they follow its culture (CommanderBattleSettings.GetFormationRoles), the same on the
        // server and every client.
        private static CommanderRole[] GetRoles(BattleSideEnum side)
        {
            return CommanderBattleSettings.GetFormationRoles(CommanderBattleTeams.GetCulture(side));
        }

        private static int CountRole(CommanderRole[] roles, CommanderRole role)
        {
            int count = 0;
            foreach (CommanderRole r in roles)
            {
                if (r == role)
                {
                    count++;
                }
            }
            return count;
        }

        // The slot's place in the army as a lane for CommanderBattleTactic: lateral is metres right of the centre
        // line, depth metres towards the enemy. The AI keeps infantry formations at their lateral offset while
        // advancing.
        public static void GetOffset(BattleSideEnum side, int slotIndex, out float lateral, out float depth)
        {
            CommanderRole[] roles = GetRoles(side);
            CommanderRole role = roles[slotIndex];
            GetRoleIndex(roles, slotIndex, out int indexInRole, out int roleCount);
            int infantryCount = CountRole(roles, CommanderRole.Infantry);
            float centered = indexInRole - (roleCount - 1) / 2f;
            float flankSide = indexInRole % 2 == 0 ? -1f : 1f;
            float infantryHalfWidth = (infantryCount - 1) / 2f * InfantrySpacing;
            switch (role)
            {
                case CommanderRole.Ranged:
                    lateral = centered * RangedSpacing;
                    depth = -22f;
                    break;
                case CommanderRole.Cavalry:
                    // Alternate left and right flank, further out for each extra formation.
                    lateral = flankSide * (infantryHalfWidth + CavalryFlankGap * (1 + indexInRole / 2));
                    depth = -12f;
                    break;
                case CommanderRole.HorseArcher:
                    // Outside the cavalry on each flank, a little further back.
                    int cavalryPerFlank = (CountRole(roles, CommanderRole.Cavalry) + 1) / 2;
                    lateral = flankSide * (infantryHalfWidth + CavalryFlankGap * (1 + cavalryPerFlank + indexInRole / 2));
                    depth = -20f;
                    break;
                default:
                    lateral = centered * InfantrySpacing;
                    depth = 0f;
                    break;
            }
        }

        private static void GetRoleIndex(CommanderRole[] roles, int slotIndex, out int indexInRole, out int roleCount)
        {
            indexInRole = 0;
            roleCount = 0;
            for (int i = 0; i < roles.Length; i++)
            {
                if (roles[i] == roles[slotIndex])
                {
                    if (i < slotIndex)
                    {
                        indexInRole++;
                    }
                    roleCount++;
                }
            }
        }

        // The singleplayer block a slot is cut from: infantry, ranged and horse archers each form one block (the
        // planner places horse archers as singleplayer does); cavalry formations alternate between the left-flank
        // class (Cavalry) and the right-flank one (LightCavalry).
        private static FormationClass GetPlanClass(CommanderRole[] roles, int slotIndex)
        {
            GetRoleIndex(roles, slotIndex, out int indexInRole, out _);
            switch (roles[slotIndex])
            {
                case CommanderRole.Ranged:
                    return FormationClass.Ranged;
                case CommanderRole.Cavalry:
                    return indexInRole % 2 == 0 ? FormationClass.Cavalry : FormationClass.LightCavalry;
                case CommanderRole.HorseArcher:
                    return FormationClass.HorseArcher;
                default:
                    return FormationClass.Infantry;
            }
        }

        private static SideLayout GetLayout(Mission mission, BattleSideEnum side)
        {
            if (_layoutMission != mission)
            {
                _layoutMission = mission;
                Layouts[0] = null;
                Layouts[1] = null;
            }
            int sideIndex = side == BattleSideEnum.Attacker ? 1 : 0;
            if (Layouts[sideIndex] == null)
            {
                LayoutRandom random = new LayoutRandom(_roundSeed, sideIndex);
                SideLayout layout = null;
                try
                {
                    layout = PlanLikeSingleplayer(mission, side, ref random);
                }
                catch (Exception e)
                {
                    CommanderBattleLog.Write($"Singleplayer deployment plan failed for {side}: {e}");
                }
                if (layout == null)
                {
                    layout = PlanFixed(mission, side);
                }
                if (_roundSeed != 0)
                {
                    MoveWithinSpawnZone(mission, side, layout, ref random);
                }
                Layouts[sideIndex] = layout;
            }
            return Layouts[sideIndex];
        }

        // As DefaultBattleMissionAgentSpawnLogic plans a singleplayer field battle's initial deployment.
        private static SideLayout PlanLikeSingleplayer(Mission mission, BattleSideEnum side, ref LayoutRandom random)
        {
            Team team = CommanderBattleTeams.GetTeam(mission, side);
            SpawnPathData pathData = mission.GetInitialSpawnPathData(side);
            if (team == null || !pathData.IsValid)
            {
                return null;
            }
            int slotCount = CommanderBattleSettings.FormationCount;
            CommanderRole[] roles = GetRoles(side);
            Dictionary<FormationClass, int> classTroops = new Dictionary<FormationClass, int>();
            for (int i = 0; i < slotCount; i++)
            {
                FormationClass planClass = GetPlanClass(roles, i);
                classTroops.TryGetValue(planClass, out int troops);
                classTroops[planClass] = troops + GetTroopCount(side, i);
            }

            DefaultTeamDeploymentPlan teamPlan = new DefaultTeamDeploymentPlan(mission, team);
            bool anyFoot = false;
            foreach (KeyValuePair<FormationClass, int> entry in classTroops)
            {
                bool mounted = entry.Key.IsMounted();
                anyFoot |= !mounted;
                teamPlan.AddTroops(entry.Key, mounted ? 0 : entry.Value, mounted ? entry.Value : 0);
            }
            // Both armies' real size, as singleplayer spaces them by the battle's size; both sides' counts arrive
            // together, so every machine has the same total.
            float baseOffset = GetSpawnPathDeploymentOffset(GetSideTroopCount(BattleSideEnum.Attacker) + GetSideTroopCount(BattleSideEnum.Defender), pathData.Path);
            DefaultBattleMissionAgentSpawnLogic.ComputeDeploymentBaseOffsets(pathData, baseOffset, out float deployingOffset, out float opposingOffset);
            DefaultBattleMissionAgentSpawnLogic.ComputeTeamDeploymentOffsets(pathData, deployingOffset, InterTeamGap, new[] { GetTeamDepth(classTroops, anyFoot) }, out float[] teamOffsets);
            teamPlan.MakeDeploymentPlan(teamOffsets[0], opposingOffset);

            SideLayout layout = new SideLayout
            {
                SlotFrames = new MatrixFrame[slotCount],
                SlotWidths = new float[slotCount]
            };
            IFormationDeploymentPlan front = teamPlan.GetFormationPlan(FormationClass.Infantry);
            if (front == null || !front.HasFrame())
            {
                return null;
            }
            layout.SideFrame = front.GetFrame();
            // Share each block out between its slots, left to right in the slots' order within their role.
            foreach (FormationClass planClass in classTroops.Keys)
            {
                IFormationDeploymentPlan plan = teamPlan.GetFormationPlan(planClass);
                if (plan == null || !plan.HasFrame())
                {
                    return null;
                }
                MatrixFrame blockFrame = plan.GetFrame();
                float blockWidth = MathF.Max(plan.PlannedWidth, 1f);
                List<int> blockSlots = new List<int>();
                for (int i = 0; i < slotCount; i++)
                {
                    if (GetPlanClass(roles, i) == planClass)
                    {
                        blockSlots.Add(i);
                    }
                }
                // The formations of a role swap places from round to round.
                if (_roundSeed != 0)
                {
                    for (int i = blockSlots.Count - 1; i > 0; i--)
                    {
                        int j = random.Next(i + 1);
                        (blockSlots[i], blockSlots[j]) = (blockSlots[j], blockSlots[i]);
                    }
                }
                float usable = MathF.Max(blockWidth - GapBetweenFormations * (blockSlots.Count - 1), (float)blockSlots.Count);
                float left = -blockWidth / 2f;
                foreach (int slot in blockSlots)
                {
                    float share = usable * GetTroopCount(side, slot) / MathF.Max(classTroops[planClass], 1);
                    layout.SlotFrames[slot] = Offset(mission, blockFrame, left + share / 2f, 0f);
                    layout.SlotWidths[slot] = share;
                    left += share + GapBetweenFormations;
                }
            }
            CommanderBattleLog.Detail($"{side} deployment planned as in singleplayer: path offset {teamOffsets[0]:0} m (target {opposingOffset:0} m), front line at {FormatPosition(layout.SideFrame.origin)}");
            return layout;
        }

        // Mission.ComputeSpawnPathDeploymentOffset with ReferenceMaxBattleSize in place of the player's setting.
        private static float GetSpawnPathDeploymentOffset(int troopCount, Path path)
        {
            float distance = 200f;
            if (troopCount > 20)
            {
                int range = MathF.Max(ReferenceMaxBattleSize - 20, 1);
                float share = (float)Math.Min(troopCount - 20, range) / range;
                distance += 400f * MathF.Pow(share, 0.6f);
            }
            distance = MathF.Min(path.GetTotalLength(), distance);
            return -distance / 2f + 1f;
        }

        // How far the side's formations reach back along the path (DefaultBattleMissionAgentSpawnLogic's
        // GetTeamSpawnPathOffsetRange): the deepest of its front-and-rear, left and right flanks.
        private static float GetTeamDepth(Dictionary<FormationClass, int> classTroops, bool anyFoot)
        {
            const float flankGap = 3f;
            float[] flankDepths = new float[(int)FormationDeploymentFlank.Count];
            foreach (KeyValuePair<FormationClass, int> entry in classTroops)
            {
                bool mounted = entry.Key.IsMounted();
                bool significantMounted = DefaultMissionDeploymentPlan.HasSignificantMountedTroops(mounted ? 0 : entry.Value, mounted ? entry.Value : 0);
                float depth = DefaultDeploymentPlan.GetFormationSpawnWidthAndDepth(entry.Key, entry.Value, significantMounted).Item2;
                FormationDeploymentFlank flank = DefaultFormationDeploymentPlan.GetFormationDefaultFlankAux(entry.Key, entry.Value, anyFoot, significantMounted, canSpawnWithHorses: true);
                // Front and rear stack into one column, as in singleplayer.
                int column = flank == FormationDeploymentFlank.Rear ? (int)FormationDeploymentFlank.Front : (int)flank;
                if (flankDepths[column] > 0.0001f)
                {
                    flankDepths[column] += flankGap;
                }
                flankDepths[column] += depth;
            }
            return MathF.Max(flankDepths[(int)FormationDeploymentFlank.Front], MathF.Max(flankDepths[(int)FormationDeploymentFlank.Left], flankDepths[(int)FormationDeploymentFlank.Right]));
        }

        // Fallback when the planner can't place the side: a fixed distance back along the path.
        private static SideLayout PlanFixed(Mission mission, BattleSideEnum side)
        {
            float pathOffset = -MathF.Min(150f, mission.GetInitialSpawnPath().GetTotalLength() / 2f - 1f);
            WorldFrame pathFrame = mission.GetSpawnPathFrame(side, pathOffset);
            Vec3 center = pathFrame.Origin.GetGroundVec3();
            Mat3 rotation = Mat3.Identity;
            rotation.RotateAboutUp(pathFrame.Rotation.f.AsVec2.Normalized().RotationInRadians);
            SideLayout layout = new SideLayout
            {
                SideFrame = new MatrixFrame(in rotation, center),
                SlotFrames = new MatrixFrame[CommanderBattleSettings.FormationCount],
                SlotWidths = new float[CommanderBattleSettings.FormationCount]
            };
            CommanderRole[] roles = GetRoles(side);
            for (int i = 0; i < layout.SlotFrames.Length; i++)
            {
                GetOffset(side, i, out float lateral, out float depth);
                layout.SlotFrames[i] = Offset(mission, layout.SideFrame, lateral, depth);
                layout.SlotWidths[i] = roles[i] == CommanderRole.Infantry ? InfantrySpacing - GapBetweenFormations : 20f;
            }
            CommanderBattleLog.Detail($"{side} deployment at a fixed distance on the spawn path, front line at {FormatPosition(layout.SideFrame.origin)}");
            return layout;
        }

        // Moves the whole army: sideways, back or forward along its facing, and turned about its front-line centre.
        // If that would put a formation (centre or either end) outside the map boundary, a smaller move is tried,
        // down to none.
        private static void MoveWithinSpawnZone(Mission mission, BattleSideEnum side, SideLayout layout, ref LayoutRandom random)
        {
            float lateral = random.Range(-CommanderBattleSettings.SpawnLateralJitter, CommanderBattleSettings.SpawnLateralJitter);
            float depth = random.Range(-CommanderBattleSettings.SpawnBackJitter, CommanderBattleSettings.SpawnForwardJitter);
            float turn = random.Range(-CommanderBattleSettings.SpawnTurnJitterDegrees, CommanderBattleSettings.SpawnTurnJitterDegrees) * MathF.PI / 180f;
            Vec2 center = layout.SideFrame.origin.AsVec2;
            Vec2 forward = layout.SideFrame.rotation.f.AsVec2.Normalized();
            Vec2 right = new Vec2(forward.y, -forward.x);
            foreach (float scale in new[] { 1f, 0.5f, 0.25f })
            {
                Vec2 shift = right * (lateral * scale) + forward * (depth * scale);
                float angle = turn * scale;
                MatrixFrame[] slots = new MatrixFrame[layout.SlotFrames.Length];
                bool inside = true;
                for (int i = 0; i < slots.Length && inside; i++)
                {
                    slots[i] = Move(mission, layout.SlotFrames[i], center, angle, shift);
                    inside = IsInside(mission, slots[i], layout.SlotWidths[i]);
                }
                if (!inside)
                {
                    continue;
                }
                layout.SideFrame = Move(mission, layout.SideFrame, center, angle, shift);
                layout.SlotFrames = slots;
                CommanderBattleLog.Detail($"{side} spawn moved {lateral * scale:0} m sideways, {depth * scale:0} m forward, turned {angle * 180f / MathF.PI:0} degrees (seed {_roundSeed})");
                return;
            }
            CommanderBattleLog.Detail($"{side} spawn not moved: the map boundary is too close (seed {_roundSeed})");
        }

        private static MatrixFrame Move(Mission mission, MatrixFrame frame, Vec2 center, float angle, Vec2 shift)
        {
            Vec2 position = center + Rotate(frame.origin.AsVec2 - center, angle) + shift;
            Vec2 forward = Rotate(frame.rotation.f.AsVec2.Normalized(), angle);
            WorldPosition ground = new WorldPosition(mission.Scene, UIntPtr.Zero, new Vec3(position.x, position.y, frame.origin.z), hasValidZ: false);
            Mat3 rotation = Mat3.Identity;
            rotation.RotateAboutUp(forward.RotationInRadians);
            return new MatrixFrame(in rotation, ground.GetGroundVec3());
        }

        // Counter-clockwise by angle radians.
        private static Vec2 Rotate(Vec2 v, float angle)
        {
            float cos = MathF.Cos(angle);
            float sin = MathF.Sin(angle);
            return new Vec2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
        }

        private static bool IsInside(Mission mission, MatrixFrame frame, float width)
        {
            Vec2 forward = frame.rotation.f.AsVec2.Normalized();
            Vec2 halfWidth = new Vec2(forward.y, -forward.x) * (width / 2f);
            Vec2 center = frame.origin.AsVec2;
            return mission.IsPositionInsideBoundaries(center) && mission.IsPositionInsideBoundaries(center + halfWidth) && mission.IsPositionInsideBoundaries(center - halfWidth);
        }

        // The frame moved lateral metres to its right and depth metres forward, on the ground, same facing.
        private static MatrixFrame Offset(Mission mission, MatrixFrame frame, float lateral, float depth)
        {
            Vec2 forward = frame.rotation.f.AsVec2.Normalized();
            Vec2 right = new Vec2(forward.y, -forward.x);
            Vec2 position = frame.origin.AsVec2 + right * lateral + forward * depth;
            WorldPosition ground = new WorldPosition(mission.Scene, UIntPtr.Zero, new Vec3(position.x, position.y, frame.origin.z), hasValidZ: false);
            Mat3 rotation = Mat3.Identity;
            rotation.RotateAboutUp(forward.RotationInRadians);
            return new MatrixFrame(in rotation, ground.GetGroundVec3());
        }

        private static string FormatPosition(Vec3 position)
        {
            return $"({position.x:0.0}, {position.y:0.0}, {position.z:0.0})";
        }
    }
}

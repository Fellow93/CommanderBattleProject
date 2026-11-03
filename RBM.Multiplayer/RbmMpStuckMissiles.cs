using System;
using System.Collections.Generic;
using HarmonyLib;
using RBM.Multiplayer.Network;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Network.Messages;

namespace RBM.Multiplayer
{
    // RBM's stuck missiles working loose (RBMCombat.RangedRework.StuckMissileLogic, same rules: thrown weapons fall out
    // after about stuckThrownFallOutSeconds, arrows and bolts out of a shield after about stuckArrowFallOutSeconds, at
    // most 8 arrows in a shield and 4 in a body), for multiplayer. RBM's class can't be used as it is: its detach changes
    // only this machine's attachments, and its loose copy goes through Mission.SpawnWeaponAux, which sends nothing. The
    // clients kept the missile, never saw the copy, and the attachment indices the engine sends later (a corpse's or a
    // dropped shield's missiles go by index) stopped matching. Here the server picks the missile as RBM does, and every
    // machine takes out the same attachment and spawns its own copy under the server's id (StuckMissileDrop), as the
    // engine's SpawnAttachedWeaponOnCorpse does. A player joining later gets the copies with the other spawned items.
    //
    // Server: added in RBM mode, fed by RBM's HandleMissileCollisionReactionPatch through RegisterPatch. Client: always
    // added in Commander Battle, for the message.
    public class RbmMpStuckMissiles : MissionLogic
    {
        private const int MaxArrowsPerShield = 8;

        private const int MaxArrowsPerBody = 4;

        private const float MinTimeFactor = 0.6f;

        private const float MaxTimeFactor = 1.4f;

        private const float ScanInterval = 0.1f;

        private const int MaxDetachesPerScan = 8;

        private const float DropOutwardSpeed = 0.7f;

        private const float DropDownwardSpeed = 0.5f;

        private const float DropSpin = 2.5f;

        // The reason vanilla gives the engine for a picked up item.
        private const int EntityRemoveReason = 73;

        private const float SameOriginToleranceSquared = 1e-8f;

        private class Entry
        {
            public Agent Agent;
            public bool InShield;
            public EquipmentIndex Slot;
            public ItemObject Shield;
            public ItemObject Item;
            public Vec3 Origin;
            public bool IsThrown;
            public float Due;
        }

        private delegate SpawnedItemEntity SpawnWeaponAuxDelegate(Mission mission, WeakGameEntity weaponEntity, MissionWeapon weapon, Mission.WeaponSpawnFlags spawnFlags, Vec3 globalVelocity, Vec3 globalAngularVelocity, bool hasLifeTime, bool spawnedOnACorpse);

        // The only spawn call that takes the drop's velocity; the public ones around it pass zero.
        private static readonly SpawnWeaponAuxDelegate SpawnWeaponAux =
            AccessTools.MethodDelegate<SpawnWeaponAuxDelegate>(AccessTools.Method(typeof(Mission), "SpawnWeaponAux"));

        // Server only, oldest first.
        private readonly List<Entry> _entries = new List<Entry>();

        private float _nextScan;

        private GameNetwork.NetworkMessageHandlerRegistererContainer _registerer;

        // Added from the submodule's OnMissionBehaviorInitialize, after MissionNetwork's own registration point.
        public override void EarlyStart()
        {
            base.EarlyStart();
            if (GameNetwork.IsClient)
            {
                _registerer = new GameNetwork.NetworkMessageHandlerRegistererContainer();
                _registerer.RegisterBaseHandler<StuckMissileDrop>(HandleDrop);
                _registerer.RegisterMessages();
            }
        }

        public override void OnRemoveBehavior()
        {
            _registerer?.UnregisterMessages();
            _registerer = null;
            _entries.Clear();
            base.OnRemoveBehavior();
        }

        public override void OnClearScene()
        {
            base.OnClearScene();
            _entries.Clear();
        }

        [HarmonyPatch(typeof(RBMCombat.RangedRework.StuckMissileLogic), nameof(RBMCombat.RangedRework.StuckMissileLogic.Register))]
        private static class RegisterPatch
        {
            private static bool Prefix(Mission mission, Agent agent, bool inShield, EquipmentIndex shieldIndex, MissionWeapon missileWeapon)
            {
                mission?.GetMissionBehavior<RbmMpStuckMissiles>()?.Add(agent, inShield, shieldIndex, missileWeapon);
                return false;
            }
        }

        // RBM's Register and Add: called right after the missile was attached, so it is the last attachment.
        private void Add(Agent agent, bool inShield, EquipmentIndex shieldIndex, MissionWeapon missile)
        {
            if (agent == null || missile.IsEmpty)
            {
                return;
            }
            bool isThrown = missile.HasAnyUsageWithWeaponClass(WeaponClass.Javelin) || missile.HasAnyUsageWithWeaponClass(WeaponClass.ThrowingAxe) ||
                missile.HasAnyUsageWithWeaponClass(WeaponClass.ThrowingKnife);
            if (!isThrown && !(missile.HasAnyUsageWithWeaponClass(WeaponClass.Arrow) || missile.HasAnyUsageWithWeaponClass(WeaponClass.Bolt)))
            {
                return;
            }
            float seconds = isThrown ? RBMConfig.RBMConfig.stuckThrownFallOutSeconds : RBMConfig.RBMConfig.stuckArrowFallOutSeconds;
            if (seconds <= 0f)
            {
                return;
            }
            Entry entry = new Entry { Agent = agent, InShield = inShield, Slot = inShield ? shieldIndex : EquipmentIndex.None, Item = missile.Item, IsThrown = isThrown };
            if (inShield)
            {
                if (!IsWeaponSlot(agent, shieldIndex))
                {
                    return;
                }
                MissionWeapon shield = agent.Equipment[shieldIndex];
                int count = shield.IsEmpty ? 0 : shield.GetAttachedWeaponsCount();
                if (count == 0)
                {
                    return;
                }
                entry.Shield = shield.Item;
                entry.Origin = shield.GetAttachedWeaponFrame(count - 1).origin;
            }
            else
            {
                int count = agent.GetAttachedWeaponsCount();
                if (count == 0)
                {
                    return;
                }
                entry.Origin = agent.GetAttachedWeaponFrame(count - 1).origin;
            }
            // An arrow in a body has no timer: only the cap pushes it out.
            entry.Due = !isThrown && !inShield ? float.MaxValue : Mission.CurrentTime + seconds * MBRandom.RandomFloatRanged(MinTimeFactor, MaxTimeFactor);
            _entries.Add(entry);
            if (!isThrown)
            {
                PushOutOldestArrowOverCap(entry);
            }
        }

        // Inside the engine's missile hit callback: the oldest is only marked due, the next scan drops it.
        private void PushOutOldestArrowOverCap(Entry added)
        {
            int count = 0;
            Entry oldest = null;
            foreach (Entry entry in _entries)
            {
                if (entry.Agent != added.Agent || entry.InShield != added.InShield || entry.IsThrown || entry.Due <= 0f)
                {
                    continue;
                }
                if (added.InShield && (entry.Slot != added.Slot || entry.Shield != added.Shield))
                {
                    continue;
                }
                count++;
                if (oldest == null)
                {
                    oldest = entry;
                }
            }
            if (count > (added.InShield ? MaxArrowsPerShield : MaxArrowsPerBody) && oldest != null)
            {
                oldest.Due = 0f;
            }
        }

        public override void OnMissionTick(float dt)
        {
            float now = Mission.CurrentTime;
            if (_entries.Count == 0 || now < _nextScan)
            {
                return;
            }
            _nextScan = now + ScanInterval;
            int dropped = 0;
            int kept = 0;
            for (int i = 0; i < _entries.Count; i++)
            {
                Entry entry = _entries[i];
                // A dead bearer's missiles are vanilla's (a corpse's, a dropped shield's).
                bool keep = entry.Agent != null && entry.Agent.IsActive();
                if (keep && entry.Due <= now && dropped < MaxDetachesPerScan)
                {
                    keep = false;
                    int index = FindIndex(entry);
                    if (index >= 0)
                    {
                        DropAndBroadcast(entry, index);
                        dropped++;
                    }
                }
                if (keep)
                {
                    _entries[kept++] = entry;
                }
            }
            _entries.RemoveRange(kept, _entries.Count - kept);
        }

        // Indices shift as others are taken out and the shield may be dropped or swapped, so the missile is looked up
        // again by its item and attach frame. -1: no longer there.
        private static int FindIndex(Entry entry)
        {
            Agent agent = entry.Agent;
            if (entry.InShield)
            {
                if (!IsWeaponSlot(agent, entry.Slot))
                {
                    return -1;
                }
                MissionWeapon shield = agent.Equipment[entry.Slot];
                if (shield.IsEmpty || shield.Item != entry.Shield)
                {
                    return -1;
                }
                for (int i = 0; i < shield.GetAttachedWeaponsCount(); i++)
                {
                    if (shield.GetAttachedWeapon(i).Item == entry.Item && IsSameOrigin(shield.GetAttachedWeaponFrame(i).origin, entry.Origin))
                    {
                        return i;
                    }
                }
                return -1;
            }
            for (int i = 0; i < agent.GetAttachedWeaponsCount(); i++)
            {
                if (agent.GetAttachedWeapon(i).Item == entry.Item && IsSameOrigin(agent.GetAttachedWeaponFrame(i).origin, entry.Origin))
                {
                    return i;
                }
            }
            return -1;
        }

        private void DropAndBroadcast(Entry entry, int index)
        {
            try
            {
                int spawnedId = Drop(Mission, entry.Agent, entry.Slot, index, entry.Item, -1);
                if (spawnedId < 0)
                {
                    return;
                }
                GameNetwork.BeginBroadcastModuleEvent();
                GameNetwork.WriteMessage(new StuckMissileDrop(entry.Agent.Index, (int)entry.Slot, index, entry.Item, spawnedId));
                GameNetwork.EndBroadcastModuleEvent(GameNetwork.EventBroadcastFlags.AddToMissionRecord);
            }
            catch (Exception e)
            {
                RbmMpLog.Write($"Stuck missile not dropped: {e}");
            }
        }

        private void HandleDrop(GameNetworkMessage baseMessage)
        {
            StuckMissileDrop message = (StuckMissileDrop)baseMessage;
            Agent agent = Mission.MissionNetworkHelper.GetAgentFromIndex(message.AgentIndex, canBeNull: true);
            if (agent == null || message.Item == null)
            {
                return;
            }
            try
            {
                Drop(Mission, agent, (EquipmentIndex)message.Slot, message.AttachmentIndex, message.Item, message.SpawnedId);
            }
            catch (Exception e)
            {
                RbmMpLog.Write($"Stuck missile not dropped: {e}");
            }
        }

        // Takes the attachment at index out of the body (slot None) or the shield in slot and spawns the loose copy where
        // it was. Returns the copy's id, -1 if none was spawned. A client whose attachment at index isn't that item (it
        // stuck elsewhere there) leaves its attachments alone but still spawns the copy, at the agent: the server's
        // messages about that id must find it.
        private static int Drop(Mission mission, Agent agent, EquipmentIndex slot, int index, ItemObject item, int forcedId)
        {
            MissionWeapon missile = new MissionWeapon(item, null, null, 1);
            MatrixFrame meshFrame = new MatrixFrame(Mat3.Identity, agent.Position + new Vec3(0f, 0f, 1f));
            // The skeleton's attachments lag the animation until the bones are brought up to date (RBM's UpdateBoneFrames).
            agent.AgentVisuals?.GetSkeleton()?.ForceUpdateBoneFrames();
            if (slot == EquipmentIndex.None)
            {
                if (index < agent.GetAttachedWeaponsCount() && agent.GetAttachedWeapon(index).Item == item)
                {
                    missile = agent.GetAttachedWeapon(index);
                    GameEntity entity = agent.AgentVisuals?.GetAttachedWeaponEntity(index);
                    if (entity != null)
                    {
                        meshFrame = GetGlobalMeshFrame(entity.WeakEntity);
                    }
                    agent.DeleteAttachedWeapon(index);
                }
                else if (forcedId < 0)
                {
                    return -1;
                }
            }
            else if (IsWeaponSlot(agent, slot))
            {
                // A copy of the slot, but the attachment lists inside it are the slot's own.
                MissionWeapon shield = agent.Equipment[slot];
                int count = shield.IsEmpty ? 0 : shield.GetAttachedWeaponsCount();
                if (index < count && shield.GetAttachedWeapon(index).Item == item)
                {
                    missile = shield.GetAttachedWeapon(index);
                    WeakGameEntity child = FindShieldChild(agent.GetWeaponEntityFromEquipmentSlot(slot), count, index, shield.GetAttachedWeaponFrame(index).origin);
                    if (child.IsValid)
                    {
                        meshFrame = GetGlobalMeshFrame(child);
                        using (new TWSharedMutexWriteLock(Scene.PhysicsAndRayCastLock))
                        {
                            child.Remove(EntityRemoveReason);
                        }
                    }
                    shield.RemoveAttachedWeapon(index);
                }
                else if (forcedId < 0)
                {
                    return -1;
                }
            }
            else if (forcedId < 0)
            {
                return -1;
            }
            return SpawnLooseCopy(mission, missile, meshFrame, agent, forcedId);
        }

        // The missile's child entity on the shield. Children line up with the attachments while the shield has no
        // children of its own; otherwise the one child at the attach frame (RBM's DetachFromShield).
        private static WeakGameEntity FindShieldChild(WeakGameEntity shieldEntity, int attachedCount, int index, Vec3 origin)
        {
            if (!shieldEntity.IsValid)
            {
                return WeakGameEntity.Invalid;
            }
            int childCount = shieldEntity.ChildCount;
            if (childCount == attachedCount)
            {
                return shieldEntity.GetChild(index);
            }
            WeakGameEntity found = WeakGameEntity.Invalid;
            for (int i = 0; i < childCount; i++)
            {
                WeakGameEntity child = shieldEntity.GetChild(i);
                if (child.IsValid && IsSameOrigin(child.GetFrame().origin, origin))
                {
                    if (found.IsValid)
                    {
                        return WeakGameEntity.Invalid;
                    }
                    found = child;
                }
            }
            return found;
        }

        // RBM's SpawnLooseCopy: a fresh pickable item with physics on the dropped-item timer, placed by its mesh (the
        // stuck missile's entity and an item entity hold the same mesh differently).
        private static int SpawnLooseCopy(Mission mission, MissionWeapon missile, MatrixFrame meshFrame, Agent agent, int forcedId)
        {
            GameEntity entity = TaleWorlds.MountAndBlade.GameEntityExtensions.Instantiate(mission.Scene, missile, showHolsterWithWeapon: false, needBatchedVersion: true);
            if (entity == null)
            {
                return -1;
            }
            MatrixFrame globalFrame = EntityFrameForMeshAt(meshFrame, entity.WeakEntity);
            entity.CreateAndAddScriptComponent(typeof(SpawnedItemEntity).Name, callScriptCallbacks: true);
            SpawnedItemEntity spawned = entity.GetFirstScriptOfType<SpawnedItemEntity>();
            if (forcedId >= 0)
            {
                spawned.Id = new MissionObjectId(forcedId, createdAtRuntime: true);
            }
            entity.SetGlobalFrame(in globalFrame);
            Vec3 outward = globalFrame.origin - agent.Position;
            outward.z = 0f;
            if (outward.Normalize() < 0.01f)
            {
                outward = Vec3.Zero;
            }
            Vec3 velocity = outward * DropOutwardSpeed;
            velocity.z = -DropDownwardSpeed;
            Vec3 spin = new Vec3(MBRandom.RandomFloatRanged(-DropSpin, DropSpin), MBRandom.RandomFloatRanged(-DropSpin, DropSpin), MBRandom.RandomFloatRanged(-DropSpin, DropSpin));
            SpawnWeaponAux(mission, entity.WeakEntity, missile, Mission.WeaponSpawnFlags.AsMissile | Mission.WeaponSpawnFlags.WithPhysics, velocity, spin, true, false);
            return spawned.Id.Id;
        }

        private static MatrixFrame GetMeshFrameInEntity(WeakGameEntity entity)
        {
            MatrixFrame frame = MatrixFrame.Identity;
            MetaMesh metaMesh = entity.MultiMeshComponentCount > 0 ? entity.GetMetaMesh(0) : null;
            if (metaMesh == null)
            {
                return frame;
            }
            frame = metaMesh.Frame;
            Mesh mesh = metaMesh.MeshCount > 0 ? metaMesh.GetMeshAtIndex(0) : null;
            return mesh != null ? frame.TransformToParent(mesh.GetLocalFrame()) : frame;
        }

        private static MatrixFrame GetGlobalMeshFrame(WeakGameEntity entity)
        {
            return entity.GetGlobalFrame().TransformToParent(GetMeshFrameInEntity(entity));
        }

        // The global frame that puts the entity's first mesh at meshFrame, without scale (a physics body can't take
        // the bearer's scale the stuck missile carries).
        private static MatrixFrame EntityFrameForMeshAt(MatrixFrame meshFrame, WeakGameEntity entity)
        {
            MatrixFrame target = meshFrame;
            Orthonormalize(ref target);
            MatrixFrame inEntity = GetMeshFrameInEntity(entity);
            Orthonormalize(ref inEntity);
            MatrixFrame frame = target.TransformToParent(inEntity.TransformToLocal(MatrixFrame.Identity));
            Orthonormalize(ref frame);
            return frame;
        }

        private static void Orthonormalize(ref MatrixFrame frame)
        {
            if (!frame.rotation.IsOrthonormal())
            {
                frame.rotation.Orthonormalize();
            }
        }

        private static bool IsWeaponSlot(Agent agent, EquipmentIndex slot)
        {
            return agent.Equipment != null && slot >= EquipmentIndex.WeaponItemBeginSlot && slot < EquipmentIndex.NumAllWeaponSlots;
        }

        private static bool IsSameOrigin(Vec3 a, Vec3 b)
        {
            return (a - b).LengthSquared < SameOriginToleranceSquared;
        }
    }
}

using System;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.MissionViews.Order;

namespace CommanderBattle.View
{
    // The stock troop placer, whose move orders start where the screen centre (or the cursor) meets the ground. Its ray
    // is cut at the map's boundary radius (MissionScreen.ScreenPointToWorldRay), which general mode's high camera can
    // fall short of: the cursor would vanish and clicks would give no order. In general mode the ray goes as far as
    // general mode's own drawing ray. The stock OrderFlag finds its ground natively and needs no change.
    public class CommanderBattleOrderTroopPlacer : OrderTroopPlacer
    {
        private const float GeneralModeRayLength = 3000f;

        private CommanderBattleOrderCameraView _cameraView;

        public CommanderBattleOrderTroopPlacer()
            : base(null)
        {
        }

        protected override bool TryGetScreenMiddleToWorldPosition(out WorldPosition worldPosition, out float collisionDistance, out WeakGameEntity collidedEntity)
        {
            if (_cameraView == null)
            {
                _cameraView = Mission.GetMissionBehavior<CommanderBattleOrderCameraView>();
            }
            if (_cameraView?.IsGeneralMode != true)
            {
                return base.TryGetScreenMiddleToWorldPosition(out worldPosition, out collisionDistance, out collidedEntity);
            }
            MissionScreen.ScreenPointToWorldRay(GetScreenPoint(), out Vec3 rayBegin, out Vec3 rayEnd);
            Vec3 direction = rayEnd - rayBegin;
            if (direction.Normalize() > 0f
                && Mission.Scene.RayCastForClosestEntityOrTerrain(rayBegin, rayBegin + direction * GeneralModeRayLength, out float distance, out WeakGameEntity entity, 0.3f, BodyFlags.CommonFocusRayCastExcludeFlags | BodyFlags.BodyOwnerFlora))
            {
                collisionDistance = distance;
                collidedEntity = entity;
                worldPosition = new WorldPosition(Mission.Scene, UIntPtr.Zero, rayBegin + direction * distance, hasValidZ: false);
                return true;
            }
            worldPosition = WorldPosition.Invalid;
            collisionDistance = 0f;
            collidedEntity = WeakGameEntity.Invalid;
            return false;
        }
    }
}

using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.MountAndBlade.View.MissionViews;
using MathF = TaleWorlds.Library.MathF;

namespace CommanderBattle.View
{
    // While the order bar is open, the third-person camera rises and pulls back from the commander so the formation
    // and the ground in front of it are in view; the mouse wheel sets how far. It falls back when the bar closes.
    // The idea and proportions come from RTS Camera's elevated camera (Li Zhenhuan, MIT licence).
    //
    // Client-only and server-blind: it only shifts the stock camera through the mission's custom camera offsets,
    // which MissionScreen adds to the third-person camera (not in first person). The commander keeps control of
    // their agent, and orders still aim along the camera ray, so nothing else changes in multiplayer.
    public class CommanderBattleOrderCameraView : MissionView
    {
        // Lift used until the player scrolls, in RTS Camera's units: up = 0.3 m and back = 0.7 m per unit.
        private const float DefaultLift = 10f;

        private const float MaxLift = 50f;

        // Lift change per mouse wheel notch.
        private const float LiftPerNotch = 2f;

        private const float UpPerLift = 0.3f;

        private const float BackPerLift = 0.7f;

        // A quick look at the order bar shouldn't swing the camera.
        private const float RiseDelaySeconds = 0.25f;

        private const float RiseSeconds = 0.7f;

        private const float FallSeconds = 1f;

        // Kept for the session, so the player's chosen height survives rounds and map changes.
        private static float _targetLift = DefaultLift;

        private float _lift = DefaultLift;

        private bool _isOrderViewOpen;

        private float _openSeconds;

        // 0 = stock camera, 1 = fully lifted.
        private float _progress;

        private bool _isApplied;

        public override void OnMissionScreenInitialize()
        {
            base.OnMissionScreenInitialize();
            Game.Current.EventManager.RegisterEvent<MissionPlayerToggledOrderViewEvent>(OnToggledOrderView);
        }

        public override void OnMissionScreenFinalize()
        {
            Game.Current.EventManager.UnregisterEvent<MissionPlayerToggledOrderViewEvent>(OnToggledOrderView);
            ClearOffsets();
            base.OnMissionScreenFinalize();
        }

        private void OnToggledOrderView(MissionPlayerToggledOrderViewEvent e)
        {
            _isOrderViewOpen = e.IsOrderEnabled;
            _openSeconds = 0f;
        }

        public override void OnMissionScreenTick(float dt)
        {
            base.OnMissionScreenTick(dt);
            Agent mainAgent = Agent.Main;
            if (mainAgent == null || !mainAgent.IsActive())
            {
                // Dead or between rounds: the spectator camera follows other agents and must not inherit the lift.
                ClearOffsets();
                return;
            }
            if (_isOrderViewOpen)
            {
                _openSeconds += dt;
                HandleScroll();
                if (_openSeconds >= RiseDelaySeconds)
                {
                    _progress = MathF.Min(1f, _progress + dt / RiseSeconds);
                }
            }
            else
            {
                _progress = MathF.Max(0f, _progress - dt / FallSeconds);
            }
            _lift = MBMath.Lerp(_targetLift, _lift, MathF.Pow(2f, -4f * dt));
            if (_progress <= 0f)
            {
                ClearOffsets();
                return;
            }
            float scale = _lift * Smooth(_progress) * mainAgent.AgentScale;
            Mission.SetCustomCameraTargetLocalOffset(Vec3.Up * (UpPerLift * scale));
            // The camera frame looks along -u: a negative y moves it back, and a small negative z tilts it down
            // a little, as RTS Camera does.
            Mission.SetCustomCameraLocalOffset(new Vec3(0f, -BackPerLift * scale, -0.03f * BackPerLift * scale));
            _isApplied = true;
        }

        // Weapon switching is off while the order bar is open, so the wheel is free. Wheel up brings the camera in.
        private void HandleScroll()
        {
            float notches = MissionScreen.SceneLayer.Input.GetDeltaMouseScroll() / 120f;
            if (notches != 0f)
            {
                _targetLift = MBMath.ClampFloat(_targetLift - notches * LiftPerNotch, 0f, MaxLift);
            }
        }

        // Writes zero offsets once, then leaves the mission's camera offsets alone.
        private void ClearOffsets()
        {
            _progress = 0f;
            if (!_isApplied)
            {
                return;
            }
            Mission.SetCustomCameraTargetLocalOffset(Vec3.Zero);
            Mission.SetCustomCameraLocalOffset(Vec3.Zero);
            _isApplied = false;
        }

        private static float Smooth(float x)
        {
            return x * x * x * (x * (6f * x - 15f) + 10f);
        }
    }
}

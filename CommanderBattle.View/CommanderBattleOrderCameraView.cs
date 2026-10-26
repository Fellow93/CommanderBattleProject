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
    // General mode (CommanderBattleGeneralModeView) uses the same camera lifted about three times as far, through
    // IsGeneralMode and ZoomGeneralMode. This view is the only one that writes the camera offsets, so the two never
    // fight over them.
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

        // General mode: about 45 m up and 105 m back by default.
        private const float GeneralDefaultLift = 150f;

        private const float GeneralMinLift = 50f;

        private const float GeneralMaxLift = 300f;

        // Each wheel notch brings the general's camera this much closer (or further, the other way), so a notch
        // feels the same at any height.
        private const float GeneralZoomPerNotch = 0.88f;

        private const float UpPerLift = 0.3f;

        private const float BackPerLift = 0.7f;

        // A quick look at the order bar shouldn't swing the camera.
        private const float RiseDelaySeconds = 0.25f;

        private const float RiseSeconds = 0.7f;

        private const float FallSeconds = 1f;

        // The stock camera only checks collision over its own few metres behind the (lifted) target, so the long
        // pull back can go into a hill behind the player, or under the ground when he looks up (the camera moves
        // back along the view, which then points down). The pull back stops where it would come this close to
        // the terrain.
        private const float TerrainClearance = 2f;

        // The follow camera's own distance behind the lifted point, checked with the pull back.
        private const float FollowDistance = 4f;

        // Metres between terrain samples along the pull back.
        private const float TerrainSampleSpacing = 4f;

        private const int MaxTerrainSamples = 64;

        // Kept for the session, so the player's chosen height survives rounds and map changes.
        private static float _targetLift = DefaultLift;

        private static float _generalTargetLift = GeneralDefaultLift;

        private float _lift = DefaultLift;

        private bool _isOrderViewOpen;

        private float _openSeconds;

        // 0 = stock camera, 1 = fully lifted.
        private float _progress;

        // How much of the pull back the terrain leaves, 0 to 1.
        private float _terrainFactor = 1f;

        private bool _isApplied;

        private bool _isGeneralMode;

        // The offsets only apply in third person: general mode switches to it and back.
        private bool _restoreFirstPerson;

        public override void OnMissionScreenInitialize()
        {
            base.OnMissionScreenInitialize();
            Game.Current.EventManager.RegisterEvent<MissionPlayerToggledOrderViewEvent>(OnToggledOrderView);
        }

        public override void OnMissionScreenFinalize()
        {
            Game.Current.EventManager.UnregisterEvent<MissionPlayerToggledOrderViewEvent>(OnToggledOrderView);
            IsGeneralMode = false;
            ClearOffsets();
            base.OnMissionScreenFinalize();
        }

        // Set by the general mode view while the general is in general mode.
        public bool IsGeneralMode
        {
            get => _isGeneralMode;
            set
            {
                if (value == _isGeneralMode)
                {
                    return;
                }
                _isGeneralMode = value;
                if (value)
                {
                    _restoreFirstPerson = Mission.CameraIsFirstPerson;
                    Mission.CameraIsFirstPerson = false;
                }
                else if (_restoreFirstPerson)
                {
                    // Kept across deaths and rounds, like the player's own choice.
                    _restoreFirstPerson = false;
                    Mission.CameraIsFirstPerson = true;
                }
            }
        }

        // notches: mouse wheel notches, up positive. Wheel up brings the camera in.
        public void ZoomGeneralMode(float notches)
        {
            if (notches != 0f)
            {
                _generalTargetLift = MBMath.ClampFloat(_generalTargetLift * MathF.Pow(GeneralZoomPerNotch, notches), GeneralMinLift, GeneralMaxLift);
            }
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
            if (_isGeneralMode)
            {
                // The camera toggle key (R) still works in MissionScreen.
                if (Mission.CameraIsFirstPerson)
                {
                    Mission.CameraIsFirstPerson = false;
                }
                _progress = MathF.Min(1f, _progress + dt / RiseSeconds);
            }
            else if (_isOrderViewOpen)
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
            _lift = MBMath.Lerp(_isGeneralMode ? _generalTargetLift : _targetLift, _lift, MathF.Pow(2f, -4f * dt));
            if (_progress <= 0f)
            {
                ClearOffsets();
                return;
            }
            float scale = _lift * Smooth(_progress) * mainAgent.AgentScale;
            float up = UpPerLift * scale;
            float back = BackPerLift * scale;
            UpdateTerrainFactor(mainAgent, up, back, dt);
            back *= _terrainFactor;
            Mission.SetCustomCameraTargetLocalOffset(Vec3.Up * up);
            // The camera frame looks along -u: a negative y moves it back, and a small negative z tilts it down
            // a little, as RTS Camera does.
            Mission.SetCustomCameraLocalOffset(new Vec3(0f, -back, -0.03f * back));
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

        // Walks the pull back from the lifted point over the terrain. Coming in is immediate, so the camera never
        // shows the inside of a hill; going out again is eased.
        private void UpdateTerrainFactor(Agent mainAgent, float up, float back, float dt)
        {
            float allowed = 1f;
            Vec3 start = mainAgent.GetEyeGlobalPosition() + Vec3.Up * up;
            float previousClearance = start.z - Mission.Scene.GetTerrainHeight(start.AsVec2) - TerrainClearance;
            // A start already that low is the stock camera's own case (a small lift), left to its collision.
            if (back > 0.01f && previousClearance > 0f)
            {
                // The direction MissionScreen pulls the camera back along: the opposite of where it looks, built
                // as MissionMainAgentController builds the look direction from the camera.
                Mat3 rotation = Mat3.Identity;
                rotation.RotateAboutUp(MissionScreen.CameraBearing);
                rotation.RotateAboutSide(MissionScreen.CameraElevation);
                Vec3 backward = -rotation.f;
                float length = back + FollowDistance;
                int samples = MBMath.ClampInt((int)(length / TerrainSampleSpacing) + 1, 4, MaxTerrainSamples);
                float previousT = 0f;
                for (int i = 1; i <= samples; i++)
                {
                    float t = (float)i / samples;
                    Vec3 point = start + backward * (length * t);
                    float clearance = point.z - Mission.Scene.GetTerrainHeight(point.AsVec2) - TerrainClearance;
                    if (clearance < 0f)
                    {
                        // Where the line meets the clearance, between the two samples.
                        float hit = previousT + (t - previousT) * previousClearance / (previousClearance - clearance);
                        allowed = MBMath.ClampFloat((length * hit - FollowDistance) / back, 0f, 1f);
                        break;
                    }
                    previousClearance = clearance;
                    previousT = t;
                }
            }
            _terrainFactor = allowed < _terrainFactor ? allowed : MBMath.Lerp(allowed, _terrainFactor, MathF.Pow(2f, -4f * dt));
        }

        // Writes zero offsets once, then leaves the mission's camera offsets alone.
        private void ClearOffsets()
        {
            _progress = 0f;
            _terrainFactor = 1f;
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

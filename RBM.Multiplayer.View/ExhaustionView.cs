using RBM.Multiplayer.Network;
using RBM.Multiplayer.Stance;
using TaleWorlds.Core.ViewModelCollection.Generic;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ScreenSystem;

namespace RBM.Multiplayer.View
{
    // RBM's first-person posture tiredness (RBM's Stance/PlayerExhaustion.cs): the 3D view pulses dark like a man
    // fighting for breath, then clears. Started by the server's StanceEvent(Exhausted); the server itself roots the
    // player with a stagger (RBM.Multiplayer's TiredReactionPatch), so there is no client-side input lock here.
    // Same timings and the same native CameraFade prefab on a layer between the scene and the HUD.
    public class ExhaustionView : MissionLogic
    {
        private const float LockSeconds = 1.6f;
        private const float RampInSeconds = 0.25f;
        private const float RecoverSeconds = 0.6f;
        private const float BreathPeriodSeconds = 1.6f;
        private const float BaseDarkness = 0.45f;
        private const float BreathDarkness = 0.47f;
        private const int LayerOrder = -50;

        private StanceNetworkClient _client;
        private MissionScreen _missionScreen;
        private GauntletLayer _layer;
        private BindingListFloatItem _darkness;

        private bool _active;
        private float _startTime;
        private float _lockUntil;

        public override void AfterStart()
        {
            base.AfterStart();
            _client = Mission.GetMissionBehavior<StanceNetworkClient>();
            if (_client != null)
            {
                _client.EventReceived += OnStanceEvent;
            }
            TryCreateLayer();
        }

        private void TryCreateLayer()
        {
            if (_layer != null || !(ScreenManager.TopScreen is MissionScreen missionScreen))
            {
                return;
            }
            _missionScreen = missionScreen;
            _darkness = new BindingListFloatItem(0f);
            _layer = new GauntletLayer("RBMExhaustionFade", LayerOrder);
            _missionScreen.AddLayer(_layer);
            _layer.LoadMovie("CameraFade", _darkness);
        }

        public override void OnRemoveBehavior()
        {
            if (_client != null)
            {
                _client.EventReceived -= OnStanceEvent;
            }
            if (_missionScreen != null && _layer != null)
            {
                _missionScreen.RemoveLayer(_layer);
            }
            _layer = null;
            _missionScreen = null;
            _darkness = null;
            _active = false;
            base.OnRemoveBehavior();
        }

        // As RBM's PlayerExhaustionLogic.TryBegin: first person and on foot only; a second one extends the first.
        private void OnStanceEvent(StanceEvent stanceEvent)
        {
            Agent main = Agent.Main;
            if (stanceEvent.EventType != StanceEventType.Exhausted || _darkness == null || main == null || main.HasMount || !Mission.CameraIsFirstPerson)
            {
                return;
            }
            float now = Mission.CurrentTime;
            if (!_active)
            {
                _active = true;
                _startTime = now;
            }
            _lockUntil = MathF.Max(_lockUntil, now + LockSeconds);
        }

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);
            TryCreateLayer();
            if (!_active)
            {
                return;
            }
            Agent main = Mission.MainAgent;
            float now = Mission.CurrentTime;
            float t = now - _startTime;
            float recovery = now > _lockUntil ? 1f - (now - _lockUntil) / RecoverSeconds : 1f;
            if (main == null || !main.IsActive() || recovery <= 0f)
            {
                _active = false;
                _darkness.Item = 0f;
                return;
            }
            float rampIn = MathF.Clamp(t / RampInSeconds, 0f, 1f);
            float breath = 0.5f * (1f - MathF.Cos(2f * MathF.PI * t / BreathPeriodSeconds));
            _darkness.Item = rampIn * recovery * (BaseDarkness + BreathDarkness * breath);
        }
    }
}

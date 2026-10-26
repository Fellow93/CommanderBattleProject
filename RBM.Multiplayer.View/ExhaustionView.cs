using RBM.Multiplayer.Network;
using TaleWorlds.Core.ViewModelCollection.Generic;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace RBM.Multiplayer.View
{
    // RBM's first-person posture tiredness (RBM's Stance/PlayerExhaustion.cs): the 3D view pulses dark like a man
    // fighting for breath, then clears. Started by the server's StanceEvent(Exhausted); the server itself roots the
    // player with a stagger (RBM.Multiplayer's TiredReactionPatch), so there is no client-side input lock here.
    // Same timings and the same native CameraFade prefab on a layer between the scene and the HUD.
    public class ExhaustionView : StanceClientView<BindingListFloatItem>
    {
        private const float LockSeconds = 1.6f;
        private const float RampInSeconds = 0.25f;
        private const float RecoverSeconds = 0.6f;
        private const float BreathPeriodSeconds = 1.6f;
        private const float BaseDarkness = 0.45f;
        private const float BreathDarkness = 0.47f;
        private const int LayerOrder = -50;

        private bool _active;
        private float _startTime;
        private float _lockUntil;

        public ExhaustionView() : base("RBMExhaustionFade", LayerOrder, "CameraFade")
        {
        }

        protected override BindingListFloatItem CreateDataSource()
        {
            return new BindingListFloatItem(0f);
        }

        public override void OnRemoveBehavior()
        {
            _active = false;
            base.OnRemoveBehavior();
        }

        // As RBM's PlayerExhaustionLogic.TryBegin: first person and on foot only; a second one extends the first.
        protected override void OnStanceEvent(StanceEvent stanceEvent)
        {
            Agent main = Agent.Main;
            if (stanceEvent.EventType != StanceEventType.Exhausted || DataSource == null || main == null || main.HasMount || !Mission.CameraIsFirstPerson)
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
            if (!_active)
            {
                return;
            }
            Agent main = Agent.Main;
            float now = Mission.CurrentTime;
            float t = now - _startTime;
            float recovery = now > _lockUntil ? 1f - (now - _lockUntil) / RecoverSeconds : 1f;
            if (main == null || !main.IsActive() || recovery <= 0f)
            {
                _active = false;
                DataSource.Item = 0f;
                return;
            }
            float rampIn = MathF.Clamp(t / RampInSeconds, 0f, 1f);
            float breath = 0.5f * (1f - MathF.Cos(2f * MathF.PI * t / BreathPeriodSeconds));
            DataSource.Item = rampIn * recovery * (BaseDarkness + BreathDarkness * breath);
        }
    }
}

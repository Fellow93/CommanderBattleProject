using System;
using RBM.Multiplayer.Network;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Network.Messages;

namespace RBM.Multiplayer.Stance
{
    // Client: receives the server's posture messages for the local player. RBM.Multiplayer.View draws from here.
    public class StanceNetworkClient : MissionLogic
    {
        private GameNetwork.NetworkMessageHandlerRegistererContainer _registerer;

        // The agent Hud belongs to.
        private Agent _hudAgent;

        // Last values for the HUD; null until the server sent any (posture off, or not spawned yet).
        public StanceHudUpdate Hud { get; private set; }

        // Raised for each StanceEvent, on the main thread.
        public event Action<StanceEvent> EventReceived;

        // Added from the submodule's OnMissionBehaviorInitialize, after MissionNetwork's own registration point
        // (OnAfterMissionCreated) has passed, so the handlers are registered here.
        public override void EarlyStart()
        {
            base.EarlyStart();
            _registerer = new GameNetwork.NetworkMessageHandlerRegistererContainer();
            _registerer.RegisterBaseHandler<StanceHudUpdate>(HandleHudUpdate);
            _registerer.RegisterBaseHandler<StanceEvent>(HandleEvent);
            _registerer.RegisterBaseHandler<StanceAnimation>(HandleAnimation);
            _registerer.RegisterMessages();
        }

        public override void OnRemoveBehavior()
        {
            _registerer?.UnregisterMessages();
            _registerer = null;
            base.OnRemoveBehavior();
        }

        public override void OnClearScene()
        {
            base.OnClearScene();
            Hud = null;
        }

        // The values are for the agent the player controlled when they were sent; after a takeover (or death) the
        // old agent's bars would show until the server's next update for the new one.
        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);
            Agent main = Agent.Main;
            if (main != _hudAgent)
            {
                _hudAgent = main;
                Hud = null;
            }
        }

        private void HandleHudUpdate(GameNetworkMessage baseMessage)
        {
            Hud = (StanceHudUpdate)baseMessage;
        }

        private void HandleEvent(GameNetworkMessage baseMessage)
        {
            EventReceived?.Invoke((StanceEvent)baseMessage);
        }

        private void HandleAnimation(GameNetworkMessage baseMessage)
        {
            StanceAnimationSync.Play((StanceAnimation)baseMessage);
        }
    }
}

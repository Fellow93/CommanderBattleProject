using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Network.Messages;

namespace CommanderBattle.Network
{
    // Sends one module message: the stock API takes a begin, the message and an end for each.
    public static class CommanderBattleNetwork
    {
        // Server: to every client.
        public static void Broadcast(GameNetworkMessage message)
        {
            GameNetwork.BeginBroadcastModuleEvent();
            GameNetwork.WriteMessage(message);
            GameNetwork.EndBroadcastModuleEvent(GameNetwork.EventBroadcastFlags.None);
        }

        // Server: to one client.
        public static void SendTo(NetworkCommunicator peer, GameNetworkMessage message)
        {
            GameNetwork.BeginModuleEventAsServer(peer);
            GameNetwork.WriteMessage(message);
            GameNetwork.EndModuleEventAsServer();
        }

        // Client: to the server.
        public static void SendToServer(GameNetworkMessage message)
        {
            GameNetwork.BeginModuleEventAsClient();
            GameNetwork.WriteMessage(message);
            GameNetwork.EndModuleEventAsClient();
        }
    }
}

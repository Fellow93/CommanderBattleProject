using TaleWorlds.MountAndBlade.Diamond;
using TaleWorlds.MountAndBlade.Multiplayer;

namespace CommanderBattle
{
    // Registers "CommanderBattle" as a game type (ds_config: GameType CommanderBattle).
    // Joining a server is handled by the stock custom-game flow in the base class.
    public class CommanderBattleMultiplayerGameMode : MissionBasedMultiplayerGameMode
    {
        public CommanderBattleMultiplayerGameMode()
            : base(CommanderBattleSettings.GameModeName)
        {
        }

        // The lobby hands out the server's public IP; ServerAddressOverrides lets a player on the same
        // machine/LAN as the server connect to a local address instead (routers without NAT loopback).
        public override void JoinCustomGame(JoinGameData joinGameData)
        {
            string advertised = joinGameData.GameServerProperties.Address;
            if (ServerAddressOverrides.TryGetOverride(advertised, out string local))
            {
                CommanderBattleLog.Write($"Joining {local} instead of advertised {advertised}");
                joinGameData.GameServerProperties.Address = local;
            }
            base.JoinCustomGame(joinGameData);
        }

        public override void StartMultiplayerGame(string scene)
        {
            CommanderBattleMissions.OpenCommanderBattleMission(scene);
        }
    }
}

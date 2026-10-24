using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace CommanderBattle
{
    // Multiplayer maps: the stock Captain/Skirmish spawn points. Campaign field-battle scenes: the side's
    // spawn path (CommanderBattleSpawnLayout); troops are then moved into their formation's place.
    public class CommanderBattleSpawnFrameBehavior : SpawnFrameBehaviorBase
    {
        private FlagDominationSpawnFrameBehavior _stock;

        public bool UsesSpawnPaths { get; private set; }

        public override void Initialize()
        {
            base.Initialize();
            UsesSpawnPaths = CommanderBattleSpawnLayout.UsesSpawnPaths(Mission.Current);
            CommanderBattleLog.Write(UsesSpawnPaths ? "Spawning from the scene's battle spawn paths" : "Spawning from multiplayer spawn points");
            if (!UsesSpawnPaths)
            {
                _stock = new FlagDominationSpawnFrameBehavior();
                _stock.Initialize();
            }
        }

        public override MatrixFrame GetSpawnFrame(Team team, bool hasMount, bool isInitialSpawn)
        {
            if (_stock != null)
            {
                return _stock.GetSpawnFrame(team, hasMount, isInitialSpawn);
            }
            // Spread out so agents spawning together don't overlap; they are placed in formation right after.
            return CommanderBattleSpawnLayout.GetFrame(Mission.Current, team.Side, (MBRandom.RandomFloat - 0.5f) * 30f, -10f - MBRandom.RandomFloat * 20f);
        }
    }
}

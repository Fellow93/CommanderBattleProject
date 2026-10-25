namespace CommanderBattle
{
    public static class CommanderBattleSettings
    {
        public const string GameModeName = "CommanderBattle";

        public const string MissionName = "MultiplayerCommanderBattle";

        // Each side fields a fixed army of TroopsPerSide soldiers in the formations below. Every formation is
        // led by a player who took command of it, or by the AI when nobody did.
        public const int TroopsPerSide = 500;

        // Slot i is formation (FormationClass)i of the side's team; a Team has 8 regular formations.
        // 4 infantry, 2 ranged, 2 cavalry.
        public static readonly CommanderRole[] FormationRoles =
        {
            CommanderRole.Infantry,
            CommanderRole.Ranged,
            CommanderRole.Cavalry,
            CommanderRole.Cavalry,
            CommanderRole.Ranged,
            CommanderRole.Infantry,
            CommanderRole.Infantry,
            CommanderRole.Infantry
        };

        public static int FormationCount => FormationRoles.Length;

        // TroopsPerSide split over the formations; the first (TroopsPerSide % count) formations get one extra.
        public static int GetTroopCount(int formationIndex)
        {
            int count = TroopsPerSide / FormationCount;
            return count + (formationIndex < TroopsPerSide % FormationCount ? 1 : 0);
        }

        // Baseline the stock spawner multiplies by GetTroopNumberMultiplierForMissingPlayer.
        // The game mode turns it into the formation's exact troop count.
        public const int BotsPerFormationBaseline = 100;

        // Share of the stock multiplayer top speed a horse keeps (CommanderBattleAgentStatCalculateModel).
        public const float HorseSpeedFactor = 0.85f;

        // Each round moves each army within its spawn zone by up to these amounts (CommanderBattleSpawnLayout):
        // sideways, back (away from the enemy) or forward, and turned; the formations of a role also swap places.
        public const float SpawnLateralJitter = 40f;

        public const float SpawnBackJitter = 40f;

        public const float SpawnForwardJitter = 15f;

        public const float SpawnTurnJitterDegrees = 10f;
    }
}

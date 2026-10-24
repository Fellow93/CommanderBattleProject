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
    }
}

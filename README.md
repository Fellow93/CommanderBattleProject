# Commander Battle

Bannerlord multiplayer game mode in the style of Warband Napoleonic Wars commander battles:

- Attackers vs Defenders, stock cultures (`CultureTeam1` / `CultureTeam2`) and stock multiplayer troop classes.
- 500 troops per side, split evenly between that side's commanders (each player is a commander).
- Up to 16 commanders per side.
- The round goes to the last side with soldiers standing. If time runs out, the side with more soldiers left wins.
- No flags, morale or gold. A dead commander can take over one of their own soldiers, the same way as in Captain mode.

## How it works

| Piece | File |
|---|---|
| Game type registration (`CommanderBattle`) | `CommanderBattle/CommanderBattleMultiplayerGameMode.cs` |
| Mission (stock components + the three below) | `CommanderBattle/CommanderBattleMissions.cs` |
| Server rules: teams, troop split, round end | `CommanderBattle/MissionMultiplayerCommanderBattle.cs` |
| Spawning: one formation per commander | `CommanderBattle/CommanderBattleSpawningBehavior.cs` |
| Client: HUD strength bar, compass, scoreboard bot counts | `CommanderBattle/MissionMultiplayerCommanderBattleClient.cs` |
| Client view list (HUD, order bar, scoreboard) | `CommanderBattle.View/CommanderBattleMissionViews.cs` |

**Two hidden teams per side.** A Bannerlord `Team` has only 8 regular formations, and each commander needs one. So each side is made of two allied teams with the same colours and banner. The team-select screen only shows the primary team. When it is full (8 commanders), the server moves the next player into the side's hidden ally team. Win checks, the HUD and the compass count both teams of a side together.

**Exact troop split.** The stock spawner computes each commander's troop count as `stock count × GetTroopNumberMultiplierForMissingPlayer`. The game mode returns the multiplier that makes that equal `500 / commanders`, with the remainder handed out one by one. Commanders and banner bearers are counted separately, so each side always has exactly 500 troops.

The client reports itself as `Captain` to the stock UI, so you get the bot counts on the scoreboard, the troop preloading and the order bar.

## Build

Requires the .NET SDK and the .NET Framework 4.7.2 targeting pack (both are installed on this machine).

```bash
dotnet build "CommanderBattle.sln" -c Release
```

The build installs the module into `<game>\Modules\CommanderBattle\`:

- `SubModule.xml`
- `bin\Win64_Shipping_Client\CommanderBattle.dll`, `CommanderBattle.View.dll`: clients
- `bin\Win64_Shipping_Server\CommanderBattle.dll`: dedicated server

The game folder is taken from the repo location. Override it with `-p:BannerlordDir=X:\path\to\game\`, for example to build against the dedicated server's binaries.

## Run a test server

1. Install **Mount & Blade II: Dedicated Server** (Steam tool, or SteamCMD app `1863440`).
2. Copy `Modules\CommanderBattle` into the dedicated server's `Modules` folder.
3. Create a config in the server's `Modules\Native` folder, for example `ds_config_commander.txt`:

   ```
   ServerName Commander Battle Test
   GameType CommanderBattle
   Map <a Captain/Skirmish map>
   CultureTeam1 vlandia
   CultureTeam2 battania
   MaxNumberOfPlayers 40
   RoundTotal 5
   RoundPreparationTimeLimit 60
   RoundTimeLimit 900
   WarmupTimeLimitInSeconds 120
   MapTimeLimit 60
   start_game_and_mission
   ```

   Use a map that has Captain spawn zones (spawn points tagged `attacker`/`defender` under a `starting` zone). Stock Captain and Skirmish maps have them.
4. Start the server with the module loaded. Listing the server needs a token: run `customserver.gettoken` in the game's console (Alt+~ in the multiplayer lobby). Check the flags against TaleWorlds' hosting docs for your server version.

   ```bash
   DedicatedCustomServer.Starter.exe _MODULES_*Native*Multiplayer*CommanderBattle*_MODULES_ /dedicatedcustomserverconfigfile ds_config_commander.txt /dedicatedcustomserverauthtoken <token> /tickrate 60
   ```

5. Each client enables **Commander Battle** in the launcher's Multiplayer tab and joins through the custom server list.

The mode forces `NumberOfBotsPerFormation 100` (the baseline for the split), `NumberOfBotsTeam1/2 0` and `AutoTeamBalanceThreshold 0`. Values in the config file for these are ignored.

## Test checklist

- [ ] The server starts with `GameType CommanderBattle` and appears in the custom server list.
- [ ] 1v1: each commander gets 500 soldiers and the round ends when one side is wiped out.
- [ ] 2v1: the two commanders on the bigger side get 250 each.
- [ ] 9+ players on one side: the 9th lands in the hidden ally team, keeps the same colours and can give orders to their own formation.
- [ ] Players 1 to 8 and 9 to 16 of a side are allies to each other (no hostility, normal friendly-fire rules).
- [ ] The 17th player on a side is moved to spectators with a message.
- [ ] Time-out: the side with more soldiers left wins the round.
- [ ] A dead commander can take over a soldier in their formation.
- [ ] Server tick rate holds with 1000+ agents. CPU is the main risk.

## Known limitations

- **Class loadout troop number.** The class loadout screen shows the stock troop count (100 × class multiplier), not the real share. The real number is what spawns and what the scoreboard shows.
- **Team-select player count.** The team-select screen only counts players in the primary team of each side.
- **Agent visuals of the other team.** During preparation, players in the hidden ally team don't see the primary team's agent visuals, and the other way round. The stock filter works per team. Once the round starts, everyone sees all agents.
- **Agent cap.** The engine caps a mission at about 2047 agents, and horses count. 1000 soldiers plus 32 commanders fits, but cavalry-heavy armies eat into the margin.

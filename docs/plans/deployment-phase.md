# Deployment phase

**Status:** idea · **Size:** L

## Goal

Before the battle starts, each commander can move their formation within their side's spawn zone and set its facing and arrangement, as in a singleplayer deployment. In commander battles much of the skill is in where you stand when the fighting starts. Today the armies start wherever the seeded layout puts them.

## What exists today

- The round's **preparation phase** (`MultiplayerRoundState.Preparation`, `RoundPreparationTimeLimit`) is when players pick a formation and class. During it, commanders only have agent *visuals*. The agents are built when the round starts (`CommanderBattleSpawningBehavior`: visuals become agents, then `SpawnAiFormations` fills the empty formations).
- **The layout** (`CommanderBattleSpawnLayout`) is computed on the server and on every client from the round seed (`SpawnLayoutSeed`). Server and clients must agree on it.
- **The order bar and lifted camera** already work for a commander with a live formation (`CommanderBattleOrderCameraView`).

## Design options

**A. Frozen start (recommended first).** After the round starts and every army has spawned, add a deployment window of N seconds (say 60):
- Every agent is held in place: no attacks, no AI tactic ticking, enemies can't reach each other.
- A commander can give movement, facing and arrangement orders to their own formation, limited to their side's deployment area. Orders outside it are clamped or refused.
- The AI-led formations keep the layout's positions, or the team AI is ticked once to place them.
- When the timer ends, or every commander presses "Ready", the battle starts.

It reuses the real order bar and real agents, so what you place is what you fight with.

**B. Plan on the visuals.** Let commanders drag their formation's position on a map or overview during preparation, and spawn the formation there. The fight starts at once, but it needs a placement UI, and the spawn position has to go to every client (the layout is no longer only the seed).

Start with A. Look at B only if A's extra minute per round feels too long.

## Things to work out

- **How to hold everyone.** Options are `Mission.AllowAiTicking = false` plus blocking player input, an invisible barrier, or the engine's deployment state (`MissionDeploymentPlan`, `DeploymentHandler` in singleplayer). Read the singleplayer deployment code in the decompiled sources before choosing.
- **The deployment area.** Use the spawn zone that the layout already uses, plus a margin. Show it to players, perhaps with the stock deployment boundary visuals from singleplayer.
- **Timer and HUD.** The stock round timer shows the round time. Deployment needs its own countdown, sent from the server (a `GameNetworkMessage`, in the shared dll).
- **Round time.** Either deployment comes off the round time, or the round timer starts after deployment. The second is fairer to the attackers, who must win before time runs out.
- **RBM.** RBM's terrain read runs "once both armies stand on the field". Check that it still runs at the right time, and that RBM's tactics don't move formations during deployment.
- **Players joining late** during deployment get their formation's orders like everyone else.

## Files it touches

- `CommanderBattle/MissionMultiplayerCommanderBattle.cs`: the deployment state, the timer, the start of the battle.
- `CommanderBattle/CommanderBattleSpawningBehavior.cs`: holding agents after spawn.
- `CommanderBattle/AI/CommanderBattleTeamAI.cs`, `RBM.Multiplayer/RbmMpServerLogic.cs`: no AI moves during deployment; tactic picked after it.
- `CommanderBattle/Network/`: a deployment start/end message with the time left.
- `CommanderBattle.View/`: countdown, Ready button, deployment area display.

## Open questions for the user

1. Deployment length: fixed (60 s?) or a server option?
2. Should a side be able to end deployment early once all its commanders are ready?
3. Can commanders see the enemy's deployment, or is it hidden until the battle starts?

## Done when

- In game: a commander moves and turns their formation during deployment and can't leave the area. No one can fight until it ends. The AI formations keep their places.
- Logs: deployment start and end per round, with the time it ended and why (timer or all ready).
- A second round goes through deployment cleanly.

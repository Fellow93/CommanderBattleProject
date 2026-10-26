# "The general has fallen!"

**Status:** idea · **Size:** S

## Goal

When a side's general dies, everyone hears about it: a centre-screen announcement with a sound, a darker message for his own side. It's one of the big moments of a round, and today it passes unnoticed unless you see it happen.

## What exists today

- The server knows the general of each side: `Team.GeneralAgent`, set by `CommanderBattleTeams.SetGeneral` for the AI general (`CommanderBattleSpawningBehavior.SpawnAiGeneral`), a player general when he spawns (`HandlePeerSpawnedFromVisuals`) and a rider he takes over (`MissionMultiplayerCommanderBattle`, the takeover code). These team fields are server only.
- `MissionMultiplayerCommanderBattle.OnAgentRemoved` already handles a player general's death (`ReleaseGeneralCommandOnDeath`, log "... went back to the AI (the general fell)"). Nothing there covers the AI general.
- The client already shows a centre pop-up with a sound for the general's suggestions (`MissionMultiplayerCommanderBattleClient.GeneralCommand.cs`: `MBInformationManager.AddQuickInformation`, a message log line, `SoundEvent.PlaySound2D("event:/ui/notification/alert")`).
- Network messages live in `CommanderBattle/Network`, broadcast with `CommanderBattleNetwork.Broadcast`.

## Design

**Server.** In `OnAgentRemoved`, when the removed agent is its team's `GeneralAgent`, the round is in progress (`RoundController.IsRoundInProgress`) and it was killed or knocked out (not removed at round end or cleared with the scene), broadcast `GeneralFallen`:
- the side;
- the general's name: the player's name, or "the <culture> general" for the AI one;
- the killer, if any: a player's name, or the killer's formation (`FormationSlot.DisplayName`, or its name once [named formations](named-formations.md) exist); none for friendly fire, a fall or a missile from nobody known.

Only the general himself counts, not his horse or a rider. Log it: "Defender general (Fellow93) fell, killed by Khan (Cavalry 3)".

**Client.** On `GeneralFallen`:
- **Enemy side and spectators:** "The Vlandian general has fallen!" in the centre, then "slain by Khan (Cavalry 3)" in the message log. A horn or a crowd roar.
- **His own side:** "Our general has fallen." in the centre, in a darker colour, with a sombre sound (a low horn, a drum).
- **The general himself** gets nothing extra: he is dead and sees the spectator card.

**Several deaths a round.** A player general who dies takes over a rider and becomes the general again (`SetGeneral`), so he can fall more than once. See the open questions.

## Things to work out

- Which FMOD events exist for a horn, a crowd roar and a drum. Look through the game's sound events, play them in game with `SoundEvent.PlaySound2D`, and check the id (`SoundEvent.GetEventIdFromString` gives -1 for an unknown one). Log a missing id once rather than failing silently.
- How `CommanderBattleFleeingLogic` removes a fleeing AI general (which `AgentState`), so a general who flees doesn't show as fallen, or gets his own "has fled the field" line.
- Whether `AddQuickInformation` can take a colour for the darker message, or the own side needs a small Gauntlet layer of its own. Read `..\RealisticBattleProject\docs\BannerlordGauntletUI.md` before building one.

## Files it touches

- New `CommanderBattle/Network/GeneralFallen.cs` (shared, never under `#if SERVER`).
- `CommanderBattle/MissionMultiplayerCommanderBattle.cs` (`OnAgentRemoved`), or a new partial `MissionMultiplayerCommanderBattle.Announcements.cs` that later announcements can share.
- `CommanderBattle/MissionMultiplayerCommanderBattleClient.cs`: register the handler and show it.
- `README.md`: a line under "The general".

## Open questions for the user

1. A player general who falls, takes over a rider and falls again: announce every time, only the first fall, or the first plus a short "... has fallen again"?
2. Name the killer, or only announce the death?
3. Sounds: a horn for the enemy and a drum or low horn for his side, or the same sound for both?
4. Should a fleeing AI general get a "has fled the field" line too?

## Done when

- In game: killing the enemy general shows the announcement and plays the sound for both sides, and the darker message on his side. Spectators see the neutral version. The same happens for an AI general and a player general.
- Nothing shows at round end, on a restart or when he flees (unless question 4 says so).
- Log: one "general fell" line per death, with the killer.

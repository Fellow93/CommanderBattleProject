# Horn signals

**Status:** in progress (v1 built, not played; the pop-up is text only, no icon yet) · **Size:** M

## Goal

The general has a few horn calls: advance, hold and fall back. His whole side hears the horn and sees a short icon on the HUD; the enemy hears it from where he stands. And when he orders a player's formation directly, that commander hears a horn instead of a beep. It's Napoleonic Wars-style flavour, and a quick way to talk to every commander at once.

## The user's decisions (2026-10-27)

- **No new keys.** The general sounds side-wide calls from the stock bark wheel: Attack → Advance, Defend → Hold, Regroup → Fall back. His voice bark still plays; the horn sounds on top. The other six barks are unchanged.
- **Orders to a player's formation sound a horn** to that commander only (2D), in place of today's alert beep on a suggestion.
- **Order → horn:** Charge and Advance → attack horn; Move, Hold and Follow → move horn; Fall back and Retreat → retreat horn.
- **The enemy hears side-wide calls** as a positional horn from the general, with no text (it gives away where he is).
- **Only a living general** sounds side-wide calls (the bark needs his agent anyway).
- **Side-specific horns** where the game has them (the MP siege retreat horns).
- **8 s cooldown** per side for side-wide calls.
- Not in v1: the AI general sounding horns on tactic changes, more calls, horns that move the AI.

## What exists today

- **Stock horn sounds** (read in the decompiled sources, not yet heard in game):
  - `event:/ui/mission/horns/move`, `.../attack`, `.../retreat`: the singleplayer team AI's tactic horns (`TacticComponent.MoveHornSoundIndex` etc., played by `SoundTacticalHorn` with `SoundEvent.PlaySound2D` and a 10 s cooldown). Our team AI runs on the server, where `PlaySound2D` reaches nobody.
  - `event:/alerts/horns/move`, `.../attack`, `.../retreat`, `.../reinforcements`: sally-out and naval notifications (`SallyOutMissionNotificationsHandler`), and `SoundManager.StartOneShotEvent(..., position)` in story missions.
  - `event:/multiplayer/retreat_horn_attacker` and `..._defender`: MP siege, played at a position (`MissionMultiplayerSiegeClient`, `MBSoundEvent.PlaySound(id, position)`).
- **The bark wheel** (`ViewCreator.CreateMissionMainAgentCheerBarkControllerView`, already in our views). The client sends `NetworkMessages.FromClient.BarkSelected(IndexOfBark)`; the server's `MissionNetworkComponent.HandleClientEventBarkSelected` calls `ControlledAgent.HandleBark`, which makes the voice (`SkinVoiceManager.VoiceType.MpBarks`: 0 Defend, 1 Attack, 2 Help, 3 Spot, 4 Thanks, 5 Sorry, 6 Affirmative, 7 Negative, 8 Regroup) and sends `BarkAgent` to the sender's team. A second handler of ours runs after the stock one, as the cheer does ([cheer-with-your-men.md](cheer-with-your-men.md)).
- **Suggestions.** The server sends a `GeneralSuggestion` (with its `GeneralSuggestionKind`) to the formation's commander and echoes it to the general. The client's `AnnounceSuggestion` (`MissionMultiplayerCommanderBattleClient.GeneralCommand.cs`) shows the pop-up and plays `event:/ui/notification/alert` to the commander.
- **The general:** `FormationSlot.IsGeneral` on the server; `IsGeneralHere` on the client.

## Design

**Horn kinds.** `HornSignalKind { Advance, Hold, FallBack }`, with one place that maps a kind (and a side) to its event id and a `GeneralSuggestionKind` to a kind. Each id is looked up once (`SoundEvent.GetEventIdFromString`, -1 when unknown); a missing one is logged once and falls back to the alert beep.

| Kind | Sound (to confirm by ear) | Text |
|---|---|---|
| Advance | `horns/attack` | "Advance!" |
| Hold | `horns/move` | "Hold!" |
| FallBack | `horns/retreat`, or the side's `retreat_horn_*` | "Fall back!" |

**1. Order horns (client only).** In `AnnounceSuggestion`, the commander's sound becomes the horn for the suggestion's kind. The general's echo stays silent, as today. No new message.

**2. Side-wide calls.** `MissionMultiplayerCommanderBattle.Horns.cs` (new partial, server):
- A second `BarkSelected` handler, registered in `AddRemoveMessageHandlers`. It always returns true (false would stop the handlers after it and mark the packet invalid).
- Bark 1 → Advance, 0 → Hold, 8 → FallBack; others are ignored. The sender must be his side's general with a living agent, the round in progress and his side off its 8 s cooldown.
- Broadcast `HornSignal(side, kind, position)`, the position being the general's agent. Log: "Attacker general (Fellow93) sounded Advance". The cooldown is cleared in `OnClearScene`.

**Client, on `HornSignal`:**
- **His side and spectators:** the horn, a centre pop-up "The general sounds: Advance!" for about 3 s with an icon, and a line in the message log.
- **The enemy:** the horn at the general's position (`MBSoundEvent.PlaySound(id, position)`), no text.

## Things to work out

- Listen to every horn event above, 2D and positional: which sound like advance / hold / retreat, which have a side variant, whether a positional one carries across a 500-a-side battle (if not, the enemy gets it 2D but quieter, or a louder event).
- The icon: text-only `MBInformationManager.AddQuickInformation` for a first pass, then a small Gauntlet layer with stock order-bar sprites (find their real names in the sprite sheets). Read `..\RealisticBattleProject\docs\BannerlordGauntletUI.md` before building one.
- Whether the general's bark wheel opens in Commander Battle at all (its barks don't depend on cosmetics, unlike taunts).

## Files it touches

- New `CommanderBattle/Network/HornSignal.cs` (shared, never under `#if SERVER`) and `CommanderBattle/HornSignals.cs` (kind, sound ids, mappings).
- New `CommanderBattle/MissionMultiplayerCommanderBattle.Horns.cs`: bark handler, cooldown, broadcast, round reset.
- New `CommanderBattle/MissionMultiplayerCommanderBattleClient.Horns.cs`: `HornSignal` handler, sound and announcement.
- `CommanderBattle/MissionMultiplayerCommanderBattleClient.GeneralCommand.cs`: the suggestion's horn.
- `README.md`: lines under "The general" and the test checklist.

## Done when

- In game: the general's Attack / Defend / Regroup barks sound Advance / Hold / Fall back; his side hears the horn and sees the pop-up, the enemy hears it from his position with no text. A second call within 8 s gives the bark only. A non-general's bark is unchanged.
- An order from the general to a player's formation plays the matching horn to that commander only.
- Nothing plays between rounds or after a restart.
- Log: one line per side-wide call; no exceptions, no "missing sound" line.

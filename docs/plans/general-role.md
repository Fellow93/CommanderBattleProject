# General role

**Status:** idea · **Size:** L

## Goal

One player per side can be the **general**. The general steers the team AI that leads every formation nobody commands. They don't move single formations by hand. They set the plan: which tactic, where to attack, when to hold. Players who command formations stay in charge of their own.

## What exists today

- **Team AI** leads the formations nobody commands: `CommanderBattleTactic` without RBM, RBM's tactics with RBM.Multiplayer (`RbmMpTactics`). The tactic is chosen by the AI itself.
- **The AI status panel** (Left Alt; `CommanderBattleAIStatusSync`, `SideAIStatus`, `CommanderBattleAIStatusView`) already sends each side its tactic and, for every formation, its behaviour, order and destination. That is most of what a general needs to see.
- Teams are created with `isPlayerGeneral: false, isPlayerSergeant: true` (`MissionMultiplayerCommanderBattle.CreateTeams`).

## Design

**Who is general.** Choose one:
- the first player to claim it, from the formation list (N), shown as an extra slot;
- a vote within the side;
- the longest-serving commander on the side.

The general can also command a formation, or stay at the back as a pure general. Decide which (see open questions).

**What the general can do,** in order of effort:
1. **Choose the tactic.** Pick from the side's available tactics (stock or RBM), or leave it on "AI chooses". The server sets the team's tactic and keeps it until the general changes it.
2. **Stance.** Advance / hold / fall back for all AI-led formations. This sets how the tactic acts, or gives a team-wide move order the tactic must respect.
3. **Objective marker.** The general marks a point on the map. AI-led formations attack it or defend it, depending on the stance.
4. **Direct orders to AI formations.** The general gives a normal order to one AI-led formation through the order bar. This is the most powerful and also the hardest, because RBM's tactics will try to overwrite the order.

Start with 1 and 2. They are mostly a choice the tactic already makes, and the panel can show the result.

## Things to work out

- **RBM's tactics** choose themselves by culture and terrain. Forcing one means setting it on the team and stopping RBM from replacing it.
  - **Checked 2026-10-26:** `TeamAIComponent.MakeDecision` runs every 5 s. It takes the best `GetTacticWeight()` from the team's option list and keeps the current tactic unless another weighs 1.5× more.
  - So forcing a tactic means `ClearTacticOptions`, then adding only the chosen tactic, then `ResetTactic(keepCurrentTactic: false)`. "AI chooses" puts the full list back.
  - `RbmMpTactics.Register` builds those lists each round. Its `Add` also registers each tactic with RBM's `TacticSelection`, so a forced tactic must go through it too.
  - The admin "force tactic" (admin-commands item 5) is the same mechanism.
  - The stance can filter the list: offensive tactics only, or defensive only. Attackers have no defensive tactics today; check whether RBM's defensive ones work for the attacking side.
- **Player formations in the tactic** (handoff open item #3): the tactic counts players' troops and may make a player's formation its "main" one. Fix that first or together with this, or the general's plan will lean on formations that ignore it.
- **Network.** The client asks (`RequestGeneral`, `SetSideTactic`, `SetSideStance`, `SetObjective`). The server checks the sender is the side's general, applies it, and sends the side the result. All messages go in the shared dll.
- **The general leaving** or disconnecting gives the AI full control back.

## Files it touches

- `CommanderBattle/FormationSlots.cs` or a new `SideGeneral` holder: who is general per side.
- `CommanderBattle/MissionMultiplayerCommanderBattle.cs`: claiming, releasing, checking requests.
- `CommanderBattle/AI/*`, `RBM.Multiplayer/RbmMpTactics.cs`: forced tactic, stance, objective.
- `CommanderBattle/Network/`: the request and state messages.
- `CommanderBattle.View/`: a general slot in the formation list, a tactic/stance panel, the objective marker.

## Open questions for the user

1. How is the general chosen (first claim, vote, other)?
2. Does the general also lead a formation, or only the AI?
3. Which powers in the first version: tactic only, tactic + stance, or up to the objective marker?

## Done when

- In game: a general picks a tactic and stance, the AI panel shows them, and the AI-led formations act on them. When the general leaves, the AI chooses again.
- Logs: general claimed/released, every tactic and stance change.

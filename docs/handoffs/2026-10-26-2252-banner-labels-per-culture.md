# Handoff: banner labels over soldiers, banners saved per culture

Session of 2026-10-26, written 22:52. It follows `2026-10-26-2227-commander-banners.md` (same session).

## State

- **Not committed.** The user wants to play it first.
- **Installed on the client only**, at 22:50 (a client-only build of `CommanderBattle.View.csproj`, which also installs the client `CommanderBattle.dll`).
- **The server is unchanged:** pid 33880, started by the round-weather session at 22:35 with the banner build. No server code changed and no message changed, so it needs no restart.
- The server target (net6.0) compiles (scratch build).

## What the user reported

The user played one round on server 33880 and picked sigil 310, shade 4 in preparation. The log: "Fellow93 picked banner sigil 310, shade 4" at 22:42:02, round 1 InProgress at 22:42:15.
- The small banner over each soldier with Left Alt still showed the old banner.
- They asked for the banner to be saved per culture.

## What was done

- **The label cause.**
  - `Mission.SpawnAgent` writes `CreateAgent` before `Agent.Build` sets `agent.Formation`, so the client gets formation index -1.
  - `MissionNetworkComponent.HandleServerEventCreateAgent` then uses `new Banner(owner's Peer.BannerCode, team colours)`, never `formation.Banner`.
  - `MissionAgentLabelView` draws that banner.
  - The shields are unaffected: they come with the weapons.
- **The label fix** (`MissionMultiplayerCommanderBattleClient.Banners.cs`):
  - `UpdateFormationBanners` sets each commander's local `VirtualPlayer.BannerCode` to their commander banner. The original is kept in `_profileBannerCodes` and read by `GetProfileBannerCode`, which the picker uses.
  - `OnAgentBuild` matches the fresh per-agent banner against each commander banner recoloured in team colours, then `Deserialize`s the shaded code into it. Our client logic is earlier in the behaviour list than the label view.
  - Side effect: the scoreboard and kill feed show the commander's banner.
- **Per culture** (`CommanderBattleClientConfig.cs`):
  - `<Banners><Banner culture=.. sigil=.. shade=../></Banners>`.
  - `TryGetBanner` and `SaveBanner` take the culture id. `GetSavedBanner(side, ...)` uses the side's culture.
  - The picker title reads "Your banner · <culture>".
  - The first build's root-level `<Banner>` is dropped on the next save. Until then it is ignored, so the first test's pick is not carried over.
- README and plan updated.

## Not verified (needs play)

- The Alt labels over your soldiers show your banner with its shade. The other side's players see it too.
- The scoreboard banner change looks right.
- Picks stay apart per culture across a map change.

## Traps

- **A new agent has no formation on the client when it is built** (see above). Anything the stock client derives from `formation.*` in `HandleServerEventCreateAgent` falls back to the owner peer's data.

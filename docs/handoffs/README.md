# Session handoffs

One file per working session, so the next session, or another one running alongside it, can pick up where it left off without the chat history. The rule is in `CLAUDE.md` ("Session handoffs").

- **Name:** `YYYY-MM-DD-HHmm-<short-topic>.md`, the local date and 24-hour time the handoff was written, for example `2026-10-25-1013-rbm-multiplayer-and-commander-battle.md`. The names then sort by time, even with several sessions on one day.
- **Newest first:** the last files in the folder listing are the newest. Read the latest one or two before starting work.
- **Leave old handoffs alone.** They are a record. When something in an older handoff is done or out of date, say so in the new one.

## What a handoff contains

1. **State.** What is committed and what isn't; what is installed and running (game and server builds, their pids). Any work that came from another session.
2. **What was done.** Grouped by area, naming the files.
3. **Verified vs not.** What was seen in a log or in game, and what only compiles.
4. **Open items.** Each with a suggested next step, plus decisions the user made, such as "keep morale off".
5. **Traps found.** Anything that cost time and isn't in `CLAUDE.md` yet; add it to `CLAUDE.md` as well when it is a lasting rule.

## Index

| Written | Handoff | Topic |
|---|---|---|
| 2026-10-25 10:13 | [2026-10-25-1013-rbm-multiplayer-and-commander-battle.md](2026-10-25-1013-rbm-multiplayer-and-commander-battle.md) | RBM.Multiplayer (RBM AI + Stance in Commander Battle), archer close combat, AI status panel, spawn randomisation, round and kick rules, texts |
| 2026-10-25 12:02 | [2026-10-25-1202-choosing-troops.md](2026-10-25-1202-choosing-troops.md) | Choosing troops: the class sets the formation's size (budget × stock troop count / role average), counts synced with the layout seed |
| 2026-10-25 12:21 | [2026-10-25-1221-choosing-troops-first-run.md](2026-10-25-1221-choosing-troops-first-run.md) | First server run: troop counts verified; net6.0 dll in the client folder fixed (per-framework obj\) |
| 2026-10-25 13:58 | [2026-10-25-1358-troop-types-phase-1.md](2026-10-25-1358-troop-types-phase-1.md) | Troop types phase 1: Vlandia's 8 types, troop selection screen, pick applies next round; installed, server running |
| 2026-10-25 15:16 | [2026-10-25-1516-troop-types-v2-all-cultures.md](2026-10-25-1516-troop-types-v2-all-cultures.md) | Troop types v2: per-culture split with horse archers, class → weight → weapons screen, all six cultures' XML; installed, server running |
| 2026-10-25 15:53 | [2026-10-25-1553-culture-variance-pass.md](2026-10-25-1553-culture-variance-pass.md) | Weight labels by multiplier bands; culture variance pass (RBM-inspired) for all six cultures; AI setup order from the data; installed, server running |
| 2026-10-25 16:11 | [2026-10-25-1611-smaller-armies-formation-list.md](2026-10-25-1611-smaller-armies-formation-list.md) | Smaller armies (54 foot / 44 mounted per formation) for server load; formation list restyled; agents 1,307 → 919, fight FPS avg 55 |
| 2026-10-25 16:38 | [2026-10-25-1638-posture-animation-sync-fix.md](2026-10-25-1638-posture-animation-sync-fix.md) | Posture animations reach clients: StanceNetworkSync never turned on (OnBehaviorInitialize skipped), SetActionChannel patch ordered first; verified on AI agents |
| 2026-10-25 16:40 | [2026-10-25-1640-scoreboard-formation-column.md](2026-10-25-1640-scoreboard-formation-column.md) | "Formation" column on the TAB scoreboard (weight · role · number · weapon setup); client installed, server DLL locked by the running server |
| 2026-10-25 16:50 | [2026-10-25-1650-frontline-mp-check.md](2026-10-25-1650-frontline-mp-check.md) | Frontline audit in MP: worker-thread ClearTargetFrame broadcast moved to the main thread (TargetFrameClearSync); Frontline counters in the formation log |
| 2026-10-25 22:08 | [2026-10-25-2208-spawn-path-sync.md](2026-10-25-2208-spawn-path-sync.md) | Spawn path picked from the round seed (stock pick was random per machine: server and client planned on different paths); Frontline round in the logs, hold-release flood found |
| 2026-10-25 22:30 | [2026-10-25-2230-slower-ranged-fire.md](2026-10-25-2230-slower-ranged-fire.md) | Bows and crossbows draw and reload at 75% speed (stats model, both sides); compiles, not installed (server and launcher held the DLLs) |
| 2026-10-25 22:33 | [2026-10-25-2233-slower-ranged-fire-installed.md](2026-10-25-2233-slower-ranged-fire-installed.md) | Slower ranged fire installed on both sides; server pid 34388 running, 0 failed patches, clean logs; not played yet |
| 2026-10-25 22:36 | [2026-10-25-2236-preparation-visuals-fix.md](2026-10-25-2236-preparation-visuals-fix.md) | Spawn path sync verified (server and client logs match over 2 rounds); preparation visuals never showed (stock helper missed the runtime sp_visual markers), markers now handed to it; client installed, not verified |
| 2026-10-25 22:45 | [2026-10-25-2245-client-zip-scene-autolink.md](2026-10-25-2245-client-zip-scene-autolink.md) | Client test zip in `artifacts\`; clients link the campaign scenes themselves at load (CampaignSceneLinker); client installed, not run yet |
| 2026-10-25 22:50 | [2026-10-25-2250-server-restart-rbm-bundle.md](2026-10-25-2250-server-restart-rbm-bundle.md) | Server restarted on the zip's build (pid 37288, 0 failed); client zip re-packed; second bundle with the singleplayer RBM module |
| 2026-10-25 23:53 | [2026-10-25-2353-order-ui-after-map-change.md](2026-10-25-2353-order-ui-after-map-change.md) | Order bar and F keys dead after a map change, attacks gave orders: stock MP order UI gated on NumberOfBotsPerFormation, now set on the client too; installed, not played yet |
| 2026-10-26 00:10 | [2026-10-26-0010-short-logs.md](2026-10-26-0010-short-logs.md) | Mod logs cut to what matters (rounds and results, formations, picks, failures, our exceptions); diagnostics behind `CommanderBattleLog.Detailed` / `RbmMpLog.Detailed`; installed, not run yet |
| 2026-10-26 00:20 | [2026-10-26-0020-hold-release-flood.md](2026-10-26-0020-hold-release-flood.md) | ClearAgentTargetFrame flood cut: hold releases sent only for holds clients got (the ...Synched setters); server pid 11340 running, 94 applied / 0 failed; not played yet |
| 2026-10-26 08:40 | [2026-10-26-0840-review-defect-fixes.md](2026-10-26-0840-review-defect-fixes.md) | Code review; five defects fixed (diagnostics with logging off, exception hook, layout seed across maps, missing resets, script defaults); server pid 8120 running, 0 failed; not played yet |
| 2026-10-26 09:02 | [2026-10-26-0902-aggressive-frontline.md](2026-10-26-0902-aggressive-frontline.md) | Frontline front rank more aggressive in MP: attack weight 1.3, back step 0.75 (`RbmMpSettings`); server pid 5996 running, 0 failed; not played yet |
| 2026-10-26 09:20 | [2026-10-26-0920-admin-commands.md](2026-10-26-0920-admin-commands.md) | Admin panel gets a Commander Battle group (Restart Round, Swap Sides, Remove Commander; `AdminCommand`, `IsAdmin` check); map/factions are the stock Mission Options; server pid 9060 running, 0 failed; not played yet |
| 2026-10-26 09:22 | [2026-10-26-0922-admin-password-param.md](2026-10-26-0922-admin-password-param.md) | `Install-CampaignScenes.ps1 -AdminPassword` writes the admin password into the server config (kept on reruns, `''` removes it); real config not yet rewritten |
| 2026-10-26 09:46 | [2026-10-26-0946-admin-join-usable-maps.md](2026-10-26-0946-admin-join-usable-maps.md) | Stock Join as Admin instead of an in-game login; 16 players; admin panel map list filled (`add_map_to_usable_maps ... CommanderBattle`); server pid 30744 running, 0 failed |
| 2026-10-26 10:09 | [2026-10-26-1009-own-admin-panel.md](2026-10-26-1009-own-admin-panel.md) | Stock admin panel groups replaced by ours: Next Map (own map list from the pool), Match, Commander Battle, Players; server pid 30000 running, 0 failed; not tried in game yet |
| 2026-10-26 10:40 | [2026-10-26-1040-review-refactors-1-4.md](2026-10-26-1040-review-refactors-1-4.md) | Review refactors 1–4: network send helpers, `Sides`/`AllSlots`, cached class lists, `MySide`/`MySlot`; server pid 38088 running, 0 failed; not played yet |
| 2026-10-26 11:13 | [2026-10-26-1113-layer-view-base-classes.md](2026-10-26-1113-layer-view-base-classes.md) | Layer-view base classes: `CommanderBattleLayerView<T>` for the 4 CommanderBattle views, `StanceClientView<T>` for the 2 RBM views; server pid 29532 running, 0 failed; not played yet |
| 2026-10-26 11:27 | [2026-10-26-1127-take-over-hint.md](2026-10-26-1127-take-over-hint.md) | Dead commander sees the stock "Press F to take control" line on their own soldier's spectator card (stock turns it on only in flag modes); server pid 9908 running, 0 failed; not played yet |
| 2026-10-26 11:58 | [2026-10-26-1158-take-over-hint-fix-n-hint.md](2026-10-26-1158-take-over-hint-fix-n-hint.md) | F hint never showed (`IsAIControlled` is false on clients, now `!IsPlayerControlled`; CLAUDE.md convention); "Press N" line in the Alt panel; server pid 2564 running, 0 failed; not played yet |
| 2026-10-26 12:20 | [2026-10-26-1220-sidearm-rule.md](2026-10-26-1220-sidearm-rule.md) | Sidearms only for spear, pike, lance and skirmisher setups; swords, axes, maces and swung two-handers carry none; Sturgian levy gets a raider axe; XMLs installed, server 2564 still on the old data |
| 2026-10-26 12:34 | [2026-10-26-1234-thrown-weapons.md](2026-10-26-1234-thrown-weapons.md) | Pila couldn't be thrown (spear-first craft, 1 per slot, never mounted, invisible to RBM skirmish AI); Empire skirmishers get Imperial javelins 5 + 3; server pid 25088 running, 0 failed; not played yet |
| 2026-10-26 12:48 | [2026-10-26-1248-play-test-verified.md](2026-10-26-1248-play-test-verified.md) | Play test: troop selection, sidearms, javelins, admin panel, F/N hints, order bar, slower ranged fire, Frontline, posture bars all reported working; no code change |
| 2026-10-26 12:50 | [2026-10-26-1250-two-player-test-verified.md](2026-10-26-1250-two-player-test-verified.md) | Two-player test: per-culture classes, Khuzait horse archers, pikemen without shields reported working (not in local logs); general role design notes; no code change |

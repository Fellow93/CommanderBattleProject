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

# Project plans

Feature ideas worked out far enough to pick one up and start. One file per feature. Pick one, check its open questions with the user, and update its status when work starts or lands.

- **Status:** `idea` (not refined), `ready` (design agreed, can start), `in progress`, `done`, `dropped`. Keep the index below in step.
- **When a plan is done,** move what it built into `README.md` (behaviour) and the session's handoff (what changed). The plan stays as a record; set it to `done` and link the handoff.
- **Plans are not specs.** The code wins. When the code shows a plan is wrong, fix the plan.

Each plan has the same sections: goal, what exists today, design, files it touches, open questions, size, and "done when" (what to check in game and in the logs).

## Index

| Plan | Status | Size | One line |
|---|---|---|---|
| [deployment-phase.md](deployment-phase.md) | idea | L | Commanders place their formation before the battle starts |
| [general-role.md](general-role.md) | in progress (phase 0 played: general + 6 bodyguards in a wedge) | L | One player per side steers the team AI |
| [general-mode.md](general-mode.md) | in progress (v2 played: camera, drawing, labels work; Ctrl+C key) | L | General presses Ctrl+C for the order camera lifted about 3× and, holding Ctrl, draws labelled path orders his side sees on Alt |
| [general-orders.md](general-orders.md) | in progress (v1 built, not played) | L | The general orders AI formations with the stock bar (one at a time, delegate hands back); orders to player formations become suggestions |
| [choosing-troops.md](choosing-troops.md) | in progress (1–3 played; role split deferred) | M | Class pick sets the formation's size (budget by stock troop count); army composition later |
| [troop-types.md](troop-types.md) | in progress (v2 played and working with two players; balance pass next) | L | Per-culture formation split (horse archers), class → weight → weapon setup selection screen |
| [end-of-round-stats.md](end-of-round-stats.md) | idea | M | Kills, losses and commander per formation at round end |
| [spectator-camera.md](spectator-camera.md) | idea | S–M | Check and polish the stock spectator free camera for observers |
| [admin-commands.md](admin-commands.md) | in progress (first pass played and working; army size and toggles not built) | M | Restart round, swap sides, army size, force tactic |
| [general-fallen.md](general-fallen.md) | idea | S | Centre-screen announcement and sound when a general dies; a darker message for his own side |
| [cheer-with-your-men.md](cheer-with-your-men.md) | in progress (v1 built, not played) | S | A commander's cheer rolls through their own formation's soldiers |
| [named-formations.md](named-formations.md) | idea | S–M | Commanders name their formation; the name shows on labels, the formation list, the scoreboard and suggestions |
| [commander-banners.md](commander-banners.md) | in progress (built, not played) | M | A player's formation carries their banner on its shields (a picked sigil in a shade of the side's colour, saved on the client), the general's riders his |
| [round-weather.md](round-weather.md) | idea | M | Time of day (never night) and weather per round, from the layout seed |

Size: S is under a day, M a few sessions, L a week or more with design rounds.

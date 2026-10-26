# Admin Panel

Server admins run the match from the **Admin Panel** in the escape menu: they pick the next map and cultures, change the match settings, restart a round, swap the sides and remove commanders.

## Joining as an admin

1. In the custom server list, **right-click** the server and choose **Join as Admin**.
2. Enter the server's admin password.

A normal join never asks for it. The server host sets the password (see [Getting Started](../getting-started.md#the-admin-password)).

Once in, open the escape menu and choose **Admin Panel**.

## The panel

The panel shows Commander Battle's own groups in place of the stock ones. The stock groups had options this mode doesn't use, such as warmup, class toggles and formation markers, and their map list left out the campaign maps.

### Next Map

The map, from the vote pool and the usable maps, by their readable names, and both sides' cultures.

- **Change Map Now** ends the match and opens that map with those cultures, with no vote. It needs automated battle switching, which the config written by `Install-CampaignScenes.ps1` turns on.
- **Apply Changes** keeps the current match and uses the map after it. A map picked this way turns the map vote off for later matches. Applying again without changing the map turns the vote back on.

### Match

- **Number of rounds** and **round time limit.** They apply to the current match too.
- **Map time limit.** It applies from the next map.

### Commander Battle

- **Restart Round** ends the round being fought with no winner, and the same round is played again, so a restart doesn't use up one of the match's rounds. It is greyed out outside a fight.
- **Swap Sides** moves every player to the other side.
    - During a fight, the round restarts and the sides swap as the next preparation starts. During preparation, they swap at once.
    - Each player keeps their formation number if the other side's formation of that number is of the same kind, and otherwise takes a free formation of their kind. The general stays the general.
    - Round scores stay with the sides (attackers and defenders), not with the players.
- **Remove Commander** lists the players who command a formation. The one you pick goes to the spectators and their formation goes to the AI. They stay on the server and can join a side again. Removing the general also hands every formation he took over back to the AI.

### Players

The stock **kick**, **ban**, **mute** and **unmute**.

## Who can use it

Only a player who joined as admin can run these commands; the server refuses them from anyone else. Every player is told what an admin did, and the server log records each command and who sent it.

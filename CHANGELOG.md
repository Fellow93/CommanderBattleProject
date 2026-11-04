# Changelog

## v0.2.1 (changes since v0.2.0)

### Fixes
- **Server crash on "charge that formation":** with RBM Multiplayer, ordering a formation that was already charging to charge a different enemy formation farther away crashed the server with no log or message. The formation now turns and charges the formation you picked.

## v0.2.0 (changes since v0.1.0)

### The general
- **A general for each side:** on top of its 8 formations, each side now has a general: a mounted hero in his culture's finest gear, leading a bodyguard of 6 elite riders. Every culture has its own general and bodyguard.
- Nobody becomes the general by accident. Take him from the last row of the formation list (N) or from the General card on the troop screen. If all 8 formations are taken, you can still take the general.
- A player general rides at the tip of his bodyguard's wedge. The riders keep the wedge pointed where he rides and hold its heading when he stops.
- A fallen player general can take over one of his riders (F), who becomes the new tip of the wedge.
- When no player is the general, an AI general leads the bodyguard and keeps behind the main formation. When a player general leaves, the AI leads his riders.
- With RBM Multiplayer, the AI never pulls the general or his riders into its cavalry.
- The scoreboard names the general's formation by its troops.
- Swap Sides (admin panel) keeps the general as the general on the other side.

### General mode
- **General mode (Ctrl+C):** a player general lifts his camera about three times as high as the order camera to see the whole field. A hint at the top of the screen shows when it is available. Ctrl+C or Esc leaves it.
- **Drawn orders:** hold Left Ctrl for a cursor, then drag a path along the ground and give it a label ("Flank left"). Click a label to delete its path. A side keeps up to 10 paths; they last until the round ends.
- His whole side sees the paths and labels while holding Left Alt. The other side and spectators never do.
- His soldier stands still while he is in general mode. Being hit, or his horse being hit, closes it.
- The order bar works in general mode, and its flag reaches the ground at any height.

### The general's orders
- **The general commands the AI's formations** with the normal order bar (F1-F9, the number keys), one formation at a time. His first order takes an AI formation over: it keeps his order and the AI leaves it alone. The Alt panel and the formation list show it as "led by the general".
- **Delegate:** a toggle in the general's order bar that hands a formation back to the AI at once.
- **Suggestions to players:** an order to a player's formation becomes a suggestion, not an order. That commander gets a pop-up with a sound and a message worded as the general's order to him ("Fellow, move here"), and a blue marker on Left Alt with the same words. Only he and the general see it.
- When the general falls, leaves, changes team or formation, or the round ends, every formation he took over goes back to the AI.
- The general's orders count as activity, so he isn't kicked for being idle.

### Horn signals
- **Side-wide calls:** the general's Attack, Defend and Regroup barks also sound Advance, Hold and Fall back on a horn. His side and spectators get a pop-up ("The general sounds: Advance!"). The enemy hears the horn from where he stands, with no text. One call per side every 8 s.
- A suggestion from the general plays a horn for its commander: the attack horn for charge and advance, the retreat horn for fall back and retreat, the move horn for the rest.

### Order bar and markers
- **Named cards:** each card in the order bar shows its troops and, for a player's formation, its commander, e.g. "Cataphract (Fellow)".
- The selected card glows gold, and a gold frame rings the selected formation's map icon while the bar is open.
- **Ally markers:** holding Left Alt shows a green name marker over each allied player within 100 m.
- **Formation labels:** every formation a player leads, on either side, carries a label with its troops and its commander next to its icon, on Left Alt and while the order bar is open. A player general's formation gets an icon and label too.
- Spectators on neither side see both sides' labels and the names of nearby players of both sides.
- The Alt panel's destination markers now also show where the general's formations are heading.
- The order camera now stops short of the ground when you look up or have a hill behind you, instead of going under it.

### Commanders
- **Cheer with your men:** when you cheer, the soldiers of your formation cheer with you, in a wave through the ranks, some of them shouting. Soldiers near the enemy or busy fighting, aiming or reloading don't join in. The general's cheer reaches his riders.
- **Battle cries:** on a charge order half your soldiers shout, on an advance one in four; some call the order out ("Charge!"). They repeat your other orders with the same word. A soldier who breaks and runs often cries out.
- **Your banner on your men's shields:** the "Banner" button on your row of the formation list (N) picks a sigil and a shade of your side's colour, or your profile banner. Your formation's shields and the small banners over your soldiers carry it, and the general's riders carry the general's. It is saved on your computer per culture, so it follows you to any server. A pick after your formation has spawned applies from the next round.

### Maps
- **Time of day and weather for each round:** mostly around noon, sometimes morning or dusk, never night; clear most of the time, sometimes overcast, foggy or raining. Every player sees the same, and a chat line names it when preparation starts. It changes only the look, not the rules.
- Admins can fix the time of day and the weather from the admin panel, for the rest of the map.

### Combat
- **Two combat modes, picked by the host:**
  - **Light** (default): the classes' multiplayer gear, the light damage curve, slower ranged fire and slower horses, as in v0.1.0.
  - **RBM:** every class wears an RBM loadout of singleplayer items, and RBM Combat decides the blows: RBM's damage, armour, knockdown, missiles, shields and horse charges, with RBM's weapon speeds, reloads and horse speeds. Face and under-shoulder hits are announced to both soldiers, and stuck javelins and arrows fall out after a while. The troop screen shows the RBM gear.
- In Light mode, bows and crossbows now reload at 67% of the stock speed (was 75%). Their draw stays at 75%.
- In Light mode, a kick or bash that lands always does at least 1 damage, so the blow counts and the victim reacts.

### RBM Multiplayer
- AI archers save their ammo: they shorten their range as their quiver empties, instead of shooting it all away at long range.

### Fixes
- A front-rank soldier you take over in melee (F) can walk away at once. Before, the server kept him where RBM's Frontline had held him.
- On sloped campaign maps, the commander shown during preparation now stands on the ground instead of floating or sinking into it.
- The map vote screen shows the game type's name instead of a missing text.

### Server
- The server config written by `Install-CampaignScenes.ps1` allows 18 players (was 16), room for a full side of commanders and the general.
- The combat mode is set in `Modules\CommanderBattle\ModuleData\combat_mode.txt` on the server: a line `RBM` or `Light` (no file: Light). Restart the server after changing it.
- The server no longer shuts itself down after being empty for 10 minutes.
- `tools\Package-Release.ps1` zips both modules for a release.

## v0.1.0 (first release)

The first release of Commander Battle and RBM Multiplayer.

### Game mode
- **Commander battles for Bannerlord:** attackers against defenders, each an army of 8 AI formations of infantry, ranged, cavalry and horse archers. Players take command of a formation; the team AI leads every formation nobody commands.
- The last side with soldiers standing wins the round. If time runs out with both armies still in the field, the defenders win.
- No flags, morale or gold.
- A fallen commander takes over one of their own soldiers with F, after a 3 second wait.
- Fleeing soldiers leave the battle near the map edge or after fleeing for 2 minutes, so a round always ends.
- Giving orders counts as activity, so a commander leading from the order bar isn't kicked for being idle.

### Armies and troops
- **Every culture fields its own army:** how many infantry, ranged, cavalry and horse archer formations a side has depends on its culture.
- **Commander Battle's own troops** for all six cultures, each class in a light, medium or heavy weight with several weapon setups.
- **Troop selection screen:** pick your formation's class, then its weight, then its weapon setup. A heavier class fields fewer men, a lighter one more; the weapon setup doesn't change the count.
- A pick made during the fight applies from the next round.
- Spear, pike and lance troops carry a sidearm. Pikemen fight without shields. Javelins throw by default, on foot and on horseback.
- AI-led formations spread over the weights and setups.

### Commanding
- **Formation list (N):** every formation of your side with its troops, weapon setup, count and leader, a pick waiting for the next round, and the side's total. Take a free formation from it.
- **Order camera:** while the order bar is open, the camera rises and pulls back so you can see your formation and the ground ahead. The mouse wheel sets the height.
- **Scoreboard:** a Formation column shows the formation each player leads.

### The Alt overview
- **AI status panel:** hold Left Alt to see your side's AI tactic and, for every formation, its troops, behaviour, order and distance still to go.
- Markers show where each AI-led formation is heading.
- The AI's main formation is marked, and a player who leads it is told.

### Maps
- **The campaign's field-battle maps,** with readable names in the loading screen, the map vote and the scoreboard. Players vote for the next map after each match.
- The armies start where a singleplayer field battle would place them, and each round in a slightly different spot.
- Clients link the maps from their own game files the first time they start; nothing is downloaded.

### Combat
- Horses run at 85% of the stock multiplayer top speed.
- Bows and crossbows draw and reload at 75% of the stock speed.

### RBM Multiplayer
- **RBM's battle AI** leads the AI formations: RBM's tactics, formation behaviours and reactions, chosen by culture and terrain.
- **Frontline** for infantry and ranged formations of more than 25 men in a charge, a little more aggressive than in singleplayer.
- AI archers on foot draw a sidearm when an enemy gets within 4 m.
- **RBM Stance:** posture and stamina, with RBM's bars, posture-break messages, exhaustion effect and stagger animations for every player. AI soldiers kick and bash.
- **A light damage curve** in place of RBM Combat: every blow is halved, and hard blows lose more.
- RBM's default settings for everyone. Your singleplayer RBM config is never read, and the original RBM mod isn't needed.

### Admin panel
- **Admin panel** in the escape menu for players who join as admin: next map and cultures, change map now, rounds and time limits, kick, ban and mute, plus Restart Round, Swap Sides and Remove Commander.

### Server hosting
- `tools\Install-CampaignScenes.ps1` links the campaign maps into the server and writes a ready server config with map voting and an optional admin password.
- `tools\Start-Server.ps1` (or `Start-Server.cmd`) starts the dedicated server and reports when it is hosting.

# The General

Each side has a general on top of its 8 formations: a mounted hero in his culture's finest gear, leading a bodyguard of **6 elite riders**. A player can take the role, or the AI leads him.

As the general you lead the battle rather than a formation. You command the AI's formations with the order bar, suggest moves to the players' formations, and can rise high above the field in **general mode** to see the whole battle and draw orders on the map.

## Becoming the general

- **Only by choice.** The general is the last row of the formation list (N), and the troop screen has a **General** card. Joining a side never makes you the general.
- The general has a single class, so the troop screen goes straight to his weapon setup.
- If all 8 formations are taken and the general is free, a message tells you to take him from the list.
- On the scoreboard, the general's formation is named after his troops.

| Culture | General | Bodyguard |
|---|---|---|
| Vlandia | Vlandian Lord | Household Knights |
| Empire | Strategos | Elite Cataphracts |
| Khuzait | Noyan | Torguud |
| Aserai | Emir | Ghulam |
| Sturgia | Voivode | Druzhina Guard |
| Battania | Clan Chieftain | Sworn Riders |

## The bodyguard

- **You ride at the tip of a wedge.** Your 6 riders form a V behind you, three to a side, and keep it pointed where you ride. When you stop, the wedge keeps its last heading.
- **The bodyguard takes no orders.** It follows you.
- **If you fall,** take over one of your riders (F). He becomes the new tip, and the others follow him.
- **If you leave,** the AI takes over your riders and leads them as the AI general.
- With RBM Multiplayer, the AI never pulls the general or his riders into its cavalry.

### The AI general

When no player is the general, the AI leads him and his riders, keeping them behind the main formation.

Player or AI, the general and his riders start each round behind the centre of their side's line.

## The general's orders

The general commands his side's formations with the **stock order bar**, like any commander: **F1-F9** for the orders, the **number keys** to pick a formation, the order flag to point. It works in the normal view and in general mode.

- **One formation at a time.** Picking a formation, also with Ctrl and a number, drops the one before.
- The bar names each card after its troops and its commander, and the selected one glows gold.

### AI formations obey

- **Every round starts with each AI formation delegated** to the team AI.
- **Your first order takes a formation over.** The team AI leaves it alone and it keeps your order. The [Alt panel](overview.md) and the formation list show it as **led by the general**.
- **Delegate** hands it back to the AI at once. This toggle shows in the general's order bar only. On a formation the AI still leads, Delegate takes it over without an order.
- **With RBM Multiplayer**, a formation you lead is never regrouped by the AI. Once you delegate it, it may be.

When the general dies, leaves, changes team or formation, or is removed by an admin, every formation he took over goes back to the AI at once. So does every formation at the end of a round. A general who takes over one of his riders starts again with everything delegated.

### Players get suggestions

You can't order a player's formation. An order to it becomes a **suggestion** instead:

- **The commander** gets a pop-up in the centre of the screen with a sound, and a line in the message log, worded as your order to him: "Fellow, move here". Holding Left Alt shows a blue marker with the same words where you pointed.
- **You** see the same pop-up and marker, without the sound.
- Nobody else hears of it, and the formation itself isn't ordered. The commander decides.

**What can be suggested:** move (to the flag, or along a dragged line, with its facing and width), charge (with or without a target), hold, follow (the general), advance, fall back and retreat. Other orders, such as facing, formation shape or firing, aren't suggested; the bar tells you so once each time you open it.

A suggestion lasts until you give that formation another one, or the round ends.

**Suggestion markers.** While holding Left Alt, the commander sees a blue marker for the latest suggestion to his formation, and you see one for each suggestion you gave. In general mode you always see yours. The marker sits at the move's point, on the formation to charge or advance on, or above the formation for the other kinds.

## General mode

General mode lifts the general's camera high above the field.

- **Ctrl+C enters and leaves it.** It is for a player general only, while his soldier is alive and the round is being fought. Whenever you could enter, a hint at the top of the screen says **Ctrl+C: General mode**.
- **The camera** is the order camera lifted about three times as far: by default about 45 m up and 105 m back from you, along the way you look. The mouse turns it; look down to see the field. It stays on you, so there is no panning.
- **The mouse wheel zooms** in and out. Your zoom is kept for the session.
- The camera stops short of the ground when you look up or have a hill behind you.
- General mode switches to third person, and back to first person when you leave, if that is where you were.
- **Your soldier stands still** in general mode, and your bodyguard stays with you. Cheering, switching weapons and dropping them are off.
- **The order bar works** in general mode. Its flag follows the centre of the screen at any height. Esc closes the bar before it leaves general mode.

### Leaving

- **Ctrl+C or Esc** leaves general mode. Esc closes the formation list or the troop screen first, if one is open.
- **Being hit**, or your horse being hit, closes general mode with the message "You were hit: general mode closed."
- It also closes when you die, stop being the general, or the round ends.

### The cursor

**Hold Left Ctrl** for a cursor. While you hold it, the view stops turning (except at the edge of the screen), and the menu buttons, drawing and the path labels take the mouse. Let go and the mouse turns the view again.

### Drawn orders

The menu at the top of the screen has two tools:

- **Path.** Hold Ctrl, hold the left mouse button and drag along the ground, then type a label of 1 to 40 characters, such as "Flank left". **Cancel** throws the path away. Very short drags, under 5 m, are ignored. If you let go of Ctrl while dragging, the path ends there and the label box comes up.
- **Delete.** Hold Ctrl and click a path's label to remove the path.

**Who sees them:** only your own side. In general mode they always show. Everyone else on your side sees them while holding Left Alt, next to the AI status panel. Each path is a yellow line on the ground with its label at the middle.

**How long they last:** until the round ends or you delete them. A side keeps up to 10 paths; drawing an 11th removes the oldest. A player who joins your side mid-round sees the paths already drawn.

Paths are drawn for your players to read. They don't move any formation by themselves: give the order with the order bar, or let your commanders follow the plan.

## Known limitations

- **The bodyguard can't be ordered.** The order bar only knows the 8 regular formations, so your riders simply follow you.
- **Dragged move lines can differ slightly.** For a move along a dragged line, the general's own game works out the line and sends it to the server. The width the formation takes can differ a little from what it would take with a direct order.
- **No general on a listen server.** The general's order bar only works on a dedicated server.

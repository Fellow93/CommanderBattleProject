# RBM Multiplayer

**RBM Multiplayer** brings the Realistic Battle Mod (RBM) to Commander Battle: RBM's battle AI, which leads every formation nobody commands, and RBM's posture and stamina system. A light damage curve takes the place of RBM's combat model.

It is a separate module. A server can run Commander Battle with or without it, and players enable it when the server runs it. You don't need the original RBM mod: RBM Multiplayer is built from RBM's own source and is self-contained.

!!! note "Dedicated servers only"
    RBM's AI and the posture system run on a dedicated server only, never on a listen server.

## Battle AI

RBM's field-battle AI leads every formation nobody commands, in place of Commander Battle's own AI:

- **RBM's tactics,** such as split infantry and archers, the embolon, holding a choke, a reserve plug and the hedgehog, chosen by culture and terrain as in singleplayer;
- **RBM's formation behaviours** and AI reactions;
- **a read of the terrain,** made each round once both armies stand on the field;
- **RBM's formation movement and spacing**, and its AI combat values.

### The AI regroups its own soldiers

As in singleplayer, RBM's tactics merge AI formations of the same kind and split flanking parties off them. Soldiers only move between formations the AI leads.

- **A formation a player commands is never touched.** No soldier moves into or out of it, and its commander keeps exactly their own men.
- **Neither is a formation the general took over,** until he hands it back, nor the general's own formation and bodyguard.
- **A formation whose commander leaves** goes back to the AI and may be regrouped.
- **Every round starts again** from Commander Battle's formations.

### Frontline

RBM's Frontline runs for AI-led and player-led infantry and ranged formations of **more than 25 men** in a charge. Instead of all piling in, men step back, find an ally, go round a flank or close up.

In Commander Battle the front rank is **more aggressive** than in singleplayer: men in contact press in rather than step back.

### Archers defend themselves

RBM keeps archers on their bows and in their places. In Commander Battle, an AI archer on foot with an enemy **within 4 m** fights him with his sidearm instead, leaving his place in the line if he must. He goes back to RBM's behaviour 3 seconds after the last enemy is gone.

## Posture and stamina

RBM's posture and stamina system runs for every soldier, players included:

- **Posture** is how well a soldier keeps his guard. Blocks, parries and hits wear it down; when it runs out he is staggered, and a blow can crush through his block.
- **Stamina** drains as he fights, and a tired soldier loses posture faster. Its effect on speed reaches AI soldiers only (see [Known limitations](#known-limitations)).
- **AI soldiers kick and bash.**

What you see:

- **RBM's posture and stamina bars,** your own and, after an exchange, your target's;
- **posture-break messages** for crush-throughs and chamber blocks;
- **the exhaustion effect** in first person, with RBM's exertion grunt;
- **the stagger, tired and knock-down animations,** on every player's screen.

The server works it all out and sends each player what they need to see.

RBM's own manual explains the system in full: [Posture & Stamina](https://fellow93.github.io/RealisticBattleProject/features/posture/).

## The damage curve

RBM's combat model (RBM Combat) isn't included yet. It expects RBM's own items, and multiplayer uses its own. On multiplayer items, RBM's formulas would leave arrows, bolts and horse charges doing almost no damage.

In its place, a light damage curve makes battles last longer: every blow's damage is **halved**, and then the harder the hit, the more it loses.

| Damage | 25 | 50 | 75 | 100 | 150 | 200 |
|---|---|---|---|---|---|---|
| Becomes | 12 | 23 | 33 | 43 | 60 | 75 |

## Settings

RBM Multiplayer always uses **RBM's default settings**, the same on the server and every client. Your singleplayer RBM config is never read or changed. The only differences from RBM's defaults:

- **RBM Combat is off**, replaced by the damage curve above.
- **Frontline's attack and back-step weights** make the front rank more aggressive, as described above.

## What differs from singleplayer RBM

| | Singleplayer RBM | RBM Multiplayer |
|---|---|---|
| Battle AI | Yes | Yes, for every formation nobody commands |
| Posture and stamina | Yes | Yes, worked out by the server |
| RBM Combat (damage and armour) | Yes | No: a light damage curve instead |
| Settings | RBM's settings screen | RBM's defaults, fixed |
| Frontline front rank | RBM's default weights | More aggressive |
| AI archers in close combat | RBM's behaviour | Draw a sidearm within 4 m |
| Your own soldier's stats | Changed by RBM, stamina included | Stock values |

## Known limitations

- **Stat changes reach AI soldiers only.** Speeds and other soldier stats are worked out separately on every machine, never sent over the network. RBM's stat changes, including stamina's effect on speed, therefore only apply to soldiers the AI controls. Your own soldier keeps the stock values, including a soldier you take over, so he moves at normal speed.
- **AI swings can look a little off.** Your game shows the AI soldiers with stock stats, while the server uses RBM's. Movement and outcomes come from the server, but an AI soldier's swing may look slightly faster or slower than it really is.
- **A short pause at the round start.** RBM reads the terrain at the start of each round, which can stall the server briefly.

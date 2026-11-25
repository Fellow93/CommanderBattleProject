# Empire shields on RBM holsters stalled the round start

Written 2026-11-25 16:16.

## What was done

- **Cause.** After the troop overhaul (`4e4ddb5`) the round never got going once the preparation countdown ended. Six shields in the Empire's RBM loadouts sit on RBM's own holsters, which multiplayer doesn't load. For each soldier spawned with one, the server logged `Could not find holster index for id "left_forearm_shield"` and its watchdog wrote a crash dump, about 3 seconds each (40 dump folders in `C:\ProgramData\Mount and Blade II Bannerlord\crashes`).
- **Fix.** `Module/ModuleData/cb_rbm_loadouts_empire.xml`: the six ids went back to the plain ones, 14 lines.

  | Was | Now |
  |---|---|
  | `battania_targe_b` | `battania_shield_targe_a` |
  | `battania_shield_targe_a_cataphract_shoulder` | `battania_shield_targe_a` |
  | `noyans_shield_shoulder` | `noyans_shield` |
  | `strapped_round_shield_shoulder` | `strapped_round_shield` |
  | `decorated_steppe_shield_kalkan_shoulder` | `decorated_steppe_shield` |
  | `tribal_steppe_shield_kalkan_shoulder` | `tribal_steppe_shield` |

- **Installed.** Built and installed to the client and the server; the server restarted as pid 40844 (126 patch classes applied, 0 failed). The old server's watchdog had to be ended by hand: it kept the DLLs locked after `Start-Server.ps1 -Stop`.

- **Three setups where two were meant.** A unit's first setup is always its own base loadout (the first `Perk`, the `Hero` and `Troop` gear); the `Setup` entries come on top. The Mourtatoi (`mp_skirmisher_empire`) and the Sagittarius (`mp_heavy_ranged_empire`) had a base that was a copy of their `Setup index="1"`, under the old perk's name. In `cb_mpclassdivisions_empire.xml` their first perk is gone; in `cb_rbm_loadouts_empire.xml` the duplicate `Setup index="1"` is gone and the old setup 2 is now 1. The Mourtatoi commander's own gear (`Hero`) went from the leftover spear, kite shield and gladius to the bow, targe, mace and arrows. Installed; the server restarted as pid 35968 (80 setups, was 82).
- **The same in four more units,** fixed the same way: the courser (`mp_light_cavalry_empire`; its base was "Throwing lances", which is now the first perk, before "Heavy pila"), the cataphract (`mp_heavy_cavalry_empire`, an empty `Setup`), the bucellarius (`cb_light_horse_archer_empire`) and the skoutatoi (`cb_skoutatoi__medium_infantry__empire`). The courser's, bucellarius's and skoutatoi's `Hero` weapons now match their troops'. Server restarted as pid 39272 (76 setups).
- **Unit editor.** The cause: the page showed a name only for the added setups, never for the unit's own loadout, which the game lists first. `tools/UnitEditor/index.html`: the Hero and roster tabs now show setup 0's name, description and icon, with a line saying it is the first setup in the game and how many the unit has; `onField` writes them to perk 0. The fields render; an edit through them was not tried. The 45 core tests pass.

Not played: no round was started on the new server. Left as they are, for the user to decide: units whose base differs only a little from an added setup (levy archer, equite, akritae, kontaratoi) and base setups still under a copied name (legionary, palatine guard, akontistae: "Spear and shield"). The unit editor still offers these items without a warning. The admin panel report was not looked into (the only admin action in the log, Restart Round, was accepted by the server). Not committed.

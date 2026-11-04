# Map voting screen: missing game type string

The map voting screen after a match (`MPIntermissionVM`) showed a missing-text error for the next game type: it looks up `str_multiplayer_game_type.<GameType option>`, i.e. `.CommanderBattle`, which only Native's stock modes had.

- `Module/ModuleData/commander_battle_strings.xml`: added `str_multiplayer_game_type.CommanderBattle` ("Commander Battle").
- Swept every variation-keyed `GameTexts` lookup in the decompiled `TaleWorlds.MountAndBlade.Multiplayer`, `.View`, `.GauntletUI` and `.ViewModelCollection` DLLs. Nothing else reachable is missing: the scoreboard and escape menu pass `Captain` (Native has it); scene names, the formation scoreboard header and the official game type name/explainer were already ours; `str_multiplayer_official_game_type_description/objective_info/troops_info` are used only by official matchmaking.
- Build: the XML deployed to the client module; DLL copies were locked by the running launcher and server (no code changed). Not checked in game, not committed.

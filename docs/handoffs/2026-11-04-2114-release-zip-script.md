# Handoff: release zip script for players

## Release packaging
- `tools/Package-Release.ps1` (new): zips the installed `<game>\Modules\CommanderBattle` and `RBM.Multiplayer` into `dist\CommanderBattle-<version>.zip`, with the version from `Module\SubModule.xml` (now `v0.2.0`). `-Build` builds first, and `-GameDir` overrides the game folder.
  - It leaves out `SceneObj` (junctions to SandBoxCore scenes; robocopy `/XJ /XD SceneObj`). Clients relink the scenes themselves.
  - It ships the repo's empty `Module\ModuleData\server_address_overrides.txt` instead of the installed copy, which holds the host's public-IP rewrites.
  - It writes each zip entry with `/` separators, because .NET Framework's `CreateFromDirectory` writes `\`.
  - It checks the zip: both `SubModule.xml` files and client DLLs present, no `SceneObj`, no backslash entries.
  - It replaces the earlier hand-made zip in `artifacts\` (2026-10-25-2245 handoff).
- `.gitignore`: `/dist/`.
- `README.md`: new "Release for players" section, after "Run a test server".
- Run on the installed build: 4.5 MB, 87 files, extracts to the two module folders.

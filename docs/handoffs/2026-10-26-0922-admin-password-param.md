# Handoff: admin password parameter for the server config

Session of 2026-10-26, written 09:22. It follows `2026-10-26-0920-admin-commands.md` in the same session, and answers that handoff's first open item.

## State

- **Not committed:** `tools/Install-CampaignScenes.ps1`, `README.md`, this handoff, and everything the 08:40, 09:02 and 09:20 handoffs list.
- **The real server config is unchanged.** It still has no admin password. The user hasn't run the script with one yet.
- **Running:** server pid 9060, as in the 09:20 handoff.

## What was done

- `Install-CampaignScenes.ps1` has a new `-AdminPassword` parameter. It writes `AdminPassword <password>` before `start_game`, because the server sends the password to the lobby when it registers.
- The script rewrites the config from scratch. Run without the parameter, it now carries over the `AdminPassword` line already in the config. `-AdminPassword ''` removes it. A password containing a line break is refused, since the config reads the rest of the line as the value.
- The README's "Admin panel" section says how to set it.

## Verified vs not

- **Verified:** ran against a scratch server folder. The password is set, kept on a rerun, removed with `''`, and a line break is refused.
- **Not verified:** a password containing spaces. The config reads the rest of the line as the value, the way `ServerName Commander Battle` is read, but nobody has tried it with a password. Joining as admin hasn't been tested either.

## Next

Run the script with `-AdminPassword <password>`, then `tools\Start-Server.ps1 -Restart`. Join with that password and go through the admin items in the README's test checklist.

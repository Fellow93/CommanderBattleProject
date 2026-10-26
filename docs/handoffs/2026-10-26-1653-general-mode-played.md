# Handoff: general mode played (drawing, labels, camera)

Session of 2026-10-26, written 16:53. It follows `2026-10-26-1651-general-mode-ctrl-c.md` (same session).

## State

- **Committed:** everything up to `74c4a38`.
- **Not committed:** the Ctrl+C key and the hint (16:51 handoff), and this handoff.
- **Running:** server pid 32432.

## Verified

The user played general mode and reports that **drawing, labelling and the camera work**. That covers:
- the order camera lifted about 3× (v2, 15:34);
- the Ctrl cursor for drawing;
- the label popup.

## Still open

- The parts of the README's general mode checklist the user didn't mention:
  - the Ctrl+C hint;
  - Delete;
  - the exit on a hit;
  - other players of the side seeing the paths on Left Alt, and the other side never seeing them;
  - late joiners;
  - clearing at round end;
  - F1–F9 for non-generals.
- **The safety exit on a hit:** the user's word on keeping it is still pending.
- **Future order types** (the user): a path with an arrowhead.
- Commit the Ctrl+C change when the user says so.

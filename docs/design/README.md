# Snapline visual concept — approved

Preview: [snapline-concept.png](snapline-concept.png).
Regenerate with `pwsh -NoProfile -File scripts/Design-Preview.ps1` from the repository root.
Static generated concept only; not a screenshot of the running application.

## Preserve

- Transparent shelf, fine rope, clothespins and hanging screenshots.
- Draggable expanded/collapsed components and saved positions.
- Discoverable entry, explicit user-controlled hiding, copy/preview/file-sharing behavior.

## Redesign as one family

- Logo: one slightly tilted screenshot held by an apricot clothespin on a short rope.
  No heavy diamond, mountains/sun or unrelated abstract character glyphs.
- Collapsed entry: mini pinned screenshot plus a small paper count tab; explain click/drag on hover.
- Capture: mint paper button, consistent thin capture icon and clear label.
- Collapse: secondary warm-white paper button with a simple chevron.
- Remove: small subtle rounded control, stronger on hover; keep accessible name and predictable hit target.
- Motion: approach, clip, brief damped swing; small hover/press feedback; idle is quiet.
  Respect reduced motion. Interaction must not depend on motion completion.

The colored rounded desktop rectangles in the preview are synthetic backgrounds used
only to show contrast. The application must not add an opaque full-width backdrop.

## Approval and validation

The user approved this concept. Implement the hanging-photo logo, miniature photo
entry and shared paper controls while preserving rope/pegs. The earlier direction
removing rope/pegs was a misunderstanding and was not shipped.
The preview itself is not test evidence. Do not force-close or overwrite a running
application; use a separate output directory for the new version.

Shared control styles, small-size icon variants and finite interruptible motion
follow `.pi/skills/snapline-test/SKILL.md`. Validate rendering,
transparent hit testing, move/click separation, interrupted animations, reduced motion,
post-capture visibility and final EXE behavior. Do not claim visual mockups prove E2E.

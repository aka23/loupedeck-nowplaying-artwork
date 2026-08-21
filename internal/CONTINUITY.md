# Continuity protocol

The structure in `internal/` is only half of it; these habits are what keep it true. A
stale wiki is worse than no wiki, because it is trusted by default.

## Session start

Read `internal/wiki/`, then `internal/commitments.md`. A SessionStart hook echoes the OPEN
item titles so the read-order survives context pressure, but the hook is a reminder, not a
substitute for reading them.

## While working

- Update `commitments.md` **as things happen**, not at session end. Anything load-bearing
  that lives only in the conversation is lost at the session boundary.
- Fill `Docs-swept` on every item you touch. It forces the question "did I update the docs
  to match?" while the answer is still cheap.
- Allocate ids from `Next ID` at the top of `commitments.md` and bump it.
- When understanding changes, edit the wiki **in place**. Do not append a correction below
  the wrong statement.

## When a decision changes — THE SWEEP

Appending to `commitments.md` gets done; editing wiki prose is what gets forgotten. That
asymmetry is how this layer rots. So, mechanically:

1. `commitments.md` — open, close or supersede the affected items; fill `Docs-swept`.
2. `README.md` — update every section the change invalidates.
3. `internal/wiki/` — grep for the OLD state's keywords and fix every hit. After the
   rename, for example: `grep -ri "SpotifyArtwork\|Spotify Artwork" internal/ CLAUDE.md *.md`.
   Zero live hits outside CLOSED history is the exit criterion.
4. `CLAUDE.md` — confirm the hard rules still hold.
5. Commit the sweep with, or immediately after, the change it documents.

## Session end

Before stopping, confirm every load-bearing fact from this session is in a file. If the
next session would have to ask the owner a question this session already answered, it is
not written down yet.

## Discipline

- Recall is a hypothesis. Verify against the current tree before acting on it.
- Author and review in separate passes.
- The layer lives in git — commit `internal/` changes alongside the work they describe.
- This repo is public. Keep `internal/` technical; nothing personal, nothing secret.

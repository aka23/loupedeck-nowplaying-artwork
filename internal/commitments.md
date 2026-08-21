# Open commitments / current state

Next ID: NPA-04

Conventions: items are `NPA-nn`, zero-padded, allocated from `Next ID` above — bump it
when you add one. `Docs-swept` is the drift guard: fill it at write time, naming which
docs you updated to match. When CLOSED passes ~10 entries, move the oldest into
`internal/archive/commitments-<year>.md`.

## Session-start read order
1. `internal/wiki/` — what this project is and how it works (read first).
2. This file — current open items, decisions, in-flight state, shipped versions.
3. `README.md` — the user-facing build/install/gotcha reference; the wiki points at it
   rather than restating it.

## OPEN

NPA-01: Logi Marketplace submission for v1.0 — ⏳ awaiting review
- Source:       Owner asked to publish to the Marketplace, 2026-08-21.
- Decision/why: Submitted as **Now Playing Artwork**, not "Spotify Artwork" — putting
                another company's trademark in the product title, on a marketplace that
                also carries that company's own plugin, is the kind of thing review
                exists to catch. Spotify is named in the description instead.
- State:        Submitted 2026-08-21 via https://marketplace.logi.com/contribute with
                `bin/NowPlayingArtwork_1_0.lplug4`. Form accepted it ("File validation
                successful"); confirmation shown was "Your submission will be published
                after it's been reviewed."
- Verification: Cannot be verified from this repo. Review is automated + manual and
                answers within 10 working days; chase `marketplacecontribute@logitech.com`.
                UNKNOWN until Logitech replies.
- Docs-swept:   n/a — nothing in the repo asserts a listing exists yet. Add a Marketplace
                link to README once it is live.
- Owner:        aka23 (watch for the review reply).

NPA-03: Working directory still named after the old plugin — 🧹 cosmetic
- Source:       Fallout from the rename (NPA-01's naming decision).
- Decision/why: Left alone deliberately — renaming the directory mid-session would have
                invalidated every path in flight.
- State:        Repo is `loupedeck-nowplaying-artwork`; the local checkout is still
                `~/Projects/loupedeck/SpotifyArtworkPlugin`. Purely cosmetic; nothing
                reads the directory name.
- Verification: n/a.
- Docs-swept:   n/a.
- Owner:        aka23, whenever convenient.

## CLOSED

NPA-00: v1.0 built, published as open source, and working on the device — ✅ done
- Outcome:      One action showing the current Spotify cover on a Loupedeck Live key,
                Play/Pause on press. Public at
                https://github.com/aka23/loupedeck-nowplaying-artwork (MIT).
- Verification: Clean `dotnet build -c Release` (0 warnings), `logiplugintool verify` OK,
                and on-device: a track change is reflected on the key in ~1s, measured
                repeatedly from `Logs/plugin_logs/NowPlayingArtwork.log`.
- Docs-swept:   README.md carries build/install/usage plus the three SDK gotchas;
                `internal/wiki/` synthesizes the rest.

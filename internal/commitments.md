# Open commitments / current state

Next ID: NPA-05

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

NPA-01: Logi Marketplace submission — v1.1 pending publication — ⏳ awaiting review
- Source:       Owner asked to publish to the Marketplace, 2026-08-21.
- Decision/why: Submitted as **Now Playing Artwork**, not "Spotify Artwork" — putting
                another company's trademark in the product title, on a marketplace that
                also carries that company's own plugin, is the kind of thing review
                exists to catch. Spotify is named in the description instead.
- State:        Submitted 2026-08-21 via https://marketplace.logi.com/contribute with
                `bin/NowPlayingArtwork_1_0.lplug4` — **superseded by v1.1, see NPA-04;
                whatever is published must be the 1.1 package.** Form accepted it ("File validation
                successful"); confirmation shown was "Your submission will be published
                after it's been reviewed."
                2026-09-22: a Marketplace reviewer replied with hands-on test feedback — see
                NPA-04 — without stating an outcome. Checked the same day: the plugin is
                **not published**. It is absent from the ~90-entry listing at
                https://marketplace.logi.com/plugins/en/4, and
                https://marketplace.logi.com/plugin/NowPlayingArtwork/en returns 404,
                where that URL pattern is the manifest `name` (cf. /plugin/Spotify/en,
                /plugin/AppleMusic/en). So the feedback is a pre-publication gate, not a
                post-publication nicety: the reply to it is on the path to the listing.
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

NPA-04: Reviewer asked for an action display name and no group — ✅ answered
- Source:       Marketplace review feedback on NPA-01, 2026-09-22, chased 2026-09-25. Two
                requests: give the action a name ("Display Artwork"), and lift it out of
                the "Now Playing Artwork" group since it is the only action.
- Decision/why: **① declined, ② adopted.**
                ② is right and has shipped. With one action, a group repeating the
                plugin's own name was nesting that carried nothing, so `groupName: null`.
                A *descriptive* group name was considered instead and rejected: it would
                have kept the nesting the reviewer objected to and still left the row
                itself blank.
                ① cannot be done on app 6.4.1.364 without writing the name across the
                artwork, which is the whole product. The mechanism took two wrong guesses
                to pin down, and both wrong versions reached a draft reply to Logitech
                before a verification pass caught them. What is actually true:
                `Plugin.GetActionDisplayName` resolves an action's label, and its first
                branch is `if (!action.HasParameter || String.IsNullOrEmpty(
                actionParameter)) return action.DisplayName;`. This command registers no
                parameters, so that branch always wins and the
                `GetCommandDisplayName` hook is never consulted for it. **This is the SDK
                working as designed, not an app defect** — the hook exists for
                parameterised actions whose label varies per state. Do not tell Logitech
                their app is at fault; they can read the same DLL.
- Verification: Measured on the device 2026-09-25, app 6.4.1.364 / service 6.4.1.3246,
                owner watching the key and the action list.
                ① A build carrying `displayName: "Display Artwork"` drew exactly that
                string over the album art.
                ② With `groupName: null` the action is listed directly under the plugin
                heading, still selectable and draggable, and the app shows the action's
                **description** in the pane beneath it — so a blank label is not the same
                thing as an unexplained action. That screenshot is the reply's strongest
                argument.
                The group is not part of the stored action id (`PluginAction` sets
                `Name = name ?? GetType().FullName`), and the live profile still holds
                `$NowPlayingArtwork___...NowPlayingArtworkCommand`, so the key assignment
                survived every rebuild. Two separate review passes were what caught the
                bad SDK claims — first an overstated one, then the "app defect" reading
                that replaced it. Neither survived contact with the decompiler. The
                corrected account is above and in `internal/wiki/decisions.md`.
                **NOT verified:** whether the contribute form's validator accepts an action
                with neither a group nor a display name. v1.0 passed with an empty display
                name but *with* a group; `logiplugintool verify` passes on 1.1. Upload
                before promising the package.
- State:        The working tree holds the shipping v1.1 change, **pending commit**. Packed
                and verified as `bin/NowPlayingArtwork_1_1.lplug4` and installed by
                extracting that package; it loads and renders at Width90 and Width60.
- Docs-swept:   `internal/wiki/decisions.md` (section rewritten in place, not appended),
                `README.md` (feature list, install step, plugin-author note, and the
                pack/verify/install commands), `internal/wiki/overview.md` (pack filename),
                `CLAUDE.md` (the hard rule now covers the group),
                `src/Actions/NowPlayingArtworkCommand.cs` (constructor comment),
                `LoupedeckPackage.yaml` (version 1.0 → 1.1).
- Evidence:     Two screenshots of the app's action list, captured 2026-09-26 and committed
                as `assets/screenshot-action-list.png` (what ships: blank row, clean
                artwork, description shown beneath) and
                `assets/screenshot-display-name-over-artwork.png` (the same panel with a
                display name: the row gains its label **and** the key preview loses the
                cover to it). The second shows the reviewer's request being granted and
                breaking the product in the same frame, which is why it carries the reply
                rather than prose. They are committed rather than attached to the mail so
                the reviewer gets full resolution and a stable URL. The probe build was
                reverted with `git checkout` and v1.1 reinstalled from the package.
- Replied:      2026-09-26, in the review thread. ② conceded up front, ① declined with the
                two screenshot URLs doing the arguing, the SDK mechanism stated the way
                the decompiler supports it, an explicit offer to reconsider if a visible
                label is a publication requirement, and a question about how to deliver
                v1.1 (contribute form or the `.lplug4` directly). **Open until he
                answers** — the delivery route and whether ① is a hard requirement are both
                still his to say.
- Owner:        aka23.

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

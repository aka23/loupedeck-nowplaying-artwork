# Now Playing Artwork — project instructions

On session start, read `internal/wiki/` first, then `internal/commitments.md`.
`README.md` is the user-facing reference; the wiki points at it rather than repeating it.

## Hard rules

- **No Spotify Web API, no OAuth, no client id, no external service.** Track state comes
  from Spotify.app through `/usr/bin/osascript` only. This is the product, not an
  implementation detail — it is why there is nothing to sign into.
- **AppleScript is passed via `ProcessStartInfo.ArgumentList`**, never assembled into a
  shell string.
- **`GetCommandImage` must never throw.** An exception there loses the frame, which on the
  device is indistinguishable from "the plugin stopped working".
- **Do not delete `src/NowPlayingArtworkApplication.cs`.** The service will not load the
  assembly without a `ClientApplication` subclass. See `internal/wiki/gotchas.md`.
- **Do not give the action a display name.** The app draws it over the artwork. See
  `internal/wiki/decisions.md`.
- **Scope is one action: artwork + Play/Pause.** No next/previous, volume, seek, track
  text, progress, or settings UI. Say no and explain, rather than adding "just one more".
- **Renaming the plugin or the command type drops the key assignment.** Warn before doing
  it, never after.

## Operating principles

- **Investigate before asking, and before asserting.** The SDK's behaviour here is mostly
  undocumented; nearly every finding in the wiki came from a measurement, and the ones that
  came from reasoning were wrong.
- **Do not guess API names or dimensions.** Check against the installed `PluginApi.dll` by
  reflection, or against the SDK's own helpers, and let the compiler and the log settle it.
- **Verify before trusting recall.** Memory and wiki notes are point-in-time hypotheses;
  confirm the file/flag/version still exists before acting on it.
- **A green log is not a working key.** The plugin can produce correct frames that the
  device never shows. Evidence from `plugin_logs` proves the plugin side only — device
  behaviour needs the human to look.
- **Separate authoring from review.** Do not approve your own change in the same pass.

## File layout

| Path | What |
|---|---|
| `src/` | Plugin source; `src/package/metadata/` is the package manifest and icons. |
| `assets/icon.svg` | Icon master; the four PNGs are rasterised from it. |
| `internal/` | Continuity layer: `commitments.md` (state), `wiki/` (knowledge), `CONTINUITY.md` (protocol). Tracked, and public — this repo is public, so keep it technical. |
| `bin/` | Build output and the packed `.lplug4`. Untracked. |

Repo language is English (code, docs, commits). Conversation with the owner is Japanese.

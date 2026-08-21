# Overview

A Logi Actions SDK plugin for Loupedeck / MX Creative Console devices. One action: it
shows the album artwork of the track playing in Spotify on a single key, and toggles
Play/Pause when the key is pressed. macOS only.

`README.md` is the user-facing source of truth for what it does, how to build it, and how
to install it. This page covers only how the pieces connect in the current code.

## Shape

| File | Role |
|---|---|
| `src/NowPlayingArtworkPlugin.cs` | `Plugin` subclass. Universal (`HasNoApplication`). Initialises `PluginLog` and `PluginResources`; no other logic. |
| `src/NowPlayingArtworkApplication.cs` | `ClientApplication` subclass that names no process and reports `Unknown`. Exists only because the service refuses to load an assembly without one — see `gotchas.md`. |
| `src/Actions/NowPlayingArtworkCommand.cs` | The whole implementation: polling, AppleScript, download, cache, rendering, Play/Pause. |
| `src/Helpers/PluginLog.cs`, `PluginResources.cs` | Unmodified SDK template helpers. Only `PluginLog` is used. |
| `src/package/metadata/LoupedeckPackage.yaml` | Package manifest — name, version, URLs, licence, supported devices. |
| `src/package/metadata/Icon*.png` | Marketplace/app icon at 16/32/48/256, rasterised from `assets/icon.svg`. |

Everything under `src/package/` is copied next to the build output by the `CopyPackage`
target in the csproj, so `bin/<Config>/` is directly packable by `logiplugintool pack`.

## How the action works

`OnLoad` starts one `PeriodicTimer` loop at 1 s and `OnUnload` cancels it, waits up to
2 s, and disposes the token source. Each tick:

1. Run `id of current track` through `/usr/bin/osascript`. A `null` return means the
   script itself failed — keep showing whatever is on the key. An empty string means
   Spotify is not running or has no track — clear the key.
2. If the id equals the last committed one, stop. This is what makes a four-minute track
   cost one download rather than 240, and what keeps the artwork still while paused
   (pausing does not change the track id).
3. On a new id, look for the cover in a 20-entry LRU cache (`MaxCachedTracks`). A hit
   repaints with no network access.
4. On a miss, fetch `artwork url of current track`, download over a shared static
   `HttpClient` (10 MB ceiling), and decode-check it before accepting.
5. **Commit the track id only after the artwork is in hand.** A failure therefore leaves
   the id uncommitted and the next tick retries, up to `MaxArtworkAttempts` (3), after
   which the track is accepted with no artwork so the key stops showing the previous
   track's cover.
6. `NotifyImageChanged` raises all three `ActionImageChanged` overloads; the service calls
   `GetCommandImage` about a millisecond later.

`GetCommandImage` never throws — a size it cannot build returns `null` rather than losing
the frame — and `RenderArtwork` centre-crops the cover to the key's aspect ratio and draws
it edge to edge.

`RunCommand` fires `playpause` through AppleScript, guarded by an `Interlocked` flag so a
fast double-press cannot overlap two scripts.

## AppleScript

All three scripts are `const String` literals passed to `/usr/bin/osascript` via
`ProcessStartInfo.ArgumentList` — never a shell string. Each run is capped at 5 s and the
process is killed on timeout. There is no Spotify Web API, no OAuth, no client id.

## Build and package

`dotnet build -c Release` → `bin/Release/{bin,metadata}` →
`logiplugintool pack ./bin/Release ./bin/NowPlayingArtwork_1_0.lplug4`.

The csproj resolves `PluginApi.dll` from the installed service first and the
`logiplugintool` store second, and a `CheckPluginApi` target fails the build with a
readable message if neither exists. See `decisions.md` for why that order matters.

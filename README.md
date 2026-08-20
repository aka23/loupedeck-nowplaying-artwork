# Now Playing Artwork for Loupedeck

Shows the album artwork of the track playing in Spotify on a single Loupedeck key,
and toggles Play/Pause when you press it.

One key, one job. It reads Spotify.app directly, so there is no account to connect,
nothing to authorise, and nothing to keep signed in.

No Spotify Web API, no OAuth, no client ID, no external service. The plugin talks to
Spotify.app directly through AppleScript, so it works offline apart from fetching the
cover image itself.

macOS only.

## What it does

- One action, in the **Now Playing Artwork** group. Universal plugin, so it can go on any
  profile. The action itself is deliberately unnamed — see the notes below.
- The artwork fills the key. No title, no track name, no icon drawn over it.
- Press the key to toggle Play/Pause.

## How it works

A single one-second loop asks Spotify for the current track id:

```applescript
tell application "Spotify" to id of current track
```

Only when the id changes does it ask for the artwork URL and download the image, so a
track that plays for four minutes causes one download, not 240. Downloaded covers are
kept in a 20-entry cache, which means skipping back to a recent track repaints from
memory with no network access at all. Pausing does not change the track id, so the
artwork stays put.

The cover is centre-cropped to the key's aspect ratio and drawn to fill it.

## Requirements

- macOS (Apple Silicon or Intel)
- A Loupedeck CT-family device — Loupedeck CT, Live, Live S, Razer Stream Controller / X
- Logi Plugin Service 6.x (ships with the Loupedeck app / Logi Options+)
- .NET SDK matching the service's runtime — see below
- Spotify.app

## Building

```sh
dotnet build -c Release
```

The build references `PluginApi.dll` from the installed Logi Plugin Service:

```
/Applications/Utilities/LogiPluginService.app/Contents/MonoBundle/PluginApi.dll
```

Override the location if yours differs:

```sh
dotnet build -c Release -p:PluginApiDir=/some/path/
```

**Target framework must match the service's PluginApi.** At the time of writing the
installed service ships PluginApi 6.4.1, which is built for .NET 10, so this project
targets `net10.0`. If your service ships an older PluginApi you will get `CS1705` and
need to retarget. The `logiplugintool` package on NuGet (6.1.4) carries an older .NET 8
PluginApi — building against *that* produces an assembly the installed service silently
refuses to load, so the project deliberately prefers the service's own copy.

## Packaging and installing

```sh
logiplugintool pack ./bin/Release ./bin/NowPlayingArtwork_1_0.lplug4
logiplugintool verify ./bin/NowPlayingArtwork_1_0.lplug4
logiplugintool install ./bin/NowPlayingArtwork_1_0.lplug4
```

If `install` fails with `Plugin installation cannot start`, the tool and the service are
out of step (this happens when `logiplugintool` is older than the installed service; the
same failure affects `uninstall`). Two things that do work:

- The Loupedeck app: **Marketplace → manage add-ons → + Install plugin from file**
- By hand: unpack the `.lplug4` into
  `~/Library/Application Support/Logi/LogiPluginService/Plugins/NowPlayingArtwork/`
  and restart the service with
  `launchctl kickstart -k gui/$UID/com.logi.pluginservice.launch`

Then drag the action out of the **Now Playing Artwork** group onto a key.

macOS will ask to let Logi Plugin Service control Spotify the first time the plugin runs
a script. Allow it, or tick it later under **System Settings → Privacy & Security →
Automation**.

## Notes for anyone writing a Loupedeck plugin

Three things cost real time to work out and are not obvious from the SDK templates:

- **A plugin assembly with no `ClientApplication` subclass will not load**, even for a
  universal plugin that has no application. The service reports only
  `Cannot load plugin from <dll>`. `NowPlayingArtworkApplication.cs` exists solely to
  satisfy this.
- **The Loupedeck app draws its own text element over the key, and its content is the
  action's display name** — returning an empty string from `GetCommandDisplayName` does
  not remove it. Removing it in the app's key editor is worse: editing a key switches it
  to a statically composed image and the artwork stops updating. Giving the action an
  empty display name leaves the app nothing to draw, so the key keeps its default live
  rendering. That is why the constructor passes `String.Empty`; the group name carries
  the identity instead, so the action is still findable in the action list.
- **An action image is `BitmapBuilder(imageSize)` sized** — 80×80 for `Width90`.
  `GetButtonWidth`/`GetButtonHeight` report the physical key (90×90) and using those
  produces an image the service will not render.

## Not included

Previous/next, volume, seek, track or artist text, progress, like, settings, Spotify Web
API, OAuth, Windows support. One action, one job.

## License

MIT — see [LICENSE](LICENSE).

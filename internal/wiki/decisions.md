# Decisions and why

Load-bearing choices whose reasoning is not obvious from the code. Each was settled by
measurement, not by reading docs — the SDK documentation does not cover most of it.

## Reference the service's PluginApi, not the SDK tool's

`src/NowPlayingArtworkPlugin.csproj` prefers
`/Applications/Utilities/LogiPluginService.app/Contents/MonoBundle/PluginApi.dll` and only
falls back to the `logiplugintool` store.

The installed service ships PluginApi **6.4.1.3246, built for .NET 10**. The newest
`logiplugintool` on NuGet (6.1.4.22672) carries a **.NET 8** PluginApi. Building against
the tool's copy produces an assembly the installed service silently refuses to load — the
log says only `Cannot load plugin from <dll>`. That is also why `TargetFramework` is
`net10.0`: against the 6.4.1 PluginApi, `net8.0` fails with `CS1705`.

The SDK's own generated skeleton does not build unmodified on this machine for the same
reason, so this is a property of the environment, not of this project.

## The action's display name is deliberately empty, and it has no group

`displayName: String.Empty` and `groupName: null` in the `NowPlayingArtworkCommand`
constructor.

The Loupedeck app composes a key from a background, an icon and a **text element whose
content is the action's display name**. Returning an empty string from
`GetCommandDisplayName` does not remove it — the app does not use that value. Removing the
text in the app's key editor is worse: editing a key switches it to a statically composed
image and the artwork stops updating altogether.

An empty display name leaves the app nothing to draw, so the key keeps its default live
rendering and shows the cover alone.

Re-measured on 2026-09-25 against app 6.4.1.364 and service 6.4.1.3246, after a Marketplace
reviewer asked for the action to be given a name: a build carrying
`displayName: "Display Artwork"` draws exactly that string across the artwork. The two
screenshots in `assets/` are that measurement —
`screenshot-action-list.png` is what ships (blank row, clean artwork, description shown
beneath), and `screenshot-display-name-over-artwork.png` is the same panel with a display
name, where the row gains its label and the key preview loses the cover to it.

The mechanism, which took two wrong guesses to pin down. `Plugin.GetActionDisplayName` is
what resolves an action's label, and its first branch is
`if (!action.HasParameter || String.IsNullOrEmpty(actionParameter)) return
action.DisplayName;`. This command registers no parameters — there is no `AddParameter` call
anywhere in it — so that branch is always the one taken, and
`GetCommandDisplayName` / `TryGetCommandDisplayName` is never consulted for it at all. The
hook is reached only for actions that carry a parameter, where the label can vary per state.

Two consequences. The override of `GetCommandDisplayName` at the bottom of the command is
**not** what keeps the key clean — `displayName: String.Empty` is. And this is the SDK
behaving exactly as written, **not** an app defect: that claim was drafted here and in a
reply to Logitech and was wrong both times. `PluginDynamicAction.Load` does contain a
prefer-the-hook-then-fall-back-on-null pattern, which is what misled it, but that code
computes `ResetDisplayName` and never the rendered label. Read the branch you are actually
on before attributing a limitation to somebody else's code.

The group was dropped for that reviewer's second point, and it holds up. With one action, a
group whose name repeats the plugin's own is nesting that carries nothing — the app already
heads the list with the plugin name. Measured: `groupName: null` leaves the action listed
directly under that heading, still selectable and draggable, and the app shows the action's
**description** in the pane beneath it. A blank label is therefore not the same thing as an
unexplained action, which is the substance of the reply sent to the reviewer.

Keeping a group but giving it a descriptive name was considered and rejected: it preserves
the nesting the reviewer objected to and still leaves the row itself blank, so it buys
nothing.

## A ClientApplication subclass exists purely to satisfy the loader

See `gotchas.md`. It is not a design choice about applications; it is the price of being
loaded at all.

## Commit the track id only after the artwork is downloaded

Setting the observed id up front (the obvious ordering) means a failed download is never
retried: the id already matches, so every later tick short-circuits and the key keeps the
*previous* track's cover indefinitely. Committing after success makes the next tick retry
naturally. `MaxArtworkAttempts` then bounds the retries and, on giving up, accepts the
track with no artwork so the key stops lying about what is playing.

## Distinguish "script failed" from "no track"

`RunAppleScriptAsync` returns `null` when osascript could not be run and `""` when the
script ran and reported nothing. Collapsing both into `IsNullOrWhiteSpace` means a
transient osascript timeout blanks the key. Only the empty-string case clears it.

## Rename from "Spotify Artwork" to "Now Playing Artwork"

The old name put another company's trademark in the product title, next to that company's
own plugin on the same marketplace. The new name describes what the key does; Spotify is
named in the description, where it is descriptive rather than a claim of association.

Cost: the plugin identifier — and therefore the action id stored in profiles — changed, so
the existing key assignment was dropped and had to be made again. **Any future rename does
the same.**

## The layer in `internal/` is committed to a public repo

This project's GitHub repo is public, so `internal/` is public too. The content is
technical and reads as extended developer documentation, which is why it was left tracked
rather than ignored. Nothing personal or secret goes in here — if that changes, gitignore
it rather than trimming it.

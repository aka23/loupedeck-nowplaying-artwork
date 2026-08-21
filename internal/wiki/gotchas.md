# Gotchas

Traps in this environment, each one found the slow way. Nearly all of them present as the
same useless symptom: "the plugin does not work".

## A plugin with no ClientApplication subclass will not load

Even for a universal plugin with no application. The service reports only
`Cannot load plugin from <dll path>` and adds the plugin to a disabled list — no
exception, no type name, nothing pointing at the cause.

Confirmed both ways against the SDK's own generated skeleton: it loads as generated, and
fails with exactly this signature the moment its generated Application class is deleted.
`src/NowPlayingArtworkApplication.cs` exists solely for this. Do not "tidy it away".

## Reading the plugin log

`~/Library/Application Support/Logi/LogiPluginService/Logs/plugin_logs/<Name>.log`.

- `Cannot load plugin from <dir> because <name> is already loaded` — **benign.** A second
  scan of the same plugin. Healthy plugins (MuteDeck, AppleMusic) print it every boot.
- `Cannot load plugin from <dll path>` followed by `added to disabled plugins list` —
  **the real failure.** Note it names the *dll*, not the directory.
- Early lines can be lost: the log file is created lazily, so a plugin's first messages
  sometimes go nowhere. Absence of `polling started` does not mean `OnLoad` never ran.
- `VERB` lines are not written on every service start. Do not conclude "the service never
  asked" from a missing Verbose line — confirm the line still exists in the source first.

## An action image is BitmapBuilder(imageSize)-sized

80×80 for `Width90`. `PluginImageSize.GetButtonWidth/GetButtonHeight` report the physical
key (90×90, and 60×90 for `Width60`) and returning an image at *those* dimensions produces
something the service will not render — the key freezes on its last good frame.

Verified against the SDK's own `BitmapBuilder.CreateActionImage`, which produces exactly
`BitmapBuilder(imageSize)` dimensions. This was tried the wrong way round once and shipped
briefly; see commit "Revert \"Draw the artwork at the key's own size…\"".

## Editing a key in the app freezes its image

The key editor (background / layout / icon / text) turns the key into a statically composed
image. Hiding the text or resizing the icon there stops the artwork updating even though
the plugin keeps producing frames and the service keeps fetching them. Resetting the key to
default restores live rendering. This is why the no-text requirement is solved in the
plugin (empty display name) rather than in the app.

## logiplugintool install and uninstall do not work here

Both fail with `Plugin installation cannot start` after a ~5 s IPC timeout on the
`controlbus` channel — the tool (6.1.4) and the service (6.4.1) are out of step, and the
tool is already the newest on NuGet. What works:

- The Loupedeck app: **Marketplace → manage add-ons → + Install plugin from file**.
- By hand: unpack the `.lplug4` into
  `~/Library/Application Support/Logi/LogiPluginService/Plugins/<Name>/`, then
  `launchctl kickstart -k gui/$UID/com.logi.pluginservice.launch`.

## Renaming the plugin drops the key assignment

The action id stored in profiles is built from the plugin name and the command's full type
name (`$NowPlayingArtwork___Loupedeck.NowPlayingArtworkPlugin.NowPlayingArtworkCommand`).
Rename either and the app drops the now-dangling assignment. Say so *before* renaming, then
have the action dragged onto the key again.

## Two obj trees break the solution build

`src/Directory.Build.props` must not override `BaseIntermediateOutputPath`. When it pointed
at `$(SolutionDir)obj\`, a solution build put intermediates at the repo root, which stopped
`src/obj/` being excluded from the default compile glob — a stale generated `AssemblyInfo.cs`
then got compiled alongside the fresh one and the build failed with 17 × `CS0579`. Building
the csproj directly still worked, which made it look intermittent.

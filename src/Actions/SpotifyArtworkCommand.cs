namespace Loupedeck.SpotifyArtworkPlugin
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    public sealed class SpotifyArtworkCommand : PluginDynamicCommand
    {
        private const Int32 MaxCachedTracks = 20;
        private const Int32 MaxArtworkBytes = 10 * 1024 * 1024;
        private const Int32 MaxArtworkAttempts = 3;

        private const String TrackIdScript = """
            if application "Spotify" is not running then return ""
            tell application "Spotify"
                try
                    return id of current track
                on error
                    return ""
                end try
            end tell
            """;

        private const String ArtworkUrlScript = """
            if application "Spotify" is not running then return ""
            tell application "Spotify"
                try
                    return artwork url of current track
                on error
                    return ""
                end try
            end tell
            """;

        private const String PlayPauseScript = """
            if application "Spotify" is running then
                tell application "Spotify" to playpause
            end if
            """;

        private static readonly HttpClient HttpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(10),
        };

        private readonly Object _stateLock = new();
        private readonly Dictionary<String, Byte[]> _artworkCache = new(StringComparer.Ordinal);
        private readonly Queue<String> _cacheOrder = new();

        private CancellationTokenSource _lifetimeCancellation;
        private Task _pollTask;
        private String _observedTrackId;
        private String _failedTrackId;
        private Int32 _failedAttempts;
        private Byte[] _currentArtwork;
        private Int32 _playPauseRunning;
        private PluginImageSize? _lastImageSize;

        public SpotifyArtworkCommand()
            : base(
                displayName: "Spotify Artwork",
                description: "Shows the current Spotify album artwork and toggles Play/Pause",
                groupName: "Spotify")
        {
        }

        protected override Boolean OnLoad()
        {
            if (this._pollTask is not null)
            {
                return true;
            }

            this._lifetimeCancellation = new CancellationTokenSource();
            this._pollTask = this.PollLoopAsync(this._lifetimeCancellation.Token);
            PluginLog.Info("Spotify artwork polling started.");
            return true;
        }

        protected override Boolean OnUnload()
        {
            var cancellation = Interlocked.Exchange(ref this._lifetimeCancellation, null);
            var pollTask = Interlocked.Exchange(ref this._pollTask, null);

            cancellation?.Cancel();

            try
            {
                pollTask?.Wait(TimeSpan.FromSeconds(2));
            }
            catch (AggregateException ex) when (ex.InnerException is OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                PluginLog.Warning(ex, "Spotify artwork polling did not stop cleanly.");
            }
            finally
            {
                cancellation?.Dispose();
            }

            PluginLog.Info("Spotify artwork polling stopped.");
            return true;
        }

        protected override void RunCommand(String actionParameter)
        {
            if (Interlocked.Exchange(ref this._playPauseRunning, 1) != 0)
            {
                return;
            }

            _ = this.TogglePlayPauseAsync();
        }

        // The service calls the non-virtual TryGetCommandDisplayName, which reports "has a name"
        // for an empty string and falls back to the action's own DisplayName for null. An empty
        // string is therefore the only way a plugin can ask for a key with no text drawn on it.
        protected override String GetCommandDisplayName(String actionParameter, PluginImageSize imageSize) => String.Empty;

        protected override BitmapImage GetCommandImage(String actionParameter, PluginImageSize imageSize)
        {
            // The service picks the image size from how the key is configured, so a size the
            // renderer cannot handle shows up as "the artwork stopped updating". Log each new size
            // once, and never let this method throw: an exception here loses the frame entirely.
            if (this._lastImageSize != imageSize)
            {
                this._lastImageSize = imageSize;
                PluginLog.Info($"Spotify artwork is being drawn at image size {imageSize}.");
            }

            PluginLog.Verbose($"Spotify artwork frame requested: size={imageSize} parameter={actionParameter ?? "(null)"}.");

            Byte[] artwork;
            lock (this._stateLock)
            {
                artwork = this._currentArtwork;
            }

            try
            {
                return RenderArtwork(artwork, imageSize);
            }
            catch (Exception ex)
            {
                PluginLog.Warning(ex, $"Could not draw the Spotify artwork at image size {imageSize}.");
                return null;
            }
        }

        private static BitmapImage RenderArtwork(Byte[] artwork, PluginImageSize imageSize)
        {
            // `new BitmapBuilder(imageSize)` sizes the bitmap to GetWidth/GetHeight, which insets the
            // artwork inside the key: 80x80 drawn into a 90x90 key leaves a 5px border on every side.
            // Build at the button size instead so the artwork reaches the edges of the key.
            var buttonWidth = imageSize.GetButtonWidth();
            var buttonHeight = imageSize.GetButtonHeight();

            using var builder = buttonWidth > 0 && buttonHeight > 0
                ? new BitmapBuilder(buttonWidth, buttonHeight)
                : new BitmapBuilder(imageSize);

            builder.Clear(0xFF000000u);

            if (artwork is null || builder.Width <= 0 || builder.Height <= 0)
            {
                return builder.ToImage();
            }

            using var source = BitmapImage.FromArray(artwork);
            if (source.Width <= 0 || source.Height <= 0)
            {
                return builder.ToImage();
            }

            var sourceAspect = (Double)source.Width / source.Height;
            var targetAspect = (Double)builder.Width / builder.Height;

            if (sourceAspect > targetAspect)
            {
                var cropWidth = Math.Max(1, (Int32)Math.Round(source.Height * targetAspect));
                source.Crop((source.Width - cropWidth) / 2, 0, cropWidth, source.Height);
            }
            else if (sourceAspect < targetAspect)
            {
                var cropHeight = Math.Max(1, (Int32)Math.Round(source.Width / targetAspect));
                source.Crop(0, (source.Height - cropHeight) / 2, source.Width, cropHeight);
            }

            builder.DrawImage(source, 0, 0, builder.Width, builder.Height, BitmapRotation.None);
            return builder.ToImage();
        }

        private async Task PollLoopAsync(CancellationToken cancellationToken)
        {
            try
            {
                await this.PollSafelyAsync(cancellationToken).ConfigureAwait(false);

                using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
                while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
                {
                    await this.PollSafelyAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Spotify artwork polling stopped unexpectedly.");
            }
        }

        private async Task PollSafelyAsync(CancellationToken cancellationToken)
        {
            try
            {
                await this.PollOnceAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                PluginLog.Warning(ex, "Could not update Spotify artwork during this polling interval.");
            }
        }

        private async Task PollOnceAsync(CancellationToken cancellationToken)
        {
            var trackId = await RunAppleScriptAsync(TrackIdScript, "read the current Spotify track", cancellationToken)
                .ConfigureAwait(false);

            if (trackId is null)
            {
                // The script could not be run. Keep showing the last known artwork.
                return;
            }

            trackId = trackId.Trim();

            if (trackId.Length == 0)
            {
                // Spotify is not running, or it has no current track.
                this.ClearArtwork();
                return;
            }

            var restoredFromCache = false;
            lock (this._stateLock)
            {
                if (String.Equals(trackId, this._observedTrackId, StringComparison.Ordinal))
                {
                    return;
                }

                if (this._artworkCache.TryGetValue(trackId, out var cachedArtwork))
                {
                    this._observedTrackId = trackId;
                    this._currentArtwork = cachedArtwork;
                    restoredFromCache = true;
                }
            }

            if (restoredFromCache)
            {
                PluginLog.Info($"Spotify artwork restored from cache for track '{trackId}'.");
                this.NotifyImageChanged();
                return;
            }

            var artworkUrl = await RunAppleScriptAsync(ArtworkUrlScript, "read the current Spotify artwork URL", cancellationToken)
                .ConfigureAwait(false);

            if (String.IsNullOrWhiteSpace(artworkUrl) ||
                !Uri.TryCreate(artworkUrl.Trim(), UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            {
                PluginLog.Warning("The current Spotify track has no usable artwork URL.");
                this.HandleArtworkFailure(trackId);
                return;
            }

            Byte[] downloadedArtwork;
            try
            {
                using var response = await HttpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    .ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                if (response.Content.Headers.ContentLength is > MaxArtworkBytes)
                {
                    PluginLog.Warning("Spotify artwork download was rejected because it was too large.");
                    this.HandleArtworkFailure(trackId);
                    return;
                }

                downloadedArtwork = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
                if (downloadedArtwork.Length == 0 || downloadedArtwork.Length > MaxArtworkBytes)
                {
                    PluginLog.Warning("Spotify artwork download returned an empty or oversized image.");
                    this.HandleArtworkFailure(trackId);
                    return;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                PluginLog.Warning(ex, "Could not download Spotify artwork.");
                this.HandleArtworkFailure(trackId);
                return;
            }

            try
            {
                if (!BitmapImage.TryCreateFromArray(downloadedArtwork, out var testImage))
                {
                    PluginLog.Warning("Spotify artwork download was not a supported image.");
                    this.HandleArtworkFailure(trackId);
                    return;
                }

                testImage.Dispose();
            }
            catch (Exception ex)
            {
                PluginLog.Warning(ex, "Could not decode the downloaded Spotify artwork.");
                this.HandleArtworkFailure(trackId);
                return;
            }

            lock (this._stateLock)
            {
                this.AddToCache(trackId, downloadedArtwork);
                this._observedTrackId = trackId;
                this._currentArtwork = downloadedArtwork;
                this._failedTrackId = null;
                this._failedAttempts = 0;
            }

            PluginLog.Info($"Spotify artwork updated for track '{trackId}' ({downloadedArtwork.Length} bytes).");
            this.NotifyImageChanged();
        }

        private async Task TogglePlayPauseAsync()
        {
            try
            {
                var cancellationToken = this._lifetimeCancellation?.Token ?? CancellationToken.None;
                await RunAppleScriptAsync(PlayPauseScript, "toggle Spotify Play/Pause", cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                PluginLog.Warning(ex, "Could not toggle Spotify Play/Pause.");
            }
            finally
            {
                Interlocked.Exchange(ref this._playPauseRunning, 0);
            }
        }

        // Records a failed artwork attempt for the given track. The track id is only committed to
        // `_observedTrackId` once the artwork is ready, so the next polling interval retries the
        // track. After `MaxArtworkAttempts` failures the track is accepted without artwork, which
        // both stops the retries and clears the previous track's artwork off the key.
        private void HandleArtworkFailure(String trackId)
        {
            var gaveUp = false;

            lock (this._stateLock)
            {
                if (!String.Equals(trackId, this._failedTrackId, StringComparison.Ordinal))
                {
                    this._failedTrackId = trackId;
                    this._failedAttempts = 0;
                }

                this._failedAttempts++;

                if (this._failedAttempts >= MaxArtworkAttempts)
                {
                    this._observedTrackId = trackId;
                    this._currentArtwork = null;
                    gaveUp = true;
                }
            }

            if (gaveUp)
            {
                PluginLog.Warning("Giving up on the artwork for the current Spotify track.");
                this.NotifyImageChanged();
            }
        }

        private void ClearArtwork()
        {
            var cleared = false;

            lock (this._stateLock)
            {
                if (this._observedTrackId is not null || this._currentArtwork is not null)
                {
                    this._observedTrackId = null;
                    this._currentArtwork = null;
                    cleared = true;
                }

                this._failedTrackId = null;
                this._failedAttempts = 0;
            }

            if (cleared)
            {
                this.NotifyImageChanged();
            }
        }

        private void AddToCache(String trackId, Byte[] artwork)
        {
            if (this._artworkCache.ContainsKey(trackId))
            {
                this._artworkCache[trackId] = artwork;
                return;
            }

            while (this._cacheOrder.Count >= MaxCachedTracks)
            {
                this._artworkCache.Remove(this._cacheOrder.Dequeue());
            }

            this._artworkCache.Add(trackId, artwork);
            this._cacheOrder.Enqueue(trackId);
        }

        private void NotifyImageChanged()
        {
            try
            {
                this.ActionImageChanged();
            }
            catch (Exception ex)
            {
                PluginLog.Warning(ex, "Could not notify Logi Plugin Service about the Spotify artwork change.");
            }
        }

        private static async Task<String> RunAppleScriptAsync(
            String script,
            String operation,
            CancellationToken cancellationToken)
        {
            try
            {
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "/usr/bin/osascript",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                    },
                };

                process.StartInfo.ArgumentList.Add("-e");
                process.StartInfo.ArgumentList.Add(script);

                if (!process.Start())
                {
                    PluginLog.Warning($"Could not start osascript to {operation}.");
                    return null;
                }

                var standardOutput = process.StandardOutput.ReadToEndAsync();
                var standardError = process.StandardError.ReadToEndAsync();

                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(5));

                try
                {
                    await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    TryKill(process);
                    if (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }

                    PluginLog.Warning($"osascript timed out while trying to {operation}.");
                    return null;
                }

                var output = await standardOutput.ConfigureAwait(false);
                var error = await standardError.ConfigureAwait(false);

                if (process.ExitCode != 0)
                {
                    PluginLog.Warning($"osascript could not {operation}: {error.Trim()}");
                    return null;
                }

                return output.Trim();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                PluginLog.Warning(ex, $"AppleScript failed while trying to {operation}.");
                return null;
            }
        }

        private static void TryKill(Process process)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
            }
        }
    }
}

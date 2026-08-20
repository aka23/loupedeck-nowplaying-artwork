namespace Loupedeck.SpotifyArtworkPlugin
{
    using System;

    // The Logi Plugin Service refuses to load a plugin assembly that contains no `ClientApplication`
    // subclass, even when the plugin is universal (`HasNoApplication`). This is that placeholder:
    // it names no process or bundle, so it never claims to be running.

    public class SpotifyArtworkApplication : ClientApplication
    {
        public SpotifyArtworkApplication()
        {
        }

        protected override String GetProcessName() => String.Empty;

        protected override String GetBundleName() => String.Empty;

        public override ClientApplicationStatus GetApplicationStatus() => ClientApplicationStatus.Unknown;
    }
}

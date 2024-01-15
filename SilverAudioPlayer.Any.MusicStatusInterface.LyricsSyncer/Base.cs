using Avalonia.Threading;
using SilverAudioPlayer.Shared;
using System;
using System.Collections.Generic;
using System.Composition;
using System.Diagnostics;

namespace SilverAudioPlayer.Any.MusicStatusInterface.LyricsSyncer
{
    [Export(typeof(IMusicStatusInterface))]

    public class LyricsSyncerBase : IMusicStatusInterface
    {
        public bool IsStarted => _IsStarted;
        private bool _IsStarted;

        public string Name => "Lyrics syncer";

        public string Description => "Create synced lyrics out of already existing lyrics";

        public WrappedStream? Icon => null;

        public Version? Version => typeof(LyricsSyncerBase).Assembly.GetName().Version;

        public string Licenses => "lyricssyncer: gplv3";

        public List<Tuple<Uri, URLType>>? Links => null;

        public void Dispose()
        {
        }
        IMusicStatusInterfaceListener? _listner;
        public void StartIPC(IMusicStatusInterfaceListener listener)
        {
            _IsStarted= true;
            listener.TrackChangedNotification += TrackChanged;
            _listner = listener;
        }

        private void TrackChanged(object? sender, Song e)
        {
            if (!string.IsNullOrEmpty(e?.Metadata?.Lyrics) && e.Metadata?.SyncedLyrics?.Count is null or 0)
            {
                //ayo might be interesting
                Debug.WriteLine($"{e.Name} Has lyrics but they aren't synced");
                Dispatcher.UIThread.InvokeAsync(new Action(() =>
                {
                    LyricsView v = new(_listner);
                    v.Show();
                }));
              
            }
        }

        public void StopIPC(IMusicStatusInterfaceListener listener)
        {
            _IsStarted = false;
            listener.TrackChangedNotification -= TrackChanged;
            _listner = null;
        }
    }
}

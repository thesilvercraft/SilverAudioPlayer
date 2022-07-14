using SilverAudioPlayer.Shared;
using System;
using System.Collections.Generic;
using System.IO;

namespace SilverAudioPlayer.Avalonia
{
    internal class SAPAvaloniaListner : IPlayStreamProviderListner
    {
        private MainWindow mainWindow;

        public SAPAvaloniaListner(MainWindow mainWindow)
        {
            this.mainWindow = mainWindow;
        }

        public void LoadSong(WrappedStream s)
        {
            mainWindow.ProcessStream(s);
        }

        public void LoadSongs(IEnumerable<WrappedStream> streams)
        {
            mainWindow.ProcessStreams(streams);
        }
    }
}
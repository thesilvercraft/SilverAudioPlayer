using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using SilverAudioPlayer.Core;
using SilverAudioPlayer.Shared;

namespace SilverAudioPlayer.Avalonia;



public class SapAvaloniaPlayerEnviroment : IHaveConfigFilesWithKnownLocations,IPlayStreamProviderListener, ISyncEnvironmentListener, IMusicStatusInterfaceListenerAdmin
{
    private readonly MainWindow mainWindow;

    public event EventHandler<Song> TrackChangedNotification;
    public event EventHandler<PlaybackState> PlayerStateChanged;

    public SapAvaloniaPlayerEnviroment(MainWindow mainWindow)
    {
        this.mainWindow = mainWindow;
    }

    public void LoadSong(WrappedStream s)
    {
        Dispatcher.UIThread.Post(()=>mainWindow.Logic.ProcessStream(s));
    }

    public void ProcessFiles(IEnumerable<string> files)
    {
        mainWindow.Logic.ProcessFiles(files);
    }

    public void LoadSongs(IEnumerable<WrappedStream> streams)
    {
       mainWindow.Logic.ProcessStreams(streams);
    }

    public IEnumerable<string> FilterFiles(IEnumerable<string> files)
    {
        return mainWindow.Logic.FilterFiles(files);
    }

    public string Name => "SilverAudioPlayer.Avalonia";

    public string Description => "AvaloniaUI (https://github.com/AvaloniaUI/Avalonia) UI for SilverAudioPlayer";

    public WrappedStream? Icon => new WrappedEmbeddedResourceStream(typeof(SapAvaloniaPlayerEnviroment).Assembly,
        "SilverAudioPlayer.Avalonia.icon.svg");

    public Version? Version => typeof(SapAvaloniaPlayerEnviroment).Assembly.GetName().Version;

    public List<Tuple<Uri, URLType>>? Links => new()
    {
        new Tuple<Uri, URLType>(
            new Uri("https://github.com/thesilvercraft/SilverAudioPlayer/tree/master/SilverAudioPlayer.Avalonia"),
            URLType.Code),
        new Tuple<Uri, URLType>(
            new Uri($"https://www.nuget.org/packages/Avalonia/{typeof(Window).Assembly.GetName().Version}"),
            URLType.PackageManager),
        new Tuple<Uri, URLType>(new Uri("https://github.com/AvaloniaUI/Avalonia"), URLType.LibraryCode),
        new Tuple<Uri, URLType>(new Uri("https://docs.avaloniaui.net/"), URLType.LibraryDocumentation)
    };

    public Task<IMetadata?>? GetMetadataAsync(WrappedStream stream)
    {
        return mainWindow.Logic.GetMetadataFromStream(stream);
    }


    public async Task<List<Song>?> GetQueue()
    {
        //TODO ASK USER BEFORE GIVING OVER QUEUE
        return mainWindow.Logic.GetQueueCopy();
    }

    public IPicture? GetBestRepresentation(IReadOnlyList<IPicture>? pictures, PictureType type = PictureType.Front)
    {
        var res = pictures?.Where(x => x.PicType == type);
        return res?.Any()==true ? res?.First() : pictures?.FirstOrDefault();
    }

    public void Play() => mainWindow.Logic.Play();

    public void Pause() => mainWindow.Logic.Pause();

    public void PlayPause() => mainWindow.Logic.PlayPause(true);

    public void Stop() => mainWindow.RemoveTrack();

    public void Next() => mainWindow.Logic.Next();

    public void Previous() => mainWindow.Logic.Previous();

    public void SetVolume(byte volume) => mainWindow.dc.Volume= volume;

    public byte GetVolume() => mainWindow.dc.Volume;

    public Song? GetCurrentTrack()
    {
        return mainWindow.dc.CurrentSong;
    }

    public double GetDuration() => mainWindow.Player?.Length()?.TotalSeconds ?? (mainWindow.dc.CurrentSong?.Metadata?.Duration / 1000) ?? 2;


    public void SetPosition(double position) => mainWindow.Player?.SetPosition(TimeSpan.FromSeconds(position));

    public double GetPosition() => mainWindow.Player?.GetPosition().TotalSeconds ?? 1;

    public PlaybackState GetState()
    {
        var x = mainWindow.Player?.GetPlaybackState();
        if(x != null)
        {
            return (PlaybackState)x;

        }
        return PlaybackState.Stopped;
    }

    public RepeatState GetRepeat() => mainWindow.dc.LoopType;

    public void SetRepeat(RepeatState state) => mainWindow.dc.LoopType = state;

    public bool GetShuffle()
    {
        return false;
    }

    public void SetShuffle(bool shuffle)
    {
    }

    public void SetRating(byte rating)
    {
        //A music player should probably not edit the metadata of the music it plays
        //If someone thinks otherwise feel free to add code to this method
    }


    public string GetLyrics()
    {
        return mainWindow.dc.CurrentSong?.Metadata?.Lyrics;
    }

    public void FireTrackChangedNotification(Song? currentSong)
    {
        Task.Run(() =>
        {
            TrackChangedNotification?.Invoke(this,currentSong);
        });
    }

    public void FirePlayerStateChanged(PlaybackState state)
    {
        Task.Run(() =>
        {
        PlayerStateChanged?.Invoke(this, state);
        });
    }
   

    public string Licenses => @"Avalonia
The MIT License (MIT)

Copyright (c) .NET Foundation and Contributors All Rights Reserved

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the ""Software""), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED ""AS IS"", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
SilverAudioPlayer.Avalonia
    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <https://www.gnu.org/licenses/>.
";

    public string[] KnownConfigFileLocations => new[] { ConfigPath.GetPath("SilverAudioPlayer.Config.xml"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SilverCraftAvaloniav1Shared", "dotfile.json") };
}
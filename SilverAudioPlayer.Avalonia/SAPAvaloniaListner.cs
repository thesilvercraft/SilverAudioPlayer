using Avalonia.Controls;
using SilverAudioPlayer.Shared;
using System;
using System.Collections.Generic;

namespace SilverAudioPlayer.Avalonia
{
    internal class SAPAvaloniaListner : IPlayStreamProviderListner
    {
        private MainWindow mainWindow;

        public SAPAvaloniaListner(MainWindow mainWindow)
        {
            this.mainWindow = mainWindow;
        }

        public IPlayerEnviroment GetEnviroment()
        {
            return new SAPAvaloniaPlayerEnviroment();
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
    public class SAPAvaloniaPlayerEnviroment : IPlayerEnviroment
    {
        public string Name => "SilverAudioPlayer.Avalonia";

        public string Description => "AvaloniaUI (https://github.com/AvaloniaUI/Avalonia) UI for SilverAudioPlayer";

        public WrappedStream? Icon => new WrappedEmbeddedResourceStream(typeof(SAPAvaloniaPlayerEnviroment).Assembly, "SilverAudioPlayer.Avalonia.icon.png");
        public Version? Version => typeof(SAPAvaloniaPlayerEnviroment).Assembly.GetName().Version;
        public List<Tuple<Uri, URLType>>? Links => new() {
            new(new("https://github.com/thesilvercraft/SilverAudioPlayer/tree/master/SilverAudioPlayer.Avalonia"), URLType.Code),
            new(new($"https://www.nuget.org/packages/Avalonia/{typeof(Window).Assembly.GetName().Version}"), URLType.PackageManager),
            new(new("https://github.com/AvaloniaUI/Avalonia"),URLType.LibraryCode),
            new(new("https://docs.avaloniaui.net/"),URLType.LibraryDocumentation)
        };
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
    }
}
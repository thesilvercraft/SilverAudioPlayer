using Avalonia.Controls.Primitives;
using SilverAudioPlayer.Shared;
using System.Diagnostics;

namespace SilverAudioPlayer.Any.PlayStreamProvider.JellyFin
{
    public class JellyFinPlayStreamProvider : IPlayStreamProvider
    {
        public IPlayStreamProviderListner ProviderListner { get; set; }

        public string Name => "JellyFin Stream Provider";

        public string Description => "dotnet wrapper for jellyfin api";

        public WrappedStream? Icon => null;

        public Version? Version => typeof(JellyFinPlayStreamProvider).Assembly.GetName().Version;

        public string Licenses => @"SilverAudioPlayer.Any.PlayStreamProvider.JellyFin
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
JellyFin.Sdk
MIT License

Copyright (c) 2021 Jellyfin

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the ""Software""), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED ""AS IS"", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.";
        public List<Tuple<Uri, URLType>>? Links => new() {
            new(new("https://github.com/thesilvercraft/SilverAudioPlayer/tree/master/SilverAudioPlayer.Any.PlayStreamProvider.JellyFin"), URLType.Code),
            new(new($"https://www.nuget.org/packages/Jellyfin.Sdk/{typeof(Jellyfin.Sdk.ApiKeyClient).Assembly.GetName().Version}"), URLType.PackageManager),
            new(new("https://github.com/jellyfin/jellyfin-sdk-csharp"),URLType.LibraryCode)
        };

        private Gui gui;

        public JellyFinPlayStreamProvider()
        {
            gui = new(this);
        }

        public void ShowGui()
        {
            gui.Show();
        }
    }
}
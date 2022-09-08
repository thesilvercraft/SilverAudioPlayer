using NAudio.Wave;
using NLayer.NAudioSupport;
using SilverAudioPlayer.Shared;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SilverAudioPlayer.NAudio
{
    public interface INaudioWaveStreamWrapper
    {
        bool CanPlay(WrappedStream stream);

        WaveStream GetStream(WrappedStream stream);
    }

    public static class NaudioWaveStreamWrapperTypeHolder
    {
        private static readonly NaudioWaveStreamWrapperTypes instnc = new();

        public static NaudioWaveStreamWrapperTypes Get()
        {
            return instnc;
        }
    }

    [Export(typeof(IPlayProvider))]
    public class NaudioWaveStreamWrapper : IPlayProvider
    {
        public IPlayProviderListner ProviderListner { set => _=value; }

        public string Name => "NAudio Player";

        public string Description => "A player that wraps around NAudio (https://github.com/naudio/NAudio) and plays audio files that derive from PCM (or WAV). MP3 is provided using https://github.com/naudio/NLayer";

        public WrappedStream? Icon => new WrappedEmbeddedResourceStream(typeof(NaudioWaveStreamWrapper).Assembly, "SilverAudioPlayer.Any.PlayProvider.NAudio.NAudioLogo.png");

        public Version? Version => typeof(NaudioWaveStreamWrapper).Assembly.GetName().Version;
        public List<Tuple<Uri, URLType>>? Links => new() {
            new(new("https://github.com/thesilvercraft/SilverAudioPlayer/tree/master/SilverAudioPlayer.NAudio"), URLType.Code),
            new(new($"https://www.nuget.org/packages/Naudio/{typeof(WaveFilePlayer).Assembly.GetName().Version}"), URLType.PackageManager),
               new(new($"https://www.nuget.org/packages/NLayer/{typeof(NLayer.MpegFile).Assembly.GetName().Version}"), URLType.PackageManager),
                 new(new($"https://www.nuget.org/packages/NLayer.NAudioSupport/{typeof(Mp3FrameDecompressor).Assembly.GetName().Version}"), URLType.PackageManager),
            new(new("https://github.com/naudio/NAudio"),URLType.LibraryCode),
            new(new("https://github.com/naudio/NLayer"),URLType.LibraryCode)
        };
        public string Licenses => @"NAudio - https://github.com/naudio/NAudio 
Copyright 2020 Mark Heath

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the ""Software""), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED ""AS IS"", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
NLayer - https://github.com/naudio/NLayer/blob/master/LICENSE
MIT License

Copyright (c) 2018 Mark Heath, Andrew Ward & Contributors

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
SOFTWARE.
SilverAudioPlayer.Any.PlayProvider.NAudio
    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <https://www.gnu.org/licenses/>.";

        public bool CanPlayFile(WrappedStream stream)
        {
            return NaudioWaveStreamWrapperTypeHolder.Get().HasWrapper(stream);
        }

        public IPlay? GetPlayer(WrappedStream stream)
        {
            if (CanPlayFile(stream))
            {
                var player = new WaveFilePlayer();
                player.LoadFromProvider(NaudioWaveStreamWrapperTypeHolder.Get().GetStream(stream));
                return player;
            }
            return null;
        }

        public Task OnStartup()
        {
            NaudioWaveStreamWrapperTypeHolder.Get();
            return Task.CompletedTask;
        }
    }

    public class NaudioWaveStreamWrapperTypes
    {
        private List<INaudioWaveStreamWrapper>? wrapper = null;
        private CompositionContainer _container;

        [ImportMany(typeof(INaudioWaveStreamWrapper))]
        private IEnumerable<Lazy<INaudioWaveStreamWrapper>>? Wrappers;

        public NaudioWaveStreamWrapperTypes()
        {
            try
            {
                var catalog = new AggregateCatalog();
                catalog.Catalogs.Add(new AssemblyCatalog(typeof(INaudioWaveStreamWrapper).Assembly));
                if (Directory.Exists(Path.Combine(AppContext.BaseDirectory, "NExtensions")))
                {
                    catalog.Catalogs.Add(new DirectoryCatalog(Path.Combine(AppContext.BaseDirectory, "NExtensions")));
                }
                catalog.Catalogs.Add(new DirectoryCatalog(AppContext.BaseDirectory, "SilverAudioPlayer.Any.*.dll"));
                switch (Environment.OSVersion.Platform)
                {
                    case PlatformID.Win32NT:
                        catalog.Catalogs.Add(new DirectoryCatalog(AppContext.BaseDirectory, "SilverAudioPlayer.Windows.*.dll"));
                        break;

                    case PlatformID.Xbox:
                        catalog.Catalogs.Add(new DirectoryCatalog(AppContext.BaseDirectory, "SilverAudioPlayer.Xbox360.*.dll"));
                        break;
                }
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    catalog.Catalogs.Add(new DirectoryCatalog(AppContext.BaseDirectory, "SilverAudioPlayer.Sheep.*.dll"));
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    catalog.Catalogs.Add(new DirectoryCatalog(AppContext.BaseDirectory, "SilverAudioPlayer.Unix.*.dll"));
                }
                _container = new CompositionContainer(catalog);
                _container.ComposeParts(this);
            }
            catch (CompositionException compositionException)
            {
                Console.WriteLine(compositionException.ToString());
            }
        }

        private List<INaudioWaveStreamWrapper> GetWrapper()
        {
            wrapper ??= new(Wrappers.Select(x => x.Value) ?? Array.Empty<INaudioWaveStreamWrapper>());
            return wrapper;
        }

        public INaudioWaveStreamWrapper? GetWrapper(WrappedStream stream)
        {
            return GetWrapper().FirstOrDefault(x => x.CanPlay(stream));
        }

        public bool HasWrapper(WrappedStream stream)
        {
            return GetWrapper().Any(x =>
            {
                var e = x.CanPlay(stream);
                return e;
            });
        }

        internal WaveStream? GetStream(WrappedStream stream)
        {
            return GetWrapper().FirstOrDefault(x => x.CanPlay(stream))?.GetStream(stream);
        }
    }
}
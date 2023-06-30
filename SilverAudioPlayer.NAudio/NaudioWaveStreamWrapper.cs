using System.Composition;
using System.Composition.Hosting;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using NAudio.Wave;
using NLayer;
using NLayer.NAudioSupport;
using SilverAudioPlayer.Shared;
using SilverMagicBytes;

namespace SilverAudioPlayer.NAudio;

public interface INaudioWaveStreamWrapper
{
    IReadOnlyList<MimeType> SupportedMimeTypes { get; }

    /// <summary>
    ///     Get how ready this player is at playing a file
    /// </summary>
    /// <param name="stream">the file</param>
    /// <returns>
    ///     A number ranging from 255 (best possible implementation), 127 (can play but not the best implementation), to 0
    ///     (can not play)
    /// </returns>
    byte GetPlayingAbility(WrappedStream stream);

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
    public IPlayProviderListner ProviderListener
    {
        set => _ = value;
    }

    public string Name => "NAudio Player";

    public string Description =>
        "A player that wraps around NAudio (https://github.com/naudio/NAudio) and plays audio files that derive from PCM (or WAV). MP3 is provided using https://github.com/naudio/NLayer";

    public WrappedStream? Icon => new WrappedEmbeddedResourceStream(typeof(NaudioWaveStreamWrapper).Assembly,
        "SilverAudioPlayer.Windows.PlayProvider.NAudio.NAudioLogo.png");

    public Version? Version => typeof(NaudioWaveStreamWrapper).Assembly.GetName().Version;

    public List<Tuple<Uri, URLType>>? Links => new()
    {
        new Tuple<Uri, URLType>(
            new Uri("https://github.com/thesilvercraft/SilverAudioPlayer/tree/master/SilverAudioPlayer.NAudio"),
            URLType.Code),
        new Tuple<Uri, URLType>(
            new Uri($"https://www.nuget.org/packages/Naudio/{typeof(WaveOutEvent).Assembly.GetName().Version}"),
            URLType.PackageManager),
        new Tuple<Uri, URLType>(
            new Uri($"https://www.nuget.org/packages/NLayer/{typeof(MpegFile).Assembly.GetName().Version}"),
            URLType.PackageManager),
        new Tuple<Uri, URLType>(
            new Uri(
                $"https://www.nuget.org/packages/NLayer.NAudioSupport/{typeof(Mp3FrameDecompressor).Assembly.GetName().Version}"),
            URLType.PackageManager),
        new Tuple<Uri, URLType>(new Uri("https://github.com/naudio/NAudio"), URLType.LibraryCode),
        new Tuple<Uri, URLType>(new Uri("https://github.com/naudio/NLayer"), URLType.LibraryCode),
        new Tuple<Uri, URLType>(new Uri("https://github.com/naudio/Vorbis"), URLType.LibraryCode)
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
Vorbis - https://github.com/naudio/Vorbis/blob/master/LICENSE
MIT License

Copyright (c) 2021 Andrew Ward

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

    public IReadOnlyList<MimeType>? SupportedMimes => NaudioWaveStreamWrapperTypeHolder.Get().Wrappers
        .SelectMany(x => x.SupportedMimeTypes).ToList();

    public bool CanPlayFile(WrappedStream stream)
    {
        return NaudioWaveStreamWrapperTypeHolder.Get().HasWrapper(stream);
    }

    public IPlay? GetPlayer(WrappedStream stream)
    {
        if (CanPlayFile(stream))
        {
            var player = new WaveFilePlayer();
            player.LoadStream(stream);
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
    private readonly CompositionHost _container;
    private List<INaudioWaveStreamWrapper>? wrapper;

    public NaudioWaveStreamWrapperTypes()
    {
        var catalog = new ContainerConfiguration();
        List<Assembly> assemblies = new();

        void AddAssembliesFrom(string path, string filter)
        {
            assemblies.AddRange(Directory.GetFiles(path, filter)
                .Select(path2 => AssemblyLoadContext.Default.LoadFromAssemblyPath(path2)).Where(x => x != null));
        }

        void PlatformLogic(string path)
        {
            if (OperatingSystem.IsWindows())
            {
                AddAssembliesFrom(path, "SilverAudioPlayer.Windows.*.dll");
                if (OperatingSystem.IsWindowsVersionAtLeast(10))
                    AddAssembliesFrom(path, "SilverAudioPlayer.Windows10.*.dll");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                AddAssembliesFrom(path, "SilverAudioPlayer.Unix.*.dll");
            }

            AddAssembliesFrom(path, "SilverAudioPlayer.Any.*.dll");
        }

        var nextpath = Path.Combine(AppContext.BaseDirectory, "NExtensions");
        if (Directory.Exists(nextpath)) PlatformLogic(nextpath);
        PlatformLogic(AppContext.BaseDirectory);
        catalog.WithAssemblies(assemblies);
        _container = catalog.CreateContainer();
        _container.SatisfyImports(this);
    }

    [ImportMany] public IEnumerable<INaudioWaveStreamWrapper> Wrappers { get; set; }

    private List<INaudioWaveStreamWrapper> GetWrapper()
    {
        wrapper ??= new List<INaudioWaveStreamWrapper>(Wrappers ?? Array.Empty<INaudioWaveStreamWrapper>());
        return wrapper;
    }

    public INaudioWaveStreamWrapper? GetWrapper(WrappedStream stream)
    {
        return GetWrapper().OrderByDescending(x => x.GetPlayingAbility(stream))
            .First(x => x.GetPlayingAbility(stream) != 0);
    }

    public bool HasWrapper(WrappedStream stream)
    {
        return GetWrapper().Any(x => x.GetPlayingAbility(stream) != 0);
    }

    internal WaveStream? GetStream(WrappedStream stream)
    {
        return GetWrapper().OrderByDescending(x => x.GetPlayingAbility(stream))
            .First(x => x.GetPlayingAbility(stream) != 0)?.GetStream(stream);
    }
}
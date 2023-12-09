using System.Composition;
using System.Diagnostics;
using NAudio.Wave;
using Silver.Composition;
using SilverAudioPlayer.Core;
using SilverAudioPlayer.Shared;
using SilverMagicBytes;

namespace SilverAudioPlayer.NAudio;

public interface INaudioWaveStreamWrapper
{
    IReadOnlyList<MimeType> SupportedMimeTypes { get; }
    WaveStream GetStream(Stream stream);
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

    public string Name => "NAudio Player";

    public string Description =>
        "A player that wraps around NAudio (https://github.com/naudio/NAudio) and plays audio files that derive from PCM (or WAV). MP3 is provided using https://github.com/naudio/NLayer";
#if CSCore
    public WrappedStream? Icon => new WrappedEmbeddedResourceStream(typeof(NaudioWaveStreamWrapper).Assembly,
        "SilverAudioPlayer.Any.PlayProvider.NAudio.cscore.svg");
#else
    public WrappedStream? Icon => new WrappedEmbeddedResourceStream(typeof(NaudioWaveStreamWrapper).Assembly,
        "SilverAudioPlayer.Any.PlayProvider.NAudio.NAudioLogo.png");
#endif
    public Version? Version => typeof(NaudioWaveStreamWrapper).Assembly.GetName().Version;

    public List<Tuple<Uri, URLType>>? Links =>
    [
        new Tuple<Uri, URLType>(
            new Uri("https://gitlab.com/silvercraft/SilverAudioPlayer/tree/master/SilverAudioPlayer.NAudio"),
            URLType.Code),
        new Tuple<Uri, URLType>(
            new Uri($"https://www.nuget.org/packages/Naudio/{typeof(WaveOutEvent).Assembly.GetName().Version}"),
            URLType.PackageManager),
        new Tuple<Uri, URLType>(new Uri("https://github.com/naudio/NAudio"), URLType.LibraryCode),
        new Tuple<Uri, URLType>(new Uri("https://github.com/naudio/NLayer"), URLType.LibraryCode),
        new Tuple<Uri, URLType>(new Uri("https://github.com/naudio/Vorbis"), URLType.LibraryCode)
#if CSCore
        ,new(new("https://www.nuget.org/packages/CSCore/"),URLType.PackageManager),
        new(new("https://github.com/filoe/cscore/"),URLType.LibraryCode),
        new(new("https://filoe.github.io/cscore/sharpDox/"), URLType.LibraryDocumentation),
        new(new("https://gitlab.com/silvercraft/SilverAudioPlayer/tree/master/SilverAudioPlayer.Any.PlayProvider.CSCore"),URLType.PackageManager)
#endif
    ];
#if CSCore
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
    along with this program.  If not, see <https://www.gnu.org/licenses/>.
CSCore
## Microsoft Public License (Ms-PL) ##
Microsoft Public License (Ms-PL)
This license governs use of the accompanying software. If you use the software, you accept this license. If you do not accept the license, do not use the software.
#### 1. Definitions ####
The terms ""reproduce,"" ""reproduction,"" ""derivative works,"" and ""distribution"" have the same meaning here as under U.S. copyright law.
A ""contribution"" is the original software, or any additions or changes to the software.
A ""contributor"" is any person that distributes its contribution under this license.
""Licensed patents"" are a contributor's patent claims that read directly on its contribution.
#### 2. Grant of Rights ####
(A) Copyright Grant- Subject to the terms of this license, including the license conditions and limitations in section 3, each contributor grants you a non-exclusive, worldwide, royalty-free copyright license to reproduce its contribution, prepare derivative works of its contribution, and distribute its contribution or any derivative works that you create.
(B) Patent Grant- Subject to the terms of this license, including the license conditions and limitations in section 3, each contributor grants you a non-exclusive, worldwide, royalty-free license under its licensed patents to make, have made, use, sell, offer for sale, import, and/or otherwise dispose of its contribution in the software or derivative works of the contribution in the software.
#### 3. Conditions and Limitations ####
(A) No Trademark License- This license does not grant you rights to use any contributors' name, logo, or trademarks.
(B) If you bring a patent claim against any contributor over patents that you claim are infringed by the software, your patent license from such contributor to the software ends automatically.
(C) If you distribute any portion of the software, you must retain all copyright, patent, trademark, and attribution notices that are present in the software.
(D) If you distribute any portion of the software in source code form, you may do so only under this license by including a complete copy of this license with your distribution. If you distribute any portion of the software in compiled or object code form, you may only do so under a license that complies with this license.
(E) The software is licensed ""as-is."" You bear the risk of using it. The contributors give no express warranties, guarantees or conditions. You may have additional consumer rights under your local laws which this license cannot change. To the extent permitted under your local laws, the contributors exclude the implied warranties of merchantability, fitness for a particular purpose and non-infringement.";
#else
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
#endif



    public IReadOnlyList<MimeType>? SupportedMimes => NaudioWaveStreamWrapperTypeHolder.Get().GetWrappers()
        .SelectMany(x => x.SupportedMimeTypes).ToList();

    public bool CanPlayFile(WrappedStream stream)
    {
        return NaudioWaveStreamWrapperTypeHolder.Get().HasWrapper(stream);
    }

    public IPlay? GetPlayer(WrappedStream stream)
    {
        if (!CanPlayFile(stream)) return null;
        var player = new WaveFilePlayer();
        player.LoadStream(stream);
        return player;

    }

    public Task OnStartup()
    {
        NaudioWaveStreamWrapperTypeHolder.Get();
        return Task.CompletedTask;
    }
}

public class NaudioWaveStreamWrapperTypes
{
    private List<INaudioWaveStreamWrapper>? wrapper;

    public NaudioWaveStreamWrapperTypes()
    {
        wrapper = PlatformLogicHelper.GetTypeAll<INaudioWaveStreamWrapper>().ToList();
        Debug.Assert(wrapper.Count != 0);
    }


    public List<INaudioWaveStreamWrapper> GetWrappers()
    {
        return wrapper;
    }

    public INaudioWaveStreamWrapper? GetWrapper(WrappedStream stream)
    {
        return GetWrappers().First(x => x.SupportedMimeTypes.Contains(stream.MimeType));
    }

    public bool HasWrapper(WrappedStream stream)
    {
        if (stream.MimeType == KnownMimes.MKVMime && File.Exists("/usr/bin/mkvextract"))
        {
            return true;
        }
        var res = GetWrappers().Any(x => x.SupportedMimeTypes.Contains(stream.MimeType));
        return res;
    }


}
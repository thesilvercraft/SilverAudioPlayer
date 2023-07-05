using System.Composition;
using ATL;
using ATL.AudioData;
using SilverAudioPlayer.Shared;
using SilverAudioPlayer.Shared.ConfigScreen;
using SilverMagicBytes;
using Version = System.Version;

namespace SilverAudioPlayer.Any.MetadataSource.Z440AtlCore;

[Export(typeof(IMetadataProvider))]
public class AtlCoreFileMetadataProvider : IMetadataProvider,
    IAmConfigurable, IAmOnceAgainAskingYouForYourMemory
{
    private readonly List<IConfigurableElement> ConfigurableElements;
    public ObjectToRemember ConfigObject = new(Guid.Parse("97db82ee-ac2c-4772-b3f6-ca45957316a8"), new ZAtlCoreConfig());
    public ObjectToRemember[] ObjectsToRememberForMe => new ObjectToRemember[] { ConfigObject };

    public AtlCoreFileMetadataProvider()
    {
        ConfigurableElements = new List<IConfigurableElement>
        {
            new SimpleCheckBox
                { GetContent = () => "Allow MIDI reading", Checked = c => {

                    if(ConfigObject.Value is ZAtlCoreConfig x)
                    {
                        x.ReadMidiMetadata=c;
                        ((ICanBeToldThatAPartOfMeIsChanged)x).PropertyChanged(x,new("ReadMidiMetadata"));
                    }

                },
                GetChecked = () => GetAllowedMidi()
                }
        };
    }
    bool GetAllowedMidi()
    {
        if (ConfigObject.Value is ZAtlCoreConfig x)
        {
            return x.ReadMidiMetadata;
        }
        return false;
    }
    public List<IConfigurableElement> GetElements()
    {
        return ConfigurableElements;
    }

    public string Name => "Z440AtlCore Metadata Provider";
    public string Description => "Metadata provider that provides metadata using AtlDotnet";

    public WrappedStream? Icon => new WrappedEmbeddedResourceStream(typeof(AtlCoreFileMetadataProvider).Assembly,
        "SilverAudioPlayer.Any.MetadataSource.Z440AtlCore.ZATLMetadata.png");

    public Version? Version => typeof(AtlCoreFileMetadataProvider).Assembly.GetName().Version;

    public string Licenses => @"atldotnet - https://github.com/Zeugma440/atldotnet
MIT License

Copyright (c) 2017 Zeugma440

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
SilverAudioPlayer.MetadataSource.Z440AtlCore
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

    public List<Tuple<Uri, URLType>>? Links => new()
    {
        new Tuple<Uri, URLType>(
            new Uri(
                "https://github.com/thesilvercraft/SilverAudioPlayer/tree/master/SilverAudioPlayer.MetadataSource.Z440AtlCore"),
            URLType.Code),
        new Tuple<Uri, URLType>(
            new Uri($"https://www.nuget.org/packages/z440.atl.core/{typeof(Track).Assembly.GetName().Version}"),
            URLType.PackageManager),
        new Tuple<Uri, URLType>(new Uri("https://github.com/Zeugma440/atldotnet"), URLType.LibraryCode)
    };


    public bool CanGetMetadata(WrappedStream stream)
    {
        using var s = stream.GetStream();
        if (!GetAllowedMidi() && stream.MimeType == KnownMimes.MidMime) return false;
        return new Track(s, stream.MimeType.RealMimeTypeToFakeMimeType()).AudioFormat.ID != -1;
    }

    public Task<IMetadata?> GetMetadata(WrappedStream stream)
    {
        return Task.FromResult((IMetadata?)new AtlCoreMetadata(stream));
    }
}
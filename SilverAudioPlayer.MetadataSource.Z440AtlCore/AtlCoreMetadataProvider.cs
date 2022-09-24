using SilverAudioPlayer.Shared;
using ATL;
using System.Composition;

namespace SilverAudioPlayer.MetadataSource.Z440AtlCore
{
    [Export(typeof(IMetadataProvider))]
    public class AtlCoreFileMetadataProvider : IMetadataProvider
    {
        public string Name => "Z440AtlCore Metadata Provider";

        public string Description => "Metadata provider that provides metadata using AtlDotnet";

        public WrappedStream? Icon => new WrappedEmbeddedResourceStream(typeof(AtlCoreFileMetadataProvider).Assembly, "SilverAudioPlayer.Any.MetadataSource.Z440AtlCore.ZATLMetadata.png");
        public System.Version? Version => typeof(AtlCoreFileMetadataProvider).Assembly.GetName().Version;

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
        public List<Tuple<Uri, URLType>>? Links => new() {
            new(new("https://github.com/thesilvercraft/SilverAudioPlayer/tree/master/SilverAudioPlayer.MetadataSource.Z440AtlCore"), URLType.Code),
            new(new($"https://www.nuget.org/packages/z440.atl.core/{typeof(Track).Assembly.GetName().Version}"), URLType.PackageManager),
            new(new("https://github.com/Zeugma440/atldotnet"),URLType.LibraryCode)
        };
        public bool CanGetMetadata(WrappedStream stream)
        {
            using var s = stream.GetStream();
            return new Track(s, stream.MimeType.RealMimeTypeToFakeMimeType()).AudioFormat != null;
        }

        public Task<Metadata?> GetMetadata(WrappedStream stream)
        {
            using var s = stream.GetStream();
            return Task.FromResult((Metadata?)new AtlCoreMetadata(new(s, stream.MimeType.RealMimeTypeToFakeMimeType())));
        }
    }
}
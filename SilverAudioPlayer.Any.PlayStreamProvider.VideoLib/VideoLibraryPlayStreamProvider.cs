using System.Composition;
using SilverAudioPlayer.Shared;
using SilverCraft.Ekorensis;
using VideoLibrary;

namespace SilverAudioPlayer.Any.PlayStreamProvider.VideoLib;
[Export(typeof(IPlayStreamProvider))]
public class VideoLibraryPlayStreamProvider :IPlayStreamProviderThatSupportsUrls
{
    private string[] Domains = new[]
    {
        "www.youtube.com","youtube.com","yewtu.be", "vid.puffyan.us", "inv.riverside.rocks", "invidio.xamh.de", "y.com.sb",
        "invidious.sethforprivacy.com","yt.artemislena.eu","invidious.tiekoetter.com","invidious.flokinet.to",
        "inv.bp.projectsegfau.lt","inv.odyssey346.dev","invidious.snopyta.org","invidious.baczek.me",
        "invidious.drivet.xyz","yt.funami.tech","invidious.slipfox.xyz","invidious.dhusch.de","invidious.weblibre.org",
        "invidious.esmailelbob.xyz","invidious.namazso.eu","invidious.privacydev.net"
    };

    public void Use(IPlayStreamProviderListener env)
    {
        if (env is IConfigurableListener listener)
        {
            var window = listener.TryGetWindow(new List<IConfigurableElement>
            {
                new SimpleLabel()
                {
                    GetContent = ()=>"To use this PlayStreamProvider you must load a URL via the player"
                }
            });
            window?.Show();
        }
     
    }

    public string Name => "libVideo";
    public string Description => "libVideo provider";
    public WrappedStream Icon => new WrappedEmbeddedResourceStream(typeof(VideoLibraryPlayStreamProvider).Assembly,
        "SilverAudioPlayer.Any.PlayStreamProvider.VideoLib.icon.svg");

    public Version? Version => typeof(VideoLibraryPlayStreamProvider).Assembly.GetName().Version;
    public string Licenses => @"SilverAudioPlayer.Any.PlayStreamProvider.VideoLib
GPL3.0

https://github.com/omansak/libvideo/blob/master/LICENSE
BSD 2-Clause License

Copyright (c) 2021, OMANSAK
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

* Redistributions of source code must retain the above copyright notice, this
  list of conditions and the following disclaimer.

* Redistributions in binary form must reproduce the above copyright notice,
  this list of conditions and the following disclaimer in the documentation
  and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS ""AS IS""
AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.";
    public List<Tuple<Uri, URLType>>? Links => new()
    {
        new Tuple<Uri, URLType>(new Uri("https://github.com/omansak/libvideo"), URLType.LibraryCode),
        new Tuple<Uri, URLType>(new Uri("https://www.nuget.org/packages/VideoLibrary"), URLType.PackageManager)
    };
   
    public bool IsUrlSupported(Uri given, IPlayStreamProviderListener listener)
    {
        return (Domains.Any(x => given.Host.StartsWith(x)));
    }

    public async Task LoadUrlAsync(Uri given, IPlayStreamProviderListener listener)
    {
        if(Domains.Any(x => given.Host.StartsWith(x)))
        {
            UriBuilder builder = new(given);
            builder.Host = Domains[0];
            given = builder.Uri;
        }
        var youTube = YouTube.Default;
        var video =await youTube.GetAllVideosAsync(given.ToString());
        listener.LoadSong(new WrappedHttpStream(video.Where(x => x.AudioBitrate > 1).OrderByDescending(x => x.AudioBitrate).First().Uri));
    }
}
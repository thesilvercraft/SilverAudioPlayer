using DynamicData;
using SilverAudioPlayer.Shared;
using SilverMagicBytes;
using System.Composition;

namespace SilverAudioPlayer.Any.PlayProvider.CSCore
{
    [Export(typeof(IPlayProvider))]
    public class CsCorePlayProvider : IPlayProvider
    {
        public IReadOnlyList<MimeType>? SupportedMimes => new List<MimeType>() { 
            KnownMimes.WAVMime, KnownMimes.FLACMime, KnownMimes.AiffMime, KnownMimes.MP3Mime,KnownMimes.Mp2Mime ,KnownMimes.MpegMime  };

        public IPlayProviderListner ProviderListener { set => _ = value; }

        public string Name => "CSCore player";

        public string Description => "A player implemented with CSCore";

        public WrappedStream? Icon => new WrappedEmbeddedResourceStream(typeof(CsCorePlayProvider).Assembly,
            "SilverAudioPlayer.Any.PlayProvider.CSCore.cscore.svg");


        public Version? Version => typeof(CsCorePlayProvider).Assembly.GetName().Version;

        public string Licenses => """
CSCore
## Microsoft Public License (Ms-PL) ##
Microsoft Public License (Ms-PL)
This license governs use of the accompanying software. If you use the software, you accept this license. If you do not accept the license, do not use the software.
#### 1. Definitions ####
The terms "reproduce," "reproduction," "derivative works," and "distribution" have the same meaning here as under U.S. copyright law.
A "contribution" is the original software, or any additions or changes to the software.
A "contributor" is any person that distributes its contribution under this license.
"Licensed patents" are a contributor's patent claims that read directly on its contribution.
#### 2. Grant of Rights ####
(A) Copyright Grant- Subject to the terms of this license, including the license conditions and limitations in section 3, each contributor grants you a non-exclusive, worldwide, royalty-free copyright license to reproduce its contribution, prepare derivative works of its contribution, and distribute its contribution or any derivative works that you create.
(B) Patent Grant- Subject to the terms of this license, including the license conditions and limitations in section 3, each contributor grants you a non-exclusive, worldwide, royalty-free license under its licensed patents to make, have made, use, sell, offer for sale, import, and/or otherwise dispose of its contribution in the software or derivative works of the contribution in the software.
#### 3. Conditions and Limitations ####
(A) No Trademark License- This license does not grant you rights to use any contributors' name, logo, or trademarks.
(B) If you bring a patent claim against any contributor over patents that you claim are infringed by the software, your patent license from such contributor to the software ends automatically.
(C) If you distribute any portion of the software, you must retain all copyright, patent, trademark, and attribution notices that are present in the software.
(D) If you distribute any portion of the software in source code form, you may do so only under this license by including a complete copy of this license with your distribution. If you distribute any portion of the software in compiled or object code form, you may only do so under a license that complies with this license.
(E) The software is licensed "as-is." You bear the risk of using it. The contributors give no express warranties, guarantees or conditions. You may have additional consumer rights under your local laws which this license cannot change. To the extent permitted under your local laws, the contributors exclude the implied warranties of merchantability, fitness for a particular purpose and non-infringement.
""";

        public List<Tuple<Uri, URLType>>? Links => new() {
            new(new("https://www.nuget.org/packages/CSCore/"),URLType.PackageManager),
        new(new("https://github.com/filoe/cscore/"),URLType.LibraryCode),
        new(new("https://filoe.github.io/cscore/sharpDox/"), URLType.LibraryDocumentation),
        new(new("https://github.com/thesilvercraft/SilverAudioPlayer/tree/master/SilverAudioPlayer.Any.PlayProvider.CSCore"),URLType.PackageManager)};

        public bool CanPlayFile(WrappedStream stream)
        {
            return true;
        }

        public IPlay? GetPlayer(WrappedStream stream)
        {
            if (DoFunny && Funny!=null)
            {
                Funny.LoadStream(stream);
                return Funny;
            }
            var player = new CSCorePlayer();
            player.LoadStream(stream);
            return player;
        }
    
        private CSCorePlayer? Funny = null;
        /// <summary>
        /// Try to share the same ISoundOut device and player, results in uh funny behaviour
        /// </summary>
        public bool DoFunny { get; set; } = false;
    
        public Task OnStartup()
        {
            if (!DoFunny) return Task.CompletedTask;
            Funny = new();
            Funny.DoFunny = DoFunny;
            return Task.CompletedTask;
        }
    }

}
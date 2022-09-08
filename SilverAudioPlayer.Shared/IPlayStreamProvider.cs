using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SilverAudioPlayer.Shared
{
    public interface IPlayStreamProvider : ICodeInformation
    {
        void ShowGui();

        public IPlayStreamProviderListner ProviderListner {  set; }
    }

    public interface IPlayStreamProviderListner
    {
        void LoadSong(WrappedStream s);

        void LoadSongs(IEnumerable<WrappedStream> streams);
        IPlayerEnviroment GetEnviroment();
    }
}
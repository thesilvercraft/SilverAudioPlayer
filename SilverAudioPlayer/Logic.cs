using SilverAudioPlayer.NAudio;
using SilverAudioPlayer.Shared;
using System.ComponentModel.Composition;

namespace SilverAudioPlayer
{
    public class Logic
    {
        [ImportMany(typeof(IPlayProvider))]
        public IEnumerable<Lazy<IPlayProvider>>? Providers;

        public bool CanGetPlayerFromURI(string URI)
        {
            return Providers.Any(x => x.IsValueCreated && x.Value.CanPlayFile(URI));
        }

        public IPlay? GetPlayerFromURI(string URI)
        {
            var provider = Providers.FirstOrDefault(x => x.IsValueCreated && x.Value.CanPlayFile(URI));
            return provider.Value.GetPlayer(URI);
        }
    }
}
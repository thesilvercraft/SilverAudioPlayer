using SilverAudioPlayer.Shared;
using System.Diagnostics;

namespace SilverAudioPlayer.Any.PlayStreamProvider.JellyFin
{
    public class JellyFinPlayStreamProvider : IPlayStreamProvider
    {
        public IPlayStreamProviderListner ProviderListner { get; set; }
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
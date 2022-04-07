using SilverAudioPlayer.Shared;
using System.ComponentModel.Composition;

namespace SilverAudioPlayer.SystemMediaSoundPlayer
{
    [Export(typeof(IPlayProvider))]
    public class SystemMediaSoundPlayerWrapperWrapper : IPlayProvider
    {
        public bool CanPlayFile(string URI)
        {
            if (Path.GetExtension(URI).ToUpperInvariant() == ".WAV" && File.Exists(URI))
            {
                using var bytes = File.OpenRead(URI);
                byte[] vs = new byte[4];
                return bytes.Read(vs, 0, 4) == 4 && vs[0] == 0x52 && vs[1] == 0x49 && vs[2] == 0x46 && vs[3] == 0x46 && bytes.Read(vs, 0, 4) == 4 && bytes.Read(vs, 0, 4) == 4 && vs[0] == 0x57 && vs[1] == 0x41 && vs[2] == 0x56 && vs[3] == 0x45;
            }
            return false;
        }

        public bool CanPlayStream(Stream stream)
        {
            //NAUDIOWAVESTREAMWRAPPER DOES NOT SUPPORT STREAMS FOR NOW
            return false;
        }

        public IPlay? GetPlayer(string URI)
        {
            if (CanPlayFile(URI))
            {
                var player = new SystemMediaSoundPlayerwrapper();
                player.LoadFile(URI);
                return player;
            }
            return null;
        }
    }
}
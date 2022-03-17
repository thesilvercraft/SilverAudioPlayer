using SilverAudioPlayer.NAudio;
using SilverAudioPlayer.Shared;
using SilverAudioPlayer.SystemMediaSoundPlayer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SilverAudioPlayer
{
    public static class Logic
    {
        public static bool CanGetPlayerFromURI(string URI)
        {
            if (WaveFilePlayer.CanPlayFile(URI))
            {
                return true;
            }
            /*if (SystemMediaSoundPlayerwrapper.CanPlayFile(URI))
            {
                return true;
            }*/
            return false;
        }

        public static IPlay? GetPlayerFromURI(string URI)
        {
            if (WaveFilePlayer.CanPlayFile(URI))
            {
                WaveFilePlayer wfp = new();
                wfp.LoadFile(URI);
                return wfp;
            }
            /*if (SystemMediaSoundPlayerwrapper.CanPlayFile(URI))
            {
                var pain = new SystemMediaSoundPlayerwrapper();
                pain.LoadFile(URI);
                return pain;
            }*/
            return null;
        }
    }
}
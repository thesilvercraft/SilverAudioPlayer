using Microsoft.VisualStudio.TestTools.UnitTesting;
using SilverAudioPlayer.NAudio;
using SilverAudioPlayer.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SilverAudioPlayer.NAudio.Tests
{
    [TestClass()]
    public class WaveFilePlayerTests
    {
        [TestMethod]
        public void FlacFileTest() => TestFilePlaying("gaming.flac");

        [TestMethod]
        public void MP3FileTest() => TestFilePlaying("gaming.mp3");

        [TestMethod]
        public void AiffFileTest() => TestFilePlaying("gaming.aiff");

        [TestMethod]
        public void MP2FileTest() => TestFilePlaying("gaming.mp2");

        [TestMethod]
        public void WMAFileTest() => TestFilePlaying("gaming.wma");

        [TestMethod]
        public void WAVFileTest() => TestFilePlaying("gaming.wav");

        private static void TestFilePlaying(string fileName)
        {
            WaveFilePlayer player = new();
            player.LoadFile(fileName);
            player.Play();
            Assert.AreEqual(player.GetPlaybackState(), PlaybackState.Playing);
            System.Threading.Thread.Sleep(1000);
            player.Stop();
            Assert.AreEqual(player.GetPlaybackState(), PlaybackState.Stopped);
            player.Dispose();
            TestFileSeeking(fileName);
        }

        public static void TestFileSeeking(string fileName)
        {
            WaveFilePlayer player = new();
            player.LoadFile(fileName);
            player.Play();
            System.Threading.Thread.Sleep(1000);
            var postoseekto = player.GetPosition() - TimeSpan.FromMilliseconds(100);
            player.SetPosition(postoseekto);
            Assert.AreEqual(player.GetPosition(), postoseekto);
            System.Threading.Thread.Sleep(1000);
            player.Stop();
            player.Dispose();
        }
    }
}
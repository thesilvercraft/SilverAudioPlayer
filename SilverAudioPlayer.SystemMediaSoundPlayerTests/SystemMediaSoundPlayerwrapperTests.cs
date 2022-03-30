using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading;

namespace SilverAudioPlayer.SystemMediaSoundPlayer.Tests
{
    [TestClass()]
    public class SystemMediaSoundPlayerwrapperTests
    {
        [TestMethod()]
        public void CanPlayFileTest()
        {
            //Valid wav file
            Assert.IsTrue(SystemMediaSoundPlayerwrapper.CanPlayFile("jmbcscream.wav"));
            //Non existant file
            Assert.IsFalse(SystemMediaSoundPlayerwrapper.CanPlayFile("asasas"));
            //MP3 file
            Assert.IsFalse(SystemMediaSoundPlayerwrapper.CanPlayFile("as.mp3"));
        }

        [TestMethod()]
        public void LoadFileTest()
        {
            var s = new SystemMediaSoundPlayerwrapper();
            s.LoadFile("jmbcscream.wav");
            s.Dispose();
        }

        [TestMethod()]
        public void PauseTest()
        {
            var s = new SystemMediaSoundPlayerwrapper();
            s.LoadFile("jmbcscream.wav");
            s.Play();
            s.Pause();
            s.Dispose();
        }

        [TestMethod()]
        public void PlayTest()
        {
            var s = new SystemMediaSoundPlayerwrapper();
            s.LoadFile("jmbcscream.wav");
            s.Play();
            Thread.Sleep(1000);
            s.Pause();
            s.Dispose();
        }

        [TestMethod()]
        public void ResumeTest()
        {
            var s = new SystemMediaSoundPlayerwrapper();
            s.LoadFile("jmbcscream.wav");
            s.Play();
            Thread.Sleep(1000);
            s.Pause();
            Thread.Sleep(1000);
            s.Resume();
            s.Dispose();
        }
    }
}
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SilverAudioPlayer.Naudio.Flac;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SilverAudioPlayer.Naudio.Flac.Tests
{
    [TestClass()]
    public class FlacNaudioWaveStreamWrapperTests
    {
        [TestMethod()]
        public void CanPlayTest()
        {
            var e = new FlacNaudioWaveStreamWrapper();
            Assert.IsTrue(e.CanPlay("as.flac"));
            Assert.IsFalse(e.CanPlay("as.mp3"));
        }

        [TestMethod()]
        public void GetStreamTest()
        {
            var e = new FlacNaudioWaveStreamWrapper();
            var z = e.GetStream("as.flac");
            Assert.IsNotNull(z);
        }
    }
}
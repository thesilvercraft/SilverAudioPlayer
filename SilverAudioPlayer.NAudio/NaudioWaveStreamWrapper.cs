using NAudio.Wave;
using SilverAudioPlayer.Shared;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace SilverAudioPlayer.NAudio
{
    public interface INaudioWaveStreamWrapper
    {
        bool CanPlay(string file);

        WaveStream GetStream(string file);
    }

    [Export(typeof(INaudioWaveStreamWrapper))]
    public class WaveFileReaderWrapper : INaudioWaveStreamWrapper
    {
        public bool CanPlay(string file)
        {
            if (File.Exists(file))
            {
                using var bytes = File.OpenRead(file);
                byte[] vs = new byte[4];
                return bytes.Read(vs, 0, 4) == 4 && vs[0] == 0x52 && vs[1] == 0x49 && vs[2] == 0x46 && vs[3] == 0x46 && bytes.Read(vs, 0, 4) == 4 && bytes.Read(vs, 0, 4) == 4 && vs[0] == 0x57 && vs[1] == 0x41 && vs[2] == 0x56 && vs[3] == 0x45;
            }
            return false;
        }

        public WaveStream GetStream(string file)
        {
            return new WaveFileReader(file);
        }
    }

    [Export(typeof(INaudioWaveStreamWrapper))]
    public class Mp3FileReaderReaderWrapper : INaudioWaveStreamWrapper
    {
        public bool CanPlay(string file)
        {
            if (File.Exists(file))
            {
                using var bytes = File.OpenRead(file);
                byte[] vs = new byte[2];
                return bytes.Read(vs, 0, 2) == 2
                    && ((vs[0] == 0xFF && vs[1] == 0xFB)
                    || (vs[0] == 0xFF && vs[1] == 0xF3)
                    || (vs[0] == 0xFF && vs[1] == 0xF2))
                    || (vs[0] == 0x49 && vs[1] == 0x44 && (bytes.Read(vs, 0, 1) == 1) && vs[0] == 0x33);
            }
            return false;
        }

        public WaveStream GetStream(string file)
        {
            return new Mp3FileReader(file);
        }
    }

    [Export(typeof(INaudioWaveStreamWrapper))]
    public class AiffFileReaderWrapper : INaudioWaveStreamWrapper
    {
        public bool CanPlay(string file)
        {
            if (File.Exists(file))
            {
                using var bytes = File.OpenRead(file);
                byte[] vs = new byte[4];
                return bytes.Read(vs, 0, 4) == 4
                    && vs[0] == 0x46 && vs[1] == 0x4F && vs[2] == 0x52 && vs[3] == 0x4D
                    && bytes.Read(vs, 0, 4) == 4
                    && bytes.Read(vs, 0, 4) == 4
                    && vs[0] == 0x41 && vs[1] == 0x49 && vs[2] == 0x46 && vs[3] == 0x46;
            }
            return false;
        }

        public WaveStream GetStream(string file)
        {
            return new AiffFileReader(file);
        }
    }

    public static class NaudioWaveStreamWrapperTypeHolder
    {
        private static readonly NaudioWaveStreamWrapperTypes instnc = new();

        public static NaudioWaveStreamWrapperTypes Get()
        {
            return instnc;
        }
    }

    [Export(typeof(IPlayProvider))]
    public class NaudioWaveStreamWrapper : IPlayProvider
    {
        public bool CanPlayFile(string URI)
        {
            return NaudioWaveStreamWrapperTypeHolder.Get().HasWrapper(URI);
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
                var player = new WaveFilePlayer();
                player.LoadFile(URI);
                return player;
            }
            return null;
        }
    }

    public class NaudioWaveStreamWrapperTypes
    {
        private List<INaudioWaveStreamWrapper>? wrapper = null;
        private CompositionContainer _container;

        [ImportMany(typeof(INaudioWaveStreamWrapper))]
        private IEnumerable<Lazy<INaudioWaveStreamWrapper>>? Wrappers;

        public NaudioWaveStreamWrapperTypes()
        {
            try
            {
                // An aggregate catalog that combines multiple catalogs.
                var catalog = new AggregateCatalog();
                // Adds all the parts found in the same assembly as the Program class.
                catalog.Catalogs.Add(new AssemblyCatalog(typeof(INaudioWaveStreamWrapper).Assembly));

                if (Directory.Exists(Path.Combine(AppContext.BaseDirectory, "Extensions")))
                {
                    catalog.Catalogs.Add(new DirectoryCatalog(Path.Combine(AppContext.BaseDirectory, "Extensions")));
                }
                else
                {
                    catalog.Catalogs.Add(new DirectoryCatalog(AppContext.BaseDirectory));
                }

                // Create the CompositionContainer with the parts in the catalog.
                _container = new CompositionContainer(catalog);
                _container.ComposeParts(this);
            }
            catch (CompositionException compositionException)
            {
                Console.WriteLine(compositionException.ToString());
            }
        }

        private List<INaudioWaveStreamWrapper> GetWrapper()
        {
            if (wrapper == null)
            {
                Debug.WriteLine(Wrappers?.Count().ToString() ?? "WRAPPERS NULL");
                wrapper = new(Wrappers.Select(x => x.Value) ?? Array.Empty<INaudioWaveStreamWrapper>())
                {
                };
            }
            return wrapper;
        }

        public bool HasWrapper(string file)
        {
            return GetWrapper().Any(x => { Debug.WriteLine(x.GetType().FullName); Debug.WriteLine(x.CanPlay(file)); return x.CanPlay(file); });
        }

        public INaudioWaveStreamWrapper? GetWrapper(string file)
        {
            return GetWrapper().FirstOrDefault(x => x.CanPlay(file));
        }
    }
}
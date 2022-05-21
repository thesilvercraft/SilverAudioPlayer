using NAudio.Wave;
using SilverAudioPlayer.Shared;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SilverAudioPlayer.NAudio
{
    public interface INaudioWaveStreamWrapper
    {
        bool CanPlay(WrappedStream stream);

        WaveStream GetStream(WrappedStream stream);
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
        public bool CanPlayFile(WrappedStream stream)
        {
            return NaudioWaveStreamWrapperTypeHolder.Get().HasWrapper(stream);
        }

        public IPlay? GetPlayer(WrappedStream stream)
        {
            if (CanPlayFile(stream))
            {
                var player = new WaveFilePlayer();
                player.LoadFromProvider(NaudioWaveStreamWrapperTypeHolder.Get().GetStream(stream));
                return player;
            }
            return null;
        }

        public Task OnStartup()
        {
            NaudioWaveStreamWrapperTypeHolder.Get();
            return Task.CompletedTask;
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
                var catalog = new AggregateCatalog();
                catalog.Catalogs.Add(new AssemblyCatalog(typeof(INaudioWaveStreamWrapper).Assembly));
                if (Directory.Exists(Path.Combine(AppContext.BaseDirectory, "NExtensions")))
                {
                    catalog.Catalogs.Add(new DirectoryCatalog(Path.Combine(AppContext.BaseDirectory, "NExtensions")));
                }
                catalog.Catalogs.Add(new DirectoryCatalog(AppContext.BaseDirectory, "SilverAudioPlayer.Any.*.dll"));
                switch (Environment.OSVersion.Platform)
                {
                    case PlatformID.Win32NT:
                        catalog.Catalogs.Add(new DirectoryCatalog(AppContext.BaseDirectory, "SilverAudioPlayer.Windows.*.dll"));
                        break;

                    case PlatformID.Xbox:
                        catalog.Catalogs.Add(new DirectoryCatalog(AppContext.BaseDirectory, "SilverAudioPlayer.Xbox360.*.dll"));
                        break;
                }
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    catalog.Catalogs.Add(new DirectoryCatalog(AppContext.BaseDirectory, "SilverAudioPlayer.Sheep.*.dll"));
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    catalog.Catalogs.Add(new DirectoryCatalog(AppContext.BaseDirectory, "SilverAudioPlayer.Unix.*.dll"));
                }
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

        public INaudioWaveStreamWrapper? GetWrapper(WrappedStream stream)
        {
            return GetWrapper().FirstOrDefault(x => x.CanPlay(stream));
        }

        public bool HasWrapper(WrappedStream stream)
        {
            return GetWrapper().Any(x =>
            {
                Debug.WriteLine(x.GetType().FullName);
                var e = x.CanPlay(stream);
                Debug.WriteLine(e);
                return e;
            });
        }

        internal WaveStream? GetStream(WrappedStream stream)
        {
            return GetWrapper().FirstOrDefault(x => x.CanPlay(stream))?.GetStream(stream);
        }
    }
}
using System.ComponentModel;
using System.Composition;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using ATL;
using Version = System.Version;
using Microsoft.Extensions.Logging.Abstractions;
using Silver.Composition;
using SilverAudioPlayer.Any.MetadataSource.File;
using SilverAudioPlayer.Any.MetadataSource.Z440AtlCore;
using SilverAudioPlayer.Any.SilverJuke;
using SilverAudioPlayer.Shared;
using SilverAudioPlayer.Shared.Metadata;
using SilverMagicBytes;
using Tmds.DBus;

namespace SilverAudioPlayer.Tests;

public class OptimizationAndHostTests
{
    private static readonly byte[] MinimalPng =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
        0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
        0x89, 0x00, 0x00, 0x00, 0x0A, 0x49, 0x44, 0x41,
        0x54, 0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00,
        0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00,
        0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE,
        0x42, 0x60, 0x82
    ];

    private sealed class FakeSilverJukeListener : IMusicStatusInterfaceListener, IPlayStreamProviderListener
    {
        public int PlayCalls { get; private set; }
        public int PauseCalls { get; private set; }
        public int PlayPauseCalls { get; private set; }
        public int NextCalls { get; private set; }
        public int PreviousCalls { get; private set; }
        public int StopCalls { get; private set; }
        public double? LastSetPosition { get; private set; }
        public List<string> ProcessedFiles { get; } = [];
        public Song? Track { get; set; }
        public PlaybackState State { get; set; } = PlaybackState.Playing;
        public double DurationSeconds { get; set; } = 180.5;
        public double PositionSeconds { get; set; } = 42.25;
        public string LyricsText { get; set; } = "Test lyrics";

        public string Name => "FakeListener";
        public string Description => "Fake";
        public WrappedStream? Icon => null;
        public Version? Version => new(1, 0, 0, 0);
        public string Licenses => "MIT";
        public List<Tuple<Uri, URLType>>? Links => null;

        public event EventHandler<Song?>? TrackChangedNotification { add { } remove { } }
        public event EventHandler<PlaybackState>? PlayerStateChanged { add { } remove { } }
        public event EventHandler<TimeSpan>? PositionChanged { add { } remove { } }

        public IPicture? GetBestRepresentation(IReadOnlyList<IPicture>? pictures, PictureType type = PictureType.Front)
            => pictures?.FirstOrDefault(p => p.PicType == type) ?? pictures?.FirstOrDefault();

        public void Play() => PlayCalls++;
        public void Pause() => PauseCalls++;
        public void PlayPause() => PlayPauseCalls++;
        public void Stop() => StopCalls++;
        public void Next() => NextCalls++;
        public void Previous() => PreviousCalls++;
        public void SetVolume(byte volume) { }
        public byte GetVolume() => 80;
        public Song? GetCurrentTrack() => Track;
        public double GetDuration() => DurationSeconds;
        public void SetPosition(double position) => LastSetPosition = position;
        public double GetPosition() => PositionSeconds;
        public PlaybackState GetState() => State;
        public RepeatState GetRepeat() => RepeatState.None;
        public void SetRepeat(RepeatState state) { }
        public bool GetShuffle() => false;
        public void SetShuffle(bool shuffle) { }
        public void SetRating(byte rating) { }
        public string GetLyrics() => LyricsText;
        public Task<IMetadata?>? GetMetadataAsync(WrappedStream stream) => null;
        public Task<IReadOnlyList<MimeType>> SupportedMimeTypes() => Task.FromResult<IReadOnlyList<MimeType>>([KnownMimes.MP3Mime]);
        public IDictionary<string, string>? GetEntriesForCurrentTrack() => Track?.Metadata?.Entries;
        public Task<List<Song>?> GetQueue() => Task.FromResult<List<Song>?>(Track is null ? [] : [Track]);
        public void AddTimedEvent(TimedEvent timedEvent) { }
        public void RemoveTimedEvent(TimedEvent timedEvent) { }
        public void LoadSong(WrappedStream s) { }
        public void LoadSong(WrappedStream s, IMetadata additionalMetadata) { }
        public void ProcessFiles(IEnumerable<string> files) => ProcessedFiles.AddRange(files);
        public void LoadSongs(IEnumerable<WrappedStream> streams) { }
        public void LoadSongs(IEnumerable<Tuple<WrappedStream, IMetadata?>> streams) { }
        public IEnumerable<string> FilterFiles(IEnumerable<string> files) => files;
    }

    private sealed class TestMetadata : IMetadata
    {
        public string? Title { get; init; }
        public string? Artist { get; init; }
        public string? Album { get; init; }
        public string? Genre => null;
        public int? Year => null;
        public ulong? Bitrate => null;
        public ulong? SampleRate => null;
        public uint? Channels => null;
        public int? TrackNumber => null;
        public int? DiscNumber => null;
        public string[]? Comments => null;
        public double? Duration => null;
        public string? Lyrics => null;
        public IList<LyricPhrase>? SyncedLyrics { get; set; }
        public IReadOnlyList<IPicture>? Pictures { get; init; }
        public IDictionary<string, string>? Entries => null;

        public void Dispose()
        {
            if (Pictures != null)
            {
                foreach (var p in Pictures) p.Dispose();
            }
        }
    }

    public sealed class TestConfig : INotifyPropertyChanged, ICanBeToldThatAPartOfMeIsChanged
    {
        public string? Value { get; set; } = "initial";
        public bool AllowedToRead { get; set; } = true;
        public event PropertyChangedEventHandler? PropertyChanged;

        void ICanBeToldThatAPartOfMeIsChanged.PropertyChanged(object e, PropertyChangedEventArgs a)
            => PropertyChanged?.Invoke(e, a);
    }

    private static int GetFreeTcpPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    [Fact]
    public void PlatformLogicHelper_CollectiblePluginAssembly_UnloadsCleanlyAfterReferencesDropped()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "sap_mef_unload_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var dllPath = Path.Combine(tempDir, $"DynamicPlugin_{Guid.NewGuid():N}.dll");

        try
        {
            var ab = new PersistedAssemblyBuilder(
                new AssemblyName(Path.GetFileNameWithoutExtension(dllPath)),
                typeof(object).Assembly);
            var mb = ab.DefineDynamicModule("DynamicPluginModule");
            var tb = mb.DefineType("DynamicPlugin.ExportedTestPlugin", TypeAttributes.Public | TypeAttributes.Class);
            var exportCtor = typeof(ExportAttribute).GetConstructor([typeof(Type)])!;
            tb.SetCustomAttribute(new CustomAttributeBuilder(exportCtor, [typeof(object)]));
            tb.DefineDefaultConstructor(MethodAttributes.Public);
            tb.CreateType();
            ab.Save(dllPath);

            var alcRef = LoadActivateAndUnload(dllPath);
            Assert.NotNull(alcRef);

            for (var i = 0; i < 20 && alcRef.IsAlive; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

            Assert.False(alcRef.IsAlive);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try
                {
                    Directory.Delete(tempDir, recursive: true);
                }
                catch
                {
                    // best-effort temp cleanup
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference? LoadActivateAndUnload(string dllPath)
    {
        var wrapped = PlatformLogicHelper.LoadAssembly(dllPath);
        Assert.True(wrapped.IsCollectible);

        var instances = Helper.GetFromAssembliesWrapped<object>([wrapped]).ToList();
        Assert.Single(instances);
        Assert.Equal("DynamicPlugin.ExportedTestPlugin", instances[0].GetType().FullName);
        instances.Clear();

        return PlatformLogicHelper.UnloadAssembly(wrapped);
    }

    [Fact]
    public async Task SilverJuke_GenHttpHost_ServesEndpointsAndEnforcesGuards()
    {
        var port = GetFreeTcpPort();
        var listener = new FakeSilverJukeListener();
        var coverStream = new WrappedSharedMemoryStream(MinimalPng, KnownMimes.PngMime, copy: true);
        using var song = new Song(new WrappedSharedMemoryStream([1, 2, 3, 4], KnownMimes.MP3Mime), "test.mp3")
        {
            Metadata = new MetadataCombo([
                new TestMetadata
                {
                    Title = "GenHTTP Song",
                    Artist = "Tester",
                    Pictures = [new Picture(coverStream, PictureType.Front)]
                }
            ], [])
        };
        listener.Track = song;

        using var juke = new SilverJuke();
        var cfg = (JukeConfig)juke.ConfigObject.Value;
        cfg.UseKestrel = false;
        cfg.Port = port;
        cfg.ReadOnly = false;
        cfg.AllowLoading = true;
        cfg.UseSlopUI = true;
        cfg.UseSwagger = false;

        juke.StartIPC(listener);
        try
        {
            Assert.True(juke.IsStarted);
            using var http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };

            var stateRes = await http.GetAsync("/state");
            var stateBody = await stateRes.Content.ReadAsStringAsync();
            Assert.True(stateRes.StatusCode == HttpStatusCode.OK, $"Expected 200 OK, got {(int)stateRes.StatusCode} ({stateRes.StatusCode}): {stateBody}");

            var trackJson = await http.GetStringAsync("/track");
            Assert.Contains("GenHTTP Song", trackJson);
            Assert.Contains("\"metadata\":", trackJson);

            var duration = await http.GetFromJsonAsync<double>("/duration");
            Assert.Equal(180.5, duration);

            var pos = await http.GetFromJsonAsync<double>("/position");
            Assert.Equal(42.25, pos);

            Assert.Equal("Test lyrics", (await http.GetStringAsync("/lyrics")).Trim('"'));
            Assert.False(await http.GetFromJsonAsync<bool>("/checkifreadonly"));

            var uiHtml = await http.GetStringAsync("/");
            Assert.Contains("SilverAudioPlayer", uiHtml, StringComparison.OrdinalIgnoreCase);

            var js = await http.GetStringAsync("/SilverJukeWebUi.js");
            Assert.Contains("/position?position=", js);

            Assert.Equal(HttpStatusCode.OK, (await http.GetAsync("/play")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await http.GetAsync("/pause")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await http.GetAsync("/playpause")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await http.GetAsync("/next")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await http.GetAsync("/previous")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await http.GetAsync("/stop")).StatusCode);
            Assert.Equal(1, listener.PlayCalls);
            Assert.Equal(1, listener.PauseCalls);
            Assert.Equal(1, listener.PlayPauseCalls);
            Assert.Equal(1, listener.NextCalls);
            Assert.Equal(1, listener.PreviousCalls);
            Assert.Equal(1, listener.StopCalls);

            // Query-string /position used by SilverJukeWebUi.js
            var queryPosRes = await http.PostAsync("/position?position=12.5", content: null);
            Assert.Equal(HttpStatusCode.OK, queryPosRes.StatusCode);
            Assert.Equal(12.5, listener.LastSetPosition);

            // JSON body /position used by REST/OpenAPI callers
            var bodyPosRes = await http.PostAsJsonAsync("/position", 27.75);
            Assert.Equal(HttpStatusCode.OK, bodyPosRes.StatusCode);
            Assert.Equal(27.75, listener.LastSetPosition);

            // /albumart with embedded PNG cover
            var artRes = await http.GetAsync("/albumart");
            Assert.Equal(HttpStatusCode.OK, artRes.StatusCode);
            var artBytes = await artRes.Content.ReadAsByteArrayAsync();
            Assert.Equal(MinimalPng, artBytes);

            // /albumart when track has no art -> 204 NoContent
            listener.Track = null;
            var noArtRes = await http.GetAsync("/albumart");
            Assert.Equal(HttpStatusCode.NoContent, noArtRes.StatusCode);

            // /loadfiles on loopback
            var loadRes = await http.PostAsJsonAsync("/loadfiles", new[] { "/tmp/a.mp3", "/tmp/b.flac" });
            Assert.Equal(HttpStatusCode.OK, loadRes.StatusCode);
            Assert.Equal(["/tmp/a.mp3", "/tmp/b.flac"], listener.ProcessedFiles);

            // ReadOnly enforcement
            cfg.ReadOnly = true;
            Assert.Equal(HttpStatusCode.Forbidden, (await http.GetAsync("/play")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await http.PostAsync("/position?position=99", null)).StatusCode);
            Assert.Equal(1, listener.PlayCalls);
            Assert.Equal(27.75, listener.LastSetPosition);
        }
        finally
        {
            juke.StopIPC(listener);
            Assert.False(juke.IsStarted);
        }
    }

    [Fact]
    public async Task SilverJuke_KestrelHost_ServesStateAndSetPosition()
    {
        var port = GetFreeTcpPort();
        var listener = new FakeSilverJukeListener();

        using var juke = new SilverJuke();
        var cfg = (JukeConfig)juke.ConfigObject.Value;
        cfg.UseKestrel = true;
        cfg.Port = port;
        cfg.ReadOnly = false;
        cfg.AllowLoading = false;
        cfg.UseSlopUI = false;
        cfg.UseSwagger = false;

        juke.StartIPC(listener);
        try
        {
            Assert.True(juke.IsStarted);
            using var http = new HttpClient { BaseAddress = new Uri($"http://localhost:{port}") };

            var stateRes = await http.GetAsync("/state");
            Assert.Equal(HttpStatusCode.OK, stateRes.StatusCode);

            var queryPosRes = await http.PostAsync("/position?position=7.5", content: null);
            Assert.Equal(HttpStatusCode.OK, queryPosRes.StatusCode);
            Assert.Equal(7.5, listener.LastSetPosition);

            var jsonPosRes = await http.PostAsJsonAsync("/position", 18.25);
            Assert.Equal(HttpStatusCode.OK, jsonPosRes.StatusCode);
            Assert.Equal(18.25, listener.LastSetPosition);
        }
        finally
        {
            juke.StopIPC(listener);
            Assert.False(juke.IsStarted);
        }
    }

    [Fact]
    public void AtlCoreMetadata_ExtractsTagsAndDisposesPictureReliances()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"sap_atl_{Guid.NewGuid():N}.mp3");
        try
        {
            // Minimal MPEG1 Layer 3 frame so ATL recognizes it as MP3 audio
            var frame = new byte[417];
            frame[0] = 0xFF;
            frame[1] = 0xFB;
            frame[2] = 0x90;
            frame[3] = 0x00;
            File.WriteAllBytes(tempFile, frame);

            var writerTrack = new Track(tempFile)
            {
                Title = "AtlTitle",
                Artist = "AtlArtist",
                Album = "AtlAlbum"
            };
            writerTrack.EmbeddedPictures.Add(PictureInfo.fromBinaryData(MinimalPng, PictureInfo.PIC_TYPE.Front));
            writerTrack.Save();

            var provider = new AtlCoreFileMetadataProvider(NullLogger<AtlCoreFileMetadataProvider>.Instance);
            using var wfs = new WrappedFileStream(tempFile);
            Assert.True(provider.CanGetMetadata(wfs));

            var expectedHash = HashHelper.HashByteArray(MinimalPng);
            var baselineReliances = SharedMemoryStreamPoolInstance.Instance.Reliances.Values
                .Sum(l => l.Count);

            var meta = new AtlCoreMetadata(wfs);
            Assert.Equal("AtlTitle", meta.Title);
            Assert.Equal("AtlArtist", meta.Artist);
            Assert.Equal("AtlAlbum", meta.Album);
            Assert.NotNull(meta.Pictures);
            Assert.Single(meta.Pictures);

            var pool = (SharedMemoryStreamPool)SharedMemoryStreamPoolInstance.Instance;
            Assert.Contains(pool.SharedStreams, s => s.Hash == expectedHash);

            meta.Dispose();
            var afterDisposeReliances = SharedMemoryStreamPoolInstance.Instance.Reliances.Values
                .Sum(l => l.Count);
            Assert.Equal(baselineReliances, afterDisposeReliances);
            Assert.DoesNotContain(pool.SharedStreams, s => s.Hash == expectedHash);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void FileMetadata_ReadsSiblingLyricsAndCoverWithoutLeakingReliances()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "sap_filemeta_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var audioPath = Path.Combine(tempDir, "sample.mp3");
            File.WriteAllBytes(audioPath, [0xFF, 0xFB, 0x90, 0x00, 0x00]);
            File.WriteAllText(Path.Combine(tempDir, "sample.lrc"), "[00:01.00]Hello world\n", Encoding.UTF8);
            File.WriteAllText(Path.Combine(tempDir, "sample.txt"), "Plain lyrics", Encoding.UTF8);
            File.WriteAllBytes(Path.Combine(tempDir, "cover.png"), MinimalPng);
            File.WriteAllText(Path.Combine(tempDir, "cover_notes.txt"), "not an image");

            var pool = (SharedMemoryStreamPool)SharedMemoryStreamPoolInstance.Instance;
            var expectedHash = HashHelper.HashByteArray(MinimalPng);

            using var wfs = new WrappedFileStream(audioPath);
            using var fm = new FileMetadata(wfs);
            Assert.Equal("Plain lyrics", fm.Lyrics);
            Assert.NotNull(fm.SyncedLyrics);
            Assert.Single(fm.SyncedLyrics);
            Assert.NotNull(fm.Pictures);
            Assert.Single(fm.Pictures);
            Assert.Contains(pool.SharedStreams, s => s.Hash == expectedHash);

            fm.Dispose();
            Assert.DoesNotContain(pool.SharedStreams, s => s.Hash == expectedHash);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void SharedMemoryStreamPool_And_SharedPool_DeduplicateAndReleaseInO1()
    {
        var pool = new SharedMemoryStreamPool();
        var (s1, r1) = pool.AcquireFromByteArray(MinimalPng, copy: false);
        var (s2, r2) = pool.AcquireFromByteArray(MinimalPng.ToArray(), copy: true);

        Assert.Same(s1, s2);
        Assert.Single(pool.SharedStreams);

        r1.Dispose();
        Assert.Single(pool.SharedStreams);

        r2.Dispose();
        Assert.Empty(pool.SharedStreams);
        Assert.Empty(pool.Reliances);

        var objPool = new SharedPool<MemoryStream>();
        using var ms = new MemoryStream([1, 2, 3]);
        var (o1, or1) = objPool.AcquireOrAdd(ms, "k1");
        var (o2, or2) = objPool.AcquireOrAdd(new MemoryStream([4]), "k1");
        Assert.Same(o1, o2);
        Assert.True(objPool.ContainsHash("k1"));

        or1.Dispose();
        Assert.True(objPool.ContainsHash("k1"));
        or2.Dispose();
        Assert.False(objPool.ContainsHash("k1"));
        Assert.False(ms.CanRead); // disposed when last reliance removed
    }

    [Fact]
    public void CommentXmlConfigReaderNotifyWhenChanged_SharesDirectoryWatcherAcrossInstancesAndDisposesCleanly()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "sap_cfgwatch_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var path1 = Path.Combine(tempDir, "cfg1.xml");
            var path2 = Path.Combine(tempDir, "cfg2.xml");

            using var reader1 = new CommentXmlConfigReaderNotifyWhenChanged<TestConfig>();
            using var reader2 = new CommentXmlConfigReaderNotifyWhenChanged<TestConfig>();

            reader1.Write(new TestConfig { Value = "one" }, path1);
            reader2.Write(new TestConfig { Value = "two" }, path2);

            var loaded1 = reader1.Read(path1);
            var loaded2 = reader2.Read(path2);

            Assert.Equal("one", loaded1?.Value);
            Assert.Equal("two", loaded2?.Value);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void LinuxWakeLock_And_TmdsDBusPlugin_LoadInNonCollectibleDefaultContext()
    {
        var prevWatchers = PlatformLogicHelper.EnableDirectoryWatchers;
        PlatformLogicHelper.EnableDirectoryWatchers = false;
        try
        {
            List<WrappedDirectoryCatalog> cats = [];
            var loaded = PlatformLogicHelper.AssembliesFrom(
                AppContext.BaseDirectory,
                "SilverAudioPlayer.Linux.PlatformHelper.Linux.dll",
                ref cats).ToList();

            var wrapped = Assert.Single(loaded);
            Assert.False(wrapped.IsCollectible);

            var providers = Helper.GetFromAssembly<IWakeLockProvider>((Assembly)wrapped).ToList();
            Assert.NotEmpty(providers);

            var screenSaverType = ((Assembly)wrapped).GetType("ScreenSaver.DBus.IScreenSaver", throwOnError: true)!;
            using var conn = new Connection("unix:path=/tmp/nonexistent-dbus");
            var createProxyMethod = typeof(Connection)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Single(m => m.Name == nameof(Connection.CreateProxy) && m.IsGenericMethodDefinition && m.GetParameters().Length == 2);
            var proxy = createProxyMethod.MakeGenericMethod(screenSaverType)
                .Invoke(conn, ["org.freedesktop.ScreenSaver", new ObjectPath("/org/freedesktop/ScreenSaver")]);
            Assert.NotNull(proxy);
        }
        finally
        {
            PlatformLogicHelper.EnableDirectoryWatchers = prevWatchers;
        }
    }

    [Fact]
    public void ExternalTmdsDBusPlugin_DetectedBeforeCreatingCollectibleAlc()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "sap_dbus_plugin_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var dbusDllPath = Path.Combine(tempDir, "DynamicDBusTestPlugin_" + Guid.NewGuid().ToString("N") + ".dll");
            var asmName = new AssemblyName(Path.GetFileNameWithoutExtension(dbusDllPath));
            var ab = new PersistedAssemblyBuilder(asmName, typeof(object).Assembly);
            var mb = ab.DefineDynamicModule(asmName.Name + ".dll");

            var dbusAttrCtor = typeof(DBusInterfaceAttribute).GetConstructor([typeof(string)])!;
            var ifaceBuilder = mb.DefineType(
                "ITestDynamicDBus",
                TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract,
                null,
                [typeof(IDBusObject)]);
            ifaceBuilder.SetCustomAttribute(new CustomAttributeBuilder(dbusAttrCtor, ["org.silveraudioplayer.TestEmit"]));
            ifaceBuilder.CreateType();
            ab.Save(dbusDllPath);

            var plainDllPath = Path.Combine(tempDir, "PlainTestPlugin_" + Guid.NewGuid().ToString("N") + ".dll");
            var plainName = new AssemblyName(Path.GetFileNameWithoutExtension(plainDllPath));
            var plainAb = new PersistedAssemblyBuilder(plainName, typeof(object).Assembly);
            var plainMb = plainAb.DefineDynamicModule(plainName.Name + ".dll");
            plainMb.DefineType("PlainPlugin", TypeAttributes.Public).CreateType();
            plainAb.Save(plainDllPath);

            Assert.True(PlatformLogicHelper.ReferencesNonCollectibleEmitLibrary(dbusDllPath));
            Assert.False(PlatformLogicHelper.ReferencesNonCollectibleEmitLibrary(plainDllPath));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}

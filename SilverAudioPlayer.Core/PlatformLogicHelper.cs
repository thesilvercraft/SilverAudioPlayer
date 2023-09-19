using System.Composition;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using SilverAudioPlayer.Shared;

namespace SilverAudioPlayer.Core;

public class WrappedAssembly : IDisposable
{
    UnloadableAssemblyLoadContext alc = new();
    private Assembly Assembly { get; set; }
    public string Path { get; set; }

    public WrappedAssembly(string path)
    {
        Path = path;
        Assembly = alc.LoadFromAssemblyPath(path);
    }

    public static implicit operator Assembly(WrappedAssembly d) => d.Assembly;

    public void Dispose()
    {
        if (alc.IsCollectible)
        {
            alc.Unload();
        }

        GC.SuppressFinalize(this);
    }
}

public class WrappedDirectoryCatalog : IDisposable
{
    private readonly string _path;
    private readonly string _filter;
    private FileSystemWatcher _watcher;

    public WrappedDirectoryCatalog(string path, string filter)
    {
        _path = path;
        _filter = filter;
        Debug.WriteLine(path + " " + filter);
        _watcher = new()
        {
            Path = path,
            EnableRaisingEvents = true,
            Filter = "*.dll",
            NotifyFilter = NotifyFilters.CreationTime
                           | NotifyFilters.DirectoryName
                           | NotifyFilters.LastWrite
                           | NotifyFilters.Size | NotifyFilters.FileName,
            IncludeSubdirectories = true
        };
        _watcher.Changed += (x, y) => { Change.Invoke(this, y.FullPath); };
        _watcher.Deleted += (x, y) => { Delete.Invoke(this, y.FullPath); };
    }

    public event EventHandler<string> Change;
    public event EventHandler<string> Delete;

    public void Dispose()
    {
        _watcher.Dispose();
        GC.SuppressFinalize(this);
    }
}

class UnloadableAssemblyLoadContext : AssemblyLoadContext
{
    public UnloadableAssemblyLoadContext() : base() //isCollectible: true
    {
    }

    protected override Assembly? Load(AssemblyName name)
    {
        return null;
    }
}

public static class Helper<T>
{
    public static IEnumerable<T> GetFromAssembly(Assembly assembly) =>
        from type in assembly.GetTypes()
        where typeof(T).IsAssignableFrom(type) &&
              type.GetCustomAttributes().Any(x => x is ExportAttribute)
        select type.GetConstructor(Type.EmptyTypes)
        into constructor
        where constructor != null
        select (T)constructor.Invoke(null)
        into result
        where result != null
        select result;

    public static IEnumerable<T> GetFromAssemblies(IEnumerable<Assembly> assemblies) =>
        assemblies.SelectMany(GetFromAssembly);

    public static IEnumerable<T> GetFromAssemblies(IEnumerable<WrappedAssembly> assemblies) =>
        assemblies.SelectMany(a => GetFromAssembly(a));
}

public static class PlatformLogicHelper
{
    static List<WrappedDirectoryCatalog> catalogs = new();
    static List<WrappedAssembly> assemblies = new();
    public static IEnumerable<T> GetTypeAll<T>()
    {
        return Helper<T>.GetFromAssemblies(assemblies);
    }
    public static T GetTypeOne<T>()
    {
       return Helper<T>.GetFromAssemblies(assemblies).First();
    }
    public static void SatisfyImports(ILogicImportable a)
    {
        RefreshCatalogsAndAssemblies();
        a.MemoryProvider = Helper<IWillProvideMemory>.GetFromAssemblies(assemblies).First();
        a.MetadataProviders = Helper<IMetadataProvider>.GetFromAssemblies(assemblies).ToList();
        a.PlayProviders = Helper<IPlayProvider>.GetFromAssemblies(assemblies).ToList();
        a.MusicStatusInterfaces = Helper<IMusicStatusInterface>.GetFromAssemblies(assemblies).ToList();
        a.SyncPlugins = Helper<ISyncPlugin>.GetFromAssemblies(assemblies).ToList();
        a.WakeLockInterfaces = Helper<IWakeLockProvider>.GetFromAssemblies(assemblies).ToList();
        a.PlayStreamProviders = Helper<IPlayStreamProvider>.GetFromAssemblies(assemblies).ToList();
        foreach (var catalog in catalogs)
        {
            catalog.Change += (sender, s) =>
            {
                var a = assemblies.First(x => x.Path == s);
                a.Dispose();
            };
            catalog.Delete += (sender, s) =>
            {
                var a = assemblies.First(x => x.Path == s);
                a.Dispose();
            };
        }
    }

    public static void RefreshCatalogsAndAssemblies()
    {
        if (assemblies.Count == 0)
        {
            assemblies = new();
            catalogs = new();
            LoadAssemblies(ref assemblies, ref catalogs);
        }
      
    }


    //TODO https://learn.microsoft.com/en-us/dotnet/standard/assembly/unloadability
    public static IEnumerable<WrappedAssembly> AssembliesFrom(string path, string filter,
        ref List<WrappedDirectoryCatalog> catalogs)
    {
        catalogs.Add(new WrappedDirectoryCatalog(path, filter));
        return Directory.GetFiles(path, filter).Select(path2 => new WrappedAssembly(path2));
    }

    public static void LoadAssemblies(ref List<WrappedAssembly> assemblies,
        ref List<WrappedDirectoryCatalog> wrappedDirectoryCatalogs)
    {
        var ExtensionsExists = Directory.Exists(Path.Combine(AppContext.BaseDirectory, "Extensions"));
        if (OperatingSystem.IsWindows())
        {
            if (ExtensionsExists)
                assemblies.AddRange(AssembliesFrom(Path.Combine(AppContext.BaseDirectory, "Extensions"),
                    "SilverAudioPlayer.Windows.*.dll", ref wrappedDirectoryCatalogs));
            assemblies.AddRange(AssembliesFrom(AppContext.BaseDirectory, "SilverAudioPlayer.Windows.*.dll",
                ref wrappedDirectoryCatalogs));
            if (OperatingSystem.IsWindowsVersionAtLeast(10))
            {
                if (ExtensionsExists)
                    assemblies.AddRange(AssembliesFrom(Path.Combine(AppContext.BaseDirectory, "Extensions"),
                        "SilverAudioPlayer.Windows10.*.dll", ref wrappedDirectoryCatalogs));
                assemblies.AddRange(AssembliesFrom(AppContext.BaseDirectory, "SilverAudioPlayer.Windows10.*.dll",
                    ref wrappedDirectoryCatalogs));
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            if (ExtensionsExists)
                assemblies.AddRange(AssembliesFrom(Path.Combine(AppContext.BaseDirectory, "Extensions"),
                    "SilverAudioPlayer.Linux.*.dll", ref wrappedDirectoryCatalogs));
            assemblies.AddRange(AssembliesFrom(AppContext.BaseDirectory, "SilverAudioPlayer.Linux.*.dll",
                ref wrappedDirectoryCatalogs));
        }

        if (ExtensionsExists)
            assemblies.AddRange(AssembliesFrom(Path.Combine(AppContext.BaseDirectory, "Extensions"),
                "SilverAudioPlayer.Any.*.dll", ref wrappedDirectoryCatalogs));
        assemblies.AddRange(AssembliesFrom(AppContext.BaseDirectory, "SilverAudioPlayer.Any.*.dll",
            ref wrappedDirectoryCatalogs));
    }
}
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;

namespace Silver.Composition;

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

public static class Helper
{
    public static IEnumerable<T> GetFromAssembly<T>(Assembly assembly) =>
        assembly.GetTypes()
            .Where(type =>
                typeof(T).IsAssignableFrom(type) && type.GetCustomAttributes().Any(x =>
                    x is System.Composition.ExportAttribute or System.ComponentModel.Composition.ExportAttribute))
            .Select(type => type.GetConstructor(Type.EmptyTypes))
            .Where(constructor => constructor != null)
            .Select(constructor => (T)constructor.Invoke(null))
            .Where(result => result != null);

    public static IEnumerable<T> GetFromAssemblies<T>(IEnumerable<Assembly> assemblies) =>
        assemblies.SelectMany(GetFromAssembly<T>);

    public static IEnumerable<T> GetFromAssembliesWrapped<T>(IEnumerable<WrappedAssembly> assemblies) =>
        assemblies.SelectMany(a => GetFromAssembly<T>(a));
}

public static class PlatformLogicHelper
{
    static List<WrappedDirectoryCatalog> catalogs = [];
    static List<WrappedAssembly> assemblies = [];

    public static IEnumerable<T> GetTypeAll<T>()
    {
        return Helper.GetFromAssembliesWrapped<T>(assemblies);
    }

    public static T GetTypeOne<T>()
    {
        return Helper.GetFromAssembliesWrapped<T>(assemblies).First();
    }

    public static void SatisfyImports(object a, bool tolist = true)
    {
        RefreshCatalogsAndAssemblies();
        foreach (var field in a.GetType().GetProperties().Where(field => field.GetCustomAttributes().Any(x =>
                     x is System.Composition.ImportManyAttribute
                         or System.ComponentModel.Composition.ImportManyAttribute)))
        {
            var res = typeof(Helper).GetMethod("GetFromAssembliesWrapped")
                ?.MakeGenericMethod(field.PropertyType.GetGenericArguments()[0])
                .Invoke(null, new[] { assemblies });
            if (field.PropertyType.IsInstanceOfType(res))
            {
                if (tolist)
                {
                    field.SetValue(a,
                        typeof(List<>).MakeGenericType(field.PropertyType.GetGenericArguments()[0])
                            .GetConstructor(new[] { res.GetType() }).Invoke(new[] { res }));
                }
                else
                {
                    field.SetValue(a, res);
                }
            }
            else
            {
                Console.WriteLine(
                    $"Failed to set {field.Name}, its type {field.PropertyType} differs from {res.GetType()}");
            }
        }

        foreach (var field in a.GetType().GetProperties().Where(field => field.GetCustomAttributes().Any(x =>
                     x is System.Composition.ImportAttribute
                         or System.ComponentModel.Composition.ImportAttribute)))
        {
            var res = typeof(Helper).GetMethod("GetFromAssembliesWrapped")
                ?.MakeGenericMethod(field.PropertyType)
                .Invoke(null, new[] { assemblies });
            var resType = typeof(IEnumerable<>).MakeGenericType(field.PropertyType);
            if (resType.IsInstanceOfType(res))
            {
                if (res is not IEnumerable enumerable) continue;
                var current = enumerable.GetEnumerator();
                if (current.MoveNext())
                {
                    field.SetValue(a, current.Current);
                }
                else
                {
                    Console.WriteLine(
                        $"No matching object was found for {field.Name}.");
                }
            }
            else
            {
                Console.WriteLine(
                    $"Failed to set {field.Name}, its type {field.PropertyType} differs from {(res?.GetType()?.ToString() ?? "null type")}");
            }
        }

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
            assemblies = [];
            catalogs = [];
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
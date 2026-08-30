using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using Microsoft.Extensions.DependencyInjection;

namespace Silver.Composition;

public class WrappedAssembly : IDisposable
{
    UnloadableAssemblyLoadContext? alc;
    internal Assembly Assembly { get; set; }
    public string? Path { get; set; }

    internal WrappedAssembly(Assembly a)
    {
        Assembly = a;
        alc = null;
        Path = null;
    }

    public WrappedAssembly(string path)
    {
        Path = path;
        alc= new();
        Assembly = alc.LoadFromAssemblyPath(path);
    }

    public static implicit operator Assembly(WrappedAssembly d) => d.Assembly;

    public void Dispose()
    {
        if (alc != null && alc.IsCollectible)
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
        _watcher.Changed += (x, y) => { Change?.Invoke(this, y.FullPath); };
        _watcher.Deleted += (x, y) => { Delete?.Invoke(this, y.FullPath); };
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
    public static IEnumerable<T> GetFromAssembly<T>(Assembly assembly, IServiceProvider? serviceProvider = null) =>
        assembly.GetTypes()
            .Where(type =>
                typeof(T).IsAssignableFrom(type) &&
                type is { IsAbstract: false, IsInterface: false } &&
                type.GetCustomAttributes().Any(x =>
                    x is System.Composition.ExportAttribute or System.ComponentModel.Composition.ExportAttribute))
            .Select(type =>
            {
                if (serviceProvider != null)
                {
                    return (T)ActivatorUtilities.CreateInstance(serviceProvider, type);
                }

                var constructor = type.GetConstructor(Type.EmptyTypes);
                return constructor != null ? (T)constructor.Invoke(null) : default;
            })
            .Where(result => result != null)!;

    public static IEnumerable<T> GetFromAssemblies<T>(IEnumerable<Assembly> assemblies,
        IServiceProvider? serviceProvider = null) =>
        assemblies.SelectMany(a => GetFromAssembly<T>(a, serviceProvider));

    public static IEnumerable<T> GetFromAssembliesWrapped<T>(IEnumerable<WrappedAssembly> assemblies,
        IServiceProvider? serviceProvider = null) =>
        assemblies.SelectMany(a => GetFromAssembly<T>(a, serviceProvider));
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

    /// <summary>
    /// 
    /// </summary>
    /// <param name="a"></param>
    /// <param name="tolist"> Set tolist = false only if the properties on object a receiving [ImportMany] are typed as IEnumerable<T>.</param>
    public static void SatisfyImports(object a, IServiceProvider? serviceProvider = null, bool tolist = true)
    {
        RefreshCatalogsAndAssemblies();

        foreach (var field in a.GetType().GetProperties().Where(field => field.GetCustomAttributes().Any(x =>
                     x is System.Composition.ImportManyAttribute
                         or System.ComponentModel.Composition.ImportManyAttribute)))
        {
            var elementType = field.PropertyType.GetGenericArguments()[0];

            var res = typeof(Helper).GetMethod(nameof(Helper.GetFromAssembliesWrapped))
                ?.MakeGenericMethod(elementType)
                .Invoke(null, [assemblies, serviceProvider]);

            if (res != null)
            {
                if (tolist)
                {
                    var listType = typeof(List<>).MakeGenericType(elementType);
                    var listInstance = Activator.CreateInstance(listType, res);
                    field.SetValue(a, listInstance);
                }
                else
                {
                    field.SetValue(a, res);
                }
            }
        }

        foreach (var field in a.GetType().GetProperties().Where(field => field.GetCustomAttributes().Any(x =>
                     x is System.Composition.ImportAttribute
                         or System.ComponentModel.Composition.ImportAttribute)))
        {
            var res = typeof(Helper).GetMethod(nameof(Helper.GetFromAssembliesWrapped))
                ?.MakeGenericMethod(field.PropertyType)
                .Invoke(null, [assemblies, serviceProvider]);

            if (res is IEnumerable enumerable)
            {
                var enumerator = enumerable.GetEnumerator();
                if (enumerator.MoveNext())
                {
                    field.SetValue(a, enumerator.Current);
                }
            }
        }
    }

    /// For use to not load the same thing twice, you might not want to use this
    /// </summary>
    /// <param name="a">the already loaded assembly</param>
    public static void AddManualAssembly(Assembly a)
    {
        assemblies.Add(new WrappedAssembly(a));
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
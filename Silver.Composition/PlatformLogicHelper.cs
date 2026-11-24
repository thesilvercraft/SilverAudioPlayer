using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.Loader;
using Microsoft.Extensions.DependencyInjection;

namespace Silver.Composition;

internal readonly record struct ExportedTypeEntry(Type ImplementationType, Type?[] ContractTypes);

public class WrappedAssembly : IDisposable
{
    private UnloadableAssemblyLoadContext? _alc;
    private Assembly? _assembly;
    private ExportedTypeEntry[]? _exportedTypes;
    private readonly Lock _cacheLock = new();

    internal Assembly Assembly => _assembly ?? throw new ObjectDisposedException(nameof(WrappedAssembly));
    public string? Path { get; set; }
    public bool IsCollectible => _alc is { IsCollectible: true };
    public bool IsDisposed => _assembly is null;

    internal WrappedAssembly(Assembly a)
    {
        _assembly = a ?? throw new ArgumentNullException(nameof(a));
        _alc = null;
        Path = null;
    }

    public WrappedAssembly(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        var fullPath = System.IO.Path.GetFullPath(path);
        Path = fullPath;
        _alc = new UnloadableAssemblyLoadContext(fullPath);
        _assembly = _alc.LoadFromAssemblyPath(fullPath);
    }

    public static implicit operator Assembly(WrappedAssembly d) => d.Assembly;

    internal IEnumerable<Type> GetMatchingExportedTypes<T>()
    {
        var entries = GetOrBuildExportCache();
        var targetType = typeof(T);
        foreach (var entry in entries)
        {
            if (!targetType.IsAssignableFrom(entry.ImplementationType)) continue;
            if (entry.ContractTypes.Length == 0) continue;

            foreach (var contractType in entry.ContractTypes)
            {
                if (contractType is null || contractType == targetType || targetType.IsAssignableFrom(contractType))
                {
                    yield return entry.ImplementationType;
                    break;
                }
            }
        }
    }

    private ExportedTypeEntry[] GetOrBuildExportCache()
    {
        if (_exportedTypes is { } cached) return cached;

        lock (_cacheLock)
        {
            if (_exportedTypes is { } cachedInLock) return cachedInLock;
            var assembly = Assembly;
            _exportedTypes = BuildExportCache(assembly);
            return _exportedTypes;
        }
    }

    internal static ExportedTypeEntry[] BuildExportCache(Assembly assembly)
    {
        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types.Where(t => t is not null).ToArray()!;
        }

        List<ExportedTypeEntry> exports = [];
        foreach (var type in types)
        {
            if (type is not { IsAbstract: false, IsInterface: false }) continue;

            List<Type?>? contractTypes = null;
            foreach (var attr in type.GetCustomAttributes(inherit: false))
            {
                switch (attr)
                {
                    case System.Composition.ExportAttribute mef2:
                        contractTypes ??= [];
                        contractTypes.Add(mef2.ContractType);
                        break;
                    case System.ComponentModel.Composition.ExportAttribute mef1:
                        contractTypes ??= [];
                        contractTypes.Add(mef1.ContractType);
                        break;
                }
            }

            if (contractTypes is { Count: > 0 })
            {
                exports.Add(new ExportedTypeEntry(type, contractTypes.ToArray()));
            }
        }

        return exports.ToArray();
    }

    internal WeakReference? UnloadInternal()
    {
        WeakReference? alcRef = null;
        lock (_cacheLock)
        {
            _exportedTypes = null;
            _assembly = null;
            if (_alc is { } alc)
            {
                alcRef = new WeakReference(alc, trackResurrection: true);
                if (alc.IsCollectible)
                {
                    alc.Unload();
                }

                _alc = null;
            }
        }

        GC.SuppressFinalize(this);
        return alcRef;
    }

    public void Dispose()
    {
        _ = UnloadInternal();
    }
}

public class WrappedDirectoryCatalog : IDisposable
{
    private readonly string _path;
    private readonly string _filter;
    private readonly FileSystemWatcher _watcher;

    public WrappedDirectoryCatalog(string path, string filter)
    {
        _path = path;
        _filter = filter;
        Debug.WriteLine(path + " " + filter);
        _watcher = new()
        {
            Path = path,
            EnableRaisingEvents = true,
            Filter = filter,
            NotifyFilter = NotifyFilters.CreationTime
                           | NotifyFilters.DirectoryName
                           | NotifyFilters.LastWrite
                           | NotifyFilters.Size | NotifyFilters.FileName,
            IncludeSubdirectories = true
        };
        _watcher.Changed += (_, y) => { Change?.Invoke(this, y.FullPath); };
        _watcher.Deleted += (_, y) => { Delete?.Invoke(this, y.FullPath); };
    }

    public event EventHandler<string>? Change;
    public event EventHandler<string>? Delete;

    public void Dispose()
    {
        _watcher.Dispose();
        GC.SuppressFinalize(this);
    }
}

internal sealed class UnloadableAssemblyLoadContext(string pluginPath)
    : AssemblyLoadContext(Path.GetFileNameWithoutExtension(pluginPath), isCollectible: true)
{
    private readonly AssemblyDependencyResolver _resolver = new(pluginPath);

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        foreach (var loaded in Default.Assemblies)
        {
            if (string.Equals(loaded.GetName().Name, assemblyName.Name, StringComparison.OrdinalIgnoreCase))
            {
                return loaded;
            }
        }

        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath is not null)
        {
            // Keep host-directory dependencies in Default ALC so shared libraries aren't duplicated across ALCs;
            // plugin-local dependencies (e.g. inside Extensions/<plugin>/) stay inside this collectible ALC.
            var baseDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var resolvedDir = Path.GetDirectoryName(assemblyPath)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!string.Equals(resolvedDir, baseDir, StringComparison.OrdinalIgnoreCase))
            {
                return LoadFromAssemblyPath(assemblyPath);
            }
        }

        return null;
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return libraryPath is not null ? LoadUnmanagedDllFromPath(libraryPath) : IntPtr.Zero;
    }
}

public static class Helper
{
    private static T? InstantiateExport<T>(Type type, IServiceProvider? serviceProvider)
    {
        if (serviceProvider != null)
        {
            return (T)ActivatorUtilities.CreateInstance(serviceProvider, type);
        }

        var constructor = type.GetConstructor(Type.EmptyTypes);
        return constructor != null ? (T)constructor.Invoke(null) : default;
    }

    public static IEnumerable<T> GetFromAssembly<T>(Assembly assembly, IServiceProvider? serviceProvider = null)
    {
        var targetType = typeof(T);
        foreach (var entry in WrappedAssembly.BuildExportCache(assembly))
        {
            if (!targetType.IsAssignableFrom(entry.ImplementationType)) continue;
            if (!entry.ContractTypes.Any(c => c is null || c == targetType || targetType.IsAssignableFrom(c))) continue;
            if (InstantiateExport<T>(entry.ImplementationType, serviceProvider) is { } instance)
            {
                yield return instance;
            }
        }
    }

    internal static IEnumerable<T> GetFromWrappedAssembly<T>(WrappedAssembly assembly,
        IServiceProvider? serviceProvider = null)
    {
        if (assembly.IsDisposed) yield break;
        foreach (var type in assembly.GetMatchingExportedTypes<T>())
        {
            if (InstantiateExport<T>(type, serviceProvider) is { } instance)
            {
                yield return instance;
            }
        }
    }

    public static IEnumerable<T> GetFromAssemblies<T>(IEnumerable<Assembly> assemblies,
        IServiceProvider? serviceProvider = null) =>
        assemblies.SelectMany(a => GetFromAssembly<T>(a, serviceProvider));

    public static IEnumerable<T> GetFromAssembliesWrapped<T>(IEnumerable<WrappedAssembly> assemblies,
        IServiceProvider? serviceProvider = null) =>
        assemblies.SelectMany(a => GetFromWrappedAssembly<T>(a, serviceProvider));
}

public static class PlatformLogicHelper
{
    private static readonly Lock _syncLock = new();
    static List<WrappedDirectoryCatalog> catalogs = [];
    static List<WrappedAssembly> assemblies = [];

    /// <summary>
    /// Disabled by default to avoid allocating unused recursive FileSystemWatchers on startup.
    /// </summary>
    public static bool EnableDirectoryWatchers { get; set; }

    public static IEnumerable<T> GetTypeAll<T>()
    {
        List<WrappedAssembly> snapshot;
        lock (_syncLock)
        {
            snapshot = [.. assemblies];
        }

        return Helper.GetFromAssembliesWrapped<T>(snapshot).ToList();
    }

    public static T GetTypeOne<T>()
    {
        List<WrappedAssembly> snapshot;
        lock (_syncLock)
        {
            snapshot = [.. assemblies];
        }

        return Helper.GetFromAssembliesWrapped<T>(snapshot).First();
    }

    /// <summary>
    /// Populates [Import] and [ImportMany] properties on <paramref name="a"/> using discovered plugin assemblies.
    /// </summary>
    /// <param name="a">Target object with [Import]/[ImportMany] properties.</param>
    /// <param name="serviceProvider">Optional DI service provider used for constructor injection.</param>
    /// <param name="tolist">Set tolist = false only if the properties on object a receiving [ImportMany] are typed as IEnumerable&lt;T&gt;.</param>
    public static void SatisfyImports(object a, IServiceProvider? serviceProvider = null, bool tolist = true)
    {
        RefreshCatalogsAndAssemblies();

        List<WrappedAssembly> snapshot;
        lock (_syncLock)
        {
            snapshot = [.. assemblies];
        }

        var method = typeof(Helper).GetMethod(nameof(Helper.GetFromAssembliesWrapped));
        foreach (var field in a.GetType().GetProperties())
        {
            var attrs = field.GetCustomAttributes(inherit: true);
            var isImportMany = attrs.Any(x =>
                x is System.Composition.ImportManyAttribute
                    or System.ComponentModel.Composition.ImportManyAttribute);
            if (isImportMany)
            {
                var elementType = field.PropertyType.GetGenericArguments()[0];
                var res = method?.MakeGenericMethod(elementType)
                    .Invoke(null, [snapshot, serviceProvider]);

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

                continue;
            }

            var isImport = attrs.Any(x =>
                x is System.Composition.ImportAttribute
                    or System.ComponentModel.Composition.ImportAttribute);
            if (isImport)
            {
                var res = method?.MakeGenericMethod(field.PropertyType)
                    .Invoke(null, [snapshot, serviceProvider]);

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
    }

    /// <summary>
    /// Registers an already loaded assembly so it isn't loaded a second time.
    /// </summary>
    /// <param name="a">The already loaded assembly.</param>
    public static void AddManualAssembly(Assembly a)
    {
        ArgumentNullException.ThrowIfNull(a);
        lock (_syncLock)
        {
            if (assemblies.All(x => !x.IsDisposed && x.Assembly != a))
            {
                assemblies.Add(new WrappedAssembly(a));
            }
        }
    }

    public static WrappedAssembly LoadAssembly(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        var fullPath = Path.GetFullPath(path);
        var fileNameNoExt = Path.GetFileNameWithoutExtension(fullPath);

        lock (_syncLock)
        {
            if (assemblies.FirstOrDefault(x =>
                    !x.IsDisposed &&
                    (string.Equals(x.Path, fullPath, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(x.Assembly.GetName().Name, fileNameNoExt, StringComparison.OrdinalIgnoreCase))) is { } existing)
            {
                return existing;
            }

            var isBaseDir = IsInBaseDirectory(Path.GetDirectoryName(fullPath));
            var wrapped = LoadPluginAssemblyCore(fullPath, preferDefaultContext: isBaseDir);

            assemblies.Add(wrapped);
            return wrapped;
        }
    }

    private static bool IsInBaseDirectory(string? directoryPath)
    {
        if (string.IsNullOrEmpty(directoryPath)) return false;
        var baseDir = Path.GetFullPath(AppContext.BaseDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var fullDir = Path.GetFullPath(directoryPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(fullDir, baseDir, StringComparison.OrdinalIgnoreCase);
    }

    internal static bool ReferencesNonCollectibleEmitLibrary(string assemblyPath)
    {
        try
        {
            using var fs = File.OpenRead(assemblyPath);
            using var peReader = new PEReader(fs);
            if (!peReader.HasMetadata) return false;
            var metadataReader = peReader.GetMetadataReader();
            foreach (var handle in metadataReader.AssemblyReferences)
            {
                var reference = metadataReader.GetAssemblyReference(handle);
                var name = metadataReader.GetString(reference.Name);
                // Tmds.DBus emits dynamic proxy/object types into a non-collectible AssemblyBuilder ("Tmds.DBus.Emit"),
                // which CLR forbids from referencing types inside any collectible AssemblyLoadContext.
                if (string.Equals(name, "Tmds.DBus", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        catch
        {
            // Fall through to standard loading.
        }

        return false;
    }

    private static WrappedAssembly LoadPluginAssemblyCore(string fullPath, bool preferDefaultContext)
    {
        var fileNameNoExt = Path.GetFileNameWithoutExtension(fullPath);
        var alreadyInDefault = AssemblyLoadContext.Default.Assemblies.FirstOrDefault(a =>
            string.Equals(a.GetName().Name, fileNameNoExt, StringComparison.OrdinalIgnoreCase));
        if (alreadyInDefault is not null)
        {
            return new WrappedAssembly(alreadyInDefault) { Path = fullPath };
        }

        if (preferDefaultContext)
        {
            try
            {
                var defaultAsm = AssemblyLoadContext.Default.LoadFromAssemblyName(new AssemblyName(fileNameNoExt));
                return new WrappedAssembly(defaultAsm) { Path = fullPath };
            }
            catch (FileNotFoundException)
            {
                // Drop-in DLL not listed in the host deps.json; treat as an external plugin below.
            }
            catch (BadImageFormatException)
            {
                // Fall through below.
            }
        }

        if (ReferencesNonCollectibleEmitLibrary(fullPath))
        {
            var nonCollectibleAsm = AssemblyLoadContext.Default.LoadFromAssemblyPath(fullPath);
            return new WrappedAssembly(nonCollectibleAsm) { Path = fullPath };
        }

        return new WrappedAssembly(fullPath);
    }

    public static WeakReference? UnloadAssembly(WrappedAssembly wrappedAssembly)
    {
        ArgumentNullException.ThrowIfNull(wrappedAssembly);
        lock (_syncLock)
        {
            assemblies.Remove(wrappedAssembly);
        }

        return wrappedAssembly.UnloadInternal();
    }

    public static IReadOnlyList<WeakReference> UnloadAll()
    {
        List<WrappedAssembly> toUnload;
        List<WrappedDirectoryCatalog> oldCatalogs;
        lock (_syncLock)
        {
            toUnload = assemblies;
            oldCatalogs = catalogs;
            assemblies = [];
            catalogs = [];
        }

        foreach (var catalog in oldCatalogs)
        {
            catalog.Dispose();
        }

        List<WeakReference> alcRefs = [];
        foreach (var wrapped in toUnload)
        {
            if (wrapped.UnloadInternal() is { } wr)
            {
                alcRefs.Add(wr);
            }
        }

        return alcRefs;
    }

    public static void RefreshCatalogsAndAssemblies()
    {
        lock (_syncLock)
        {
            if (assemblies.Count == 0)
            {
                assemblies = [];
                catalogs = [];
                LoadAssemblies(ref assemblies, ref catalogs);
            }
        }
    }

    //TODO https://learn.microsoft.com/en-us/dotnet/standard/assembly/unloadability
    public static IEnumerable<WrappedAssembly> AssembliesFrom(string path, string filter,
        ref List<WrappedDirectoryCatalog> catalogs)
    {
        if (EnableDirectoryWatchers)
        {
            catalogs.Add(new WrappedDirectoryCatalog(path, filter));
        }

        if (!Directory.Exists(path)) return [];

        var preferDefaultContext = IsInBaseDirectory(path);
        List<WrappedAssembly> loaded = [];
        foreach (var file in Directory.GetFiles(path, filter))
        {
            var fullPath = Path.GetFullPath(file);
            loaded.Add(LoadPluginAssemblyCore(fullPath, preferDefaultContext));
        }

        return loaded;
    }

    public static void LoadAssemblies(ref List<WrappedAssembly> assemblies,
        ref List<WrappedDirectoryCatalog> wrappedDirectoryCatalogs)
    {
        var seenNames = new HashSet<string>(
            assemblies.Where(a => !a.IsDisposed).Select(a => a.Assembly.GetName().Name ?? string.Empty),
            StringComparer.OrdinalIgnoreCase);

        static void AddRangeUnique(List<WrappedAssembly> target, HashSet<string> seen, IEnumerable<WrappedAssembly> items)
        {
            foreach (var item in items)
            {
                var name = item.Assembly.GetName().Name ?? item.Path ?? string.Empty;
                if (seen.Add(name))
                {
                    target.Add(item);
                }
                else
                {
                    item.Dispose();
                }
            }
        }

        var extensionsDir = Path.Combine(AppContext.BaseDirectory, "Extensions");
        var extensionsExists = Directory.Exists(extensionsDir);
        if (OperatingSystem.IsWindows())
        {
            if (extensionsExists)
                AddRangeUnique(assemblies, seenNames, AssembliesFrom(extensionsDir, "SilverAudioPlayer.Windows.*.dll", ref wrappedDirectoryCatalogs));
            AddRangeUnique(assemblies, seenNames, AssembliesFrom(AppContext.BaseDirectory, "SilverAudioPlayer.Windows.*.dll",
                ref wrappedDirectoryCatalogs));
            if (OperatingSystem.IsWindowsVersionAtLeast(10))
            {
                if (extensionsExists)
                    AddRangeUnique(assemblies, seenNames, AssembliesFrom(extensionsDir, "SilverAudioPlayer.Windows10.*.dll", ref wrappedDirectoryCatalogs));
                AddRangeUnique(assemblies, seenNames, AssembliesFrom(AppContext.BaseDirectory, "SilverAudioPlayer.Windows10.*.dll",
                    ref wrappedDirectoryCatalogs));
            }
        }
        else if (OperatingSystem.IsLinux())
        {
            if (extensionsExists)
                AddRangeUnique(assemblies, seenNames, AssembliesFrom(extensionsDir, "SilverAudioPlayer.Linux.*.dll", ref wrappedDirectoryCatalogs));
            AddRangeUnique(assemblies, seenNames, AssembliesFrom(AppContext.BaseDirectory, "SilverAudioPlayer.Linux.*.dll",
                ref wrappedDirectoryCatalogs));
        }

        if (extensionsExists)
            AddRangeUnique(assemblies, seenNames, AssembliesFrom(extensionsDir, "SilverAudioPlayer.Any.*.dll", ref wrappedDirectoryCatalogs));
        AddRangeUnique(assemblies, seenNames, AssembliesFrom(AppContext.BaseDirectory, "SilverAudioPlayer.Any.*.dll",
            ref wrappedDirectoryCatalogs));
    }
}
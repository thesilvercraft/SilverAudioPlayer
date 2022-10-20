using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Text;
using System.Threading.Tasks;

namespace SilverAudioPlayer.Core
{
    public static class PlatformLogicHelper
    {
        public static IEnumerable<Assembly> AssembliesFrom(string path, string filter)
        {
            return (Directory.GetFiles(path, filter).Select(path2 => AssemblyLoadContext.Default.LoadFromAssemblyPath(path2)).Where(x => x != null));
        }
        [TimingAdvice]
        public static void LoadAssemblies(ref List<Assembly> assemblies)
        {
            bool ExtensionsExists = Directory.Exists(Path.Combine(AppContext.BaseDirectory, "Extensions"));
            if (OperatingSystem.IsWindows())
            {
                if (ExtensionsExists)
                {
                    assemblies.AddRange(AssembliesFrom(Path.Combine(AppContext.BaseDirectory, "Extensions"), "SilverAudioPlayer.Windows.*.dll"));
                }
                assemblies.AddRange(AssembliesFrom(AppContext.BaseDirectory, "SilverAudioPlayer.Windows.*.dll"));
                if (OperatingSystem.IsWindowsVersionAtLeast(10))
                {
                    if (ExtensionsExists)
                    {
                        assemblies.AddRange(AssembliesFrom(Path.Combine(AppContext.BaseDirectory, "Extensions"), "SilverAudioPlayer.Windows10.*.dll"));
                    }
                    assemblies.AddRange(AssembliesFrom(AppContext.BaseDirectory, "SilverAudioPlayer.Windows10.*.dll"));
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                if (ExtensionsExists)
                {
                    assemblies.AddRange(AssembliesFrom(Path.Combine(AppContext.BaseDirectory, "Extensions"), "SilverAudioPlayer.Linux.*.dll"));
                }
                assemblies.AddRange(AssembliesFrom(AppContext.BaseDirectory, "SilverAudioPlayer.Linux.*.dll"));
            }
            if (ExtensionsExists)
            {
                assemblies.AddRange(AssembliesFrom(Path.Combine(AppContext.BaseDirectory, "Extensions"), "SilverAudioPlayer.Any.*.dll"));
            }
            assemblies.AddRange(AssembliesFrom(AppContext.BaseDirectory, "SilverAudioPlayer.Any.*.dll"));
        }
    }
}

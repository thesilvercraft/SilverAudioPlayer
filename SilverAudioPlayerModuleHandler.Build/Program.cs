using System.Diagnostics;
using System.Xml.Serialization;
using SilverAudioPlayerModuleHandler.Build;
using File = System.IO.File;
using SilverAudioPlayerModuleHandler;
using Microsoft.Extensions.DependencyModel;
string Version = "7.0.0-dev";
//Q&A
//What the hell is this?
//This code is currently responsible for creating the SilverAudioPlayerModules.xml file which might be used in the future
//to aid in package development (maybe) to allow users to choose which plugins to install using their system package 
//manager
Dictionary<string, string> PackageNames = [];

/*foreach (var arg in args)
{
    switch (arg.ToLower())
    {
        case "gi" or "generateindex":*/

GenIndex();
/*  break;
case "bs" or "builddebian":*/
//BuildFirstPart(Debian2ndPart, "debian");
/*    break;
default:
    Console.WriteLine("Ayo bro, uh pass arguments and stuff\n gi \t OR \t generateindex \t - creates the index needed for other stuff\n bd \t OR \t builddebian \t - builds packages for debian in build/debian");
    break;
}
}
*/
//BuildFirstPart(Arch2ndPart, "arch");


return;


string? CreateAndFillNeededDirForPackageArch(string basePath, string pathBegin, Module mod)
{
    throw new NotImplementedException();
}

string GetParent(string path, int n) => n > 0 ? GetParent(Directory.GetParent(path).FullName, n - 1) : Directory.GetParent(path).FullName;
void Debian2ndPart(string parent, SilverAudioPlayerModulesInfo info, string output)
{
    var path = Path.Join(parent, "build", "debian");

    foreach (var module in info.Modules)
    {
        string? modpath = CreateAndFillNeededDirForPackageDebian(parent, path, module);
        if (!string.IsNullOrEmpty(modpath))
        {
            Process.Start("dpkg-deb", $"--build {modpath}");
        }
    }
    Console.WriteLine("Done????");
}
string GetPackageName(string input)
{
    if (PackageNames.TryGetValue(input, out string? value))
    {
        return value;
    }
    return PackageNames[input] = (input.StartsWith("SilverCraft") ? "" : "silvercraft-") + input.ToLower().Replace('.', '-');
}
string? CreateAndFillNeededDirForPackageDebian(string basePath, string pathBegin, Module mod)
{
    if (mod.Name.StartsWith("SilverAudioPlayer.Win"))
    {
        return null;
    }
    var path = Path.Join(pathBegin, GetPackageName(mod.Name));

    if (!Path.Exists(path))
    {
        Directory.CreateDirectory(path);
    }
    var debInfoDir = Path.Combine(path, "DEBIAN");
    Directory.CreateDirectory(debInfoDir);
    File.WriteAllText(Path.Combine(debInfoDir, "control"), $@"Package: {GetPackageName(mod.Name)}
Version: {Version}
Architecture: all
Essential: no
Priority: optional
Depends: dotnet-runtime-8.0{(mod.Dependencies.Count > 0 ? ',' : null)}{string.Join(',', mod.Dependencies.Select(x => GetPackageName(x)))} 
Maintainer: silverdiamond
Description: {mod.Name} for SilverAudioPlayer
");
    var opt = Path.Combine(path, "opt", "silveraudioplayer");
    Directory.CreateDirectory(opt);
    foreach (var file in mod.Files)
    {
        var filetoCopy = Path.Combine(basePath, file.Name);
        if (File.Exists(filetoCopy))
        {
            var dirandname = file.Name.Split("/net8.0/publish/")[1];
            if (file.LocationToPlaceLinux != null)
            {
                var realLocAndName = Path.Combine(path, file.LocationToPlaceLinux[1..]);
                var realLoc = Path.GetDirectoryName(realLocAndName);
                if (!string.IsNullOrWhiteSpace(realLoc))
                {
                    Directory.CreateDirectory(realLoc);
                }
                else
                {
                    Console.Error.WriteLine($"\x1b[41mFile {filetoCopy} has invalid loc specified.... Try regenerating after regenererating index");
                    return null;

                }
                File.Copy(filetoCopy, realLocAndName);

            }
            else
            {
                var dir = Path.GetDirectoryName(Path.Combine(opt, dirandname));
                if (!string.IsNullOrWhiteSpace(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                File.Copy(filetoCopy, Path.Combine(opt, dirandname));
            }
        }
        else
        {
            Console.Error.WriteLine($"\x1b[41mFile {filetoCopy} doesnt exist???? Try regenerating after regenererating index");
            return null;
        }
    }
    foreach ((var from, var to) in mod.Links.Select(link => new Tuple<string, string>(Path.Combine(path, link.LocationFrom[1..]), Path.Combine(path, link.LocationTo[1..]))))
    {
        if (!File.Exists(to))
        {
            Console.Error.WriteLine($"\x1b[41mFile to link {to} doesnt exist???? Try regenerating after regenererating index");
            return null;
        }
        var realLoc = Path.GetDirectoryName(from);
        if (!string.IsNullOrWhiteSpace(realLoc))
        {
            Directory.CreateDirectory(realLoc);
        }
        File.CreateSymbolicLink(from, to);
    }
    return path;

}
void BuildFirstPart(Action<string, SilverAudioPlayerModulesInfo, string> next, string kind)
{
    var parent = GetParent(AppContext.BaseDirectory, 4);
    XmlSerializer s = new(typeof(SilverAudioPlayerModulesInfo));
    using var mainfile = new FileStream(Path.Combine(parent, "SilverAudioPlayerModules.xml"), FileMode.Open);
    SilverAudioPlayerModulesInfo? info = s.Deserialize(mainfile) as SilverAudioPlayerModulesInfo;
    if (info is not null)
    {
        var genBuildDir = Path.Combine(parent, "build");
        if (!Path.Exists(genBuildDir))
        {
            Directory.CreateDirectory(genBuildDir);
        }
        else
        {
            Directory.Delete(genBuildDir, true);
            Directory.CreateDirectory(genBuildDir);

        }
        var modBuildDir = Path.Combine(genBuildDir, kind);
        if (!Path.Exists(modBuildDir))
        {
            Directory.CreateDirectory(modBuildDir);
        }
        next(parent, info, modBuildDir);
    }
}
void APart(string parent, SilverAudioPlayerModulesInfo info, string output)
{
    using var stream = typeof(SilverAudioPlayerModulesInfo).Assembly.GetManifestResourceStream(
        "SilverAudioPlayerModuleHandler.Build.PackageTemplate.txt");
    using var reader = new StreamReader(stream);
    var t = reader.ReadToEnd();
    if (info != null)
    {
        foreach (var module in info.Modules)
        {
            File.WriteAllText(Path.Combine(parent, module.Name + ".PKGBUILD"), string.Format(t, "sap-" + module.Name, "7", "1", "package for silveraudioplayer", Path.GetRelativePath(parent, GetParent(Path.Combine(parent, module.Files[1].Name), 3))));
        }
    }
}

int Build(string directory, Action<string> standardOut, string additional = "")
{
    //-p:ExtraDefineConstants=
    var p = new Process();
    p.StartInfo.FileName = "dotnet";
    p.StartInfo.Arguments = "publish -c Release -p:ExtraDefineConstants= ";
    p.StartInfo.WorkingDirectory = directory;
    p.StartInfo.UseShellExecute = false;
    //   p.StartInfo.RedirectStandardOutput = true;
    //   p.EnableRaisingEvents = true;
    /*  p.OutputDataReceived += (sender, eventArgs) =>
      {
          if (eventArgs.Data == null) return;
          standardOut(eventArgs.Data);
      };*/
    p.Start();
    //  p.BeginOutputReadLine();
    p.WaitForExit();
    return p.ExitCode;
}

string DoMagic(string aAAAAAAA)
{
    if (aAAAAAAA.StartsWith("lib/netstandard"))
    {
        return aAAAAAAA[19..];
    }
    if (aAAAAAAA.StartsWith("lib/netcoreapp"))
    {
        return aAAAAAAA[18..];
    }
    if (aAAAAAAA.StartsWith("lib/net"))
    {
        return aAAAAAAA[11..];
    }
    return aAAAAAAA;
}

string? ProcessDepOfModule(RuntimeLibrary r, SilverAudioPlayerModulesInfo info, string parentPublishFolder, string parent)
{
    if (info.Modules.Any(x => x.Name == r.Name))
    {
        return null;
    }
    Module m = new()
    {
        Name = r.Name
    };
    m.Files.AddRange(r.RuntimeAssemblyGroups.SelectMany(x => x.AssetPaths).Select(x => DoMagic(x)).Distinct().Select(x => new SilverAudioPlayerModuleHandler.Build.File()
    {
        Name = Path.GetRelativePath(parent, Path.Combine(parentPublishFolder, x))
    }));
    m.Files.AddRange(r.NativeLibraryGroups.SelectMany(x => x.AssetPaths).Select(x => DoMagic(x)).Distinct().Select(x => new SilverAudioPlayerModuleHandler.Build.File()
    {
        Name = Path.GetRelativePath(parent, Path.Combine(parentPublishFolder, x))
    }));
    m.Files.AddRange(r.ResourceAssemblies.Select(x => x.Path).Select(x => DoMagic(x)).Distinct().Select(x => new SilverAudioPlayerModuleHandler.Build.File()
    {
        Name = Path.GetRelativePath(parent, Path.Combine(parentPublishFolder, x))
    }));
    m.Dependencies.AddRange(r.Dependencies.Select(x => x.Name));
    if (m.Files.Count == 0)
    {
        return null;
    }
    if (r.Name == "Catenoid")
    {
        var asasassdasa = new string[] { "Catenoid.deps.json", "Catenoid", "Catenoid.runtimeconfig.json" };

        var range = asasassdasa.Select(x =>
            new SilverAudioPlayerModuleHandler.Build.File()
            {
                Name = Path.GetRelativePath(parent, Path.Combine(parentPublishFolder, x))
            }).GroupBy(x => Path.GetExtension(x.Name) is ".pdb" or ".dbg");

        m.Files.AddRange(range.Where(x => x.Key == false).SelectMany(x => x));
        m.Debug.AddRange(range.Where(x => x.Key == true).SelectMany(x => x));
        m.Files.Add(new SilverAudioPlayerModuleHandler.Build.File()
        {
            Name = Path.GetRelativePath(parent, Path.Combine(parentPublishFolder, "Catenoid.desktop")),
            LocationToPlaceLinux = "/usr/share/applications/Catenoid.desktop"
        });
        m.Files.Add(new SilverAudioPlayerModuleHandler.Build.File()
        {
            Name = Path.GetRelativePath(parent, Path.Combine(parentPublishFolder, "icon.svg")),
            LocationToPlaceLinux = "/usr/share/pixmaps/sap.svg"
        });
        m.Links.Add(new()
        {
            LocationFrom = "/usr/bin/catenoid",
            LocationTo = "/opt/catenoid/catenoid"
        });

    }
    info.Modules.Add(m);
    return r.Name;
}
void ProcessModule(string dir, SilverAudioPlayerModulesInfo info, string parent)
{

    var projects = Directory.GetFiles(dir, "*.csproj").ToList();
    if (projects.Count == 0) return;
    var project = Path.GetFileNameWithoutExtension(projects[0]);
    if (info.Modules.Any(x => x.Name == project))
    {
        return;
    }
    Debug.WriteLine(dir);
    var bin = Path.Combine(dir, "bin");
    //TODO check if project is net6 or 8 or whatever the hell
    var publishFolder = Path.Combine(bin, "Release", "net8.0", "publish");
    if (!Directory.Exists(publishFolder)) return;
    ///AYO PUMP THE BREAKS gotta read .deps.json first
    var depsfile = Path.Combine(publishFolder, project + ".deps.json");

    if (File.Exists(depsfile))
    {
        using DependencyContextJsonReader reader = new();
        using FileStream readeroffile = new(depsfile, FileMode.Open);
        var res = reader.Read(readeroffile);
        var a = res.RuntimeLibraries[0];
        Debug.Assert(a.Name == project);
        foreach (var runtimeLibrary in res.RuntimeLibraries)
        {
            var r = ProcessDepOfModule(runtimeLibrary, info, publishFolder, parent);
        }
    }
    else
    {
        Console.WriteLine($"AYO AYO AYO {project} has no dep file :3 !!!!!!!");
    }
}
void GenIndex()
{
    var parent = GetParent(AppContext.BaseDirectory, 4);
    SilverAudioPlayerModulesInfo info = new()
    {
        Modules = []
    };
    //var sorted = new List<Module>();
    // var visited = new HashSet<Module>();
    var exitCode = Build(parent, Console.WriteLine);
    if (exitCode != 0)
    {
        Console.WriteLine($"Failed to compile {parent}, {exitCode}");
    }
    foreach (var dir in Directory.GetDirectories(parent))
    {

        if (File.Exists(Path.Combine(dir, ".SilverAudioPlayerModule")))
        {
            ProcessModule(dir, info, parent);
        }
    }
    foreach (var module in info.Modules)
    {
        //make sure all deps exist

        foreach (var d in module.Dependencies.ToList())
        {
            if (!info.Modules.Any(x => x.Name == d))
            {
                Console.WriteLine($"Dependancy {d} of {module.Name} not found, maybe empty?");
                module.Dependencies.Remove(d);
            }
            else
            {
                foreach (var file in info.Modules.First(x => x.Name == d).Files)
                {

                    var filetoCopy = Path.Combine(parent, file.Name);
                    if (!File.Exists(filetoCopy))
                    {
                        Console.WriteLine($"Dependency {d} of {module.Name} file {file.Name} not found??????????");
                        module.Dependencies.Remove(d);
                        info.Modules.Remove(info.Modules.First(x => x.Name == d));
                        break;
                    }
                }
            }
        }
    }

    XmlSerializer s = new(typeof(SilverAudioPlayerModulesInfo));
    using var mainfile = new FileStream(Path.Combine(parent, "SilverAudioPlayerModules.xml"), FileMode.Create);
    s.Serialize(mainfile, info);
}


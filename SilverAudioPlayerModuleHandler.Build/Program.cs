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
Dictionary<string, string> PackageNames = new();

/*foreach (var arg in args)
{
    switch (arg.ToLower())
    {
        case "gi" or "generateindex":*/
GenIndex();
/*  break;
case "bs" or "builddebian":*/
BuildFirstPart(Debian2ndPart, "debian");
/*    break;
default:
    Console.WriteLine("Ayo bro, uh pass arguments and stuff\n gi \t OR \t generateindex \t - creates the index needed for other stuff\n bd \t OR \t builddebian \t - builds packages for debian in build/debian");
    break;
}
}
*/


//GenPkg();
return;


string GetParent(string path, int n) => n > 0 ? GetParent(Directory.GetParent(path).FullName, n - 1) : Directory.GetParent(path).FullName;
void Debian2ndPart(string parent, SilverAudioPlayerModulesInfo info, string output)
{
    foreach (var module in info.Modules)
    {
        var modpath = Path.Join(parent, "build", "debian", GetPackageName(module.Name));
        CreateAndFillNeededDirForPackageDebian(parent, modpath, module);
        Process.Start("dpkg-deb", $"--build {modpath}");
    }
    Console.WriteLine("Done????");
}
string GetPackageName(string input)
{
    if (PackageNames.TryGetValue(input, out string? value))
    {
        return value;
    }
    return PackageNames[input] = input.ToLower().Replace('.', '-');
}
void CreateAndFillNeededDirForPackageDebian(string basePath, string path, Module mod)
{
    //path would be something like ...../build/debian/silveraudioplayer-naudio/
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
            var dir = Path.GetDirectoryName(Path.Combine(opt, dirandname));
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.Copy(filetoCopy, Path.Combine(opt, dirandname));
        }
        else
        {
            Console.Error.WriteLine($"File {filetoCopy} doesnt exist???? Try regenerating after regenererating index");
        }
    }
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
    p.StartInfo.Arguments = "publish -c Release";
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
        Console.WriteLine(aAAAAAAA[19..]);
        return aAAAAAAA[19..];
    }
    if (aAAAAAAA.StartsWith("lib/netcoreapp"))
    {
        Console.WriteLine(aAAAAAAA[18..]);
        return aAAAAAAA[18..];
    }
    if (aAAAAAAA.StartsWith("lib/net"))
    {

        Console.WriteLine(aAAAAAAA[11..]);
        return aAAAAAAA[11..];
    }
    return aAAAAAAA;
}

string? ProcessDepOfModule(RuntimeLibrary r, SilverAudioPlayerModulesInfo info, string parentPublishFolder, string parent)
{
    //wtf am i gonna do here?????
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
    if (!m.Files.Any())
    {
        return null;
    }
    if (r.Name == "SilverAudioPlayer.Avalonia")
    {
        var asasassdasa = new string[] { "SilverAudioPlayer.Avalonia.deps.json", "SilverAudioPlayer.Avalonia", "SilverAudioPlayer.Avalonia.runtimeconfig.json" };

        var range = asasassdasa.Select(x =>
            new SilverAudioPlayerModuleHandler.Build.File()
            {
                Name =  Path.GetRelativePath(parent,Path.Combine(parentPublishFolder, x))
            }).GroupBy(x => Path.GetExtension(x.Name) is ".pdb" or ".dbg");
        m.Files.AddRange(range.Where(x => x.Key == false).SelectMany(x => x));
        m.Debug.AddRange(range.Where(x => x.Key == true).SelectMany(x => x));

    }
    info.Modules.Add(m);
    return r.Name;
}
void ProcessModule(string dir, SilverAudioPlayerModulesInfo info, string parent)
{

    var projects = Directory.GetFiles(dir, "*.csproj").ToList();
    if (!projects.Any()) return;
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
            /* if (r != null)
             {
                 Helper.Visit(r.Item1, visited, sorted, x => info.Modules.Where(y => x.Dependencies.Contains(y.Name)), false);
                 foreach (var deps in r.Item2)
                 {
                     Helper.Visit(deps, visited, sorted, x => info.Modules.Where(y => x.Dependencies.Contains(y.Name)), false);

                 }
             }*/
        }
    }
    foreach (var module in info.Modules)
    {
        //make sure all deps exist

        foreach (var d in module.Dependencies.ToList())
        {
            if (!info.Modules.Any(x => x.Name == d))
            {
                Console.WriteLine($"Dependancy {d} of {module.Name} not found");
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
                //make sure all dep files exist maybe eh
            }
        }
    }
    //var sorted =info.Modules.TSort(, false).ToList();
    //info.Modules = sorted;
    ///var allModuleFiles = info.Modules.Select(x => x.Files).ToList();

    XmlSerializer s = new(typeof(SilverAudioPlayerModulesInfo));
    using var mainfile = new FileStream(Path.Combine(parent, "SilverAudioPlayerModules.xml"), FileMode.Create);
    s.Serialize(mainfile, info);
}


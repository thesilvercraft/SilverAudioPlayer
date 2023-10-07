using System.Diagnostics;
using System.Xml.Serialization;
using SilverAudioPlayerModuleHandler.Build;
using File = System.IO.File;

//Q&A
//What the hell is this?
//This code is currently responsible for creating the SilverAudioPlayerModules.xml file which might be used in the future
//to aid in package development (maybe) to allow users to choose which plugins to install using their system package 
//manager

/*foreach (var arg in args)
{
    switch (arg.ToLower())
    {
        case "gi" or "generateindex":
            GenIndex();
            break;
        
    }
}*/
GenIndex();
//GenPkg();
return;

string GetParent(string path, int n) => n > 0 ? GetParent(Directory.GetParent(path).FullName, n - 1) : Directory.GetParent(path).FullName;

void GenPkg()
{
    var parent = GetParent(AppContext.BaseDirectory, 4);
    XmlSerializer s=new(typeof(SilverAudioPlayerModulesInfo));
    using var mainfile = new FileStream(Path.Combine(parent, "SilverAudioPlayerModules.xml"), FileMode.Open);
    SilverAudioPlayerModulesInfo? info = s.Deserialize(mainfile) as SilverAudioPlayerModulesInfo;
 using   var stream =typeof(SilverAudioPlayerModulesInfo).Assembly.GetManifestResourceStream(
        "SilverAudioPlayerModuleHandler.Build.PackageTemplate.txt");
 using var reader = new StreamReader(stream);
 var t = reader.ReadToEnd();
    if (info != null)
    {
        foreach (var module in info.Modules)
        {
            File.WriteAllText(Path.Combine(parent,module.Name+".PKGBUILD"), string.Format(t, "sap-"+module.Name, "7","1","package for silveraudioplayer", Path.GetRelativePath( parent,GetParent( Path.Combine(parent, module.Files[1].Name),3))));
        }
    }

}

int Build(string directory, Action<string> standardOut, string additional="")
{
    //-p:ExtraDefineConstants=
    var p = new Process();
    p.StartInfo.FileName = "dotnet";
    p.StartInfo.Arguments = "build -c Release";
    p.StartInfo.WorkingDirectory = directory;
    p.StartInfo.UseShellExecute = false;
    p.StartInfo.RedirectStandardOutput = true;
    p.EnableRaisingEvents = true;
    p.OutputDataReceived += (sender, eventArgs) =>
    {
        if (eventArgs.Data == null) return;
        standardOut(eventArgs.Data);
    };
    p.Start();
    p.BeginOutputReadLine();
    p.WaitForExit();
    return p.ExitCode;
}

List<string> CleanDependant(List<string> files, List<Tuple<string, string>> libs)
{
    files.RemoveAll(x => libs.Any(y => x.Contains( Path.DirectorySeparatorChar  + Path.GetFileNameWithoutExtension( y.Item2))));
    return files;
}

void ProcessModule(string dir, SilverAudioPlayerModulesInfo info, string parent)
{
     var projects = Directory.GetFiles(dir, "*.csproj").ToList();
        if (!projects.Any()) return;
        Debug.WriteLine(dir);
        var project = Path.GetFileNameWithoutExtension(projects[0]);
        var bin = Path.Combine(dir, "bin");
        if (Directory.Exists(bin))
        {
            Directory.Delete(bin, true);
        }
        List<Tuple<string, string>> libs = new();
        var listenForLibs = false;
        var exitCode = Build(dir, (eventArgs) =>
        {
            Console.WriteLine(eventArgs);
            switch (listenForLibs)
            {
                case true:
                {
                    var lib = eventArgs.Split(" -> ");
                    if (lib.Length == 2)
                    {
                        if (lib[0].Trim() != project)
                        {
                            libs.Add(new(lib[0].Trim() , lib[1].Trim() ));
                            Console.WriteLine($"Library {lib[0].Trim()} registered");
                        }
                    }

                    break;
                }
                case false when eventArgs.Contains("projects are up-to-date for restore."):
                    listenForLibs = true;
                    break;
            }
        });
        if (exitCode != 0) return;
        Module m = new();
        m.Name = project;
        var net6 = Path.Combine(bin, "Release", "net6.0");
        if (!Directory.Exists(net6)) return;
        var net6files = Directory.GetFiles(net6);
        var net6filesfiltered = CleanDependant(net6files.ToList(), libs);
        var range = net6filesfiltered.Select(x => Path.GetRelativePath(parent, x)).Select(x =>
            new SilverAudioPlayerModuleHandler.Build.File()
            {
                Name = x
            }).GroupBy(x=>Path.GetExtension(x.Name) is ".pdb" or ".dbg");
        m.Files.AddRange(range.Where(x=>x.Key==false).SelectMany(x=>x));
        m.Debug.AddRange(range.Where(x=>x.Key).SelectMany(x=>x));
        m.Dependencies.AddRange(libs.Select(x=>x.Item1));
        foreach (var lib in libs.Where(lib => !info.Modules.Any(x => x.Name ==  lib.Item1)).ToList())
        {
            ProcessModule( GetParent(lib.Item2,3), info, parent);
        }
        info.Modules.Add(m);
}
void GenIndex()
{
    var parent = GetParent(AppContext.BaseDirectory, 4);
    SilverAudioPlayerModulesInfo info = new();
    foreach (var dir in Directory.GetDirectories(parent))
    {
       if( File.Exists(Path.Combine(dir, ".SilverAudioPlayerModule")))
       {
           ProcessModule(dir, info, parent);
       }
    }

    ///var allModuleFiles = info.Modules.Select(x => x.Files).ToList();
   
    XmlSerializer s=new(typeof(SilverAudioPlayerModulesInfo));
    using var mainfile = new FileStream(Path.Combine(parent, "SilverAudioPlayerModules.xml"), FileMode.Create);
    s.Serialize(mainfile, info);
}
    

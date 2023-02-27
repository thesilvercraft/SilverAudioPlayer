using DynamicData.Experimental;
using SilverAudioPlayer.Shared;
using SilverAudioPlayer.Shared.ConfigScreen;
using System.Composition;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;

namespace SilverAudioPlayer.Windows.MusicStatusInterface.FortniteCarRadio;

//contains some code from https://github.com/ApertureC/Fortnite-Music-Changer/blob/master/Fortnite%20Music%20WPF/LogFileReader.cs
/*MIT License

Copyright (c) 2018 ApertureC

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.*/
[Export(typeof(IMusicStatusInterface))]
public class FortnitePlayTracker : IMusicStatusInterface, IAmConfigurable, IAmOnceAgainAskingYouForYourMemory
{
    private readonly List<IConfigurableElement> ConfigurableElements;
    public FortnitePlayTracker()
    {
        ConfigurableElements = new List<IConfigurableElement>
        {
            new SimpleTextBox
                { GetContent = () => ((FortniteCarRadioConfig)ConfigObject.Value).VolDef.ToString(), GetPlaceholder= ()=>"L", SetContent = c => {
                     Debug.WriteLine("l1");

                    if(ConfigObject.Value is FortniteCarRadioConfig x)
                    {
                        x.VolDef= byte.Parse(c);
                    }

                },

                },
             new SimpleTextBox
                { GetContent = () => ((FortniteCarRadioConfig)ConfigObject.Value).VolVeh.ToString(),
                 GetPlaceholder= ()=>"L", 
                 SetContent = c => {
                     Debug.WriteLine("l2");
                    if(ConfigObject.Value is FortniteCarRadioConfig x)
                    {
                        x.VolVeh= byte.Parse(c);
                    }
                },
                },
               new SimpleButton
                { GetContent= ()=>"save",
                 Clicked= () =>
                 { if(ConfigObject.Value is FortniteCarRadioConfig x)
                    {
                        ((ICanBeToldThatAPartOfMeIsChanged)x).PropertyChanged(x,new("VolDef"));

                        ((ICanBeToldThatAPartOfMeIsChanged)x).PropertyChanged(x,new("VolVeh"));
                     }
                 }
                }
        };
    }
    public List<IConfigurableElement> GetElements()
    {
        return ConfigurableElements;
    }

    public ObjectToRemember ConfigObject = new(Guid.Parse("d11919d0-281a-4fd0-9694-98c9d648dec1"), new FortniteCarRadioConfig());
    public ObjectToRemember[] ObjectsToRememberForMe => new ObjectToRemember[] { ConfigObject };
    public string Name => "fORTNITE";

    public string Description => "awful plugin do not use";

    public WrappedStream? Icon => new WrappedEmbeddedResourceStream(typeof(FortnitePlayTracker).Assembly,
             "SilverAudioPlayer.Windows.MusicStatusInterface.FortniteCarRadio.defnotfrtnt.svg");

    public Version? Version => typeof(FortnitePlayTracker).Assembly.GetName().Version;

    public string Licenses => "MIT";

    public List<Tuple<Uri, URLType>>? Links { get; set; }
    IMusicStatusInterfaceListener Env;

    FileSystemWatcher Watcher;
    static string FortniteLogDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\FortniteGame\Saved\Logs";
    static string FortniteLog = Path.Combine(FortniteLogDir , "FortniteGame.log");
  
    StreamReader StreamReader;
    private void NewWrite(object sender, FileSystemEventArgs e)
    {
        if(Process.GetProcessesByName("FortniteClient-Win64-Shipping").Length ==0)
        {
            return;
        }
        if (StreamReader == null && File.Exists(FortniteLog))
        {
            FileStream fileStream = File.Open(FortniteLog, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            StreamReader = new StreamReader(fileStream);
        }
        if(StreamReader == null)
        {
            //what
            return;
        }
        string currentLine;
        while ((currentLine = StreamReader.ReadLine()) != null) // go through the new lines.
        {
            if (currentLine.Contains("Adding scene: VehicleHUD_Scene"))
                {
                Env.SetVolume(((FortniteCarRadioConfig)ConfigObject.Value).VolVeh);
            }else if(currentLine.Contains("Removing scene: VehicleHUD_Scene"))
            {
                Env.SetVolume(((FortniteCarRadioConfig)ConfigObject.Value).VolDef);
            }
        }
    }

   
    public void StartIPC(IMusicStatusInterfaceListener listener)
    {
        Env = listener;
        Watcher = new FileSystemWatcher
        {
            Filter = "FortniteGame.log",
            Path = FortniteLogDir,
            NotifyFilter = NotifyFilters.LastWrite
        };

        Watcher.Changed += NewWrite;
        Watcher.EnableRaisingEvents = true;
    }

    public void StopIPC(IMusicStatusInterfaceListener listener)
    {
        Watcher?.Dispose();
        StreamReader?.Dispose();
    }

    public void Dispose()
    {
    }
}
using System.Composition;
using System.Diagnostics;
using SilverAudioPlayer.Shared;
using SilverCraft.Ekorensis;

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
    public bool IsStarted => _IsStarted;
    private bool _IsStarted;
    private bool _isInVehicle;

    public FortnitePlayTracker()
    {
        ConfigurableElements =
        [
            new SimpleRow(
                new SimpleLabel("Default volume (%):"),
                new SimpleNumericUpDown
                {
                    Minimum = 0,
                    Maximum = 100,
                    Increment = 1,
                    DecimalPlaces = 0,
                    GetValue = () =>
                    {
                        if (ConfigObject.Value is FortniteCarRadioConfig x)
                        {
                            return x.VolDef;
                        }

                        return 100;
                    },
                    ValueChanged = val =>
                    {
                        if (ConfigObject.Value is not FortniteCarRadioConfig x) return;
                        x.VolDef = (byte)val;
                        ((ICanBeToldThatAPartOfMeIsChanged)x).PropertyChanged(x, new("VolDef"));
                        if (_IsStarted && Env != null && !_isInVehicle)
                        {
                            Env.SetVolume(x.VolDef);
                        }
                    }
                }
            ),
            new SimpleRow(
                new SimpleLabel("Vehicle volume (%):"),
                new SimpleNumericUpDown
                {
                    Minimum = 0,
                    Maximum = 100,
                    Increment = 1,
                    DecimalPlaces = 0,
                    GetValue = () =>
                    {
                        if (ConfigObject.Value is FortniteCarRadioConfig x)
                        {
                            return x.VolVeh;
                        }

                        return 30;
                    },
                    ValueChanged = val =>
                    {
                        if (ConfigObject.Value is not FortniteCarRadioConfig x) return;
                        x.VolVeh = (byte)val;
                        ((ICanBeToldThatAPartOfMeIsChanged)x).PropertyChanged(x, new("VolVeh"));
                        if (_IsStarted && Env != null && _isInVehicle)
                        {
                            Env.SetVolume(x.VolVeh);
                        }
                    }
                }
            )
        ];
    }

    public IEnumerable<IConfigurableElement> GetElements()
    {
        return ConfigurableElements;
    }

    public ObjectToRemember ConfigObject =
        new(Guid.Parse("d11919d0-281a-4fd0-9694-98c9d648dec1"), new FortniteCarRadioConfig());

    public IEnumerable<ObjectToRemember> ObjectsToRememberForMe => new ObjectToRemember[] { ConfigObject };
    public string Name => "fORTNITE";

    public string Description => "awful plugin do not use";

    public WrappedStream? Icon => new WrappedEmbeddedResourceStream(typeof(FortnitePlayTracker).Assembly,
        "SilverAudioPlayer.Windows.MusicStatusInterface.FortniteCarRadio.defnotfrtnt.svg");

    public Version? Version => typeof(FortnitePlayTracker).Assembly.GetName().Version;

    public string Licenses =>
        "The awful FortnitePlayTracker is licensed under the MIT license (called the expat license because MIT actually uses many licenses)";

    public List<Tuple<Uri, URLType>>? Links { get; } =
        [new Tuple<Uri, URLType>(new Uri(ConstantCodeInfo.GetCurrentUrl()), URLType.Code)];

    private IMusicStatusInterfaceListener? Env;

    private FileSystemWatcher? Watcher;

    static readonly string FortniteLogDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FortniteGame", "Saved",
            "Logs");

    static readonly string FortniteLog = Path.Combine(FortniteLogDir, "FortniteGame.log");

    private StreamReader? StreamReader;

    private void NewWrite(object sender, FileSystemEventArgs e)
    {
        if (!_IsStarted || Env == null) return;

        if (Process.GetProcessesByName("FortniteClient-Win64-Shipping").Length == 0)
        {
            return;
        }

        if (StreamReader == null && File.Exists(FortniteLog))
        {
            FileStream fileStream = File.Open(FortniteLog, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            StreamReader = new StreamReader(fileStream);
        }

        if (StreamReader == null)
        {
            return;
        }

        string? currentLine;
        while ((currentLine = StreamReader.ReadLine()) != null)
        {
            if (currentLine.Contains("Adding scene: VehicleHUD_Scene"))
            {
                _isInVehicle = true;
                Env.SetVolume(((FortniteCarRadioConfig)ConfigObject.Value).VolVeh);
            }
            else if (currentLine.Contains("Removing scene: VehicleHUD_Scene"))
            {
                _isInVehicle = false;
                Env.SetVolume(((FortniteCarRadioConfig)ConfigObject.Value).VolDef);
            }
        }
    }

    public void StartIPC(IMusicStatusInterfaceListener listener)
    {
        if (!File.Exists(FortniteLog)) return;
        Env = listener;
        _IsStarted = true;
        _isInVehicle = false;
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
        _IsStarted = false;
        Env = null;

        Watcher?.Dispose();
        StreamReader?.Dispose();
    }

    public void Dispose()
    {
    }
}
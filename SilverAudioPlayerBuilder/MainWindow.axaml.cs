using Avalonia.Controls;
using SilverCraft.AvaloniaUtils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;

namespace SilverAudioPlayerBuilder
{
    public partial class MainWindow : Window
    {
        public Dictionary<string, string> Modules = new()
        {
            {"JF","JellyFin support, musiclibrary" },
            {"DWMID","DryWetMidi, player and metadata, playback on Windows" },
            {"ZATL","Z440AtlCore, metadata provider" },
            {"NA", "NAudio, player, (with CSCore, PortAudio, libsndfile, openmpt) on Win/Linux" },
            {"LLib","Local library, musiclibrary, Proof-Of-Concept" },
            {"DISCRD","Discord, status interface, Alpha" },
            {"CAST","Chromecast player, Proof-Of-Concept" },
            {"VLC","libVLC, player, partially broken on Win" },
            {"SMTC","System Media Transport Controls, Windows 10+, WINDOWS SDK NEEDED" },
            {"FRT","Fortnite Car, music status, radio volume changer, Windows" },
            {"CAD","CD Art Display, Windows 7+, Winforms needed" },
            {"MPRIS","Linux DBus MPRIS, music status" },
            {"LSync","Sync, Proof-Of-Concept, calls ffmpeg to convert files to mp3's that it stores on a given drive" },
            {"LPlatform","Linux DBus platform stuff for screensaver inhibit" },
            {"MMIDI","Managed midi, player" },
            {"juke","silverjuke web interface, needs http://localhost:36169/" },
        };
        Dictionary<string,CheckBox> CheckBoxes = [];
        public MainWindow()
        {
            InitializeComponent();
            this.DoAfterInitTasks(true);
            MainGrid = this.FindControl<Grid>("MainGrid");
            var a = new StringBuilder(Modules.Count*2).Insert(0, "*,", Modules.Count).Append('*').ToString();
            MainGrid.RowDefinitions = new RowDefinitions(a);
            int o = 0;
            foreach(var module in Modules)
            {
                var checkbox = new CheckBox
                {
                    Content = module.Value,
                    FontSize=20,
                    FontWeight=Avalonia.Media.FontWeight.DemiBold,
                };
                
                if (module.Key.Contains('*'))
                {
                    var x = module.Key.IndexOf('*');
                    if(x !=-1)
                    {
                        var y = module.Key.IndexOf('*',x+1);
                        if(y !=-1)
                        {
                            var s = module.Key.Substring(x+1,y-1);
                            bool isset = false;
                            checkbox.Click += (x, y) => {
                                CheckBoxes[s].IsChecked = true;
                                if(!isset)
                                {
                                    CheckBoxes[s].Click += (x, y) =>
                                    {
                                        checkbox.IsChecked = checkbox.IsChecked==true && (CheckBoxes[s].IsChecked == true);
                                    };
                                    isset = true;
                                }
                                
                            };
                        }
                    }

                }
                checkbox.SetValue(Grid.RowProperty, o);
                o++;
                MainGrid.Children.Add(checkbox);
                CheckBoxes.Add(module.Key,checkbox);
            }
            Button DOIT = new()
            {
                Content = "COMPILE"
            };
            DOIT.SetValue(Grid.RowProperty, o);
            DOIT.Click += DOIT_Click;
            MainGrid.Children.Add(DOIT);

        }

        private void DOIT_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            List<string> selectedArgs = [];
            foreach(var c in CheckBoxes)
            {
                if(c.Value.IsChecked==true)
                {
                    selectedArgs.Add(c.Key.Replace("*",""));
                }
            }
            var cmd = $"\"dotnet publish {Path.Combine(Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetParent(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)).FullName).FullName).FullName).FullName, "Catenoid", "Catenoid.csproj")} -c Release -p:ExtraDefineConstants=\"{string.Join("%3B", selectedArgs)}\" --interactive\"";
            Debug.WriteLine(cmd);
            if (Environment.OSVersion.Platform==PlatformID.Unix)
            {
                Process.Start(new ProcessStartInfo() { FileName = "sh", Arguments = "-i -c "+cmd, CreateNoWindow = false });
               //Process.Start("xdg-open", Path.Combine(Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetParent(Environment.CurrentDirectory).FullName).FullName).FullName).FullName, "Catenoid", "bin", "Release"));
            }
            else
            {
               var process= Process.Start(new ProcessStartInfo() { FileName = "cmd", Arguments = $"/k " + cmd, CreateNoWindow = false });
                //process.WaitForExit();
               //if (process.ExitCode==0)
               // {
                //    Process.Start("explorer.exe", Path.Combine(Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetParent(Environment.CurrentDirectory).FullName).FullName).FullName).FullName, "Catenoid", "bin", "Release"));
                //}
            }

        }
    }
}

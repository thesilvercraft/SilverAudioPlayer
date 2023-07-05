using Avalonia.Controls;
using DynamicData;
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
            {"DWMID","DryWetMidi, player, playback on Windows" },
            {"ZATL","Z440AtlCore, metadata provider" },
            {"VLC","libVLC, player, Beta" },
            {"NA", "NAudio, player, partially broken" },
            {"LLib","Local library, musiclibrary, Proof-Of-Concept" },
            {"*NA*FLAC","NAudio.Flac, extends NAudio with FLAC support" },
            {"*NA*VORB","NAudio.Vorbis, extends NAudio with Vorbis (OGG) support" },
            {"CSCORE","CSCore, player, Beta" },
            {"DISCRD","Discord, status interface, Alpha" },
            {"SMTC","System Media Transport Controls, Windows 10+" },
            {"CAD","CD Art Display, Windows 7+" },
            {"MPRIS","Linux DBus, music status, Proof-Of-Concept, nearly empty code" },
            {"LSync","Linux, sync, Proof-Of-Concept, calls ffmpeg to convert files to mp3's that it stores on a given drive" },
            {"CAST","Chromecast player, Proof-Of-Concept" },
            {"FRT","Fortnite Car, music status, radio volume changer (Proof-Of-Concept)" },
        };
        Dictionary<string,CheckBox> CheckBoxes = new();
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
            List<string> selectedArgs = new();
            foreach(var c in CheckBoxes)
            {
                if(c.Value.IsChecked==true)
                {
                    selectedArgs.Add(c.Key.Replace("*",""));
                }
            }
            var cmd = $"\"dotnet publish {Path.Combine(Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetParent(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)).FullName).FullName).FullName).FullName, "SilverAudioPlayer.Avalonia", "SilverAudioPlayer.Avalonia.csproj")} -c Release -p:ExtraDefineConstants=\"{string.Join("%3B", selectedArgs)}\" --interactive\"";
            Debug.WriteLine(cmd);
            if (Environment.OSVersion.Platform==PlatformID.Unix)
            {
                Process.Start(new ProcessStartInfo() { FileName = "sh", Arguments = "-i -c "+cmd, CreateNoWindow = false });
               //Process.Start("xdg-open", Path.Combine(Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetParent(Environment.CurrentDirectory).FullName).FullName).FullName).FullName, "SilverAudioPlayer.Avalonia", "bin", "Release"));
            }
            else
            {
               var process= Process.Start(new ProcessStartInfo() { FileName = "cmd", Arguments = $"/k " + cmd, CreateNoWindow = false });
                //process.WaitForExit();
               //if (process.ExitCode==0)
               // {
                //    Process.Start("explorer.exe", Path.Combine(Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetParent(Environment.CurrentDirectory).FullName).FullName).FullName).FullName, "SilverAudioPlayer.Avalonia", "bin", "Release"));
                //}
            }

        }
    }
}

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
            {"JF","JellyFin support" },
            {"DWMID","DryWetMidi" },
            {"ZATL","Z440AtlCore" },
            {"NA","NAudio" },
            {"*NA*FLAC","NAudio.Flac" },
            {"*NA*VORB","NAudio.Vorbis" },
            {"DISCRD","Discord" },
            {"SMTC","System Media Transport Controls - windows 10+" },
            {"CAD","CD Art Display - windows 7+" },
        };
        Dictionary<string,CheckBox> CheckBoxes = new Dictionary<string,CheckBox>();
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
                    Content = module.Value
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
                Process.Start(new ProcessStartInfo() { FileName = "cmd", Arguments = $"/k "+cmd, CreateNoWindow = false });
                //Process.Start("explorer.exe", Path.Combine(Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetParent(Environment.CurrentDirectory).FullName).FullName).FullName).FullName, "SilverAudioPlayer.Avalonia", "bin", "Release"));
            }

        }
    }
}

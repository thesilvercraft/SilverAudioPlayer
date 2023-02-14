using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using DynamicData;
using SilverAudioPlayer.Shared;
using SilverAudioPlayer.Shared.ConfigScreen;

namespace SilverAudioPlayer.Avalonia;

public partial class Info : Window
{
    public MainWindow MainWindow;

    public Info()
    {
        InitializeComponent();
        CapBox = this.FindControl<ListBox>("CapBox");
        this.DoAfterInitTasksF();
    }

    public Info(MainWindow mainWindow) : this()
    {
        MainWindow = mainWindow;
        List<ICodeInformation> info = new();
        info.AddRange(mainWindow.Logic.PlayProviders);
        info.AddRange(mainWindow.Logic.MusicStatusInterfaces);
        info.AddRange(mainWindow.Logic.MetadataProviders);
        info.AddRange(mainWindow.Logic.WakeLockInterfaces);
        info.AddRange(mainWindow.Logic.PlayStreamProviders);
        info.AddRange(mainWindow.Logic.PlayStreamProviders);
        info.AddRange(mainWindow.Logic.SyncPlugins);
        var ir = Settings.GetInfoRecords(info);
        DataContext = new
        {
            Title = "About " + mainWindow.Env.Name,
            ProductName = mainWindow.Env.Name,
            ProductDescription = mainWindow.Env.Description,
            ProductIcon = Settings.DecodeImage(mainWindow.Env.Icon,200),
            Items = ir.Item1,
            LicenseText = ir.Item2
        };
    }
    public void ElementDoubleTapped(object _, RoutedEventArgs args)
    {
        var y = (InfoPRecord?)CapBox.SelectedItem;
        Settings.ShowElementActionWindow(y, MainWindow);
    }
}


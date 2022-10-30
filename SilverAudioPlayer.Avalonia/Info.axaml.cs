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
        this.DoAfterInitTasks(true);
    }

    public Info(MainWindow mainWindow) : this()
    {
        MainWindow = mainWindow;
        List<ICodeInformation> info = new();
        info.AddRange(mainWindow.Logic.PlayProviders.Select(x => x));
        info.AddRange(mainWindow.Logic.MusicStatusInterfaces.Select(x => x));
        info.AddRange(mainWindow.Logic.MetadataProviders.Select(x => x));
        info.AddRange(mainWindow.Logic.WakeLockInterfaces.Select(x => x));
        var ir = Settings.GetInfoRecords(info);
        SAPAvaloniaPlayerEnviroment sap = new();
        DataContext = new
        {
            Title = "About " + sap.Name,
            ProductName = sap.Name,
            ProductDescription = sap.Description,
            ProductIcon = sap.Icon == null ? null : Bitmap.DecodeToWidth(sap.Icon.GetStream(), 200),
            Items = ir.Item1,
            LicenseText = ir.Item2
        };
    }
    public void ElementDoubleTapped(object _, RoutedEventArgs args)
    {
        var y = (InfoPRecord?)CapBox.SelectedItem;
        Settings.ShowElementActionWindow(y);
    }
}


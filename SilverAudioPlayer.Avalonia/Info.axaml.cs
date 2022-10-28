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
        ObservableCollection<ICodeInformation> info = new();
        info.AddRange(mainWindow.Logic.PlayProviders.Select(x => x));
        info.AddRange(mainWindow.Logic.MusicStatusInterfaces.Select(x => x));
        info.AddRange(mainWindow.Logic.MetadataProviders.Select(x => x));
        info.AddRange(mainWindow.Logic.WakeLockInterfaces.Select(x => x));

        ObservableCollection<InfoPRecord> infop = new();
        StringBuilder licenses = new();
        foreach (var item in info)
        {
            infop.Add(new InfoPRecord(
                item.Name,
                item.Description,
                item.Version,
                item.Icon == null ? null : Bitmap.DecodeToHeight(item.Icon.GetStream(), 80),
                item.Licenses,
                item
            ));
            licenses.AppendLine(item.Licenses);
        }

        SAPAvaloniaPlayerEnviroment sap = new();
        DataContext = new
        {
            Title = "About " + sap.Name,
            ProductName = sap.Name,
            ProductDescription = sap.Description,
            ProductIcon = sap.Icon == null ? null : Bitmap.DecodeToWidth(sap.Icon.GetStream(), 200),
            Items = infop,
            LicenseText = licenses.ToString()
        };
    }

    public static void OpenBrowser(string url)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            Process.Start("xdg-open", url);
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            Process.Start("open", url);
        else
            throw new NotSupportedException("Os not supported");
    }

    public void ElementDoubleTapped(object _, RoutedEventArgs args)
    {
        var y = (InfoPRecord?)CapBox.SelectedItem;
        if (y?.Item.Links != null)
        {
            LaunchActionsWindow launchActionsWindow = new();
            List<SAction> actions = new();
            foreach (var z in y.Item.Links)
                actions.Add(new SAction
                    { ActionName = "Open " + z.Item1, ActionToInvoke = () => OpenBrowser(z.Item1.ToString()) });
            if (y.Item is IAmConfigurable configurable)
                actions.Add(new SAction
                {
                    ActionName = "🔧Configure", ActionToInvoke = () =>
                    {
                        ConfigureWindow cw = new();
                        cw.HandleConfiguration(configurable);
                        cw.Show();
                    }
                });
            launchActionsWindow.AddActions(actions);
            launchActionsWindow.Show();
        }
    }
}

internal record InfoPRecord(string Name, string Description, Version? Version, Bitmap? Icon, string Licenses,
    ICodeInformation Item);
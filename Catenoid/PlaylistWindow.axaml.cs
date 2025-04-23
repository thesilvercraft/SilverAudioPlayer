using System;
using System.IO;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Selection;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using ReactiveUI;
using SilverAudioPlayer.Shared;

namespace Catenoid;

public partial class PlaylistWindow : Window
{
    bool FolderWatcher = true;
    private PlaylistWindowContext dataContext;
    MainWindow mainWindow;
    public PlaylistWindow(MainWindow mainWindow)
    {
        InitializeComponent();
        this.mainWindow = mainWindow;
        PlaylistPath=    ConfigPath.GetPath("Playlists");
        dataContext = new();
        dataContext.Playlists.AddRange(Directory.GetFiles(PlaylistPath));

        DataContext = dataContext;
        this.DoAfterInitTasksF();
        if (OperatingSystem.IsLinux())
        {
            MainGrid.RowDefinitions[0].Height = GridLength.Parse("0");
        }
        if (FolderWatcher)
        {
            watcher = new FileSystemWatcher
            {
                Path = PlaylistPath,
                EnableRaisingEvents = true,
                Filter = "*.spl.br"
            };
            watcher.Created+= WatcherOnCreated;
            watcher.Renamed += WatcherOnRenamed;
            watcher.Deleted+= WatcherOnDeleted;
        }       
    }

    private FileSystemWatcher watcher;
  

    private void WatcherOnDeleted(object sender, FileSystemEventArgs e)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            dataContext.Playlists.Remove(e.FullPath);
        });
    }

    private void WatcherOnRenamed(object sender, RenamedEventArgs e)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            dataContext.Playlists.Remove(e.OldFullPath);
            dataContext.Playlists.Add(e.FullPath);
        });
    }

    private void WatcherOnCreated(object sender, FileSystemEventArgs e)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            dataContext.Playlists.Add(e.FullPath);
        });
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (FolderWatcher)
        {
            watcher.Dispose();
        }
    }

    private string PlaylistPath;

    private void DeleteSelected(object? sender, RoutedEventArgs e)
    {
        foreach (var selectionSelectedItem in dataContext.Selection.SelectedItems)
        {
            File.Delete(selectionSelectedItem);
            if (!FolderWatcher)
            {
                dataContext.Playlists.Remove(selectionSelectedItem);
            }
        }

    }

    private void MainGrid_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (dataContext.Selection.SelectedItems.Count == 1)
        {
            var playlist = dataContext.Selection.SelectedItems[0];
            if (File.Exists(playlist))
            {
                var pl = mainWindow.Logic.TryReadPlaylist(new WrappedFileStream(playlist));
                if (pl == null) return;
                foreach (var song in pl.Tracks)
                {
                    foreach (var source in song.TrackLoadInfos)
                    {
                        mainWindow.Logic.AddWithURLBad(source.URL, source.Source == "http", false);
                        break;
                    }
                }
                /*if (pl.IndexOfActive == null || !(dc.Queue.Count > pl.IndexOfActive)) return;
                Logic.HandleSongChanging(dc.Queue[pl.IndexOfActive.Value]);
                Logic.SetPosition(pl.PositionOfActive ?? 1);*/
            }
        }
    }
}

public partial class PlaylistWindowContext :ObservableObject
{
    [ObservableProperty] public AvaloniaList<string> playlists=new();
    public SelectionModel<string> Selection { get; } = new();
}
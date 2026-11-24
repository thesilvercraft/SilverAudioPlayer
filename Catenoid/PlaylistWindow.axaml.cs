using System;
using System.IO;
using System.Linq;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Selection;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using SilverAudioPlayer.Shared;
using SilverAudioPlayer.Shared.SilverPlaylist;

namespace Catenoid;

public partial class PlaylistWindow : Window
{
    readonly bool FolderWatcher = true;
    private readonly PlaylistWindowContext dataContext = new();
    private readonly MainWindow? mainWindow;
    private readonly FileSystemWatcher? watcher;
    private readonly string PlaylistPath = string.Empty;

    public PlaylistWindow()
    {
        InitializeComponent();
        DataContext = dataContext;
        this.DoAfterInitTasksF();
    }

    public PlaylistWindow(MainWindow mainWindow) : this()
    {
        this.mainWindow = mainWindow;
        PlaylistPath = mainWindow.Logic.PlayerContext.ServiceProvider.GetRequiredService<IConfigPathProvider>().GetPath("Playlists");
        Directory.CreateDirectory(PlaylistPath);
        dataContext.Playlists.AddRange(Directory.GetFiles(PlaylistPath, "*.spl.br"));

        if (!FolderWatcher) return;
        watcher = new FileSystemWatcher
        {
            Path = PlaylistPath,
            EnableRaisingEvents = true,
            Filter = "*.spl.br"
        };
        watcher.Created += WatcherOnCreated;
        watcher.Renamed += WatcherOnRenamed;
        watcher.Deleted += WatcherOnDeleted;
    }

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
            if (e.FullPath.EndsWith(".spl.br", StringComparison.OrdinalIgnoreCase) &&
                !dataContext.Playlists.Contains(e.FullPath))
            {
                dataContext.Playlists.Add(e.FullPath);
            }
        });
    }

    private void WatcherOnCreated(object sender, FileSystemEventArgs e)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (!dataContext.Playlists.Contains(e.FullPath))
            {
                dataContext.Playlists.Add(e.FullPath);
            }
        });
    }

    protected override void OnClosed(EventArgs e)
    {
        if (watcher != null)
        {
            watcher.Created -= WatcherOnCreated;
            watcher.Renamed -= WatcherOnRenamed;
            watcher.Deleted -= WatcherOnDeleted;
            watcher.Dispose();
        }
        base.OnClosed(e);
    }

    private void SavePlaylistClick(object? sender, RoutedEventArgs e)
    {
        if (mainWindow == null || string.IsNullOrEmpty(PlaylistPath)) return;
        var rawName = PlaylistFileName.Text?.Trim();
        if (string.IsNullOrEmpty(rawName)) return;

        var safeName = Path.GetFileName(rawName);
        if (string.IsNullOrWhiteSpace(safeName)) return;
        if (!safeName.EndsWith(".spl.br", StringComparison.OrdinalIgnoreCase))
        {
            safeName += ".spl.br";
        }

        try
        {
            Directory.CreateDirectory(PlaylistPath);
            var targetPath = Path.Combine(PlaylistPath, safeName);
            var queue = mainWindow.Logic.GetQueueCopy();
            using var fs = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.Read);
            SilverPlaylistHelper.SerialiseBrotli(SilverPlaylistHelper.FromQueueCopy(queue, mainWindow.Env), fs);
            if (!FolderWatcher && !dataContext.Playlists.Contains(targetPath))
            {
                dataContext.Playlists.Add(targetPath);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to save playlist {Name}", safeName);
        }
    }

    private void DeleteSelected(object? sender, RoutedEventArgs e)
    {
        var selected = dataContext.Selection.SelectedItems.Where(x => !string.IsNullOrEmpty(x)).ToList();
        foreach (var selectionSelectedItem in selected)
        {
            if (selectionSelectedItem == null) continue;
            try
            {
                if (File.Exists(selectionSelectedItem))
                {
                    File.Delete(selectionSelectedItem);
                }
                if (!FolderWatcher)
                {
                    dataContext.Playlists.Remove(selectionSelectedItem);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to delete playlist {Path}", selectionSelectedItem);
            }
        }
    }

    private void MainGrid_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (mainWindow == null) return;
        if (dataContext.Selection.SelectedItems.Count == 1)
        {
            var playlist = dataContext.Selection.SelectedItems[0];
            if (!string.IsNullOrEmpty(playlist) && File.Exists(playlist))
            {
                using var playlistStream = new WrappedFileStream(playlist);
                var pl = mainWindow.Logic.TryReadPlaylist(playlistStream);
                if (pl == null) return;
                foreach (var song in pl.Tracks)
                {
                    foreach (var source in song.TrackLoadInfos)
                    {
                        mainWindow.Logic.AddWithURLBad(source.URL, source.Source == "http", false);
                        break;
                    }
                }
            }
        }
    }
}

public partial class PlaylistWindowContext :ObservableObject
{
    [ObservableProperty] public AvaloniaList<string> playlists=new();
    public SelectionModel<string> Selection { get; } = new();
}

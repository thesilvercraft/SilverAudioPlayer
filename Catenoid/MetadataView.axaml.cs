using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Serilog;
using SilverAudioPlayer.Shared;
using SilverAudioPlayer.Shared.Metadata;

namespace Catenoid;

public partial class MetadataView : Window
{
    private readonly MetadataViewDataContext dataContext = new();
    private Song? s;
    private PictureViewer? _pictureViewer;

    public MetadataView()
    {
        DataContext = dataContext;
        InitializeComponent();
        mainTreeView = this.FindControl<TreeView>("mainTreeView");
        if (mainTreeView != null)
        {
            mainTreeView.Tapped += MainTreeView_Tapped;
        }
        this.DoAfterInitTasksF();
    }

    private static void MainTreeView_Tapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is not Visual source) return;
        if (source.FindAncestorOfType<ToggleButton>() != null ||
            source.FindAncestorOfType<TextBox>() != null)
        {
            return;
        }

        var item = source.FindAncestorOfType<TreeViewItem>();
        if (item is { DataContext: Field { SubFields.Count: > 0 } })
        {
            item.IsExpanded = !item.IsExpanded;
        }
    }

    private int _loadVersion;

    public void LoadSong(Song s)
    {
        this.s = s;
        var version = Interlocked.Increment(ref _loadVersion);
        dataContext.ValuePairs.Clear();
        Opened -= MetadataView_Opened;
        Opened += MetadataView_Opened;

        _ = Task.Run(() =>
        {
            var root = new Field("Song", s.ToString());
            ProcessSubProperties(s, root, new HashSet<object>(ReferenceEqualityComparer.Instance));
            Dispatcher.UIThread.Post(() =>
            {
                if (Volatile.Read(ref _loadVersion) != version) return;
                dataContext.ValuePairs.Clear();
                dataContext.ValuePairs.Add(root);
            });
        });
    }

    private void MetadataView_Opened(object? sender, EventArgs e)
    {
        if (s?.Metadata?.Pictures is not { Count: > 0 } pictures || pictures[0]?.Data is not WrappedStream b)
        {
            return;
        }

        var version = Volatile.Read(ref _loadVersion);
        _ = Task.Run(() =>
        {
            try
            {
                using var stream = b.GetStream();
                var next = new Bitmap(stream);
                Dispatcher.UIThread.Post(() =>
                {
                    if (Volatile.Read(ref _loadVersion) != version)
                    {
                        next.Dispose();
                        return;
                    }
                    var prev = dataContext.Bitmaps[0];
                    dataContext.Bitmaps[0] = next;
                    prev?.Dispose();
                });
            }
            catch (Exception exception)
            {
                Log.Error(exception, "Error in loading metadata picture");
            }
        });
    }

    protected override void OnClosed(EventArgs e)
    {
        Interlocked.Increment(ref _loadVersion);
        Opened -= MetadataView_Opened;
        var prev = dataContext.Bitmaps[0];
        dataContext.Bitmaps[0] = null;
        prev?.Dispose();
        _pictureViewer?.Close();
        _pictureViewer = null;
        base.OnClosed(e);
    }

    private static bool ShouldSkipType(Type t)
    {
        return typeof(byte[]).IsAssignableFrom(t)
               || typeof(Stream).IsAssignableFrom(t)
               || typeof(Delegate).IsAssignableFrom(t)
               || typeof(MemberInfo).IsAssignableFrom(t)
               || typeof(Assembly).IsAssignableFrom(t)
               || typeof(WaitHandle).IsAssignableFrom(t)
               || t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IObservable<>));
    }

    private void ProcessSubProperties(object thing, Field parentfield, HashSet<object> visited, int allowedlength = 7)
    {
        if (allowedlength == 0) return;
        switch (thing)
        {
            case string:
            case ValueType:
                return;
        }

        if (ShouldSkipType(thing.GetType()) || !visited.Add(thing))
        {
            return;
        }

        try
        {
            switch (thing)
            {
                case IList tlist:
                    {
                        var c = tlist.Count;
                        switch (c)
                        {
                            case 0:
                                {
                                    var pf = new Field("empty", "this IList is empty");
                                    parentfield.SubFields.Add(pf);
                                    return;
                                }
                            case > 200:
                                c = 200;
                                break;
                        }

                        for (var i = 0; i < c; i++)
                        {
                            var item = tlist[i];
                            var pf = new Field(i.ToString(), item?.ToString() ?? "null");
                            parentfield.SubFields.Add(pf);
                            if (item is not null) ProcessSubProperties(item, pf, visited, allowedlength - 1);
                        }
                        return;
                    }
                case IDictionary tdict:
                    {
                        if (tdict.Count == 0)
                        {
                            var pf = new Field("empty", "this IDictionary is empty");
                            parentfield.SubFields.Add(pf);
                            return;
                        }
                        foreach (var key in tdict.Keys)
                        {
                            var item = tdict[key];
                            var pf = new Field(key?.ToString() ?? "null", item?.ToString() ?? "null");
                            parentfield.SubFields.Add(pf);
                            if (item is not null) ProcessSubProperties(item, pf, visited, allowedlength - 1);
                        }
                        return;
                    }
            }

            var typeofmetadata = thing.GetType();
            var properties = typeofmetadata.GetProperties();
            var typeofmetadataref = typeof(Metadata);
            var typeofmetadatapropsref = typeofmetadataref.GetProperties();

            bool IsPartOfImplementation(PropertyInfo x)
            {
                return typeofmetadata.IsSubclassOf(typeofmetadataref) && typeofmetadatapropsref.Any(m => x.Name == m.Name);
            }

            foreach (var property in properties
                         .Where(x => x.CanRead && x.GetIndexParameters().Length == 0 && !ShouldSkipType(x.PropertyType))
                         .OrderByDescending(x => IsPartOfImplementation(x)))
                try
                {
                    var v = property.GetValue(thing);
                    var pf = new Field(property.Name, v?.ToString() ?? "null");
                    parentfield.SubFields.Add(pf);
                    if (v != null) ProcessSubProperties(v, pf, visited, allowedlength - 1);
                }
                catch (Exception e)
                {
                    //No crash needed
                    Log.Information(e, "Exception in ProcessSubProperties");
                }
        }
        finally
        {
            visited.Remove(thing);
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void IMG_DoubleTapped(object? sender, global::Avalonia.Input.TappedEventArgs e)
    {
        if (s?.Metadata?.Pictures is not { Count: > 0 } pictures) return;
        if (_pictureViewer is { IsVisible: true })
        {
            _pictureViewer.Activate();
            return;
        }
        _pictureViewer = new PictureViewer(pictures);
        _pictureViewer.Closed += (_, _) => _pictureViewer = null;
        _pictureViewer.Show();
    }
}

public class Field
{
    public Field(string name, string value)
    {
        FieldName = name;
        FieldValue = value;
    }

    public ObservableCollection<Field> SubFields { get; set; } = [];

    public string FieldName { get; }
    public string FieldValue { get; }
}

internal class MetadataViewDataContext
{
    public ObservableCollection<Field> ValuePairs { get; } = [];

    public ObservableCollection<Bitmap?> Bitmaps { get; } = [null];
}
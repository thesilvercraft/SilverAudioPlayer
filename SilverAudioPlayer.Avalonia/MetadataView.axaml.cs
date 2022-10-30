using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Serilog;
using SilverAudioPlayer.Shared;

namespace SilverAudioPlayer.Avalonia;

public partial class MetadataView : Window
{
    private readonly DC dataContext = new();
    private readonly ILogger? Log;
    private Song s;

    public MetadataView()
    {
        DataContext = dataContext;
        InitializeComponent();
#if DEBUG
        this.AttachDevTools();
#endif
        mainTreeView = this.FindControl<TreeView>("mainTreeView");
        this.DoAfterInitTasks(true);
        Log = Logger.GetLogger(typeof(MetadataView));
    }

    public void LoadSong(Song s)
    {
        this.s = s;
        var x = new Field("Song", s.ToString());

        ProcessSubProperties(s, x);
        dataContext.ValuePairs.Add(x);
        Opened += MetadataView_Opened;
    }

    private void MetadataView_Opened(object? sender, EventArgs e)
    {
        if (s != null && s?.Metadata?.Pictures?.Count > 0 && s?.Metadata?.Pictures[0]?.Data is byte[] b)
        {
            using var memstream = new MemoryStream(b);
            dataContext.Bitmaps[0] = new Bitmap(memstream);
        }
    }

    private void ProcessSubProperties(object thing, Field parentfield, int allowedlength = 6)
    {
        if (allowedlength == 0) return;
        if (thing is string) return;
        if (thing is IList tlist)
        {
            var c = tlist.Count;
            if (c > 200) c = 200;
            for (var i = 0; i < c; i++)
            {
                var item = tlist[i];
                var pf = new Field(i.ToString(), item?.ToString() ?? "null");
                parentfield.SubFields.Add(pf);
                if (item is not null) ProcessSubProperties(item, pf, allowedlength - 1);
            }

            return;
        }

        var typeofmetadata = thing.GetType();
        var properties = typeofmetadata.GetProperties();
        var typeofmetadataref = typeof(Metadata);
        var typeofmetadatapropsref = typeofmetadataref.GetProperties();

        bool IsPartOfImplementation(PropertyInfo x)
        {
            return typeofmetadata.IsSubclassOf(typeofmetadataref) && typeofmetadatapropsref.Any(m => x.Name == m.Name);
        }

        foreach (var property in properties.Where(x => x.CanRead && x.GetGetMethod()?.GetParameters().Length == 0)
                     .OrderByDescending(x => IsPartOfImplementation(x)))
            try
            {
                var v = property.GetValue(thing);
                var pf = new Field(property.Name, v?.ToString() ?? "null");
                parentfield.SubFields.Add(pf);
                if (v != null) ProcessSubProperties(v, pf, allowedlength - 1);
            }
            catch (Exception e)
            {
                //No crash needed
                Log?.Information(e, "Exception in ProcessSubProperties");
            }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void IMG_DoubleTapped(object? sender, RoutedEventArgs e)
    {
        PictureViewer pv = new(s?.Metadata?.Pictures);

        pv.Show();
    }
}

public class Field
{
    public Field(string name, string value)
    {
        FieldName = name;
        FieldValue = value;
    }

    public ObservableCollection<Field> SubFields { get; set; } = new();

    public string FieldName { get; }
    public string FieldValue { get; }
}

internal class DC
{
    public ObservableCollection<Field> ValuePairs { get; } = new();

    public ObservableCollection<Bitmap?> Bitmaps { get; } = new() { null };
}
using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Serilog;
using SilverAudioPlayer.Shared;
using SilverAudioPlayer.Shared.Metadata;

namespace Catenoid;

public partial class MetadataView : Window
{
    private readonly MetadataViewDataContext dataContext = new();
    private Song? s;

    public MetadataView()
    {
        DataContext = dataContext;
        InitializeComponent();
        mainTreeView = this.FindControl<TreeView>("mainTreeView");
        this.DoAfterInitTasksF();
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
        if (s != null && s?.Metadata?.Pictures?.Count > 0 && s?.Metadata?.Pictures[0]?.Data is WrappedStream b)
        {
            try
            {
                dataContext.Bitmaps[0] = new Bitmap(b.GetStream());
            }
            catch (Exception exception)
            {
                Log.Error(exception,"Error in loading metadata picture");
            }
        }
    }

    private void ProcessSubProperties(object thing, Field parentfield, int allowedlength = 7)
    {
        if (allowedlength == 0) return;
        switch (thing)
        {
            case string:
            case int:
                return;
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
                        if (item is not null) ProcessSubProperties(item, pf, allowedlength - 1);
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
                        if (item is not null) ProcessSubProperties(item, pf, allowedlength - 1);
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
                Log.Information(e, "Exception in ProcessSubProperties");
            }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void IMG_DoubleTapped(object? sender, global::Avalonia.Input.TappedEventArgs e)
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

    public ObservableCollection<Field> SubFields { get; set; } = [];

    public string FieldName { get; }
    public string FieldValue { get; }
}

internal class MetadataViewDataContext
{
    public ObservableCollection<Field> ValuePairs { get; } = [];

    public ObservableCollection<Bitmap?> Bitmaps { get; } = [null];
}
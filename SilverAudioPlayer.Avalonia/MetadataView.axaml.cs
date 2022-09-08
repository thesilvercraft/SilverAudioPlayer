using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using SilverAudioPlayer.Shared;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace SilverAudioPlayer.Avalonia
{
    public partial class MetadataView : Window
    {
        private Song s;

       
        public void LoadSong(Song s)
        {
            this.s = s;
            var x = new Field("Song", s.ToString());

            processproperties(s, x);
            dc.ValuePairs.Add(x);
            Opened += MetadataView_Opened;
        }
        
        private void MetadataView_Opened(object? sender, EventArgs e)
        {
            if (s != null && s?.Metadata?.Pictures?.Count > 0)
            {
                var memstream = new MemoryStream(s.Metadata.Pictures[0].Data);
                dc.Bitmaps[0] = new Bitmap(memstream);
            }
        }

        private DC dc = new();

        public MetadataView()
        {
            DataContext = dc;
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            mainTreeView = this.FindControl<TreeView>("mainTreeView");
            this.DoAfterInitTasks(true);
        }

        private void processproperties(object thing, Field parentfield, int allowedlength=5)
        {
            if(allowedlength==0) return;
            if (thing is string)
            {
                return;
            }
            if (thing is IList tlist)
            {
                
                for (int i = 0; i < tlist.Count; i++)
                {
                    object? item = tlist[i];
                    var pf = new Field(i.ToString(),null);
                    parentfield.SubFields.Add(pf);
                    processproperties(item, pf, allowedlength-1);
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
            foreach (var property in properties.Where(x => x.CanRead && x.GetGetMethod()?.GetParameters().Length == 0).OrderByDescending(x => IsPartOfImplementation(x)))
            {
                try
                {
                    var v = property.GetValue(thing);
                    var pf = new Field(property.Name, v?.ToString());
                    parentfield.SubFields.Add(pf);
                    if (v != null)
                    {
                        processproperties(v, pf, allowedlength - 1);
                    }
                }
                catch (Exception e)
                {
                }
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public void Pictures_Click(object? sender, PointerPressedEventArgs e)
        {
        }
    }
    public class Field
    {
        public ObservableCollection<Field> SubFields { get; set; } = new();

        public string FieldName { get; }
        public string FieldValue { get; }

        public Field(string name, string value)
        {
           FieldName = name;
           FieldValue = value;
        }
    }
    internal class DC
    {
        public ObservableCollection<Field> ValuePairs { get; }
    = new ObservableCollection<Field>();

        public ObservableCollection<Bitmap> Bitmaps { get; }
= new ObservableCollection<Bitmap>() { null };
    }
}
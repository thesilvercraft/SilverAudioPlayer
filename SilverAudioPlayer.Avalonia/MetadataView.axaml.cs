using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
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

        public MetadataView(Song s) : this()
        {
            this.s = s;
            processproperties(s.Metadata);
            Opened += MetadataView_Opened;
        }

        private void MetadataView_Opened(object? sender, EventArgs e)
        {
            if (s != null && s?.Metadata?.Pictures?.Count > 0)
            {
                var memstream = new MemoryStream(s.Metadata.Pictures[0].Data);
                dc.Bitmaps[0] = new Bitmap(memstream);
            }
            Debug.WriteLine("dab");
        }

        private DC dc = new();

        public MetadataView()
        {
            DataContext = dc;
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
        }

        private void processproperties(object thing, int meta1 = 20, int meta2 = 30, int graduality = 10, int overflow = 3)
        {
            Debug.WriteLine("Processing properties");
            if (thing is string)
            {
                return;
            }
            if (thing is IList tlist && overflow != 0)
            {
                foreach (var item in tlist)
                {
                    processproperties(item, meta1 + graduality, meta2 + graduality, graduality, overflow - 1);
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
                    dc.ValuePairs.Add(new(property.Name, v?.ToString()));
                    Debug.WriteLine($"{property.Name} : {v?.ToString()}");
                    if (v != null && overflow != 0)
                    {
                        processproperties(v, meta1 + graduality, meta2 + graduality, graduality, overflow - 1);
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

    internal class DC
    {
        public ObservableCollection<KeyValuePair<string, string>> ValuePairs { get; }
    = new ObservableCollection<KeyValuePair<string, string>>();

        public ObservableCollection<Bitmap> Bitmaps { get; }
= new ObservableCollection<Bitmap>() { null };
    }
}
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SilverAudioPlayer.Shared;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;

namespace SilverAudioPlayer.Avalonia
{
    public partial class MainWindow : Window
    {
        private ObservableCollection<Song> Songs;

        public MainWindow()
        {
            InitializeComponent();
            Songs = new();
            TreeView.Items = Songs;
            AddHandler(DragDrop.DropEvent, Drop);
            AddHandler(DragDrop.DragOverEvent, DragOver);
            this.AttachDevTools();
            //SetupDnd("Main", (s) => s.Set(DataFormats.FileNames, GetFromIList(TreeView.SelectedItems)), DragDropEffects.Copy | DragDropEffects.Link);
        }

        private string[] GetFromIList(System.Collections.IList list)
        {
            List<string> a = new();
            foreach (var item in list)
            {
                a.Add(((Song)item).URI);
            }
            return a.ToArray();
        }

        private void DragOver(object sender, DragEventArgs e)
        {
            if (e.Source is Control c && c.Name == "MoveTarget")
            {
                e.DragEffects &= (DragDropEffects.Move);
            }
            else
            {
                e.DragEffects &= (DragDropEffects.Copy);
            }
            Debug.WriteLine($"{string.Join(' ', e.Data.GetDataFormats())}");
            if (!e.Data.Contains(DataFormats.FileNames))
                e.DragEffects = DragDropEffects.None;
        }

        private void Drop(object sender, DragEventArgs e)
        {
            if (e.Source is Control c && c.Name == "MoveTarget")
            {
                e.DragEffects &= (DragDropEffects.Move);
            }
            else
            {
                e.DragEffects &= (DragDropEffects.Copy);
            }
            if (e.Data.Contains(DataFormats.FileNames))
            {
                ProcessFiles(e.Data.GetFileNames());
            }
        }

        private void ProcessFiles(IEnumerable<string> files)
        {
            foreach (var file in files)
            {
                AddSong(new Song(file, file, Guid.NewGuid()));
            }
            TreeView.Items = Songs;

            TreeView.InvalidateVisual();
            Debug.WriteLine(Songs.Count);
        }

        public void ClearAll(object sender, RoutedEventArgs e)
        {
            Songs.Clear();
        }

        public void RemoveSelected(object sender, RoutedEventArgs e)
        {
            foreach (var selected in TreeView.SelectedItems)
            {
                Songs.Remove((Song)selected);
            }
        }

        private void AddSong(Song s)
        {
            Songs.Add(s);
        }
    }
}
using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ReactiveUI;

namespace SilverAudioPlayer.Avalonia;
public class ComandPaletteWindowDataContext : ReactiveObject
{
    public AvaloniaList<ICommandPaletteItem> Items{get;set;}=new();
}
public partial class CommandPalette : Window
{
    ComandPaletteWindowDataContext dc=new();
    public CommandPalette()
    {
        InitializeComponent();
        DataContext=dc;
        SearchBox.TextChanged+=TextChanged;
        this.DoAfterInitTasksF();
         if (OperatingSystem.IsLinux())
        {
            MainGrid.RowDefinitions[0].Height = GridLength.Parse("0");
        }
    }
        public void ElementDoubleTapped(object _, global::Avalonia.Input.TappedEventArgs args)
    {
        ((ICommandPaletteItem?)List.SelectedItem)?.Execute(App.mainWindow.Env);
    }
    private void TextChanged(object? sender, TextChangedEventArgs e)
    {
       var a= App.ComandPalette.Search(SearchBox.Text);
dc.Items.Clear();
dc.Items.AddRange(a);
    }
}
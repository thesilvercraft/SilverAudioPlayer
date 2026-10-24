using System;
using Avalonia.Collections;
using Avalonia.Controls;
using ReactiveUI;
using SilverAudioPlayer.Shared;

namespace Catenoid;
public class CommandPaletteWindowDataContext : ReactiveObject
{
    public AvaloniaList<ICommandPaletteItem> Items { get; set; } = new();
}
public partial class CommandPalette : Window
{
    readonly CommandPaletteWindowDataContext dc = new();
    public CommandPalette()
    {
        InitializeComponent();
        DataContext = dc;
        SearchBox.TextChanged += TextChanged;
        this.DoAfterInitTasksF();
    }
    public void ElementDoubleTapped(object _, Avalonia.Input.TappedEventArgs args)
    {
        if (List.SelectedItem is ICommandPaletteItem item)
        {
            item.Execute(App.mainWindow.Env);
        }
    }
    private void TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (SearchBox.Text != null)
        {
            var a = App.CommandPalette.Search(SearchBox.Text);
            dc.Items.Clear();
            dc.Items.AddRange(a);
        }
    }
}
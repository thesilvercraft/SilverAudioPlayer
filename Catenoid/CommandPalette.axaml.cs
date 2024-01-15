using System;
using Avalonia.Collections;
using Avalonia.Controls;
using ReactiveUI;

namespace Catenoid;
public class ComandPaletteWindowDataContext : ReactiveObject
{
    public AvaloniaList<ICommandPaletteItem> Items { get; set; } = new();
}
public partial class CommandPalette : Window
{
    readonly ComandPaletteWindowDataContext dc = new();
    public CommandPalette()
    {
        InitializeComponent();
        DataContext = dc;
        SearchBox.TextChanged += TextChanged;
        this.DoAfterInitTasksF();
        if (OperatingSystem.IsLinux())
        {
            MainGrid.RowDefinitions[0].Height = GridLength.Parse("0");
        }
    }
    public void ElementDoubleTapped(object _, Avalonia.Input.TappedEventArgs args)
    {
        ((ICommandPaletteItem?)List.SelectedItem)?.Execute(App.mainWindow.Env);
    }
    private void TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (SearchBox.Text != null)
        {
            var a = App.ComandPalette.Search(SearchBox.Text);
            dc.Items.Clear();
            dc.Items.AddRange(a);
        }
    }
}
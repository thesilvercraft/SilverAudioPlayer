using System;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Input;
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
        KeyDown += OnWindowKeyDown;
        RefreshItems(string.Empty);
        Opened += (_, _) => SearchBox.Focus();
        this.DoAfterInitTasksF();
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            var item = List.SelectedItem as ICommandPaletteItem ?? (dc.Items.Count > 0 ? dc.Items[0] : null);
            ExecuteItem(item);
            e.Handled = true;
        }
    }

    private void ExecuteItem(ICommandPaletteItem? item)
    {
        if (item == null || App.mainWindow?.Env is not { } env) return;
        item.Execute(env);
        Close();
    }

    public void ElementDoubleTapped(object? _, TappedEventArgs args)
    {
        if (List.SelectedItem is ICommandPaletteItem item)
        {
            ExecuteItem(item);
        }
    }

    private void RefreshItems(string query)
    {
        var results = App.CommandPalette.Search(query);
        dc.Items.Clear();
        dc.Items.AddRange(results);
        if (dc.Items.Count > 0)
        {
            List.SelectedIndex = 0;
        }
    }

    private void TextChanged(object? sender, TextChangedEventArgs e)
    {
        RefreshItems(SearchBox.Text ?? string.Empty);
    }
}
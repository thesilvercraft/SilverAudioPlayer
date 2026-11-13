using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using SilverCraft.AvaloniaUtils;
using SilverCraft.Ekorensis;

namespace SilverCraft.Ekonrensis.Avalonia;

public partial class EkonrensisWindow : Window, IConfigurableWindow
{
    public EkonrensisWindow()
    {
        InitializeComponent();
        this.DoAfterInitTasks(true);
        MainGrid = this.FindControl<StackPanel>("MainGrid");
    }

    public Control GetControl(IConfigurableElement element)
    {
        switch (element)
        {
            case IConfigurableCheckBox checkBox:
            {
                CheckBox cb = new()
                {
                    Content = checkBox.Content,
                    IsChecked = checkBox.Toggled,
                    VerticalAlignment = VerticalAlignment.Center
                };
                cb.IsCheckedChanged += (_, _) => checkBox.Toggled = (bool)(cb.IsChecked ?? false);
                return cb;
            }
            case IConfigurableTextBox textBox:
            {
                TextBox tb = new()
                {
                    Text = textBox.Content,
                    PlaceholderText = textBox.Placeholder,
                    VerticalAlignment = VerticalAlignment.Center
                };
                tb.TextChanged += (_, _) => { textBox.Content = tb.Text ?? string.Empty; };
                return tb;
            }
            case IConfigurableLabel label:
            {
                Label tb = new()
                {
                    Content = label.Content,
                    VerticalAlignment = VerticalAlignment.Center
                };
                return tb;
            }
            case IConfigurableDropDown dropDown:
            {
                ComboBox cb = new()
                {
                    ItemsSource = dropDown.Options,
                    SelectedItem = dropDown.Selection,
                    PlaceholderText = dropDown.Placeholder,
                    VerticalAlignment = VerticalAlignment.Center
                };
                cb.SelectionChanged += (_, _) =>
                {
                    if (cb.SelectedItem != null)
                    {
                        dropDown.Selection = cb.SelectedItem.ToString() ?? string.Empty;
                    }
                };
                return cb;
            }
            case IConfigurableNumericUpDown numericUpDown:
            {
                NumericUpDown nud = new()
                {
                    Minimum = numericUpDown.Minimum,
                    Maximum = numericUpDown.Maximum,
                    Increment = numericUpDown.Increment,
                    Value = numericUpDown.Value,
                    FormatString = numericUpDown.DecimalPlaces > 0
                        ? $"0.{new string('0', numericUpDown.DecimalPlaces)}"
                        : "0",
                    VerticalAlignment = VerticalAlignment.Center
                };
                nud.ValueChanged += (_, _) =>
                {
                    if (nud.Value.HasValue)
                    {
                        numericUpDown.Value = nud.Value.Value;
                    }
                };
                return nud;
            }
            case IConfigurableButton button:
            {
                Button cb = new()
                {
                    Content = button.Content,
                    VerticalAlignment = VerticalAlignment.Center
                };
                cb.Click += (_, _) => button.Click();
                return cb;
            }
            case IConfigurableRow row:
            {
                StackPanel wrapPanel = new()
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    VerticalAlignment = VerticalAlignment.Center
                };
                foreach (var childElement in row.Content)
                {
                    wrapPanel.Children.Add(GetControl(childElement));
                }

                return wrapPanel;
            }
            case IConfigurableGallery gallery:
            {
                ListBox lb = new()
                {
                    ItemsSource = gallery.Items
                };
                lb.DoubleTapped += (_, _) =>
                {
                    if (lb.SelectedItem is IGalleryItem item)
                    {
                        gallery.DoubleTapped(item);
                    }
                };
                return lb;
            }
            default:
                throw new NotSupportedException($"Unknown config element type: {element.GetType().FullName}");
        }
    }

    public void HandleConfiguration(IEnumerable<IConfigurableElement> elements)
    {
        if (MainGrid == null) return;
        foreach (var element in elements) MainGrid.Children.Add( GetControl(element));
    }

    public void HandleConfiguration(IAmConfigurable configurable)
    {
        HandleConfiguration(configurable.GetElements());
    }

    public void SetTitle(string title)
    {
        Dispatcher.UIThread.Invoke(() => { Title = title; });
    }
}
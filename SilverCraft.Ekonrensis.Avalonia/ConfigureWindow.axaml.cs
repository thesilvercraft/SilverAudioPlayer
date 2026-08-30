using System;
using System.Collections.Generic;
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
                        IsChecked = checkBox.Toggled
                    };
                    cb.IsCheckedChanged += (_, _) => checkBox.Toggled = (bool)cb.IsChecked;
                    return cb;
                }
            case IConfigurableTextBox textBox:
                {
                    TextBox tb = new()
                    {
                        Text = textBox.Content,
                        PlaceholderText = textBox.Placeholder
                    };
                    tb.TextChanged += (_, _) =>
                    {
                        textBox.Content = tb.Text;
                    };
                    return tb;
                }
            case IConfigurableLabel label:
                {
                    Label tb = new()
                    {
                        Content = label.Content,
                    };
                    return tb;
                }
            case IConfigurableDropDown dropDown:
                {
                    ComboBox cb = new()
                    {
                        ItemsSource = dropDown.Options,
                        SelectedItem = dropDown.Selection
                    };
                    cb.SelectionChanged += (_, _) => dropDown.Selection =  (string)cb.SelectedItem;
                    cb.PlaceholderText = dropDown.Placeholder;
                    return cb;
                }
            case IConfigurableButton button:
                {
                    Button cb = new()
                    {
                        Content = button.Content
                    };
                    cb.Click += (_, _) => button.Click();
                    return cb;
                }
            case IConfigurableRow row:
                {
                    StackPanel wrapPanel = new()
                    {
                        Orientation = Orientation.Horizontal
                    };
                    foreach (var childElement in row.Content)
                    {
                        wrapPanel.Children.Add(GetControl(childElement));
                    }
                    return wrapPanel;
                }
            default:
                throw new NotSupportedException("Unknown config type");
        }
    }
    public void HandleConfiguration(IEnumerable<IConfigurableElement> elements)
    {
        foreach (var element in elements) MainGrid.Children.Add(GetControl(element));
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
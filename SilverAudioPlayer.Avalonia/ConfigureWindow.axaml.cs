using Avalonia.Controls;
using Avalonia.Layout;
using DynamicData;
using System;
using SilverAudioPlayer.Shared.ConfigScreen;

namespace SilverAudioPlayer.Avalonia
{
    public partial class ConfigureWindow : Window
    {
        public ConfigureWindow()
        {
            InitializeComponent();
            this.DoAfterInitTasks(true);
            MainGrid = this.FindControl<StackPanel>("MainGrid");

        }
        public Control GetControl( IConfigurableElement element)
        {
            if (element is IConfigurableCheckBox checkBox)
            {
                CheckBox cb = new()
                {
                    Content = checkBox.Content,
                    IsChecked = checkBox.Toggled
                };
                cb.Checked += (x, y) => checkBox.Toggled = (bool)cb.IsChecked;
                return cb;
            }
            else if (element is IConfigurableTextBox textBox)
            {
                TextBox tb = new()
                {
                    Text = textBox.Content,
                    Watermark = textBox.Placeholder
                };
                tb.TextInput += (x, y) => textBox.Content = tb.Text;
                return tb;
            }
            else if(element is IConfigurableDropDown dropDown)
            {
                ComboBox cb = new()
                {
                    Items = dropDown.Options,
                    SelectedItem = dropDown.Selection
                };
                cb.SelectionChanged += (x, y) => dropDown.Selection = (string)cb.SelectedItem;
                cb.PlaceholderText = dropDown.Placeholder;
                return cb;
            }
            else if(element is IConfigurableButton button)
            {
                Button cb = new();
                cb.Content = button.Content;
                cb.Click += (x, y) => button.Click();
                return cb;
            }
            else if(element is IConfigurableRow row)
            {

                StackPanel wrapPanel = new();
                wrapPanel.Orientation = Orientation.Horizontal;
                foreach(var elemet in row.Content)
                {
                    wrapPanel.Children.Add(GetControl(elemet));
                }
                return wrapPanel;
            }
            throw new NotSupportedException("Unknown config type");
        }
        public void HandleConfiguration(IAmConfigurable configurable)
        {
            var g = configurable.GetElements();
            foreach(var element in g)
            {
                    MainGrid.Children.Add(GetControl(element));

            }
        }
    }
}

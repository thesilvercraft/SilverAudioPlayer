using Avalonia.Controls;
using Avalonia.Interactivity;
using DynamicData;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive.Linq;

namespace SilverAudioPlayer.Avalonia
{
    public class SAction
    {
        public string ActionName { get; set; }
        public void Invoke()
        {
            ActionToInvoke?.Invoke();
        }
        public Action ActionToInvoke { get; set; }
    }
    public class ActionDataContext
    {
        public ObservableCollection<SAction> Actions { get; set; } = new();
    }
    public partial class LaunchActionsWindow : Window
    {
        ActionDataContext x;
        public LaunchActionsWindow()
        {
            InitializeComponent();
            x = new ();
            DataContext = x;
            LB = this.FindControl<ListBox>("LB");
            this.DoAfterInitTasks(true);
        }

        public void ElementDoubleTapped(object _, RoutedEventArgs args)
        {
            ((SAction?)LB.SelectedItem)?.Invoke();
        }

        public void AddActions(List<SAction> action)
        {
            x.Actions.AddRange(action);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using DynamicData;

namespace Catenoid;

public class SAction
{
    public string ActionName { get; set; }
    public Action ActionToInvoke { get; set; }

    public void Invoke()
    {
        ActionToInvoke?.Invoke();
    }
}

public class ActionDataContext
{
    public ObservableCollection<SAction> Actions { get; set; } = [];
}

public partial class LaunchActionsWindow : Window
{
    private readonly ActionDataContext x;

    public LaunchActionsWindow()
    {
        InitializeComponent();
        x = new ActionDataContext();
        DataContext = x;
        LB = this.FindControl<ListBox>("LB");
        this.DoAfterInitTasksF();
    }

    public void ElementDoubleTapped(object _, global::Avalonia.Input.TappedEventArgs args)
    {
        ((SAction?)LB.SelectedItem)?.Invoke();
    }

    public void AddActions(List<SAction> action)
    {
        x.Actions.AddRange(action);
    }
}
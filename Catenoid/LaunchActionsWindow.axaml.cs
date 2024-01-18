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
    private readonly ActionDataContext x = new ActionDataContext();

    public LaunchActionsWindow()
    {
        InitializeComponent();
        DataContext = x;
        LB = this.FindControl<ListBox>("LB");
        this.DoAfterInitTasksF();
    }
    public LaunchActionsWindow(List<SAction> action) :this()
    {
        x.Actions.AddRange(action);
    }

    public void ElementDoubleTapped(object _, global::Avalonia.Input.TappedEventArgs args)
    {
        ((SAction?)LB.SelectedItem)?.Invoke();
    }

   
}
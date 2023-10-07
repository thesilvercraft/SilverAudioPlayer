using System;

namespace SilverCraft.Ekorensis;

public class SimpleButton : IConfigurableButton
{
    public Action Clicked;
    public Func<string> GetContent;
    public string Content => GetContent();

    public void Click()
    {
        Clicked();
    }
}
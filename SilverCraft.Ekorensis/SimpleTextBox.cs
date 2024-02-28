using System;

namespace SilverCraft.Ekorensis;

public class SimpleTextBox : IConfigurableTextBox
{
    public Func<string> GetContent;
    public Func<string> GetPlaceholder;
    public Action<string> SetContent;

    public string Placeholder => GetPlaceholder();

    public string Content
    {
        get => GetContent();
        set => SetContent(value);
    }
}

public class SimpleLabel : IConfigurableLabel
{
    public Func<string> GetContent;
    public string Content
    {
        get => GetContent();
    }
}
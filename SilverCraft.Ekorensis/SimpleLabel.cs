using System;

namespace SilverCraft.Ekorensis;

public class SimpleLabel : IConfigurableLabel
{
    public SimpleLabel() { }

    public SimpleLabel(string content)
    {
        _content = content;
    }

    public SimpleLabel(Func<string> getContent)
    {
        GetContent = getContent;
    }

    public Func<string>? GetContent;

    private string _content = string.Empty;
    public string Content
    {
        get => GetContent != null ? GetContent() : _content;
        set => _content = value;
    }
}

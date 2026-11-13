using System;

namespace SilverCraft.Ekorensis;

public class SimpleButton : IConfigurableButton
{
    public SimpleButton()
    {
    }

    public SimpleButton(string content, Action? clicked = null)
    {
        _content = content;
        Clicked = clicked;
    }

    public SimpleButton(Func<string> getContent, Action? clicked = null)
    {
        GetContent = getContent;
        Clicked = clicked;
    }

    public Action? Clicked;
    public Func<string>? GetContent;

    private string _content = string.Empty;

    public string Content
    {
        get => GetContent != null ? GetContent() : _content;
        set => _content = value;
    }

    public void Click()
    {
        Clicked?.Invoke();
    }
}
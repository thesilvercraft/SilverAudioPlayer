using System;

namespace SilverCraft.Ekorensis;

public class SimpleTextBox : IConfigurableTextBox
{
    public SimpleTextBox()
    {
    }

    public SimpleTextBox(string content, string placeholder = "", Action<string>? setContent = null)
    {
        _content = content;
        _placeholder = placeholder;
        SetContent = setContent;
    }

    public SimpleTextBox(Func<string> getContent, Func<string>? getPlaceholder = null,
        Action<string>? setContent = null)
    {
        GetContent = getContent;
        GetPlaceholder = getPlaceholder;
        SetContent = setContent;
    }

    public Func<string>? GetContent;
    public Func<string>? GetPlaceholder;
    public Action<string>? SetContent;

    private string _placeholder = string.Empty;

    public string Placeholder
    {
        get => GetPlaceholder != null ? GetPlaceholder() : _placeholder;
        set => _placeholder = value;
    }

    private string _content = string.Empty;

    public string Content
    {
        get => GetContent != null ? GetContent() : _content;
        set
        {
            _content = value;
            SetContent?.Invoke(value);
        }
    }
}
using System;

namespace SilverCraft.Ekorensis;

public class SimpleCheckBox : IConfigurableCheckBox
{
    public SimpleCheckBox()
    {
    }

    public SimpleCheckBox(string content, bool toggled = false, Action<bool>? onChecked = null)
    {
        _content = content;
        _toggled = toggled;
        Checked = onChecked;
    }

    public SimpleCheckBox(Func<string> getContent, Func<bool> getChecked, Action<bool>? onChecked = null)
    {
        GetContent = getContent;
        GetChecked = getChecked;
        Checked = onChecked;
    }

    public Action<bool>? Checked;
    public Func<bool>? GetChecked;
    public Func<string>? GetContent;

    private string _content = string.Empty;

    public string Content
    {
        get => GetContent != null ? GetContent() : _content;
        set => _content = value;
    }

    private bool _toggled;

    public bool Toggled
    {
        get => GetChecked != null ? GetChecked() : _toggled;
        set
        {
            _toggled = value;
            Checked?.Invoke(value);
        }
    }
}
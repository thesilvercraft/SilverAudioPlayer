using System;

namespace SilverCraft.Ekorensis;

public class SimpleDropDown : IConfigurableDropDown
{
    public SimpleDropDown()
    {
    }

    public SimpleDropDown(string[] options, string selection = "", string placeholder = "",
        Action<string>? setSelection = null)
    {
        _options = options;
        _selection = selection;
        _placeholder = placeholder;
        SetSelection = setSelection;
    }

    public SimpleDropDown(Func<string[]> getOptions, Func<string>? getSelection = null,
        Func<string>? getPlaceholder = null, Action<string>? setSelection = null)
    {
        GetOptions = getOptions;
        GetSelection = getSelection;
        GetPlaceholder = getPlaceholder;
        SetSelection = setSelection;
    }

    public Func<string[]>? GetOptions;
    public Func<string>? GetPlaceholder;
    public Func<string>? GetSelection;
    public Action<string>? SetSelection;

    private string _placeholder = string.Empty;

    public string Placeholder
    {
        get => GetPlaceholder != null ? GetPlaceholder() : _placeholder;
        set => _placeholder = value;
    }

    private string _selection = string.Empty;

    public string Selection
    {
        get => GetSelection != null ? GetSelection() : _selection;
        set
        {
            _selection = value;
            SetSelection?.Invoke(value);
        }
    }

    private string[] _options = [];

    public string[] Options
    {
        get => GetOptions != null ? GetOptions() : _options;
        set => _options = value;
    }
}
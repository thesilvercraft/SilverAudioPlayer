namespace SilverAudioPlayer.Shared.ConfigScreen;

/// <summary>
///     A checkbox
///     <seealso href="https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.checkbox?view=windowsdesktop-6.0" />
/// </summary>
public interface IConfigurableCheckBox : IConfigurableElement
{
    public string Content { get; }
    public bool Toggled { get; set; }
}
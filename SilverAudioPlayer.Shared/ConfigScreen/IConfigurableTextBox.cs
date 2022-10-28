namespace SilverAudioPlayer.Shared.ConfigScreen;

/// <summary>
///     A text box
///     <seealso href="https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.textbox?view=windowsdesktop-7.0" />
/// </summary>
public interface IConfigurableTextBox : IConfigurableElement
{
    public string Placeholder { get; }
    public string Content { get; set; }
}
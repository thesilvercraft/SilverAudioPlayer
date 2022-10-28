namespace SilverAudioPlayer.Shared.ConfigScreen;

/// <summary>
///     A drop down
///     <seealso
///         href="https://learn.microsoft.com/en-us/dotnet/api/system.windows.controls.combobox?view=windowsdesktop-7.0" />
/// </summary>
public interface IConfigurableDropDown : IConfigurableElement
{
    public string Placeholder { get; }
    public string Selection { get; set; }
    public string[] Options { get; }
}
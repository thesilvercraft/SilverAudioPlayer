namespace SilverAudioPlayer.Shared.ConfigScreen
{
    /// <summary>
    /// A push button <seealso href="https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.button?view=windowsdesktop-6.0"/>
    /// </summary>
    public interface IConfigurableButton : IConfigurableElement
    {
        public string Content { get; }
        public void Click();
    }
}
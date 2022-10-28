namespace SilverAudioPlayer.Shared.ConfigScreen;

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
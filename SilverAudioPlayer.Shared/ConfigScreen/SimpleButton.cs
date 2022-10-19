namespace SilverAudioPlayer.Shared.ConfigScreen
{
    public class SimpleButton : IConfigurableButton
    {
        public Action Clicked;
        public Func<string> GetContent;
        public string Content => GetContent();
        public void Click()
        {
            Clicked();
        }
    }
}
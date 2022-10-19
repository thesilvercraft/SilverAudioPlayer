namespace SilverAudioPlayer.Shared.ConfigScreen
{
    public class SimpleCheckBox : IConfigurableCheckBox
    {
        public Action<bool> Checked;
        public Func<bool> GetChecked;
        public Func<string> GetContent;

        public string Content => GetContent();

        public bool Toggled { get => GetChecked(); set => Checked(value); }
    }
}
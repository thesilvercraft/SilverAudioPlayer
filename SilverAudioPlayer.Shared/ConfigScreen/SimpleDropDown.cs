namespace SilverAudioPlayer.Shared.ConfigScreen
{
    public class SimpleDropDown : IConfigurableDropDown
    {
        public Func<string> GetPlaceholder;
        public Func<string> GetSelection;
        public Action<string> SetSelection;
        public Func<string[]> GetOptions;
        public string Placeholder => GetPlaceholder();
        public string Selection { get => GetSelection(); set => SetSelection(value); }
        public string[] Options => GetOptions();
    }
}
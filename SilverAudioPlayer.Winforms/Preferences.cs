using SilverConfig;

namespace SilverAudioPlayer
{
    public class Preferences
    {
        [Comment("Does the Form attempt to process WndProc messages?")]
        public bool ProcessMessages { get; set; } = true;

        [Comment("Does the player immidiatly play after selection")]
        public bool PlayAfterSelect { get; set; } = true;

        [Comment("Does the player play the same song all the time")]
        public bool LoopSong { get; set; } = false;

        [Comment("Does the player push back the song to the end of the queue when it has ended playing the song")]
        public bool LoopQueueDestructive { get; set; } = false;

        [Comment("Colour of progress bar The byte-ordering of the 32-bit ARGB value is AARRGGBB. The most significant byte (MSB), represented by AA, is the alpha component value. The second, third, and fourth bytes, represented by RR, GG, and BB, respectively, are the color components red, green, and blue, respectively. https://docs.microsoft.com/en-us/dotnet/api/system.drawing.color.fromargb?view=net-6.0")]
        public int ProgressBarColour { get; set; } = Color.Cyan.ToArgb();

        [Comment("Does the progress bar look like a rainbow")]
        public bool ProgressBarRainbow { get; set; } = false;

        [Comment("Should the rainbow be cached and to what extent")]
        public byte ProgressBarRainbowCaching { get; set; } = 0;

        [Comment("Should the rainbow be shifting?")]
        public byte ProgressBarRainbowShift { get; set; } = 0;

        [Comment("Should the player check for metadata in the start playing method?")]
        public bool CheckForMetadataInSP { get; set; } = true;

        [Comment("How often should the config be automatically saved with current settings?")]
        public ulong MillisecondIntervalOfAutoSave { get; set; } = 300000;

        [Comment("Volume, 0-100% (linearity not guaranteed)")]
        public byte Volume { get; set; } = 70;

        [Comment("Should the player read the metadata of files when they are added to the queue? also enables sorting by track number")]
        public bool FillMetadataOfLoadedFilesOnLoad { get; set; } = true;

        [Comment("Should the player read args.txt so that it can load files from other instances? (like vlc does but worse)")]
        public bool AutoMagicallyLoadFromArgstxt { get; set; } = true;

        [Comment("Should the player react to media controls")]
        public bool HandleMediaControls { get; set; } = Environment.OSVersion.Version.Major <= 6 && Environment.OSVersion.Version.Minor <= 1;
    }
}
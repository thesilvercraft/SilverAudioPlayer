namespace SilverAudioPlayer
{
    public partial class TimedProgressBar : UserControl
    {
        public TimedProgressBar()
        {
            InitializeComponent();
        }

        public bool LabelsVisible { get => LabelElapsed.Visible; set => SetVisible(value); }

        private void SetVisible(bool val)
        {
            LabelElapsed.Visible = val;
            LabelEnd.Visible = val;
        }

        public string LabelTimeFormat { get; set; } = "g";
        private TimeSpan _max { get; set; } = new TimeSpan(1);
        public TimeSpan Max { get => _max; set => SetMax(value); }
        private TimeSpan _min { get; set; } = TimeSpan.FromSeconds(0);
        public TimeSpan Min { get => _min; set => SetMin(value); }
        private TimeSpan _pos { get; set; } = TimeSpan.FromSeconds(0);
        public TimeSpan Pos { get => _pos; set => SetPos(value); }

        private void SetMax(TimeSpan max)
        {
            if (max < Min)
            {
                throw new InvalidOperationException("TimedProgressBar.Max may not be smaller then TimedProgressBar.Min");
            }
            _max = max;
            ProgressBar.Maximum = (ulong)max.TotalMilliseconds;
            if (LabelsVisible)
            {
                LabelEnd.Text = max.ToString(LabelTimeFormat);
            }
        }

        private void SetMin(TimeSpan min)
        {
            if (min > Max)
            {
                throw new InvalidOperationException("TimedProgressBar.Min may not be larger then TimedProgressBar.Max");
            }
            _min = min;
            ProgressBar.Minimum = (ulong)min.TotalMilliseconds;
        }

        private void SetPos(TimeSpan pos)
        {
            if (pos < Min)
            {
                throw new InvalidOperationException("TimedProgressBar.Pos may not be smaller then TimedProgressBar.Min");
            }
            if (pos > Max)
            {
                throw new InvalidOperationException("TimedProgressBar.Pos may not be larger then TimedProgressBar.Max");
            }
            _pos = pos;
            ProgressBar.Value = (ulong)pos.TotalMilliseconds;
            if (LabelsVisible)
            {
                LabelElapsed.Text = pos.ToString(LabelTimeFormat);
            }
        }
    }
}
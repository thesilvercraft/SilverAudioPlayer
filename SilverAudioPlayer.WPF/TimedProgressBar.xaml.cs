using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SilverAudioPlayer.WPF
{
    /// <summary>
    /// Interaction logic for TimedProgressBar.xaml
    /// </summary>
    public partial class TimedProgressBar : UserControl
    {
        public TimedProgressBar()
        {
            InitializeComponent();
        }

        public bool LabelsVisible { get => LeftLabel.Visibility == Visibility.Visible; set => SetVisible(value); }

        private void SetVisible(bool val)
        {
            LeftLabel.Visibility = val ? Visibility.Hidden : Visibility.Visible;
            RightLabel.Visibility = val ? Visibility.Hidden : Visibility.Visible;
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
                RightLabel.Content = max.ToString(LabelTimeFormat);
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
                LeftLabel.Content = pos.ToString(LabelTimeFormat);
            }
        }
    }
}
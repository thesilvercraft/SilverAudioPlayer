using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    /// Interaction logic for LameProgressBar.xaml
    /// </summary>
    public partial class LameProgressBar : UserControl
    {
        public LameProgressBar()
        {
            InitializeComponent();
        }

        public ulong Minimum { get => _Minimum; set => SetMin(value); }
        private ulong _Minimum = 0;
        public ulong Maximum { get => _Maximum; set => SetMax(value); }
        private ulong _Maximum = 100;

        public ulong Value { get => _Value; set => SetVal(value); }
        private ulong _Value = 0;

        private void SetMax(ulong v)
        {
            if (v != _Maximum)
            {
                _Maximum = v;
                OnValueChange();
            }
        }

        private void SetMin(ulong v)
        {
            if (v != _Minimum)
            {
                _Minimum = v;
                OnValueChange();
            }
        }

        private void SetVal(ulong v)
        {
            if (v != _Value)
            {
                _Value = v;
                OnValueChange();
            }
        }

        private void OnValueChange()
        {
            var progress = (Maximum - Minimum) / ((double)Value - Minimum);
            DaRectangle.Width = this.ActualWidth / progress;
            Debug.WriteLine(progress);
            Debug.WriteLine(this.ActualWidth);
            Debug.WriteLine(DaRectangle.ActualWidth);

            InvalidateVisual();
        }
    }
}
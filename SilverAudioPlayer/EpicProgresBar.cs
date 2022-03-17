﻿using System;
using System.Drawing;
using System.Windows.Forms;

namespace SilverAudioPlayer
{
    public partial class EpicProgresBar : UserControl
    {
        public ulong Minimum { get => _Minimum; set => SetMin(value); }
        private ulong _Minimum = 0;
        public ulong Maximum { get => _Maximum; set => SetMax(value); }
        private ulong _Maximum = 100;

        public ulong Value { get => _Value; set => SetVal(value); }
        private ulong _Value = 0;

        public Color Color { get; set; } = Color.Red;
        public bool Rainbow { get; set; } = true;
        public int CacheRainbowDecimals { get; set; } = 3;

        private void SetMax(ulong v)
        {
            if (v != _Maximum)
            {
                _Maximum = v;
                Invalidate();
            }
        }

        private void SetMin(ulong v)
        {
            if (v != _Minimum)
            {
                _Minimum = v;
                Invalidate();
            }
        }

        private void SetVal(ulong v)
        {
            if (v != _Value)
            {
                _Value = v;
                Invalidate();
            }
        }

        public EpicProgresBar()
        {
            InitializeComponent();
        }

        public static Color RainbowC(double progress)
        {
            float div = ((float)(Math.Abs(progress % 1) * 6));
            int ascending = (int)((div % 1) * 255);
            int descending = 255 - ascending;

            return (int)div switch
            {
                0 => Color.FromArgb(255, 255, ascending, 0),
                1 => Color.FromArgb(255, descending, 255, 0),
                2 => Color.FromArgb(255, 0, 255, ascending),
                3 => Color.FromArgb(255, 0, descending, 255),
                4 => Color.FromArgb(255, ascending, 0, 255),
                _ => Color.FromArgb(255, 255, 0, descending),
            };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.Clear(base.BackColor);
            var progress = (Maximum - Minimum) / ((double)Value - Minimum);
            if (Rainbow)
            {
                if (CacheRainbowDecimals <= 0)
                {
                    for (var a = 1; a < (int)(Width / progress); a++)
                    {
                        e.Graphics.FillRectangle(new SolidBrush(RainbowC((double)a / Width)), new(a - 1, 0, 1, Height));
                    }
                }
                else
                {
                    Color? prevcol = null;
                    double prevprog = 0;
                    for (var a = 1; a < (int)(Width / progress); a++)
                    {
                        double prog = (double)a / Width;
                        if (Math.Round(prog, CacheRainbowDecimals) == Math.Round(prevprog, CacheRainbowDecimals) && prevprog != 0 && prevcol != null)
                        {
                            e.Graphics.FillRectangle(new SolidBrush((Color)prevcol), new(a - 1, 0, 1, Height));
                        }
                        else
                        {
                            prevprog = prog;
                            prevcol = RainbowC(prog);
                            e.Graphics.FillRectangle(new SolidBrush((Color)prevcol), new(a - 1, 0, 1, Height));
                        }
                    }
                }
            }
            else
            {
                e.Graphics.FillRectangle(new SolidBrush(Color), new(0, 0, (int)(Width / progress), Height));
            }
        }
    }
}
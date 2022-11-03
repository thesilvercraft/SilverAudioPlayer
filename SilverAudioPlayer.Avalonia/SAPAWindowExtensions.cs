using Avalonia.Controls;
using Avalonia.Media;
using SilverCraft.AvaloniaUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SilverAudioPlayer.Avalonia
{
    public static class SAPAWindowExtensions
    {
        static GradientStops defBGStops = new()
        {
            new(Color.FromUInt32(0x7F6969ff), 0),
            new(Color.FromUInt32(0x7F696969), 1)
        };
        public static void DoAfterInitTasksF(this Window w)
        {
            var b = new LinearGradientBrush();
            b.GradientStops=defBGStops;
            w.DoAfterInitTasks(true, b);
        }
    }
}

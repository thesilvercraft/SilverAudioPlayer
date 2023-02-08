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
        public static LinearGradientBrush defBrush = new LinearGradientBrush
        {
            GradientStops = defBGStops
        };
        public static void DoAfterInitTasksF(this Window w)
        {
            if(WindowExtensions.envBackend.GetBool("SAPDoNotDoInitTasks")==true)
            {
                return;
            }
           
            w.DoAfterInitTasks(true, defBrush);
        }
    }
}

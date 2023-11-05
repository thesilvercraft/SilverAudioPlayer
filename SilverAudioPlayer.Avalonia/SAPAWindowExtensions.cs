using Avalonia.Controls;
using Avalonia.Media;
using SilverCraft.AvaloniaUtils;

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
        public static void DoAfterInitTasksF(this Window w, bool register=true, SubscriptionOptions options=null, bool followOptionsOnCall=true)
        {
            if(WindowExtensions.envBackend.GetBool("SAPDoNotDoInitTasks")==true)
            {
                return;
            }
            w.DoAfterInitTasks(register, defBackground:defBrush, options:options, followOptionsOnCall:followOptionsOnCall);
        }
    }
}

using System.Diagnostics;
using System.IO;
using Avalonia.Controls;
using SilverCraft.AvaloniaUtils;

namespace Catenoid
{
    public static class SAPAWindowExtensions
    {
        public static void DoAfterInitTasksF(this Window w, bool register=true, SubscriptionOptions? options=null, bool followOptionsOnCall=true)
        {
            if(WindowExtensions.envBackend.GetBool("SAPDoNotDoInitTasks")==true)
            {
                return;
            }
            w.DoAfterInitTasks(register, options:options, followOptionsOnCall:followOptionsOnCall);
        }
        public const string CopiedToClipboard="Copied to clipboard";
        public static void Toast(string content)
        {
            if(File.Exists("/bin/notify-send"))
            {
                Process.Start("notify-send", $"-a Catenoid -i sap.svg -u low \"{content}\"");
            }
        }
    }
}









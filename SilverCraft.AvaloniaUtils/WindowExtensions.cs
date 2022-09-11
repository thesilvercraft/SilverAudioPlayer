using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using System;

namespace SilverAudioPlayer.Avalonia
{
    public static class WindowExtensions
    {
        public static string? GetEnv(this string EnvvarName)
        {
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                return Environment.GetEnvironmentVariable(EnvvarName, EnvironmentVariableTarget.User);
            }
            return Environment.GetEnvironmentVariable(EnvvarName);
        }
        public static T? GetEnv<T>(this string EnvvarName) where T : struct
        {
            if (Enum.TryParse(GetEnv(EnvvarName), out T value2))
            {
                return value2;
            }
            return null;
        }
        public static void SetEnv(this string EnvvarName, string? Value)
        {
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                Environment.SetEnvironmentVariable(EnvvarName, Value, EnvironmentVariableTarget.User);
            }
            else
            {
                Environment.SetEnvironmentVariable(EnvvarName, Value);
            }
        }
        public static EventHandler<Tuple<bool, WindowTransparencyLevel, Color>> OnStyleChange;
        public static Color ReadColor(this string varname, Color? def= null)
        {
            var color = GetEnv(varname);
            if (color != null)
            {
                if (!Color.TryParse(color, out Color c))
                {
                    if (Enum.TryParse(color, out KnownColor kc))
                    {
                        c = kc.ToColor();
                    }
                    else
                    {
                        return def ?? Colors.Coral;
                    }
                }
                return c;
            }
            return def ?? Colors.Coral;

        }
        public static Color ToColor(this KnownColor kc)
        {
            return Color.FromUInt32((uint)kc);
        }
        public static void DoAfterInitTasks(this Window w, bool firstrun)
        {
            w.TransparencyLevelHint = GetEnv<WindowTransparencyLevel>("SAPTransparency") ?? WindowTransparencyLevel.AcrylicBlur;
            if(firstrun)
            {
                EventHandler<Tuple<bool, WindowTransparencyLevel, Color>> x = ( _, _) => {
                    Dispatcher.UIThread.InvokeAsync(() => w.DoAfterInitTasks(false));
                };
                OnStyleChange +=x;
                w.Closing += (_, _) => { if (OnStyleChange != null) { OnStyleChange -= x; } };
            }
            var color = ReadColor("SAPColor",def:Colors.Black);

                w.Background = new SolidColorBrush(color, GetEnv("DisableSAPTransparency") == "true" ? 1 : 0.3);

        }

    }
}
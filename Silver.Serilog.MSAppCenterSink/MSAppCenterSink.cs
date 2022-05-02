using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using Serilog.Core;
using Serilog.Events;
using System.Diagnostics;

namespace Silver.Serilog.MSAppCenterSink
{
    public class MSAppCenterSink : ILogEventSink
    {
        private string Shorten(string s, int length)
        {
            if (s.Length > length)
                return s[..(length - 3)] + "...";
            else
                return s;
        }

        private bool? CachedTrackingAllowed = null;
        private static readonly string optdir = Path.Combine(AppContext.BaseDirectory, "settings\\", "tracking\\");

        private string optout = Path.Combine(optdir, "donttrackevents.txt");
        private string optin = Path.Combine(optdir, "donttrackevents.txt");

        private bool TrackingAllowed()
        {
            if (!Directory.Exists(optdir))
            {
                Directory.CreateDirectory(optdir);
            }
            if (CachedTrackingAllowed != null)
            {
                return (bool)CachedTrackingAllowed;
            }
            if (File.Exists(optout))
            {
                CachedTrackingAllowed = false;
                return false;
            }
            if (File.Exists(optin))
            {
                CachedTrackingAllowed = true;
                return true;
            }
            File.WriteAllText(optout, "To opt into tracking just rename this file into trackevents.txt, to opt out rename it to donttrackevents.txt BUT WHATEVER YOU DO RELAUNCH THE APP");
            Debug.WriteLine(optout);
            Process.Start("explorer.exe", string.Format("/select,\"{0}\"", optout));
            Process.Start("notepad.exe", optout);

            return false;
        }

        public void Emit(LogEvent logEvent)
        {
            if (TrackingAllowed())
            {
                var message = logEvent.RenderMessage();
                var d = new Dictionary<string, string>();
                foreach (var thing in logEvent.Properties)
                {
                    d.Add(Shorten(thing.Key, 125), Shorten(thing.Value.ToString(), 125));
                }
                switch (logEvent.Level)
                {
                    case LogEventLevel.Information:
                    case LogEventLevel.Warning:
                        Analytics.TrackEvent(Shorten(message, 255), d);
                        break;

                    case LogEventLevel.Error:
                    case LogEventLevel.Fatal:
                        if (logEvent.Exception != null)
                            Crashes.TrackError(logEvent.Exception, d);
                        else
                            Crashes.TrackError(new Exception(message), d);
                        break;

                    default:
                        break;
                }
            }
        }
    }
}
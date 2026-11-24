using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DynamicData.Kernel;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;
using Silver.Composition;
using SilverAudioPlayer.Shared;
using SilverCraft.Shared;

namespace Catenoid;

[Serializable]
public class ProvidersReturnedNullException : Exception
{
    public ProvidersReturnedNullException()
    {
    }

    public ProvidersReturnedNullException(string message) : base(message)
    {
    }

    public ProvidersReturnedNullException(string message, Exception inner) : base(message, inner)
    {
    }

}

public class GuiSink : ILogEventSink
{
    private const int MaxBufferChars = 50_000;
    private const int TrimTargetChars = 40_000;
    private readonly Lock _lock = new();
    public StringBuilder writer = new();
    public event Action<string>? New;

    public string GetText()
    {
        lock (_lock)
        {
            return writer.ToString();
        }
    }

    public void Emit(LogEvent logEvent)
    {
        StringBuilder stringBuilder = new();
        switch (logEvent.Level)
        {
            case LogEventLevel.Verbose:
                stringBuilder.Append("[VER]");
                break;
            case LogEventLevel.Debug:
                stringBuilder.Append("[DBG]");
                break;
            case LogEventLevel.Information:
                stringBuilder.Append("[INFO]");
                break;
            case LogEventLevel.Warning:
                stringBuilder.Append("[WARN]");
                break;
            case LogEventLevel.Error:
                stringBuilder.Append("[ERROR]");
                break;
            case LogEventLevel.Fatal:
                stringBuilder.Append("[FATAL]");
                break;
            default:
                break;
        }
        stringBuilder.Append(' ');
        stringBuilder.Append(logEvent.Timestamp.ToString("R", CultureInfo.InvariantCulture));
        var context = logEvent.Properties.FirstOrOptional(x => x.Key == "SourceContext");
        if (context.HasValue && context.Value.Value is ScalarValue sv &&
            sv.Value is string rawValue)
        {
            stringBuilder.Append(" [");
            stringBuilder.Append(rawValue);
            stringBuilder.Append(']');
        }
        stringBuilder.Append(' ');

        if (logEvent.Exception != null)
        {
            stringBuilder.Append(' ');
            stringBuilder.Append(logEvent.Exception.GetType());
            stringBuilder.AppendLine();
            stringBuilder.Append(logEvent.Exception.Message);
        }

        stringBuilder.Append(' ');
        List<string> ignorelist = ["SourceContext"];
        stringBuilder.AppendLine(logEvent.RenderMessage());
        var included = logEvent.Properties
           .Where(p => !ignorelist.Contains(p.Key) && !TemplateContainsPropertyName(logEvent.MessageTemplate, p.Key))
           .Select(p => new LogEventProperty(p.Key, p.Value));
        if (included.Any())
        {
            stringBuilder.AppendLine(string.Join('\n', included.Select(x => x.Name + ": " + x.Value)));
        }
        var render = stringBuilder.ToString();
        lock (_lock)
        {
            writer.AppendLine(render);
            if (writer.Length > MaxBufferChars)
            {
                writer.Remove(0, writer.Length - TrimTargetChars);
            }
        }
        New?.Invoke(render);
    }
    static bool TemplateContainsPropertyName(MessageTemplate template, string propertyName)
    {
        foreach (var token in template.Tokens)
        {
            if (token is PropertyToken namedProperty &&
                namedProperty.PropertyName == propertyName)
            {
                return true;
            }
        }

        return false;
    }
}


public class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }
    public static Config mwconfig = new();
    public static CommentXmlConfigReaderNotifyWhenChanged<Config> mwconfreader = new();
    public static string _configPath = string.Empty;
    public static SilverAudioPlayer.Shared.CommandPalette CommandPalette = new();
    public static MainWindow? mainWindow;
    public static GuiSink sink = new();
    private static readonly BootupLogic BootupLogic = new();
    private static readonly Lock ConfigWriteLock = new();

    private static void ShowFatalKDialog(string detailedError)
    {
        if (!File.Exists(Program.KDialogLocation)) return;
        var psi = new ProcessStartInfo
        {
            FileName = Program.KDialogLocation,
            UseShellExecute = false
        };
        psi.ArgumentList.Add("--desktopfile");
        psi.ArgumentList.Add("silveraudioplayer.desktop");
        psi.ArgumentList.Add("--title");
        psi.ArgumentList.Add("Fatal error");
        psi.ArgumentList.Add("--detailederror");
        psi.ArgumentList.Add("An unexpected error occured \nPlease report this issue on gitlab.");
        psi.ArgumentList.Add(detailedError);
        using var proc = Process.Start(psi);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var services = BootupLogic.BuildServices();
            var configPathProvider = services.GetRequiredService<IConfigPathProvider>();
            _configPath = configPathProvider.GetPath("SilverAudioPlayer.Config.xml");
            mwconfreader = new CommentXmlConfigReaderNotifyWhenChanged<Config>();
            if (!File.Exists(_configPath)) mwconfreader.Write(new Config(), _configPath);
            mwconfig = mwconfreader.Read(_configPath) ?? new Config();
            if (mwconfig is INotifyPropertyChanged y)
            {
                y.PropertyChanged += (x, _) =>
                {
                    if (x != mwconfig || mwconfig is not ICanBeToldThatAPartOfMeIsChanged L) return;
                    lock (ConfigWriteLock)
                    {
                        if (!L.AllowedToRead) return;
                        L.AllowedToRead = false;
                        try
                        {
                            mwconfreader.Write(mwconfig, _configPath);
                        }
                        finally
                        {
                            L.AllowedToRead = true;
                        }
                    }
                };
            }
            if (mwconfig.UserLogPreferences == ConfigStyle.LogWindow)
            {
                LogWindow lw = new();
                lw.Show();
                BootupLogic.SetupCrashFallNets(exception =>
                {
                    var randomPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                    File.WriteAllText(randomPath, sink.GetText());
                    ShowFatalKDialog($"A more detailed account on what happened may be saved in {randomPath}\n{exception}");
                });
            }
            else
            {
                BootupLogic.SetupCrashFallNets(exception =>
                {
                    ShowFatalKDialog(exception.ToString());
                });
            }
            var logger = services.GetRequiredService<ILogger>();
            var mw = new MainWindow(services)
            {
                Logic =
                {
                    Log = logger,
                }
            };
            mainWindow = mw;
            Environment.SetEnvironmentVariable("BASEDIR", AppContext.BaseDirectory);
            desktop.MainWindow = mw;
            PlatformLogicHelper.SatisfyImports(mw.Logic,services);
            if (mw.Logic.PlayProviders == null)
                throw new ProvidersReturnedNullException("The 'mw.Logic.Providers' returned null.");
            mw.Logic.PlayableMimes = [];
            
            var canProvideMemory = mw.Logic.MemoryProvider != null;
            if (!canProvideMemory)
            {
                logger.Warning("Failed to find a memory provider, proceeding anyways");
            }

            foreach (var mimes in mw.Logic.PlayProviders.Select(provider => provider.SupportedMimes))
            {
                if (mimes != null)
                {
                    mw.Logic.PlayableMimes.AddRange(mimes);
                }
            }

            var playproviderloadtask = Task.Run(() => Task.WhenAll(
                mw.Logic.PlayProviders
                    .Where(provider => provider != null)
                    .Select(provider => provider.OnStartup())));
            if (canProvideMemory && mw is IAmOnceAgainAskingYouForYourMemory mainwindowMemory)
            {
                mw.Logic.MemoryProvider?.RegisterObjectsToRemember(mainwindowMemory.ObjectsToRememberForMe);
            }
            BootupLogic.SetupMemoryLogic(mw.Logic);
            BootupLogic.SetupCommandPalette(mw.Logic, CommandPalette, (item, memoryAsker, l) =>
            {
                var actions = memoryAsker.ObjectsToRememberForMe.Select(ob => l.GetConfig(ob.Id)).Select(m =>
                        new SAction
                        {
                            ActionName = "Open " + m,
                            ActionToInvoke = () => { EnvironmentExtensions.OpenBrowser(m); }
                        })
                    .ToList();
                LaunchActionsWindow launchActionsWindow = new(actions);
                launchActionsWindow.Show();
            }, EkorensisUtils.LaunchConfigurableWindow);

            if (desktop.Args is { Length: > 0 } args)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await playproviderloadtask.ConfigureAwait(false);
                        mw.Logic.ProcessFiles(args);
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, "Failed to process startup files");
                    }
                    finally
                    {
                        mw.Startup();
                    }
                });
            }
            else
            {
                mw.Startup();
            }
        }
        base.OnFrameworkInitializationCompleted();
    }
}

using Microsoft.Extensions.Configuration;
using Microsoft.Win32;
using ReactiveUI;
using Serilog;
using Serilog.Events;
using SilverAudioPlayer.Core;
using SilverAudioPlayer.Shared;
using SilverMagicBytes;
using System;
using System.Collections.Generic;
using System.Composition;
using System.Composition.Hosting;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
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
using System.Windows.Threading;
using Path = System.IO.Path;

namespace SilverAudioPlayer.WPF
{

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

        protected ProvidersReturnedNullException(
            SerializationInfo info,
            StreamingContext context) : base(info, context)
        {
        }
    }
    public class MainWindowContext : PlayerContext
    {
        private readonly MainWindow mainWindow;
        //  private GradientStops _GradientStops;
        // private IBrush _pbForeGround;

        private string _Title;

        public MainWindowContext(MainWindow mw)
        {
            mainWindow = mw ?? throw new ArgumentNullException(nameof(mw));
            //    Selection = new SelectionModel<Song>();
            //    Selection.SingleSelect = false;
            //    GradientStops defPBStops = new()
            //{
            //    new(KnownColor.Coral.ToColor(), 0),
            //    new(KnownColor.SilverCraftBlue.ToColor(), 1)
            //};
            //    _pbForeGround = WindowExtensions.envBackend.GetString("SAPPBColor").ParseBackground(new LinearGradientBrush() { GradientStops = defPBStops });
            //    if (_pbForeGround is LinearGradientBrush lgb)
            //    {
            //        GradientStops = lgb.GradientStops;
            //    }
            //    else if (_pbForeGround is SolidColorBrush scb)
            //    {
            //        GradientStops = new GradientStops();
            //        GradientStops.Add(new GradientStop(scb.Color, 0));
            //    }
            //    else
            //    {
            //        GradientStops = new GradientStops();
            //        GradientStops.Add(new GradientStop(KnownColor.Coral.ToColor(), 0));
            //    }
        }

        //public IBrush PBForeground
        //{
        //    get => _pbForeGround;
        //    set => this.RaiseAndSetIfChanged(ref _pbForeGround, value);
        //}

        //public SelectionModel<Song> Selection { get; }

        //public string Title
        //{
        //    get => _Title;
        //    set => this.RaiseAndSetIfChanged(ref _Title, value);
        //}

        //public GradientStops GradientStops
        //{
        //    get => _GradientStops;
        //    set => this.RaiseAndSetIfChanged(ref _GradientStops, value);
        //}

        //public void RunTheThing()
        //{
        //    Info i = new(mainWindow);
        //    i.Show();
        //}

        //public void LyricsView()
        //{
        //    LyricsView i = new(mainWindow);
        //    i.Show();
        //}
    }
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private CompositionHost Container;

        Logic<MainWindowContext> Logic;
        private CancellationTokenSource? token = new();

        private Thread? th;
        private void SndThrd(CancellationToken e)
        {
            var exit = false;

            while (!(e.IsCancellationRequested && exit))
                if (Logic.Player?.GetPlaybackState() == PlaybackState.Playing)
                {
                    if (e.IsCancellationRequested) return;
                    try
                    {
                        PB.Dispatcher.Invoke(new Action(() =>
                        {
                            if (IsVisible)
                            {
                                var x = Logic.Player?.GetPosition() ?? TimeSpan.FromMilliseconds(2);
                                PB.Value = x.TotalMilliseconds;
                                // LT.Text = x.ToString();
                            }

                            exit = PB.Value >= PB.Maximum;
                        }), DispatcherPriority.DataBind);
                    }
                    catch (Exception ex)
                    {
                        Logic.log.Error(ex.Message);
                        return;
                    }

                    Thread.Sleep(70);
                }
                else if (Logic.Player?.GetPlaybackState() == PlaybackState.Paused)
                {
                    //uses 12% of cpu when paused if removed lmao
                    Thread.Sleep(270);
                }
                else
                {
                    return;
                }
        }
        public MainWindow()
        {
            var configuration = new ConfigurationBuilder()
               .SetBasePath(Directory.GetCurrentDirectory())
               .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "Configs", "appsettings.json"), true)
               .Build();
            var logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .WriteTo.File(Path.Combine(AppContext.BaseDirectory, "log.txt"), LogEventLevel.Information,
                    rollingInterval: RollingInterval.Day, rollOnFileSizeLimit: true)
                .WriteTo.Debug()
                .CreateLogger();
            Logger.GetLoggerFunc += e => logger.ForContext(e);
            Log.Logger = logger;
            var ctx = new MainWindowContext(this)
            {
                SetLoopType = lt =>
                {
                    /*if (config.LoopType != lt)
                    {
                        config.LoopType = lt;
                        dc.RaisePropertyChanged(nameof(dc.LoopType));
                        config._AllowedRead = false;
                        reader.Write(config, ConfigPath);
                        config._AllowedRead = true;
                    }*/
                },
                //GetLoopType = () => config.LoopType,
                VolumeChanged = vol =>
                {
                    /*config.Volume = vol;
                    Player?.SetVolume(vol);
                    if (config._AllowedRead)
                    {
                        config._AllowedRead = false;
                        reader.Write(config, ConfigPath);
                        config._AllowedRead = true;
                    }*/
                },
                // GetVolume = () => config.Volume,
                ResetUIScrollBar = () =>
                {
                    PB.Dispatcher.Invoke(new Action(() => { PB.Value = 0; }), System.Windows.Threading.DispatcherPriority.DataBind);

                    /* Dispatcher.UIThread.InvokeAsync(() => PB.Value = 0);
                     Dispatcher.UIThread.InvokeAsync(() => LT.Text = TimeSpan.Zero.ToString());
                    */
                    token = new CancellationTokenSource();
                    th = new Thread(() => SndThrd(token.Token));
                },
                SetScrollBarTextTo = scrl =>
                {
                    PB.Dispatcher.Invoke(new Action(() => { PB.Maximum = scrl.TotalMilliseconds; }), System.Windows.Threading.DispatcherPriority.DataBind);


                    /*Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        RT.Text = scrl.ToString();
                    });*/
                },
                HandleLateStageMetadataAndScrollBar = () =>
                {
                    th ??= new Thread(() => SndThrd(token.Token));
                    th.Start();

                    if (Logic.CurrentSong?.Metadata != null)
                    {
                        if (Logic.CurrentSong.Metadata.Title != null)
                            this.Dispatcher.Invoke(() =>
                                Title = Logic.CurrentSong.TitleOrURL() + " - SilverAudioPlayer");
                        if (Logic.CurrentSong?.Metadata?.Pictures?.Any() == true)
                        {
                            var buffer = Logic.CurrentSong.Metadata.Pictures[0].Data;
                            if (buffer != null)
                                try
                                {
                                    var memstream = new MemoryStream(buffer);
                                    var imageSource = new BitmapImage();
                                    imageSource.BeginInit();
                                    imageSource.StreamSource = memstream;
                                    imageSource.EndInit();
                                    Img.Dispatcher.Invoke(() => Img.Source = imageSource);
                                }
                                catch (Exception ex)
                                {
                                    //We have more important things to do than having our app crashed
                                    Log.Error(ex, "Error loading image into main window");
                                }
                        }
                        else
                        {
                            Img.Dispatcher.Invoke(() => Img.Source = null);
                        }
                    }
                },
                ShowMessageBox = (s, s1) =>
                {
                    MessageBox.Show(s, s1);
                }
            };
            DataContext = ctx;
            Logic = new(ctx);
            Environment.SetEnvironmentVariable("BASEDIR", AppContext.BaseDirectory);

            List<Assembly> assemblies = new();
            PlatformLogicHelper.LoadAssemblies(ref assemblies);
            var catalog = new ContainerConfiguration();
            catalog.WithAssemblies(assemblies);
            Container = catalog.CreateContainer();
            Container.SatisfyImports(Logic);
            if (Logic.PlayProviders == null)
                throw new ProvidersReturnedNullException("The 'mw.Logic.Providers' returned null.");
            Logic.PlayableMimes = new List<MimeType>();
            foreach (var provider in Logic.PlayProviders)
            {
                var name = provider.GetType().Name;
                Debug.WriteLine($"Play provider {name} loaded.");
                if (provider.SupportedMimes != null) Logic.PlayableMimes.AddRange(provider.SupportedMimes);
            }

            var playproviderloadtask = Task.Run(async () =>
            {
                foreach (var playprovider in Logic.PlayProviders)
                    if (playprovider != null)
                        await playprovider.OnStartup();
            });
            if (Logic.MetadataProviders == null)
                throw new ProvidersReturnedNullException("The 'mw.Logic.MetadataProviders' returned null.");
            foreach (var provider in Logic.MetadataProviders)
            {
                var name = provider.GetType().Name;
                Debug.WriteLine($"Metadata provider {name} loaded.");
            }

            if (Logic.MusicStatusInterfaces == null)
                throw new ProvidersReturnedNullException("The 'mw.Logic.MusicStatusInterfaces' returned null.");
            if (Logic.WakeLockInterfaces == null)
                throw new ProvidersReturnedNullException("The 'mw.Logic.WakeLockInterfaces' returned null.");
            foreach (var provider in Logic.MusicStatusInterfaces)
            {
                var name = provider.GetType().Name;
                Debug.WriteLine($"Music status interface {name} loaded.");
            }

            Logic.log = logger;
            //Logic.ProcessFiles(desktop.Args);
            InitializeComponent();
            Logic.MainWindow_Opened(null, EventArgs.Empty);
        }

        private void ListView_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop) || e.Data.GetDataPresent("UniformResourceLocatorW"))
            {
                e.Effects = DragDropEffects.Copy;
            }
        }

        private void ListView_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                Logic.ProcessFiles(files);
            }
            else if (e.Data.GetDataPresent("UniformResourceLocatorW"))
            {
                string files = (string)e.Data.GetData("UniformResourceLocatorW");
                Logic.ProcessFiles(new[] { files });
            }
        }

        private void PlayClick(object sender, RoutedEventArgs e) => Logic.Play();
        private void PauseClick(object sender, RoutedEventArgs e) => Logic.Pause();
        private void StopClick(object sender, RoutedEventArgs e) => Logic.RemoveTrack();


        private void PB_MouseUp(object sender, MouseButtonEventArgs e)
        {
            var sp = e.GetPosition(PB);
            var a = PB.Minimum + (PB.Maximum - PB.Minimum) * sp.X / PB.ActualWidth;
            PB.Value = a;
            var at = TimeSpan.FromMilliseconds(a);
            //LT.Text = at.ToString();
            Logic.Player?.SetPosition(at);
        }

        private void ListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (LV.SelectedItem is Song song)
            {
                Logic.HandleSongChanging(song, Logic.CurrentSong == null);
            }
        }
    }
}


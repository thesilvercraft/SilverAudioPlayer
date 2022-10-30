using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Newtonsoft.Json.Linq;
using ReactiveUI;
using Serilog;
using SilverAudioPlayer.Core;
using SilverAudioPlayer.Shared;
using SilverConfig.CobaltExtensions;
using Swordfish.NET.Collections;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Threading;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace SilverAudioPlayer.WinUI
{
    public static class BindingUtils
    {
        public static IEnumerable Fix(IEnumerable x) => x.OfType<object>();
    }
    public class MainWindowContext : PlayerContext
    {
        private readonly MainWindow mainWindow;

        public MainWindowContext(MainWindow mainWindow)
        {
            this.mainWindow = mainWindow;
        }
        //  private GradientStops _GradientStops;
        /*  private IBrush _pbForeGround;

          private string _Title;

          public MainWindowContext(MainWindow mw)
          {
              mainWindow = mw ?? throw new ArgumentNullException(nameof(mw));
              Selection = new SelectionModel<Song>();
              Selection.SingleSelect = false;
              _pbForeGround = "SAPPBColor".ReadBackground(KnownColor.Coral.ToColor());
              if (_pbForeGround is LinearGradientBrush lgb)
              {
                  GradientStops = lgb.GradientStops;
              }
              else
              {
                  GradientStops = new GradientStops();
                  GradientStops.Add(new GradientStop(KnownColor.Coral.ToColor(), 0));
              }
          }

          public IBrush PBForeground
          {
              get => _pbForeGround;
              set => this.RaiseAndSetIfChanged(ref _pbForeGround, value);
          }

          public SelectionModel<Song> Selection { get; }

          public string Title
          {
              get => _Title;
              set => this.RaiseAndSetIfChanged(ref _Title, value);
          }

          public GradientStops GradientStops
          {
              get => _GradientStops;
              set => this.RaiseAndSetIfChanged(ref _GradientStops, value);
          }

          public void RunTheThing()
          {
              Info i = new(mainWindow);
              i.Show();
          }

          public void LyricsView()
          {
              LyricsView i = new(mainWindow);
              i.Show();
          }*/
    }

    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        public Config config;
        public string ConfigPath = Path.Combine(AppContext.BaseDirectory, "SilverAudioPlayer.Config.xml");
        public Logic<MainWindowContext> Logic { get; set; }
        private readonly MainWindowContext dc;
        public CommentXmlConfigReaderNotifyWhenChanged<Config> reader;
        private CancellationTokenSource? token = new();
        private Thread? th;
        /*private MetadataView? metadataView;

        public void Metadata_Click(object? sender, PointerPressedEventArgs e)
        {
            if (CurrentSong != null)
            {
                //metadataView?.Close();
                metadataView = new MetadataView();
                metadataView.LoadSong(CurrentSong);
                metadataView.Show();
            }
        }*/
        ConcurrentObservableCollection<Song> _queue = new();

        public MainWindow()
        {
            this.InitializeComponent();
            reader = new CommentXmlConfigReaderNotifyWhenChanged<Config>();
            if (!File.Exists(ConfigPath)) reader.Write(new Config(), ConfigPath);
            config = reader.Read(ConfigPath) ?? new Config();
            dc = new MainWindowContext(this)
            {
                SetLoopType = lt =>
                {
                    if (config.LoopType != lt)
                    {
                        config.LoopType = lt;
                        dc.RaisePropertyChanged(nameof(dc.LoopType));
                        config._AllowedRead = false;
                        reader.Write(config, ConfigPath);
                        config._AllowedRead = true;
                    }
                },
                GetLoopType = () => config.LoopType,
                VolumeChanged = vol =>
                {
                    config.Volume = vol;
                    Logic.Player?.SetVolume(vol);
                    if (config._AllowedRead)
                    {
                        config._AllowedRead = false;
                        reader.Write(config, ConfigPath);
                        config._AllowedRead = true;
                    }
                },
                GetVolume = () => config.Volume,
                ResetUIScrollBar = () =>
                {
                    DispatcherQueue.TryEnqueue(() => PB.Value = 0);
                    DispatcherQueue.TryEnqueue(() => LT.Text = TimeSpan.Zero.ToString());
                    token = new CancellationTokenSource();
                    th = new Thread(() => SndThrd(token.Token));
                },
                SetScrollBarTextTo = scrl =>
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        PB.Maximum = scrl.TotalMilliseconds;
                        RT.Text = scrl.ToString();
                    });
                },
                HandleLateStageMetadataAndScrollBar = () =>
                {
                    th.Start();
                    if (dc.CurrentSong?.Metadata != null)
                    {
                        if (dc.CurrentSong.Metadata.Title != null)
                            DispatcherQueue.TryEnqueue(() =>
                                Title = dc.CurrentSong.TitleOrURL() + " - SilverAudioPlayer");
                        if (dc.CurrentSong?.Metadata?.Pictures?.Any() == true)
                        {
                            var buffer = dc.CurrentSong.Metadata.Pictures[0].Data;
                            if (buffer != null)
                                try
                                {
                                    var memstream = new MemoryStream(buffer);
                                    var bitmapimg = new BitmapImage();
                                    bitmapimg.SetSource(memstream.AsRandomAccessStream());
                                    DispatcherQueue.TryEnqueue(() => Image.Source = bitmapimg);
                                }
                                catch (Exception ex)
                                {
                                    //We have more important things to do than having our app crashed
                                    Log.Error(ex, "Error loading image into main window");
                                }
                        }
                        else
                        {
                            DispatcherQueue.TryEnqueue(() => { Image.Source = null; });
                        }
                    }
                },
                ShowMessageBox = (s, s1) => {
                    //var window = new MessageBox(s, s1);
                   // window.ShowDialog(this);
                },
                GetQueue= ()=> _queue
            };
            mainListBox.ItemsSource = _queue;
            _queue.CollectionChanged += _queue_CollectionChanged;
            Logic = new Logic<MainWindowContext>(dc);
        }

        private void _queue_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            Debug.WriteLine("WHAT");

        }

        private void myButton_Click(object sender, RoutedEventArgs e)
        {
        
        }
      
        private void SndThrd(CancellationToken e)
        {
            var exit = false;

            while (!(e.IsCancellationRequested && exit))
                if (Logic.Player?.GetPlaybackState() == PlaybackState.Playing)
                {
                    if (e.IsCancellationRequested) return;
                    try
                    {
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            
                                var x = Logic.Player?.GetPosition() ?? TimeSpan.FromMilliseconds(2);
                                PB.Value = x.TotalMilliseconds;
                                LT.Text = x.ToString();
                            

                            exit = PB.Value >= PB.Maximum;
                        });
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

        private void DragOver(object sender, DragEventArgs e)
        {
            if (e.OriginalSource is Control c && c.Name == "MoveTarget")
                e.AcceptedOperation = DataPackageOperation.Move;
            else if (e.DataView.Contains(StandardDataFormats.Uri) || e.DataView.Contains(StandardDataFormats.StorageItems))
                e.AcceptedOperation = DataPackageOperation.Copy;
            else
                e.AcceptedOperation = DataPackageOperation.None;

        }

        private async void Drop(object sender, DragEventArgs e)
        {
            await Task.Delay(100);
            if (e.DataView.Contains(StandardDataFormats.StorageItems)) 
                Logic.ProcessFiles((await e.DataView.GetStorageItemsAsync()).Select(x=>x.Path));
            if (e.DataView.Contains(StandardDataFormats.Uri)) 
                Logic.ProcessFiles(new[] { (await e.DataView.GetUriAsync()).ToString() });
        }

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            Logic.Play();
        }

        private void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            Logic.Pause();

        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
        }

        private void PB_PointerReleased_1(object sender, PointerRoutedEventArgs e)
        {
            var sp = e.GetCurrentPoint(PB);
            var a = PB.Minimum + (PB.Maximum - PB.Minimum) * sp.Position.X / PB.ActualWidth;
            PB.Value = a;
            var at = TimeSpan.FromMilliseconds(a);
            LT.Text = at.ToString();
            Logic.Player?.SetPosition(at);
        }
    }
}

using Avalonia.Controls;
using Avalonia.Media.Imaging;
using DynamicData;
using SilverAudioPlayer.Shared;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace SilverAudioPlayer.Avalonia
{
    public partial class Info : Window
    {
        public Info()
        {
            InitializeComponent();
            this.DoAfterInitTasks(true);
        }
        public Info(MainWindow mainWindow) : this()
        {
            MainWindow = mainWindow;
            ObservableCollection<ICodeInformation> info = new();
            info.AddRange(mainWindow.Logic.PlayProviders.Select(x => x.Value));
            info.AddRange(mainWindow.Logic.MusicStatusInterfaces.Select(x => x.Value));
            info.AddRange(mainWindow.Logic.MetadataProviders.Select(x => x.Value));
            ObservableCollection<object> infop = new();
            StringBuilder licenses = new();
            foreach (var item in info)
            {
                infop.Add(new
                {
                    item.Name,
                    item.Description,
                    item.Version,
                    Icon = item.Icon == null ? null : Bitmap.DecodeToHeight(item.Icon.GetStream(), 80),
                    item.Licenses,
                });
                licenses.AppendLine(item.Licenses);
            }
            SAPAvaloniaPlayerEnviroment sap = new();
            DataContext = new
            {
                Title = "About "+sap.Name,
                ProductName = sap.Name,
                ProductDescription = sap.Description,
                ProductIcon = sap.Icon == null ? null: Bitmap.DecodeToWidth(sap.Icon.GetStream(),200),
                Items = infop,
                LicenseText=licenses.ToString()
            };
        }
        public MainWindow MainWindow;

    }
}

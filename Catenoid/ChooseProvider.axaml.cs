using Avalonia.Controls;
using DynamicData;
using SilverAudioPlayer.Shared;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace Catenoid
{
    public class ChooseProviderDataContext
    {
        public ObservableCollection<InfoPRecord> playProviders { get; set; } = [];
        public bool? SetAsDefaultForFileType { get; set; } = false;
    }

    public partial class ChooseProvider : Window
    {
        ChooseProviderDataContext dc;
        public ChooseProvider()
        {
            InitializeComponent();
            this.DoAfterInitTasksF();
            dc = new();
            DataContext = dc;
            CapBox = this.FindControl<ListBox>("CapBox");
            CapBox.SelectionChanged += CapBox_SelectionChanged;
        }

        private void CapBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (CapBox.SelectedItem == null) return;
            Selected = ((InfoPRecord)CapBox.SelectedItem).Item;
            Close();
        }

        public bool? SetAsDefaultForFileType => dc.SetAsDefaultForFileType;

        public ICodeInformation? Selected = null;

        public async Task SetProvidersAsync(IEnumerable<ICodeInformation> providers)
        {
            dc.playProviders.Clear();
            var (records, _) = await Settings.GetInfoRecordsAsync([.. providers]);
            dc.playProviders.AddRange(records);
        }
    }
}
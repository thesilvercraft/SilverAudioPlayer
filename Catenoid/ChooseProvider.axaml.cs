using Avalonia.Controls;
using DynamicData;
using SilverAudioPlayer.Shared;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

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
            if(CapBox.SelectedItem != null)
            {
                Selected = ((InfoPRecord)CapBox.SelectedItem).Item;
                Close();
            }
        }
        public bool? SetAsDefaultForFileType => dc.SetAsDefaultForFileType;

        public ICodeInformation? Selected = null;
     

        public void SetProviders(IEnumerable<ICodeInformation> providers )
        {
            dc.playProviders.Clear();
            dc.playProviders.AddRange(Settings.GetInfoRecords(providers.ToList()).Item1);
        }
    }
}

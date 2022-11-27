using Avalonia.Controls;
using Avalonia.Controls.Selection;
using DynamicData;
using SilverAudioPlayer.Shared;
using Swordfish.NET.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace SilverAudioPlayer.Avalonia
{
    public class ChooseProviderDataContext
    {
        public ObservableCollection<InfoPRecord> playProviders { get; set; } = new();
        public bool? SetAsDefaultIfPresent { get; set; } = false;
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
                Selected = (IPlayProvider)(((InfoPRecord)CapBox.SelectedItem).Item);
                Close();
            }
        }
        public bool? SetAsDefaultIfPresent =>dc.SetAsDefaultIfPresent;
        public bool? SetAsDefaultForFileType => dc.SetAsDefaultForFileType;

        public IPlayProvider? Selected = null;
     

        public void SetProviders(IEnumerable<IPlayProvider> providers )
        {
            dc.playProviders.Clear();
            dc.playProviders.AddRange(Settings.GetInfoRecords(providers.Cast<ICodeInformation>().ToList()).Item1);
        }
    }
}

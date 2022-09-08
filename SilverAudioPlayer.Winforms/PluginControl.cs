using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SilverAudioPlayer.Winforms
{
    public partial class PluginControl : UserControl
    {
        public PluginControl(string Name, string Description, Version version, Image? icon)
        {
            InitializeComponent();
            nameLabel.Text = Name;
            descriptionLabel.Text = Description;
            versionLabel.Text = version.ToString();
            pictureBox1.Image = icon;
        }
    }
}

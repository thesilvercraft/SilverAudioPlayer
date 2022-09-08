using SilverAudioPlayer.Core;
using SilverAudioPlayer.Shared;
using SilverFormsUtils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SilverAudioPlayer.Winforms
{
    public partial class Manual : Form
    {
        public Manual(Logic l)
        {
            InitializeComponent();
            List<ICodeInformation> info = new();
            info.AddRange(l.PlayProviders.Select(x => x.Value)); ;
            info.AddRange(l.MusicStatusInterfaces.Select(x => x.Value));
            info.AddRange(l.MetadataProviders.Select(x => x.Value));
            List<object> infop = new();
            StringBuilder licenses = new();
            int a = 400;
            foreach (var item in info)
            {
                PluginControl c = new(item.Name,
                    item.Description,
                    item.Version,
                    item.Icon == null ? null : Image.FromStream(item.Icon.GetStream()));
                c.Location = new(10, a);
                a += 150;
                Controls.Add(c);
                licenses.AppendLine(item.Licenses);
            }
            richTextBox1.Text = licenses.ToString();
            if (GetDarkModePreference.ShouldIUseDarkMode())
            {
                this.UseDarkModeBar(true);
                this.UseDarkModeForThingsInsideOfForm(true, true);
            }
        }
    }
}

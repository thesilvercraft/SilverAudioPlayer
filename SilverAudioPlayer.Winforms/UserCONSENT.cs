using Microsoft.AppCenter.Crashes;
using SilverFormsUtils;
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
    public partial class UserCONSENT : Form
    {
        public UserCONSENT()
        {
            InitializeComponent();
            if (GetDarkModePreference.ShouldIUseDarkMode())
            {
                this.UseDarkModeBar(true);
                this.UseDarkModeForThingsInsideOfForm(true, true);
            }
        }

        private void yesalways_Click(object sender, EventArgs e)
        {
            Crashes.NotifyUserConfirmation(UserConfirmation.AlwaysSend);
            Close();

        }

        private void yassnowbutton_Click(object sender, EventArgs e)
        {
            Crashes.NotifyUserConfirmation(UserConfirmation.Send);
            Close();

        }

        private void noneverbutton_Click(object sender, EventArgs e)
        {
            Crashes.NotifyUserConfirmation(UserConfirmation.DontSend);
            Close();

        }
    }
}
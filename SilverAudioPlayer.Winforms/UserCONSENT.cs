#if MS
using Microsoft.AppCenter.Crashes;
using System.Windows.Forms;
#endif

using SilverFormsUtils;

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
#if MS
            Crashes.NotifyUserConfirmation(UserConfirmation.AlwaysSend);
            Close();
#endif
        }

        private void yassnowbutton_Click(object sender, EventArgs e)
        {
#if MS
            Crashes.NotifyUserConfirmation(UserConfirmation.Send);
            Close();
#endif
        }

        private void noneverbutton_Click(object sender, EventArgs e)
        {
#if MS
            Crashes.NotifyUserConfirmation(UserConfirmation.DontSend);
            Close();
#endif
        }
    }
}
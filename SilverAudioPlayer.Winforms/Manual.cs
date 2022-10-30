using SilverAudioPlayer.Core;
using SilverAudioPlayer.Shared;
using SilverFormsUtils;
using System.Text;

namespace SilverAudioPlayer.Winforms;

public partial class Manual : Form
{
    public Manual(Logic<MainWindowContext> l)
    {
        InitializeComponent();
        List<ICodeInformation> info = new();
        info.AddRange(l.PlayProviders.Select(x => x));
        ;
        info.AddRange(l.MusicStatusInterfaces.Select(x => x));
        info.AddRange(l.MetadataProviders.Select(x => x));
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
using SilverAudioPlayer.Shared;
using SilverFormsUtils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SilverAudioPlayer.Winforms
{
    public partial class MetadataForm : Form
    {
        private Label nometadatafoundlabel = new() { Text = "No metadata found! :(", Location = new Point(10, 10), AutoSize = true };
        private LinkLabel nometadatafoundlinklabel = new() { Text = "Try to reload it?", Location = new Point(10, 20), AutoSize = true };
        private Song Song;
        private readonly Color DefaultColor = Color.White;
        private readonly Color Meta1 = Color.Red;
        private readonly Color Meta2 = Color.Blue;
        private readonly Color Meta1B = Color.Orange;

        private void AddLabel(string? text, int pos, int x, Color? color = null)
        {
            color ??= DefaultBackColor;
            this.Controls.Add(new Label() { Text = text ?? "", Location = new Point(x, pos == 1 ? 15 : Controls[Controls.Count - 1].Location.Y + Controls[Controls.Count - 1].Size.Height + 5), AutoSize = true, ForeColor = (Color)color });
        }

        private int poscount = 1;

        private void AddLabel(string? text, int x, Color? color = null)
        {
            AddLabel(text, poscount++, x, color);
        }

        public MetadataForm(ref Song song)
        {
            InitializeComponent();
            if (GetDarkModePreference.ShouldIUseDarkMode())
            {
                this.UseDarkModeBar(true);
                this.UseDarkModeForThingsInsideOfForm(true, true);
            }
            Song = song;
            AutoScroll = true;
            Font = new Font(Font.FontFamily, 15f);
            if (song.Metadata == null)
            {
                Controls.Add(nometadatafoundlabel);
                Controls.Add(nometadatafoundlinklabel);
                nometadatafoundlinklabel.Click += Nometadatafoundlinklabel_Click;
            }
            else
            {
                processproperties(song.Metadata);

                if (song.Metadata?.Pictures?.Any() == true)
                {
                    var d = song.Metadata.Pictures[0].Data;
                    if (d != null)
                    {
                        using var data = new MemoryStream(d);
                        pictureBox1.Image = Image.FromStream(data);
                    }
                }
            }
        }

        private void processproperties(object thing, int meta1 = 20, int meta2 = 30, int graduality = 10, int overflow = 3)
        {
            if (thing is string)
            {
                return;
            }
            if (thing is IList tlist && overflow != 0)
            {
                foreach (var item in tlist)
                {
                    processproperties(item, meta1 + graduality, meta2 + graduality, graduality, overflow - 1);
                }
                return;
            }
            var typeofmetadata = thing.GetType();
            var properties = typeofmetadata.GetProperties();
            var typeofmetadataref = typeof(Metadata);
            var typeofmetadatapropsref = typeofmetadataref.GetProperties();

            bool IsPartOfImplementation(PropertyInfo x)
            {
                return typeofmetadata.IsSubclassOf(typeofmetadataref) && typeofmetadatapropsref.Any(m => x.Name == m.Name);
            }
            foreach (var property in properties.Where(x => x.CanRead && x.GetGetMethod()?.GetParameters().Length == 0).OrderByDescending(x => IsPartOfImplementation(x)))
            {
                try
                {
                    var v = property.GetValue(thing);
                    AddLabel(property.Name + ":", meta1, color: IsPartOfImplementation(property) ? Meta1 : Meta1B);
                    AddLabel(v?.ToString(), meta2, Meta2);
                    if (v != null && overflow != 0)
                    {
                        processproperties(v, meta1 + graduality, meta2 + graduality, graduality, overflow - 1);
                    }
                }
                catch (Exception e)
                {
                }
            }
        }

        private void Nometadatafoundlinklabel_Click(object? sender, EventArgs e)
        {
            if (Song.Metadata == null)
            {
                throw new NotImplementedException("Not implemented");
            }
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {
            if (Song.Metadata != null && Song.Metadata.Pictures != null && Song.Metadata.Pictures.Any())
            {
                PictureForm pf = new(Song);
                pf.Show();
            }
        }
    }
}
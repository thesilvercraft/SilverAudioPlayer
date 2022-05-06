using SilverAudioPlayer.Shared;
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
using Microsoft.Win32;

namespace SilverAudioPlayer.Winforms
{
    public partial class PictureForm : Form
    {
        public static string GetDefaultExtension(string mimeType)
        {
            string defaultExt;
            RegistryKey key;
            object value;
            key = Registry.ClassesRoot.OpenSubKey(@"MIME\Database\Content Type\" + mimeType, false);
            value = key != null ? key.GetValue("Extension", null) : null;
            defaultExt = value != null ? value.ToString() : string.Empty;
            return defaultExt;
        }

        private Song Song;
        private List<Picture> Pictures;
        private int CurrentPictureIndex = 0;

        public PictureForm(Song song)
        {
            InitializeComponent();
            if (GetDarkModePreference.ShouldIUseDarkMode())
            {
                this.UseDarkModeBar(true);
                this.UseDarkModeForThingsInsideOfForm(true, true);
            }
            Song = song;
            Pictures = song.Metadata.Pictures.ToList();
            DisableIfNeeded();
            ChangePicture();
        }

        private void ChangePicture()
        {
            using var stream = new MemoryStream(Pictures[CurrentPictureIndex].Data);
            pictureBox1.Image = Image.FromStream(stream);
            Text = "Picture " + (CurrentPictureIndex + 1) + " of " + Pictures.Count + " - " + pictureBox1.Image.Width + "x" + pictureBox1.Image.Height + " - " + Pictures[CurrentPictureIndex].Position + " - " + Pictures[CurrentPictureIndex].PicType + " - " + Pictures[CurrentPictureIndex].Description + " - " + Pictures[CurrentPictureIndex].Hash + " - " + Pictures[CurrentPictureIndex].MimeType;
        }

        private void DisableIfNeeded()
        {
            left.Enabled = CurrentPictureIndex > 0;
            right.Enabled = CurrentPictureIndex < Pictures.Count - 1;
        }

        private void left_Click(object sender, EventArgs e)
        {
            if (CurrentPictureIndex > 0)
            {
                CurrentPictureIndex--;
                ChangePicture();
            }
            DisableIfNeeded();
        }

        private void right_Click(object sender, EventArgs e)
        {
            if (CurrentPictureIndex < Pictures.Count - 1)
            {
                CurrentPictureIndex++;
                ChangePicture();
            }
            DisableIfNeeded();
        }

        private void clipboardbutton_Click(object sender, EventArgs e)
        {
            Clipboard.Clear();
            Clipboard.SetImage(pictureBox1.Image);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Stream myStream;
            SaveFileDialog saveFileDialog1 = new();
            var filetype = GetDefaultExtension(Pictures[CurrentPictureIndex].MimeType) ?? ".jpeg";
            MessageBox.Show(filetype);
            saveFileDialog1.Title = "Save picture (RAW)";
            saveFileDialog1.Filter = $"{filetype} files (*{filetype})|*.{filetype}|All files (*.*)|*.*";
            saveFileDialog1.FilterIndex = 1;
            saveFileDialog1.RestoreDirectory = true;

            if (saveFileDialog1.ShowDialog() == DialogResult.OK)
            {
                if ((myStream = saveFileDialog1.OpenFile()) != null)
                {
                    // Code to write the stream goes here.
                    myStream.Write(Pictures[CurrentPictureIndex].Data, 0, Pictures[CurrentPictureIndex].Data.Length);
                    myStream.Close();
                }
            }
        }

        private void zipButton_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog folderBrowserDialog = new();
            folderBrowserDialog.Description = "Select a folder to save the pictures to";
            if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
            {
                var folder = folderBrowserDialog.SelectedPath;
                foreach (var picture in Pictures)
                {
                    var filetype = GetDefaultExtension(picture.MimeType) ?? ".jpeg";
                    var filename = picture.Position + " " + picture.Hash + " " + picture.Description + filetype;
                    using var filestream = new FileStream(Path.Combine(folder , filename), FileMode.Create);
                    filestream.Write(picture.Data);
                }
                }
            
            }
    }
}
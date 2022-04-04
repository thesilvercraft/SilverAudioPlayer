namespace SilverAudioPlayer
{
    partial class TimedProgressBar
    {
        /// <summary> 
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.ProgressBar = new SilverAudioPlayer.EpicProgressBar();
            this.LabelEnd = new System.Windows.Forms.Label();
            this.LabelElapsed = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // ProgressBar
            // 
            this.ProgressBar.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.ProgressBar.CacheRainbowDecimals = 3;
            this.ProgressBar.Color = System.Drawing.Color.Red;
            this.ProgressBar.Location = new System.Drawing.Point(14, 14);
            this.ProgressBar.Maximum = ((ulong)(100ul));
            this.ProgressBar.Minimum = ((ulong)(0ul));
            this.ProgressBar.Name = "ProgressBar";
            this.ProgressBar.Rainbow = true;
            this.ProgressBar.Size = new System.Drawing.Size(837, 20);
            this.ProgressBar.TabIndex = 0;
            this.ProgressBar.Value = ((ulong)(0ul));
            // 
            // LabelEnd
            // 
            this.LabelEnd.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.LabelEnd.AutoSize = true;
            this.LabelEnd.Location = new System.Drawing.Point(763, 47);
            this.LabelEnd.Name = "LabelEnd";
            this.LabelEnd.Size = new System.Drawing.Size(88, 15);
            this.LabelEnd.TabIndex = 1;
            this.LabelEnd.Text = "00:00:00.000000";
            // 
            // LabelElapsed
            // 
            this.LabelElapsed.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.LabelElapsed.AutoSize = true;
            this.LabelElapsed.Location = new System.Drawing.Point(14, 47);
            this.LabelElapsed.Name = "LabelElapsed";
            this.LabelElapsed.Size = new System.Drawing.Size(88, 15);
            this.LabelElapsed.TabIndex = 2;
            this.LabelElapsed.Text = "00:00:00.000000";
            // 
            // TimedProgressBar
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.LabelElapsed);
            this.Controls.Add(this.LabelEnd);
            this.Controls.Add(this.ProgressBar);
            this.Name = "TimedProgressBar";
            this.Size = new System.Drawing.Size(866, 64);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private Label LabelEnd;
        private Label LabelElapsed;
        public EpicProgressBar ProgressBar;
    }
}

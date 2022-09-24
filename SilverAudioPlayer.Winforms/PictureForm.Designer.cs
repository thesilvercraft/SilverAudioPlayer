namespace SilverAudioPlayer.Winforms
{
    partial class PictureForm
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

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            this.clipboardbutton = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.button1 = new System.Windows.Forms.Button();
            this.zipButton = new System.Windows.Forms.Button();
            this.left = new System.Windows.Forms.Button();
            this.right = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            this.SuspendLayout();
            // 
            // pictureBox1
            // 
            this.pictureBox1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.pictureBox1.Location = new System.Drawing.Point(38, 16);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new System.Drawing.Size(500, 500);
            this.pictureBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.pictureBox1.TabIndex = 0;
            this.pictureBox1.TabStop = false;
            // 
            // clipboardbutton
            // 
            this.clipboardbutton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.clipboardbutton.Location = new System.Drawing.Point(38, 522);
            this.clipboardbutton.Name = "clipboardbutton";
            this.clipboardbutton.Size = new System.Drawing.Size(127, 23);
            this.clipboardbutton.TabIndex = 1;
            this.clipboardbutton.Text = "Copy to clipboard";
            this.clipboardbutton.UseVisualStyleBackColor = true;
            this.clipboardbutton.Click += new System.EventHandler(this.ClipboardClick);
            // 
            // label1
            // 
            this.label1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(171, 526);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(48, 15);
            this.label1.TabIndex = 2;
            this.label1.Text = "Save as:";
            // 
            // button1
            // 
            this.button1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.button1.Location = new System.Drawing.Point(222, 522);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(118, 23);
            this.button1.TabIndex = 3;
            this.button1.Text = "Current image";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.SaveClick);
            // 
            // zipButton
            // 
            this.zipButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.zipButton.Location = new System.Drawing.Point(346, 522);
            this.zipButton.Name = "zipButton";
            this.zipButton.Size = new System.Drawing.Size(158, 23);
            this.zipButton.TabIndex = 4;
            this.zipButton.Text = "All original images";
            this.zipButton.UseVisualStyleBackColor = true;
            this.zipButton.Click += new System.EventHandler(this.ZipClick);
            // 
            // left
            // 
            this.left.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
            this.left.Location = new System.Drawing.Point(11, 236);
            this.left.MinimumSize = new System.Drawing.Size(10, 10);
            this.left.Name = "left";
            this.left.Size = new System.Drawing.Size(21, 23);
            this.left.TabIndex = 5;
            this.left.Text = "<";
            this.left.UseVisualStyleBackColor = true;
            this.left.Click += new System.EventHandler(this.LeftClick);
            // 
            // right
            // 
            this.right.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.right.Location = new System.Drawing.Point(551, 236);
            this.right.MinimumSize = new System.Drawing.Size(10, 10);
            this.right.Name = "right";
            this.right.Size = new System.Drawing.Size(21, 23);
            this.right.TabIndex = 6;
            this.right.Text = ">";
            this.right.UseVisualStyleBackColor = true;
            this.right.Click += new System.EventHandler(this.RightClick);
            // 
            // PictureForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(584, 561);
            this.Controls.Add(this.right);
            this.Controls.Add(this.left);
            this.Controls.Add(this.zipButton);
            this.Controls.Add(this.button1);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.clipboardbutton);
            this.Controls.Add(this.pictureBox1);
            this.Name = "PictureForm";
            this.Text = "PictureForm";
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private PictureBox pictureBox1;
        private Button clipboardbutton;
        private Label label1;
        private Button button1;
        private Button zipButton;
        private Button left;
        private Button right;
    }
}
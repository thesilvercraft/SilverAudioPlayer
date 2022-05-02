namespace SilverAudioPlayer.Winforms
{
    partial class UserCONSENT
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
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.yesalways = new System.Windows.Forms.Button();
            this.noneverbutton = new System.Windows.Forms.Button();
            this.yassnowbutton = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Segoe UI", 27.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.label1.Location = new System.Drawing.Point(12, 9);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(143, 50);
            this.label1.TabIndex = 0;
            this.label1.Text = "Hewwo";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(12, 68);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(647, 15);
            this.label2.TabIndex = 1;
            this.label2.Text = "You mightz havez notizez thatz the applicationz hadz an error. Do you wantz to se" +
    "ndz the errorz to MICRO$OFT and Silver?";
            // 
            // yesalways
            // 
            this.yesalways.Location = new System.Drawing.Point(12, 137);
            this.yesalways.Name = "yesalways";
            this.yesalways.Size = new System.Drawing.Size(214, 23);
            this.yesalways.TabIndex = 2;
            this.yesalways.Text = "YASS PLEASE alwayz sendz the errorz";
            this.yesalways.UseVisualStyleBackColor = true;
            this.yesalways.Click += new System.EventHandler(this.yesalways_Click);
            // 
            // noneverbutton
            // 
            this.noneverbutton.Location = new System.Drawing.Point(639, 137);
            this.noneverbutton.Name = "noneverbutton";
            this.noneverbutton.Size = new System.Drawing.Size(149, 23);
            this.noneverbutton.TabIndex = 3;
            this.noneverbutton.Text = "no i hatez the sendz";
            this.noneverbutton.UseVisualStyleBackColor = true;
            this.noneverbutton.Click += new System.EventHandler(this.noneverbutton_Click);
            // 
            // yassnowbutton
            // 
            this.yassnowbutton.Location = new System.Drawing.Point(295, 137);
            this.yassnowbutton.Name = "yassnowbutton";
            this.yassnowbutton.Size = new System.Drawing.Size(250, 23);
            this.yassnowbutton.TabIndex = 4;
            this.yassnowbutton.Text = "Yass sendz itz now but asks me later againz";
            this.yassnowbutton.UseVisualStyleBackColor = true;
            this.yassnowbutton.Click += new System.EventHandler(this.yassnowbutton_Click);
            // 
            // UserCONSENT
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 169);
            this.Controls.Add(this.yassnowbutton);
            this.Controls.Add(this.noneverbutton);
            this.Controls.Add(this.yesalways);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Name = "UserCONSENT";
            this.Text = "do you wants to sendz the errorz?";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private Label label1;
        private Label label2;
        private Button yesalways;
        private Button noneverbutton;
        private Button yassnowbutton;
    }
}
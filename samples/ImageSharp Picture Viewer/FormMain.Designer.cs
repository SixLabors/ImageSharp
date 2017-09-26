namespace ImageSharp_Picture_Viewer
{
    partial class FormMain
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
            this.PictureBoxMain = new System.Windows.Forms.PictureBox();
            this.DialogOpenFile = new System.Windows.Forms.OpenFileDialog();
            this.DialogColorChooser = new System.Windows.Forms.ColorDialog();
            ((System.ComponentModel.ISupportInitialize)(this.PictureBoxMain)).BeginInit();
            this.SuspendLayout();
            // 
            // PictureBoxMain
            // 
            this.PictureBoxMain.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Center;
            this.PictureBoxMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.PictureBoxMain.Location = new System.Drawing.Point(0, 0);
            this.PictureBoxMain.Margin = new System.Windows.Forms.Padding(0);
            this.PictureBoxMain.MinimumSize = new System.Drawing.Size(20, 20);
            this.PictureBoxMain.Name = "PictureBoxMain";
            this.PictureBoxMain.Size = new System.Drawing.Size(284, 262);
            this.PictureBoxMain.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.PictureBoxMain.TabIndex = 0;
            this.PictureBoxMain.TabStop = false;
            this.PictureBoxMain.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.PictureBoxMain_MouseDoubleClick);
            // 
            // DialogOpenFile
            //
            this.DialogOpenFile.AutoUpgradeEnabled = false; 
            this.DialogOpenFile.Filter = "BMP files (*.bmp)|*.bmp|GIF files (*.gif)|*.gif|JPEG files (*.jpeg;*.jpg)|*.jpeg;*.jpg|PNG files (*.png)|*.png|All files (*.*)|*.*";
            // 
            // FormMain
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.ClientSize = new System.Drawing.Size(284, 262);
            this.Controls.Add(this.PictureBoxMain);
            this.MinimumSize = new System.Drawing.Size(20, 20);
            this.Name = "FormMain";
            this.Text = "ImageSharp Picture Viewer";
            ((System.ComponentModel.ISupportInitialize)(this.PictureBoxMain)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.PictureBox PictureBoxMain;
        private System.Windows.Forms.OpenFileDialog DialogOpenFile;
        private System.Windows.Forms.ColorDialog DialogColorChooser;
    }
}


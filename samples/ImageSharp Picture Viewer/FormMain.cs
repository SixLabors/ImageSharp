using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SixLabors.ImageSharp;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace ImageSharp_Picture_Viewer
{
    public partial class FormMain : Form
    {
        public FormMain(string imageFile = null)
        {
            InitializeComponent();

            if(imageFile == null)
            {
                if(this.DialogOpenFile.ShowDialog(this) == DialogResult.OK)
                {
                    imageFile = this.DialogOpenFile.FileName;
                }
                
                if((imageFile != null) && (imageFile.Trim().Length > 0))
                {
                    using (var image = SixLabors.ImageSharp.Image.Load(imageFile))
                    {
                        int w = image.Width;
                        int h = image.Height;
                        int ch = 4; // number of channels (ie. assuming 32 bit RGB in this case)

                        byte[] imageData = image.Frames.RootFrame.SavePixelData().ToArray();
                        var bitmap = new Bitmap(w, h, w * ch, PixelFormat.Format32bppArgb, Marshal.UnsafeAddrOfPinnedArrayElement(imageData, 0));

                        System.Drawing.Image image2 = bitmap;
                        this.PictureBoxMain.Image = image2;
                    }
                    //var bitmap = new Bitmap(this.DialogOpenFile.FileName);
                    //Image image = bitmap;
                    //this.PictureBoxMain.Image = image;
                }
            }
        }

        private void PictureBoxMain_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if(this.DialogColorChooser.ShowDialog(this) == DialogResult.OK)
            {
                this.PictureBoxMain.BackColor = this.DialogColorChooser.Color;
            }
        }
    }
}

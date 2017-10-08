using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SixLabors.ImageSharp;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System;
using System.IO;

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
                    /* Using SixLabors.ImageSharp classes */
                    // .NET Standard 1.3+ 
                    using (var imageIS = SixLabors.ImageSharp.Image.Load(imageFile))
                    // .NET Standard 1.1 - 1.2
                    //using (System.IO.FileStream stream = File.OpenRead(imageFile))
                    //using (var imageIS = SixLabors.ImageSharp.Image.Load<Rgba32>(stream))
                    // Common code
                    {
                        int w = imageIS.Width;
                        int h = imageIS.Height;
                        int ch = 4; // Number of color channels (ie. assuming 32 bit ARGB in this case)

                        // Get the pixel data in RGBA format
                        byte[] imageData = imageIS.Frames.RootFrame.SavePixelData();

                        // Swap from RGBA to BGRA if necessary (this is how pixel colors of a DIB is stored on memory for little-endian CPUs)
                        if (BitConverter.IsLittleEndian)
                        {
                            byte temp;
                            for (int i = 0; i < imageData.Length; i = i + ch)
                            {
                                temp = imageData[i];
                                imageData[i] = imageData[i + 2];
                                imageData[i + 1] = imageData[i + 1];
                                imageData[i + 2] = temp;
                                imageData[i + 3] = imageData[i + 3];
                            }
                        }

                        // Create the image on .NET Framework format (DIB)
                        var bitmap = new Bitmap(w, h, w * ch, format: PixelFormat.Format32bppArgb, scan0: Marshal.UnsafeAddrOfPinnedArrayElement(imageData, 0));

                        // Set the DPIs as in the SixLabors.ImageSharp image loaded
                        bitmap.SetResolution((float)imageIS.MetaData.HorizontalResolution, (float)imageIS.MetaData.VerticalResolution);

                        // Create a image that is useble by the GUI controls/widgets
                        System.Drawing.Image imageSDI = bitmap;

                        // Set the image on the PictureBox control/widget
                        this.PictureBoxMain.Image = imageSDI;
                    }

                    /* .NET Framework classes */
                    //var bitmap = new Bitmap(imageFile);
                    //Image imageSDI = bitmap;
                    //this.PictureBoxMain.Image = imageSDI;
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

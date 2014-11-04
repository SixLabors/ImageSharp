

namespace ImageProcessor.Common.Extensions
{
    using System.Drawing;
    using System.Drawing.Imaging;

    public static class ImageExtensions
    {
        public static Image ChangePixelFormat(this Image image, PixelFormat format)
        {
            Bitmap clone = new Bitmap(image.Width, image.Height, format);
            clone.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            using (Graphics graphics = Graphics.FromImage(clone))
            {
                graphics.DrawImage(image, new Rectangle(0, 0, clone.Width, clone.Height));
            }

            image = new Bitmap(clone);
            return image;
        }
    }
}

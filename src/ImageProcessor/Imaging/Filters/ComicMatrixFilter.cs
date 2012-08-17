// -----------------------------------------------------------------------
// <copyright file="ComicMatrixFilter.cs" company="James South">
// TODO: Update copyright text.
// </copyright>
// -----------------------------------------------------------------------

namespace ImageProcessor.Imaging.Filters
{
    #region Using
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.Drawing.Drawing2D;
    #endregion

    /// <summary>
    /// Encapsulates methods with which to add a comic filter to an image.
    /// </summary>
    class ComicMatrixFilter : IMatrixFilter
    {
        /// <summary>
        /// Enumurates Argb colour channels.
        /// </summary>
        private enum ChannelArgb
        {
            /// <summary>
            /// The blue channel
            /// </summary>
            Blue = 0,

            /// <summary>
            /// The green channel
            /// </summary>
            Green = 1,

            /// <summary>
            /// The red channel
            /// </summary>
            Red = 2,

            /// <summary>
            /// The alpha channel
            /// </summary>
            Alpha = 3
        }

        /// <summary>
        /// Gets the <see cref="T:System.Drawing.Imaging.ColoMatrix"/> for this filter instance.
        /// </summary>
        public ColorMatrix Matrix
        {
            get { return ColorMatrixes.LoSatch; }
        }

        /// <summary>
        /// Processes the image.
        /// </summary>
        /// <param name="factory">
        /// The the current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class containing
        /// the image to process.
        /// </param>
        /// <param name="image">The current image to process</param>
        /// <param name="newImage">The new Image to return</param>
        /// <returns>
        /// The processed image from the current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class.
        /// </returns>
        public Image ProcessImage(ImageFactory factory, Image image, Image newImage)
        {
            // Bitmaps for comic pattern
            Bitmap hisatchBitmap = null;
            Bitmap patternBitmap = null;

            try
            {
                using (Graphics graphics = Graphics.FromImage(newImage))
                {
                    using (ImageAttributes attributes = new ImageAttributes())
                    {
                        attributes.SetColorMatrix(this.Matrix);

                        Rectangle rectangle = new Rectangle(0, 0, image.Width, image.Height);

                        // Set the attributes to LoSatch and draw the image.
                        graphics.DrawImage(image, rectangle, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);

                        // Create a bitmap for overlaying.
                        hisatchBitmap = new Bitmap(rectangle.Width, rectangle.Height, PixelFormat.Format32bppPArgb);

                        // Set the color matrix
                        attributes.SetColorMatrix(ColorMatrixes.HiSatch);

                        // Draw the image with the hisatch colormatrix.
                        using (var g = Graphics.FromImage(hisatchBitmap))
                        {
                            g.DrawImage(image, rectangle, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);
                        }

                        // We need to create a new image now with the hi saturation colormatrix and a pattern mask to paint it
                        // onto the other image with.
                        patternBitmap = new Bitmap(rectangle.Width, rectangle.Height, PixelFormat.Format32bppPArgb);

                        // Create the pattern mask.
                        using (var g = Graphics.FromImage(patternBitmap))
                        {
                            g.Clear(Color.Black);
                            g.SmoothingMode = SmoothingMode.HighQuality;
                            for (var y = 0; y < image.Height; y += 10)
                            {
                                for (var x = 0; x < image.Width; x += 6)
                                {
                                    g.FillEllipse(Brushes.White, x, y, 4, 4);
                                    g.FillEllipse(Brushes.White, x + 3, y + 5, 4, 4);
                                }
                            }
                        }

                        // Transfer the alpha channel from the mask to the hi sturation image.
                        TransferOneArgbChannelFromOneBitmapToAnother(patternBitmap, hisatchBitmap, ChannelArgb.Blue, ChannelArgb.Alpha);

                        // Overlay the image.
                        graphics.DrawImage(hisatchBitmap, 0, 0);

                        // Dispose of the other images
                        hisatchBitmap.Dispose();
                        patternBitmap.Dispose();
                    }
                }

                // Reassign the image.
                image.Dispose();
                image = newImage;
            }
            catch
            {
                if (newImage != null)
                {
                    newImage.Dispose();
                }

                if (hisatchBitmap != null)
                {
                    hisatchBitmap.Dispose();
                }

                if (patternBitmap != null)
                {
                    patternBitmap.Dispose();
                }
            }
            return image;
        }

        /// <summary>
        /// Transfers a single ARGB channel from one image to another.
        /// </summary>
        /// <param name="source">
        /// The source.
        /// </param>
        /// <param name="destination">
        /// The destination.
        /// </param>
        /// <param name="sourceChannel">
        /// The source channel.
        /// </param>
        /// <param name="destinationChannel">
        /// The destination channel.
        /// </param>
        private static void TransferOneArgbChannelFromOneBitmapToAnother(Bitmap source, Bitmap destination, ChannelArgb sourceChannel, ChannelArgb destinationChannel)
        {
            if (source.Size != destination.Size)
            {
                throw new ArgumentException();
            }

            Rectangle rectangle = new Rectangle(Point.Empty, source.Size);

            // Lockbits the source.
            BitmapData bitmapDataSource = source.LockBits(rectangle, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);

            // Declare an array to hold the bytes of the bitmap.
            int bytes = bitmapDataSource.Stride * bitmapDataSource.Height;

            // Allocate a buffer for the source image
            byte[] sourceRgbValues = new byte[bytes];

            // Copy the RGB values into the array.
            System.Runtime.InteropServices.Marshal.Copy(bitmapDataSource.Scan0, sourceRgbValues, 0, bytes);

            // Unlockbits the source.
            source.UnlockBits(bitmapDataSource);

            // Lockbits the destination.
            BitmapData bitmapDataDestination = destination.LockBits(rectangle, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);

            // Allocate a buffer for image
            byte[] destinationRgbValues = new byte[bytes];

            // Copy the RGB values into the array.
            System.Runtime.InteropServices.Marshal.Copy(bitmapDataDestination.Scan0, destinationRgbValues, 0, bytes);

            int s = (int)sourceChannel;
            int d = (int)destinationChannel;

            for (int i = rectangle.Height * rectangle.Width; i > 0; i--)
            {
                destinationRgbValues[d] = sourceRgbValues[s];
                d += 4;
                s += 4;
            }

            // Copy the RGB values back to the bitmap
            System.Runtime.InteropServices.Marshal.Copy(destinationRgbValues, 0, bitmapDataDestination.Scan0, bytes);

            // Unlock bits the destination.
            destination.UnlockBits(bitmapDataDestination);
        }

    }
}

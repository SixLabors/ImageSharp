// -----------------------------------------------------------------------
// <copyright file="ImageExtensions.cs" company="James South">
//     Copyright (c) James South.
//     Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace ImageProcessor.Helpers.Extensions
{
    #region Using

    using System;
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.IO;
    using System.Runtime.InteropServices;

    #endregion

    /// <summary>
    /// Extensions to the <see cref="T:System.Drawing.Image"/> class
    /// </summary>
    public static class ImageExtensions
    {
        /// <summary>
        /// Converts an image to an array of bytes.
        /// </summary>
        /// <param name="image">The <see cref="T:System.Drawing.Image"/> instance that this method extends.</param>
        /// <param name="imageFormat">The <see cref="T:System.Drawing.Imaging.ImageFormat"/> to export the image with.</param>
        /// <returns>A byte array representing the current image.</returns>
        public static byte[] ToBytes(this Image image, ImageFormat imageFormat)
        {
            BitmapData data = ((Bitmap)image).LockBits(new Rectangle(0, 0, image.Width, image.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            int length = image.Width * image.Height * 4;
            byte[] byteArray = new byte[length];

            if (data.Stride == image.Width * 4)
            {
                Marshal.Copy(data.Scan0, byteArray, 0, length);
            }
            else
            {
                for (int i = 0, l = image.Height; i < l; i++)
                {
                    IntPtr p = new IntPtr(data.Scan0.ToInt32() + data.Stride * i);
                    Marshal.Copy(p, byteArray, i * image.Width * 4, image.Width * 4);
                }
            }

            ((Bitmap)image).UnlockBits(data);

            return byteArray;
        }

        public static Image FromBytes(this Image image, byte[] bytes)
        {
            BitmapData data = ((Bitmap)image).LockBits(new Rectangle(0, 0, image.Width, image.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

            if (data.Stride == image.Width * 4)
            {
                Marshal.Copy(bytes, 0, data.Scan0, bytes.Length);
            }
            else
            {
                for (int i = 0, l = image.Height; i < l; i++)
                {
                    IntPtr p = new IntPtr(data.Scan0.ToInt32() + data.Stride * i);
                    Marshal.Copy(bytes, i * image.Width * 4, p, image.Width * 4);
                }
            }

            ((Bitmap)image).UnlockBits(data);
            return image;
        }
    }
}

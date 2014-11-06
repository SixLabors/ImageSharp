// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ImageBuffer.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   The image buffer for storing pixel information.
//   Adapted from <see href="https://github.com/drewnoakes" />
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging.Quantizers.WuQuantizer
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Drawing.Drawing2D;
    using System.Drawing.Imaging;
    using System.Runtime.InteropServices;

    using ImageProcessor.Common.Exceptions;

    /// <summary>
    /// The image buffer for storing pixel information.
    /// Adapted from <see href="https://github.com/drewnoakes"/>
    /// </summary>
    internal class ImageBuffer
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ImageBuffer"/> class.
        /// </summary>
        /// <param name="image">
        /// The image to store.
        /// </param>
        public ImageBuffer(Bitmap image)
        {
            this.Image = image;
        }

        /// <summary>
        /// Gets the image.
        /// </summary>
        public Bitmap Image { get; private set; }

        /// <summary>
        /// Gets the pixel lines.
        /// </summary>
        /// <exception cref="QuantizationException">
        /// Thrown if the given image is not a 32 bit per pixel image.
        /// </exception>
        public IEnumerable<Pixel[]> PixelLines
        {
            get
            {
                int bitDepth = System.Drawing.Image.GetPixelFormatSize(this.Image.PixelFormat);
                if (bitDepth != 32)
                {
                    throw new QuantizationException(
                        string.Format(
                            "The image you are attempting to quantize does not contain a 32 bit ARGB palette. This image has a bit depth of {0} with {1} colors.",
                            bitDepth,
                            this.Image.Palette.Entries.Length));
                }

                int width = this.Image.Width;
                int height = this.Image.Height;
                int[] buffer = new int[width];
                Pixel[] pixels = new Pixel[width];
                //using (FastBitmap bitmap = new FastBitmap(this.Image))
                //{
                //    for (int y = 0; y < height; y++)
                //    {
                //        for (int x = 0; x < width; x++)
                //        {
                //            Color color = bitmap.GetPixel(x, y);
                //            pixels[x] = new Pixel(color.A, color.R, color.G, color.B);
                //        }

                //        yield return pixels;
                //    }
                //}

                for (int rowIndex = 0; rowIndex < height; rowIndex++)
                {
                    BitmapData data = this.Image.LockBits(Rectangle.FromLTRB(0, rowIndex, width, rowIndex + 1), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                    try
                    {
                        Marshal.Copy(data.Scan0, buffer, 0, width);
                        for (int pixelIndex = 0; pixelIndex < buffer.Length; pixelIndex++)
                        {
                            pixels[pixelIndex] = new Pixel(buffer[pixelIndex]);
                        }
                    }
                    finally
                    {
                        this.Image.UnlockBits(data);
                    }

                    yield return pixels;
                }
            }
        }

        /// <summary>
        /// Updates the pixel indexes.
        /// </summary>
        /// <param name="lineIndexes">
        /// The line indexes.
        /// </param>
        public void UpdatePixelIndexes(IEnumerable<byte[]> lineIndexes)
        {
            int width = this.Image.Width;
            int height = this.Image.Height;

            IEnumerator<byte[]> indexesIterator = lineIndexes.GetEnumerator();
            
            for (int rowIndex = 0; rowIndex < height; rowIndex++)
            {
                indexesIterator.MoveNext();


                BitmapData data = this.Image.LockBits(Rectangle.FromLTRB(0, rowIndex, width, rowIndex + 1), ImageLockMode.WriteOnly, PixelFormat.Format8bppIndexed);

                try
                {
                    Marshal.Copy(indexesIterator.Current, 0, data.Scan0, width);
                }
                finally
                {
                    this.Image.UnlockBits(data);
                }
            }
        }
    }
}

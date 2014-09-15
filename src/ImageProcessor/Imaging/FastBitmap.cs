// --------------------------------------------------------------------------------------------------------------------
// <copyright file="FastBitmap.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Allows fast access to <see cref="System.Drawing.Bitmap" />'s pixel data.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging
{
    using System;
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Allows fast access to <see cref="System.Drawing.Bitmap"/>'s pixel data.
    /// </summary>
    public class FastBitmap : IDisposable
    {
        /// <summary>
        /// The bitmap.
        /// </summary>
        private readonly Bitmap bitmap;

        /// <summary>
        /// The width of the bitmap.
        /// </summary>
        private readonly int width;

        /// <summary>
        /// The height of the bitmap.
        /// </summary>
        private readonly int height;

        /// <summary>
        /// The stride width of the bitmap.
        /// </summary>
        private int stride;

        /// <summary>
        /// The bitmap data.
        /// </summary>
        private BitmapData bitmapData;

        /// <summary>
        /// The pixel buffer for holding pixel data.
        /// </summary>
        private byte[] pixelBuffer;

        /// <summary>
        /// The buffer length.
        /// </summary>
        private int bufferLength;

        /// <summary>
        /// A value indicating whether this instance of the given entity has been disposed.
        /// </summary>
        /// <value><see langword="true"/> if this instance has been disposed; otherwise, <see langword="false"/>.</value>
        /// <remarks>
        /// If the entity is disposed, it must not be disposed a second
        /// time. The isDisposed field is set the first time the entity
        /// is disposed. If the isDisposed field is true, then the Dispose()
        /// method will not dispose again. This help not to prolong the entity's
        /// life in the Garbage Collector.
        /// </remarks>
        private bool isDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="FastBitmap"/> class.
        /// </summary>
        /// <param name="bitmap">The input bitmap.</param>
        public FastBitmap(Bitmap bitmap)
        {
            this.bitmap = bitmap;
            this.width = this.bitmap.Width;
            this.height = this.bitmap.Height;
            this.LockBitmap();
        }

        /// <summary>
        /// Gets the width, in pixels of the <see cref="System.Drawing.Bitmap"/>.
        /// </summary>
        public int Width
        {
            get
            {
                return this.width;
            }
        }

        /// <summary>
        /// Gets the height, in pixels of the <see cref="System.Drawing.Bitmap"/>.
        /// </summary>
        public int Height
        {
            get
            {
                return this.height;
            }
        }

        /// <summary>
        /// Allows the implicit conversion of an instance of <see cref="FastBitmap"/> to a 
        /// <see cref="System.Drawing.Image"/>.
        /// </summary>
        /// <param name="fastBitmap">
        /// The instance of <see cref="FastBitmap"/> to convert.
        /// </param>
        /// <returns>
        /// An instance of <see cref="System.Drawing.Image"/>.
        /// </returns>
        public static implicit operator Image(FastBitmap fastBitmap)
        {
            return fastBitmap.bitmap;
        }

        /// <summary>
        /// Allows the implicit conversion of an instance of <see cref="FastBitmap"/> to a 
        /// <see cref="System.Drawing.Bitmap"/>.
        /// </summary>
        /// <param name="fastBitmap">
        /// The instance of <see cref="FastBitmap"/> to convert.
        /// </param>
        /// <returns>
        /// An instance of <see cref="System.Drawing.Bitmap"/>.
        /// </returns>
        public static implicit operator Bitmap(FastBitmap fastBitmap)
        {
            return fastBitmap.bitmap;
        }

        /// <summary>
        /// Gets the color at the specified pixel of the <see cref="System.Drawing.Bitmap"/>.
        /// </summary>
        /// <param name="x">The x-coordinate of the pixel to retrieve.</param>
        /// <param name="y">The y-coordinate of the pixel to retrieve.</param>
        /// <returns>The <see cref="System.Drawing.Color"/> at the given pixel.</returns>
        public Color GetPixel(int x, int y)
        {
            if ((x < 0) || (x >= this.width))
            {
                throw new ArgumentOutOfRangeException("x", "Value cannot be less than zero or greater than the bitmap width.");
            }

            if ((y < 0) || (y >= this.height))
            {
                throw new ArgumentOutOfRangeException("y", "Value cannot be less than zero or greater than the bitmap height.");
            }

            int position = (x * 4) + (y * this.stride);
            byte blue = this.pixelBuffer[position];
            byte green = this.pixelBuffer[position + 1];
            byte red = this.pixelBuffer[position + 2];
            byte alpha = this.pixelBuffer[position + 3];
            return Color.FromArgb(alpha, red, green, blue);
        }

        /// <summary>
        /// Sets the color of the specified pixel of the <see cref="System.Drawing.Bitmap"/>.
        /// </summary>
        /// <param name="x">The x-coordinate of the pixel to set.</param>
        /// <param name="y">The y-coordinate of the pixel to set.</param>
        /// <param name="color">
        /// A <see cref="System.Drawing.Color"/> color structure that represents the 
        /// color to set the specified pixel.
        /// </param>
        public void SetPixel(int x, int y, Color color)
        {
            if ((x < 0) || (x >= this.width))
            {
                throw new ArgumentOutOfRangeException("x", "Value cannot be less than zero or greater than the bitmap width.");
            }

            if ((y < 0) || (y >= this.height))
            {
                throw new ArgumentOutOfRangeException("y", "Value cannot be less than zero or greater than the bitmap height.");
            }

            int position = (x * 4) + (y * this.stride);
            this.pixelBuffer[position] = color.B;
            this.pixelBuffer[position + 1] = color.G;
            this.pixelBuffer[position + 2] = color.R;
            this.pixelBuffer[position + 3] = color.A;
        }

        /// <summary>
        /// Disposes the object and frees resources for the Garbage Collector.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);

            // This object will be cleaned up by the Dispose method.
            // Therefore, you should call GC.SupressFinalize to
            // take this object off the finalization queue 
            // and prevent finalization code for this object
            // from executing a second time.
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the object and frees resources for the Garbage Collector.
        /// </summary>
        /// <param name="disposing">If true, the object gets disposed.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (this.isDisposed)
            {
                return;
            }

            if (disposing)
            {
                // Dispose of any managed resources here.
                this.UnlockBitmap();
            }

            // Call the appropriate methods to clean up
            // unmanaged resources here.
            // Note disposing is done.
            this.isDisposed = true;
        }

        /// <summary>
        /// Locks the bitmap into system memory.
        /// </summary>
        private void LockBitmap()
        {
            Rectangle bounds = new Rectangle(Point.Empty, this.bitmap.Size);

            // Lock the bitmap
            this.bitmapData = this.bitmap.LockBits(bounds, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);

            // Copy the bitmap data across to the array for manipulation.
            this.stride = this.bitmapData.Stride;
            this.bufferLength = this.stride * this.bitmapData.Height;
            this.pixelBuffer = new byte[this.bufferLength];
            Marshal.Copy(this.bitmapData.Scan0, this.pixelBuffer, 0, this.pixelBuffer.Length);
        }

        /// <summary>
        /// Unlocks the bitmap from system memory.
        /// </summary>
        private void UnlockBitmap()
        {
            // Copy the RGB values back to the bitmap and unlock the bitmap.
            Marshal.Copy(this.pixelBuffer, 0, this.bitmapData.Scan0, this.bufferLength);
            this.bitmap.UnlockBits(this.bitmapData);
            this.bitmapData = null;
            this.pixelBuffer = null;
        }
    }
}

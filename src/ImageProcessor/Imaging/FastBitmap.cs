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

    using ImageProcessor.Imaging.Colors;

    /// <summary>
    /// Allows fast access to <see cref="System.Drawing.Bitmap"/>'s pixel data.
    /// </summary>
    public unsafe class FastBitmap : IDisposable
    {
        /// <summary>
        /// The integral representation of the 8bppIndexed pixel format.
        /// </summary>
        // ReSharper disable once InconsistentNaming
        private const int Format8bppIndexed = (int)PixelFormat.Format8bppIndexed;

        /// <summary>
        /// The integral representation of the 24bppRgb pixel format.
        /// </summary>
        // ReSharper disable once InconsistentNaming
        private const int Format24bppRgb = (int)PixelFormat.Format24bppRgb;

        /// <summary>
        /// The integral representation of the 32bppArgb pixel format.
        /// </summary>
        // ReSharper disable once InconsistentNaming
        private const int Format32bppArgb = (int)PixelFormat.Format32bppArgb;

        /// <summary>
        /// The integral representation of the 32bppPArgb pixel format.
        /// </summary>
        // ReSharper disable once InconsistentNaming
        private const int Format32bppPArgb = (int)PixelFormat.Format32bppPArgb;

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
        /// The color channel - blue, green, red, alpha.
        /// </summary>
        private readonly int channel;

        /// <summary>
        /// Whether to compute integral rectangles.
        /// </summary>
        private readonly bool computeIntegrals;

        /// <summary>
        /// Whether to compute tilted integral rectangles.
        /// </summary>
        private readonly bool computeTilted;

        /// <summary>
        /// The normal integral image.
        /// </summary>
        private long[,] normalSumImage;

        /// <summary>
        /// The squared integral image.
        /// </summary>
        private long[,] squaredSumImage;

        /// <summary>
        /// The tilted sum image.
        /// </summary>
        private long[,] tiltedSumImage;

        /// <summary>
        /// The normal width.
        /// </summary>
        private int normalWidth;

        /// <summary>
        /// The tilted width.
        /// </summary>
        private int tiltedWidth;

        /// <summary>
        /// The number of bytes in a row.
        /// </summary>
        private int bytesInARow;

        /// <summary>
        /// The normal integral sum.
        /// </summary>
        private long* normalSum;

        /// <summary>
        /// The squared integral sum.
        /// </summary>
        private long* squaredSum;

        /// <summary>
        /// The tilted integral sum.
        /// </summary>
        private long* tiltedSum;

        /// <summary>
        /// The normal sum handle.
        /// </summary>
        private GCHandle normalSumHandle;

        /// <summary>
        /// The squared sum handle.
        /// </summary>
        private GCHandle squaredSumHandle;

        /// <summary>
        /// The tilted sum handle.
        /// </summary>
        private GCHandle tiltedSumHandle;

        /// <summary>
        /// The size of the color32 structure.
        /// </summary>
        private int pixelSize;

        /// <summary>
        /// The bitmap data.
        /// </summary>
        private BitmapData bitmapData;

        /// <summary>
        /// The position of the first pixel in the bitmap.
        /// </summary>
        private byte* pixelBase;

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
        public FastBitmap(Image bitmap)
            : this(bitmap, false)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FastBitmap"/> class.
        /// </summary>
        /// <param name="bitmap">The input bitmap.</param>
        /// <param name="computeIntegrals">
        /// Whether to compute integral rectangles.
        /// </param>
        public FastBitmap(Image bitmap, bool computeIntegrals)
            : this(bitmap, computeIntegrals, false)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FastBitmap"/> class.
        /// </summary>
        /// <param name="bitmap">The input bitmap.</param>
        /// <param name="computeIntegrals">
        /// Whether to compute integral rectangles.
        /// </param>
        /// <param name="computeTilted">
        /// Whether to compute tilted integral rectangles.
        /// </param>
        public FastBitmap(Image bitmap, bool computeIntegrals, bool computeTilted)
        {
            int pixelFormat = (int)bitmap.PixelFormat;

            // Check image format
            if (!(pixelFormat == Format8bppIndexed ||
                  pixelFormat == Format24bppRgb ||
                  pixelFormat == Format32bppArgb ||
                  pixelFormat == Format32bppPArgb))
            {
                throw new ArgumentException("Only 8bpp, 24bpp and 32bpp images are supported.");
            }

            this.bitmap = (Bitmap)bitmap;
            this.width = this.bitmap.Width;
            this.height = this.bitmap.Height;

            this.channel = pixelFormat == Format8bppIndexed ? 0 : 2;
            this.computeIntegrals = computeIntegrals;
            this.computeTilted = computeTilted;

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
        /// Gets the Integral Image for values' sum.
        /// </summary>
        public long[,] NormalImage
        {
            get { return this.normalSumImage; }
        }

        /// <summary>
        /// Gets the Integral Image for values' squared sum.
        /// </summary>
        public long[,] SquaredImage
        {
            get { return this.squaredSumImage; }
        }

        /// <summary>
        /// Gets the Integral Image for tilted values' sum.
        /// </summary>
        public long[,] TiltedImage
        {
            get { return this.tiltedSumImage; }
        }

        /// <summary>
        /// Gets the pixel data for the given position.
        /// </summary>
        /// <param name="x">
        /// The x position of the pixel.
        /// </param>
        /// <param name="y">
        /// The y position of the pixel.
        /// </param>
        /// <returns>
        /// The <see cref="Color32"/>.
        /// </returns>
        private Color32* this[int x, int y]
        {
            get { return (Color32*)(this.pixelBase + (y * this.bytesInARow) + (x * this.pixelSize)); }
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
#if DEBUG
            if ((x < 0) || (x >= this.width))
            {
                throw new ArgumentOutOfRangeException("x", "Value cannot be less than zero or greater than the bitmap width.");
            }

            if ((y < 0) || (y >= this.height))
            {
                throw new ArgumentOutOfRangeException("y", "Value cannot be less than zero or greater than the bitmap height.");
            }
#endif
            Color32* data = this[x, y];
            return Color.FromArgb(data->A, data->R, data->G, data->B);
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
#if DEBUG
            if ((x < 0) || (x >= this.width))
            {
                throw new ArgumentOutOfRangeException("x", "Value cannot be less than zero or greater than the bitmap width.");
            }

            if ((y < 0) || (y >= this.height))
            {
                throw new ArgumentOutOfRangeException("y", "Value cannot be less than zero or greater than the bitmap height.");
            }
#endif
            Color32* data = this[x, y];
            data->R = color.R;
            data->G = color.G;
            data->B = color.B;
            data->A = color.A;
        }

        /// <summary>
        /// Gets the sum of the pixels in a rectangle of the Integral image.
        /// </summary>
        /// <param name="x">The horizontal position of the rectangle <c>x</c>.</param>
        /// <param name="y">The vertical position of the rectangle <c>y</c>.</param>
        /// <param name="rectangleWidth">The rectangle's width <c>w</c>.</param>
        /// <param name="rectangleHeight">The rectangle's height <c>h</c>.</param>
        /// <returns>
        /// The sum of all pixels contained in the rectangle, computed
        /// as I[y, x] + I[y + h, x + w] - I[y + h, x] - I[y, x + w].
        /// </returns>
        public long GetSum(int x, int y, int rectangleWidth, int rectangleHeight)
        {
            int a = (this.normalWidth * y) + x;
            int b = (this.normalWidth * (y + rectangleHeight)) + (x + rectangleWidth);
            int c = (this.normalWidth * (y + rectangleHeight)) + x;
            int d = (this.normalWidth * y) + (x + rectangleWidth);

            return this.normalSum[a] + this.normalSum[b] - this.normalSum[c] - this.normalSum[d];
        }

        /// <summary>
        /// Gets the sum of the squared pixels in a rectangle of the Integral image.
        /// </summary>
        /// <param name="x">The horizontal position of the rectangle <c>x</c>.</param>
        /// <param name="y">The vertical position of the rectangle <c>y</c>.</param>
        /// <param name="rectangleWidth">The rectangle's width <c>w</c>.</param>
        /// <param name="rectangleHeight">The rectangle's height <c>h</c>.</param>
        /// <returns>
        /// The sum of all pixels contained in the rectangle, computed
        /// as I²[y, x] + I²[y + h, x + w] - I²[y + h, x] - I²[y, x + w].
        /// </returns>
        public long GetSum2(int x, int y, int rectangleWidth, int rectangleHeight)
        {
            int a = (this.normalWidth * y) + x;
            int b = (this.normalWidth * (y + rectangleHeight)) + (x + rectangleWidth);
            int c = (this.normalWidth * (y + rectangleHeight)) + x;
            int d = (this.normalWidth * y) + (x + rectangleWidth);

            return this.squaredSum[a] + this.squaredSum[b] - this.squaredSum[c] - this.squaredSum[d];
        }

        /// <summary>
        /// Gets the sum of the pixels in a tilted rectangle of the Integral image.
        /// </summary>
        /// <param name="x">The horizontal position of the rectangle <c>x</c>.</param>
        /// <param name="y">The vertical position of the rectangle <c>y</c>.</param>
        /// <param name="rectangleWidth">The rectangle's width <c>w</c>.</param>
        /// <param name="rectangleHeight">The rectangle's height <c>h</c>.</param>
        /// <returns>
        /// The sum of all pixels contained in the rectangle, computed
        /// as T[y + w, x + w + 1] + T[y + h, x - h + 1] - T[y, x + 1] - T[y + w + h, x + w - h + 1].
        /// </returns>
        public long GetSumT(int x, int y, int rectangleWidth, int rectangleHeight)
        {
            int a = (this.tiltedWidth * (y + rectangleWidth)) + (x + rectangleWidth + 1);
            int b = (this.tiltedWidth * (y + rectangleHeight)) + (x - rectangleHeight + 1);
            int c = (this.tiltedWidth * y) + (x + 1);
            int d = (this.tiltedWidth * (y + rectangleWidth + rectangleHeight)) + (x + rectangleWidth - rectangleHeight + 1);

            return this.tiltedSum[a] + this.tiltedSum[b] - this.tiltedSum[c] - this.tiltedSum[d];
        }

        /// <summary>
        /// Disposes the object and frees resources for the Garbage Collector.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);

            // This object will be cleaned up by the Dispose method.
            // Therefore, you should call GC.SuppressFinalize to
            // take this object off the finalization queue 
            // and prevent finalization code for this object
            // from executing a second time.
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Determines whether the specified <see cref="T:System.Object"/> is equal to the current <see cref="T:System.Object"/>.
        /// </summary>
        /// <returns>
        /// true if the specified object  is equal to the current object; otherwise, false.
        /// </returns>
        /// <param name="obj">The object to compare with the current object. </param>
        public override bool Equals(object obj)
        {
            FastBitmap fastBitmap = obj as FastBitmap;

            if (fastBitmap == null)
            {
                return false;
            }

            return this.bitmap == fastBitmap.bitmap;
        }

        /// <summary>
        /// Serves as a hash function for a particular type. 
        /// </summary>
        /// <returns>
        /// A hash code for the current <see cref="T:System.Object"/>.
        /// </returns>
        public override int GetHashCode()
        {
            return this.bitmap.GetHashCode();
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
            if (this.normalSumHandle.IsAllocated)
            {
                this.normalSumHandle.Free();
                this.normalSum = null;
            }

            if (this.squaredSumHandle.IsAllocated)
            {
                this.squaredSumHandle.Free();
                this.squaredSum = null;
            }

            if (this.tiltedSumHandle.IsAllocated)
            {
                this.tiltedSumHandle.Free();
                this.tiltedSum = null;
            }

            // Note disposing is done.
            this.isDisposed = true;
        }

        /// <summary>
        /// Locks the bitmap into system memory.
        /// </summary>
        private void LockBitmap()
        {
            Rectangle bounds = new Rectangle(Point.Empty, this.bitmap.Size);

            // Figure out the number of bytes in a row. This is rounded up to be a multiple
            // of 4 bytes, since a scan line in an image must always be a multiple of 4 bytes
            // in length.
            this.pixelSize = Image.GetPixelFormatSize(this.bitmap.PixelFormat) / 8;
            this.bytesInARow = bounds.Width * this.pixelSize;
            if (this.bytesInARow % 4 != 0)
            {
                this.bytesInARow = 4 * ((this.bytesInARow / 4) + 1);
            }

            // Lock the bitmap
            this.bitmapData = this.bitmap.LockBits(bounds, ImageLockMode.ReadWrite, PixelFormat.Format32bppPArgb);

            // Set the value to the first scan line
            this.pixelBase = (byte*)this.bitmapData.Scan0.ToPointer();

            if (this.computeIntegrals)
            {
                // Allocate values for integral image calculation.
                this.normalWidth = this.width + 1;
                int normalHeight = this.height + 1;

                this.tiltedWidth = this.width + 2;
                int tiltedHeight = this.height + 2;

                this.normalSumImage = new long[normalHeight, this.normalWidth];
                this.normalSumHandle = GCHandle.Alloc(this.normalSumImage, GCHandleType.Pinned);
                this.normalSum = (long*)this.normalSumHandle.AddrOfPinnedObject().ToPointer();

                this.squaredSumImage = new long[normalHeight, this.normalWidth];
                this.squaredSumHandle = GCHandle.Alloc(this.squaredSumImage, GCHandleType.Pinned);
                this.squaredSum = (long*)this.squaredSumHandle.AddrOfPinnedObject().ToPointer();

                if (this.computeTilted)
                {
                    this.tiltedSumImage = new long[tiltedHeight, this.tiltedWidth];
                    this.tiltedSumHandle = GCHandle.Alloc(this.tiltedSumImage, GCHandleType.Pinned);
                    this.tiltedSum = (long*)this.tiltedSumHandle.AddrOfPinnedObject().ToPointer();
                }

                this.CalculateIntegrals();
            }
        }

        /// <summary>
        /// Computes all possible rectangular areas in the image.
        /// </summary>
        private void CalculateIntegrals()
        {
            // Calculate integral and integral squared values.
            int stride = this.bitmapData.Stride;
            int offset = stride - this.bytesInARow;
            byte* srcStart = this.pixelBase + this.channel;

            // Do the job
            byte* src = srcStart;

            // For each line
            for (int y = 1; y <= this.height; y++)
            {
                int yy = this.normalWidth * y;
                int y1 = this.normalWidth * (y - 1);

                // For each pixel
                for (int x = 1; x <= this.width; x++, src += this.pixelSize)
                {
                    int pixel = *src;
                    int pixelSquared = pixel * pixel;

                    int r = yy + x;
                    int a = yy + (x - 1);
                    int b = y1 + x;
                    int g = y1 + (x - 1);

                    this.normalSum[r] = pixel + this.normalSum[a] + this.normalSum[b] - this.normalSum[g];
                    this.squaredSum[r] = pixelSquared + this.squaredSum[a] + this.squaredSum[b] - this.squaredSum[g];
                }

                src += offset;
            }

            if (this.computeTilted)
            {
                src = srcStart;

                // Left-to-right, top-to-bottom pass
                for (int y = 1; y <= this.height; y++, src += offset)
                {
                    int yy = this.tiltedWidth * y;
                    int y1 = this.tiltedWidth * (y - 1);

                    for (int x = 2; x < this.width + 2; x++, src += this.pixelSize)
                    {
                        int a = y1 + (x - 1);
                        int b = yy + (x - 1);
                        int g = y1 + (x - 2);
                        int r = yy + x;

                        this.tiltedSum[r] = *src + this.tiltedSum[a] + this.tiltedSum[b] - this.tiltedSum[g];
                    }
                }

                {
                    int yy = this.tiltedWidth * this.height;
                    int y1 = this.tiltedWidth * (this.height + 1);

                    for (int x = 2; x < this.width + 2; x++, src += this.pixelSize)
                    {
                        int a = yy + (x - 1);
                        int c = yy + (x - 2);
                        int b = y1 + (x - 1);
                        int r = y1 + x;

                        this.tiltedSum[r] = this.tiltedSum[a] + this.tiltedSum[b] - this.tiltedSum[c];
                    }
                }

                // Right-to-left, bottom-to-top pass
                for (int y = this.height; y >= 0; y--)
                {
                    int yy = this.tiltedWidth * y;
                    int y1 = this.tiltedWidth * (y + 1);

                    for (int x = this.width + 1; x >= 1; x--)
                    {
                        int r = yy + x;
                        int b = y1 + (x - 1);

                        this.tiltedSum[r] += this.tiltedSum[b];
                    }
                }

                for (int y = this.height + 1; y >= 0; y--)
                {
                    int yy = this.tiltedWidth * y;

                    for (int x = this.width + 1; x >= 2; x--)
                    {
                        int r = yy + x;
                        int b = yy + (x - 2);

                        this.tiltedSum[r] -= this.tiltedSum[b];
                    }
                }
            }
        }

        /// <summary>
        /// Unlocks the bitmap from system memory.
        /// </summary>
        private void UnlockBitmap()
        {
            // Copy the RGB values back to the bitmap and unlock the bitmap.
            this.bitmap.UnlockBits(this.bitmapData);
            this.bitmapData = null;
            this.pixelBase = null;
        }
    }
}
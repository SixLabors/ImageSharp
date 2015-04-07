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
        /// The number of bytes in a row.
        /// </summary>
        private int bytesInARow;

        /// <summary>
        /// The size of the color32 structure.
        /// </summary>
        private int color32Size;

        /// <summary>
        /// The color channel - blue, green, red, alpha.
        /// </summary>
        private int channel;

        /// <summary>
        /// Whether to compute tilted integral rectangles.
        /// </summary>
        private bool computeTilted;

        private long[,] nSumImage; // normal integral image
        private long[,] sSumImage; // squared integral image
        private long[,] tSumImage; // tilted integral image

        private long* nSum; // normal  integral image
        private long* sSum; // squared integral image
        private long* tSum; // tilted  integral image

        private GCHandle nSumHandle;
        private GCHandle sSumHandle;
        private GCHandle tSumHandle;

        private int nWidth;
        private int nHeight;

        private int tWidth;
        private int tHeight;

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
            : this(bitmap, 2, false)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FastBitmap"/> class.
        /// </summary>
        /// <param name="bitmap">The input bitmap.</param>
        /// <param name="integralColorChannel">
        /// The integral color channel. Blue, Green, Red, or Alpha.
        /// </param>
        /// <param name="computeTilted">
        /// Whether to compute tilted integral rectangles.
        /// </param>
        public FastBitmap(Image bitmap, int integralColorChannel, bool computeTilted)
        {
            this.bitmap = (Bitmap)bitmap;
            this.width = this.bitmap.Width;
            this.height = this.bitmap.Height;
            this.channel = integralColorChannel;
            this.computeTilted = computeTilted;

            this.nWidth = this.width + 1;
            this.nHeight = this.height + 1;

            this.tWidth = this.width + 2;
            this.tHeight = this.height + 2;

            this.nSumImage = new long[this.nHeight, this.nWidth];
            this.nSumHandle = GCHandle.Alloc(this.nSumImage, GCHandleType.Pinned);
            this.nSum = (long*)this.nSumHandle.AddrOfPinnedObject().ToPointer();

            this.sSumImage = new long[this.nHeight, this.nWidth];
            this.sSumHandle = GCHandle.Alloc(this.sSumImage, GCHandleType.Pinned);
            this.sSum = (long*)this.sSumHandle.AddrOfPinnedObject().ToPointer();

            if (this.computeTilted)
            {
                this.tSumImage = new long[this.tHeight, this.tWidth];
                this.tSumHandle = GCHandle.Alloc(this.tSumImage, GCHandleType.Pinned);
                this.tSum = (long*)this.tSumHandle.AddrOfPinnedObject().ToPointer();
            }

            this.LockBitmap();
            this.CalculateIntegrals();
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
            get { return (Color32*)(this.pixelBase + (y * this.bytesInARow) + (x * this.color32Size)); }
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
            int a = (this.nWidth * y) + x;
            int b = (this.nWidth * (y + rectangleHeight)) + (x + rectangleWidth);
            int c = (this.nWidth * (y + rectangleHeight)) + x;
            int d = (this.nWidth * y) + (x + rectangleWidth);

            return this.nSum[a] + this.nSum[b] - this.nSum[c] - this.nSum[d];
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
            int a = (this.nWidth * y) + x;
            int b = (this.nWidth * (y + rectangleHeight)) + (x + rectangleWidth);
            int c = (this.nWidth * (y + rectangleHeight)) + x;
            int d = (this.nWidth * y) + (x + rectangleWidth);

            return this.sSum[a] + this.sSum[b] - this.sSum[c] - this.sSum[d];
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
            int a = (this.tWidth * (y + rectangleWidth)) + (x + rectangleWidth + 1);
            int b = (this.tWidth * (y + rectangleHeight)) + (x - rectangleHeight + 1);
            int c = (this.tWidth * y) + (x + 1);
            int d = (this.tWidth * (y + rectangleWidth + rectangleHeight)) + (x + rectangleWidth - rectangleHeight + 1);

            return this.tSum[a] + this.tSum[b] - this.tSum[c] - this.tSum[d];
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
            if (this.nSumHandle.IsAllocated)
            {
                this.nSumHandle.Free();
                this.nSum = null;
            }

            if (this.sSumHandle.IsAllocated)
            {
                this.sSumHandle.Free();
                this.sSum = null;
            }

            if (this.tSumHandle.IsAllocated)
            {
                this.tSumHandle.Free();
                this.tSum = null;
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
            this.color32Size = sizeof(Color32);
            this.bytesInARow = bounds.Width * this.color32Size;
            if (this.bytesInARow % 4 != 0)
            {
                this.bytesInARow = 4 * ((this.bytesInARow / 4) + 1);
            }

            // Lock the bitmap
            this.bitmapData = this.bitmap.LockBits(bounds, ImageLockMode.ReadWrite, PixelFormat.Format32bppPArgb);

            // Set the value to the first scan line
            this.pixelBase = (byte*)this.bitmapData.Scan0.ToPointer();
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
            // TODO. Make this parallel
            for (int y = 1; y <= this.height; y++)
            {
                int yy = this.nWidth * y;
                int y1 = this.nWidth * (y - 1);

                // For each pixel
                for (int x = 1; x <= this.width; x++, src += this.color32Size)
                {
                    int pixel = *src;
                    int pixelSquared = pixel * pixel;

                    int r = yy + x;
                    int a = yy + (x - 1);
                    int b = y1 + x;
                    int c = y1 + (x - 1);

                    this.nSum[r] = pixel + this.nSum[a] + this.nSum[b] - this.nSum[c];
                    this.sSum[r] = pixelSquared + this.sSum[a] + this.sSum[b] - this.sSum[c];
                }

                src += offset;
            }

            if (this.computeTilted)
            {
                src = srcStart;

                // Left-to-right, top-to-bottom pass
                for (int y = 1; y <= this.height; y++, src += offset)
                {
                    int yy = this.tWidth * y;
                    int y1 = this.tWidth * (y - 1);

                    for (int x = 2; x < this.width + 2; x++, src += this.color32Size)
                    {
                        int a = y1 + (x - 1);
                        int b = yy + (x - 1);
                        int c = y1 + (x - 2);
                        int r = yy + x;

                        this.tSum[r] = *src + this.tSum[a] + this.tSum[b] - this.tSum[c];
                    }
                }

                {
                    int yy = this.tWidth * this.height;
                    int y1 = this.tWidth * (this.height + 1);

                    for (int x = 2; x < this.width + 2; x++, src += this.color32Size)
                    {
                        int a = yy + (x - 1);
                        int c = yy + (x - 2);
                        int b = y1 + (x - 1);
                        int r = y1 + x;

                        this.tSum[r] = this.tSum[a] + this.tSum[b] - this.tSum[c];
                    }
                }

                // Right-to-left, bottom-to-top pass
                for (int y = this.height; y >= 0; y--)
                {
                    int yy = this.tWidth * y;
                    int y1 = this.tWidth * (y + 1);

                    for (int x = this.width + 1; x >= 1; x--)
                    {
                        int r = yy + x;
                        int b = y1 + (x - 1);

                        this.tSum[r] += this.tSum[b];
                    }
                }

                for (int y = this.height + 1; y >= 0; y--)
                {
                    int yy = this.tWidth * y;

                    for (int x = this.width + 1; x >= 2; x--)
                    {
                        int r = yy + x;
                        int b = yy + (x - 2);

                        this.tSum[r] -= this.tSum[b];
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

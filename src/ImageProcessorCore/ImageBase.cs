// <copyright file="ImageBase.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    /// <summary>
    /// The base class of all images. Encapsulates the basic properties and methods
    /// required to manipulate images.
    /// </summary>
    public abstract unsafe class ImageBase : IImageBase, IDisposable
    {
        /// <summary>
        /// The position of the first pixel in the bitmap.
        /// </summary>
        private float* pixelsBase;

        /// <summary>
        /// The array of pixels.
        /// </summary>
        private float[] pixelsArray;

        /// <summary>
        /// Provides a way to access the pixels from unmanaged memory.
        /// </summary>
        private GCHandle pixelsHandle;

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
        internal bool IsDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageBase"/> class.
        /// </summary>
        protected ImageBase()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageBase"/> class.
        /// </summary>
        /// <param name="width">The width of the image in pixels.</param>
        /// <param name="height">The height of the image in pixels.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if either <paramref name="width"/> or <paramref name="height"/> are less than or equal to 0.
        /// </exception>
        protected ImageBase(int width, int height)
        {
            Guard.MustBeGreaterThan(width, 0, nameof(width));
            Guard.MustBeGreaterThan(height, 0, nameof(height));

            this.Width = width;
            this.Height = height;

            // Assign the pointer and pixels.
            this.pixelsArray = new float[width * height * 4];
            this.pixelsHandle = GCHandle.Alloc(this.pixelsArray, GCHandleType.Pinned);
            this.pixelsBase = (float*)this.pixelsHandle.AddrOfPinnedObject().ToPointer();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageBase"/> class.
        /// </summary>
        /// <param name="other">
        /// The other <see cref="ImageBase"/> to create this instance from.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if the given <see cref="ImageBase"/> is null.
        /// </exception>
        protected ImageBase(ImageBase other)
        {
            Guard.NotNull(other, nameof(other), "Other image cannot be null.");

            this.Width = other.Width;
            this.Height = other.Height;
            this.Quality = other.Quality;
            this.FrameDelay = other.FrameDelay;

            // Assign the pointer and copy the pixels.
            this.pixelsArray = new float[this.Width * this.Height * 4];
            this.pixelsHandle = GCHandle.Alloc(this.pixelsArray, GCHandleType.Pinned);
            this.pixelsBase = (float*)this.pixelsHandle.AddrOfPinnedObject().ToPointer();
            Array.Copy(other.pixelsArray, this.pixelsArray, other.pixelsArray.Length);
        }

        /// <inheritdoc/>
        ~ImageBase()
        {
            this.Dispose(false);
        }

        /// <summary>
        /// Gets or sets the maximum allowable width in pixels.
        /// </summary>
        public static int MaxWidth { get; set; } = int.MaxValue;

        /// <summary>
        /// Gets or sets the maximum allowable height in pixels.
        /// </summary>
        public static int MaxHeight { get; set; } = int.MaxValue;

        /// <inheritdoc/>
        public float[] Pixels => this.pixelsArray;

        /// <inheritdoc/>
        public int Width { get; private set; }

        /// <inheritdoc/>
        public int Height { get; private set; }

        /// <inheritdoc/>
        public double PixelRatio => (double)this.Width / this.Height;

        /// <inheritdoc/>
        public Rectangle Bounds => new Rectangle(0, 0, this.Width, this.Height);

        /// <inheritdoc/>
        public int Quality { get; set; }

        /// <inheritdoc/>
        public int FrameDelay { get; set; }

        /// <inheritdoc/>
        public Color this[int x, int y, [CallerLineNumber] int line = 0]
        {
            get
            {
#if DEBUG
                if ((x < 0) || (x >= this.Width))
                {
                    throw new ArgumentOutOfRangeException(nameof(x), "Value cannot be less than zero or greater than the bitmap width.");
                }

                if ((y < 0) || (y >= this.Height))
                {
                    throw new ArgumentOutOfRangeException(nameof(y), "Value cannot be less than zero or greater than the bitmap height.");
                }
#endif
                return *((Color*)(this.pixelsBase + ((y * this.Width) + x) * 4));
            }

            set
            {
#if DEBUG
                if ((x < 0) || (x >= this.Width))
                {
                    throw new ArgumentOutOfRangeException(nameof(x), "Value cannot be less than zero or greater than the bitmap width.");
                }

                if ((y < 0) || (y >= this.Height))
                {
                    throw new ArgumentOutOfRangeException(nameof(y), "Value cannot be less than zero or greater than the bitmap height.");
                }
#endif
                *(Color*)(this.pixelsBase + (((y * this.Width) + x) * 4)) = value;
            }
        }

        /// <inheritdoc/>
        public void SetPixels(int width, int height, float[] pixels)
        {
#if DEBUG
            if (width <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "Width must be greater than or equals than zero.");
            }

            if (height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(height), "Height must be greater than or equal than zero.");
            }

            if (pixels.Length != width * height * 4)
            {
                throw new ArgumentException("Pixel array must have the length of Width * Height * 4.");
            }
#endif
            this.Width = width;
            this.Height = height;

            // Ensure nothing is preserved if previously allocated.
            if (this.pixelsHandle.IsAllocated)
            {
                this.pixelsArray = null;
                this.pixelsHandle.Free();
                this.pixelsBase = null;
            }

            this.pixelsArray = pixels;
            this.pixelsHandle = GCHandle.Alloc(this.pixelsArray, GCHandleType.Pinned);
            this.pixelsBase = (float*)this.pixelsHandle.AddrOfPinnedObject().ToPointer();
        }

        /// <inheritdoc/>
        public void ClonePixels(int width, int height, float[] pixels)
        {
#if DEBUG
            if (width <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "Width must be greater than or equals than zero.");
            }

            if (height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(height), "Height must be greater than or equal than zero.");
            }

            if (pixels.Length != width * height * 4)
            {
                throw new ArgumentException("Pixel array must have the length of Width * Height * 4.");
            }
#endif
            this.Width = width;
            this.Height = height;

            // Assign the pointer and copy the pixels.
            this.pixelsArray = new float[pixels.Length];
            this.pixelsHandle = GCHandle.Alloc(this.pixelsArray, GCHandleType.Pinned);
            this.pixelsBase = (float*)this.pixelsHandle.AddrOfPinnedObject().ToPointer();
            Array.Copy(pixels, this.pixelsArray, pixels.Length);
        }

        /// <inheritdoc/>
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
        /// Disposes the object and frees resources for the Garbage Collector.
        /// </summary>
        /// <param name="disposing">If true, the object gets disposed.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (this.IsDisposed)
            {
                return;
            }

            if (disposing)
            {
                // Dispose of any managed resources here.
                this.pixelsArray = null;
            }

            if (this.pixelsHandle.IsAllocated)
            {
                this.pixelsHandle.Free();
                this.pixelsBase = null;
            }

            // Note disposing is done.
            this.IsDisposed = true;
        }
    }
}

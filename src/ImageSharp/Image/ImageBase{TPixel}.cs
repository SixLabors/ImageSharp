// <copyright file="ImageBase{TPixel}.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.Diagnostics;
    using ImageSharp.PixelFormats;
    using Processing;

    /// <summary>
    /// The base class of all images. Encapsulates the basic properties and methods required to manipulate
    /// images in different pixel formats.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    [DebuggerDisplay("Image: {Width}x{Height}")]
    public abstract class ImageBase<TPixel> : IImageBase<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Gets or sets the maximum allowable width in pixels.
        /// </summary>
        public const int MaxWidth = int.MaxValue;

        /// <summary>
        /// Gets or sets the maximum allowable height in pixels.
        /// </summary>
        public const int MaxHeight = int.MaxValue;

        /// <summary>
        /// The image pixels
        /// </summary>
        private TPixel[] pixelBuffer;

        /// <summary>
        /// A value indicating whether this instance of the given entity has been disposed.
        /// </summary>
        /// <value><see langword="true"/> if this instance has been disposed; otherwise, <see langword="false"/>.</value>
        /// <remarks>
        /// If the entity is disposed, it must not be disposed a second time. The isDisposed field is set the first time the entity
        /// is disposed. If the isDisposed field is true, then the Dispose() method will not dispose again. This help not to prolong the entity's
        /// life in the Garbage Collector.
        /// </remarks>
        private bool isDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageBase{TPixel}"/> class.
        /// </summary>
        /// <param name="configuration">
        /// The configuration providing initialization code which allows extending the library.
        /// </param>
        protected ImageBase(Configuration configuration)
        {
            this.Configuration = configuration ?? Configuration.Default;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageBase{TPixel}"/> class.
        /// </summary>
        /// <param name="configuration">
        /// The configuration providing initialization code which allows extending the library.
        /// </param>
        /// <param name="width">The width of the image in pixels.</param>
        /// <param name="height">The height of the image in pixels.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if either <paramref name="width"/> or <paramref name="height"/> are less than or equal to 0.
        /// </exception>
        protected ImageBase(Configuration configuration, int width, int height)
            : this(configuration)
        {
            Guard.MustBeGreaterThan(width, 0, nameof(width));
            Guard.MustBeGreaterThan(height, 0, nameof(height));

            this.Width = width;
            this.Height = height;
            this.RentPixels();
            this.ClearPixels();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageBase{TPixel}"/> class.
        /// </summary>
        /// <param name="other">
        /// The other <see cref="ImageBase{TPixel}"/> to create this instance from.
        /// </param>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown if the given <see cref="ImageBase{TPixel}"/> is null.
        /// </exception>
        protected ImageBase(ImageBase<TPixel> other)
            : this(other.Configuration)
        {
            Guard.NotNull(other, nameof(other), "Other image cannot be null.");

            this.Width = other.Width;
            this.Height = other.Height;
            this.CopyProperties(other);

            // Rent then copy the pixels. Unsafe.CopyBlock gives us a nice speed boost here.
            this.RentPixels();
            using (PixelAccessor<TPixel> sourcePixels = other.Lock())
            using (PixelAccessor<TPixel> target = this.Lock())
            {
                // Check we can do this without crashing
                sourcePixels.CopyTo(target);
            }
        }

        /// <inheritdoc/>
        public TPixel[] Pixels => this.pixelBuffer;

        /// <inheritdoc/>
        public int Width { get; private set; }

        /// <inheritdoc/>
        public int Height { get; private set; }

        /// <inheritdoc/>
        public double PixelRatio => (double)this.Width / this.Height;

        /// <inheritdoc/>
        public Rectangle Bounds => new Rectangle(0, 0, this.Width, this.Height);

        /// <summary>
        /// Gets the configuration providing initialization code which allows extending the library.
        /// </summary>
        public Configuration Configuration { get; private set; }

        /// <summary>
        /// Applies the processor.
        /// </summary>
        /// <param name="processor">The processor.</param>
        /// <param name="rectangle">The rectangle.</param>
        public virtual void ApplyProcessor(IImageProcessor<TPixel> processor, Rectangle rectangle)
        {
            processor.Apply(this, rectangle);
        }

        /// <inheritdoc />
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

        /// <inheritdoc/>
        public PixelAccessor<TPixel> Lock()
        {
            return new PixelAccessor<TPixel>(this);
        }

        /// <summary>
        /// Switches the buffers used by the image and the PixelAccessor meaning that the Image will "own" the buffer from the PixelAccessor and the PixelAccessor will now own the Images buffer.
        /// </summary>
        /// <param name="pixelSource">The pixel source.</param>
        internal void SwapPixelsBuffers(PixelAccessor<TPixel> pixelSource)
        {
            Guard.NotNull(pixelSource, nameof(pixelSource));

            int newWidth = pixelSource.Width;
            int newHeight = pixelSource.Height;

            // Push my memory into the accessor (which in turn unpins the old puffer ready for the images use)
            TPixel[] newPixels = pixelSource.ReturnCurrentColorsAndReplaceThemInternally(this.Width, this.Height, this.pixelBuffer);
            this.Width = newWidth;
            this.Height = newHeight;
            this.pixelBuffer = newPixels;
        }

        /// <summary>
        /// Copies the properties from the other <see cref="IImageBase"/>.
        /// </summary>
        /// <param name="other">
        /// The other <see cref="IImageBase"/> to copy the properties from.
        /// </param>
        protected void CopyProperties(IImageBase other)
        {
            DebugGuard.NotNull(other, nameof(other));

            this.Configuration = other.Configuration;
        }

        /// <summary>
        /// Releases any unmanaged resources from the inheriting class.
        /// </summary>
        protected virtual void ReleaseUnmanagedResources()
        {
            // TODO release unmanaged resources here
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

            this.ReleaseUnmanagedResources();

            if (disposing)
            {
                this.ReturnPixels();
            }

            // Note disposing is done.
            this.isDisposed = true;
        }

        /// <summary>
        /// Rents the pixel array from the pool.
        /// </summary>
        private void RentPixels()
        {
            this.pixelBuffer = PixelDataPool<TPixel>.Rent(this.Width * this.Height);
        }

        /// <summary>
        /// Returns the rented pixel array back to the pool.
        /// </summary>
        private void ReturnPixels()
        {
            PixelDataPool<TPixel>.Return(this.pixelBuffer);
            this.pixelBuffer = null;
        }

        /// <summary>
        /// Clears the pixel array.
        /// </summary>
        private void ClearPixels()
        {
            Array.Clear(this.pixelBuffer, 0, this.Width * this.Height);
        }
    }
}
// <copyright file="ImageBase.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>


namespace ImageProcessorCore
{
    using System;
    using System.Diagnostics;

    /// <summary>
    /// The base class of all images. Encapsulates the basic properties and methods required to manipulate 
    /// images in different pixel formats.
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
    [DebuggerDisplay("Image: {Width}x{Height}")]
    public abstract class ImageBase<TColor, TPacked> : IImageBase<TColor, TPacked>
        where TColor : IPackedVector<TPacked>
        where TPacked : struct
    {
        /// <summary>
        /// The image pixels		
        /// </summary>		
        private TColor[] pixelBuffer;

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageBase{TColor, TPacked}"/> class.
        /// </summary>
        protected ImageBase()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageBase{TColor, TPacked}"/> class.
        /// </summary>
        /// <param name="width">The width of the image in pixels.</param>
        /// <param name="height">The height of the image in pixels.</param>
        /// <exception cref="System.ArgumentOutOfRangeException">
        /// Thrown if either <paramref name="width"/> or <paramref name="height"/> are less than or equal to 0.
        /// </exception>
        protected ImageBase(int width, int height)
        {
            Guard.MustBeGreaterThan(width, 0, nameof(width));
            Guard.MustBeGreaterThan(height, 0, nameof(height));

            this.Width = width;
            this.Height = height;
            this.pixelBuffer = new TColor[width * height];
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageBase{TColor, TPacked}"/> class.
        /// </summary>
        /// <param name="other">
        /// The other <see cref="ImageBase{TColor, TPacked}"/> to create this instance from.
        /// </param>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown if the given <see cref="ImageBase{TColor, TPacked}"/> is null.
        /// </exception>
        protected ImageBase(ImageBase<TColor, TPacked> other)
        {
            Guard.NotNull(other, nameof(other), "Other image cannot be null.");

            this.Width = other.Width;
            this.Height = other.Height;
            this.CopyProperties(other);

            // Copy the pixels. Unsafe.CopyBlock gives us a nice speed boost here.
            this.pixelBuffer = new TColor[this.Width * this.Height];
            using (PixelAccessor<TColor, TPacked> sourcePixels = other.Lock())
            using (PixelAccessor<TColor, TPacked> target = this.Lock())
            {
                sourcePixels.CopyImage(target);
            }
        }

        /// <inheritdoc/>
        public int MaxWidth { get; set; } = int.MaxValue;

        /// <inheritdoc/>
        public int MaxHeight { get; set; } = int.MaxValue;

        /// <inheritdoc/>
        //public TColor[] Pixels { get; private set; }
        public TColor[] Pixels => this.pixelBuffer;

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
        public void SetPixels(int width, int height, TColor[] pixels)
        {
            Guard.MustBeGreaterThan(width, 0, nameof(width));
            Guard.MustBeGreaterThan(height, 0, nameof(height));

            if (pixels.Length != width * height)
            {
                throw new ArgumentException("Pixel array must have the length of Width * Height.");
            }

            this.Width = width;
            this.Height = height;
            this.pixelBuffer = pixels;
        }

        /// <inheritdoc/>
        public void ClonePixels(int width, int height, TColor[] pixels)
        {
            Guard.MustBeGreaterThan(width, 0, nameof(width));
            Guard.MustBeGreaterThan(height, 0, nameof(height));

            if (pixels.Length != width * height)
            {
                throw new ArgumentException("Pixel array must have the length of Width * Height.");
            }

            this.Width = width;
            this.Height = height;

            // Copy the pixels. TODO: use Unsafe.Copy.
            this.pixelBuffer = new TColor[pixels.Length];
            Array.Copy(pixels, this.pixelBuffer, pixels.Length);
        }

        /// <inheritdoc/>
        public PixelAccessor<TColor, TPacked> Lock()
        {
            return new PixelAccessor<TColor, TPacked>(this);
        }

        /// <summary>
        /// Copies the properties from the other <see cref="ImageBase{TColor, TPacked}"/>.
        /// </summary>
        /// <param name="other">
        /// The other <see cref="ImageBase{TColor, TPacked}"/> to copy the properties from.
        /// </param>
        protected void CopyProperties(ImageBase<TColor, TPacked> other)
        {
            this.Quality = other.Quality;
            this.FrameDelay = other.FrameDelay;
        }
    }
}

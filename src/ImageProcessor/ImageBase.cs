// <copyright file="ImageBase.cs" company="James South">
// Copyright (c) James South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor
{
    using System;

    /// <summary>
    /// The base class of all images. Encapsulates the basic properties and methods
    /// required to manipulate images.
    /// </summary>
    public abstract class ImageBase : IImageBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ImageBase"/> class.
        /// </summary>
        protected ImageBase()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageBase"/> class.
        /// </summary>
        /// <param name="width">
        /// The width of the image in pixels.
        /// </param>
        /// <param name="height">
        /// The height of the image in pixels.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if either <paramref name="width"/> or <paramref name="height"/> are less than or equal to 0.
        /// </exception>
        protected ImageBase(int width, int height)
        {
            Guard.MustBeGreaterThan(width, 0, nameof(width));
            Guard.MustBeGreaterThan(height, 0, nameof(height));

            this.Width = width;
            this.Height = height;

            this.Pixels = new float[width * height * 4];
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

            float[] pixels = other.Pixels;

            this.Width = other.Width;
            this.Height = other.Height;
            this.Quality = other.Quality;
            this.FrameDelay = other.FrameDelay;
            this.Pixels = new float[pixels.Length];
            Array.Copy(pixels, this.Pixels, pixels.Length);
        }

        /// <summary>
        /// Gets or sets the maximum allowable width in pixels.
        /// </summary>
        public static int MaxWidth { get; set; } = int.MaxValue;

        /// <summary>
        /// Gets or sets the maximum allowable height in pixels.
        /// </summary>
        public static int MaxHeight { get; set; } = int.MaxValue;

        /// <summary>
        /// Gets the image pixels as byte array.
        /// </summary>
        /// <remarks>
        /// The returned array has a length of Width * Height * 4 bytes
        /// and stores the blue, the green, the red and the alpha value for
        /// each pixel in this order.
        /// </remarks>
        public float[] Pixels { get; private set; }

        /// <summary>
        /// Gets the width in pixels.
        /// </summary>
        public int Width { get; private set; }

        /// <summary>
        /// Gets the height in pixels.
        /// </summary>
        public int Height { get; private set; }

        /// <summary>
        /// Gets the pixel ratio made up of the width and height.
        /// </summary>
        public double PixelRatio => (double)this.Width / this.Height;

        /// <summary>
        /// Gets the <see cref="Rectangle"/> representing the bounds of the image.
        /// </summary>
        public Rectangle Bounds => new Rectangle(0, 0, this.Width, this.Height);

        /// <inheritdoc/>
        public int Quality { get; set; }

        /// <summary>
        /// Gets or sets the frame delay for animated images.
        /// If not 0, this field specifies the number of hundredths (1/100) of a second to
        /// wait before continuing with the processing of the Data Stream.
        /// The clock starts ticking immediately after the graphic is rendered.
        /// </summary>
        public int FrameDelay { get; set; }

        /// <inheritdoc/>
        public Color this[int x, int y]
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

                int start = ((y * this.Width) + x) * 4;
                return new Color(this.Pixels[start], this.Pixels[start + 1], this.Pixels[start + 2], this.Pixels[start + 3]);
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
                int start = ((y * this.Width) + x) * 4;

                this.Pixels[start + 0] = value.R;
                this.Pixels[start + 1] = value.G;
                this.Pixels[start + 2] = value.B;
                this.Pixels[start + 3] = value.A;
            }
        }

        /// <inheritdoc/>
        public void SetPixels(int width, int height, float[] pixels)
        {
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

            this.Width = width;
            this.Height = height;
            this.Pixels = pixels;
        }
    }
}

// <copyright file="ImageBase.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore
{
    using System;

    /// <summary>
    /// The base class of all images. Encapsulates the basic properties and methods
    /// required to manipulate images.
    /// </summary>
    public abstract class ImageBase
    {
        /// <summary>
        /// The array of pixels.
        /// </summary>
        private float[] pixelsArray;

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

            // Copy the pixels.
            this.pixelsArray = new float[this.Width * this.Height * 4];
            Array.Copy(other.pixelsArray, this.pixelsArray, other.pixelsArray.Length);
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
        /// and stores the red, the green, the blue, and the alpha value for
        /// each pixel in this order.
        /// </remarks>
        public float[] Pixels => this.pixelsArray;

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

        /// <summary>
        /// Gets or sets th quality of the image. This affects the output quality of lossy image formats.
        /// </summary>
        public int Quality { get; set; }

        /// <summary>
        /// Gets or sets the frame delay for animated images.
        /// If not 0, this field specifies the number of hundredths (1/100) of a second to
        /// wait before continuing with the processing of the Data Stream.
        /// The clock starts ticking immediately after the graphic is rendered.
        /// </summary>
        public int FrameDelay { get; set; }

        /// <summary>
        /// Sets the pixel array of the image to the given value.
        /// </summary>
        /// <param name="width">The new width of the image. Must be greater than zero.</param>
        /// <param name="height">The new height of the image. Must be greater than zero.</param>
        /// <param name="pixels">
        /// The array with colors. Must be a multiple of four times the width and height.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if either <paramref name="width"/> or <paramref name="height"/> are less than or equal to 0.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if the <paramref name="pixels"/> length is not equal to Width * Height * 4.
        /// </exception>
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
            this.pixelsArray = pixels;
        }

        /// <summary>
        /// Sets the pixel array of the image to the given value, creating a copy of 
        /// the original pixels.
        /// </summary>
        /// <param name="width">The new width of the image. Must be greater than zero.</param>
        /// <param name="height">The new height of the image. Must be greater than zero.</param>
        /// <param name="pixels">
        /// The array with colors. Must be a multiple of four times the width and height.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if either <paramref name="width"/> or <paramref name="height"/> are less than or equal to 0.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if the <paramref name="pixels"/> length is not equal to Width * Height * 4.
        /// </exception>
        public void ClonePixels(int width, int height, float[] pixels)
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

            // Copy the pixels.
            this.pixelsArray = new float[pixels.Length];
            Array.Copy(pixels, this.pixelsArray, pixels.Length);
        }

        /// <summary>
        /// Locks the image providing access to the pixels.
        /// <remarks>
        /// It is imperative that the accessor is correctly disposed off after use.
        /// </remarks>
        /// </summary>
        /// <returns>The <see cref="PixelAccessor"/></returns>
        public PixelAccessor Lock()
        {
            return new PixelAccessor(this);
        }
    }
}

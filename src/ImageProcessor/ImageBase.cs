// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ImageBase.cs" company="James South">
//   Copyright © James South and contributors.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   The base class of all images. Encapsulates all the properties and methods
//   required to manipulate images.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor
{
    using System;

    /// <summary>
    /// The base class of all images. Encapsulates the basic properties and methods 
    /// required to manipulate images.
    /// </summary>
    public abstract class ImageBase
    {
        /// <summary>
        /// The maximum allowable width in pixels.
        /// </summary>
        private static int maxWidth = int.MaxValue;

        /// <summary>
        /// The maximum allowable height in pixels.
        /// </summary>
        private static int maxHeight = int.MaxValue;

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
            Guard.GreaterThan(width, 0, "width");
            Guard.GreaterThan(height, 0, "height");

            this.Width = width;
            this.Height = height;

            this.Pixels = new byte[width * height * 4];
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
            Guard.NotNull(other, "other", "Other image cannot be null.");

            byte[] pixels = other.Pixels;

            this.Width = other.Width;
            this.Height = other.Height;
            this.Pixels = new byte[pixels.Length];
            Array.Copy(pixels, this.Pixels, pixels.Length);
        }

        /// <summary>
        /// Gets or sets the maximum allowable width in pixels.
        /// </summary>
        public static int MaxWidth
        {
            get
            {
                return maxWidth;
            }

            set
            {
                maxWidth = value;
            }
        }

        /// <summary>
        /// Gets or sets the maximum allowable height in pixels.
        /// </summary>
        public static int MaxHeight
        {
            get
            {
                return maxHeight;
            }

            set
            {
                maxHeight = value;
            }
        }

        /// <summary>
        /// Gets the image pixels as byte array.
        /// </summary>
        /// <remarks>
        /// The returned array has a length of Width * Height * 4 bytes
        /// and stores the blue, the green, the red and the alpha value for
        /// each pixel in this order.
        /// </remarks>
        public byte[] Pixels { get; private set; }

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
        public double PixelRatio
        {
            get { return (double)this.Width / this.Height; }
        }

        /// <summary>
        /// Gets the <see cref="Rectangle"/> representing the bounds of the image.
        /// </summary>
        public Rectangle Bounds
        {
            get
            {
                return new Rectangle(0, 0, this.Width, this.Height);
            }
        }

        /// <summary>
        /// Gets or sets the color of a pixel at the specified position.
        /// </summary>
        /// <param name="x">
        /// The x-coordinate of the pixel. Must be greater
        /// than zero and smaller than the width of the pixel.
        /// </param>
        /// <param name="y">
        /// The y-coordinate of the pixel. Must be greater
        /// than zero and smaller than the width of the pixel.
        /// </param>
        /// <returns>The <see cref="Bgra"/> at the specified position.</returns>
        public Bgra this[int x, int y]
        {
            get
            {
#if DEBUG
                if ((x < 0) || (x >= this.Width))
                {
                    throw new ArgumentOutOfRangeException("x", "Value cannot be less than zero or greater than the bitmap width.");
                }

                if ((y < 0) || (y >= this.Width))
                {
                    throw new ArgumentOutOfRangeException("y", "Value cannot be less than zero or greater than the bitmap height.");
                }
#endif

                int start = ((y * this.Width) + x) * 4;
                return new Bgra(this.Pixels[start], this.Pixels[start + 1], this.Pixels[start + 2], this.Pixels[start + 3]);
            }

            set
            {
#if DEBUG
                if ((x < 0) || (x >= this.Width))
                {
                    throw new ArgumentOutOfRangeException("x", "Value cannot be less than zero or greater than the bitmap width.");
                }

                if ((y < 0) || (y >= this.Width))
                {
                    throw new ArgumentOutOfRangeException("y", "Value cannot be less than zero or greater than the bitmap height.");
                }
#endif

                int start = ((y * this.Width) + x) * 4;

                this.Pixels[start + 0] = value.B;
                this.Pixels[start + 1] = value.G;
                this.Pixels[start + 2] = value.R;
                this.Pixels[start + 3] = value.A;
            }
        }

        /// <summary>
        /// Sets the pixel array of the image.
        /// </summary>
        /// <param name="width">
        /// The new width of the image. Must be greater than zero.</param>
        /// <param name="height">The new height of the image. Must be greater than zero.</param>
        /// <param name="pixels">
        /// The array with colors. Must be a multiple
        /// of four, width and height.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if either <paramref name="width"/> or <paramref name="height"/> are less than or equal to 0.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if the <paramref name="pixels"/> length is not equal to Width * Height * 4.
        /// </exception>
        public void SetPixels(int width, int height, byte[] pixels)
        {
            if (width <= 0)
            {
                throw new ArgumentOutOfRangeException("width", "Width must be greater than or equals than zero.");
            }

            if (height <= 0)
            {
                throw new ArgumentOutOfRangeException("height", "Height must be greater than or equal than zero.");
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

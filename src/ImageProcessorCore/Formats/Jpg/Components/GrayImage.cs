// <copyright file="GrayImage.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Formats
{
    /// <summary>
    /// Represents a grayscale image
    /// </summary>
    internal class GrayImage
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GrayImage"/> class.
        /// </summary>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        public GrayImage(int width, int height)
        {
            this.Width = width;
            this.Height = height;
            this.Pixels = new byte[width * height];
            this.Stride = width;
            this.Offset = 0;
        }

        /// <summary>
        /// Prevents a default instance of the <see cref="GrayImage"/> class from being created.
        /// </summary>
        private GrayImage()
        {
        }

        /// <summary>
        /// Gets or sets the pixels.
        /// </summary>
        public byte[] Pixels { get; set; }

        /// <summary>
        /// Gets or sets the stride.
        /// </summary>
        public int Stride { get; set; }

        /// <summary>
        /// Gets or sets the horizontal position.
        /// </summary>
        public int X { get; set; }

        /// <summary>
        /// Gets or sets the vertical position.
        /// </summary>
        public int Y { get; set; }

        /// <summary>
        /// Gets or sets the width.
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// Gets or sets the height.
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// Gets or sets the offset
        /// </summary>
        public int Offset { get; set; }

        /// <summary>
        /// Gets an image made up of a subset of the originals pixels.
        /// </summary>
        /// <param name="x">The x-coordinate of the image.</param>
        /// <param name="y">The y-coordinate of the image.</param>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        /// <returns>
        /// The <see cref="GrayImage"/>.
        /// </returns>
        public GrayImage Subimage(int x, int y, int width, int height)
        {
            return new GrayImage
            {
                Width = width,
                Height = height,
                Pixels = this.Pixels,
                Stride = this.Stride,
                Offset = (y * this.Stride) + x
            };
        }

        /// <summary>
        /// Gets the row offset at the given position
        /// </summary>
        /// <param name="y">The y-coordinate of the image.</param>
        /// <returns>The <see cref="int"/></returns>
        public int GetRowOffset(int y)
        {
            return this.Offset + (y * this.Stride);
        }
    }
}

// <copyright file="IImageBase.cs" company="James South">
// Copyright (c) James South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor
{
    using System;

    /// <summary>
    /// Encapsulates the basic properties and methods required to manipulate images.
    /// </summary>
    public interface IImageBase
    {
        /// <summary>
        /// Gets the image pixels as byte array.
        /// </summary>
        /// <remarks>
        /// The returned array has a length of Width * Height * 4 bytes
        /// and stores the blue, the green, the red and the alpha value for
        /// each pixel in this order.
        /// </remarks>
        byte[] Pixels { get; }

        /// <summary>
        /// Gets the width in pixels.
        /// </summary>
        int Width { get; }

        /// <summary>
        /// Gets the height in pixels.
        /// </summary>
        int Height { get; }

        /// <summary>
        /// Gets the pixel ratio made up of the width and height.
        /// </summary>
        double PixelRatio { get; }

        /// <summary>
        /// Gets the <see cref="Rectangle"/> representing the bounds of the image.
        /// </summary>
        Rectangle Bounds { get; }

        /// <summary>
        /// Gets or sets th quality of the image. This affects the output quality of lossy image formats.
        /// </summary>
        int Quality { get; set; }

        /// <summary>
        /// Gets or sets the frame delay for animated images.
        /// If not 0, this field specifies the number of hundredths (1/100) of a second to
        /// wait before continuing with the processing of the Data Stream.
        /// The clock starts ticking immediately after the graphic is rendered.
        /// </summary>
        int FrameDelay { get; set; }

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
        /// <returns>The <see cref="Bgra32"/> at the specified position.</returns>
        Bgra32 this[int x, int y] { get; set; }

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
        void SetPixels(int width, int height, byte[] pixels);
    }
}

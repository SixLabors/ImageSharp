// <copyright file="IImage.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    using ImageProcessor.Formats;

    /// <summary>
    /// Encapsulates an image, which consists of the pixel data for a graphics image and its attributes.
    /// </summary>
    public interface IImage : IImageBase
    {
        /// <summary>
        /// Gets or sets the resolution of the image in x- direction. It is defined as
        ///  number of dots per inch and should be an positive value.
        /// </summary>
        /// <value>The density of the image in x- direction.</value>
        double HorizontalResolution { get; set; }

        /// <summary>
        /// Gets or sets the resolution of the image in y- direction. It is defined as
        /// number of dots per inch and should be an positive value.
        /// </summary>
        /// <value>The density of the image in y- direction.</value>
        double VerticalResolution { get; set; }

        /// <summary>
        /// Gets the width of the image in inches. It is calculated as the width of the image
        /// in pixels multiplied with the density. When the density is equals or less than zero
        /// the default value is used.
        /// </summary>
        /// <value>The width of the image in inches.</value>
        double InchWidth { get; }

        /// <summary>
        /// Gets the height of the image in inches. It is calculated as the height of the image
        /// in pixels multiplied with the density. When the density is equals or less than zero
        /// the default value is used.
        /// </summary>
        /// <value>The height of the image in inches.</value>
        double InchHeight { get; }

        /// <summary>
        /// Gets a value indicating whether this image is animated.
        /// </summary>
        /// <value>
        /// <c>True</c> if this image is animated; otherwise, <c>false</c>.
        /// </value>
        bool IsAnimated { get; }

        /// <summary>
        /// Gets or sets the number of times any animation is repeated.
        /// <remarks>0 means to repeat indefinitely.</remarks>
        /// </summary>
        ushort RepeatCount { get; set; }

        /// <summary>
        /// Gets the currently loaded image format.
        /// </summary>
        IImageFormat CurrentImageFormat { get; }

        /// <summary>
        /// Gets the other frames for the animation.
        /// </summary>
        /// <value>The list of frame images.</value>
        IList<ImageFrame> Frames { get; }

        /// <summary>
        /// Gets the list of properties for storing meta information about this image.
        /// </summary>
        /// <value>A list of image properties.</value>
        IList<ImageProperty> Properties { get; }

        /// <summary>
        /// Saves the image to the given stream using the currently loaded image format.
        /// </summary>
        /// <param name="stream">The stream to save the image to.</param>
        /// <exception cref="ArgumentNullException">Thrown if the stream is null.</exception>
        void Save(Stream stream);

        /// <summary>
        /// Saves the image to the given stream using the given image format.
        /// </summary>
        /// <param name="stream">The stream to save the image to.</param>
        /// <param name="format">The format to save the image as.</param>
        /// <exception cref="ArgumentNullException">Thrown if the stream is null.</exception>
        void Save(Stream stream, IImageFormat format);
    }
}
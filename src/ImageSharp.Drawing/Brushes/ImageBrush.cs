// <copyright file="ImageBrush.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Drawing.Brushes
{
    using ImageSharp.PixelFormats;

    /// <summary>
    /// Provides an implementation of a solid brush for painting with repeating images. The brush uses <see cref="Rgba32"/> for painting.
    /// </summary>
    public class ImageBrush : ImageBrush<Rgba32>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ImageBrush" /> class.
        /// </summary>
        /// <param name="image">The image to paint.</param>
        public ImageBrush(IImageBase<Rgba32> image)
            : base(image)
        {
        }
    }
}

// <copyright file="SolidBrush.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Drawing.Brushes
{
    using ImageSharp.PixelFormats;

    /// <summary>
    /// Provides an implementation of a solid brush for painting solid color areas. The brush uses <see cref="Rgba32"/> for painting.
    /// </summary>
    public class SolidBrush : SolidBrush<Rgba32>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SolidBrush" /> class.
        /// </summary>
        /// <param name="color">The color.</param>
        public SolidBrush(Rgba32 color)
            : base(color)
        {
        }
    }
}

// <copyright file="SolidBrush.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Drawing.Brushes
{
    /// <summary>
    /// Provides an implementation of a solid brush for painting solid color areas. The brush uses <see cref="Color"/> for painting.
    /// </summary>
    public class SolidBrush : SolidBrush<Color>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SolidBrush" /> class.
        /// </summary>
        /// <param name="color">The color.</param>
        public SolidBrush(Color color)
            : base(color)
        {
        }
    }
}

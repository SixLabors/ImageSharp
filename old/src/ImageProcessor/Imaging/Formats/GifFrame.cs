// --------------------------------------------------------------------------------------------------------------------
// <copyright file="GifFrame.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   A single gif frame.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging.Formats
{
    using System;
    using System.Drawing;

    /// <summary>
    /// A single gif frame.
    /// </summary>
    public class GifFrame
    {
        /// <summary>
        /// Gets or sets the image.
        /// </summary>
        public Image Image { get; set; }

        /// <summary>
        /// Gets or sets the delay in milliseconds.
        /// </summary>
        public TimeSpan Delay { get; set; }

        /// <summary>
        /// Gets or sets the x position of the frame.
        /// </summary>
        public int X { get; set; }

        /// <summary>
        /// Gets or sets the Y position of the frame.
        /// </summary>
        public int Y { get; set; }
    }
}

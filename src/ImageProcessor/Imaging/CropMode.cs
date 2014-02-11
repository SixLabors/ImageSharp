// --------------------------------------------------------------------------------------------------------------------
// <copyright file="CropMode.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Enumerated cop modes to apply to cropped images.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging
{
    /// <summary>
    /// Enumerated cop modes to apply to cropped images.
    /// </summary>
    public enum CropMode
    {
        /// <summary>
        /// Crops the image using the standard rectangle model of x, y, width, height.
        /// </summary>
        Pixels,

        /// <summary>
        /// Crops the image using percentages model left, top, right, bottom.
        /// </summary>
        Percentage
    }
}

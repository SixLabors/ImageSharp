// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ImageLayer.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Encapsulates the properties required to add an rotation layer to an image.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging
{
    /// <summary>
    /// A rotation layer to apply an inside rotation to an image
    /// </summary>
    public class RotateInsideLayer
    {
        /// <summary>
        /// Gets or sets the rotation angle.
        /// </summary>
        public float Angle { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to keep the image dimensions.
        /// If set to true, the image is zoomed inside the area.
        /// If set to false, the area is resized to match the rotated image.
        /// </summary>
        public bool KeepImageDimensions { get; set; }
    }
}
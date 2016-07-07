// <copyright file="ResizeMode.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore
{
    /// <summary>
    /// Enumerated resize modes to apply to resized images.
    /// </summary>
    public enum ResizeMode
    {
        /// <summary>
        /// Crops the resized image to fit the bounds of its container.
        /// </summary>
        Crop,

        /// <summary>
        /// Pads the resized image to fit the bounds of its container.
        /// If only one dimension is passed, will maintain the original aspect ratio.
        /// </summary>
        Pad,

        /// <summary>
        /// Pads the image to fit the bound of the container without resizing the 
        /// original source.
        /// When downscaling, performs the same functionality as <see cref="ResizeMode.Pad"/>
        /// </summary>
        BoxPad,

        /// <summary>
        /// Constrains the resized image to fit the bounds of its container maintaining
        /// the original aspect ratio. 
        /// </summary>
        Max,

        /// <summary>
        /// Resizes the image until the shortest side reaches the set given dimension.
        /// Upscaling is disabled in this mode and the original image will be returned 
        /// if attempted.
        /// </summary>
        Min,

        /// <summary>
        /// Stretches the resized image to fit the bounds of its container.
        /// </summary>
        Stretch
    }
}

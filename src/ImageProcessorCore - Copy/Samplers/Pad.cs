// <copyright file="Pad.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>-------------------------------------------------------------------------------------------------------------------

namespace ImageProcessorCore
{
    using Processors;

    /// <summary>
    /// Extension methods for the <see cref="Image"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Evenly pads an image to fit the new dimensions.
        /// </summary>
        /// <param name="source">The source image to pad.</param>
        /// <param name="width">The new width.</param>
        /// <param name="height">The new height.</param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Pad(this Image source, int width, int height, ProgressEventHandler progressHandler = null)
        {
            ResizeOptions options = new ResizeOptions
            {
                Size = new Size(width, height),
                Mode = ResizeMode.BoxPad,
                Sampler = new NearestNeighborResampler()
            };

            return Resize(source, options, progressHandler);
        }
    }
}

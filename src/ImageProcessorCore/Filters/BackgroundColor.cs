// <copyright file="BackgroundColor.cs" company="James Jackson-South">
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
        /// Combines the given image together with the current one by blending their pixels.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="color">The color to set as the background.</param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image BackgroundColor(this Image source, Color color, ProgressEventHandler progressHandler = null)
        {
            BackgroundColorProcessor processor = new BackgroundColorProcessor(color);
            processor.OnProgress += progressHandler;

            try
            {
                return source.Process(source.Bounds, processor);
            }
            finally
            {
                processor.OnProgress -= progressHandler;
            }
        }
    }
}

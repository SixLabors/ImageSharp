// <copyright file="BoxBlur.cs" company="James Jackson-South">
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
        /// Applies a box blur to the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="radius">The 'radius' value representing the size of the area to sample.</param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image BoxBlur(this Image source, int radius = 7, ProgressEventHandler progressHandler = null)
        {
            return BoxBlur(source, radius, source.Bounds, progressHandler);
        }

        /// <summary>
        /// Applies a box blur to the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="radius">The 'radius' value representing the size of the area to sample.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image BoxBlur(this Image source, int radius, Rectangle rectangle, ProgressEventHandler progressHandler = null)
        {
            BoxBlurProcessor processor = new BoxBlurProcessor(radius);
            processor.OnProgress += progressHandler;

            try
            {
                return source.Process(rectangle, processor);
            }
            finally
            {
                processor.OnProgress -= progressHandler;
            }
        }
    }
}

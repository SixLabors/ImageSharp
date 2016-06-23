// <copyright file="DetectEdges.cs" company="James Jackson-South">
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
        /// Detects any edges within the image. Uses the <see cref="SobelProcessor"/> filter
        /// operating in greyscale mode.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image DetectEdges(this Image source, ProgressEventHandler progressHandler = null)
        {
            return DetectEdges(source, source.Bounds, new SobelProcessor { Greyscale = true }, progressHandler);
        }

        /// <summary>
        /// Detects any edges within the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="filter">The filter for detecting edges.</param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image DetectEdges(this Image source, IEdgeDetectorFilter filter, ProgressEventHandler progressHandler = null)
        {
            return DetectEdges(source, source.Bounds, filter, progressHandler);
        }

        /// <summary>
        /// Detects any edges within the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <param name="filter">The filter for detecting edges.</param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image DetectEdges(this Image source, Rectangle rectangle, IEdgeDetectorFilter filter, ProgressEventHandler progressHandler = null)
        {
            filter.OnProgress += progressHandler;

            try
            {
                return source.Process(rectangle, filter);
            }
            finally
            {
                filter.OnProgress -= progressHandler;
            }
        }
    }
}

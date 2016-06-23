// <copyright file="GuassianSharpen.cs" company="James Jackson-South">
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
        /// Applies a Guassian sharpening filter to the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="sigma">The 'sigma' value representing the weight of the blur.</param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image GuassianSharpen(this Image source, float sigma = 3f, ProgressEventHandler progressHandler = null)
        {
            return GuassianSharpen(source, sigma, source.Bounds, progressHandler);
        }

        /// <summary>
        /// Applies a Guassian sharpening filter to the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="sigma">The 'sigma' value representing the weight of the blur.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image GuassianSharpen(this Image source, float sigma, Rectangle rectangle, ProgressEventHandler progressHandler = null)
        {
            GuassianSharpenProcessor processor = new GuassianSharpenProcessor(sigma);
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

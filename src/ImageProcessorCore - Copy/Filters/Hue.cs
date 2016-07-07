// <copyright file="Hue.cs" company="James Jackson-South">
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
        /// Alters the hue component of the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="degrees">The angle in degrees to adjust the image.</param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Hue(this Image source, float degrees, ProgressEventHandler progressHandler = null)
        {
            return Hue(source, degrees, source.Bounds, progressHandler);
        }

        /// <summary>
        /// Alters the hue component of the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="degrees">The angle in degrees to adjust the image.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Hue(this Image source, float degrees, Rectangle rectangle, ProgressEventHandler progressHandler = null)
        {
            HueProcessor processor = new HueProcessor(degrees);
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

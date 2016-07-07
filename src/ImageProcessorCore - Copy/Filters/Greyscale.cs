// <copyright file="Greyscale.cs" company="James Jackson-South">
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
        /// Applies greyscale toning to the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="mode">The formula to apply to perform the operation.</param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Greyscale(this Image source, GreyscaleMode mode = GreyscaleMode.Bt709, ProgressEventHandler progressHandler = null)
        {
            return Greyscale(source, source.Bounds, mode, progressHandler);
        }

        /// <summary>
        /// Applies greyscale toning to the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <param name="mode">The formula to apply to perform the operation.</param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Greyscale(this Image source, Rectangle rectangle, GreyscaleMode mode = GreyscaleMode.Bt709, ProgressEventHandler progressHandler = null)
        {
            IImageProcessor processor = mode == GreyscaleMode.Bt709
                ? (IImageProcessor)new GreyscaleBt709Processor()
                : new GreyscaleBt601Processor();

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

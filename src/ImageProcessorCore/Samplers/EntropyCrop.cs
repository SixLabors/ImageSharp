// <copyright file="EntropyCrop.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>-------------------------------------------------------------------------------------------------------------------

namespace ImageProcessorCore
{
    /// <summary>
    /// Extension methods for the <see cref="Image"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Crops an image to the area of greatest entropy.
        /// </summary>
        /// <param name="source">The image to crop.</param>
        /// <param name="threshold">The threshold for entropic density.</param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image"/></returns>
        public static Image EntropyCrop(this Image source, float threshold = .5f, ProgressEventHandler progressHandler = null)
        {
            EntropyCropProcessor processor = new EntropyCropProcessor(threshold);
            processor.OnProgress += progressHandler;

            try
            {
                return source.Process(source.Width, source.Height, source.Bounds, Rectangle.Empty, processor);
            }
            finally
            {
                processor.OnProgress -= progressHandler;
            }
        }
    }
}

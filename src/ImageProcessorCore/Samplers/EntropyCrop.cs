// <copyright file="EntropyCrop.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore
{
    using Processors;

    /// <summary>
    /// Extension methods for the <see cref="Image{TColor, TPacked}"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Crops an image to the area of greatest entropy.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="source">The image to crop.</param>
        /// <param name="threshold">The threshold for entropic density.</param>
        /// <returns>The <see cref="Image"/></returns>
        public static Image<TColor, TPacked> EntropyCrop<TColor, TPacked>(this Image<TColor, TPacked> source, float threshold = .5f)
            where TColor : struct, IPackedPixel<TPacked>
            where TPacked : struct
        {
            EntropyCropProcessor<TColor, TPacked> processor = new EntropyCropProcessor<TColor, TPacked>(threshold);
            return source.Process(source.Width, source.Height, source.Bounds, Rectangle.Empty, processor);
        }
    }
}
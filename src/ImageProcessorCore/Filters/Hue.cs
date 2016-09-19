// <copyright file="Hue.cs" company="James Jackson-South">
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
        /// Alters the hue component of the image.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="degrees">The angle in degrees to adjust the image.</param>
        /// <returns>The <see cref="Image{TColor, TPacked}"/>.</returns>
        public static Image<TColor, TPacked> Hue<TColor, TPacked>(this Image<TColor, TPacked> source, float degrees)
            where TColor : IPackedVector<TPacked>
            where TPacked : struct
        {
            return Hue(source, degrees, source.Bounds);
        }

        /// <summary>
        /// Alters the hue component of the image.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="degrees">The angle in degrees to adjust the image.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image{TColor, TPacked}"/>.</returns>
        public static Image<TColor, TPacked> Hue<TColor, TPacked>(this Image<TColor, TPacked> source, float degrees, Rectangle rectangle)
            where TColor : IPackedVector<TPacked>
            where TPacked : struct
        {
            return source.Process(rectangle, new HueProcessor<TColor, TPacked>(degrees));
        }
    }
}

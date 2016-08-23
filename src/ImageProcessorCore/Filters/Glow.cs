// <copyright file="Glow.cs" company="James Jackson-South">
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
        /// Applies a radial glow effect to an image.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image{TColor, TPacked}"/>.</returns>
        public static Image<TColor, TPacked> Glow<TColor, TPacked>(this Image<TColor, TPacked> source, ProgressEventHandler progressHandler = null)
            where TColor : IPackedVector<TPacked>
            where TPacked : struct
        {
            return Glow(source, default(TColor), source.Bounds.Width * .5F, source.Bounds, progressHandler);
        }

        /// <summary>
        /// Applies a radial glow effect to an image.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="color">The color to set as the glow.</param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image{TColor, TPacked}"/>.</returns>
        public static Image<TColor, TPacked> Glow<TColor, TPacked>(this Image<TColor, TPacked> source, TColor color, ProgressEventHandler progressHandler = null)
            where TColor : IPackedVector<TPacked>
            where TPacked : struct
        {
            return Glow(source, color, source.Bounds.Width * .5F, source.Bounds, progressHandler);
        }

        /// <summary>
        /// Applies a radial glow effect to an image.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="radius">The the radius.</param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image{TColor, TPacked}"/>.</returns>
        public static Image<TColor, TPacked> Glow<TColor, TPacked>(this Image<TColor, TPacked> source, float radius, ProgressEventHandler progressHandler = null)
            where TColor : IPackedVector<TPacked>
            where TPacked : struct
        {
            return Glow(source, default(TColor), radius, source.Bounds, progressHandler);
        }

        /// <summary>
        /// Applies a radial glow effect to an image.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image{TColor, TPacked}"/>.</returns>
        public static Image<TColor, TPacked> Glow<TColor, TPacked>(this Image<TColor, TPacked> source, Rectangle rectangle, ProgressEventHandler progressHandler = null)
            where TColor : IPackedVector<TPacked>
            where TPacked : struct
        {
            return Glow(source, default(TColor), 0, rectangle, progressHandler);
        }

        /// <summary>
        /// Applies a radial glow effect to an image.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="color">The color to set as the glow.</param>
        /// <param name="radius">The the radius.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image{TColor, TPacked}"/>.</returns>
        public static Image<TColor, TPacked> Glow<TColor, TPacked>(this Image<TColor, TPacked> source, TColor color, float radius, Rectangle rectangle, ProgressEventHandler progressHandler = null)
            where TColor : IPackedVector<TPacked>
            where TPacked : struct
        {
            GlowProcessor<TColor, TPacked> processor = new GlowProcessor<TColor, TPacked> { Radius = radius, };

            if (!color.Equals(default(TColor)))
            {
                processor.GlowColor = color;
            }

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

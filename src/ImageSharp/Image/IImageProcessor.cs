// <copyright file="IImageProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Processing
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Encapsulates methods to alter the pixels of an image.
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    public interface IImageProcessor<TColor>
        where TColor : struct, IPackedPixel, IEquatable<TColor>
    {
        /// <summary>
        /// Gets or sets the parallel options for processing tasks in parallel.
        /// </summary>
        ParallelOptions ParallelOptions { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to compress
        /// or expand individual pixel colors the value on processing.
        /// </summary>
        bool Compand { get; set; }

        /// <summary>
        /// Applies the process to the specified portion of the specified <see cref="ImageBase{TColor}"/>.
        /// </summary>
        /// <param name="source">The source image. Cannot be null.</param>
        /// <param name="sourceRectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to draw.
        /// </param>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="source"/> is null.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// <paramref name="sourceRectangle"/> doesnt fit the dimension of the image.
        /// </exception>
        void Apply(ImageBase<TColor> source, Rectangle sourceRectangle);
    }
}

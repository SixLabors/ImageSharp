// <copyright file="IImageOperationsProvider.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using ImageSharp.PixelFormats;

    /// <summary>
    /// Represents an interface that will create IImageOperations
    /// </summary>
    internal interface IImageOperationsProvider
    {
        /// <summary>
        /// Called during Mutate operations to generate the imageoperations provider.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format</typeparam>
        /// <param name="source">The source image.</param>
        /// <returns>A new IImageOPeration</returns>
        IImageOperations<TPixel> CreateMutator<TPixel>(Image<TPixel> source)
            where TPixel : struct, IPixel<TPixel>;
    }

    /// <summary>
    /// The default implmentation of IImageOperationsProvider
    /// </summary>
    internal class DefaultImageOperationsProvider : IImageOperationsProvider
    {
        /// <inheritdoc/>
        public IImageOperations<TPixel> CreateMutator<TPixel>(Image<TPixel> source)
            where TPixel : struct, IPixel<TPixel>
        {
            return new ImageOperations<TPixel>(source);
        }
    }
}

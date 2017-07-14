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
    internal interface IImageProcessorApplicatorFactory
    {
        /// <summary>
        /// Called during Mutate operations to generate the imageoperations provider.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format</typeparam>
        /// <param name="source">The source image.</param>
        /// <param name="mutate">A flag to determin with the image operations is allowed to mutate the source image or not.</param>
        /// <returns>A new IImageOPeration</returns>
        IInternalImageProcessorApplicator<TPixel> CreateImageOperations<TPixel>(Image<TPixel> source, bool mutate)
            where TPixel : struct, IPixel<TPixel>;
    }

    /// <summary>
    /// The default implmentation of IImageOperationsProvider
    /// </summary>
    internal class DefaultImageOperationsProvider : IImageProcessorApplicatorFactory
    {
        /// <inheritdoc/>
        public IInternalImageProcessorApplicator<TPixel> CreateImageOperations<TPixel>(Image<TPixel> source, bool mutate)
            where TPixel : struct, IPixel<TPixel>
        {
            return new DefaultInternalImageProcessorApplicator<TPixel>(source, mutate);
        }
    }
}

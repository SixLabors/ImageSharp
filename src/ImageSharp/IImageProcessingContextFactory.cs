// <copyright file="IImageProcessorApplicatorFactory.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using ImageSharp.PixelFormats;

    /// <summary>
    /// Represents an interface that will create IInternalImageProcessingContext instances
    /// </summary>
    internal interface IImageProcessingContextFactory
    {
        /// <summary>
        /// Called during Mutate operations to generate the image operations provider.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format</typeparam>
        /// <param name="source">The source image.</param>
        /// <param name="mutate">A flag to determin with the image operations is allowed to mutate the source image or not.</param>
        /// <returns>A new IImageOPeration</returns>
        IInternalImageProcessingContext<TPixel> CreateImageProcessingContext<TPixel>(Image<TPixel> source, bool mutate)
            where TPixel : struct, IPixel<TPixel>;
    }

    /// <summary>
    /// The default implmentation of IImageOperationsProvider
    /// </summary>
    internal class DefaultImageOperationsProvider : IImageProcessingContextFactory
    {
        /// <inheritdoc/>
        public IInternalImageProcessingContext<TPixel> CreateImageProcessingContext<TPixel>(Image<TPixel> source, bool mutate)
            where TPixel : struct, IPixel<TPixel>
        {
            return new DefaultInternalImageProcessorContext<TPixel>(source, mutate);
        }
    }
}

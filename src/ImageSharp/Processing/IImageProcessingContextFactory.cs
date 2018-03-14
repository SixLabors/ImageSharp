// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Represents an interface that will create IInternalImageProcessingContext instances
    /// </summary>
    internal interface IImageProcessingContextFactory
    {
        /// <summary>
        /// Called during mutate operations to generate the image operations provider.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format</typeparam>
        /// <param name="source">The source image.</param>
        /// <param name="mutate">A flag to determine whether image operations are allowed to mutate the source image.</param>
        /// <returns>A new <see cref="IInternalImageProcessingContext{TPixel}"/></returns>
        IInternalImageProcessingContext<TPixel> CreateImageProcessingContext<TPixel>(Image<TPixel> source, bool mutate)
            where TPixel : struct, IPixel<TPixel>;
    }

    /// <summary>
    /// The default implementation of <see cref="IImageProcessingContextFactory"/>
    /// </summary>
    internal class DefaultImageOperationsProviderFactory : IImageProcessingContextFactory
    {
        /// <inheritdoc/>
        public IInternalImageProcessingContext<TPixel> CreateImageProcessingContext<TPixel>(Image<TPixel> source, bool mutate)
            where TPixel : struct, IPixel<TPixel>
        {
            return new DefaultInternalImageProcessorContext<TPixel>(source, mutate);
        }
    }
}
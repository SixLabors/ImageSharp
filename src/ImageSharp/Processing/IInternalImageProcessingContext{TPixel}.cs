// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// An interface for internal operations we don't want to expose on <see cref="IImageProcessingContext"/>.
    /// </summary>
    /// <typeparam name="TPixel">The pixel type.</typeparam>
    internal interface IInternalImageProcessingContext<TPixel> : IImageProcessingContext
        where TPixel : unmanaged, IPixel<TPixel>
    {
        /// <summary>
        /// Returns the result image to return by <see cref="ProcessingExtensions.Clone(Image, Configuration, System.Action{IImageProcessingContext})"/>
        /// (and other overloads).
        /// </summary>
        /// <returns>The current image or a new image depending on whether it is requested to mutate the source image.</returns>
        Image<TPixel> GetResultImage();
    }
}

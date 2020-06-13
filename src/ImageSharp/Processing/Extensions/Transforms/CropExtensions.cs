// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Processing.Processors.Transforms;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Defines extensions that allow the application of cropping operations on an <see cref="Image"/>
    /// using Mutate/Clone.
    /// </summary>
    public static class CropExtensions
    {
        /// <summary>
        /// Crops an image to the given width and height.
        /// </summary>
        /// <param name="source">The image to resize.</param>
        /// <param name="width">The target image width.</param>
        /// <param name="height">The target image height.</param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext Crop(this IImageProcessingContext source, int width, int height) =>
            Crop(source, new Rectangle(0, 0, width, height));

        /// <summary>
        /// Crops an image to the given rectangle.
        /// </summary>
        /// <param name="source">The image to crop.</param>
        /// <param name="cropRectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to retain.
        /// </param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext Crop(this IImageProcessingContext source, Rectangle cropRectangle) =>
            source.ApplyProcessor(new CropProcessor(cropRectangle, source.GetCurrentSize()));
    }
}
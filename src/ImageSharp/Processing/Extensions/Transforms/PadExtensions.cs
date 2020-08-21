// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Defines extensions that allow the application of padding operations on an <see cref="Image"/>
    /// using Mutate/Clone.
    /// </summary>
    public static class PadExtensions
    {
        /// <summary>
        /// Evenly pads an image to fit the new dimensions.
        /// </summary>
        /// <param name="source">The source image to pad.</param>
        /// <param name="width">The new width.</param>
        /// <param name="height">The new height.</param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext Pad(this IImageProcessingContext source, int width, int height)
            => source.Pad(width, height, default);

        /// <summary>
        /// Evenly pads an image to fit the new dimensions with the given background color.
        /// </summary>
        /// <param name="source">The source image to pad.</param>
        /// <param name="width">The new width.</param>
        /// <param name="height">The new height.</param>
        /// <param name="color">The background color with which to pad the image.</param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext Pad(this IImageProcessingContext source, int width, int height, Color color)
        {
            var options = new ResizeOptions
            {
                Size = new Size(width, height),
                Mode = ResizeMode.BoxPad,
                Sampler = KnownResamplers.NearestNeighbor,
            };

            return color.Equals(default) ? source.Resize(options) : source.Resize(options).BackgroundColor(color);
        }
    }
}
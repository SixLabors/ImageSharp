// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Processing.Processors.Overlays;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Defines extension methods to replace the background color of an <see cref="Image"/>
    /// using Mutate/Clone.
    /// </summary>
    public static class BackgroundColorExtensions
    {
        /// <summary>
        /// Replaces the background color of image with the given one.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="color">The color to set as the background.</param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext BackgroundColor(this IImageProcessingContext source, Color color) =>
            BackgroundColor(source, source.GetGraphicsOptions(), color);

        /// <summary>
        /// Replaces the background color of image with the given one.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="color">The color to set as the background.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext BackgroundColor(
            this IImageProcessingContext source,
            Color color,
            Rectangle rectangle) =>
            BackgroundColor(source, source.GetGraphicsOptions(), color, rectangle);

        /// <summary>
        /// Replaces the background color of image with the given one.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="options">The options effecting pixel blending.</param>
        /// <param name="color">The color to set as the background.</param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext BackgroundColor(
            this IImageProcessingContext source,
            GraphicsOptions options,
            Color color) =>
            source.ApplyProcessor(new BackgroundColorProcessor(options, color));

        /// <summary>
        /// Replaces the background color of image with the given one.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="options">The options effecting pixel blending.</param>
        /// <param name="color">The color to set as the background.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext BackgroundColor(
            this IImageProcessingContext source,
            GraphicsOptions options,
            Color color,
            Rectangle rectangle) =>
            source.ApplyProcessor(new BackgroundColorProcessor(options, color), rectangle);
    }
}

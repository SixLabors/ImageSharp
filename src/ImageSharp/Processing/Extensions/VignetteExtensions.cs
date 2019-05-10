// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Primitives;
using SixLabors.ImageSharp.Processing.Processors.Overlays;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Defines extensions that allow the application of a radial glow to an <see cref="Image"/>
    /// using Mutate/Clone.
    /// </summary>
    public static class VignetteExtensions
    {
        /// <summary>
        /// Applies a radial vignette effect to an image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext Vignette(this IImageProcessingContext source) =>
            Vignette(source, GraphicsOptions.Default);

        /// <summary>
        /// Applies a radial vignette effect to an image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="color">The color to set as the vignette.</param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext Vignette(this IImageProcessingContext source, Color color) =>
            Vignette(source, GraphicsOptions.Default, color);

        /// <summary>
        /// Applies a radial vignette effect to an image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="radiusX">The the x-radius.</param>
        /// <param name="radiusY">The the y-radius.</param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext Vignette(
            this IImageProcessingContext source,
            float radiusX,
            float radiusY) =>
            Vignette(source, GraphicsOptions.Default, radiusX, radiusY);

        /// <summary>
        /// Applies a radial vignette effect to an image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext Vignette(this IImageProcessingContext source, Rectangle rectangle) =>
            Vignette(source, GraphicsOptions.Default, rectangle);

        /// <summary>
        /// Applies a radial vignette effect to an image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="color">The color to set as the vignette.</param>
        /// <param name="radiusX">The the x-radius.</param>
        /// <param name="radiusY">The the y-radius.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext Vignette(
            this IImageProcessingContext source,
            Color color,
            float radiusX,
            float radiusY,
            Rectangle rectangle) =>
            source.Vignette(GraphicsOptions.Default, color, radiusX, radiusY, rectangle);

        /// <summary>
        /// Applies a radial vignette effect to an image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="options">The options effecting pixel blending.</param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext Vignette(this IImageProcessingContext source, GraphicsOptions options) =>
            source.VignetteInternal(
                options,
                Color.Black,
                ValueSize.PercentageOfWidth(.5f),
                ValueSize.PercentageOfHeight(.5f));

        /// <summary>
        /// Applies a radial vignette effect to an image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="options">The options effecting pixel blending.</param>
        /// <param name="color">The color to set as the vignette.</param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext Vignette(
            this IImageProcessingContext source,
            GraphicsOptions options,
            Color color) =>
            source.VignetteInternal(
                options,
                color,
                ValueSize.PercentageOfWidth(.5f),
                ValueSize.PercentageOfHeight(.5f));

        /// <summary>
        /// Applies a radial vignette effect to an image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="options">The options effecting pixel blending.</param>
        /// <param name="radiusX">The the x-radius.</param>
        /// <param name="radiusY">The the y-radius.</param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext Vignette(
            this IImageProcessingContext source,
            GraphicsOptions options,
            float radiusX,
            float radiusY) =>
            source.VignetteInternal(options, Color.Black, radiusX, radiusY);

        /// <summary>
        /// Applies a radial vignette effect to an image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="options">The options effecting pixel blending.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext Vignette(
            this IImageProcessingContext source,
            GraphicsOptions options,
            Rectangle rectangle) =>
            source.VignetteInternal(
                options,
                Color.Black,
                ValueSize.PercentageOfWidth(.5f),
                ValueSize.PercentageOfHeight(.5f),
                rectangle);

        /// <summary>
        /// Applies a radial vignette effect to an image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="options">The options effecting pixel blending.</param>
        /// <param name="color">The color to set as the vignette.</param>
        /// <param name="radiusX">The the x-radius.</param>
        /// <param name="radiusY">The the y-radius.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext Vignette(
            this IImageProcessingContext source,
            GraphicsOptions options,
            Color color,
            float radiusX,
            float radiusY,
            Rectangle rectangle) =>
            source.VignetteInternal(options, color, radiusX, radiusY, rectangle);

        private static IImageProcessingContext VignetteInternal(
            this IImageProcessingContext source,
            GraphicsOptions options,
            Color color,
            ValueSize radiusX,
            ValueSize radiusY,
            Rectangle rectangle) =>
            source.ApplyProcessor(new VignetteProcessor(color, radiusX, radiusY, options), rectangle);

        private static IImageProcessingContext VignetteInternal(
            this IImageProcessingContext source,
            GraphicsOptions options,
            Color color,
            ValueSize radiusX,
            ValueSize radiusY) =>
            source.ApplyProcessor(new VignetteProcessor(color, radiusX, radiusY, options));
    }
}
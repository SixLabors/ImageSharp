// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Processing.Processors.PixelShading;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Defines extension methods that allow the application of user defined pixel shaders to an <see cref="Image"/>.
    /// </summary>
    public static class PixelShaderExtensions
    {
        /// <summary>
        /// Applies a user defined pixel shader to the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="pixelShader">The user defined pixel shader to use to modify images.</param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext ApplyPixelShaderProcessor(this IImageProcessingContext source, PixelShader pixelShader)
            => source.ApplyProcessor(new PixelShaderProcessor(pixelShader));

        /// <summary>
        /// Applies a user defined pixel shader to the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="pixelShader">The user defined pixel shader to use to modify images.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext ApplyPixelShaderProcessor(this IImageProcessingContext source, PixelShader pixelShader, Rectangle rectangle)
            => source.ApplyProcessor(new PixelShaderProcessor(pixelShader), rectangle);

        /// <summary>
        /// Applies a user defined pixel shader to the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="pixelShader">The user defined pixel shader to use to modify images.</param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext ApplyPixelShaderProcessor(this IImageProcessingContext source, PositionAwarePixelShader pixelShader)
            => source.ApplyProcessor(new PositionAwarePixelShaderProcessor(pixelShader));

        /// <summary>
        /// Applies a user defined pixel shader to the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="pixelShader">The user defined pixel shader to use to modify images.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext ApplyPixelShaderProcessor(this IImageProcessingContext source, PositionAwarePixelShader pixelShader, Rectangle rectangle)
            => source.ApplyProcessor(new PositionAwarePixelShaderProcessor(pixelShader), rectangle);
    }
}

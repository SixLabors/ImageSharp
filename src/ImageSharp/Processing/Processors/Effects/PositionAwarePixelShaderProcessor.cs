// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Effects
{
    /// <summary>
    /// Applies a user defined pixel shader effect through a given delegate.
    /// </summary>
    public sealed class PositionAwarePixelShaderProcessor : IImageProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PositionAwarePixelShaderProcessor"/> class.
        /// </summary>
        /// <param name="pixelShader">
        /// The user defined pixel shader to use to modify images.
        /// </param>
        public PositionAwarePixelShaderProcessor(PositionAwarePixelShader pixelShader)
        {
            this.PixelShader = pixelShader;
        }

        /// <summary>
        /// Gets the user defined pixel shader.
        /// </summary>
        public PositionAwarePixelShader PixelShader { get; }

        /// <inheritdoc />
        public IImageProcessor<TPixel> CreatePixelSpecificProcessor<TPixel>(Image<TPixel> source, Rectangle sourceRectangle)
            where TPixel : struct, IPixel<TPixel>
        {
            return new PositionAwarePixelShaderProcessor<TPixel>(this, source, sourceRectangle);
        }
    }
}

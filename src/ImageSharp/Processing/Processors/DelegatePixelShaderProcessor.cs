// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Delegates;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors
{
    /// <summary>
    /// Applies a user defined pixel shader effect through a given delegate.
    /// </summary>
    public sealed class DelegatePixelShaderProcessor : IImageProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DelegatePixelShaderProcessor"/> class.
        /// </summary>
        /// <param name="pixelShader">
        /// The user defined pixel shader to use to modify images.
        /// </param>
        public DelegatePixelShaderProcessor(PixelShader pixelShader)
        {
            this.PixelShader = pixelShader;
        }

        /// <summary>
        /// Gets the user defined pixel shader.
        /// </summary>
        public PixelShader PixelShader { get; }

        /// <inheritdoc />
        public IImageProcessor<TPixel> CreatePixelSpecificProcessor<TPixel>(Image<TPixel> source, Rectangle sourceRectangle)
            where TPixel : struct, IPixel<TPixel>
        {
            return new DelegatePixelShaderProcessor<TPixel>(this, source, sourceRectangle);
        }
    }
}

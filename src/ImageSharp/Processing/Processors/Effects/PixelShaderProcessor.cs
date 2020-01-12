// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Effects
{
    /// <summary>
    /// Applies a user defined pixel shader effect through a given delegate.
    /// </summary>
    public sealed class PixelShaderProcessor : IImageProcessor
    {
        /// <summary>
        /// The default <see cref="PixelConversionModifiers"/> to apply during the pixel conversions.
        /// </summary>
        public const PixelConversionModifiers DefaultModifiers = PixelConversionModifiers.None;

        /// <summary>
        /// Initializes a new instance of the <see cref="PixelShaderProcessor"/> class.
        /// </summary>
        /// <param name="pixelShader">
        /// The user defined pixel shader to use to modify images.
        /// </param>
        public PixelShaderProcessor(PixelShader pixelShader)
        {
            this.PixelShader = pixelShader;
            this.Modifiers = DefaultModifiers;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PixelShaderProcessor"/> class.
        /// </summary>
        /// <param name="pixelShader">
        /// The user defined pixel shader to use to modify images.
        /// </param>
        /// <param name="modifiers">The <see cref="PixelConversionModifiers"/> to apply during the pixel conversions.</param>
        public PixelShaderProcessor(PixelShader pixelShader, PixelConversionModifiers modifiers)
        {
            this.PixelShader = pixelShader;
            this.Modifiers = modifiers;
        }

        /// <summary>
        /// Gets the user defined pixel shader.
        /// </summary>
        public PixelShader PixelShader { get; }

        /// <summary>
        /// Gets the <see cref="PixelConversionModifiers"/> to apply during the pixel conversions.
        /// </summary>
        public PixelConversionModifiers Modifiers { get; }

        /// <inheritdoc />
        public IImageProcessor<TPixel> CreatePixelSpecificProcessor<TPixel>(Configuration configuration, Image<TPixel> source, Rectangle sourceRectangle)
            where TPixel : struct, IPixel<TPixel>
            => new PixelShaderProcessor<TPixel>(configuration, this, source, sourceRectangle);
    }
}

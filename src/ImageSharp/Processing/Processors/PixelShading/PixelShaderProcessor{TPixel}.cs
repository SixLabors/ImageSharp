// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Delegates;
using SixLabors.ImageSharp.Processing.Processors.PixelShading.Abstract;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.PixelShading
{
    /// <summary>
    /// Applies a user defined pixel shader effect through a given delegate.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class PixelShaderProcessor<TPixel> : PixelShaderProcessorBase<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// The user defined pixel shader.
        /// </summary>
        private readonly PixelShader pixelShader;

        /// <summary>
        /// Initializes a new instance of the <see cref="PixelShaderProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="definition">The <see cref="PixelShaderProcessor"/> defining the processor parameters.</param>
        /// <param name="source">The source <see cref="Image{TPixel}"/> for the current processor instance.</param>
        /// <param name="sourceRectangle">The source area to process for the current processor instance.</param>
        public PixelShaderProcessor(PixelShaderProcessor definition, Image<TPixel> source, Rectangle sourceRectangle)
            : base(source, sourceRectangle)
        {
            this.pixelShader = definition.PixelShader;
        }

        /// <inheritdoc/>
        protected override void ApplyPixelShader(Span<Vector4> span, int offsetY, int offsetX) => this.pixelShader(span);
    }
}

// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;

using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Advanced.ParallelUtils;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Effects
{
    /// <summary>
    /// Applies a user defined pixel shader effect through a given delegate.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal abstract class PixelShaderProcessorBase<TPixel> : ImageProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// The <see cref="PixelConversionModifiers"/> to apply during the pixel conversions.
        /// </summary>
        private readonly PixelConversionModifiers modifiers;

        /// <summary>
        /// Initializes a new instance of the <see cref="PixelShaderProcessorBase{TPixel}"/> class.
        /// </summary>
        /// <param name="modifiers">The <see cref="PixelConversionModifiers"/> to apply during the pixel conversions.</param>
        /// <param name="source">The source <see cref="Image{TPixel}"/> for the current processor instance.</param>
        /// <param name="sourceRectangle">The source area to process for the current processor instance.</param>
        protected PixelShaderProcessorBase(PixelConversionModifiers modifiers, Image<TPixel> source, Rectangle sourceRectangle)
            : base(source, sourceRectangle)
        {
            this.modifiers = modifiers;
        }

        /// <inheritdoc/>
        protected override void OnFrameApply(ImageFrame<TPixel> source)
        {
            var interest = Rectangle.Intersect(this.SourceRectangle, source.Bounds());
            int startX = interest.X;
            Configuration configuration = this.Configuration;
            PixelConversionModifiers modifiers = this.modifiers;

            ParallelHelper.IterateRowsWithTempBuffer<Vector4>(
                interest,
                this.Configuration,
                (rows, vectorBuffer) =>
                {
                    for (int y = rows.Min; y < rows.Max; y++)
                    {
                        Span<Vector4> vectorSpan = vectorBuffer.Span;
                        int length = vectorSpan.Length;
                        Span<TPixel> rowSpan = source.GetPixelRowSpan(y).Slice(startX, length);
                        PixelOperations<TPixel>.Instance.ToVector4(configuration, rowSpan, vectorSpan, modifiers);

                        // Run the user defined pixel shader on the current row of pixels
                        this.ApplyPixelShader(vectorSpan, y, startX);

                        PixelOperations<TPixel>.Instance.FromVector4Destructive(configuration, vectorSpan, rowSpan, modifiers);
                    }
                });
        }

        /// <summary>
        /// Applies the current pixel shader effect on a target row of preprocessed pixels.
        /// </summary>
        /// <param name="span">The target row of <see cref="Vector4"/> pixels to process.</param>
        /// <param name="offsetY">The initial vertical offset for the input pixels to process.</param>
        /// <param name="offsetX">The initial horizontal offset for the input pixels to process.</param>
        protected abstract void ApplyPixelShader(Span<Vector4> span, int offsetY, int offsetX);
    }
}

// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Effects
{
    /// <summary>
    /// The base class for all processors that accept a user defined row processing delegate.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal abstract class PixelRowDelegateProcessorBase<TPixel> : ImageProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// The <see cref="PixelConversionModifiers"/> to apply during the pixel conversions.
        /// </summary>
        private readonly PixelConversionModifiers modifiers;

        /// <summary>
        /// Initializes a new instance of the <see cref="PixelRowDelegateProcessorBase{TPixel}"/> class.
        /// </summary>
        /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
        /// <param name="modifiers">The <see cref="PixelConversionModifiers"/> to apply during the pixel conversions.</param>
        /// <param name="source">The source <see cref="Image{TPixel}"/> for the current processor instance.</param>
        /// <param name="sourceRectangle">The source area to process for the current processor instance.</param>
        protected PixelRowDelegateProcessorBase(Configuration configuration, PixelConversionModifiers modifiers, Image<TPixel> source, Rectangle sourceRectangle)
            : base(configuration, source, sourceRectangle)
            => this.modifiers = modifiers;

        /// <inheritdoc/>
        protected override void OnFrameApply(ImageFrame<TPixel> source)
        {
            var interest = Rectangle.Intersect(this.SourceRectangle, source.Bounds());
            int startX = interest.X;
            Configuration configuration = this.Configuration;
            PixelConversionModifiers modifiers = this.modifiers;

            ParallelRowIterator.IterateRows<Vector4>(
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

                        // Run the user defined pixel shader to the current row of pixels
                        this.ApplyPixelRowDelegate(vectorSpan, new Point(startX, y));

                        PixelOperations<TPixel>.Instance.FromVector4Destructive(configuration, vectorSpan, rowSpan, modifiers);
                    }
                });
        }

        /// <summary>
        /// Applies the current pixel row delegate to a target row of preprocessed pixels.
        /// </summary>
        /// <param name="span">The target row of <see cref="Vector4"/> pixels to process.</param>
        /// <param name="offset">The initial horizontal and vertical offset for the input pixels to process.</param>
        protected abstract void ApplyPixelRowDelegate(Span<Vector4> span, Point offset);
    }
}

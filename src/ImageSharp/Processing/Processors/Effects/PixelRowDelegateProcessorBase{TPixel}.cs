// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
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

            ParallelRowIterator.IterateRows<RowIntervalAction, Vector4>(
                interest,
                this.Configuration,
                new RowIntervalAction(interest.X, source, this.Configuration, this.modifiers, this));
        }

        /// <summary>
        /// Applies the current pixel row delegate to a target row of preprocessed pixels.
        /// </summary>
        /// <param name="span">The target row of <see cref="Vector4"/> pixels to process.</param>
        /// <param name="offset">The initial horizontal and vertical offset for the input pixels to process.</param>
        protected abstract void ApplyPixelRowDelegate(Span<Vector4> span, Point offset);

        /// <summary>
        /// A <see langword="struct"/> implementing the convolution logic for <see cref="PixelRowDelegateProcessorBase{T}"/>.
        /// </summary>
        private readonly struct RowIntervalAction : IRowIntervalAction<Vector4>
        {
            private readonly int startX;
            private readonly ImageFrame<TPixel> source;
            private readonly Configuration configuration;
            private readonly PixelConversionModifiers modifiers;
            private readonly PixelRowDelegateProcessorBase<TPixel> processor;

            [MethodImpl(InliningOptions.ShortMethod)]
            public RowIntervalAction(
                int startX,
                ImageFrame<TPixel> source,
                Configuration configuration,
                PixelConversionModifiers modifiers,
                PixelRowDelegateProcessorBase<TPixel> processor)
            {
                this.startX = startX;
                this.source = source;
                this.configuration = configuration;
                this.modifiers = modifiers;
                this.processor = processor;
            }

            /// <inheritdoc/>
            [MethodImpl(InliningOptions.ShortMethod)]
            public void Invoke(in RowInterval rows, Memory<Vector4> memory)
            {
                for (int y = rows.Min; y < rows.Max; y++)
                {
                    Span<Vector4> vectorSpan = memory.Span;
                    int length = vectorSpan.Length;
                    Span<TPixel> rowSpan = this.source.GetPixelRowSpan(y).Slice(this.startX, length);
                    PixelOperations<TPixel>.Instance.ToVector4(this.configuration, rowSpan, vectorSpan, this.modifiers);

                    // Run the user defined pixel shader to the current row of pixels
                    this.processor.ApplyPixelRowDelegate(vectorSpan, new Point(this.startX, y));

                    PixelOperations<TPixel>.Instance.FromVector4Destructive(this.configuration, vectorSpan, rowSpan, this.modifiers);
                }
            }
        }
    }
}

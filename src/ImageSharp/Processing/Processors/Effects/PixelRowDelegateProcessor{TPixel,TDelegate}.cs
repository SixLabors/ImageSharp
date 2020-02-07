// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Effects
{
    /// <summary>
    /// The base class for all processors that accept a user defined row processing delegate.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <typeparam name="TDelegate">The row processor type.</typeparam>
    internal sealed class PixelRowDelegateProcessor<TPixel, TDelegate> : ImageProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
        where TDelegate : struct, IPixelRowDelegate
    {
        private readonly TDelegate rowDelegate;

        /// <summary>
        /// The <see cref="PixelConversionModifiers"/> to apply during the pixel conversions.
        /// </summary>
        private readonly PixelConversionModifiers modifiers;

        /// <summary>
        /// Initializes a new instance of the <see cref="PixelRowDelegateProcessor{TPixel,TDelegate}"/> class.
        /// </summary>
        /// <param name="rowDelegate">The row processor to use to process each pixel row</param>
        /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
        /// <param name="modifiers">The <see cref="PixelConversionModifiers"/> to apply during the pixel conversions.</param>
        /// <param name="source">The source <see cref="Image{TPixel}"/> for the current processor instance.</param>
        /// <param name="sourceRectangle">The source area to process for the current processor instance.</param>
        public PixelRowDelegateProcessor(
            in TDelegate rowDelegate,
            Configuration configuration,
            PixelConversionModifiers modifiers,
            Image<TPixel> source,
            Rectangle sourceRectangle)
            : base(configuration, source, sourceRectangle)
        {
            this.rowDelegate = rowDelegate;
            this.modifiers = modifiers;
        }

        /// <inheritdoc/>
        protected override void OnFrameApply(ImageFrame<TPixel> source)
        {
            var interest = Rectangle.Intersect(this.SourceRectangle, source.Bounds());

            ParallelRowIterator.IterateRows<RowAction, Vector4>(
                interest,
                this.Configuration,
                new RowAction(interest.X, source, this.Configuration, this.modifiers, this.rowDelegate));
        }

        /// <summary>
        /// A <see langword="struct"/> implementing the convolution logic for <see cref="PixelRowDelegateProcessor{TPixel,TDelegate}"/>.
        /// </summary>
        private readonly struct RowAction : IRowAction<Vector4>
        {
            private readonly int startX;
            private readonly ImageFrame<TPixel> source;
            private readonly Configuration configuration;
            private readonly PixelConversionModifiers modifiers;
            private readonly TDelegate rowProcessor;

            [MethodImpl(InliningOptions.ShortMethod)]
            public RowAction(
                int startX,
                ImageFrame<TPixel> source,
                Configuration configuration,
                PixelConversionModifiers modifiers,
                in TDelegate rowProcessor)
            {
                this.startX = startX;
                this.source = source;
                this.configuration = configuration;
                this.modifiers = modifiers;
                this.rowProcessor = rowProcessor;
            }

            /// <inheritdoc/>
            [MethodImpl(InliningOptions.ShortMethod)]
            public void Invoke(int y, Span<Vector4> span)
            {
                int length = span.Length;
                Span<TPixel> rowSpan = this.source.GetPixelRowSpan(y).Slice(this.startX, length);
                PixelOperations<TPixel>.Instance.ToVector4(this.configuration, rowSpan, span, this.modifiers);

                // Run the user defined pixel shader to the current row of pixels
                Unsafe.AsRef(this.rowProcessor).Invoke(span, new Point(this.startX, y));

                PixelOperations<TPixel>.Instance.FromVector4Destructive(this.configuration, span, rowSpan, this.modifiers);
            }
        }
    }
}

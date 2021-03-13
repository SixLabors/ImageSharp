// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Filters
{
    /// <summary>
    /// Provides methods that accept a <see cref="ColorMatrix"/> matrix to apply free-form filters to images.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class FilterProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        private readonly FilterProcessor definition;

        /// <summary>
        /// Initializes a new instance of the <see cref="FilterProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
        /// <param name="definition">The <see cref="FilterProcessor"/>.</param>
        /// <param name="source">The source <see cref="Image{TPixel}"/> for the current processor instance.</param>
        /// <param name="sourceRectangle">The source area to process for the current processor instance.</param>
        public FilterProcessor(Configuration configuration, FilterProcessor definition, Image<TPixel> source, Rectangle sourceRectangle)
            : base(configuration, source, sourceRectangle)
        {
            this.definition = definition;
        }

        /// <inheritdoc/>
        protected override void OnFrameApply(ImageFrame<TPixel> source)
        {
            var interest = Rectangle.Intersect(this.SourceRectangle, source.Bounds());
            var operation = new RowOperation(interest.X, source, this.definition.Matrix, this.Configuration);

            ParallelRowIterator.IterateRows<RowOperation, Vector4>(
                this.Configuration,
                interest,
                in operation);
        }

        /// <summary>
        /// A <see langword="struct"/> implementing the convolution logic for <see cref="FilterProcessor{TPixel}"/>.
        /// </summary>
        private readonly struct RowOperation : IRowOperation<Vector4>
        {
            private readonly int startX;
            private readonly ImageFrame<TPixel> source;
            private readonly ColorMatrix matrix;
            private readonly Configuration configuration;

            [MethodImpl(InliningOptions.ShortMethod)]
            public RowOperation(
                int startX,
                ImageFrame<TPixel> source,
                ColorMatrix matrix,
                Configuration configuration)
            {
                this.startX = startX;
                this.source = source;
                this.matrix = matrix;
                this.configuration = configuration;
            }

            /// <inheritdoc/>
            [MethodImpl(InliningOptions.ShortMethod)]
            public void Invoke(int y, Span<Vector4> span)
            {
                Span<TPixel> rowSpan = this.source.GetPixelRowSpan(y).Slice(this.startX, span.Length);
                PixelOperations<TPixel>.Instance.ToVector4(this.configuration, rowSpan, span, PixelConversionModifiers.Scale);

                ColorNumerics.Transform(span, ref Unsafe.AsRef(this.matrix));

                PixelOperations<TPixel>.Instance.FromVector4Destructive(this.configuration, span, rowSpan, PixelConversionModifiers.Scale);
            }
        }
    }
}

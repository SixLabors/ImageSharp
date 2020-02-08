// Copyright (c) Six Labors and contributors.
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
        where TPixel : struct, IPixel<TPixel>
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

            ParallelRowIterator.IterateRows<RowIntervalOperation, Vector4>(
                interest,
                this.Configuration,
                new RowIntervalOperation(interest.X, source, this.definition.Matrix, this.Configuration));
        }

        /// <summary>
        /// A <see langword="struct"/> implementing the convolution logic for <see cref="FilterProcessor{TPixel}"/>.
        /// </summary>
        private readonly struct RowIntervalOperation : IRowIntervalOperation<Vector4>
        {
            private readonly int startX;
            private readonly ImageFrame<TPixel> source;
            private readonly ColorMatrix matrix;
            private readonly Configuration configuration;

            [MethodImpl(InliningOptions.ShortMethod)]
            public RowIntervalOperation(
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
            public void Invoke(in RowInterval rows, Memory<Vector4> memory)
            {
                for (int y = rows.Min; y < rows.Max; y++)
                {
                    Span<Vector4> vectorSpan = memory.Span;
                    int length = vectorSpan.Length;
                    Span<TPixel> rowSpan = this.source.GetPixelRowSpan(y).Slice(this.startX, length);
                    PixelOperations<TPixel>.Instance.ToVector4(this.configuration, rowSpan, vectorSpan);

                    Vector4Utils.Transform(vectorSpan, ref Unsafe.AsRef(this.matrix));

                    PixelOperations<TPixel>.Instance.FromVector4Destructive(this.configuration, vectorSpan, rowSpan);
                }
            }
        }
    }
}

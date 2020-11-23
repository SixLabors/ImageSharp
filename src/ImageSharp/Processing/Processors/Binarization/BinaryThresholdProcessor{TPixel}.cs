// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Binarization
{
    /// <summary>
    /// Performs simple binary threshold filtering against an image.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class BinaryThresholdProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        private readonly BinaryThresholdProcessor definition;

        /// <summary>
        /// Initializes a new instance of the <see cref="BinaryThresholdProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
        /// <param name="definition">The <see cref="BinaryThresholdProcessor"/> defining the processor parameters.</param>
        /// <param name="source">The source <see cref="Image{TPixel}"/> for the current processor instance.</param>
        /// <param name="sourceRectangle">The source area to process for the current processor instance.</param>
        public BinaryThresholdProcessor(Configuration configuration, BinaryThresholdProcessor definition, Image<TPixel> source, Rectangle sourceRectangle)
            : base(configuration, source, sourceRectangle)
        {
            this.definition = definition;
        }

        /// <inheritdoc/>
        protected override void OnFrameApply(ImageFrame<TPixel> source)
        {
            byte threshold = (byte)MathF.Round(this.definition.Threshold * 255F);
            TPixel upper = this.definition.UpperColor.ToPixel<TPixel>();
            TPixel lower = this.definition.LowerColor.ToPixel<TPixel>();

            Rectangle sourceRectangle = this.SourceRectangle;
            Configuration configuration = this.Configuration;

            var interest = Rectangle.Intersect(sourceRectangle, source.Bounds());
            bool isAlphaOnly = typeof(TPixel) == typeof(A8);

            var operation = new RowOperation(interest, source, upper, lower, threshold, isAlphaOnly);
            ParallelRowIterator.IterateRows(
                configuration,
                interest,
                in operation);
        }

        /// <summary>
        /// A <see langword="struct"/> implementing the clone logic for <see cref="BinaryThresholdProcessor{TPixel}"/>.
        /// </summary>
        private readonly struct RowOperation : IRowOperation
        {
            private readonly ImageFrame<TPixel> source;
            private readonly TPixel upper;
            private readonly TPixel lower;
            private readonly byte threshold;
            private readonly int minX;
            private readonly int maxX;
            private readonly bool isAlphaOnly;

            [MethodImpl(InliningOptions.ShortMethod)]
            public RowOperation(
                Rectangle bounds,
                ImageFrame<TPixel> source,
                TPixel upper,
                TPixel lower,
                byte threshold,
                bool isAlphaOnly)
            {
                this.source = source;
                this.upper = upper;
                this.lower = lower;
                this.threshold = threshold;
                this.minX = bounds.X;
                this.maxX = bounds.Right;
                this.isAlphaOnly = isAlphaOnly;
            }

            /// <inheritdoc/>
            [MethodImpl(InliningOptions.ShortMethod)]
            public void Invoke(int y)
            {
                Rgba32 rgba = default;
                Span<TPixel> row = this.source.GetPixelRowSpan(y);
                ref TPixel rowRef = ref MemoryMarshal.GetReference(row);

                for (int x = this.minX; x < this.maxX; x++)
                {
                    ref TPixel color = ref Unsafe.Add(ref rowRef, x);
                    color.ToRgba32(ref rgba);

                    // Convert to grayscale using ITU-R Recommendation BT.709 if required
                    byte luminance = this.isAlphaOnly ? rgba.A : ColorNumerics.Get8BitBT709Luminance(rgba.R, rgba.G, rgba.B);
                    color = luminance >= this.threshold ? this.upper : this.lower;
                }
            }
        }
    }
}

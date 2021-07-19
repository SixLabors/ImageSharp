// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Normalization
{
    /// <summary>
    /// Normalize an image by stretching the dynamic range to full contrast
    /// Applicable to an <see cref="Image"/>.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class AutoLevelProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        private readonly int luminanceLevels;

        /// <summary>
        /// Initializes a new instance of the <see cref="AutoLevelProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
        /// <param name="source">The source <see cref="Image{TPixel}"/> for the current processor instance.</param>
        /// <param name="sourceRectangle">The source area to process for the current processor instance.</param>
        public AutoLevelProcessor(
            Configuration configuration,
            Image<TPixel> source,
            Rectangle sourceRectangle)
            : base(configuration, source, sourceRectangle)
        {
            // TODO I don't know how to get channel bit depth for non-grayscale types
            if (!(typeof(TPixel) == typeof(L16) || typeof(TPixel) == typeof(L8)))
            {
                throw new ArgumentException("AutoLevelHistogramProcessor only works for L8 or L16 pixel types");
            }

            this.luminanceLevels = ColorNumerics.GetColorCountForBitDepth(source.PixelType.BitsPerPixel);
        }

        protected override void OnFrameApply(ImageFrame<TPixel> source)
        {
            var interest = Rectangle.Intersect(this.SourceRectangle, source.Bounds());

            int[] rowMinimums = new int[source.Height];
            int[] rowMaximums = new int[source.Height];

            var grayscaleOperation = new GrayscaleLevelsMinMaxRowOperation(interest, source, this.luminanceLevels, rowMinimums, rowMaximums);
            ParallelRowIterator.IterateRows(
                this.Configuration,
                interest,
                in grayscaleOperation);

            int minLuminance = rowMinimums.Min();
            int maxLuminance = rowMaximums.Max();

            if (minLuminance == 0 && maxLuminance == this.luminanceLevels - 1)
            {
                return;
            }

            var contrastStretchOperation = new GrayscaleLevelsContrastStretchOperation(interest, source, this.luminanceLevels, minLuminance, maxLuminance);
            ParallelRowIterator.IterateRows(
                this.Configuration,
                interest,
                in contrastStretchOperation);
        }

        /// <summary>
        /// A <see langword="struct"/> to calculate the min and max luminance of a row for <see cref="AutoLevelProcessor{TPixel}"/>.
        /// </summary>
        private readonly struct GrayscaleLevelsMinMaxRowOperation : IRowOperation
        {
            private readonly Rectangle bounds;
            private readonly ImageFrame<TPixel> source;
            private readonly int luminanceLevels;
            private readonly int[] rowMinimums;
            private readonly int[] rowMaximums;

            [MethodImpl(InliningOptions.ShortMethod)]
            public GrayscaleLevelsMinMaxRowOperation(
                 Rectangle bounds,
                 ImageFrame<TPixel> source,
                 int luminanceLevels,
                 int[] rowMinimums,
                 int[] rowMaximums)
            {
                this.bounds = bounds;
                this.source = source;
                this.luminanceLevels = luminanceLevels;
                this.rowMinimums = rowMinimums;
                this.rowMaximums = rowMaximums;
            }

            /// <inheritdoc/>
#if NETSTANDARD2_0
            // https://github.com/SixLabors/ImageSharp/issues/1204
            [MethodImpl(MethodImplOptions.NoOptimization)]
#else
            [MethodImpl(InliningOptions.ShortMethod)]
#endif
            public void Invoke(int y)
            {
                Span<TPixel> pixelRow = this.source.GetPixelRowSpan(y);
                int levels = this.luminanceLevels;

                int minLuminance = int.MaxValue;
                int maxLuminance = int.MinValue;

                for (int x = this.bounds.X; x < this.bounds.Width; x++)
                {
                    // TODO: We should bulk convert here.
                    var vector = pixelRow[x].ToVector4();
                    int luminance = ColorNumerics.GetBT709Luminance(ref vector, levels);
                    minLuminance = Math.Min(luminance, minLuminance);
                    maxLuminance = Math.Max(luminance, maxLuminance);
                }

                this.rowMinimums[y] = minLuminance;
                this.rowMaximums[y] = maxLuminance;
            }
        }

        /// <summary>
        /// A <see langword="struct"/> to contrast stretch a row for <see cref="AutoLevelProcessor{TPixel}"/>.
        /// </summary>
        private readonly struct GrayscaleLevelsContrastStretchOperation : IRowOperation
        {
            private readonly Rectangle bounds;
            private readonly ImageFrame<TPixel> source;
            private readonly int luminanceLevels;
            private readonly int minLuminance;
            private readonly int maxLuminance;

            [MethodImpl(InliningOptions.ShortMethod)]
            public GrayscaleLevelsContrastStretchOperation(
                 Rectangle bounds,
                 ImageFrame<TPixel> source,
                 int luminanceLevels,
                 int minLuminance,
                 int maxLuminance)
            {
                this.bounds = bounds;
                this.source = source;
                this.luminanceLevels = luminanceLevels;
                this.minLuminance = minLuminance;
                this.maxLuminance = maxLuminance;
            }

            /// <inheritdoc/>
#if NETSTANDARD2_0
            // https://github.com/SixLabors/ImageSharp/issues/1204
            [MethodImpl(MethodImplOptions.NoOptimization)]
#else
            [MethodImpl(InliningOptions.ShortMethod)]
#endif
            public void Invoke(int y)
            {
                Span<TPixel> pixelRow = this.source.GetPixelRowSpan(y);
                float dynamicRange = this.maxLuminance - this.minLuminance;

                for (int x = this.bounds.X; x < this.bounds.Width; x++)
                {
                    // TODO: We should bulk convert here.
                    ref TPixel pixel = ref pixelRow[x];
                    var vector = pixel.ToVector4();
                    int luminance = ColorNumerics.GetBT709Luminance(ref vector, this.luminanceLevels);
                    float luminanceConstrastStretched = (luminance - this.minLuminance) / dynamicRange;
                    pixel.FromVector4(new Vector4(luminanceConstrastStretched, luminanceConstrastStretched, luminanceConstrastStretched, vector.W));
                }
            }
        }
    }
}

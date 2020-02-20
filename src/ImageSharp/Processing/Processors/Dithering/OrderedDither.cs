// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace SixLabors.ImageSharp.Processing.Processors.Dithering
{
    /// <summary>
    /// An ordered dithering matrix with equal sides of arbitrary length
    /// </summary>
    public readonly partial struct OrderedDither : IDither, IEquatable<OrderedDither>, IEquatable<IDither>
    {
        private readonly DenseMatrix<float> thresholdMatrix;
        private readonly int modulusX;
        private readonly int modulusY;

        /// <summary>
        /// Initializes a new instance of the <see cref="OrderedDither"/> struct.
        /// </summary>
        /// <param name="length">The length of the matrix sides</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public OrderedDither(uint length)
        {
            DenseMatrix<uint> ditherMatrix = OrderedDitherFactory.CreateDitherMatrix(length);

            // Create a new matrix to run against, that pre-thresholds the values.
            // We don't want to adjust the original matrix generation code as that
            // creates known, easy to test values.
            // https://en.wikipedia.org/wiki/Ordered_dithering#Algorithm
            var thresholdMatrix = new DenseMatrix<float>((int)length);
            float m2 = length * length;
            for (int y = 0; y < length; y++)
            {
                for (int x = 0; x < length; x++)
                {
                    thresholdMatrix[y, x] = ((ditherMatrix[y, x] + 1) / m2) - .5F;
                }
            }

            this.modulusX = ditherMatrix.Columns;
            this.modulusY = ditherMatrix.Rows;
            this.thresholdMatrix = thresholdMatrix;
        }

        /// <summary>
        /// Compares the two <see cref="OrderedDither"/> instances to determine whether they are equal.
        /// </summary>
        /// <param name="left">The first source instance.</param>
        /// <param name="right">The second source instance.</param>
        /// <returns>The <see cref="bool"/>.</returns>
        public static bool operator ==(IDither left, OrderedDither right)
            => right == left;

        /// <summary>
        /// Compares the two <see cref="OrderedDither"/> instances to determine whether they are unequal.
        /// </summary>
        /// <param name="left">The first source instance.</param>
        /// <param name="right">The second source instance.</param>
        /// <returns>The <see cref="bool"/>.</returns>
        public static bool operator !=(IDither left, OrderedDither right)
            => !(right == left);

        /// <summary>
        /// Compares the two <see cref="OrderedDither"/> instances to determine whether they are equal.
        /// </summary>
        /// <param name="left">The first source instance.</param>
        /// <param name="right">The second source instance.</param>
        /// <returns>The <see cref="bool"/>.</returns>
        public static bool operator ==(OrderedDither left, IDither right)
            => left.Equals(right);

        /// <summary>
        /// Compares the two <see cref="OrderedDither"/> instances to determine whether they are unequal.
        /// </summary>
        /// <param name="left">The first source instance.</param>
        /// <param name="right">The second source instance.</param>
        /// <returns>The <see cref="bool"/>.</returns>
        public static bool operator !=(OrderedDither left, IDither right)
            => !(left == right);

        /// <summary>
        /// Compares the two <see cref="OrderedDither"/> instances to determine whether they are equal.
        /// </summary>
        /// <param name="left">The first source instance.</param>
        /// <param name="right">The second source instance.</param>
        /// <returns>The <see cref="bool"/>.</returns>
        public static bool operator ==(OrderedDither left, OrderedDither right)
            => left.Equals(right);

        /// <summary>
        /// Compares the two <see cref="OrderedDither"/> instances to determine whether they are unequal.
        /// </summary>
        /// <param name="left">The first source instance.</param>
        /// <param name="right">The second source instance.</param>
        /// <returns>The <see cref="bool"/>.</returns>
        public static bool operator !=(OrderedDither left, OrderedDither right)
            => !(left == right);

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void ApplyQuantizationDither<TFrameQuantizer, TPixel>(
            ref TFrameQuantizer quantizer,
            ReadOnlyMemory<TPixel> palette,
            ImageFrame<TPixel> source,
            Memory<byte> output,
            Rectangle bounds)
            where TFrameQuantizer : struct, IFrameQuantizer<TPixel>
            where TPixel : struct, IPixel<TPixel>
        {
            var ditherOperation = new QuantizeDitherRowIntervalOperation<TFrameQuantizer, TPixel>(
                ref quantizer,
                in Unsafe.AsRef(this),
                source,
                output,
                bounds,
                palette,
                ImageMaths.GetBitsNeededForColorDepth(palette.Span.Length));

            ParallelRowIterator.IterateRows(
                quantizer.Configuration,
                bounds,
                in ditherOperation);
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void ApplyPaletteDither<TPixel>(
            Configuration configuration,
            ReadOnlyMemory<TPixel> palette,
            ImageFrame<TPixel> source,
            Rectangle bounds,
            float scale)
            where TPixel : struct, IPixel<TPixel>
        {
            var ditherOperation = new PaletteDitherRowIntervalOperation<TPixel>(
                in Unsafe.AsRef(this),
                source,
                bounds,
                palette,
                scale,
                ImageMaths.GetBitsNeededForColorDepth(palette.Span.Length));

            ParallelRowIterator.IterateRows(
                configuration,
                bounds,
                in ditherOperation);
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        internal TPixel Dither<TPixel>(
            TPixel source,
            int x,
            int y,
            int bitDepth,
            float scale)
            where TPixel : struct, IPixel<TPixel>
        {
            Rgba32 rgba = default;
            source.ToRgba32(ref rgba);
            Rgba32 attempt;

            // Spread assumes an even colorspace distribution and precision.
            // Calculated as 0-255/component count. 256 / bitDepth
            // https://bisqwit.iki.fi/story/howto/dither/jy/
            // https://en.wikipedia.org/wiki/Ordered_dithering#Algorithm
            int spread = 256 / bitDepth;
            float factor = spread * this.thresholdMatrix[y % this.modulusY, x % this.modulusX] * scale;

            attempt.R = (byte)(rgba.R + factor).Clamp(byte.MinValue, byte.MaxValue);
            attempt.G = (byte)(rgba.G + factor).Clamp(byte.MinValue, byte.MaxValue);
            attempt.B = (byte)(rgba.B + factor).Clamp(byte.MinValue, byte.MaxValue);
            attempt.A = (byte)(rgba.A + factor).Clamp(byte.MinValue, byte.MaxValue);

            TPixel result = default;
            result.FromRgba32(attempt);

            return result;
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
            => obj is OrderedDither dither && this.Equals(dither);

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public bool Equals(OrderedDither other)
            => this.thresholdMatrix.Equals(other.thresholdMatrix) && this.modulusX == other.modulusX && this.modulusY == other.modulusY;

        /// <inheritdoc/>
        public bool Equals(IDither other)
            => this.Equals((object)other);

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public override int GetHashCode()
            => HashCode.Combine(this.thresholdMatrix, this.modulusX, this.modulusY);

        private readonly struct QuantizeDitherRowIntervalOperation<TFrameQuantizer, TPixel> : IRowIntervalOperation
            where TFrameQuantizer : struct, IFrameQuantizer<TPixel>
            where TPixel : struct, IPixel<TPixel>
        {
            private readonly TFrameQuantizer quantizer;
            private readonly OrderedDither dither;
            private readonly ImageFrame<TPixel> source;
            private readonly Memory<byte> output;
            private readonly Rectangle bounds;
            private readonly ReadOnlyMemory<TPixel> palette;
            private readonly int bitDepth;

            [MethodImpl(InliningOptions.ShortMethod)]
            public QuantizeDitherRowIntervalOperation(
                ref TFrameQuantizer quantizer,
                in OrderedDither dither,
                ImageFrame<TPixel> source,
                Memory<byte> output,
                Rectangle bounds,
                ReadOnlyMemory<TPixel> palette,
                int bitDepth)
            {
                this.quantizer = quantizer;
                this.dither = dither;
                this.source = source;
                this.output = output;
                this.bounds = bounds;
                this.palette = palette;
                this.bitDepth = bitDepth;
            }

            [MethodImpl(InliningOptions.ShortMethod)]
            public void Invoke(in RowInterval rows)
            {
                ReadOnlySpan<TPixel> paletteSpan = this.palette.Span;
                Span<byte> outputSpan = this.output.Span;
                int width = this.bounds.Width;
                int offsetY = this.bounds.Top;
                int offsetX = this.bounds.Left;
                float scale = this.quantizer.Options.DitherScale;

                for (int y = rows.Min; y < rows.Max; y++)
                {
                    Span<TPixel> row = this.source.GetPixelRowSpan(y);
                    int rowStart = (y - offsetY) * width;

                    // TODO: This can be a bulk operation.
                    for (int x = this.bounds.Left; x < this.bounds.Right; x++)
                    {
                        TPixel dithered = this.dither.Dither(row[x], x, y, this.bitDepth, scale);
                        outputSpan[rowStart + x - offsetX] = this.quantizer.GetQuantizedColor(dithered, paletteSpan, out TPixel _);
                    }
                }
            }
        }

        private readonly struct PaletteDitherRowIntervalOperation<TPixel> : IRowIntervalOperation
            where TPixel : struct, IPixel<TPixel>
        {
            private readonly OrderedDither dither;
            private readonly ImageFrame<TPixel> source;
            private readonly Rectangle bounds;
            private readonly EuclideanPixelMap<TPixel> pixelMap;
            private readonly float scale;
            private readonly int bitDepth;

            [MethodImpl(InliningOptions.ShortMethod)]
            public PaletteDitherRowIntervalOperation(
                in OrderedDither dither,
                ImageFrame<TPixel> source,
                Rectangle bounds,
                ReadOnlyMemory<TPixel> palette,
                float scale,
                int bitDepth)
            {
                this.dither = dither;
                this.source = source;
                this.bounds = bounds;
                this.pixelMap = new EuclideanPixelMap<TPixel>(palette);
                this.scale = scale;
                this.bitDepth = bitDepth;
            }

            [MethodImpl(InliningOptions.ShortMethod)]
            public void Invoke(in RowInterval rows)
            {
                for (int y = rows.Min; y < rows.Max; y++)
                {
                    Span<TPixel> row = this.source.GetPixelRowSpan(y);

                    for (int x = this.bounds.Left; x < this.bounds.Right; x++)
                    {
                        TPixel dithered = this.dither.Dither(row[x], x, y, this.bitDepth, this.scale);
                        this.pixelMap.GetClosestColor(dithered, out TPixel transformed);
                        row[x] = transformed;
                    }
                }
            }
        }
    }
}

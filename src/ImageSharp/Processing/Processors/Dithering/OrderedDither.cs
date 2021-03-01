// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Advanced;
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ApplyQuantizationDither<TFrameQuantizer, TPixel>(
            ref TFrameQuantizer quantizer,
            ImageFrame<TPixel> source,
            IndexedImageFrame<TPixel> destination,
            Rectangle bounds)
            where TFrameQuantizer : struct, IQuantizer<TPixel>
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var ditherOperation = new QuantizeDitherRowOperation<TFrameQuantizer, TPixel>(
                ref quantizer,
                in Unsafe.AsRef(this),
                source,
                destination,
                bounds);

            ParallelRowIterator.IterateRows(
                quantizer.Configuration,
                bounds,
                in ditherOperation);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ApplyPaletteDither<TPaletteDitherImageProcessor, TPixel>(
            in TPaletteDitherImageProcessor processor,
            ImageFrame<TPixel> source,
            Rectangle bounds)
            where TPaletteDitherImageProcessor : struct, IPaletteDitherImageProcessor<TPixel>
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var ditherOperation = new PaletteDitherRowOperation<TPaletteDitherImageProcessor, TPixel>(
                in processor,
                in Unsafe.AsRef(this),
                source,
                bounds);

            ParallelRowIterator.IterateRows(
                processor.Configuration,
                bounds,
                in ditherOperation);
        }

        // Spread assumes an even colorspace distribution and precision.
        // Cubed root used because we always compare to Rgb.
        // https://bisqwit.iki.fi/story/howto/dither/jy/
        // https://en.wikipedia.org/wiki/Ordered_dithering#Algorithm
        internal static int CalculatePaletteSpread(int colors) => (int)(255 / (Math.Pow(colors, 1.0 / 3) - 1));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal TPixel Dither<TPixel>(
            TPixel source,
            int x,
            int y,
            int spread,
            float scale)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Unsafe.SkipInit(out Rgba32 rgba);
            source.ToRgba32(ref rgba);
            Unsafe.SkipInit(out Rgba32 attempt);

            float factor = spread * this.thresholdMatrix[y % this.modulusY, x % this.modulusX] * scale;

            attempt.R = (byte)Numerics.Clamp(rgba.R + factor, byte.MinValue, byte.MaxValue);
            attempt.G = (byte)Numerics.Clamp(rgba.G + factor, byte.MinValue, byte.MaxValue);
            attempt.B = (byte)Numerics.Clamp(rgba.B + factor, byte.MinValue, byte.MaxValue);
            attempt.A = (byte)Numerics.Clamp(rgba.A + factor, byte.MinValue, byte.MaxValue);

            TPixel result = default;
            result.FromRgba32(attempt);

            return result;
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
            => obj is OrderedDither dither && this.Equals(dither);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(OrderedDither other)
            => this.thresholdMatrix.Equals(other.thresholdMatrix) && this.modulusX == other.modulusX && this.modulusY == other.modulusY;

        /// <inheritdoc/>
        public bool Equals(IDither other)
            => this.Equals((object)other);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
            => HashCode.Combine(this.thresholdMatrix, this.modulusX, this.modulusY);

        private readonly struct QuantizeDitherRowOperation<TFrameQuantizer, TPixel> : IRowOperation
            where TFrameQuantizer : struct, IQuantizer<TPixel>
            where TPixel : unmanaged, IPixel<TPixel>
        {
            private readonly TFrameQuantizer quantizer;
            private readonly OrderedDither dither;
            private readonly ImageFrame<TPixel> source;
            private readonly IndexedImageFrame<TPixel> destination;
            private readonly Rectangle bounds;
            private readonly int spread;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public QuantizeDitherRowOperation(
                ref TFrameQuantizer quantizer,
                in OrderedDither dither,
                ImageFrame<TPixel> source,
                IndexedImageFrame<TPixel> destination,
                Rectangle bounds)
            {
                this.quantizer = quantizer;
                this.dither = dither;
                this.source = source;
                this.destination = destination;
                this.bounds = bounds;
                this.spread = CalculatePaletteSpread(destination.Palette.Length);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Invoke(int y)
            {
                ref TFrameQuantizer quantizer = ref Unsafe.AsRef(this.quantizer);
                int spread = this.spread;
                float scale = this.quantizer.Options.DitherScale;

                ReadOnlySpan<TPixel> sourceRow = this.source.GetPixelRowSpan(y).Slice(this.bounds.X, this.bounds.Width);
                Span<byte> destRow =
                    this.destination.GetWritablePixelRowSpanUnsafe(y - this.bounds.Y).Slice(0, sourceRow.Length);

                for (int x = 0; x < sourceRow.Length; x++)
                {
                    TPixel dithered = this.dither.Dither(sourceRow[x], x, y, spread, scale);
                    destRow[x] = quantizer.GetQuantizedColor(dithered, out TPixel _);
                }
            }
        }

        private readonly struct PaletteDitherRowOperation<TPaletteDitherImageProcessor, TPixel> : IRowOperation
            where TPaletteDitherImageProcessor : struct, IPaletteDitherImageProcessor<TPixel>
            where TPixel : unmanaged, IPixel<TPixel>
        {
            private readonly TPaletteDitherImageProcessor processor;
            private readonly OrderedDither dither;
            private readonly ImageFrame<TPixel> source;
            private readonly Rectangle bounds;
            private readonly float scale;
            private readonly int spread;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public PaletteDitherRowOperation(
                in TPaletteDitherImageProcessor processor,
                in OrderedDither dither,
                ImageFrame<TPixel> source,
                Rectangle bounds)
            {
                this.processor = processor;
                this.dither = dither;
                this.source = source;
                this.bounds = bounds;
                this.scale = processor.DitherScale;
                this.spread = CalculatePaletteSpread(processor.Palette.Length);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Invoke(int y)
            {
                ref TPaletteDitherImageProcessor processor = ref Unsafe.AsRef(this.processor);
                int spread = this.spread;
                float scale = this.scale;

                Span<TPixel> row = this.source.GetPixelRowSpan(y).Slice(this.bounds.X, this.bounds.Width);

                for (int x = 0; x < row.Length; x++)
                {
                    ref TPixel sourcePixel = ref row[x];
                    TPixel dithered = this.dither.Dither(sourcePixel, x, y, spread, scale);
                    sourcePixel = processor.GetPaletteColor(dithered);
                }
            }
        }
    }
}

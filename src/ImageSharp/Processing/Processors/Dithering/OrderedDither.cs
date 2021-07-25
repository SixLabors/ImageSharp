// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
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
            ImageFrame<TPixel> source,
            IndexedImageFrame<TPixel> destination,
            Rectangle bounds)
            where TFrameQuantizer : struct, IQuantizer<TPixel>
            where TPixel : unmanaged, IPixel<TPixel>
        {
            int spread = CalculatePaletteSpread(destination.Palette.Length);
            float scale = quantizer.Options.DitherScale;

            for (int y = bounds.Top; y < bounds.Bottom; y++)
            {
                ReadOnlySpan<TPixel> sourceRow = source.GetPixelRowSpan(y).Slice(bounds.X, bounds.Width);
                Span<byte> destRow = destination.GetWritablePixelRowSpanUnsafe(y - bounds.Y).Slice(0, sourceRow.Length);

                for (int x = 0; x < sourceRow.Length; x++)
                {
                    TPixel dithered = this.Dither(sourceRow[x], x, y, spread, scale);
                    destRow[x] = quantizer.GetQuantizedColor(dithered, out TPixel _);
                }
            }
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void ApplyPaletteDither<TPaletteDitherImageProcessor, TPixel>(
            in TPaletteDitherImageProcessor processor,
            ImageFrame<TPixel> source,
            Rectangle bounds)
            where TPaletteDitherImageProcessor : struct, IPaletteDitherImageProcessor<TPixel>
            where TPixel : unmanaged, IPixel<TPixel>
        {
            int spread = CalculatePaletteSpread(processor.Palette.Length);
            float scale = processor.DitherScale;

            for (int y = bounds.Top; y < bounds.Bottom; y++)
            {
                Span<TPixel> row = source.GetPixelRowSpan(y).Slice(bounds.X, bounds.Width);

                for (int x = 0; x < row.Length; x++)
                {
                    ref TPixel sourcePixel = ref row[x];
                    TPixel dithered = this.Dither(sourcePixel, x, y, spread, scale);
                    sourcePixel = processor.GetPaletteColor(dithered);
                }
            }
        }

        // Spread assumes an even colorspace distribution and precision.
        // TODO: Cubed root is currently used to represent 3 color channels
        // but we should introduce something to PixelTypeInfo.
        // https://bisqwit.iki.fi/story/howto/dither/jy/
        // https://en.wikipedia.org/wiki/Ordered_dithering#Algorithm
        internal static int CalculatePaletteSpread(int colors)
            => (int)(255 / Math.Max(1, Math.Pow(colors, 1.0 / 3) - 1));

        [MethodImpl(InliningOptions.ShortMethod)]
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
    }
}

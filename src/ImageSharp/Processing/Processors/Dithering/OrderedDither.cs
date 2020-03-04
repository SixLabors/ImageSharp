// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
            ImageFrame<TPixel> source,
            QuantizedFrame<TPixel> destination,
            Rectangle bounds)
            where TFrameQuantizer : struct, IFrameQuantizer<TPixel>
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var ditherOperation = new QuantizeDitherRowIntervalOperation<TFrameQuantizer, TPixel>(
                ref quantizer,
                in Unsafe.AsRef(this),
                source,
                destination,
                bounds);

            ParallelRowIterator.IterateRowIntervals(
                quantizer.Configuration,
                bounds,
                in ditherOperation);
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
            var ditherOperation = new PaletteDitherRowIntervalOperation<TPaletteDitherImageProcessor, TPixel>(
                in processor,
                in Unsafe.AsRef(this),
                source,
                bounds);

            ParallelRowIterator.IterateRowIntervals(
                processor.Configuration,
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
            where TPixel : unmanaged, IPixel<TPixel>
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
            where TPixel : unmanaged, IPixel<TPixel>
        {
            private readonly TFrameQuantizer quantizer;
            private readonly OrderedDither dither;
            private readonly ImageFrame<TPixel> source;
            private readonly QuantizedFrame<TPixel> destination;
            private readonly Rectangle bounds;
            private readonly int bitDepth;

            [MethodImpl(InliningOptions.ShortMethod)]
            public QuantizeDitherRowIntervalOperation(
                ref TFrameQuantizer quantizer,
                in OrderedDither dither,
                ImageFrame<TPixel> source,
                QuantizedFrame<TPixel> destination,
                Rectangle bounds)
            {
                this.quantizer = quantizer;
                this.dither = dither;
                this.source = source;
                this.destination = destination;
                this.bounds = bounds;
                this.bitDepth = ImageMaths.GetBitsNeededForColorDepth(destination.Palette.Length);
            }

            [MethodImpl(InliningOptions.ShortMethod)]
            public void Invoke(in RowInterval rows)
            {
                int offsetY = this.bounds.Top;
                int offsetX = this.bounds.Left;
                float scale = this.quantizer.Options.DitherScale;

                for (int y = rows.Min; y < rows.Max; y++)
                {
                    ref TPixel sourceRowRef = ref MemoryMarshal.GetReference(this.source.GetPixelRowSpan(y));
                    ref byte destinationRowRef = ref MemoryMarshal.GetReference(this.destination.GetPixelRowSpan(y - offsetY));

                    for (int x = this.bounds.Left; x < this.bounds.Right; x++)
                    {
                        TPixel dithered = this.dither.Dither(Unsafe.Add(ref sourceRowRef, x), x, y, this.bitDepth, scale);
                        Unsafe.Add(ref destinationRowRef, x - offsetX) = Unsafe.AsRef(this.quantizer).GetQuantizedColor(dithered, out TPixel _);
                    }
                }
            }
        }

        private readonly struct PaletteDitherRowIntervalOperation<TPaletteDitherImageProcessor, TPixel> : IRowIntervalOperation
            where TPaletteDitherImageProcessor : struct, IPaletteDitherImageProcessor<TPixel>
            where TPixel : unmanaged, IPixel<TPixel>
        {
            private readonly TPaletteDitherImageProcessor processor;
            private readonly OrderedDither dither;
            private readonly ImageFrame<TPixel> source;
            private readonly Rectangle bounds;
            private readonly float scale;
            private readonly int bitDepth;

            [MethodImpl(InliningOptions.ShortMethod)]
            public PaletteDitherRowIntervalOperation(
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
                this.bitDepth = ImageMaths.GetBitsNeededForColorDepth(processor.Palette.Span.Length);
            }

            [MethodImpl(InliningOptions.ShortMethod)]
            public void Invoke(in RowInterval rows)
            {
                for (int y = rows.Min; y < rows.Max; y++)
                {
                    ref TPixel sourceRowRef = ref MemoryMarshal.GetReference(this.source.GetPixelRowSpan(y));

                    for (int x = this.bounds.Left; x < this.bounds.Right; x++)
                    {
                        ref TPixel sourcePixel = ref Unsafe.Add(ref sourceRowRef, x);
                        TPixel dithered = this.dither.Dither(sourcePixel, x, y, this.bitDepth, this.scale);
                        sourcePixel = Unsafe.AsRef(this.processor).GetPaletteColor(dithered);
                    }
                }
            }
        }
    }
}

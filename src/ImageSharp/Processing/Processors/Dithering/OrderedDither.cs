// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Dithering
{
    /// <summary>
    /// An ordered dithering matrix with equal sides of arbitrary length
    /// </summary>
    public class OrderedDither : IDither
    {
        private readonly DenseMatrix<float> thresholdMatrix;
        private readonly int modulusX;
        private readonly int modulusY;

        /// <summary>
        /// Initializes a new instance of the <see cref="OrderedDither"/> class.
        /// </summary>
        /// <param name="length">The length of the matrix sides</param>
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

        /// <inheritdoc/>
        public DitherType DitherType { get; } = DitherType.OrderedDither;

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public TPixel Dither<TPixel>(
            ImageFrame<TPixel> image,
            Rectangle bounds,
            TPixel source,
            TPixel transformed,
            int x,
            int y,
            int bitDepth)
            where TPixel : struct, IPixel<TPixel>
        {
            // TODO: Should we consider a pixel format with a larger coror range?
            Rgba32 rgba = default;
            source.ToRgba32(ref rgba);
            Rgba32 attempt;

            // Srpead assumes an even colorspace distribution and precision.
            // Calculated as 0-255/component count. 256 / bitDepth
            // https://bisqwit.iki.fi/story/howto/dither/jy/
            // https://en.wikipedia.org/wiki/Ordered_dithering#Algorithm
            int spread = 256 / bitDepth;
            float factor = spread * this.thresholdMatrix[y % this.modulusY, x % this.modulusX];

            attempt.R = (byte)(rgba.R + factor).Clamp(byte.MinValue, byte.MaxValue);
            attempt.G = (byte)(rgba.G + factor).Clamp(byte.MinValue, byte.MaxValue);
            attempt.B = (byte)(rgba.B + factor).Clamp(byte.MinValue, byte.MaxValue);
            attempt.A = (byte)(rgba.A + factor).Clamp(byte.MinValue, byte.MaxValue);

            TPixel result = default;
            result.FromRgba32(attempt);

            return result;
        }
    }
}

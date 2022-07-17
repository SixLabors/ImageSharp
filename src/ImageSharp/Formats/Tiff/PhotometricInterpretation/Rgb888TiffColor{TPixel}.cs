// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation
{
    /// <summary>
    /// Implements the 'RGB' photometric interpretation (optimized for 8-bit full color images).
    /// </summary>
    internal class Rgb888TiffColor<TPixel> : TiffBaseColorDecoder<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        private readonly Configuration configuration;

        public Rgb888TiffColor(Configuration configuration) => this.configuration = configuration;

        /// <inheritdoc/>
        public override void Decode(ReadOnlySpan<byte> data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
        {
            int offset = 0;

            for (int y = top; y < top + height; y++)
            {
                Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);
                int byteCount = pixelRow.Length * 3;
                PixelOperations<TPixel>.Instance.FromRgb24Bytes(
                    this.configuration,
                    data.Slice(offset, byteCount),
                    pixelRow,
                    pixelRow.Length);

                offset += byteCount;
            }
        }
    }
}

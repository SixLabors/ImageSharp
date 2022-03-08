// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.Formats.Tiff.Utils;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation
{
    /// <summary>
    /// Implements the 'RGB' photometric interpretation with an alpha channel and with 16 bits for each channel.
    /// </summary>
    internal class Rgba16161616TiffColor<TPixel> : TiffBaseColorDecoder<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        private readonly bool isBigEndian;

        private readonly Configuration configuration;

        /// <summary>
        /// Initializes a new instance of the <see cref="Rgba16161616TiffColor{TPixel}" /> class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="isBigEndian">if set to <c>true</c> decodes the pixel data as big endian, otherwise as little endian.</param>
        public Rgba16161616TiffColor(Configuration configuration, bool isBigEndian)
        {
            this.configuration = configuration;
            this.isBigEndian = isBigEndian;
        }

        /// <inheritdoc/>
        public override void Decode(ReadOnlySpan<byte> data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
        {
            // Note: due to an issue with netcore 2.1 and default values and unpredictable behavior with those,
            // we define our own defaults as a workaround. See: https://github.com/dotnet/runtime/issues/55623
            Rgba64 rgba = TiffUtils.Rgba64Default;
            var color = default(TPixel);
            color.FromVector4(TiffUtils.Vector4Default);

            int offset = 0;

            for (int y = top; y < top + height; y++)
            {
                Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);

                if (this.isBigEndian)
                {
                    for (int x = 0; x < pixelRow.Length; x++)
                    {
                        ulong r = TiffUtils.ConvertToUShortBigEndian(data.Slice(offset, 2));
                        offset += 2;
                        ulong g = TiffUtils.ConvertToUShortBigEndian(data.Slice(offset, 2));
                        offset += 2;
                        ulong b = TiffUtils.ConvertToUShortBigEndian(data.Slice(offset, 2));
                        offset += 2;
                        ulong a = TiffUtils.ConvertToUShortBigEndian(data.Slice(offset, 2));
                        offset += 2;

                        pixelRow[x] = TiffUtils.ColorFromRgba64(rgba, r, g, b, a, color);
                    }
                }
                else
                {
                    int byteCount = pixelRow.Length * 8;
                    PixelOperations<TPixel>.Instance.FromRgba64Bytes(
                        this.configuration,
                        data.Slice(offset, byteCount),
                        pixelRow,
                        pixelRow.Length);

                    offset += byteCount;
                }
            }
        }
    }
}

// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using SixLabors.ImageSharp.Formats.Tiff.Utils;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation
{
    /// <summary>
    /// Implements the 'RGB' photometric interpretation with 'Planar' layout for each color channel with 32 bit.
    /// </summary>
    internal class Rgb32PlanarTiffColor<TPixel> : TiffBasePlanarColorDecoder<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        private readonly bool isBigEndian;

        /// <summary>
        /// Initializes a new instance of the <see cref="Rgb32PlanarTiffColor{TPixel}" /> class.
        /// </summary>
        /// <param name="isBigEndian">if set to <c>true</c> decodes the pixel data as big endian, otherwise as little endian.</param>
        public Rgb32PlanarTiffColor(bool isBigEndian) => this.isBigEndian = isBigEndian;

        /// <inheritdoc/>
        public override void Decode(IMemoryOwner<byte>[] data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
        {
            // Note: due to an issue with netcore 2.1 and default values and unpredictable behavior with those,
            // we define our own defaults as a workaround. See: https://github.com/dotnet/runtime/issues/55623
            var color = default(TPixel);
            color.FromVector4(TiffUtils.Vector4Default);

            Span<byte> redData = data[0].GetSpan();
            Span<byte> greenData = data[1].GetSpan();
            Span<byte> blueData = data[2].GetSpan();

            int offset = 0;
            for (int y = top; y < top + height; y++)
            {
                Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);
                if (this.isBigEndian)
                {
                    for (int x = 0; x < pixelRow.Length; x++)
                    {
                        ulong r = TiffUtils.ConvertToUIntBigEndian(redData.Slice(offset, 4));
                        ulong g = TiffUtils.ConvertToUIntBigEndian(greenData.Slice(offset, 4));
                        ulong b = TiffUtils.ConvertToUIntBigEndian(blueData.Slice(offset, 4));

                        offset += 4;

                        pixelRow[x] = TiffUtils.ColorScaleTo32Bit(r, g, b, color);
                    }
                }
                else
                {
                    for (int x = 0; x < pixelRow.Length; x++)
                    {
                        ulong r = TiffUtils.ConvertToUIntLittleEndian(redData.Slice(offset, 4));
                        ulong g = TiffUtils.ConvertToUIntLittleEndian(greenData.Slice(offset, 4));
                        ulong b = TiffUtils.ConvertToUIntLittleEndian(blueData.Slice(offset, 4));

                        offset += 4;

                        pixelRow[x] = TiffUtils.ColorScaleTo32Bit(r, g, b, color);
                    }
                }
            }
        }
    }
}

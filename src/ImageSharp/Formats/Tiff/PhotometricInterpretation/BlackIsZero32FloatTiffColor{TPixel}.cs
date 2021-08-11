// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using SixLabors.ImageSharp.Formats.Tiff.Utils;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation
{
    /// <summary>
    /// Implements the 'BlackIsZero' photometric interpretation for 32-bit float grayscale images.
    /// </summary>
    internal class BlackIsZero32FloatTiffColor<TPixel> : TiffBaseColorDecoder<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        private readonly bool isBigEndian;

        /// <summary>
        /// Initializes a new instance of the <see cref="BlackIsZero32FloatTiffColor{TPixel}" /> class.
        /// </summary>
        /// <param name="isBigEndian">if set to <c>true</c> decodes the pixel data as big endian, otherwise as little endian.</param>
        public BlackIsZero32FloatTiffColor(bool isBigEndian) => this.isBigEndian = isBigEndian;

        /// <inheritdoc/>
        public override void Decode(ReadOnlySpan<byte> data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
        {
            // Note: due to an issue with netcore 2.1 and default values and unpredictable behavior with those,
            // we define our own defaults as a workaround. See: https://github.com/dotnet/runtime/issues/55623
            var color = default(TPixel);
            color.FromVector4(TiffUtils.Vector4Default);
            byte[] buffer = new byte[4];

            int offset = 0;
            for (int y = top; y < top + height; y++)
            {
                Span<TPixel> pixelRow = pixels.GetRowSpan(y).Slice(left, width);
                if (this.isBigEndian)
                {
                    for (int x = 0; x < pixelRow.Length; x++)
                    {
                        data.Slice(offset, 4).CopyTo(buffer);
                        Array.Reverse(buffer);
                        float intensity = BitConverter.ToSingle(buffer, 0);
                        offset += 4;

                        var colorVector = new Vector4(intensity, intensity, intensity, 1.0f);
                        color.FromVector4(colorVector);
                        pixelRow[x] = color;
                    }
                }
                else
                {
                    for (int x = 0; x < pixelRow.Length; x++)
                    {
                        data.Slice(offset, 4).CopyTo(buffer);
                        float intensity = BitConverter.ToSingle(buffer, 0);
                        offset += 4;

                        var colorVector = new Vector4(intensity, intensity, intensity, 1.0f);
                        color.FromVector4(colorVector);
                        pixelRow[x] = color;
                    }
                }
            }
        }
    }
}

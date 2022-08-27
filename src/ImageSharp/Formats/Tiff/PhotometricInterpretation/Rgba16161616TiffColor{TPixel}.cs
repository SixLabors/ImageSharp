// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using System.Buffers;
using System.Numerics;
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

        private readonly MemoryAllocator memoryAllocator;

        private readonly TiffExtraSampleType? extraSamplesType;

        /// <summary>
        /// Initializes a new instance of the <see cref="Rgba16161616TiffColor{TPixel}" /> class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="memoryAllocator">The memory allocator.</param>
        /// <param name="isBigEndian">if set to <c>true</c> decodes the pixel data as big endian, otherwise as little endian.</param>
        /// <param name="extraSamplesType">The type of the extra samples.</param>
        public Rgba16161616TiffColor(Configuration configuration, MemoryAllocator memoryAllocator, TiffExtraSampleType? extraSamplesType, bool isBigEndian)
        {
            this.configuration = configuration;
            this.isBigEndian = isBigEndian;
            this.memoryAllocator = memoryAllocator;
            this.extraSamplesType = extraSamplesType;
        }

        /// <inheritdoc/>
        public override void Decode(ReadOnlySpan<byte> data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
        {
            Rgba64 rgba = TiffUtils.Rgba64Default;
            var color = default(TPixel);
            color.FromScaledVector4(Vector4.Zero);

            bool hasAssociatedAlpha = this.extraSamplesType.HasValue && this.extraSamplesType == TiffExtraSampleType.AssociatedAlphaData;
            int offset = 0;

            using IMemoryOwner<Vector4> vectors = hasAssociatedAlpha ? this.memoryAllocator.Allocate<Vector4>(width) : null;
            Span<Vector4> vectorsSpan = hasAssociatedAlpha ? vectors.GetSpan() : Span<Vector4>.Empty;
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

                        pixelRow[x] = hasAssociatedAlpha ?
                            TiffUtils.ColorFromRgba64Premultiplied(rgba, r, g, b, a, color) :
                            TiffUtils.ColorFromRgba64(rgba, r, g, b, a, color);
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

                    if (hasAssociatedAlpha)
                    {
                        PixelOperations<TPixel>.Instance.ToVector4(this.configuration, pixelRow, vectorsSpan);
                        PixelOperations<TPixel>.Instance.FromVector4Destructive(this.configuration, vectorsSpan, pixelRow, PixelConversionModifiers.Premultiply | PixelConversionModifiers.Scale);
                    }

                    offset += byteCount;
                }
            }
        }
    }
}

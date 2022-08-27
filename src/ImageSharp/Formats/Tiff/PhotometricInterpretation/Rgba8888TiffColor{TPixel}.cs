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
    /// Implements the 'RGB' photometric interpretation with an alpha channel and 8 bits per channel.
    /// </summary>
    internal class Rgba8888TiffColor<TPixel> : TiffBaseColorDecoder<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        private readonly Configuration configuration;

        private readonly MemoryAllocator memoryAllocator;

        private readonly TiffExtraSampleType? extraSamplesType;

        public Rgba8888TiffColor(Configuration configuration, MemoryAllocator memoryAllocator, TiffExtraSampleType? extraSamplesType)
        {
            this.configuration = configuration;
            this.memoryAllocator = memoryAllocator;
            this.extraSamplesType = extraSamplesType;
        }

        /// <inheritdoc/>
        public override void Decode(ReadOnlySpan<byte> data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
        {
            int offset = 0;
            bool hasAssociatedAlpha = this.extraSamplesType.HasValue && this.extraSamplesType == TiffExtraSampleType.AssociatedAlphaData;

            var color = default(TPixel);
            color.FromScaledVector4(Vector4.Zero);
            using IMemoryOwner<Vector4> vectors = hasAssociatedAlpha ? this.memoryAllocator.Allocate<Vector4>(width) : null;
            Span<Vector4> vectorsSpan = hasAssociatedAlpha ? vectors.GetSpan() : Span<Vector4>.Empty;
            for (int y = top; y < top + height; y++)
            {
                Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);
                int byteCount = pixelRow.Length * 4;
                PixelOperations<TPixel>.Instance.FromRgba32Bytes(
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

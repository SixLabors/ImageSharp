// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using System.Numerics;
using SixLabors.ImageSharp.Formats.Tiff.Utils;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation
{
    /// <summary>
    /// Implements the 'RGB' photometric interpretation with alpha channel (for all bit depths).
    /// </summary>
    internal class RgbaTiffColor<TPixel> : TiffBaseColorDecoder<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        private readonly float rFactor;

        private readonly float gFactor;

        private readonly float bFactor;

        private readonly float aFactor;

        private readonly ushort bitsPerSampleR;

        private readonly ushort bitsPerSampleG;

        private readonly ushort bitsPerSampleB;

        private readonly ushort bitsPerSampleA;

        private readonly TiffExtraSampleType? extraSamplesType;

        public RgbaTiffColor(TiffExtraSampleType? extraSampleType, TiffBitsPerSample bitsPerSample)
        {
            this.bitsPerSampleR = bitsPerSample.Channel0;
            this.bitsPerSampleG = bitsPerSample.Channel1;
            this.bitsPerSampleB = bitsPerSample.Channel2;
            this.bitsPerSampleA = bitsPerSample.Channel3;

            this.rFactor = (1 << this.bitsPerSampleR) - 1.0f;
            this.gFactor = (1 << this.bitsPerSampleG) - 1.0f;
            this.bFactor = (1 << this.bitsPerSampleB) - 1.0f;
            this.aFactor = (1 << this.bitsPerSampleA) - 1.0f;

            this.extraSamplesType = extraSampleType;
        }

        /// <inheritdoc/>
        public override void Decode(ReadOnlySpan<byte> data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
        {
            var color = default(TPixel);

            var bitReader = new BitReader(data);

            bool hasAssociatedAlpha = this.extraSamplesType.HasValue && this.extraSamplesType == TiffExtraSampleType.AssociatedAlphaData;

            for (int y = top; y < top + height; y++)
            {
                Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);
                for (int x = 0; x < pixelRow.Length; x++)
                {
                    float r = bitReader.ReadBits(this.bitsPerSampleR) / this.rFactor;
                    float g = bitReader.ReadBits(this.bitsPerSampleG) / this.gFactor;
                    float b = bitReader.ReadBits(this.bitsPerSampleB) / this.bFactor;
                    float a = bitReader.ReadBits(this.bitsPerSampleB) / this.aFactor;

                    var vec = new Vector4(r, g, b, a);
                    if (hasAssociatedAlpha)
                    {
                        pixelRow[x] = TiffUtils.UnPremultiply(ref vec, color);
                    }
                    else
                    {
                        color.FromScaledVector4(vec);
                        pixelRow[x] = color;
                    }
                }

                bitReader.NextRow();
            }
        }
    }
}

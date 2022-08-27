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
    /// Implements the 'RGB' photometric interpretation with an alpha channel and with 'Planar' layout (for all bit depths).
    /// </summary>
    internal class RgbaPlanarTiffColor<TPixel> : TiffBasePlanarColorDecoder<TPixel>
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

        private readonly TiffExtraSampleType? extraSampleType;

        public RgbaPlanarTiffColor(TiffExtraSampleType? extraSampleType, TiffBitsPerSample bitsPerSample)
        {
            this.bitsPerSampleR = bitsPerSample.Channel0;
            this.bitsPerSampleG = bitsPerSample.Channel1;
            this.bitsPerSampleB = bitsPerSample.Channel2;
            this.bitsPerSampleA = bitsPerSample.Channel3;

            this.rFactor = (1 << this.bitsPerSampleR) - 1.0f;
            this.gFactor = (1 << this.bitsPerSampleG) - 1.0f;
            this.bFactor = (1 << this.bitsPerSampleB) - 1.0f;
            this.aFactor = (1 << this.bitsPerSampleA) - 1.0f;

            this.extraSampleType = extraSampleType;
        }

        /// <summary>
        /// Decodes pixel data using the current photometric interpretation.
        /// </summary>
        /// <param name="data">The buffers to read image data from.</param>
        /// <param name="pixels">The image buffer to write pixels to.</param>
        /// <param name="left">The x-coordinate of the left-hand side of the image block.</param>
        /// <param name="top">The y-coordinate of the  top of the image block.</param>
        /// <param name="width">The width of the image block.</param>
        /// <param name="height">The height of the image block.</param>
        public override void Decode(IMemoryOwner<byte>[] data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
        {
            var color = default(TPixel);
            bool hasAssociatedAlpha = this.extraSampleType.HasValue && this.extraSampleType == TiffExtraSampleType.AssociatedAlphaData;

            var rBitReader = new BitReader(data[0].GetSpan());
            var gBitReader = new BitReader(data[1].GetSpan());
            var bBitReader = new BitReader(data[2].GetSpan());
            var aBitReader = new BitReader(data[3].GetSpan());

            for (int y = top; y < top + height; y++)
            {
                Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);
                for (int x = 0; x < pixelRow.Length; x++)
                {
                    float r = rBitReader.ReadBits(this.bitsPerSampleR) / this.rFactor;
                    float g = gBitReader.ReadBits(this.bitsPerSampleG) / this.gFactor;
                    float b = bBitReader.ReadBits(this.bitsPerSampleB) / this.bFactor;
                    float a = aBitReader.ReadBits(this.bitsPerSampleA) / this.aFactor;

                    var vec = new Vector4(r, g, b, a);
                    if (hasAssociatedAlpha)
                    {
                        color = TiffUtils.UnPremultiply(ref vec, color);
                    }
                    else
                    {
                        color.FromScaledVector4(vec);
                    }

                    pixelRow[x] = color;
                }

                rBitReader.NextRow();
                gBitReader.NextRow();
                bBitReader.NextRow();
                aBitReader.NextRow();
            }
        }
    }
}

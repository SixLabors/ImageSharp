using System;
using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.Quantization.PaletteLookup
{
    internal struct LinearPaletteLookup<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        private readonly Vector4[] paletteVectors;

        public LinearPaletteLookup(Configuration configuration, ReadOnlyMemory<TPixel> palette)
        {
            this.paletteVectors = new Vector4[palette.Length];
            PixelOperations<TPixel>.Instance.ToVector4(configuration, palette.Span, this.paletteVectors);
        }

        public byte GetPaletteIndexFor(TPixel pixel)
        {
            Vector4[] palette = this.paletteVectors;
            float minDistance = float.MaxValue;
            var pixelVec = pixel.ToVector4();
            int minIdx = -1;

            for (int i = 0; i < this.paletteVectors.Length; i++)
            {
                Vector4 paletteVec = palette[i];
                float distance = Vector4.DistanceSquared(pixelVec, paletteVec);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    minIdx = i;
                }
            }

            return (byte)minIdx;
        }
    }
}

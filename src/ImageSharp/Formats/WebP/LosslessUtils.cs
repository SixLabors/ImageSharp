namespace SixLabors.ImageSharp.Formats.WebP
{
    /// <summary>
    /// Utility functions for the lossless decoder.
    /// </summary>
    internal static class LosslessUtils
    {
        /// <summary>
        /// Add green to blue and red channels (i.e. perform the inverse transform of 'subtract green').
        /// </summary>
        /// <param name="pixelData">The pixel data to apply the transformation.</param>
        public static void AddGreenToBlueAndRed(uint[] pixelData)
        {
            for (int i = 0; i < pixelData.Length; i++)
            {
                uint argb = pixelData[i];
                uint green = (argb >> 8) & 0xff;
                uint redBlue = argb & 0x00ff00ffu;
                redBlue += (green << 16) | green;
                redBlue &= 0x00ff00ffu;
                pixelData[i] = (argb & 0xff00ff00u) | redBlue;
            }
        }

        public static void ColorSpaceInverseTransform(Vp8LTransform transform, uint[] pixelData, int yEnd)
        {
            int width = transform.XSize;
            int tileWidth = 1 << transform.Bits;
            int mask = tileWidth - 1;
            int safeWidth = width & ~mask;
            int remainingWidth = width - safeWidth;
            int tilesPerRow = SubSampleSize(width, transform.Bits);
            int y = 0;

            /*uint[] predRow = transform.Data + (y >> transform.Bits) * tilesPerRow;

            while (y < yEnd)
            {
                uint[] pred = predRow;
                VP8LMultipliers m = { 0, 0, 0 };
                const uint32_t* const src_safe_end = src + safeWidth;
                const uint32_t* const src_end = src + width;
                while (src<src_safe_end)
                {
                    ColorCodeToMultipliers(*pred++, &m);
                    VP8LTransformColorInverse(&m, src, tileWidth, dst);
                    src += tileWidth;
                    dst += tileWidth;
                }

                if (src < src_end)
                {
                    ColorCodeToMultipliers(*pred++, &m);
                    VP8LTransformColorInverse(&m, src, remainingWidth, dst);
                    src += remaining_width;
                    dst += remaining_width;
                }

                ++y;
                if ((y & mask) == 0)
                {
                    predRow += tilesPerRow;
                }
            }*/
        }

        /// <summary>
        /// Computes sampled size of 'size' when sampling using 'sampling bits'.
        /// </summary>
        public static int SubSampleSize(int size, int samplingBits)
        {
            return (size + (1 << samplingBits) - 1) >> samplingBits;
        }
    }
}

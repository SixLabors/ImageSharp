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

        public static void ColorSpaceInverseTransform(Vp8LTransform transform, uint[] pixelData)
        {
            int width = transform.XSize;
            int yEnd = transform.YSize;
            int tileWidth = 1 << transform.Bits;
            int mask = tileWidth - 1;
            int safeWidth = width & ~mask;
            int remainingWidth = width - safeWidth;
            int tilesPerRow = SubSampleSize(width, transform.Bits);
            int y = 0;
            uint predRow = transform.Data[(y >> transform.Bits) * tilesPerRow];

            int pixelPos = 0;
            while (y < yEnd)
            {
                uint pred = predRow;
                Vp8LMultipliers m = default(Vp8LMultipliers);
                int srcSafeEnd = pixelPos + safeWidth;
                int srcEnd = pixelPos + width;
                while (pixelPos < srcSafeEnd)
                {
                    ColorCodeToMultipliers(pred++, ref m);
                    TransformColorInverse(m, pixelData, pixelPos, tileWidth);
                    pixelPos += tileWidth;
                }

                if (pixelPos < srcEnd)
                {
                    ColorCodeToMultipliers(pred++, ref m);
                    TransformColorInverse(m, pixelData, pixelPos, remainingWidth);
                    pixelPos += remainingWidth;
                }

                ++y;
                if ((y & mask) == 0)
                {
                    predRow += (uint)tilesPerRow;
                }
            }
        }

        public static void TransformColorInverse(Vp8LMultipliers m, uint[] pixelData, int start, int numPixels)
        {
            int end = start + numPixels;
            for (int i = start; i < end; i++)
            {
                uint argb = pixelData[i];
                sbyte green = (sbyte)(argb >> 8);
                uint red = argb >> 16;
                int newRed = (int)(red & 0xff);
                int newBlue = (int)argb & 0xff;
                newRed += ColorTransformDelta(m.GreenToRed, (sbyte)green);
                newRed &= 0xff;
                newBlue += ColorTransformDelta(m.GreenToBlue, (sbyte)green);
                newBlue += ColorTransformDelta(m.RedToBlue, (sbyte)newRed);
                newBlue &= 0xff;
                var pixelValue = (uint)((argb & 0xff00ff00u) | (newRed << 16) | newBlue);
                pixelData[i] = (uint)((argb & 0xff00ff00u) | (newRed << 16) | newBlue);
            }
        }

        /// <summary>
        /// Computes sampled size of 'size' when sampling using 'sampling bits'.
        /// </summary>
        public static int SubSampleSize(int size, int samplingBits)
        {
            return (size + (1 << samplingBits) - 1) >> samplingBits;
        }

        private static int ColorTransformDelta(sbyte colorPred, sbyte color)
        {
            return ((int)colorPred * color) >> 5;
        }

        private static void ColorCodeToMultipliers(uint colorCode, ref Vp8LMultipliers m)
        {
            m.GreenToRed = (sbyte)(colorCode & 0xff);
            m.GreenToBlue = (sbyte)((colorCode >> 8) & 0xff);
            m.RedToBlue = (sbyte)((colorCode >> 16) & 0xff);
        }

        internal struct Vp8LMultipliers
        {
            public sbyte GreenToRed;

            public sbyte GreenToBlue;

            public sbyte RedToBlue;
        }
    }
}

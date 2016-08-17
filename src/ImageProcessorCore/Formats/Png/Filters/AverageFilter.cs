namespace ImageProcessorCore.Formats
{
    using System;

    internal static class AverageFilter
    {
        public static byte[] Decode(byte[] scanline, byte[] previousScanline, int bytesPerPixel)
        {
            byte[] result = new byte[scanline.Length];

            for (int x = 1; x < scanline.Length; x++)
            {
                byte left = (x - bytesPerPixel < 1) ? (byte)0 : result[x - bytesPerPixel];
                byte above = previousScanline[x];

                result[x] = (byte)((scanline[x] + Average(left, above)) % 256);
            }

            return result;
        }

        public static byte[] Encode(byte[] scanline, byte[] previousScanline, int bytesPerPixel)
        {
            var encodedScanline = new byte[scanline.Length + 1];

            encodedScanline[0] = (byte)FilterType.Average;

            for (int x = 0; x < scanline.Length; x++)
            {
                byte left = (x - bytesPerPixel < 0) ? (byte)0 : scanline[x - bytesPerPixel];
                byte above = previousScanline[x];

                encodedScanline[x + 1] = (byte)((scanline[x] - Average(left, above)) % 256);
            }

            return encodedScanline;
        }

        private static int Average(byte left, byte above)
        {
            return Convert.ToInt32(Math.Floor((left + above) / 2.0));
        }
    }
}

namespace ImageProcessorCore.Formats
{
    using System;

    internal static class PaethFilter
    {
        public static byte[] Decode(byte[] scanline, byte[] previousScanline, int bytesPerPixel)
        {
            byte[] result = new byte[scanline.Length];

            for (int x = 1; x < scanline.Length; x++)
            {
                byte left = (x - bytesPerPixel < 1) ? (byte)0 : result[x - bytesPerPixel];
                byte above = previousScanline[x];
                byte upperLeft = (x - bytesPerPixel < 1) ? (byte)0 : previousScanline[x - bytesPerPixel];

                result[x] = (byte)((scanline[x] + PaethPredictor(left, above, upperLeft)) % 256);
            }

            return result;
        }

        public static byte[] Encode(byte[] scanline, byte[] previousScanline, int bytesPerPixel)
        {
            var encodedScanline = new byte[scanline.Length + 1];

            encodedScanline[0] = (byte)FilterType.Paeth;

            for (int x = 0; x < scanline.Length; x++)
            {
                byte left = (x - bytesPerPixel < 0) ? (byte)0 : scanline[x - bytesPerPixel];
                byte above = previousScanline[x];
                byte upperLeft = (x - bytesPerPixel < 0) ? (byte)0 : previousScanline[x - bytesPerPixel];

                encodedScanline[x + 1] = (byte)((scanline[x] - PaethPredictor(left, above, upperLeft)) % 256);
            }

            return encodedScanline;
        }

        private static int PaethPredictor(int a, int b, int c)
        {
            int p = a + b - c;
            int pa = Math.Abs(p - a);
            int pb = Math.Abs(p - b);
            int pc = Math.Abs(p - c);

            if ((pa <= pb) && (pa <= pc))
            {
                return a;
            }
            else
            {
                if (pb <= pc)
                {
                    return b;
                }
                else
                {
                    return c;
                }
            }
        }
    }

}

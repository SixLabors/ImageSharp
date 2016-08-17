namespace ImageProcessorCore.Formats
{
    internal static class UpFilter
    {
        public static byte[] Decode(byte[] scanline, byte[] previousScanline)
        {
            byte[] result = new byte[scanline.Length];

            for (int x = 1; x < scanline.Length; x++)
            {
                byte above = previousScanline[x];

                result[x] = (byte)((scanline[x] + above) % 256);
            }

            return result;
        }

        public static byte[] Encode(byte[] scanline, byte[] previousScanline)
        {
            var encodedScanline = new byte[scanline.Length + 1];

            encodedScanline[0] = (byte)FilterType.Up;

            for (int x = 0; x < scanline.Length; x++)
            {
                byte above = previousScanline[x];

                encodedScanline[x + 1] = (byte)((scanline[x] - above) % 256);
            }

            return encodedScanline;
        }
    }
}

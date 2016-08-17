namespace ImageProcessorCore.Formats
{
    internal static class SubFilter
    {
        public static byte[] Decode(byte[] scanline, int bytesPerPixel)
        {
            byte[] result = new byte[scanline.Length];

            for (int x = 1; x < scanline.Length; x++)
            {
                byte priorRawByte = (x - bytesPerPixel < 1) ? (byte)0 : result[x - bytesPerPixel];

                result[x] = (byte)((scanline[x] + priorRawByte) % 256);
            }

            return result;
        }

        public static byte[] Encode(byte[] scanline, int bytesPerPixel)
        {
            var encodedScanline = new byte[scanline.Length + 1];

            encodedScanline[0] = (byte)FilterType.Sub;

            for (int x = 0; x < scanline.Length; x++)
            {
                byte priorRawByte = (x - bytesPerPixel < 0) ? (byte)0 : scanline[x - bytesPerPixel];

                encodedScanline[x + 1] = (byte)((scanline[x] - priorRawByte) % 256);
            }

            return encodedScanline;
        }
    }
}

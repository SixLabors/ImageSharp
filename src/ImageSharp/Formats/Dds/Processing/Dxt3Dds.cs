using System;
using SixLabors.ImageSharp.Formats.Dds;
using SixLabors.ImageSharp.Formats.Dds.Processing;

namespace Pfim
{
    internal class Dxt3Dds : CompressedDds
    {
        private const byte PIXEL_DEPTH = 4;
        private const byte DIV_SIZE = 4;

        protected override byte DivSize => DIV_SIZE;
        protected override byte CompressedBytesPerBlock => 16;
        protected override byte PixelDepthBytes => PIXEL_DEPTH;
        public override int BitsPerPixel => PIXEL_DEPTH * 8;
        public override ImageFormat Format => ImageFormat.Rgba32;

        public Dxt3Dds(DdsHeader ddsHeader, DdsHeaderDxt10 ddsHeaderDxt10)
            : base(ddsHeader, ddsHeaderDxt10)
        {
        }

        private readonly Color888[] colors = new Color888[4];

        protected override int Decode(Span<byte> stream, Span<byte> data, int streamIndex, int dataIndex, int stride)
        {
            /* 
             * Strategy for decompression:
             * -We're going to decode both alpha and color at the same time 
             * to save on space and time as we don't have to allocate an array 
             * to store values for later use.
             */

            // Remember where the alpha data is stored so we can decode simultaneously
            int alphaPtr = streamIndex;

            // Jump ahead to the color data
            streamIndex += 8;

            // Colors are stored in a pair of 16 bits
            ushort color0 = stream[streamIndex++];
            color0 |= (ushort)(stream[streamIndex++] << 8);

            ushort color1 = (stream[streamIndex++]);
            color1 |= (ushort)(stream[streamIndex++] << 8);

            // Extract R5G6B5 (in that order)
            colors[0].r = (byte)((color0 & 0x1f));
            colors[0].g = (byte)((color0 & 0x7E0) >> 5);
            colors[0].b = (byte)((color0 & 0xF800) >> 11);
            colors[0].r = (byte)(colors[0].r << 3 | colors[0].r >> 2);
            colors[0].g = (byte)(colors[0].g << 2 | colors[0].g >> 3);
            colors[0].b = (byte)(colors[0].b << 3 | colors[0].b >> 2);

            colors[1].r = (byte)((color1 & 0x1f));
            colors[1].g = (byte)((color1 & 0x7E0) >> 5);
            colors[1].b = (byte)((color1 & 0xF800) >> 11);
            colors[1].r = (byte)(colors[1].r << 3 | colors[1].r >> 2);
            colors[1].g = (byte)(colors[1].g << 2 | colors[1].g >> 3);
            colors[1].b = (byte)(colors[1].b << 3 | colors[1].b >> 2);

            // Used the two extracted colors to create two new colors
            // that are slightly different.
            colors[2].r = (byte)((2 * colors[0].r + colors[1].r) / 3);
            colors[2].g = (byte)((2 * colors[0].g + colors[1].g) / 3);
            colors[2].b = (byte)((2 * colors[0].b + colors[1].b) / 3);

            colors[3].r = (byte)((colors[0].r + 2 * colors[1].r) / 3);
            colors[3].g = (byte)((colors[0].g + 2 * colors[1].g) / 3);
            colors[3].b = (byte)((colors[0].b + 2 * colors[1].b) / 3);

            for (int i = 0; i < 4; i++)
            {
                byte rowVal = stream[streamIndex++];

                // Each row of rgb values have 4 alpha values that  are
                // encoded in 4 bits
                ushort rowAlpha = stream[alphaPtr++];
                rowAlpha |= (ushort)(stream[alphaPtr++] << 8);

                for (int j = 0; j < 8; j += 2)
                {
                    byte currentAlpha = (byte)((rowAlpha >> (j * 2)) & 0x0f);
                    currentAlpha |= (byte)(currentAlpha << 4);
                    var col = colors[((rowVal >> j) & 0x03)];
                    data[dataIndex++] = col.r;
                    data[dataIndex++] = col.g;
                    data[dataIndex++] = col.b;
                    data[dataIndex++] = currentAlpha;
                }
                dataIndex += PIXEL_DEPTH * (stride - DIV_SIZE);
            }
            return streamIndex;
        }
    }
}

using System;
using Pfim.dds;
using SixLabors.ImageSharp.Formats.Dds;
using SixLabors.ImageSharp.Formats.Dds.Processing;

namespace Pfim
{
    internal class Dxt5Dds : CompressedDds
    {
        private const byte PIXEL_DEPTH = 4;
        private const byte DIV_SIZE = 4;

        private readonly byte[] alpha = new byte[8];
        private readonly Color888[] colors = new Color888[4];

        public override int BitsPerPixel => 8 * PIXEL_DEPTH;
        public override ImageFormat Format => ImageFormat.Rgba32;
        protected override byte DivSize => DIV_SIZE;
        protected override byte CompressedBytesPerBlock => 16;

        public Dxt5Dds(DdsHeader ddsHeader, DdsHeaderDxt10 ddsHeaderDxt10)
            : base(ddsHeader, ddsHeaderDxt10)
        {
        }

        protected override byte PixelDepthBytes => PIXEL_DEPTH;

        protected override int Decode(Span<byte> stream, Span<byte> data, int streamIndex, int dataIndex, int stride)
        {
            streamIndex = Bc5Dds.ExtractGradient(alpha, stream, streamIndex);

            ulong alphaCodes = stream[streamIndex++];
            alphaCodes |= ((ulong)stream[streamIndex++] << 8);
            alphaCodes |= ((ulong)stream[streamIndex++] << 16);
            alphaCodes |= ((ulong)stream[streamIndex++] << 24);
            alphaCodes |= ((ulong)stream[streamIndex++] << 32);
            alphaCodes |= ((ulong)stream[streamIndex++] << 40);

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

            colors[2].r = (byte)((2 * colors[0].r + colors[1].r) / 3);
            colors[2].g = (byte)((2 * colors[0].g + colors[1].g) / 3);
            colors[2].b = (byte)((2 * colors[0].b + colors[1].b) / 3);

            colors[3].r = (byte)((colors[0].r + 2 * colors[1].r) / 3);
            colors[3].g = (byte)((colors[0].g + 2 * colors[1].g) / 3);
            colors[3].b = (byte)((colors[0].b + 2 * colors[1].b) / 3);

            for (int alphaShift = 0; alphaShift < 48; alphaShift += 12)
            {
                byte rowVal = stream[streamIndex++];
                for (int j = 0; j < 4; j++)
                {
                    // 3 bits determine alpha index to use
                    byte alphaIndex = (byte)((alphaCodes >> (alphaShift + 3 * j)) & 0x07);
                    var col = colors[((rowVal >> (j * 2)) & 0x03)];
                    data[dataIndex++] = col.r;
                    data[dataIndex++] = col.g;
                    data[dataIndex++] = col.b;
                    data[dataIndex++] = alpha[alphaIndex];
                }
                dataIndex += PIXEL_DEPTH * (stride - DIV_SIZE);
            }
            return streamIndex;
        }
    }
}

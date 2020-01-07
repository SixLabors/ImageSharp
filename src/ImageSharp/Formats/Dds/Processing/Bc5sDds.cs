using System;
using System.Collections.Generic;
using System.Text;
using SixLabors.ImageSharp.Formats.Dds;
using SixLabors.ImageSharp.Formats.Dds.Processing;

namespace Pfim.dds
{
    internal class Bc5sDds : CompressedDds
    {
        public Bc5sDds(DdsHeader ddsHeader, DdsHeaderDxt10 ddsHeaderDxt10)
            : base(ddsHeader, ddsHeaderDxt10)
        {
        }

        public override int BitsPerPixel => 24;
        public override ImageFormat Format => ImageFormat.Rgb24;
        protected override byte PixelDepthBytes => 3;
        protected override byte DivSize => 4;
        protected override byte CompressedBytesPerBlock => 16;

        protected override int Decode(Span<byte> stream, Span<byte> data, int streamIndex, int dataIndex, int stride)
        {
            sbyte red0 = (sbyte)stream[streamIndex++];
            sbyte red1 = (sbyte)stream[streamIndex++];
            red0 = (red0 == -128) ? (sbyte)-127 : red0;
            red1 = (red1 == -128) ? (sbyte)-127 : red1;
            ulong rIndex = stream[streamIndex++];
            rIndex |= ((ulong)stream[streamIndex++] << 8);
            rIndex |= ((ulong)stream[streamIndex++] << 16);
            rIndex |= ((ulong)stream[streamIndex++] << 24);
            rIndex |= ((ulong)stream[streamIndex++] << 32);
            rIndex |= ((ulong)stream[streamIndex++] << 40);

            sbyte green0 = (sbyte)stream[streamIndex++];
            sbyte green1 = (sbyte)stream[streamIndex++];
            green0 = (green0 == -128) ? (sbyte)-127 : green0;
            green1 = (green1 == -128) ? (sbyte)-127 : green1;
            ulong gIndex = stream[streamIndex++];
            gIndex |= ((ulong)stream[streamIndex++] << 8);
            gIndex |= ((ulong)stream[streamIndex++] << 16);
            gIndex |= ((ulong)stream[streamIndex++] << 24);
            gIndex |= ((ulong)stream[streamIndex++] << 32);
            gIndex |= ((ulong)stream[streamIndex++] << 40);

            for (int i = 0; i < 16; ++i)
            {
                byte rSel = (byte)((uint)(rIndex >> (3 * i)) & 0x07);
                byte gSel = (byte)((uint)(gIndex >> (3 * i)) & 0x07);

                data[dataIndex++] = 0; // skip blue
                data[dataIndex++] = Bc4sDds.InterpolateColor(gSel, green0, green1);
                data[dataIndex++] = Bc4sDds.InterpolateColor(rSel, red0, red1);

                // Is mult 4?
                if (((i + 1) & 0x3) == 0)
                {
                    dataIndex += PixelDepthBytes * (stride - DivSize);
                }
            }

            return streamIndex;
        }
    }
}

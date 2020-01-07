using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using Pfim;

namespace SixLabors.ImageSharp.Formats.Dds.Processing
{
    internal class Bc4Dds : CompressedDds
    {
        public Bc4Dds(DdsHeader ddsHeader, DdsHeaderDxt10 ddsHeaderDxt10)
            : base(ddsHeader, ddsHeaderDxt10)
        {
        }

        public override int BitsPerPixel => 8;
        public override ImageFormat Format => ImageFormat.Rgb8;
        protected override byte PixelDepthBytes => 1;
        protected override byte DivSize => 4;
        protected override byte CompressedBytesPerBlock => 8;

        protected override int Decode(Span<byte> stream, Span<byte> data, int streamIndex, int dataIndex, int stride)
        {
            byte red0 = stream[streamIndex++];
            byte red1 = stream[streamIndex++];
            ulong rIndex = stream[streamIndex++];
            rIndex |= (ulong)stream[streamIndex++] << 8;
            rIndex |= (ulong)stream[streamIndex++] << 16;
            rIndex |= (ulong)stream[streamIndex++] << 24;
            rIndex |= (ulong)stream[streamIndex++] << 32;
            rIndex |= (ulong)stream[streamIndex++] << 40;

            for (int i = 0; i < 16; ++i)
            {
                byte index = (byte)((uint)(rIndex >> 3 * i) & 0x07);

                data[dataIndex++] = InterpolateColor(index, red0, red1);

                // Is mult 4?
                if ((i + 1 & 0x3) == 0)
                {
                    dataIndex += PixelDepthBytes * (stride - DivSize);
                }
            }

            return streamIndex;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static byte InterpolateColor(byte index, byte red0, byte red1)
        {
            byte red;
            if (index == 0)
            {
                red = red0;
            }
            else if (index == 1)
            {
                red = red1;
            }
            else
            {
                if (red0 > red1)
                {
                    index -= 1;
                    red = (byte)((red0 * (7 - index) + red1 * index) / 7.0f + 0.5f);
                }
                else
                {
                    if (index == 6)
                    {
                        red = 0;
                    }
                    else if (index == 7)
                    {
                        red = 255;
                    }
                    else
                    {
                        index -= 1;
                        red = (byte)((red0 * (5 - index) + red1 * index) / 5.0f + 0.5f);
                    }
                }
            }
            return red;
        }
    }
}

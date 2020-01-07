using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using SixLabors.ImageSharp.Formats.Dds;
using SixLabors.ImageSharp.Formats.Dds.Processing;

namespace Pfim.dds
{
    internal class Bc4sDds : CompressedDds
    {
        private const float Multiplier = (255.0f / 254.0f);

        public Bc4sDds(DdsHeader ddsHeader, DdsHeaderDxt10 ddsHeaderDxt10)
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

            for (int i = 0; i < 16; ++i)
            {
                uint index = (byte)((uint)(rIndex >> (3 * i)) & 0x07);

                data[dataIndex++] = InterpolateColor((byte)index, red0, red1);

                // Is mult 4?
                if (((i + 1) & 0x3) == 0)
                {
                    dataIndex += PixelDepthBytes * (stride - DivSize);
                }
            }

            return streamIndex;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static byte InterpolateColor(byte index, sbyte red0, sbyte red1)
        {
            float red;
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
                    red = (red0 * (7 - index) + red1 * index) / 7.0f;
                }
                else
                {
                    if (index == 6)
                    {
                        red = -127.0f;
                    }
                    else if (index == 7)
                    {
                        red = 127.0f;
                    }
                    else
                    {
                        index -= 1;
                        red = (red0 * (5 - index) + red1 * index) / 5.0f;
                    }
                }
            }
            return (byte)(((red + 127) * Multiplier) + 0.5f);
        }
    }
}

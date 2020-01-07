using System;
using SixLabors.ImageSharp.Formats.Dds;
using SixLabors.ImageSharp.Formats.Dds.Processing;

namespace Pfim.dds
{
    internal class Bc5Dds : CompressedDds
    {
        private readonly byte[] _firstGradient = new byte[8];
        private readonly byte[] _secondGradient = new byte[8];

        public Bc5Dds(DdsHeader ddsHeader, DdsHeaderDxt10 ddsHeaderDxt10)
            : base(ddsHeader, ddsHeaderDxt10)
        {
        }

        public override int BitsPerPixel => PixelDepthBytes * 8;
        public override ImageFormat Format => ImageFormat.Rgb24;
        protected override byte PixelDepthBytes => 3;
        protected override byte DivSize => 4;
        protected override byte CompressedBytesPerBlock => 16;

        protected override int Decode(Span<byte> stream, Span<byte> data, int streamIndex, int dataIndex, int stride)
        {
            streamIndex = ExtractGradient(_firstGradient, stream, streamIndex);
            ulong firstCodes = stream[streamIndex++];
            firstCodes |= ((ulong)stream[streamIndex++] << 8);
            firstCodes |= ((ulong)stream[streamIndex++] << 16);
            firstCodes |= ((ulong)stream[streamIndex++] << 24);
            firstCodes |= ((ulong)stream[streamIndex++] << 32);
            firstCodes |= ((ulong)stream[streamIndex++] << 40);

            streamIndex = ExtractGradient(_secondGradient, stream, streamIndex);
            ulong secondCodes = stream[streamIndex++];
            secondCodes |= ((ulong)stream[streamIndex++] << 8);
            secondCodes |= ((ulong)stream[streamIndex++] << 16);
            secondCodes |= ((ulong)stream[streamIndex++] << 24);
            secondCodes |= ((ulong)stream[streamIndex++] << 32);
            secondCodes |= ((ulong)stream[streamIndex++] << 40);

            for (int alphaShift = 0; alphaShift < 48; alphaShift += 12)
            {
                for (int j = 0; j < 4; j++)
                {
                    // 3 bits determine alpha index to use
                    byte firstIndex = (byte)((firstCodes >> (alphaShift + 3 * j)) & 0x07);
                    byte secondIndex = (byte)((secondCodes >> (alphaShift + 3 * j)) & 0x07);
                    data[dataIndex++] = 0; // skip blue
                    data[dataIndex++] = _secondGradient[secondIndex];
                    data[dataIndex++] = _firstGradient[firstIndex];
                }
                dataIndex += PixelDepthBytes * (stride - DivSize);
            }

            return streamIndex;
        }

        internal static int ExtractGradient(Span<byte> gradient, Span<byte> stream, int bIndex)
        {
            byte endpoint0;
            byte endpoint1;
            gradient[0] = endpoint0 = stream[bIndex++];
            gradient[1] = endpoint1 = stream[bIndex++];

            if (endpoint0 > endpoint1)
            {
                for (int i = 1; i < 7; i++)
                    gradient[1 + i] = (byte)(((7 - i) * endpoint0 + i * endpoint1) / 7);
            }
            else
            {
                for (int i = 1; i < 5; ++i)
                    gradient[1 + i] = (byte)(((5 - i) * endpoint0 + i * endpoint1) / 5);
                gradient[6] = 0;
                gradient[7] = 255;
            }
            return bIndex;
        }
    }
}

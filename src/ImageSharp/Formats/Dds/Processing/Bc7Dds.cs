using SixLabors.ImageSharp.Formats.Dds;
using SixLabors.ImageSharp.Formats.Dds.Processing;
using SixLabors.ImageSharp.Formats.Dds.Processing.Bc6hBc7;
using System;
using System.Diagnostics;

namespace Pfim.dds
{
    internal class Bc7Dds : CompressedDds
    { // Code based on commit 138efff1b9c53fd9a5dd34b8c865e8f5ae798030 2019/10/24 in DirectXTex C++ library
        private struct ModeInfo
        {
            public byte uPartitions;
            public byte uPartitionBits;
            public byte uPBits;
            public byte uRotationBits;
            public byte uIndexModeBits;
            public byte uIndexPrec;
            public byte uIndexPrec2;
            public LDRColorA RGBAPrec;
            public LDRColorA RGBAPrecWithP;

            public ModeInfo(byte uParts, byte uPartBits, byte upBits, byte uRotBits, byte uIndModeBits, byte uIndPrec, byte uIndPrec2, LDRColorA rgbaPrec, LDRColorA rgbaPrecWithP)
            {
                uPartitions = uParts;
                uPartitionBits = uPartBits;
                uPBits = upBits;
                uRotationBits = uRotBits;
                uIndexModeBits = uIndModeBits;
                uIndexPrec = uIndPrec;
                uIndexPrec2 = uIndPrec2;
                RGBAPrec = rgbaPrec;
                RGBAPrecWithP = rgbaPrecWithP;
            }
        }

        private static readonly ModeInfo[] ms_aInfo = new ModeInfo[]
        {
            new ModeInfo(2, 4, 6, 0, 0, 3, 0, new LDRColorA(4,4,4,0), new LDRColorA(5,5,5,0)),
                // Mode 0: Color only, 3 Subsets, RGBP 4441 (unique P-bit), 3-bit indecies, 16 partitions
            new ModeInfo(1, 6, 2, 0, 0, 3, 0, new LDRColorA(6,6,6,0), new LDRColorA(7,7,7,0)),
                // Mode 1: Color only, 2 Subsets, RGBP 6661 (shared P-bit), 3-bit indecies, 64 partitions
            new ModeInfo(2, 6, 0, 0, 0, 2, 0, new LDRColorA(5,5,5,0), new LDRColorA(5,5,5,0)),
                // Mode 2: Color only, 3 Subsets, RGB 555, 2-bit indecies, 64 partitions
            new ModeInfo(1, 6, 4, 0, 0, 2, 0, new LDRColorA(7,7,7,0), new LDRColorA(8,8,8,0)),
                // Mode 3: Color only, 2 Subsets, RGBP 7771 (unique P-bit), 2-bits indecies, 64 partitions
            new ModeInfo(0, 0, 0, 2, 1, 2, 3, new LDRColorA(5,5,5,6), new LDRColorA(5,5,5,6)),
                // Mode 4: Color w/ Separate Alpha, 1 Subset, RGB 555, A6, 16x2/16x3-bit indices, 2-bit rotation, 1-bit index selector
            new ModeInfo(0, 0, 0, 2, 0, 2, 2, new LDRColorA(7,7,7,8), new LDRColorA(7,7,7,8)),
                // Mode 5: Color w/ Separate Alpha, 1 Subset, RGB 777, A8, 16x2/16x2-bit indices, 2-bit rotation
            new ModeInfo(0, 0, 2, 0, 0, 4, 0, new LDRColorA(7,7,7,7), new LDRColorA(8,8,8,8)),
                // Mode 6: Color+Alpha, 1 Subset, RGBAP 77771 (unique P-bit), 16x4-bit indecies
            new ModeInfo(1, 6, 4, 0, 0, 2, 0, new LDRColorA(5,5,5,5), new LDRColorA(6,6,6,6))
                // Mode 7: Color+Alpha, 2 Subsets, RGBAP 55551 (unique P-bit), 2-bit indices, 64 partitions
        };

        private readonly byte[] currentBlock;

        public Bc7Dds(DdsHeader ddsHeader, DdsHeaderDxt10 ddsHeaderDxt10)
            : base(ddsHeader, ddsHeaderDxt10)
        {
            currentBlock = new byte[CompressedBytesPerBlock];
        }

        public override int BitsPerPixel => PixelDepthBytes * 8;
        public override ImageFormat Format => ImageFormat.Rgba32;
        protected override byte PixelDepthBytes => 4;
        protected override byte DivSize => 4;
        protected override byte CompressedBytesPerBlock => 16;

        protected override int Decode(Span<byte> stream, Span<byte> data, int streamIndex, int dataIndex, int stride)
        {
            // I would prefer to use Span, but not sure if I should reference System.Memory in this project
            // copy data instead
            Buffer.BlockCopy(stream.ToArray(), streamIndex, currentBlock, 0, currentBlock.Length);
            streamIndex += currentBlock.Length;

            uint uFirst = 0;
            while (uFirst < 128 && GetBit(ref uFirst) == 0) { }
            byte uMode = (byte)(uFirst - 1);

            if (uMode < 8)
            {
                byte uPartitions = ms_aInfo[uMode].uPartitions;
                Debug.Assert(uPartitions < Constants.BC7_MAX_REGIONS);

                var uNumEndPts = (byte)((uPartitions + 1u) << 1);
                byte uIndexPrec = ms_aInfo[uMode].uIndexPrec;
                byte uIndexPrec2 = ms_aInfo[uMode].uIndexPrec2;
                int i;
                uint uStartBit = uMode + 1u;
                int[] P = new int[6];
                byte uShape = GetBits(ref uStartBit, ms_aInfo[uMode].uPartitionBits);
                Debug.Assert(uShape < Constants.BC7_MAX_SHAPES);

                byte uRotation = GetBits(ref uStartBit, ms_aInfo[uMode].uRotationBits);
                Debug.Assert(uRotation < 4);

                byte uIndexMode = GetBits(ref uStartBit, ms_aInfo[uMode].uIndexModeBits);
                Debug.Assert(uIndexMode < 2);

                LDRColorA[] c = new LDRColorA[Constants.BC7_MAX_REGIONS << 1];
                for (i = 0; i < c.Length; ++i) c[i] = new LDRColorA();
                LDRColorA RGBAPrec = ms_aInfo[uMode].RGBAPrec;
                LDRColorA RGBAPrecWithP = ms_aInfo[uMode].RGBAPrecWithP;

                Debug.Assert(uNumEndPts <= (Constants.BC7_MAX_REGIONS << 1));

                // Red channel
                for (i = 0; i < uNumEndPts; i++)
                {
                    if (uStartBit + RGBAPrec.r > 128)
                    {
                        Debug.WriteLine("BC7: Invalid block encountered during decoding");
                        Helpers.FillWithErrorColors(data, ref dataIndex, Constants.NUM_PIXELS_PER_BLOCK, DivSize, stride);
                        return streamIndex;
                    }

                    c[i].r = GetBits(ref uStartBit, RGBAPrec.r);
                }

                // Green channel
                for (i = 0; i < uNumEndPts; i++)
                {
                    if (uStartBit + RGBAPrec.g > 128)
                    {
                        Debug.WriteLine("BC7: Invalid block encountered during decoding");
                        Helpers.FillWithErrorColors(data, ref dataIndex, Constants.NUM_PIXELS_PER_BLOCK, DivSize, stride);
                        return streamIndex;
                    }

                    c[i].g = GetBits(ref uStartBit, RGBAPrec.g);
                }

                // Blue channel
                for (i = 0; i < uNumEndPts; i++)
                {
                    if (uStartBit + RGBAPrec.b > 128)
                    {
                        Debug.WriteLine("BC7: Invalid block encountered during decoding");
                        Helpers.FillWithErrorColors(data, ref dataIndex, Constants.NUM_PIXELS_PER_BLOCK, DivSize, stride);
                        return streamIndex;
                    }

                    c[i].b = GetBits(ref uStartBit, RGBAPrec.b);
                }

                // Alpha channel
                for (i = 0; i < uNumEndPts; i++)
                {
                    if (uStartBit + RGBAPrec.a > 128)
                    {
                        Debug.WriteLine("BC7: Invalid block encountered during decoding");
                        Helpers.FillWithErrorColors(data, ref dataIndex, Constants.NUM_PIXELS_PER_BLOCK, DivSize, stride);
                        return streamIndex;
                    }

                    c[i].a = (byte)(RGBAPrec.a != 0 ? GetBits(ref uStartBit, RGBAPrec.a) : 255u);
                }

                // P-bits
                Debug.Assert(ms_aInfo[uMode].uPBits <= 6);
                for (i = 0; i < ms_aInfo[uMode].uPBits; i++)
                {
                    if (uStartBit > 127)
                    {
                        Debug.WriteLine("BC7: Invalid block encountered during decoding");
                        Helpers.FillWithErrorColors(data, ref dataIndex, Constants.NUM_PIXELS_PER_BLOCK, DivSize, stride);
                        return streamIndex;
                    }

                    P[i] = GetBit(ref uStartBit);
                }

                if (ms_aInfo[uMode].uPBits != 0)
                {
                    for (i = 0; i < uNumEndPts; i++)
                    {
                        int pi = i * ms_aInfo[uMode].uPBits / uNumEndPts;
                        for (byte ch = 0; ch < Constants.BC7_NUM_CHANNELS; ch++)
                        {
                            if (RGBAPrec[ch] != RGBAPrecWithP[ch])
                            {
                                c[i][ch] = (byte)((c[i][ch] << 1) | P[pi]);
                            }
                        }
                    }
                }

                for (i = 0; i < uNumEndPts; i++)
                {
                    c[i] = Unquantize(c[i], RGBAPrecWithP);
                }

                byte[] w1 = new byte[Constants.NUM_PIXELS_PER_BLOCK], w2 = new byte[Constants.NUM_PIXELS_PER_BLOCK];

                // read color indices
                for (i = 0; i < Constants.NUM_PIXELS_PER_BLOCK; i++)
                {
                    uint uNumBits = Helpers.IsFixUpOffset(ms_aInfo[uMode].uPartitions, uShape, i) ? uIndexPrec - 1u : uIndexPrec;
                    if (uStartBit + uNumBits > 128)
                    {
                        Debug.WriteLine("BC7: Invalid block encountered during decoding");
                        Helpers.FillWithErrorColors(data, ref dataIndex, Constants.NUM_PIXELS_PER_BLOCK, DivSize, stride);
                        return streamIndex;
                    }
                    w1[i] = GetBits(ref uStartBit, uNumBits);
                }

                // read alpha indices
                if (uIndexPrec2 != 0)
                {
                    for (i = 0; i < Constants.NUM_PIXELS_PER_BLOCK; i++)
                    {
                        uint uNumBits = (i != 0 ? uIndexPrec2 : uIndexPrec2 - 1u);
                        if (uStartBit + uNumBits > 128)
                        {
                            Debug.WriteLine("BC7: Invalid block encountered during decoding");
                            Helpers.FillWithErrorColors(data, ref dataIndex, Constants.NUM_PIXELS_PER_BLOCK, DivSize, stride);
                            return streamIndex;
                        }
                        w2[i] = GetBits(ref uStartBit, uNumBits);
                    }
                }

                for (i = 0; i < Constants.NUM_PIXELS_PER_BLOCK; ++i)
                {
                    byte uRegion = Constants.g_aPartitionTable[uPartitions][uShape][i];
                    LDRColorA outPixel = new LDRColorA();
                    if (uIndexPrec2 == 0)
                    {
                        LDRColorA.Interpolate(c[uRegion << 1], c[(uRegion << 1) + 1], w1[i], w1[i], uIndexPrec, uIndexPrec, outPixel);
                    }
                    else
                    {
                        if (uIndexMode == 0)
                        {
                            LDRColorA.Interpolate(c[uRegion << 1], c[(uRegion << 1) + 1], w1[i], w2[i], uIndexPrec, uIndexPrec2, outPixel);
                        }
                        else
                        {
                            LDRColorA.Interpolate(c[uRegion << 1], c[(uRegion << 1) + 1], w2[i], w1[i], uIndexPrec2, uIndexPrec, outPixel);
                        }
                    }

                    switch (uRotation)
                    {
                        case 1: ByteSwap(ref outPixel.r, ref outPixel.a); break;
                        case 2: ByteSwap(ref outPixel.g, ref outPixel.a); break;
                        case 3: ByteSwap(ref outPixel.b, ref outPixel.a); break;
                    }

                    // Note: whether it's sRGB is not taken into consideration
                    // we're returning data that could be either/or depending 
                    // on the input BC7 format
                    data[dataIndex++] = outPixel.b;
                    data[dataIndex++] = outPixel.g;
                    data[dataIndex++] = outPixel.r;
                    data[dataIndex++] = outPixel.a;

                    // Is mult 4?
                    if (((i + 1) & 0x3) == 0)
                        dataIndex += PixelDepthBytes * (stride - DivSize);
                }
            }
            else
            {
                Debug.WriteLine("BC7: Reserved mode 8 encountered during decoding");
                // Per the BC7 format spec, we must return transparent black
                for (int i = 0; i < Constants.NUM_PIXELS_PER_BLOCK; ++i)
                {
                    data[dataIndex++] = 0;
                    data[dataIndex++] = 0;
                    data[dataIndex++] = 0;
                    data[dataIndex++] = 0;

                    // Is mult 4?
                    if (((i + 1) & 0x3) == 0)
                        dataIndex += PixelDepthBytes * (stride - DivSize);
                }
            }

            return streamIndex;
        }

        public byte GetBit(ref uint uStartBit)
        {
            Debug.Assert(uStartBit < 128);
            uint uIndex = uStartBit >> 3;
            var ret = (byte)((currentBlock[uIndex] >> (int)(uStartBit - (uIndex << 3))) & 0x01);
            uStartBit++;
            return ret;
        }
        public byte GetBits(ref uint uStartBit, uint uNumBits)
        {
            if (uNumBits == 0) return 0;
            Debug.Assert(uStartBit + uNumBits <= 128 && uNumBits <= 8);
            byte ret;
            uint uIndex = uStartBit >> 3;
            uint uBase = uStartBit - (uIndex << 3);
            if (uBase + uNumBits > 8)
            {
                uint uFirstIndexBits = 8 - uBase;
                uint uNextIndexBits = uNumBits - uFirstIndexBits;
                ret = (byte)((uint)(currentBlock[uIndex] >> (int)uBase) | ((currentBlock[uIndex + 1] & ((1u << (int)uNextIndexBits) - 1)) << (int)uFirstIndexBits));
            }
            else
            {
                ret = (byte)((currentBlock[uIndex] >> (int)uBase) & ((1 << (int)uNumBits) - 1));
            }
            Debug.Assert(ret < (1 << (int)uNumBits));
            uStartBit += uNumBits;
            return ret;
        }

        private static byte Unquantize(byte comp, uint uPrec)
        {
            Debug.Assert(0 < uPrec && uPrec <= 8);
            comp = (byte)(comp << (int)(8u - uPrec));
            return (byte)(comp | (comp >> (int)uPrec));
        }
        private static LDRColorA Unquantize(LDRColorA c, LDRColorA RGBAPrec)
        {
            LDRColorA q = new LDRColorA();
            q.r = Unquantize(c.r, RGBAPrec.r);
            q.g = Unquantize(c.g, RGBAPrec.g);
            q.b = Unquantize(c.b, RGBAPrec.b);
            q.a = RGBAPrec.a > 0 ? Unquantize(c.a, RGBAPrec.a) : (byte)255u;
            return q;
        }

        private void ByteSwap(ref byte a, ref byte b)
        {
            byte tmp = a;
            a = b;
            b = tmp;
        }
    }
}

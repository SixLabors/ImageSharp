// <copyright file="JpegEncoderCore.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

using System.Numerics;

namespace ImageProcessorCore.Formats
{
    using System;
    using System.IO;

    internal class JpegEncoderCore
    {
        /// <summary>
        /// Maps from the zig-zag ordering to the natural ordering. For example,
        /// unzig[3] is the column and row of the fourth element in zig-zag order. The
        /// value is 16, which means first column (16%8 == 0) and third row (16/8 == 2).
        /// </summary>
        private static readonly int[] Unzig =
        {
            0, 1, 8, 16, 9, 2, 3, 10, 17, 24, 32, 25, 18, 11, 4, 5, 12, 19, 26,
            33, 40, 48, 41, 34, 27, 20, 13, 6, 7, 14, 21, 28, 35, 42, 49, 56, 57,
            50, 43, 36, 29, 22, 15, 23, 30, 37, 44, 51, 58, 59, 52, 45, 38, 31,
            39, 46, 53, 60, 61, 54, 47, 55, 62, 63,
        };

        private const int NQuantIndex = 2;

        /// <summary>
        /// Counts the number of bits needed to hold an integer.
        /// </summary>
        private readonly byte[] bitCount =
        {
            0, 1, 2, 2, 3, 3, 3, 3, 4, 4, 4, 4, 4, 4, 4, 4, 5, 5, 5, 5, 5, 5, 5, 5, 5,
            5, 5, 5, 5, 5, 5, 5, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6,
            6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7,
            7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7,
            7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7,
            7, 7, 7, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8,
            8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8,
            8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8,
            8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8,
            8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8,
            8, 8, 8, 8, 8, 8,
        };

        /// <summary>
        /// The unscaled quantization tables in zig-zag order. Each
        /// encoder copies and scales the tables according to its quality parameter.
        /// The values are derived from section K.1 after converting from natural to
        /// zig-zag order.
        /// </summary>
        private readonly byte[,] unscaledQuant = {
            {
                // Luminance.
                16, 11, 12, 14, 12, 10, 16, 14, 13, 14, 18, 17, 16, 19, 24, 40,
                26, 24, 22, 22, 24, 49, 35, 37, 29, 40, 58, 51, 61, 60, 57, 51,
                56, 55, 64, 72, 92, 78, 64, 68, 87, 69, 55, 56, 80, 109, 81,
                87, 95, 98, 103, 104, 103, 62, 77, 113, 121, 112, 100, 120, 92,
                101, 103, 99,
            },
            {
                // Chrominance.
                17, 18, 18, 24, 21, 24, 47, 26, 26, 47, 99, 66, 56, 66, 99, 99,
                99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99,
                99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99,
                99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99,
            }
        };

        /// <summary>
        /// The Huffman encoding specifications.
        /// This encoder uses the same Huffman encoding for all images.
        /// </summary>
        private readonly HuffmanSpec[] theHuffmanSpec = {
            // Luminance DC.
            new HuffmanSpec(
                new byte[]
                {
                    0, 1, 5, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0
                },
                new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 }),
            new HuffmanSpec(
                new byte[]
                {
                    0, 2, 1, 3, 3, 2, 4, 3, 5, 5, 4, 4, 0, 0, 1, 125
                },
                new byte[]
                {
                    0x01, 0x02, 0x03, 0x00, 0x04, 0x11, 0x05, 0x12, 0x21,
                    0x31, 0x41, 0x06, 0x13, 0x51, 0x61, 0x07, 0x22, 0x71,
                    0x14, 0x32, 0x81, 0x91, 0xa1, 0x08, 0x23, 0x42, 0xb1,
                    0xc1, 0x15, 0x52, 0xd1, 0xf0, 0x24, 0x33, 0x62, 0x72,
                    0x82, 0x09, 0x0a, 0x16, 0x17, 0x18, 0x19, 0x1a, 0x25,
                    0x26, 0x27, 0x28, 0x29, 0x2a, 0x34, 0x35, 0x36, 0x37,
                    0x38, 0x39, 0x3a, 0x43, 0x44, 0x45, 0x46, 0x47, 0x48,
                    0x49, 0x4a, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58, 0x59,
                    0x5a, 0x63, 0x64, 0x65, 0x66, 0x67, 0x68, 0x69, 0x6a,
                    0x73, 0x74, 0x75, 0x76, 0x77, 0x78, 0x79, 0x7a, 0x83,
                    0x84, 0x85, 0x86, 0x87, 0x88, 0x89, 0x8a, 0x92, 0x93,
                    0x94, 0x95, 0x96, 0x97, 0x98, 0x99, 0x9a, 0xa2, 0xa3,
                    0xa4, 0xa5, 0xa6, 0xa7, 0xa8, 0xa9, 0xaa, 0xb2, 0xb3,
                    0xb4, 0xb5, 0xb6, 0xb7, 0xb8, 0xb9, 0xba, 0xc2, 0xc3,
                    0xc4, 0xc5, 0xc6, 0xc7, 0xc8, 0xc9, 0xca, 0xd2, 0xd3,
                    0xd4, 0xd5, 0xd6, 0xd7, 0xd8, 0xd9, 0xda, 0xe1, 0xe2,
                    0xe3, 0xe4, 0xe5, 0xe6, 0xe7, 0xe8, 0xe9, 0xea, 0xf1,
                    0xf2, 0xf3, 0xf4, 0xf5, 0xf6, 0xf7, 0xf8, 0xf9, 0xfa
                }),
            new HuffmanSpec(
                new byte[]
                {
                    0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0
                },
                new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 }), 

            // Chrominance AC.
            new HuffmanSpec(
                new byte[]
                {
                    0, 2, 1, 2, 4, 4, 3, 4, 7, 5, 4, 4, 0, 1, 2, 119
                },
                new byte[]
                {
                    0x00, 0x01, 0x02, 0x03, 0x11, 0x04, 0x05, 0x21, 0x31,
                    0x06, 0x12, 0x41, 0x51, 0x07, 0x61, 0x71, 0x13, 0x22,
                    0x32, 0x81, 0x08, 0x14, 0x42, 0x91, 0xa1, 0xb1, 0xc1,
                    0x09, 0x23, 0x33, 0x52, 0xf0, 0x15, 0x62, 0x72, 0xd1,
                    0x0a, 0x16, 0x24, 0x34, 0xe1, 0x25, 0xf1, 0x17, 0x18,
                    0x19, 0x1a, 0x26, 0x27, 0x28, 0x29, 0x2a, 0x35, 0x36,
                    0x37, 0x38, 0x39, 0x3a, 0x43, 0x44, 0x45, 0x46, 0x47,
                    0x48, 0x49, 0x4a, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58,
                    0x59, 0x5a, 0x63, 0x64, 0x65, 0x66, 0x67, 0x68, 0x69,
                    0x6a, 0x73, 0x74, 0x75, 0x76, 0x77, 0x78, 0x79, 0x7a,
                    0x82, 0x83, 0x84, 0x85, 0x86, 0x87, 0x88, 0x89, 0x8a,
                    0x92, 0x93, 0x94, 0x95, 0x96, 0x97, 0x98, 0x99, 0x9a,
                    0xa2, 0xa3, 0xa4, 0xa5, 0xa6, 0xa7, 0xa8, 0xa9, 0xaa,
                    0xb2, 0xb3, 0xb4, 0xb5, 0xb6, 0xb7, 0xb8, 0xb9, 0xba,
                    0xc2, 0xc3, 0xc4, 0xc5, 0xc6, 0xc7, 0xc8, 0xc9, 0xca,
                    0xd2, 0xd3, 0xd4, 0xd5, 0xd6, 0xd7, 0xd8, 0xd9, 0xda,
                    0xe2, 0xe3, 0xe4, 0xe5, 0xe6, 0xe7, 0xe8, 0xe9, 0xea,
                    0xf2, 0xf3, 0xf4, 0xf5, 0xf6, 0xf7, 0xf8, 0xf9, 0xfa,
                })
        };

        /// <summary>
        /// A compiled look-up table representation of a huffmanSpec.
        /// Each value maps to a uint32 of which the 8 most significant bits hold the
        /// codeword size in bits and the 24 least significant bits hold the codeword.
        /// The maximum codeword size is 16 bits.
        /// </summary>
        private class HuffmanLut
        {
            public readonly uint[] Values;

            public HuffmanLut(HuffmanSpec s)
            {
                int maxValue = 0;

                foreach (var v in s.Values)
                {
                    if (v > maxValue) maxValue = v;
                }

                this.Values = new uint[maxValue + 1];

                int code = 0;
                int k = 0;

                for (int i = 0; i < s.Count.Length; i++)
                {
                    int nBits = (i + 1) << 24;
                    for (int j = 0; j < s.Count[i]; j++)
                    {
                        this.Values[s.Values[k]] = (uint)(nBits | code);
                        code++;
                        k++;
                    }

                    code <<= 1;
                }
            }
        }

        // w is the writer to write to. err is the first error encountered during
        // writing. All attempted writes after the first error become no-ops.
        private Stream outputStream;

        /// <summary>
        /// A scratch buffer to reduce allocations.
        /// </summary>
        private readonly byte[] buffer = new byte[16];

        /// <summary>
        /// The accumulated bits to write to the stream.
        /// </summary>
        private uint bits;

        /// <summary>
        /// The accumulated bits to write to the stream.
        /// </summary>
        private uint nBits;

        /// <summary>
        /// The scaled quantization tables, in zig-zag order.
        /// </summary>
        private readonly byte[][] quant = new byte[NQuantIndex][]; // [Block.blockSize];

        // The compiled representations of theHuffmanSpec.
        private readonly HuffmanLut[] theHuffmanLUT = new HuffmanLut[4];

        /// <summary>
        /// The subsampling method to use.
        /// </summary>
        private JpegSubsample subsample;

        /// <summary>
        /// Writes the given byte to the stream.
        /// </summary>
        /// <param name="b"></param>
        private void WriteByte(byte b)
        {
            var data = new byte[1];
            data[0] = b;
            this.outputStream.Write(data, 0, 1);
        }

        /// <summary>
        /// Emits the least significant nBits bits of bits to the bit-stream.
        /// The precondition is bits <example>&lt; 1&lt;&lt;nBits &amp;&amp; nBits &lt;= 16</example>.
        /// </summary>
        /// <param name="bits"></param>
        /// <param name="nBits"></param>
        private void Emit(uint bits, uint nBits)
        {
            nBits += this.nBits;
            bits <<= (int)(32 - nBits);
            bits |= this.bits;
            while (nBits >= 8)
            {
                byte b = (byte)(bits >> 24);
                this.WriteByte(b);
                if (b == 0xff) this.WriteByte(0x00);
                bits <<= 8;
                nBits -= 8;
            }

            this.bits = bits;
            this.nBits = nBits;
        }

        /// <summary>
        /// Emits the given value with the given Huffman encoder.
        /// </summary>
        /// <param name="index">The index of the Huffman encoder</param>
        /// <param name="value">The value to encode.</param>
        private void EmitHuff(HuffIndex index, int value)
        {
            uint x = this.theHuffmanLUT[(int)index].Values[value];
            this.Emit(x & ((1 << 24) - 1), x >> 24);
        }

        /// <summary>
        /// Emits a run of runLength copies of value encoded with the given Huffman encoder.
        /// </summary>
        /// <param name="index">The index of the Huffman encoder</param>
        /// <param name="runLength">The number of copies to encode.</param>
        /// <param name="value">The value to encode.</param>
        private void EmitHuffRLE(HuffIndex index, int runLength, int value)
        {
            int a = value;
            int b = value;
            if (a < 0)
            {
                a = -value;
                b = value - 1;
            }

            uint bt;
            if (a < 0x100)
            {
                bt = this.bitCount[a];
            }
            else
            {
                bt = 8 + (uint)this.bitCount[a >> 8];
            }

            this.EmitHuff(index, (int)((uint)(runLength << 4) | bt));
            if (bt > 0)
            {
                this.Emit((uint)b & (uint)((1 << ((int)bt)) - 1), bt);
            }
        }


        /// <summary>
        /// Writes a block of pixel data using the given quantization table,
        /// returning the post-quantized DC value of the DCT-transformed block. 
        /// The block is in natural (not zig-zag) order.
        /// </summary>
        /// <param name="block">The block to write.</param>
        /// <param name="index">The quantization table index.</param>
        /// <param name="prevDC">The previous DC value.</param>
        /// <returns></returns>
        private int WriteBlock(Block block, QuantIndex index, int prevDC)
        {
            FDCT.Transform(block);

            // Emit the DC delta.
            int dc = Round(block[0], 8 * this.quant[(int)index][0]);
            this.EmitHuffRLE((HuffIndex)(2 * (int)index + 0), 0, dc - prevDC);

            // Emit the AC components.
            var h = (HuffIndex)(2 * (int)index + 1);
            int runLength = 0;

            for (int zig = 1; zig < Block.BlockSize; zig++)
            {
                int ac = Round(block[Unzig[zig]], 8 * this.quant[(int)index][zig]);

                if (ac == 0)
                {
                    runLength++;
                }
                else
                {
                    while (runLength > 15)
                    {
                        this.EmitHuff(h, 0xf0);
                        runLength -= 16;
                    }

                    this.EmitHuffRLE(h, runLength, ac);
                    runLength = 0;
                }
            }

            if (runLength > 0) this.EmitHuff(h, 0x00);
            return dc;
        }

        // toYCbCr converts the 8x8 region of m whose top-left corner is p to its
        // YCbCr values.
        private void ToYCbCr<TColor, TPacked>(PixelAccessor<TColor, TPacked> pixels, int x, int y, Block yBlock, Block cbBlock, Block crBlock)
            where TColor : IPackedVector<TPacked>
            where TPacked : struct
        {
            int xmax = pixels.Width - 1;
            int ymax = pixels.Height - 1;
            for (int j = 0; j < 8; j++)
            {
                for (int i = 0; i < 8; i++)
                {
                    YCbCr color = new Color(pixels[Math.Min(x + i, xmax), Math.Min(y + j, ymax)].ToVector4());
                    int index = (8 * j) + i;
                    yBlock[index] = (int)color.Y;
                    cbBlock[index] = (int)color.Cb;
                    crBlock[index] = (int)color.Cr;
                }
            }
        }

        /// <summary>
        /// Scales the 16x16 region represented by the 4 src blocks to the 8x8
        /// dst block.
        /// </summary>
        /// <param name="destination">The destination block array</param>
        /// <param name="source">The source block array.</param>
        private void Scale16X16_8X8(Block destination, Block[] source)
        {
            for (int i = 0; i < 4; i++)
            {
                int dstOff = ((i & 2) << 4) | ((i & 1) << 2);
                for (int y = 0; y < 4; y++)
                {
                    for (int x = 0; x < 4; x++)
                    {
                        int j = 16 * y + 2 * x;
                        int sum = source[i][j] + source[i][j + 1] + source[i][j + 8] + source[i][j + 9];
                        destination[8 * y + x + dstOff] = (sum + 2) / 4;
                    }
                }
            }
        }

        // The SOS marker "\xff\xda" followed by 8 bytes:
        // - the marker length "\x00\x08",
        // - the number of components "\x01",
        // - component 1 uses DC table 0 and AC table 0 "\x01\x00",
        // - the bytes "\x00\x3f\x00". Section B.2.3 of the spec says that for
        // sequential DCTs, those bytes (8-bit Ss, 8-bit Se, 4-bit Ah, 4-bit Al)
        // should be 0x00, 0x3f, 0x00<<4 | 0x00.
        private readonly byte[] SOSHeaderY =
        {
            JpegConstants.Markers.XFF, JpegConstants.Markers.SOS,
            0x00, 0x08, // Length (high byte, low byte), must be 6 + 2 * (number of components in scan)
            0x01, // Number of components in a scan, 1
            0x01, // Component Id Y
            0x00, // DC/AC Huffman table 
            0x00, // Ss - Start of spectral selection.
            0x3f, // Se - End of spectral selection.
            0x00 // Ah + Ah (Successive approximation bit position high + low)
        };

        // The SOS marker "\xff\xda" followed by 12 bytes:
        // - the marker length "\x00\x0c",
        // - the number of components "\x03",
        // - component 1 uses DC table 0 and AC table 0 "\x01\x00",
        // - component 2 uses DC table 1 and AC table 1 "\x02\x11",
        // - component 3 uses DC table 1 and AC table 1 "\x03\x11",
        // - the bytes "\x00\x3f\x00". Section B.2.3 of the spec says that for
        // sequential DCTs, those bytes (8-bit Ss, 8-bit Se, 4-bit Ah, 4-bit Al)
        // should be 0x00, 0x3f, 0x00<<4 | 0x00.
        private readonly byte[] SOSHeaderYCbCr =
        {
            JpegConstants.Markers.XFF, JpegConstants.Markers.SOS,
            0x00, 0x0c, // Length (high byte, low byte), must be 6 + 2 * (number of components in scan)
            0x03, // Number of components in a scan, 3
            0x01, // Component Id Y
            0x00, // DC/AC Huffman table 
            0x02, // Component Id Cb
            0x11, // DC/AC Huffman table 
            0x03, // Component Id Cr
            0x11, // DC/AC Huffman table 
            0x00, // Ss - Start of spectral selection.
            0x3f, // Se - End of spectral selection.
            0x00 // Ah + Ah (Successive approximation bit position high + low)
        };

        // Encode writes the Image m to w in JPEG 4:2:0 baseline format with the given
        // options. Default parameters are used if a nil *Options is passed.
        public void Encode<TColor, TPacked>(Image<TColor, TPacked> image, Stream stream, int quality, JpegSubsample sample)
            where TColor : IPackedPixel<TPacked>
            where TPacked : struct
        {
            Guard.NotNull(image, nameof(image));
            Guard.NotNull(stream, nameof(stream));

            ushort max = JpegConstants.MaxLength;
            if (image.Width >= max || image.Height >= max)
            {
                throw new ImageFormatException($"Image is too large to encode at {image.Width}x{image.Height}.");
            }

            this.outputStream = stream;
            this.subsample = sample;

            // TODO: This should be static should it not?
            for (int i = 0; i < this.theHuffmanSpec.Length; i++)
            {
                this.theHuffmanLUT[i] = new HuffmanLut(this.theHuffmanSpec[i]);
            }

            for (int i = 0; i < NQuantIndex; i++)
            {
                this.quant[i] = new byte[Block.BlockSize];
            }

            if (quality < 1) quality = 1;
            if (quality > 100) quality = 100;

            // Convert from a quality rating to a scaling factor.
            int scale;
            if (quality < 50)
            {
                scale = 5000 / quality;
            }
            else
            {
                scale = 200 - quality * 2;
            }

            // Initialize the quantization tables.
            for (int i = 0; i < NQuantIndex; i++)
            {
                for (int j = 0; j < Block.BlockSize; j++)
                {
                    int x = this.unscaledQuant[i, j];
                    x = (x * scale + 50) / 100;
                    if (x < 1) x = 1;
                    if (x > 255) x = 255;
                    this.quant[i][j] = (byte)x;
                }
            }

            // Compute number of components based on input image type.
            int componentCount = 3;

            // Write the Start Of Image marker.
            WriteApplicationHeader((short)image.HorizontalResolution, (short)image.VerticalResolution);

            WriteProfiles(image);

            // Write the quantization tables.
            this.WriteDQT();

            // Write the image dimensions.
            this.WriteSOF0(image.Width, image.Height, componentCount);

            // Write the Huffman tables.
            this.WriteDHT(componentCount);

            // Write the image data.
            using (PixelAccessor<TColor, TPacked> pixels = image.Lock())
            {
                this.WriteSOS<TColor, TPacked>(pixels);
            }

            // Write the End Of Image marker.
            this.buffer[0] = 0xff;
            this.buffer[1] = 0xd9;
            stream.Write(this.buffer, 0, 2);
            stream.Flush();
        }

        /// <summary>
        /// Gets the quotient of the two numbers rounded to the nearest integer, instead of rounded to zero.
        /// </summary>
        /// <param name="dividend">The value to divide.</param>
        /// <param name="divisor">The value to divide by.</param>
        /// <returns>The <see cref="int"/></returns>
        private static int Round(int dividend, int divisor)
        {
            if (dividend >= 0)
            {
                return (dividend + (divisor >> 1)) / divisor;
            }

            return -((-dividend + (divisor >> 1)) / divisor);
        }

        /// <summary>
        /// Writes the application header containing the Jfif identifier plus extra data.
        /// </summary>
        /// <param name="horizontalResolution">The resolution of the image in the x- direction.</param>
        /// <param name="verticalResolution">The resolution of the image in the y- direction.</param>
        private void WriteApplicationHeader(short horizontalResolution, short verticalResolution)
        {
            // Write the start of image marker. Markers are always prefixed with with 0xff.
            this.buffer[0] = JpegConstants.Markers.XFF;
            this.buffer[1] = JpegConstants.Markers.SOI;

            // Write the JFIF headers
            this.buffer[2] = JpegConstants.Markers.XFF;
            this.buffer[3] = JpegConstants.Markers.APP0; // Application Marker
            this.buffer[4] = 0x00;
            this.buffer[5] = 0x10;
            this.buffer[6] = 0x4a; // J
            this.buffer[7] = 0x46; // F
            this.buffer[8] = 0x49; // I
            this.buffer[9] = 0x46; // F
            this.buffer[10] = 0x00; // = "JFIF",'\0'
            this.buffer[11] = 0x01; // versionhi
            this.buffer[12] = 0x01; // versionlo
            this.buffer[13] = 0x01; // xyunits as dpi

            // No thumbnail
            this.buffer[14] = 0x00; // Thumbnail width
            this.buffer[15] = 0x00; // Thumbnail height

            this.outputStream.Write(this.buffer, 0, 16);

            // Resolution. Big Endian
            this.buffer[0] = (byte)(horizontalResolution >> 8);
            this.buffer[1] = (byte)horizontalResolution;
            this.buffer[2] = (byte)(verticalResolution >> 8);
            this.buffer[3] = (byte)verticalResolution;

            this.outputStream.Write(this.buffer, 0, 4);
        }

        private void WriteProfiles<TColor, TPacked>(Image<TColor, TPacked> image)
            where TColor : IPackedPixel<TPacked>
            where TPacked : struct
        {
            WriteProfile(image.ExifProfile);
        }

        private void WriteProfile(ExifProfile exifProfile)
        {
            if (exifProfile == null)
            {
                return;
            }

            byte[] data = exifProfile.ToByteArray();
            if (data == null || data.Length == 0)
            {
                return;
            }

            if (data.Length > 65533)
            {
                throw new ImageFormatException("Exif profile size exceeds limit.");
            }

            int length = data.Length + 2;

            this.buffer[0] = JpegConstants.Markers.XFF;
            this.buffer[1] = JpegConstants.Markers.APP1; // Application Marker
            this.buffer[2] = (byte)((length >> 8) & 0xFF);
            this.buffer[3] = (byte)(length & 0xFF);

            this.outputStream.Write(this.buffer, 0, 4);
            this.outputStream.Write(data, 0, data.Length);
        }

        /// <summary>
        /// Writes the Define Quantization Marker and tables.
        /// </summary>
        private void WriteDQT()
        {
            int markerlen = 2 + NQuantIndex * (1 + Block.BlockSize);
            this.WriteMarkerHeader(JpegConstants.Markers.DQT, markerlen);
            for (int i = 0; i < NQuantIndex; i++)
            {
                this.WriteByte((byte)i);
                this.outputStream.Write(this.quant[i], 0, this.quant[i].Length);
            }
        }

        /// <summary>
        /// Writes the Start Of Frame (Baseline) marker
        /// </summary>
        /// <param name="width">The width of the image</param>
        /// <param name="height">The height of the image</param>
        /// <param name="componentCount"></param>
        private void WriteSOF0(int width, int height, int componentCount)
        {
            // "default" to 4:2:0
            byte[] subsamples = { 0x22, 0x11, 0x11 };
            byte[] chroma = { 0x00, 0x01, 0x01 };

            switch (this.subsample)
            {
                case JpegSubsample.Ratio444:
                    subsamples = new byte[] { 0x11, 0x11, 0x11 };
                    break;
                case JpegSubsample.Ratio420:
                    subsamples = new byte[] { 0x22, 0x11, 0x11 };
                    break;
            }

            // Length (high byte, low byte), 8 + components * 3.
            int markerlen = 8 + 3 * componentCount;
            this.WriteMarkerHeader(JpegConstants.Markers.SOF0, markerlen);
            this.buffer[0] = 8; // Data Precision. 8 for now, 12 and 16 bit jpegs not supported
            this.buffer[1] = (byte)(height >> 8);
            this.buffer[2] = (byte)(height & 0xff); // (2 bytes, Hi-Lo), must be > 0 if DNL not supported
            this.buffer[3] = (byte)(width >> 8);
            this.buffer[4] = (byte)(width & 0xff); // (2 bytes, Hi-Lo), must be > 0 if DNL not supported
            this.buffer[5] = (byte)componentCount; // Number of components (1 byte), usually 1 = Gray scaled, 3 = color YCbCr or YIQ, 4 = color CMYK)
            if (componentCount == 1)
            {
                this.buffer[6] = 1;

                // No subsampling for grayscale images.
                this.buffer[7] = 0x11;
                this.buffer[8] = 0x00;
            }
            else
            {
                for (int i = 0; i < componentCount; i++)
                {
                    this.buffer[3 * i + 6] = (byte)(i + 1);

                    // We use 4:2:0 chroma subsampling by default.
                    this.buffer[3 * i + 7] = subsamples[i];
                    this.buffer[3 * i + 8] = chroma[i];
                }
            }

            this.outputStream.Write(this.buffer, 0, 3 * (componentCount - 1) + 9);
        }

        /// <summary>
        /// Writes the Define Huffman Table marker and tables.
        /// </summary>
        /// <param name="nComponent">The number of components to write.</param>
        private void WriteDHT(int nComponent)
        {
            byte[] headers = { 0x00, 0x10, 0x01, 0x11 };
            int markerlen = 2;
            HuffmanSpec[] specs = this.theHuffmanSpec;

            if (nComponent == 1)
            {
                // Drop the Chrominance tables.
                specs = new[] { this.theHuffmanSpec[0], this.theHuffmanSpec[1] };
            }

            foreach (var s in specs)
            {
                markerlen += 1 + 16 + s.Values.Length;
            }

            this.WriteMarkerHeader(JpegConstants.Markers.DHT, markerlen);
            for (int i = 0; i < specs.Length; i++)
            {
                HuffmanSpec spec = specs[i];

                this.WriteByte(headers[i]);
                this.outputStream.Write(spec.Count, 0, spec.Count.Length);
                this.outputStream.Write(spec.Values, 0, spec.Values.Length);
            }
        }

        /// <summary>
        /// Writes the StartOfScan marker.
        /// </summary>
        /// <param name="pixels">The pixel accessor providing acces to the image pixels.</param>
        private void WriteSOS<TColor, TPacked>(PixelAccessor<TColor, TPacked> pixels)
            where TColor : IPackedVector<TPacked>
            where TPacked : struct
        {
            // TODO: We should allow grayscale writing.
            this.outputStream.Write(this.SOSHeaderYCbCr, 0, this.SOSHeaderYCbCr.Length);

            switch (this.subsample)
            {
                case JpegSubsample.Ratio444:
                    this.Encode444<TColor, TPacked>(pixels);
                    break;
                case JpegSubsample.Ratio420:
                    this.Encode420<TColor, TPacked>(pixels);
                    break;
            }

            // Pad the last byte with 1's.
            this.Emit(0x7f, 7);
        }



        /// <summary>
        /// Encodes the image with no subsampling.
        /// </summary>
        /// <param name="pixels">The pixel accessor providing acces to the image pixels.</param>
        private void Encode444<TColor, TPacked>(PixelAccessor<TColor, TPacked> pixels)
            where TColor : IPackedVector<TPacked>
            where TPacked : struct
        {
            Block b = new Block();
            Block cb = new Block();
            Block cr = new Block();
            int prevDCY = 0, prevDCCb = 0, prevDCCr = 0;

            for (int y = 0; y < pixels.Height; y += 8)
            {
                for (int x = 0; x < pixels.Width; x += 8)
                {
                    this.ToYCbCr<TColor, TPacked>(pixels, x, y, b, cb, cr);
                    prevDCY = this.WriteBlock(b, QuantIndex.Luminance, prevDCY);
                    prevDCCb = this.WriteBlock(cb, QuantIndex.Chrominance, prevDCCb);
                    prevDCCr = this.WriteBlock(cr, QuantIndex.Chrominance, prevDCCr);
                }
            }
        }

        /// <summary>
        /// Encodes the image with subsampling. The Cb and Cr components are each subsampled 
        /// at a factor of 2 both horizontally and vertically.
        /// </summary>
        /// <param name="pixels">The pixel accessor providing acces to the image pixels.</param>
        private void Encode420<TColor, TPacked>(PixelAccessor<TColor, TPacked> pixels)
            where TColor : IPackedVector<TPacked>
            where TPacked : struct
        {
            Block b = new Block();
            Block[] cb = new Block[4];
            Block[] cr = new Block[4];
            int prevDCY = 0, prevDCCb = 0, prevDCCr = 0;

            for (int i = 0; i < 4; i++) cb[i] = new Block();
            for (int i = 0; i < 4; i++) cr[i] = new Block();

            for (int y = 0; y < pixels.Height; y += 16)
            {
                for (int x = 0; x < pixels.Width; x += 16)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        int xOff = (i & 1) * 8;
                        int yOff = (i & 2) * 4;

                        this.ToYCbCr<TColor, TPacked>(pixels, x + xOff, y + yOff, b, cb[i], cr[i]);
                        prevDCY = this.WriteBlock(b, QuantIndex.Luminance, prevDCY);
                    }

                    this.Scale16X16_8X8(b, cb);
                    prevDCCb = this.WriteBlock(b, QuantIndex.Chrominance, prevDCCb);
                    this.Scale16X16_8X8(b, cr);
                    prevDCCr = this.WriteBlock(b, QuantIndex.Chrominance, prevDCCr);
                }
            }
        }

        /// <summary>
        /// Writes the header for a marker with the given length.
        /// </summary>
        /// <param name="marker">The marker to write.</param>
        /// <param name="length">The marker length.</param>
        private void WriteMarkerHeader(byte marker, int length)
        {
            // Markers are always prefixed with with 0xff.
            this.buffer[0] = JpegConstants.Markers.XFF;
            this.buffer[1] = marker;
            this.buffer[2] = (byte)(length >> 8);
            this.buffer[3] = (byte)(length & 0xff);
            this.outputStream.Write(this.buffer, 0, 4);
        }

        /// <summary>
        /// Enumerates the Huffman tables
        /// </summary>
        private enum HuffIndex
        {
            LuminanceDC = 0,

            LuminanceAC = 1,

            ChrominanceDC = 2,

            ChrominanceAC = 3,
        }

        /// <summary>
        /// Enumerates the quantization tables
        /// </summary>
        private enum QuantIndex
        {
            /// <summary>
            /// Luminance
            /// </summary>
            Luminance = 0,

            /// <summary>
            /// Chrominance
            /// </summary>
            Chrominance = 1,
        }

        /// <summary>
        /// The Huffman encoding specifications.
        /// </summary>
        private struct HuffmanSpec
        {
            /// <summary>
            /// Initializes a n ew instance of the <see cref="HuffmanSpec"/> struct.
            /// </summary>
            /// <param name="count">The number of codes.</param>
            /// <param name="values">The decoded values.</param>
            public HuffmanSpec(byte[] count, byte[] values)
            {
                this.Count = count;
                this.Values = values;
            }

            /// <summary>
            /// Gets count[i] - The number of codes of length i bits.
            /// </summary>
            public readonly byte[] Count;

            /// <summary>
            /// Gets value[i] - The decoded value of the i'th codeword.
            /// </summary>
            public readonly byte[] Values;
        }
    }
}

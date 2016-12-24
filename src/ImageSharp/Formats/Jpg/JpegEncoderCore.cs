// <copyright file="JpegEncoderCore.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    using System;
    using System.IO;
    using System.Numerics;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Image encoder for writing an image to a stream as a jpeg.
    /// </summary>
    internal unsafe class JpegEncoderCore
    {
        /// <summary>
        /// The number of quantization tables.
        /// </summary>
        private const int QuantizationTableCount = 2;

#pragma warning disable SA1118 // ParameterMustNotSpanMultipleLines

        /// <summary>
        /// The Huffman encoding specifications.
        /// This encoder uses the same Huffman encoding for all images.
        /// </summary>
        private static readonly HuffmanSpec[] TheHuffmanSpecs =
        {
            // Luminance DC.
            new HuffmanSpec(
                new byte[]
                {
                    0, 1, 5, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0
                },
                new byte[]
                {
                    0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11
                }),
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
                new byte[]
                {
                    0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11
                }),

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
                    0xf2, 0xf3, 0xf4, 0xf5, 0xf6, 0xf7, 0xf8, 0xf9, 0xfa
                })
        };
#pragma warning restore SA1118 // ParameterMustNotSpanMultipleLines

        /// <summary>
        /// The compiled representations of theHuffmanSpec.
        /// </summary>
        private static readonly HuffmanLut[] TheHuffmanLut = new HuffmanLut[4];

        /// <summary>
        /// Counts the number of bits needed to hold an integer.
        /// </summary>
        private static readonly uint[] BitCountLut =
            {
                0, 1, 2, 2, 3, 3, 3, 3, 4, 4, 4, 4, 4, 4, 4, 4, 5, 5, 5, 5, 5, 5,
                5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6,
                6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6,
                7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7,
                7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7,
                7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7,
                7, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8,
                8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8,
                8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8,
                8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8,
                8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8,
                8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8,
                8, 8, 8,
            };

        /// <summary>
        /// The unscaled quantization tables in zig-zag order. Each
        /// encoder copies and scales the tables according to its quality parameter.
        /// The values are derived from section K.1 after converting from natural to
        /// zig-zag order.
        /// </summary>
        private static readonly byte[,] UnscaledQuant =
            {
                    {
                        // Luminance.
                        16, 11, 12, 14, 12, 10, 16, 14, 13, 14, 18, 17, 16, 19, 24,
                        40, 26, 24, 22, 22, 24, 49, 35, 37, 29, 40, 58, 51, 61, 60,
                        57, 51, 56, 55, 64, 72, 92, 78, 64, 68, 87, 69, 55, 56, 80,
                        109, 81, 87, 95, 98, 103, 104, 103, 62, 77, 113, 121, 112,
                        100, 120, 92, 101, 103, 99,
                    },
                    {
                        // Chrominance.
                        17, 18, 18, 24, 21, 24, 47, 26, 26, 47, 99, 66, 56, 66,
                        99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99,
                        99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99,
                        99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99,
                        99, 99, 99, 99, 99, 99, 99, 99,
                    }
            };

        /// <summary>
        /// The SOS (Start Of Scan) marker "\xff\xda" followed by 12 bytes:
        /// - the marker length "\x00\x0c",
        /// - the number of components "\x03",
        /// - component 1 uses DC table 0 and AC table 0 "\x01\x00",
        /// - component 2 uses DC table 1 and AC table 1 "\x02\x11",
        /// - component 3 uses DC table 1 and AC table 1 "\x03\x11",
        /// - the bytes "\x00\x3f\x00". Section B.2.3 of the spec says that for
        /// sequential DCTs, those bytes (8-bit Ss, 8-bit Se, 4-bit Ah, 4-bit Al)
        /// should be 0x00, 0x3f, 0x00&lt;&lt;4 | 0x00.
        /// </summary>
        private static readonly byte[] SosHeaderYCbCr =
            {
                JpegConstants.Markers.XFF, JpegConstants.Markers.SOS, // Marker
                0x00, 0x0c,

                // Length (high byte, low byte), must be 6 + 2 * (number of components in scan)
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

        /// <summary>
        /// A scratch buffer to reduce allocations.
        /// </summary>
        private readonly byte[] buffer = new byte[16];

        /// <summary>
        /// A buffer for reducing the number of stream writes when emitting Huffman tables. 64 seems to be enough.
        /// </summary>
        private readonly byte[] emitBuffer = new byte[64];

        /// <summary>
        /// A buffer for reducing the number of stream writes when emitting Huffman tables. Max combined table lengths + identifier.
        /// </summary>
        private readonly byte[] huffmanBuffer = new byte[179];

        /// <summary>
        /// The scaled luminance table, in zig-zag order.
        /// </summary>
        private Block8x8F luminanceQuantTable;

        /// <summary>
        /// The scaled chrominance table, in zig-zag order.
        /// </summary>
        private Block8x8F chrominanceQuantTable;

        /// <summary>
        /// The accumulated bits to write to the stream.
        /// </summary>
        private uint accumulatedBits;

        /// <summary>
        /// The accumulated bit count.
        /// </summary>
        private uint bitCount;

        /// <summary>
        /// The output stream. All attempted writes after the first error become no-ops.
        /// </summary>
        private Stream outputStream;

        /// <summary>
        /// The subsampling method to use.
        /// </summary>
        private JpegSubsample subsample;

        /// <summary>
        /// Initializes static members of the <see cref="JpegEncoderCore"/> class.
        /// </summary>
        static JpegEncoderCore()
        {
            // Initialize the Huffman tables
            for (int i = 0; i < TheHuffmanSpecs.Length; i++)
            {
                TheHuffmanLut[i] = new HuffmanLut(TheHuffmanSpecs[i]);
            }
        }

        /// <summary>
        /// Enumerates the Huffman tables
        /// </summary>
        private enum HuffIndex
        {
            /// <summary>
            /// The DC luminance huffman table index
            /// </summary>
            LuminanceDC = 0,

            // ReSharper disable UnusedMember.Local

            /// <summary>
            /// The AC luminance huffman table index
            /// </summary>
            LuminanceAC = 1,

            /// <summary>
            /// The DC chrominance huffman table index
            /// </summary>
            ChrominanceDC = 2,

            /// <summary>
            /// The AC chrominance huffman table index
            /// </summary>
            ChrominanceAC = 3,

            // ReSharper restore UnusedMember.Local
        }

        /// <summary>
        /// Enumerates the quantization tables
        /// </summary>
        private enum QuantIndex
        {
            /// <summary>
            /// The luminance quantization table index
            /// </summary>
            Luminance = 0,

            /// <summary>
            /// The chrominance quantization table index
            /// </summary>
            Chrominance = 1,
        }

        /// <summary>
        /// Encode writes the image to the jpeg baseline format with the given options.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="image">The image to write from.</param>
        /// <param name="stream">The stream to write to.</param>
        /// <param name="quality">The quality.</param>
        /// <param name="sample">The subsampling mode.</param>
        public void Encode<TColor>(Image<TColor> image, Stream stream, int quality, JpegSubsample sample)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
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

            if (quality < 1)
            {
                quality = 1;
            }

            if (quality > 100)
            {
                quality = 100;
            }

            // Convert from a quality rating to a scaling factor.
            int scale;
            if (quality < 50)
            {
                scale = 5000 / quality;
            }
            else
            {
                scale = 200 - (quality * 2);
            }

            // Initialize the quantization tables.
            InitQuantizationTable(0, scale, ref this.luminanceQuantTable);
            InitQuantizationTable(1, scale, ref this.chrominanceQuantTable);

            // Compute number of components based on input image type.
            int componentCount = 3;

            // Write the Start Of Image marker.
            this.WriteApplicationHeader((short)image.HorizontalResolution, (short)image.VerticalResolution);

            this.WriteProfiles(image);

            // Write the quantization tables.
            this.WriteDefineQuantizationTables();

            // Write the image dimensions.
            this.WriteStartOfFrame(image.Width, image.Height, componentCount);

            // Write the Huffman tables.
            this.WriteDefineHuffmanTables(componentCount);

            // Write the image data.
            using (PixelAccessor<TColor> pixels = image.Lock())
            {
                this.WriteStartOfScan(pixels);
            }

            // Write the End Of Image marker.
            this.buffer[0] = JpegConstants.Markers.XFF;
            this.buffer[1] = JpegConstants.Markers.EOI;
            stream.Write(this.buffer, 0, 2);
            stream.Flush();
        }

        private static void InitQuantizationTable(int i, int scale, ref Block8x8F quant)
        {
            for (int j = 0; j < Block8x8F.ScalarCount; j++)
            {
                int x = UnscaledQuant[i, j];
                x = ((x * scale) + 50) / 100;
                if (x < 1)
                {
                    x = 1;
                }

                if (x > 255)
                {
                    x = 255;
                }

                quant[j] = x;
            }
        }

        /// <summary>
        /// Converts the 8x8 region of the image whose top-left corner is x,y to its YCbCr values.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="pixels">The pixel accessor.</param>
        /// <param name="x">The x-position within the image.</param>
        /// <param name="y">The y-position within the image.</param>
        /// <param name="yBlock">The luminance block.</param>
        /// <param name="cbBlock">The red chroma block.</param>
        /// <param name="crBlock">The blue chroma block.</param>
        /// <param name="rgbBytes">Temporal <see cref="PixelArea{TColor}"/> provided by the caller</param>
        private static void ToYCbCr<TColor>(
            PixelAccessor<TColor> pixels,
            int x,
            int y,
            Block8x8F* yBlock,
            Block8x8F* cbBlock,
            Block8x8F* crBlock,
            PixelArea<TColor> rgbBytes)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            float* yBlockRaw = (float*)yBlock;
            float* cbBlockRaw = (float*)cbBlock;
            float* crBlockRaw = (float*)crBlock;

            rgbBytes.Reset();
            pixels.CopyRGBBytesStretchedTo(rgbBytes, y, x);

            byte* data = (byte*)rgbBytes.DataPointer;

            for (int j = 0; j < 8; j++)
            {
                int j8 = j * 8;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 v = new Vector3(data[0], data[1], data[2]);

                    // Convert returned bytes into the YCbCr color space. Assume RGBA
                    float yy = (0.299F * v.X) + (0.587F * v.Y) + (0.114F * v.Z);
                    float cb = 128 + ((-0.168736F * v.X) - (0.331264F * v.Y) + (0.5F * v.Z));
                    float cr = 128 + ((0.5F * v.X) - (0.418688F * v.Y) - (0.081312F * v.Z));

                    int index = j8 + i;

                    yBlockRaw[index] = yy;
                    cbBlockRaw[index] = cb;
                    crBlockRaw[index] = cr;

                    data += 3;
                }
            }
        }

        /// <summary>
        /// Emits the least significant count of bits of bits to the bit-stream.
        /// The precondition is bits <example>&lt; 1&lt;&lt;nBits &amp;&amp; nBits &lt;= 16</example>.
        /// </summary>
        /// <param name="bits">The packed bits.</param>
        /// <param name="count">The number of bits</param>
        private void Emit(uint bits, uint count)
        {
            count += this.bitCount;
            bits <<= (int)(32 - count);
            bits |= this.accumulatedBits;

            // Only write if more than 8 bits.
            if (count >= 8)
            {
                // Track length
                int len = 0;
                while (count >= 8)
                {
                    byte b = (byte)(bits >> 24);
                    this.emitBuffer[len++] = b;
                    if (b == 0xff)
                    {
                        this.emitBuffer[len++] = 0x00;
                    }

                    bits <<= 8;
                    count -= 8;
                }

                if (len > 0)
                {
                    this.outputStream.Write(this.emitBuffer, 0, len);
                }
            }

            this.accumulatedBits = bits;
            this.bitCount = count;
        }

        /// <summary>
        /// Emits the given value with the given Huffman encoder.
        /// </summary>
        /// <param name="index">The index of the Huffman encoder</param>
        /// <param name="value">The value to encode.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EmitHuff(HuffIndex index, int value)
        {
            uint x = TheHuffmanLut[(int)index].Values[value];
            this.Emit(x & ((1 << 24) - 1), x >> 24);
        }

        /// <summary>
        /// Emits a run of runLength copies of value encoded with the given Huffman encoder.
        /// </summary>
        /// <param name="index">The index of the Huffman encoder</param>
        /// <param name="runLength">The number of copies to encode.</param>
        /// <param name="value">The value to encode.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
                bt = BitCountLut[a];
            }
            else
            {
                bt = 8 + BitCountLut[a >> 8];
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
        /// <param name="index">The quantization table index.</param>
        /// <param name="prevDC">The previous DC value.</param>
        /// <param name="src">Source block</param>
        /// <param name="tempDest">Temporal block to be used as FDCT Destination</param>
        /// <param name="temp2">Temporal block 2</param>
        /// <param name="quant">Quantization table</param>
        /// <param name="unzigPtr">The 8x8 Unzig block ptr</param>
        /// <returns>The <see cref="int"/></returns>
        private float WriteBlock(
            QuantIndex index,
            float prevDC,
            Block8x8F* src,
            Block8x8F* tempDest,
            Block8x8F* temp2,
            Block8x8F* quant,
            int* unzigPtr)
        {
            DCT.TransformFDCT(ref *src, ref *tempDest, ref *temp2);

            Block8x8F.UnZigDivRound(tempDest, temp2, quant, unzigPtr);

            float* d = (float*)temp2;

            // Emit the DC delta.
            float dc = d[0];

            this.EmitHuffRLE((HuffIndex)((2 * (int)index) + 0), 0, (int)(dc - prevDC));

            // Emit the AC components.
            HuffIndex h = (HuffIndex)((2 * (int)index) + 1);
            int runLength = 0;

            for (int zig = 1; zig < Block8x8F.ScalarCount; zig++)
            {
                float ac = d[zig];

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

                    this.EmitHuffRLE(h, runLength, (int)ac);
                    runLength = 0;
                }
            }

            if (runLength > 0)
            {
                this.EmitHuff(h, 0x00);
            }

            return dc;
        }

        /// <summary>
        /// Writes the application header containing the JFIF identifier plus extra data.
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

        /// <summary>
        /// Writes the metadata profiles to the image.
        /// </summary>
        /// <param name="image">The image.</param>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        private void WriteProfiles<TColor>(Image<TColor> image)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            this.WriteProfile(image.ExifProfile);
        }

        /// <summary>
        /// Writes the EXIF profile.
        /// </summary>
        /// <param name="exifProfile">The exif profile.</param>
        /// <exception cref="ImageFormatException">
        /// Thrown if the EXIF profile size exceeds the limit
        /// </exception>
        private void WriteProfile(ExifProfile exifProfile)
        {
            const int Max = 65533;
            byte[] data = exifProfile?.ToByteArray();
            if (data == null || data.Length == 0)
            {
                return;
            }

            if (data.Length > Max)
            {
                throw new ImageFormatException($"Exif profile size exceeds limit. nameof{Max}");
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
        private void WriteDefineQuantizationTables()
        {
            // Marker + quantization table lengths
            int markerlen = 2 + (QuantizationTableCount * (1 + Block8x8F.ScalarCount));
            this.WriteMarkerHeader(JpegConstants.Markers.DQT, markerlen);

            // Loop through and collect the tables as one array.
            // This allows us to reduce the number of writes to the stream.
            byte[] dqt = new byte[(QuantizationTableCount * Block8x8F.ScalarCount) + QuantizationTableCount];
            int offset = 0;

            WriteDataToDqt(dqt, ref offset, QuantIndex.Luminance, ref this.luminanceQuantTable);
            WriteDataToDqt(dqt, ref offset, QuantIndex.Chrominance, ref this.chrominanceQuantTable);

            this.outputStream.Write(dqt, 0, dqt.Length);
        }

#pragma warning disable SA1204
        private static void WriteDataToDqt(byte[] dqt, ref int offset, QuantIndex i, ref Block8x8F q)
        {
            dqt[offset++] = (byte)i;
            for (int j = 0; j < Block8x8F.ScalarCount; j++)
            {
                dqt[offset++] = (byte)q[j];
            }
        }
#pragma warning restore SA1204

        /// <summary>
        /// Writes the Start Of Frame (Baseline) marker
        /// </summary>
        /// <param name="width">The width of the image</param>
        /// <param name="height">The height of the image</param>
        /// <param name="componentCount">The number of components in a pixel</param>
        private void WriteStartOfFrame(int width, int height, int componentCount)
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
            int markerlen = 8 + (3 * componentCount);
            this.WriteMarkerHeader(JpegConstants.Markers.SOF0, markerlen);
            this.buffer[0] = 8; // Data Precision. 8 for now, 12 and 16 bit jpegs not supported
            this.buffer[1] = (byte)(height >> 8);
            this.buffer[2] = (byte)(height & 0xff); // (2 bytes, Hi-Lo), must be > 0 if DNL not supported
            this.buffer[3] = (byte)(width >> 8);
            this.buffer[4] = (byte)(width & 0xff); // (2 bytes, Hi-Lo), must be > 0 if DNL not supported
            this.buffer[5] = (byte)componentCount;

            // Number of components (1 byte), usually 1 = Gray scaled, 3 = color YCbCr or YIQ, 4 = color CMYK)
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
                    this.buffer[(3 * i) + 6] = (byte)(i + 1);

                    // We use 4:2:0 chroma subsampling by default.
                    this.buffer[(3 * i) + 7] = subsamples[i];
                    this.buffer[(3 * i) + 8] = chroma[i];
                }
            }

            this.outputStream.Write(this.buffer, 0, (3 * (componentCount - 1)) + 9);
        }

        /// <summary>
        /// Writes the Define Huffman Table marker and tables.
        /// </summary>
        /// <param name="componentCount">The number of components to write.</param>
        private void WriteDefineHuffmanTables(int componentCount)
        {
            // Table identifiers.
            byte[] headers = { 0x00, 0x10, 0x01, 0x11 };
            int markerlen = 2;
            HuffmanSpec[] specs = TheHuffmanSpecs;

            if (componentCount == 1)
            {
                // Drop the Chrominance tables.
                specs = new[] { TheHuffmanSpecs[0], TheHuffmanSpecs[1] };
            }

            foreach (HuffmanSpec s in specs)
            {
                markerlen += 1 + 16 + s.Values.Length;
            }

            this.WriteMarkerHeader(JpegConstants.Markers.DHT, markerlen);
            for (int i = 0; i < specs.Length; i++)
            {
                HuffmanSpec spec = specs[i];
                int len = 0;

                fixed (byte* huffman = this.huffmanBuffer)
                fixed (byte* count = spec.Count)
                fixed (byte* values = spec.Values)
                {
                    huffman[len++] = headers[i];

                    for (int c = 0; c < spec.Count.Length; c++)
                    {
                        huffman[len++] = count[c];
                    }

                    for (int v = 0; v < spec.Values.Length; v++)
                    {
                        huffman[len++] = values[v];
                    }
                }

                this.outputStream.Write(this.huffmanBuffer, 0, len);
            }
        }

        /// <summary>
        /// Writes the StartOfScan marker.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="pixels">
        /// The pixel accessor providing access to the image pixels.
        /// </param>
        private void WriteStartOfScan<TColor>(PixelAccessor<TColor> pixels)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            // TODO: We should allow grayscale writing.
            this.outputStream.Write(SosHeaderYCbCr, 0, SosHeaderYCbCr.Length);

            switch (this.subsample)
            {
                case JpegSubsample.Ratio444:
                    this.Encode444(pixels);
                    break;
                case JpegSubsample.Ratio420:
                    this.Encode420(pixels);
                    break;
            }

            // Pad the last byte with 1's.
            this.Emit(0x7f, 7);
        }

        /// <summary>
        /// Encodes the image with no subsampling.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="pixels">The pixel accessor providing access to the image pixels.</param>
        private void Encode444<TColor>(PixelAccessor<TColor> pixels)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            // TODO: Need a JpegEncoderCoreCore<TColor> class or struct to hold all this mess:
            Block8x8F b = default(Block8x8F);
            Block8x8F cb = default(Block8x8F);
            Block8x8F cr = default(Block8x8F);

            Block8x8F temp1 = default(Block8x8F);
            Block8x8F temp2 = default(Block8x8F);

            Block8x8F onStackLuminanceQuantTable = this.luminanceQuantTable;
            Block8x8F onStackChrominanceQuantTable = this.chrominanceQuantTable;

            UnzigData unzig = UnzigData.Create();

            // ReSharper disable once InconsistentNaming
            float prevDCY = 0, prevDCCb = 0, prevDCCr = 0;

            using (PixelArea<TColor> rgbBytes = new PixelArea<TColor>(8, 8, ComponentOrder.XYZ, true))
            {
                for (int y = 0; y < pixels.Height; y += 8)
                {
                    for (int x = 0; x < pixels.Width; x += 8)
                    {
                        ToYCbCr(pixels, x, y, &b, &cb, &cr, rgbBytes);

                        prevDCY = this.WriteBlock(
                            QuantIndex.Luminance,
                            prevDCY,
                            &b,
                            &temp1,
                            &temp2,
                            &onStackLuminanceQuantTable,
                            unzig.Data);
                        prevDCCb = this.WriteBlock(
                            QuantIndex.Chrominance,
                            prevDCCb,
                            &cb,
                            &temp1,
                            &temp2,
                            &onStackChrominanceQuantTable,
                            unzig.Data);
                        prevDCCr = this.WriteBlock(
                            QuantIndex.Chrominance,
                            prevDCCr,
                            &cr,
                            &temp1,
                            &temp2,
                            &onStackChrominanceQuantTable,
                            unzig.Data);
                    }
                }
            }
        }

#pragma warning disable SA1201 // MethodShouldNotFollowAStruct

        /// <summary>
        /// This struct belongs to Encode420. Much easeier to understand code if they are together. Why should I move it Up? :P
        /// </summary>
        private struct BlockQuad
        {
            public fixed float Data[4 * Block8x8F.ScalarCount];
        }

        /// <summary>
        /// Encodes the image with subsampling. The Cb and Cr components are each subsampled
        /// at a factor of 2 both horizontally and vertically.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="pixels">The pixel accessor providing access to the image pixels.</param>
        private void Encode420<TColor>(PixelAccessor<TColor> pixels)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            // TODO: Need a JpegEncoderCoreCore<TColor> class or struct to hold all this mess:
            Block8x8F b = default(Block8x8F);

            BlockQuad cb = default(BlockQuad);
            BlockQuad cr = default(BlockQuad);
            Block8x8F* cbPtr = (Block8x8F*)cb.Data;
            Block8x8F* crPtr = (Block8x8F*)cr.Data;

            Block8x8F temp1 = default(Block8x8F);
            Block8x8F temp2 = default(Block8x8F);

            Block8x8F onStackLuminanceQuantTable = this.luminanceQuantTable;
            Block8x8F onStackChrominanceQuantTable = this.chrominanceQuantTable;

            UnzigData unzig = UnzigData.Create();

            // ReSharper disable once InconsistentNaming
            float prevDCY = 0, prevDCCb = 0, prevDCCr = 0;

            using (var rgbBytes = new PixelArea<TColor>(8, 8, ComponentOrder.XYZ, true))
            {
                for (int y = 0; y < pixels.Height; y += 16)
                {
                    for (int x = 0; x < pixels.Width; x += 16)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            int xOff = (i & 1) * 8;
                            int yOff = (i & 2) * 4;

                            ToYCbCr(pixels, x + xOff, y + yOff, &b, cbPtr + i, crPtr + i, rgbBytes);

                            prevDCY = this.WriteBlock(
                                QuantIndex.Luminance,
                                prevDCY,
                                &b,
                                &temp1,
                                &temp2,
                                &onStackLuminanceQuantTable,
                                unzig.Data);
                        }

                        Block8x8F.Scale16X16To8X8(&b, cbPtr);
                        prevDCCb = this.WriteBlock(
                            QuantIndex.Chrominance,
                            prevDCCb,
                            &b,
                            &temp1,
                            &temp2,
                            &onStackChrominanceQuantTable,
                            unzig.Data);
                        Block8x8F.Scale16X16To8X8(&b, crPtr);
                        prevDCCr = this.WriteBlock(
                            QuantIndex.Chrominance,
                            prevDCCr,
                            &b,
                            &temp1,
                            &temp2,
                            &onStackChrominanceQuantTable,
                            unzig.Data);
                    }
                }
            }
        }

#pragma warning restore SA1201

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
        /// The Huffman encoding specifications.
        /// </summary>
        private struct HuffmanSpec
        {
            /// <summary>
            /// Gets count[i] - The number of codes of length i bits.
            /// </summary>
            public readonly byte[] Count;

            /// <summary>
            /// Gets value[i] - The decoded value of the codeword at the given index.
            /// </summary>
            public readonly byte[] Values;

            /// <summary>
            /// Initializes a new instance of the <see cref="HuffmanSpec"/> struct.
            /// </summary>
            /// <param name="count">The number of codes.</param>
            /// <param name="values">The decoded values.</param>
            public HuffmanSpec(byte[] count, byte[] values)
            {
                this.Count = count;
                this.Values = values;
            }
        }

        /// <summary>
        /// A compiled look-up table representation of a huffmanSpec.
        /// Each value maps to a uint32 of which the 8 most significant bits hold the
        /// codeword size in bits and the 24 least significant bits hold the codeword.
        /// The maximum codeword size is 16 bits.
        /// </summary>
        private class HuffmanLut
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="HuffmanLut"/> class.
            /// </summary>
            /// <param name="spec">The encoding specifications.</param>
            public HuffmanLut(HuffmanSpec spec)
            {
                int maxValue = 0;

                foreach (byte v in spec.Values)
                {
                    if (v > maxValue)
                    {
                        maxValue = v;
                    }
                }

                this.Values = new uint[maxValue + 1];

                int code = 0;
                int k = 0;

                for (int i = 0; i < spec.Count.Length; i++)
                {
                    int bits = (i + 1) << 24;
                    for (int j = 0; j < spec.Count[i]; j++)
                    {
                        this.Values[spec.Values[k]] = (uint)(bits | code);
                        code++;
                        k++;
                    }

                    code <<= 1;
                }
            }

            /// <summary>
            /// Gets the collection of huffman values.
            /// </summary>
            public uint[] Values { get; }
        }
    }
}

// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Encoder
{
    internal class HuffmanScanEncoder
    {
        /// <summary>
        /// Number of bytes cached before being written to target stream via Stream.Write(byte[], offest, count).
        /// </summary>
        /// <remarks>
        /// This is subject to change, 1024 seems to be the best value in terms of performance.
        /// <see cref="Emit(uint, uint)"/> expects it to be at least 8 (see comments in method body).
        /// </remarks>
        private const int EmitBufferSizeInBytes = 1024;

        /// <summary>
        /// A buffer for reducing the number of stream writes when emitting Huffman tables.
        /// </summary>
        private byte[] emitBuffer = new byte[EmitBufferSizeInBytes];

        /// <summary>
        /// Number of filled bytes in <see cref="emitBuffer"/> buffer
        /// </summary>
        private int emitLen = 0;

        /// <summary>
        /// Emmited bits 'micro buffer' before being transfered to the <see cref="emitBuffer"/>.
        /// </summary>
        private uint accumulatedBits;

        /// <summary>
        /// Number of jagged bits stored in <see cref="accumulatedBits"/>
        /// </summary>
        private uint bitCount;

        private Block8x8F temporalBlock1;
        private Block8x8F temporalBlock2;

        /// <summary>
        /// The output stream. All attempted writes after the first error become no-ops.
        /// </summary>
        private Stream target;

        /// <summary>
        /// Gets the counts the number of bits needed to hold an integer.
        /// </summary>
        // The C# compiler emits this as a compile-time constant embedded in the PE file.
        // This is effectively compiled down to: return new ReadOnlySpan<byte>(&data, length)
        // More details can be found: https://github.com/dotnet/roslyn/pull/24621
        private static ReadOnlySpan<byte> BitCountLut => new byte[]
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

        public HuffmanScanEncoder(Stream outputStream)
        {
            this.target = outputStream;
        }

        /// <summary>
        /// Encodes the image with no subsampling.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="pixels">The pixel accessor providing access to the image pixels.</param>
        /// <param name="luminanceQuantTable">Luminance quantization table provided by the callee</param>
        /// <param name="chrominanceQuantTable">Chrominance quantization table provided by the callee</param>
        /// <param name="cancellationToken">The token to monitor for cancellation.</param>
        public void Encode444<TPixel>(Image<TPixel> pixels, ref Block8x8F luminanceQuantTable, ref Block8x8F chrominanceQuantTable, CancellationToken cancellationToken)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var unzig = ZigZag.CreateUnzigTable();

            // ReSharper disable once InconsistentNaming
            int prevDCY = 0, prevDCCb = 0, prevDCCr = 0;

            var pixelConverter = YCbCrForwardConverter<TPixel>.Create();
            ImageFrame<TPixel> frame = pixels.Frames.RootFrame;
            Buffer2D<TPixel> pixelBuffer = frame.PixelBuffer;
            RowOctet<TPixel> currentRows = default;

            for (int y = 0; y < pixels.Height; y += 8)
            {
                cancellationToken.ThrowIfCancellationRequested();
                currentRows.Update(pixelBuffer, y);

                for (int x = 0; x < pixels.Width; x += 8)
                {
                    pixelConverter.Convert(frame, x, y, ref currentRows);

                    prevDCY = this.WriteBlock(
                        QuantIndex.Luminance,
                        prevDCY,
                        ref pixelConverter.Y,
                        ref luminanceQuantTable,
                        ref unzig);

                    prevDCCb = this.WriteBlock(
                        QuantIndex.Chrominance,
                        prevDCCb,
                        ref pixelConverter.Cb,
                        ref chrominanceQuantTable,
                        ref unzig);

                    prevDCCr = this.WriteBlock(
                        QuantIndex.Chrominance,
                        prevDCCr,
                        ref pixelConverter.Cr,
                        ref chrominanceQuantTable,
                        ref unzig);
                }
            }

            // Pad the last byte with 1's.
            this.Emit(0x7f, 7);
            this.target.Write(this.emitBuffer, 0, this.emitLen);
        }

        /// <summary>
        /// Encodes the image with subsampling. The Cb and Cr components are each subsampled
        /// at a factor of 2 both horizontally and vertically.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="pixels">The pixel accessor providing access to the image pixels.</param>
        /// <param name="luminanceQuantTable">Luminance quantization table provided by the callee</param>
        /// <param name="chrominanceQuantTable">Chrominance quantization table provided by the callee</param>
        /// <param name="cancellationToken">The token to monitor for cancellation.</param>
        public void Encode420<TPixel>(Image<TPixel> pixels, ref Block8x8F luminanceQuantTable, ref Block8x8F chrominanceQuantTable, CancellationToken cancellationToken)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            // TODO: Need a JpegScanEncoder<TPixel> class or struct that encapsulates the scan-encoding implementation. (Similar to JpegScanDecoder.)
            Block8x8F b = default;
            Span<Block8x8F> cb = stackalloc Block8x8F[4];
            Span<Block8x8F> cr = stackalloc Block8x8F[4];

            var unzig = ZigZag.CreateUnzigTable();

            var pixelConverter = YCbCrForwardConverter<TPixel>.Create();

            // ReSharper disable once InconsistentNaming
            int prevDCY = 0, prevDCCb = 0, prevDCCr = 0;
            ImageFrame<TPixel> frame = pixels.Frames.RootFrame;
            Buffer2D<TPixel> pixelBuffer = frame.PixelBuffer;
            RowOctet<TPixel> currentRows = default;

            for (int y = 0; y < pixels.Height; y += 16)
            {
                cancellationToken.ThrowIfCancellationRequested();
                for (int x = 0; x < pixels.Width; x += 16)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        int xOff = (i & 1) * 8;
                        int yOff = (i & 2) * 4;

                        currentRows.Update(pixelBuffer, y + yOff);
                        pixelConverter.Convert(frame, x + xOff, y + yOff, ref currentRows);

                        cb[i] = pixelConverter.Cb;
                        cr[i] = pixelConverter.Cr;

                        prevDCY = this.WriteBlock(
                            QuantIndex.Luminance,
                            prevDCY,
                            ref pixelConverter.Y,
                            ref luminanceQuantTable,
                            ref unzig);
                    }

                    Block8x8F.Scale16X16To8X8(ref b, cb);
                    prevDCCb = this.WriteBlock(
                        QuantIndex.Chrominance,
                        prevDCCb,
                        ref b,
                        ref chrominanceQuantTable,
                        ref unzig);

                    Block8x8F.Scale16X16To8X8(ref b, cr);
                    prevDCCr = this.WriteBlock(
                        QuantIndex.Chrominance,
                        prevDCCr,
                        ref b,
                        ref chrominanceQuantTable,
                        ref unzig);
                }
            }

            // Pad the last byte with 1's.
            this.Emit(0x7f, 7);
            this.target.Write(this.emitBuffer, 0, this.emitLen);
        }


        /// <summary>
        /// Encodes the image with no chroma, just luminance.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="pixels">The pixel accessor providing access to the image pixels.</param>
        /// <param name="luminanceQuantTable">Luminance quantization table provided by the callee</param>
        /// <param name="cancellationToken">The token to monitor for cancellation.</param>
        public void EncodeGrayscale<TPixel>(Image<TPixel> pixels, ref Block8x8F luminanceQuantTable, CancellationToken cancellationToken)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var unzig = ZigZag.CreateUnzigTable();

            // ReSharper disable once InconsistentNaming
            int prevDCY = 0;

            var pixelConverter = LuminanceForwardConverter<TPixel>.Create();
            ImageFrame<TPixel> frame = pixels.Frames.RootFrame;
            Buffer2D<TPixel> pixelBuffer = frame.PixelBuffer;
            RowOctet<TPixel> currentRows = default;

            for (int y = 0; y < pixels.Height; y += 8)
            {
                cancellationToken.ThrowIfCancellationRequested();
                currentRows.Update(pixelBuffer, y);

                for (int x = 0; x < pixels.Width; x += 8)
                {
                    pixelConverter.Convert(frame, x, y, ref currentRows);

                    prevDCY = this.WriteBlock(
                        QuantIndex.Luminance,
                        prevDCY,
                        ref pixelConverter.Y,
                        ref luminanceQuantTable,
                        ref unzig);
                }
            }

            // Pad the last byte with 1's.
            this.Emit(0x7f, 7);
            this.target.Write(this.emitBuffer, 0, this.emitLen);
        }

        /// <summary>
        /// Writes a block of pixel data using the given quantization table,
        /// returning the post-quantized DC value of the DCT-transformed block.
        /// The block is in natural (not zig-zag) order.
        /// </summary>
        /// <param name="index">The quantization table index.</param>
        /// <param name="prevDC">The previous DC value.</param>
        /// <param name="src">Source block</param>
        /// <param name="quant">Quantization table</param>
        /// <param name="unZig">The 8x8 Unzig block.</param>
        /// <returns>The <see cref="int"/>.</returns>
        private int WriteBlock(
            QuantIndex index,
            int prevDC,
            ref Block8x8F src,
            ref Block8x8F quant,
            ref ZigZag unZig)
        {
            ref Block8x8F refTemp1 = ref this.temporalBlock1;
            ref Block8x8F refTemp2 = ref this.temporalBlock2;

            FastFloatingPointDCT.TransformFDCT(ref src, ref refTemp1, ref refTemp2);

            Block8x8F.Quantize(ref refTemp1, ref refTemp2, ref quant, ref unZig);

            int dc = (int)refTemp2[0];

            // Emit the DC delta.
            this.EmitHuffRLE((HuffIndex)((2 * (int)index) + 0), 0, dc - prevDC);

            // Emit the AC components.
            var h = (HuffIndex)((2 * (int)index) + 1);
            int runLength = 0;

            for (int zig = 1; zig < Block8x8F.Size; zig++)
            {
                int ac = (int)refTemp2[zig];

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

            if (runLength > 0)
            {
                this.EmitHuff(h, 0x00);
            }

            return dc;
        }

        /// <summary>
        /// Emits the least significant count of bits to the stream write buffer.
        /// The precondition is bits
        /// <example>
        /// &lt; 1&lt;&lt;nBits &amp;&amp; nBits &lt;= 16
        /// </example>
        /// .
        /// </summary>
        /// <param name="bits">The packed bits.</param>
        /// <param name="count">The number of bits</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        private void Emit(uint bits, uint count)
        {
            count += this.bitCount;
            bits <<= (int)(32 - count);
            bits |= this.accumulatedBits;

            // Only write if more than 8 bits.
            if (count >= 8)
            {
                // Track length
                while (count >= 8)
                {
                    byte b = (byte)(bits >> 24);
                    this.emitBuffer[this.emitLen++] = b;
                    if (b == byte.MaxValue)
                    {
                        this.emitBuffer[this.emitLen++] = byte.MinValue;
                    }

                    bits <<= 8;
                    count -= 8;
                }

                // This can emit 4 times of:
                // 1 byte guaranteed
                // 1 extra byte.MinValue byte if previous one was byte.MaxValue
                // Thus writing (1 + 1) * 4 = 8 bytes max
                // So we must check if emit buffer has extra 8 bytes, if not - call stream.Write
                if (this.emitLen > EmitBufferSizeInBytes - 8)
                {
                    this.target.Write(this.emitBuffer, 0, this.emitLen);
                    this.emitLen = 0;
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
        [MethodImpl(InliningOptions.ShortMethod)]
        private void EmitHuff(HuffIndex index, int value)
        {
            uint x = HuffmanLut.TheHuffmanLut[(int)index].Values[value];
            this.Emit(x & ((1 << 24) - 1), x >> 24);
        }

        /// <summary>
        /// Emits a run of runLength copies of value encoded with the given Huffman encoder.
        /// </summary>
        /// <param name="index">The index of the Huffman encoder</param>
        /// <param name="runLength">The number of copies to encode.</param>
        /// <param name="value">The value to encode.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
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
                bt = 8 + (uint)BitCountLut[a >> 8];
            }

            this.EmitHuff(index, (int)((uint)(runLength << 4) | bt));
            if (bt > 0)
            {
                this.Emit((uint)b & (uint)((1 << ((int)bt)) - 1), bt);
            }
        }
    }
}

// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#if SUPPORTS_RUNTIME_INTRINSICS
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif
using System.Threading;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Encoder
{
    internal class HuffmanScanEncoder
    {
        /// <summary>
        /// Compiled huffman tree to encode given values.
        /// </summary>
        /// <remarks>Yields codewords by index consisting of [run length | bitsize].</remarks>
        private HuffmanLut[] huffmanTables;

        /// <summary>
        /// Number of bytes cached before being written to target stream via Stream.Write(byte[], offest, count).
        /// </summary>
        /// <remarks>
        /// This is subject to change, 1024 seems to be the best value in terms of performance.
        /// <see cref="Emit(int, int)"/> expects it to be at least 8 (see comments in method body).
        /// </remarks>
        private const int EmitBufferSizeInBytes = 1024;

        /// <summary>
        /// A buffer for reducing the number of stream writes when emitting Huffman tables.
        /// </summary>
        private readonly uint[] emitBuffer = new uint[EmitBufferSizeInBytes / 4];

        private readonly byte[] streamWriteBuffer = new byte[EmitBufferSizeInBytes * 2];

        private const int BytesPerCodingUnit = 256 * 3;

        private int emitWriteIndex = (EmitBufferSizeInBytes / 4);

        /// <summary>
        /// Emmited bits 'micro buffer' before being transfered to the <see cref="emitBuffer"/>.
        /// </summary>
        private uint accumulatedBits;

        /// <summary>
        /// Number of jagged bits stored in <see cref="accumulatedBits"/>
        /// </summary>
        private int bitCount;

        private Block8x8F temporalBlock1;
        private Block8x8F temporalBlock2;

        /// <summary>
        /// The output stream. All attempted writes after the first error become no-ops.
        /// </summary>
        private readonly Stream target;

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
            this.huffmanTables = HuffmanLut.TheHuffmanLut;

            var unzig = ZigZag.CreateUnzigTable();

            // ReSharper disable once InconsistentNaming
            int prevDCY = 0, prevDCCb = 0, prevDCCr = 0;

            ImageFrame<TPixel> frame = pixels.Frames.RootFrame;
            Buffer2D<TPixel> pixelBuffer = frame.PixelBuffer;
            RowOctet<TPixel> currentRows = default;

            var pixelConverter = new YCbCrForwardConverter444<TPixel>(frame);

            for (int y = 0; y < pixels.Height; y += 8)
            {
                cancellationToken.ThrowIfCancellationRequested();
                currentRows.Update(pixelBuffer, y);

                for (int x = 0; x < pixels.Width; x += 8)
                {
                    pixelConverter.Convert(x, y, ref currentRows);

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

                    if (this.emitWriteIndex < this.emitBuffer.Length / 2)
                    {
                        this.WriteToStream();
                    }
                }
            }

            this.EmitFinalBits();
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
            this.huffmanTables = HuffmanLut.TheHuffmanLut;

            var unzig = ZigZag.CreateUnzigTable();

            // ReSharper disable once InconsistentNaming
            int prevDCY = 0, prevDCCb = 0, prevDCCr = 0;
            ImageFrame<TPixel> frame = pixels.Frames.RootFrame;
            Buffer2D<TPixel> pixelBuffer = frame.PixelBuffer;
            RowOctet<TPixel> currentRows = default;

            var pixelConverter = new YCbCrForwardConverter420<TPixel>(frame);

            for (int y = 0; y < pixels.Height; y += 16)
            {
                cancellationToken.ThrowIfCancellationRequested();
                for (int x = 0; x < pixels.Width; x += 16)
                {
                    for (int i = 0; i < 2; i++)
                    {
                        int yOff = i * 8;
                        currentRows.Update(pixelBuffer, y + yOff);
                        pixelConverter.Convert(x, y, ref currentRows, i);

                        prevDCY = this.WriteBlock(
                            QuantIndex.Luminance,
                            prevDCY,
                            ref pixelConverter.YLeft,
                            ref luminanceQuantTable,
                            ref unzig);

                        prevDCY = this.WriteBlock(
                            QuantIndex.Luminance,
                            prevDCY,
                            ref pixelConverter.YRight,
                            ref luminanceQuantTable,
                            ref unzig);
                    }

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

            this.FlushInternalBuffer();
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
            this.huffmanTables = HuffmanLut.TheHuffmanLut;

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

            this.FlushInternalBuffer();
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

            // Emit the DC delta.
            int dc = (int)refTemp2[0];
            this.EmitDirectCurrentTerm(this.huffmanTables[2 * (int)index].Values, dc - prevDC);

            // Emit the AC components.
            int[] acHuffTable = this.huffmanTables[(2 * (int)index) + 1].Values;

            int runLength = 0;
            int lastValuableIndex = GetLastValuableElementIndex(ref refTemp2);
            for (int zig = 1; zig <= lastValuableIndex; zig++)
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
                        this.EmitHuff(acHuffTable, 0xf0);
                        runLength -= 16;
                    }

                    this.EmitHuffRLE(acHuffTable, runLength, ac);
                    runLength = 0;
                }
            }

            // if mcu block contains trailing zeros - we must write end of block (EOB) value indicating that current block is over
            // this can be done for any number of trailing zeros, even when all 63 ac values are zero
            // (Block8x8F.Size - 1) == 63 - last index of the mcu elements
            if (lastValuableIndex != Block8x8F.Size - 1)
            {
                this.EmitHuff(acHuffTable, 0x00);
            }

            return dc;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private void EmitFinalBits()
        {
            // Bytes count we want to write to the output stream
            int valuableBytesCount = (int)Numerics.DivideCeil((uint)this.bitCount, 8);

            // Padding all 4 bytes with 1's while not corrupting initial bits stored in accumulatedBits
            uint packedBytes = (this.accumulatedBits | (uint.MaxValue >> this.bitCount)) >> ((4 - valuableBytesCount) * 8);

            // 2x size due to possible stuff bytes, max out to 8
            Span<byte> tempBuffer = stackalloc byte[valuableBytesCount * 2];

            // Write bytes to temporal buffer
            int writeCount = 0;
            for (int i = 0; i < valuableBytesCount; i++)
            {
                byte value = (byte)(packedBytes >> (i * 8));
                tempBuffer[writeCount++] = value;
                if (value == 0xff)
                {
                    tempBuffer[writeCount++] = 0;
                }
            }

            // Write temporal buffer to the output stream
            this.target.Write(tempBuffer, 0, writeCount);
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
        private void Emit(uint bits, int count)
        {
            this.accumulatedBits |= bits >> this.bitCount;

            count += this.bitCount;

            if (count >= 32)
            {
                this.emitBuffer[--this.emitWriteIndex] = this.accumulatedBits;
                this.accumulatedBits = bits << (32 - this.bitCount);

                count -= 32;
            }

            this.bitCount = count;
        }

        /// <summary>
        /// Emits the given value with the given Huffman encoder.
        /// </summary>
        /// <param name="table">Compiled Huffman spec values.</param>
        /// <param name="value">The value to encode.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        private void EmitHuff(int[] table, int value)
        {
            int x = table[value];
            this.Emit((uint)x & 0xffff_ff00u, x & 0xff);
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private void EmitDirectCurrentTerm(int[] table, int value)
        {
            int a = value;
            int b = value;
            if (a < 0)
            {
                a = -value;
                b = value - 1;
            }

            int valueLen = GetHuffmanEncodingLength((uint)a);

            this.EmitHuff(table, valueLen);
            this.Emit((uint)b << (32 - valueLen), valueLen);
        }

        /// <summary>
        /// Emits a run of runLength copies of value encoded with the given Huffman encoder.
        /// </summary>
        /// <param name="table">Compiled Huffman spec values.</param>
        /// <param name="runLength">The number of copies to encode.</param>
        /// <param name="value">The value to encode.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        private void EmitHuffRLE(int[] table, int runLength, int value)
        {
            int a = value;
            int b = value;
            if (a < 0)
            {
                a = -value;
                b = value - 1;
            }

            int valueLen = GetHuffmanEncodingLength((uint)a);

            this.EmitHuff(table, (runLength << 4) | valueLen);
            this.Emit((uint)b << (32 - valueLen), valueLen);
        }

        /// <summary>
        /// Writes remaining bytes from internal buffer to the target stream.
        /// </summary>
        /// <remarks>Pads last byte with 1's if necessary</remarks>
        private void FlushInternalBuffer()
        {
            // pad last byte with 1's
            //int padBitsCount = 8 - (this.bitCount % 8);
            //if (padBitsCount != 0)
            //{
            //    this.Emit((1 << padBitsCount) - 1, padBitsCount);
            //    this.target.Write(this.emitBuffer, 0, this.emitLen);
            //}
        }

        /// <summary>
        /// Calculates how many minimum bits needed to store given value for Huffman jpeg encoding.
        /// </summary>
        /// <remarks>
        /// This is an internal operation supposed to be used only in <see cref="HuffmanScanEncoder"/> class for jpeg encoding.
        /// </remarks>
        /// <param name="value">The value.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        internal static int GetHuffmanEncodingLength(uint value)
        {
            DebugGuard.IsTrue(value <= (1 << 16), "Huffman encoder is supposed to encode a value of 16bit size max");
#if SUPPORTS_BITOPERATIONS
            // This should have been implemented as (BitOperations.Log2(value) + 1) as in non-intrinsic implementation
            // But internal log2 is implementated like this: (31 - (int)Lzcnt.LeadingZeroCount(value))

            // BitOperations.Log2 implementation also checks if input value is zero for the convention 0->0
            // Lzcnt would return 32 for input value of 0 - no need to check that with branching
            // Fallback code if Lzcnt is not supported still use if-check
            // But most modern CPUs support this instruction so this should not be a problem
            return 32 - System.Numerics.BitOperations.LeadingZeroCount(value);
#else
            // Ideally:
            // if 0 - return 0 in this case
            // else - return log2(value) + 1
            //
            // Hack based on input value constaint:
            // We know that input values are guaranteed to be maximum 16 bit large for huffman encoding
            // We can safely shift input value for one bit -> log2(value << 1)
            // Because of the 16 bit value constraint it won't overflow
            // With that input value change we no longer need to add 1 before returning
            // And this eliminates need to check if input value is zero - it is a standard convention which Log2SoftwareFallback adheres to
            return Numerics.Log2(value << 1);
#endif
        }

        /// <summary>
        /// Returns index of the last non-zero element in given mcu block.
        /// If all values of the mcu block are zero, this method might return different results depending on the runtime and hardware support.
        /// This is jpeg mcu specific code, mcu[0] stores a dc value which will be encoded outside of the loop.
        /// This method is guaranteed to return either -1 or 0 if all elements are zero.
        /// </summary>
        /// <remarks>
        /// This is an internal operation supposed to be used only in <see cref="HuffmanScanEncoder"/> class for jpeg encoding.
        /// </remarks>
        /// <param name="mcu">Mcu block.</param>
        /// <returns>Index of the last non-zero element.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        internal static int GetLastValuableElementIndex(ref Block8x8F mcu)
        {
#if SUPPORTS_RUNTIME_INTRINSICS
            if (Avx2.IsSupported)
            {
                const int equalityMask = unchecked((int)0b1111_1111_1111_1111_1111_1111_1111_1111);

                Vector256<int> zero8 = Vector256<int>.Zero;

                ref Vector256<float> mcuStride = ref mcu.V0;

                for (int i = 7; i >= 0; i--)
                {
                    int areEqual = Avx2.MoveMask(Avx2.CompareEqual(Avx.ConvertToVector256Int32(Unsafe.Add(ref mcuStride, i)), zero8).AsByte());

                    // we do not know for sure if this stride contain all non-zero elements or if it has some trailing zeros
                    if (areEqual != equalityMask)
                    {
                        // last index in the stride, we go from the end to the start of the stride
                        int startIndex = i * 8;
                        int index = startIndex + 7;
                        ref float elemRef = ref Unsafe.As<Block8x8F, float>(ref mcu);
                        while (index >= startIndex && (int)Unsafe.Add(ref elemRef, index) == 0)
                        {
                            index--;
                        }

                        // this implementation will return -1 if all ac components are zero and dc are zero
                        return index;
                    }
                }

                return -1;
            }
            else
#endif
            {
                int index = Block8x8F.Size - 1;
                ref float elemRef = ref Unsafe.As<Block8x8F, float>(ref mcu);

                while (index > 0 && (int)Unsafe.Add(ref elemRef, index) == 0)
                {
                    index--;
                }

                // this implementation will return 0 if all ac components and dc are zero
                return index;
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private void WriteToStream()
        {
            Span<byte> emitBytes = MemoryMarshal.AsBytes(this.emitBuffer.AsSpan());

            int writeIdx = 0;
            int start = emitBytes.Length - 1;
            int end = (this.emitWriteIndex * 4) - 1;
            for (int i = start; i > end; i--)
            {
                byte value = emitBytes[i];
                this.streamWriteBuffer[writeIdx++] = value;
                if (value == 0xff)
                {
                    this.streamWriteBuffer[writeIdx++] = 0x00;
                }
            }

            this.target.Write(this.streamWriteBuffer, 0, writeIdx);
            this.emitWriteIndex = this.emitBuffer.Length;
        }
    }
}

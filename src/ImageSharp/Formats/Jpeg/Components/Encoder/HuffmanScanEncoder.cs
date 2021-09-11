// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Encoder
{
    internal class HuffmanScanEncoder
    {
        /// <summary>
        /// Maximum number of bytes encoded jpeg 8x8 block can occupy.
        /// It's highly unlikely for block to occupy this much space - it's a theoretical limit.
        /// </summary>
        /// <remarks>
        /// Where 16 is maximum huffman code binary length according to itu
        /// specs. 10 is maximum value binary length, value comes from discrete
        /// cosine tranform with value range: [-1024..1023]. Block stores
        /// 8x8 = 64 values thus multiplication by 64. Then divided by 8 to get
        /// the number of bytes. This value is then multiplied by
        /// <see cref="MaxBytesPerBlockMultiplier"/> for performance reasons.
        /// </remarks>
        private const int MaxBytesPerBlock = (16 + 10) * 64 / 8 * MaxBytesPerBlockMultiplier;

        /// <summary>
        /// Multiplier used within cache buffers size calculation.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Theoretically, <see cref="MaxBytesPerBlock"/> bytes buffer can fit
        /// exactly one minimal coding unit. In reality, coding blocks occupy much
        /// less space than the theoretical maximum - this can be exploited.
        /// If temporal buffer size is multiplied by at least 2, second half of
        /// the resulting buffer will be used as an overflow 'guard' if next
        /// block would occupy maximum number of bytes. While first half may fit
        /// many blocks before needing to flush.
        /// </para>
        /// <para>
        /// This is subject to change. This can be equal to 1 but recomended
        /// value is 2 or even greater - futher benchmarking needed.
        /// </para>
        /// </remarks>
        private const int MaxBytesPerBlockMultiplier = 2;

        /// <summary>
        /// <see cref="streamWriteBuffer"/> size multiplier.
        /// </summary>
        /// <remarks>
        /// Jpeg specification requiers to insert 'stuff' bytes after each
        /// 0xff byte value. Worst case scenarion is when all bytes are 0xff.
        /// While it's highly unlikely (if not impossible) to get such
        /// combination, it's theoretically possible so buffer size must be guarded.
        /// </remarks>
        private const int OutputBufferLengthMultiplier = 2;

        /// <summary>
        /// Compiled huffman tree to encode given values.
        /// </summary>
        /// <remarks>Yields codewords by index consisting of [run length | bitsize].</remarks>
        private HuffmanLut[] huffmanTables;

        /// <summary>
        /// Buffer for temporal storage of huffman rle encoding bit data.
        /// </summary>
        /// <remarks>
        /// Encoding bits are assembled to 4 byte unsigned integers and then copied to this buffer.
        /// This process does NOT include inserting stuff bytes.
        /// </remarks>
        private readonly uint[] emitBuffer;

        /// <summary>
        /// Buffer for temporal storage which is then written to the output stream.
        /// </summary>
        /// <remarks>
        /// Encoding bits from <see cref="emitBuffer"/> are copied to this byte buffer including stuff bytes.
        /// </remarks>
        private readonly byte[] streamWriteBuffer;

        private int emitWriteIndex;

        /// <summary>
        /// Emitted bits 'micro buffer' before being transferred to the <see cref="emitBuffer"/>.
        /// </summary>
        private uint accumulatedBits;

        /// <summary>
        /// Number of jagged bits stored in <see cref="accumulatedBits"/>
        /// </summary>
        private int bitCount;

        private Block8x8 tempBlock;

        /// <summary>
        /// The output stream. All attempted writes after the first error become no-ops.
        /// </summary>
        private readonly Stream target;

        public HuffmanScanEncoder(int componentCount, Stream outputStream)
        {
            int emitBufferByteLength = MaxBytesPerBlock * componentCount;
            this.emitBuffer = new uint[emitBufferByteLength / sizeof(uint)];
            this.emitWriteIndex = this.emitBuffer.Length;

            this.streamWriteBuffer = new byte[emitBufferByteLength * OutputBufferLengthMultiplier];

            this.target = outputStream;
        }

        private bool IsFlushNeeded
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this.emitWriteIndex < this.emitBuffer.Length / 2;
        }

        /// <summary>
        /// Encodes the image with no subsampling.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="pixels">The pixel accessor providing access to the image pixels.</param>
        /// <param name="luminanceQuantTable">Luminance quantization table provided by the callee.</param>
        /// <param name="chrominanceQuantTable">Chrominance quantization table provided by the callee.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation.</param>
        public void Encode444<TPixel>(Image<TPixel> pixels, ref Block8x8F luminanceQuantTable, ref Block8x8F chrominanceQuantTable, CancellationToken cancellationToken)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            // Calculate reciprocal quantization tables for FDCT method
            for (int i = 0; i < Block8x8F.Size; i++)
            {
                luminanceQuantTable[i] = FastFloatingPointDCT.DctReciprocalAdjustmentCoefficients[i] / luminanceQuantTable[i];
                chrominanceQuantTable[i] = FastFloatingPointDCT.DctReciprocalAdjustmentCoefficients[i] / chrominanceQuantTable[i];
            }

            this.huffmanTables = HuffmanLut.TheHuffmanLut;

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
                        ref luminanceQuantTable);

                    prevDCCb = this.WriteBlock(
                        QuantIndex.Chrominance,
                        prevDCCb,
                        ref pixelConverter.Cb,
                        ref chrominanceQuantTable);

                    prevDCCr = this.WriteBlock(
                        QuantIndex.Chrominance,
                        prevDCCr,
                        ref pixelConverter.Cr,
                        ref chrominanceQuantTable);

                    if (this.IsFlushNeeded)
                    {
                        this.FlushToStream();
                    }
                }
            }

            this.FlushRemainingBytes();
        }

        /// <summary>
        /// Encodes the image with subsampling. The Cb and Cr components are each subsampled
        /// at a factor of 2 both horizontally and vertically.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="pixels">The pixel accessor providing access to the image pixels.</param>
        /// <param name="luminanceQuantTable">Luminance quantization table provided by the callee.</param>
        /// <param name="chrominanceQuantTable">Chrominance quantization table provided by the callee.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation.</param>
        public void Encode420<TPixel>(Image<TPixel> pixels, ref Block8x8F luminanceQuantTable, ref Block8x8F chrominanceQuantTable, CancellationToken cancellationToken)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            // Calculate reciprocal quantization tables for FDCT method
            for (int i = 0; i < Block8x8F.Size; i++)
            {
                luminanceQuantTable[i] = FastFloatingPointDCT.DctReciprocalAdjustmentCoefficients[i] / luminanceQuantTable[i];
                chrominanceQuantTable[i] = FastFloatingPointDCT.DctReciprocalAdjustmentCoefficients[i] / chrominanceQuantTable[i];
            }

            this.huffmanTables = HuffmanLut.TheHuffmanLut;

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
                            ref luminanceQuantTable);

                        prevDCY = this.WriteBlock(
                            QuantIndex.Luminance,
                            prevDCY,
                            ref pixelConverter.YRight,
                            ref luminanceQuantTable);
                    }

                    prevDCCb = this.WriteBlock(
                        QuantIndex.Chrominance,
                        prevDCCb,
                        ref pixelConverter.Cb,
                        ref chrominanceQuantTable);

                    prevDCCr = this.WriteBlock(
                        QuantIndex.Chrominance,
                        prevDCCr,
                        ref pixelConverter.Cr,
                        ref chrominanceQuantTable);

                    if (this.IsFlushNeeded)
                    {
                        this.FlushToStream();
                    }
                }
            }

            this.FlushRemainingBytes();
        }

        /// <summary>
        /// Encodes the image with no chroma, just luminance.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="pixels">The pixel accessor providing access to the image pixels.</param>
        /// <param name="luminanceQuantTable">Luminance quantization table provided by the callee.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation.</param>
        public void EncodeGrayscale<TPixel>(Image<TPixel> pixels, ref Block8x8F luminanceQuantTable, CancellationToken cancellationToken)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            // Calculate reciprocal quantization tables for FDCT method
            for (int i = 0; i < Block8x8F.Size; i++)
            {
                luminanceQuantTable[i] = FastFloatingPointDCT.DctReciprocalAdjustmentCoefficients[i] / luminanceQuantTable[i];
            }

            this.huffmanTables = HuffmanLut.TheHuffmanLut;

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
                        ref luminanceQuantTable);

                    if (this.IsFlushNeeded)
                    {
                        this.FlushToStream();
                    }
                }
            }

            this.FlushRemainingBytes();
        }

        /// <summary>
        /// Encodes the image with no subsampling and keeps the pixel data as Rgb24.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="pixels">The pixel accessor providing access to the image pixels.</param>
        /// <param name="luminanceQuantTable">Luminance quantization table provided by the callee.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation.</param>
        public void EncodeRgb<TPixel>(Image<TPixel> pixels, ref Block8x8F luminanceQuantTable, CancellationToken cancellationToken)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            // Calculate reciprocal quantization tables for FDCT method
            for (int i = 0; i < Block8x8F.Size; i++)
            {
                luminanceQuantTable[i] = FastFloatingPointDCT.DctReciprocalAdjustmentCoefficients[i] / luminanceQuantTable[i];
            }

            this.huffmanTables = HuffmanLut.TheHuffmanLut;

            // ReSharper disable once InconsistentNaming
            int prevDCR = 0, prevDCG = 0, prevDCB = 0;

            ImageFrame<TPixel> frame = pixels.Frames.RootFrame;
            Buffer2D<TPixel> pixelBuffer = frame.PixelBuffer;
            RowOctet<TPixel> currentRows = default;

            var pixelConverter = new RgbForwardConverter<TPixel>(frame);

            for (int y = 0; y < pixels.Height; y += 8)
            {
                cancellationToken.ThrowIfCancellationRequested();
                currentRows.Update(pixelBuffer, y);

                for (int x = 0; x < pixels.Width; x += 8)
                {
                    pixelConverter.Convert(x, y, ref currentRows);

                    prevDCR = this.WriteBlock(
                        QuantIndex.Luminance,
                        prevDCR,
                        ref pixelConverter.R,
                        ref luminanceQuantTable);

                    prevDCG = this.WriteBlock(
                        QuantIndex.Luminance,
                        prevDCG,
                        ref pixelConverter.G,
                        ref luminanceQuantTable);

                    prevDCB = this.WriteBlock(
                        QuantIndex.Luminance,
                        prevDCB,
                        ref pixelConverter.B,
                        ref luminanceQuantTable);

                    if (this.IsFlushNeeded)
                    {
                        this.FlushToStream();
                    }
                }
            }

            this.FlushRemainingBytes();
        }

        /// <summary>
        /// Writes a block of pixel data using the given quantization table,
        /// returning the post-quantized DC value of the DCT-transformed block.
        /// The block is in natural (not zig-zag) order.
        /// </summary>
        /// <param name="index">The quantization table index.</param>
        /// <param name="prevDC">The previous DC value.</param>
        /// <param name="block">Source block.</param>
        /// <param name="quant">Quantization table.</param>
        /// <returns>The <see cref="int"/>.</returns>
        private int WriteBlock(
            QuantIndex index,
            int prevDC,
            ref Block8x8F block,
            ref Block8x8F quant)
        {
            ref Block8x8 spectralBlock = ref this.tempBlock;

            // Shifting level from 0..255 to -128..127
            block.AddInPlace(-128f);

            // Discrete cosine transform
            FastFloatingPointDCT.TransformFDCT(ref block);

            // Quantization
            Block8x8F.Quantize(ref block, ref spectralBlock, ref quant);

            // Emit the DC delta.
            int dc = spectralBlock[0];
            this.EmitHuffRLE(this.huffmanTables[2 * (int)index].Values, 0, dc - prevDC);

            // Emit the AC components.
            int[] acHuffTable = this.huffmanTables[(2 * (int)index) + 1].Values;

            int lastValuableIndex = spectralBlock.GetLastNonZeroIndex();

            int runLength = 0;
            for (int zig = 1; zig <= lastValuableIndex; zig++)
            {
                const int zeroRun1 = 1 << 4;
                const int zeroRun16 = 16 << 4;

                int ac = spectralBlock[zig];
                if (ac == 0)
                {
                    runLength += zeroRun1;
                }
                else
                {
                    while (runLength >= zeroRun16)
                    {
                        this.EmitHuff(acHuffTable, 0xf0);
                        runLength -= zeroRun16;
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
        private void FlushRemainingBytes()
        {
            // Bytes count we want to write to the output stream
            int valuableBytesCount = (int)Numerics.DivideCeil((uint)this.bitCount, 8);

            // Padding all 4 bytes with 1's while not corrupting initial bits stored in accumulatedBits
            uint packedBytes = this.accumulatedBits | (uint.MaxValue >> this.bitCount);

            int writeIndex = this.emitWriteIndex;
            this.emitBuffer[writeIndex - 1] = packedBytes;

            this.FlushToStream((writeIndex * 4) - valuableBytesCount);
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

        /// <summary>
        /// Emits given value via huffman rle encoding.
        /// </summary>
        /// <param name="table">Compiled Huffman spec values.</param>
        /// <param name="runLength">The number of preceding zeroes, preshifted by 4 to the left.</param>
        /// <param name="value">The value to encode.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        private void EmitHuffRLE(int[] table, int runLength, int value)
        {
            DebugGuard.IsTrue((runLength & 0xf) == 0, $"{nameof(runLength)} parameter must be shifted to the left by 4 bits");

            int a = value;
            int b = value;
            if (a < 0)
            {
                a = -value;
                b = value - 1;
            }

            int valueLen = GetHuffmanEncodingLength((uint)a);

            // Huffman prefix code
            int huffPackage = table[runLength | valueLen];
            int prefixLen = huffPackage & 0xff;
            uint prefix = (uint)huffPackage & 0xffff_0000u;

            // Actual encoded value
            uint encodedValue = (uint)b << (32 - valueLen);

            // Doing two binary shifts to get rid of leading 1's in negative value case
            this.Emit(prefix | (encodedValue >> prefixLen), prefixLen + valueLen);
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
            // But internal log2 is implemented like this: (31 - (int)Lzcnt.LeadingZeroCount(value))

            // BitOperations.Log2 implementation also checks if input value is zero for the convention 0->0
            // Lzcnt would return 32 for input value of 0 - no need to check that with branching
            // Fallback code if Lzcnt is not supported still use if-check
            // But most modern CPUs support this instruction so this should not be a problem
            return 32 - BitOperations.LeadingZeroCount(value);
#else
            // Ideally:
            // if 0 - return 0 in this case
            // else - return log2(value) + 1
            //
            // Hack based on input value constraint:
            // We know that input values are guaranteed to be maximum 16 bit large for huffman encoding
            // We can safely shift input value for one bit -> log2(value << 1)
            // Because of the 16 bit value constraint it won't overflow
            // With that input value change we no longer need to add 1 before returning
            // And this eliminates need to check if input value is zero - it is a standard convention which Log2SoftwareFallback adheres to
            return Numerics.Log2(value << 1);
#endif
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private void FlushToStream() => this.FlushToStream(this.emitWriteIndex * 4);

        [MethodImpl(InliningOptions.ShortMethod)]
        private void FlushToStream(int endIndex)
        {
            Span<byte> emitBytes = MemoryMarshal.AsBytes(this.emitBuffer.AsSpan());

            int writeIdx = 0;
            int startIndex = emitBytes.Length - 1;
            for (int i = startIndex; i >= endIndex; i--)
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

// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using SixLabors.ImageSharp.Formats.Jpeg.Common;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jpeg.PdfJsPort.Components
{
    internal class ScanDecoder
    {
        public const int FastBits = 9;

        // bmask[n] = (1 << n) - 1
        private static readonly uint[] Bmask = { 0, 1, 3, 7, 15, 31, 63, 127, 255, 511, 1023, 2047, 4095, 8191, 16383, 32767, 65535 };

        // bias[n] = (-1 << n) + 1
        private static readonly int[] Bias = { 0, -1, -3, -7, -15, -31, -63, -127, -255, -511, -1023, -2047, -4095, -8191, -16383, -32767 };

        private readonly DoubleBufferedStreamReader stream;
        private readonly PdfJsFrameComponent[] components;
        private readonly ZigZag dctZigZag;
        private int codeBits;
        private uint codeBuffer;
        private bool nomore;
        private byte marker;

        private int todo;
        private int restartInterval;
        private int componentIndex;
        private int componentsLength;
        private int eobrun;
        private int spectralStart;
        private int spectralEnd;
        private int successiveHigh;
        private int successiveLow;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScanDecoder"/> class.
        /// </summary>
        /// <param name="stream">The input stream</param>
        /// <param name="components">The scan components</param>
        /// <param name="componentIndex">The component index within the array</param>
        /// <param name="componentsLength">The length of the components. Different to the array length</param>
        /// <param name="restartInterval">The reset interval</param>
        /// <param name="spectralStart">The spectral selection start</param>
        /// <param name="spectralEnd">The spectral selection end</param>
        /// <param name="successiveHigh">The successive approximation bit high end</param>
        /// <param name="successiveLow">The successive approximation bit low end</param>
        public ScanDecoder(
            DoubleBufferedStreamReader stream,
            PdfJsFrameComponent[] components,
            int componentIndex,
            int componentsLength,
            int restartInterval,
            int spectralStart,
            int spectralEnd,
            int successiveHigh,
            int successiveLow)
        {
            this.dctZigZag = ZigZag.CreateUnzigTable();
            this.stream = stream;
            this.components = components;
            this.marker = PdfJsJpegConstants.Markers.Prefix;
            this.componentIndex = componentIndex;
            this.componentsLength = componentsLength;
            this.restartInterval = restartInterval;
            this.spectralStart = spectralStart;
            this.spectralEnd = spectralEnd;
            this.successiveHigh = successiveHigh;
            this.successiveLow = successiveLow;
        }

        /// <summary>
        /// Decodes the entropy coded data.
        /// </summary>
        /// <param name="frame">The image frame.</param>
        /// <param name="dcHuffmanTables">The DC Huffman tables.</param>
        /// <param name="acHuffmanTables">The AC Huffman tables.</param>
        /// <param name="fastACTables">The fast AC decoding tables.</param>
        /// <returns>The <see cref="int"/></returns>
        public int ParseEntropyCodedData(
            PdfJsFrame frame,
            PdfJsHuffmanTables dcHuffmanTables,
            PdfJsHuffmanTables acHuffmanTables,
            FastACTables fastACTables)
        {
            this.Reset();

            if (!frame.Progressive)
            {
                if (this.componentsLength == 1)
                {
                    PdfJsFrameComponent component = this.components[this.componentIndex];

                    // Non-interleaved data, we just need to process one block at a time,
                    // in trivial scanline order
                    // number of blocks to do just depends on how many actual "pixels" this
                    // component has, independent of interleaved MCU blocking and such
                    int w = component.WidthInBlocks;
                    int h = component.HeightInBlocks;
                    ref short blockDataRef = ref MemoryMarshal.GetReference(MemoryMarshal.Cast<Block8x8, short>(component.SpectralBlocks.Span));
                    ref PdfJsHuffmanTable dcHuffmanTable = ref dcHuffmanTables[component.DCHuffmanTableId];
                    ref PdfJsHuffmanTable acHuffmanTable = ref acHuffmanTables[component.ACHuffmanTableId];
                    Span<short> fastAC = fastACTables.Tables.GetRowSpan(component.ACHuffmanTableId);

                    int mcu = 0;
                    for (int j = 0; j < h; j++)
                    {
                        for (int i = 0; i < w; i++)
                        {
                            int blockRow = mcu / w;
                            int blockCol = mcu % w;
                            int offset = component.GetBlockBufferOffset(blockRow, blockCol);
                            this.DecodeBlock(component, ref Unsafe.Add(ref blockDataRef, offset), ref dcHuffmanTable, ref acHuffmanTable, fastAC);
                            mcu++;

                            // Every data block is an MCU, so countdown the restart interval
                            if (this.todo-- <= 0)
                            {
                                if (this.codeBits < 24)
                                {
                                    this.GrowBufferUnsafe();
                                }

                                // If it's NOT a restart, then just bail, so we get corrupt data
                                // rather than no data
                                if (!this.IsRestartMarker(this.marker))
                                {
                                    return 1;
                                }

                                this.Reset();
                            }
                        }
                    }
                }
                else
                {
                    // Interleaved
                    int mcu = 0;
                    int mcusPerColumn = frame.McusPerColumn;
                    int mcusPerLine = frame.McusPerLine;
                    for (int j = 0; j < mcusPerColumn; j++)
                    {
                        for (int i = 0; i < mcusPerLine; i++)
                        {
                            try
                            {
                                // Scan an interleaved mcu... process components in order
                                for (int k = 0; k < this.componentsLength; k++)
                                {
                                    PdfJsFrameComponent component = this.components[k];
                                    ref short blockDataRef = ref MemoryMarshal.GetReference(MemoryMarshal.Cast<Block8x8, short>(component.SpectralBlocks.Span));
                                    ref PdfJsHuffmanTable dcHuffmanTable = ref dcHuffmanTables[component.DCHuffmanTableId];
                                    ref PdfJsHuffmanTable acHuffmanTable = ref acHuffmanTables[component.ACHuffmanTableId];
                                    Span<short> fastAC = fastACTables.Tables.GetRowSpan(component.ACHuffmanTableId);
                                    int h = component.HorizontalSamplingFactor;
                                    int v = component.VerticalSamplingFactor;

                                    // Scan out an mcu's worth of this component; that's just determined
                                    // by the basic H and V specified for the component
                                    for (int y = 0; y < v; y++)
                                    {
                                        for (int x = 0; x < h; x++)
                                        {
                                            int mcuRow = mcu / mcusPerLine;
                                            int mcuCol = mcu % mcusPerLine;
                                            int blockRow = (mcuRow * v) + y;
                                            int blockCol = (mcuCol * h) + x;
                                            int offset = component.GetBlockBufferOffset(blockRow, blockCol);
                                            this.DecodeBlock(component, ref Unsafe.Add(ref blockDataRef, offset), ref dcHuffmanTable, ref acHuffmanTable, fastAC);
                                        }
                                    }
                                }
                            }
                            catch
                            {
                                break;
                            }

                            // After all interleaved components, that's an interleaved MCU,
                            // so now count down the restart interval
                            mcu++;
                            if (this.todo-- <= 0)
                            {
                                if (this.codeBits < 24)
                                {
                                    this.GrowBufferUnsafe();
                                }

                                // If it's NOT a restart, then just bail, so we get corrupt data
                                // rather than no data
                                if (!this.IsRestartMarker(this.marker))
                                {
                                    return 1;
                                }

                                this.Reset();
                            }
                        }
                    }
                }
            }

            return 1;
        }

        private int DecodeBlock(
            PdfJsFrameComponent component,
            ref short blockDataRef,
            ref PdfJsHuffmanTable dcTable,
            ref PdfJsHuffmanTable acTable,
            Span<short> fastAc)
        {
            this.CheckBits();
            int t = this.DecodeHuffman(ref dcTable);

            if (t < 0)
            {
                throw new ImageFormatException("Bad Huffman code");
            }

            int diff = t != 0 ? this.ExtendReceive(t) : 0;
            int dc = component.DcPredictor + diff;
            component.DcPredictor = dc;
            blockDataRef = (short)dc;

            // Decode AC Components, See Jpeg Spec
            int k = 1;
            do
            {
                int zig;
                int s;

                this.CheckBits();
                int c = this.PeekBits();
                int r = fastAc[c];

                if (r != 0)
                {
                    // Fast AC path
                    k += (r >> 4) & 15; // Run
                    s = r & 15; // Combined Length
                    this.codeBuffer <<= s;
                    this.codeBits -= s;

                    // Decode into unzigzag location
                    zig = this.dctZigZag[k++];
                    Unsafe.Add(ref blockDataRef, zig) = (short)(r >> 8);
                }
                else
                {
                    int rs = this.DecodeHuffman(ref acTable);

                    if (rs < 0)
                    {
                        throw new ImageFormatException("Bad Huffman code");
                    }

                    s = rs & 15;
                    r = rs >> 4;

                    if (s == 0)
                    {
                        if (rs != 0xF0)
                        {
                            break; // End block
                        }

                        k += 16;
                    }
                    else
                    {
                        k += r;

                        // Decode into unzigzag location
                        zig = this.dctZigZag[k++];
                        Unsafe.Add(ref blockDataRef, zig) = (short)this.ExtendReceive(s);
                    }
                }
            } while (k < 64);

            return 1;
        }

        private int DecodeBlockProgressiveDC(
            PdfJsFrameComponent component,
            ref short blockDataRef,
            ref PdfJsHuffmanTable dcTable)
        {
            if (this.spectralEnd != 0)
            {
                throw new ImageFormatException("Can't merge DC and AC.");
            }

            this.CheckBits();

            if (this.successiveHigh == 0)
            {
                // First scan for DC coefficient, must be first
                int t = this.DecodeHuffman(ref dcTable);
                int diff = t > 0 ? this.ExtendReceive(t) : 0;

                int dc = component.DcPredictor + diff;
                component.DcPredictor = dc;

                blockDataRef = (short)(dc << this.successiveLow);
            }
            else
            {
                // Refinement scan for DC coefficient
                if (this.GetBit() > 0)
                {
                    blockDataRef += (short)(1 << this.successiveLow);
                }
            }

            return 1;
        }

        private int DecodeBlockProgressiveAC(
            PdfJsFrameComponent component,
            ref short blockDataRef,
            ref PdfJsHuffmanTable acTable,
            Span<short> fac)
        {
            int k;

            if (this.spectralStart == 0)
            {
                throw new ImageFormatException("Can't merge DC and AC.");
            }

            if (this.successiveHigh == 0)
            {
                int shift = this.successiveLow;

                if (this.eobrun > 0)
                {
                    this.eobrun--;
                    return 1;
                }

                k = this.spectralStart;
                do
                {
                    int zig;
                    int s;

                    this.CheckBits();
                    int c = this.PeekBits();
                    int r = fac[c];

                    if (r > 0)
                    {
                        // Fast AC path
                        k += (r >> 4) & 15; // Run
                        s = r & 15; // Combined length
                        this.codeBuffer <<= s;
                        this.codeBits -= s;

                        // Decode into unzigzag location
                        zig = this.dctZigZag[k++];
                        Unsafe.Add(ref blockDataRef, zig) = (short)((r >> 8) << shift);
                    }
                    else
                    {
                        int rs = this.DecodeHuffman(ref acTable);

                        if (rs < 0)
                        {
                            throw new ImageFormatException("Bad Huffman code.");
                        }

                        s = rs & 15;
                        r = rs >> 4;

                        if (s == 0)
                        {
                            if (r < 15)
                            {
                                this.eobrun = 1 << r;
                                if (r > 0)
                                {
                                    this.eobrun += this.GetBits(r);
                                }

                                this.eobrun--;
                                break;
                            }

                            k += 16;
                        }
                        else
                        {
                            k += r;
                            zig = this.dctZigZag[k++];
                            Unsafe.Add(ref blockDataRef, zig) = (short)(this.ExtendReceive(s) << shift);
                        }
                    }
                }
                while (k <= this.spectralEnd);
            }
            else
            {
                // Refinement scan for these AC coefficients
                short bit = (short)(1 << this.successiveLow);

                if (this.eobrun > 0)
                {
                    this.eobrun--;
                    for (k = this.spectralStart; k < this.spectralEnd; k++)
                    {
                        ref short p = ref Unsafe.Add(ref blockDataRef, this.dctZigZag[k]);
                        if (p != 0)
                        {
                            if (this.GetBit() > 0)
                            {
                                if ((p & bit) == 0)
                                {
                                    if (p > 0)
                                    {
                                        p += bit;
                                    }
                                    else
                                    {
                                        p -= bit;
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    k = this.spectralStart;
                    do
                    {
                        int rs = this.DecodeHuffman(ref acTable);
                        if (rs < 0)
                        {
                            throw new ImageFormatException("Bad Huffman code.");
                        }

                        int s = rs & 15;
                        int r = rs >> 4;

                        if (s == 0)
                        {
                            // r=15 s=0 should write 16 0s, so we just do
                            // a run of 15 0s and then write s (which is 0),
                            // so we don't have to do anything special here
                            if (r < 15)
                            {
                                this.eobrun = (1 << r) - 1;

                                if (r > 0)
                                {
                                    this.eobrun += this.GetBits(r);
                                }

                                r = 64; // Force end of block
                            }
                        }
                        else
                        {
                            if (s != 1)
                            {
                                throw new ImageFormatException("Bad Huffman code.");
                            }

                            // Sign bit
                            if (this.GetBit() > 0)
                            {
                                s = bit;
                            }
                            else
                            {
                                s -= bit;
                            }
                        }

                        // Advance by r
                        while (k <= this.spectralEnd)
                        {
                            ref short p = ref Unsafe.Add(ref blockDataRef, this.dctZigZag[k++]);
                            if (p != 0)
                            {
                                if (this.GetBit() > 0)
                                {
                                    if ((p & bit) == 0)
                                    {
                                        if (p > 0)
                                        {
                                            p += bit;
                                        }
                                        else
                                        {
                                            p -= bit;
                                        }
                                    }
                                }
                            }
                            else
                            {
                                if (r == 0)
                                {
                                    p = (short)s;
                                    break;
                                }

                                r--;
                            }
                        }
                    }
                    while (k <= this.spectralEnd);
                }
            }

            return 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetBits(int n)
        {
            if (this.codeBits < n)
            {
                this.GrowBufferUnsafe();
            }

            uint k = this.Lrot(this.codeBuffer, n);
            this.codeBuffer = k & ~Bmask[n];
            k &= Bmask[n];
            this.codeBits -= n;
            return (int)k;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetBit()
        {
            if (this.codeBits < 1)
            {
                this.GrowBufferUnsafe();
            }

            uint k = this.codeBuffer;
            this.codeBuffer <<= 1;
            this.codeBits--;

            return (int)(k & 0x80000000);
        }

        private void GrowBufferUnsafe()
        {
            do
            {
                // TODO: EOF
                int b = this.nomore ? 0 : this.stream.ReadByte();
                if (b == PdfJsJpegConstants.Markers.Prefix)
                {
                    long position = this.stream.Position - 1;
                    int c = this.stream.ReadByte();
                    while (c == PdfJsJpegConstants.Markers.Prefix)
                    {
                        if (c != 0)
                        {
                            this.marker = (byte)c;
                            this.nomore = true;
                            if (!this.IsRestartMarker(this.marker))
                            {
                                this.stream.Position = position;
                            }

                            return;
                        }
                    }
                }

                this.codeBuffer |= (uint)b << (24 - this.codeBits);
                this.codeBits += 8;
            }
            while (this.codeBits <= 24);
        }

        private int DecodeHuffman(ref PdfJsHuffmanTable table)
        {
            this.CheckBits();

            // Look at the top FastBits and determine what symbol ID it is,
            // if the code is <= FastBits.
            int c = this.PeekBits();
            int k = table.Lookahead[c];
            if (k < 0xFF)
            {
                int s = table.Sizes[k];
                if (s > this.codeBits)
                {
                    return -1;
                }

                this.codeBuffer <<= s;
                this.codeBits -= s;
                return table.Values[k];
            }

            // Naive test is to shift the code_buffer down so k bits are
            // valid, then test against MaxCode. To speed this up, we've
            // preshifted maxcode left so that it has (16-k) 0s at the
            // end; in other words, regardless of the number of bits, it
            // wants to be compared against something shifted to have 16;
            // that way we don't need to shift inside the loop.
            uint temp = this.codeBuffer >> 16;
            for (k = FastBits + 1; ; k++)
            {
                if (temp < table.MaxCode[k])
                {
                    break;
                }
            }

            if (k == 17)
            {
                // Error! code not found
                this.codeBits -= 16;
                return -1;
            }

            if (k > this.codeBits)
            {
                return -1;
            }

            // Convert the huffman code to the symbol id
            c = (int)(((this.codeBuffer >> (32 - k)) & Bmask[k]) + table.ValOffset[k]);

            // Convert the id to a symbol
            this.codeBits -= k;
            this.codeBuffer <<= k;
            return table.Values[c];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int ExtendReceive(int n)
        {
            if (this.codeBits < n)
            {
                this.GrowBufferUnsafe();
            }

            int sgn = (int)((int)this.codeBuffer >> 31);
            uint k = this.Lrot(this.codeBuffer, n);
            this.codeBuffer = k & ~Bmask[n];
            k &= Bmask[n];
            this.codeBits -= n;
            return (int)(k + (Bias[n] & ~sgn));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CheckBits()
        {
            if (this.codeBits < 16)
            {
                this.GrowBufferUnsafe();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int PeekBits()
        {
            return (int)((this.codeBuffer >> (32 - FastBits)) & ((1 << FastBits) - 1));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private uint Lrot(uint x, int y)
        {
            return (x << y) | (x >> (32 - y));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsRestartMarker(byte x)
        {
            return x >= PdfJsJpegConstants.Markers.RST0 && x <= PdfJsJpegConstants.Markers.RST7;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Reset()
        {
            this.codeBits = 0;
            this.codeBuffer = 0;
            this.nomore = false;

            for (int i = 0; i < this.components.Length; i++)
            {
                PdfJsFrameComponent c = this.components[i];
                c.DcPredictor = 0;
            }

            this.marker = PdfJsJpegConstants.Markers.Prefix;
            this.eobrun = 0;

            // No more than 1<<31 MCUs if no restartInterval? that's plenty safe since we don't even allow 1<<30 pixels
            this.todo = this.restartInterval > 0 ? this.restartInterval : 0x7FFFFFFF;
        }
    }
}
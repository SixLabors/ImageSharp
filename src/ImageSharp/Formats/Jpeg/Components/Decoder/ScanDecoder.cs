// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.IO;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder
{
    /// <summary>
    /// Decodes the Huffman encoded spectral scan.
    /// Originally ported from <see href="https://github.com/rds1983/StbSharp"/>
    /// with additional fixes for both performance and common encoding errors.
    /// </summary>
    internal class ScanDecoder
    {
        // The number of bits that can be read via a LUT.
        public const int FastBits = 9;

        // LUT mask for n rightmost bits. Bmask[n] = (1 << n) - 1
        private static readonly uint[] Bmask = { 0, 1, 3, 7, 15, 31, 63, 127, 255, 511, 1023, 2047, 4095, 8191, 16383, 32767, 65535 };

        // LUT Bias[n] = (-1 << n) + 1
        private static readonly int[] Bias = { 0, -1, -3, -7, -15, -31, -63, -127, -255, -511, -1023, -2047, -4095, -8191, -16383, -32767 };

        private readonly JpegFrame frame;
        private readonly HuffmanTables dcHuffmanTables;
        private readonly HuffmanTables acHuffmanTables;
        private readonly FastACTables fastACTables;

        private readonly DoubleBufferedStreamReader stream;
        private readonly JpegComponent[] components;
        private readonly ZigZag dctZigZag;

        // The restart interval.
        private readonly int restartInterval;

        // The current component index.
        private readonly int componentIndex;

        // The number of interleaved components.
        private readonly int componentsLength;

        // The spectral selection start.
        private readonly int spectralStart;

        // The spectral selection end.
        private readonly int spectralEnd;

        // The successive approximation high bit end.
        private readonly int successiveHigh;

        // The successive approximation low bit end.
        private readonly int successiveLow;

        // The number of valid bits left to read in the buffer.
        private int codeBits;

        // The entropy encoded code buffer.
        private uint codeBuffer;

        // Whether there is more data to pull from the stream for the current mcu.
        private bool nomore;

        // Whether we have prematurely reached the end of the file.
        private bool eof;

        // The current, if any, marker in the input stream.
        private byte marker;

        // Whether we have a bad marker, I.E. One that is not between RST0 and RST7
        private bool badMarker;

        // The opening position of an identified marker.
        private long markerPosition;

        // How many mcu's are left to do.
        private int todo;

        // The End-Of-Block countdown for ending the sequence prematurely when the remaining coefficients are zero.
        private int eobrun;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScanDecoder"/> class.
        /// </summary>
        /// <param name="stream">The input stream.</param>
        /// <param name="frame">The image frame.</param>
        /// <param name="dcHuffmanTables">The DC Huffman tables.</param>
        /// <param name="acHuffmanTables">The AC Huffman tables.</param>
        /// <param name="fastACTables">The fast AC decoding tables.</param>
        /// <param name="componentIndex">The component index within the array.</param>
        /// <param name="componentsLength">The length of the components. Different to the array length.</param>
        /// <param name="restartInterval">The reset interval.</param>
        /// <param name="spectralStart">The spectral selection start.</param>
        /// <param name="spectralEnd">The spectral selection end.</param>
        /// <param name="successiveHigh">The successive approximation bit high end.</param>
        /// <param name="successiveLow">The successive approximation bit low end.</param>
        public ScanDecoder(
            DoubleBufferedStreamReader stream,
            JpegFrame frame,
            HuffmanTables dcHuffmanTables,
            HuffmanTables acHuffmanTables,
            FastACTables fastACTables,
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
            this.frame = frame;
            this.dcHuffmanTables = dcHuffmanTables;
            this.acHuffmanTables = acHuffmanTables;
            this.fastACTables = fastACTables;
            this.components = frame.Components;
            this.marker = JpegConstants.Markers.XFF;
            this.markerPosition = 0;
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
        public void ParseEntropyCodedData()
        {
            this.Reset();

            if (!this.frame.Progressive)
            {
                this.ParseBaselineData();
            }
            else
            {
                this.ParseProgressiveData();
            }

            if (this.badMarker)
            {
                this.stream.Position = this.markerPosition;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint LRot(uint x, int y) => (x << y) | (x >> (32 - y));

        private void ParseBaselineData()
        {
            if (this.componentsLength == 1)
            {
                this.ParseBaselineDataNonInterleaved();
            }
            else
            {
                this.ParseBaselineDataInterleaved();
            }
        }

        private void ParseBaselineDataInterleaved()
        {
            // Interleaved
            int mcu = 0;
            int mcusPerColumn = this.frame.McusPerColumn;
            int mcusPerLine = this.frame.McusPerLine;
            for (int j = 0; j < mcusPerColumn; j++)
            {
                for (int i = 0; i < mcusPerLine; i++)
                {
                    // Scan an interleaved mcu... process components in order
                    for (int k = 0; k < this.componentsLength; k++)
                    {
                        JpegComponent component = this.components[k];

                        ref HuffmanTable dcHuffmanTable = ref this.dcHuffmanTables[component.DCHuffmanTableId];
                        ref HuffmanTable acHuffmanTable = ref this.acHuffmanTables[component.ACHuffmanTableId];
                        ref short fastACRef = ref this.fastACTables.GetAcTableReference(component);
                        int h = component.HorizontalSamplingFactor;
                        int v = component.VerticalSamplingFactor;

                        // Scan out an mcu's worth of this component; that's just determined
                        // by the basic H and V specified for the component
                        for (int y = 0; y < v; y++)
                        {
                            for (int x = 0; x < h; x++)
                            {
                                if (this.eof)
                                {
                                    return;
                                }

                                int mcuRow = mcu / mcusPerLine;
                                int mcuCol = mcu % mcusPerLine;
                                int blockRow = (mcuRow * v) + y;
                                int blockCol = (mcuCol * h) + x;

                                this.DecodeBlockBaseline(
                                    component,
                                    blockRow,
                                    blockCol,
                                    ref dcHuffmanTable,
                                    ref acHuffmanTable,
                                    ref fastACRef);
                            }
                        }
                    }

                    // After all interleaved components, that's an interleaved MCU,
                    // so now count down the restart interval
                    mcu++;
                    if (!this.ContinueOnMcuComplete())
                    {
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// Non-interleaved data, we just need to process one block at a ti
        /// in trivial scanline order
        /// number of blocks to do just depends on how many actual "pixels"
        /// component has, independent of interleaved MCU blocking and such
        /// </summary>
        private void ParseBaselineDataNonInterleaved()
        {
            JpegComponent component = this.components[this.componentIndex];

            int w = component.WidthInBlocks;
            int h = component.HeightInBlocks;

            ref HuffmanTable dcHuffmanTable = ref this.dcHuffmanTables[component.DCHuffmanTableId];
            ref HuffmanTable acHuffmanTable = ref this.acHuffmanTables[component.ACHuffmanTableId];
            ref short fastACRef = ref this.fastACTables.GetAcTableReference(component);

            int mcu = 0;
            for (int j = 0; j < h; j++)
            {
                for (int i = 0; i < w; i++)
                {
                    if (this.eof)
                    {
                        return;
                    }

                    int blockRow = mcu / w;
                    int blockCol = mcu % w;

                    this.DecodeBlockBaseline(
                        component,
                        blockRow,
                        blockCol,
                        ref dcHuffmanTable,
                        ref acHuffmanTable,
                        ref fastACRef);

                    // Every data block is an MCU, so countdown the restart interval
                    mcu++;
                    if (!this.ContinueOnMcuComplete())
                    {
                        return;
                    }
                }
            }
        }

        private void ParseProgressiveData()
        {
            if (this.componentsLength == 1)
            {
                this.ParseProgressiveDataNonInterleaved();
            }
            else
            {
                this.ParseProgressiveDataInterleaved();
            }
        }

        private void ParseProgressiveDataInterleaved()
        {
            // Interleaved
            int mcu = 0;
            int mcusPerColumn = this.frame.McusPerColumn;
            int mcusPerLine = this.frame.McusPerLine;
            for (int j = 0; j < mcusPerColumn; j++)
            {
                for (int i = 0; i < mcusPerLine; i++)
                {
                    // Scan an interleaved mcu... process components in order
                    for (int k = 0; k < this.componentsLength; k++)
                    {
                        JpegComponent component = this.components[k];
                        ref HuffmanTable dcHuffmanTable = ref this.dcHuffmanTables[component.DCHuffmanTableId];
                        int h = component.HorizontalSamplingFactor;
                        int v = component.VerticalSamplingFactor;

                        // Scan out an mcu's worth of this component; that's just determined
                        // by the basic H and V specified for the component
                        for (int y = 0; y < v; y++)
                        {
                            for (int x = 0; x < h; x++)
                            {
                                if (this.eof)
                                {
                                    return;
                                }

                                int mcuRow = mcu / mcusPerLine;
                                int mcuCol = mcu % mcusPerLine;
                                int blockRow = (mcuRow * v) + y;
                                int blockCol = (mcuCol * h) + x;

                                this.DecodeBlockProgressiveDC(
                                    component,
                                    blockRow,
                                    blockCol,
                                    ref dcHuffmanTable);
                            }
                        }
                    }

                    // After all interleaved components, that's an interleaved MCU,
                    // so now count down the restart interval
                    mcu++;
                    if (!this.ContinueOnMcuComplete())
                    {
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// Non-interleaved data, we just need to process one block at a time,
        /// in trivial scanline order
        /// number of blocks to do just depends on how many actual "pixels" this
        /// component has, independent of interleaved MCU blocking and such
        /// </summary>
        private void ParseProgressiveDataNonInterleaved()
        {
            JpegComponent component = this.components[this.componentIndex];

            int w = component.WidthInBlocks;
            int h = component.HeightInBlocks;

            ref HuffmanTable dcHuffmanTable = ref this.dcHuffmanTables[component.DCHuffmanTableId];
            ref HuffmanTable acHuffmanTable = ref this.acHuffmanTables[component.ACHuffmanTableId];
            ref short fastACRef = ref this.fastACTables.GetAcTableReference(component);

            int mcu = 0;
            for (int j = 0; j < h; j++)
            {
                for (int i = 0; i < w; i++)
                {
                    if (this.eof)
                    {
                        return;
                    }

                    int blockRow = mcu / w;
                    int blockCol = mcu % w;

                    if (this.spectralStart == 0)
                    {
                        this.DecodeBlockProgressiveDC(
                            component,
                            blockRow,
                            blockCol,
                            ref dcHuffmanTable);
                    }
                    else
                    {
                        this.DecodeBlockProgressiveAC(
                            component,
                            blockRow,
                            blockCol,
                            ref acHuffmanTable,
                            ref fastACRef);
                    }

                    // Every data block is an MCU, so countdown the restart interval
                    mcu++;
                    if (!this.ContinueOnMcuComplete())
                    {
                        return;
                    }
                }
            }
        }

        private void DecodeBlockBaseline(
            JpegComponent component,
            int row,
            int col,
            ref HuffmanTable dcTable,
            ref HuffmanTable acTable,
            ref short fastACRef)
        {
            this.CheckBits();
            int t = this.DecodeHuffman(ref dcTable);

            if (t < 0)
            {
                JpegThrowHelper.ThrowBadHuffmanCode();
            }

            ref short blockDataRef = ref component.GetBlockDataReference(col, row);

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
                int r = Unsafe.Add(ref fastACRef, c);

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
                        JpegThrowHelper.ThrowBadHuffmanCode();
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
        }

        private void DecodeBlockProgressiveDC(
            JpegComponent component,
            int row,
            int col,
            ref HuffmanTable dcTable)
        {
            if (this.spectralEnd != 0)
            {
                JpegThrowHelper.ThrowImageFormatException("Can't merge DC and AC.");
            }

            this.CheckBits();

            ref short blockDataRef = ref component.GetBlockDataReference(col, row);

            if (this.successiveHigh == 0)
            {
                // First scan for DC coefficient, must be first
                int t = this.DecodeHuffman(ref dcTable);
                int diff = t != 0 ? this.ExtendReceive(t) : 0;

                int dc = component.DcPredictor + diff;
                component.DcPredictor = dc;

                blockDataRef = (short)(dc << this.successiveLow);
            }
            else
            {
                // Refinement scan for DC coefficient
                if (this.GetBit() != 0)
                {
                    blockDataRef += (short)(1 << this.successiveLow);
                }
            }
        }

        private void DecodeBlockProgressiveAC(
            JpegComponent component,
            int row,
            int col,
            ref HuffmanTable acTable,
            ref short fastACRef)
        {
            if (this.spectralStart == 0)
            {
                JpegThrowHelper.ThrowImageFormatException("Can't merge DC and AC.");
            }

            ref short blockDataRef = ref component.GetBlockDataReference(col, row);

            if (this.successiveHigh == 0)
            {
                // MCU decoding for AC initial scan (either spectral selection,
                // or first pass of successive approximation).
                int shift = this.successiveLow;

                if (this.eobrun != 0)
                {
                    this.eobrun--;
                    return;
                }

                int k = this.spectralStart;
                do
                {
                    int zig;
                    int s;

                    this.CheckBits();
                    int c = this.PeekBits();
                    int r = Unsafe.Add(ref fastACRef, c);

                    if (r != 0)
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
                            JpegThrowHelper.ThrowBadHuffmanCode();
                        }

                        s = rs & 15;
                        r = rs >> 4;

                        if (s == 0)
                        {
                            if (r < 15)
                            {
                                this.eobrun = 1 << r;
                                if (r != 0)
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
                this.DecodeBlockProgressiveACRefined(ref blockDataRef, ref acTable);
            }
        }

        private void DecodeBlockProgressiveACRefined(ref short blockDataRef, ref HuffmanTable acTable)
        {
            int k;

            // Refinement scan for these AC coefficients
            short bit = (short)(1 << this.successiveLow);

            if (this.eobrun != 0)
            {
                this.eobrun--;
                for (k = this.spectralStart; k <= this.spectralEnd; k++)
                {
                    ref short p = ref Unsafe.Add(ref blockDataRef, this.dctZigZag[k]);
                    if (p != 0)
                    {
                        if (this.GetBit() != 0)
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
                        JpegThrowHelper.ThrowBadHuffmanCode();
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

                            if (r != 0)
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
                            JpegThrowHelper.ThrowBadHuffmanCode();
                        }

                        // Sign bit
                        if (this.GetBit() != 0)
                        {
                            s = bit;
                        }
                        else
                        {
                            s = -bit;
                        }
                    }

                    // Advance by r
                    while (k <= this.spectralEnd)
                    {
                        ref short p = ref Unsafe.Add(ref blockDataRef, this.dctZigZag[k++]);
                        if (p != 0)
                        {
                            if (this.GetBit() != 0)
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

        [MethodImpl(InliningOptions.ShortMethod)]
        private int GetBits(int n)
        {
            if (this.codeBits < n)
            {
                this.FillBuffer();
            }

            uint k = LRot(this.codeBuffer, n);
            this.codeBuffer = k & ~Bmask[n];
            k &= Bmask[n];
            this.codeBits -= n;
            return (int)k;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private int GetBit()
        {
            if (this.codeBits < 1)
            {
                this.FillBuffer();
            }

            uint k = this.codeBuffer;
            this.codeBuffer <<= 1;
            this.codeBits--;

            return (int)(k & 0x80000000);
        }

        [MethodImpl(InliningOptions.ColdPath)]
        private void FillBuffer()
        {
            // Attempt to load at least the minimum nbumber of required bits into the buffer.
            // We fail to do so only if we hit a marker or reach the end of the input stream.
            do
            {
                int b = this.nomore ? 0 : this.stream.ReadByte();

                if (b == -1)
                {
                    // We've encountered the end of the file stream which means there's no EOI marker in the image
                    // or the SOS marker has the wrong dimensions set.
                    this.eof = true;
                    b = 0;
                }

                // Found a marker.
                if (b == JpegConstants.Markers.XFF)
                {
                    this.markerPosition = this.stream.Position - 1;
                    int c = this.stream.ReadByte();
                    while (c == JpegConstants.Markers.XFF)
                    {
                        c = this.stream.ReadByte();

                        if (c == -1)
                        {
                            this.eof = true;
                            c = 0;
                            break;
                        }
                    }

                    if (c != 0)
                    {
                        this.marker = (byte)c;
                        this.nomore = true;
                        if (!this.HasRestart())
                        {
                            this.badMarker = true;
                        }

                        return;
                    }
                }

                this.codeBuffer |= (uint)b << (24 - this.codeBits);
                this.codeBits += 8;
            }
            while (this.codeBits <= 24);
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private int DecodeHuffman(ref HuffmanTable table)
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

            return this.DecodeHuffmanSlow(ref table);
        }

        [MethodImpl(InliningOptions.ColdPath)]
        private int DecodeHuffmanSlow(ref HuffmanTable table)
        {
            // Naive test is to shift the code_buffer down so k bits are
            // valid, then test against MaxCode. To speed this up, we've
            // preshifted maxcode left so that it has (16-k) 0s at the
            // end; in other words, regardless of the number of bits, it
            // wants to be compared against something shifted to have 16;
            // that way we don't need to shift inside the loop.
            uint temp = this.codeBuffer >> 16;
            int k;
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
            int c = (int)(((this.codeBuffer >> (32 - k)) & Bmask[k]) + table.ValOffset[k]);

            // Convert the id to a symbol
            this.codeBits -= k;
            this.codeBuffer <<= k;
            return table.Values[c];
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private int ExtendReceive(int n)
        {
            if (this.codeBits < n)
            {
                this.FillBuffer();
            }

            int sgn = (int)this.codeBuffer >> 31;
            uint k = LRot(this.codeBuffer, n);
            this.codeBuffer = k & ~Bmask[n];
            k &= Bmask[n];
            this.codeBits -= n;
            return (int)(k + (Bias[n] & ~sgn));
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private void CheckBits()
        {
            if (this.codeBits < 16)
            {
                this.FillBuffer();
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private int PeekBits() => (int)((this.codeBuffer >> (32 - FastBits)) & ((1 << FastBits) - 1));

        [MethodImpl(InliningOptions.ShortMethod)]
        private bool ContinueOnMcuComplete()
        {
            if (--this.todo > 0)
            {
                return true;
            }

            if (this.codeBits < 24)
            {
                this.FillBuffer();
            }

            // If it's NOT a restart, then just bail, so we get corrupt data rather than no data.
            // Reset the stream to before any bad markers to ensure we can read sucessive segments.
            if (this.badMarker)
            {
                this.stream.Position = this.markerPosition;
            }

            if (!this.HasRestart())
            {
                return false;
            }

            this.Reset();

            return true;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private bool HasRestart()
        {
            byte m = this.marker;
            return m >= JpegConstants.Markers.RST0 && m <= JpegConstants.Markers.RST7;
        }

        private void Reset()
        {
            this.codeBits = 0;
            this.codeBuffer = 0;

            for (int i = 0; i < this.components.Length; i++)
            {
                JpegComponent c = this.components[i];
                c.DcPredictor = 0;
            }

            this.nomore = false;
            this.marker = JpegConstants.Markers.XFF;
            this.markerPosition = 0;
            this.badMarker = false;
            this.eobrun = 0;

            // No more than 1<<31 MCUs if no restartInterval? that's plenty safe since we don't even allow 1<<30 pixels
            this.todo = this.restartInterval > 0 ? this.restartInterval : int.MaxValue;
        }
    }
}
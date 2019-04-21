// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder
{
    /// <summary>
    /// Decodes the Huffman encoded spectral scan.
    /// Originally ported from <see href="https://github.com/t0rakka/mango"/>
    /// with additional fixes for both performance and common encoding errors.
    /// </summary>
    internal class HuffmanScanDecoder
    {
        public const int JpegRegisterSize = 64;

        // Huffman look-ahead table log2 size
        public const int JpegHuffLookupBits = 8;

        public const int JpegHuffSlowBits = JpegHuffLookupBits + 1;

        public const int JpegHuffLookupSize = 1 << JpegHuffLookupBits;

        private readonly JpegFrame frame;
        private readonly HuffmanTable[] dcHuffmanTables;
        private readonly HuffmanTable[] acHuffmanTables;

        private readonly DoubleBufferedStreamReader stream;
        private readonly HuffmanScanBuffer scanBuffer;
        private readonly JpegComponent[] components;

        // The restart interval.
        private readonly int restartInterval;

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

        // How many mcu's are left to do.
        private int todo;

        // The End-Of-Block countdown for ending the sequence prematurely when the remaining coefficients are zero.
        private int eobrun;

        private ZigZag dctZigZag;

        /// <summary>
        /// Initializes a new instance of the <see cref="HuffmanScanDecoder"/> class.
        /// </summary>
        /// <param name="stream">The input stream.</param>
        /// <param name="frame">The image frame.</param>
        /// <param name="dcHuffmanTables">The DC Huffman tables.</param>
        /// <param name="acHuffmanTables">The AC Huffman tables.</param>
        /// <param name="componentsLength">The length of the components. Different to the array length.</param>
        /// <param name="restartInterval">The reset interval.</param>
        /// <param name="spectralStart">The spectral selection start.</param>
        /// <param name="spectralEnd">The spectral selection end.</param>
        /// <param name="successiveHigh">The successive approximation bit high end.</param>
        /// <param name="successiveLow">The successive approximation bit low end.</param>
        public HuffmanScanDecoder(
            DoubleBufferedStreamReader stream,
            JpegFrame frame,
            HuffmanTable[] dcHuffmanTables,
            HuffmanTable[] acHuffmanTables,
            int componentsLength,
            int restartInterval,
            int spectralStart,
            int spectralEnd,
            int successiveHigh,
            int successiveLow)
        {
            this.dctZigZag = ZigZag.CreateUnzigTable();
            this.stream = stream;
            this.scanBuffer = new HuffmanScanBuffer(stream);
            this.frame = frame;
            this.dcHuffmanTables = dcHuffmanTables;
            this.acHuffmanTables = acHuffmanTables;
            this.components = frame.Components;
            this.componentsLength = componentsLength;
            this.restartInterval = restartInterval;
            this.todo = restartInterval;
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
            if (!this.frame.Progressive)
            {
                this.ParseBaselineData();
            }
            else
            {
                this.ParseProgressiveData();
            }

            if (this.scanBuffer.BadMarker)
            {
                this.stream.Position = this.scanBuffer.MarkerPosition;
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static int Extend(int v, int nbits) => v - ((((v + v) >> nbits) - 1) & ((1 << nbits) - 1));

        [MethodImpl(InliningOptions.ShortMethod)]
        private static int GetBits(HuffmanScanBuffer buffer, int nbits) => (int)ExtractBits(buffer.Data, buffer.Remain -= nbits, nbits);

        [MethodImpl(InliningOptions.ShortMethod)]
        private static int PeekBits(HuffmanScanBuffer buffer, int nbits) => (int)ExtractBits(buffer.Data, buffer.Remain - nbits, nbits);

        [MethodImpl(InliningOptions.ShortMethod)]
        private static ulong ExtractBits(ulong value, int offset, int size) => (value >> offset) & (ulong)((1 << size) - 1);

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

        private unsafe void ParseBaselineDataInterleaved()
        {
            // Interleaved
            int mcu = 0;
            int mcusPerColumn = this.frame.McusPerColumn;
            int mcusPerLine = this.frame.McusPerLine;

            // Pre-derive the huffman table to avoid in-loop checks.
            for (int i = 0; i < this.componentsLength; i++)
            {
                int order = this.frame.ComponentOrder[i];
                JpegComponent component = this.components[order];

                ref HuffmanTable dcHuffmanTable = ref this.dcHuffmanTables[component.DCHuffmanTableId];
                ref HuffmanTable acHuffmanTable = ref this.acHuffmanTables[component.ACHuffmanTableId];
                dcHuffmanTable.Configure();
                acHuffmanTable.Configure();
            }

            for (int j = 0; j < mcusPerColumn; j++)
            {
                for (int i = 0; i < mcusPerLine; i++)
                {
                    // Scan an interleaved mcu... process components in order
                    int mcuRow = mcu / mcusPerLine;
                    int mcuCol = mcu % mcusPerLine;
                    for (int k = 0; k < this.componentsLength; k++)
                    {
                        int order = this.frame.ComponentOrder[k];
                        JpegComponent component = this.components[order];

                        ref HuffmanTable dcHuffmanTable = ref this.dcHuffmanTables[component.DCHuffmanTableId];
                        ref HuffmanTable acHuffmanTable = ref this.acHuffmanTables[component.ACHuffmanTableId];

                        int h = component.HorizontalSamplingFactor;
                        int v = component.VerticalSamplingFactor;

                        // Scan out an mcu's worth of this component; that's just determined
                        // by the basic H and V specified for the component
                        for (int y = 0; y < v; y++)
                        {
                            int blockRow = (mcuRow * v) + y;
                            Span<Block8x8> blockSpan = component.SpectralBlocks.GetRowSpan(blockRow);
                            ref Block8x8 blockRef = ref MemoryMarshal.GetReference(blockSpan);

                            for (int x = 0; x < h; x++)
                            {
                                int blockCol = (mcuCol * h) + x;

                                this.DecodeBlockBaseline(
                                    component,
                                    ref Unsafe.Add(ref blockRef, blockCol),
                                    ref dcHuffmanTable,
                                    ref acHuffmanTable);
                            }
                        }
                    }

                    // After all interleaved components, that's an interleaved MCU,
                    // so now count down the restart interval
                    mcu++;
                    this.HandleRestart();
                }
            }
        }

        /// <summary>
        /// Non-interleaved data, we just need to process one block at a time in trivial scanline order
        /// number of blocks to do just depends on how many actual "pixels" each component has,
        /// independent of interleaved MCU blocking and such.
        /// </summary>
        private unsafe void ParseBaselineDataNonInterleaved()
        {
            JpegComponent component = this.components[this.frame.ComponentOrder[0]];

            int w = component.WidthInBlocks;
            int h = component.HeightInBlocks;

            ref HuffmanTable dcHuffmanTable = ref this.dcHuffmanTables[component.DCHuffmanTableId];
            ref HuffmanTable acHuffmanTable = ref this.acHuffmanTables[component.ACHuffmanTableId];
            dcHuffmanTable.Configure();
            acHuffmanTable.Configure();

            int mcu = 0;
            for (int j = 0; j < h; j++)
            {
                Span<Block8x8> blockSpan = component.SpectralBlocks.GetRowSpan(j);
                ref Block8x8 blockRef = ref MemoryMarshal.GetReference(blockSpan);

                for (int i = 0; i < w; i++)
                {
                    this.DecodeBlockBaseline(
                        component,
                        ref Unsafe.Add(ref blockRef, i),
                        ref dcHuffmanTable,
                        ref acHuffmanTable);

                    // Every data block is an MCU, so countdown the restart interval
                    mcu++;

                    this.HandleRestart();
                }
            }
        }

        private void CheckProgressiveData()
        {
            // Validate successive scan parameters.
            // Logic has been adapted from libjpeg.
            // See Table B.3 – Scan header parameter size and values. itu-t81.pdf
            bool invalid = false;
            if (this.spectralStart == 0)
            {
                if (this.spectralEnd != 0)
                {
                    invalid = true;
                }
            }
            else
            {
                // Need not check Ss/Se < 0 since they came from unsigned bytes.
                if (this.spectralEnd < this.spectralStart || this.spectralEnd > 63)
                {
                    invalid = true;
                }

                // AC scans may have only one component.
                if (this.componentsLength != 1)
                {
                    invalid = true;
                }
            }

            if (this.successiveHigh != 0)
            {
                // Successive approximation refinement scan: must have Al = Ah-1.
                if (this.successiveHigh - 1 != this.successiveLow)
                {
                    invalid = true;
                }
            }

            // TODO: How does this affect 12bit jpegs.
            // According to libjpeg the range covers 8bit only?
            if (this.successiveLow > 13)
            {
                invalid = true;
            }

            if (invalid)
            {
                JpegThrowHelper.ThrowBadProgressiveScan(this.spectralStart, this.spectralEnd, this.successiveHigh, this.successiveLow);
            }
        }

        private void ParseProgressiveData()
        {
            this.CheckProgressiveData();

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

            // Pre-derive the huffman table to avoid in-loop checks.
            for (int k = 0; k < this.componentsLength; k++)
            {
                int order = this.frame.ComponentOrder[k];
                JpegComponent component = this.components[order];
                ref HuffmanTable dcHuffmanTable = ref this.dcHuffmanTables[component.DCHuffmanTableId];
                dcHuffmanTable.Configure();
            }

            for (int j = 0; j < mcusPerColumn; j++)
            {
                for (int i = 0; i < mcusPerLine; i++)
                {
                    // Scan an interleaved mcu... process components in order
                    int mcuRow = mcu / mcusPerLine;
                    int mcuCol = mcu % mcusPerLine;
                    for (int k = 0; k < this.componentsLength; k++)
                    {
                        int order = this.frame.ComponentOrder[k];
                        JpegComponent component = this.components[order];
                        ref HuffmanTable dcHuffmanTable = ref this.dcHuffmanTables[component.DCHuffmanTableId];

                        int h = component.HorizontalSamplingFactor;
                        int v = component.VerticalSamplingFactor;

                        // Scan out an mcu's worth of this component; that's just determined
                        // by the basic H and V specified for the component
                        for (int y = 0; y < v; y++)
                        {
                            int blockRow = (mcuRow * v) + y;
                            Span<Block8x8> blockSpan = component.SpectralBlocks.GetRowSpan(blockRow);
                            ref Block8x8 blockRef = ref MemoryMarshal.GetReference(blockSpan);

                            for (int x = 0; x < h; x++)
                            {
                                if (this.scanBuffer.Eof)
                                {
                                    return;
                                }

                                int blockCol = (mcuCol * h) + x;

                                this.DecodeBlockProgressiveDC(
                                    component,
                                    ref Unsafe.Add(ref blockRef, blockCol),
                                    ref dcHuffmanTable);
                            }
                        }
                    }

                    // After all interleaved components, that's an interleaved MCU,
                    // so now count down the restart interval
                    mcu++;
                    this.HandleRestart();
                }
            }
        }

        /// <summary>
        /// Non-interleaved data, we just need to process one block at a time,
        /// in trivial scanline order
        /// number of blocks to do just depends on how many actual "pixels" this
        /// component has, independent of interleaved MCU blocking and such
        /// </summary>
        private unsafe void ParseProgressiveDataNonInterleaved()
        {
            JpegComponent component = this.components[this.frame.ComponentOrder[0]];

            int w = component.WidthInBlocks;
            int h = component.HeightInBlocks;

            if (this.spectralStart == 0)
            {
                ref HuffmanTable dcHuffmanTable = ref this.dcHuffmanTables[component.DCHuffmanTableId];
                dcHuffmanTable.Configure();

                int mcu = 0;
                for (int j = 0; j < h; j++)
                {
                    Span<Block8x8> blockSpan = component.SpectralBlocks.GetRowSpan(j);
                    ref Block8x8 blockRef = ref MemoryMarshal.GetReference(blockSpan);

                    for (int i = 0; i < w; i++)
                    {
                        if (this.scanBuffer.Eof)
                        {
                            return;
                        }

                        this.DecodeBlockProgressiveDC(
                            component,
                            ref Unsafe.Add(ref blockRef, i),
                            ref dcHuffmanTable);

                        // Every data block is an MCU, so countdown the restart interval
                        mcu++;
                        this.HandleRestart();
                    }
                }
            }
            else
            {
                ref HuffmanTable acHuffmanTable = ref this.acHuffmanTables[component.ACHuffmanTableId];
                acHuffmanTable.Configure();

                int mcu = 0;
                for (int j = 0; j < h; j++)
                {
                    Span<Block8x8> blockSpan = component.SpectralBlocks.GetRowSpan(j);
                    ref Block8x8 blockRef = ref MemoryMarshal.GetReference(blockSpan);

                    for (int i = 0; i < w; i++)
                    {
                        if (this.scanBuffer.Eof)
                        {
                            return;
                        }

                        this.DecodeBlockProgressiveAC(
                            ref Unsafe.Add(ref blockRef, i),
                            ref acHuffmanTable);

                        // Every data block is an MCU, so countdown the restart interval
                        mcu++;
                        this.HandleRestart();
                    }
                }
            }
        }

        private void DecodeBlockBaseline(
            JpegComponent component,
            ref Block8x8 block,
            ref HuffmanTable dcTable,
            ref HuffmanTable acTable)
        {
            ref short blockDataRef = ref Unsafe.As<Block8x8, short>(ref block);
            HuffmanScanBuffer buffer = this.scanBuffer;
            ref ZigZag zigzag = ref this.dctZigZag;

            // DC
            int t = this.DecodeHuffman(buffer, ref dcTable);
            if (t != 0)
            {
                t = Receive(buffer, t);
            }

            t += component.DcPredictor;
            component.DcPredictor = t;
            blockDataRef = (short)t;

            // AC
            for (int i = 1; i < 64;)
            {
                int s = this.DecodeHuffman(buffer, ref acTable);

                int r = s >> 4;
                s &= 15;

                if (s != 0)
                {
                    i += r;
                    s = Receive(buffer, s);
                    Unsafe.Add(ref blockDataRef, zigzag[i++]) = (short)s;
                }
                else
                {
                    if (r == 0)
                    {
                        break;
                    }

                    i += 16;
                }
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private unsafe int DecodeHuffman(HuffmanScanBuffer buffer, ref HuffmanTable h)
        {
            buffer.CheckBits();
            int v = PeekBits(buffer, JpegHuffLookupBits);
            int symbol = h.LookaheadValue[v];
            int size = h.LookaheadSize[v];

            if (size == JpegHuffSlowBits)
            {
                ulong x = this.scanBuffer.Data << (JpegRegisterSize - this.scanBuffer.Remain);
                while (x > h.MaxCode[size])
                {
                    size++;
                }

                v = (int)(x >> (JpegRegisterSize - size));
                symbol = h.Values[h.ValOffset[size] + v];
            }

            buffer.Remain -= size;

            return symbol;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static int Receive(HuffmanScanBuffer buffer, int nbits)
        {
            buffer.CheckBits();
            return Extend(GetBits(buffer, nbits), nbits);
        }

        private void DecodeBlockProgressiveDC(JpegComponent component, ref Block8x8 block, ref HuffmanTable dcTable)
        {
            ref short blockDataRef = ref Unsafe.As<Block8x8, short>(ref block);
            HuffmanScanBuffer buffer = this.scanBuffer;

            if (this.successiveHigh == 0)
            {
                // First scan for DC coefficient, must be first
                int s = this.DecodeHuffman(buffer, ref dcTable);
                if (s != 0)
                {
                    s = Receive(buffer, s);
                }

                s += component.DcPredictor;
                component.DcPredictor = s;
                blockDataRef = (short)(s << this.successiveLow);
            }
            else
            {
                // Refinement scan for DC coefficient
                buffer.CheckBits();
                blockDataRef |= (short)(GetBits(buffer, 1) << this.successiveLow);
            }
        }

        private void DecodeBlockProgressiveAC(ref Block8x8 block, ref HuffmanTable acTable)
        {
            ref short blockDataRef = ref Unsafe.As<Block8x8, short>(ref block);
            if (this.successiveHigh == 0)
            {
                // MCU decoding for AC initial scan (either spectral selection,
                // or first pass of successive approximation).
                if (this.eobrun != 0)
                {
                    --this.eobrun;
                    return;
                }

                HuffmanScanBuffer buffer = this.scanBuffer;
                ref ZigZag zigzag = ref this.dctZigZag;
                int start = this.spectralStart;
                int end = this.spectralEnd;
                int low = this.successiveLow;

                for (int i = start; i <= end; ++i)
                {
                    int s = this.DecodeHuffman(buffer, ref acTable);
                    int r = s >> 4;
                    s &= 15;

                    i += r;

                    if (s != 0)
                    {
                        s = Receive(buffer, s);
                        Unsafe.Add(ref blockDataRef, zigzag[i]) = (short)(s << low);
                    }
                    else
                    {
                        if (r != 15)
                        {
                            this.eobrun = 1 << r;
                            if (r != 0)
                            {
                                buffer.CheckBits();
                                this.eobrun += GetBits(buffer, r);
                            }

                            --this.eobrun;
                            break;
                        }
                    }
                }
            }
            else
            {
                // Refinement scan for these AC coefficients
                this.DecodeBlockProgressiveACRefined(ref blockDataRef, ref acTable);
            }
        }

        private void DecodeBlockProgressiveACRefined(ref short blockDataRef, ref HuffmanTable acTable)
        {
            // Refinement scan for these AC coefficients
            HuffmanScanBuffer buffer = this.scanBuffer;
            ref ZigZag zigzag = ref this.dctZigZag;
            int start = this.spectralStart;
            int end = this.spectralEnd;

            int p1 = 1 << this.successiveLow;
            int m1 = (-1) << this.successiveLow;

            int k = start;

            if (this.eobrun == 0)
            {
                for (; k <= end; k++)
                {
                    int s = this.DecodeHuffman(buffer, ref acTable);
                    int r = s >> 4;
                    s &= 15;

                    if (s != 0)
                    {
                        buffer.CheckBits();
                        if (GetBits(buffer, 1) != 0)
                        {
                            s = p1;
                        }
                        else
                        {
                            s = m1;
                        }
                    }
                    else
                    {
                        if (r != 15)
                        {
                            this.eobrun = 1 << r;

                            if (r != 0)
                            {
                                buffer.CheckBits();
                                this.eobrun += GetBits(buffer, r);
                            }

                            break;
                        }
                    }

                    do
                    {
                        ref short coef = ref Unsafe.Add(ref blockDataRef, zigzag[k]);
                        if (coef != 0)
                        {
                            buffer.CheckBits();
                            if (GetBits(buffer, 1) != 0)
                            {
                                if ((coef & p1) == 0)
                                {
                                    coef += (short)(coef >= 0 ? p1 : m1);
                                }
                            }
                        }
                        else
                        {
                            if (--r < 0)
                            {
                                break;
                            }
                        }

                        k++;
                    }
                    while (k <= end);

                    if ((s != 0) && (k < 64))
                    {
                        Unsafe.Add(ref blockDataRef, zigzag[k]) = (short)s;
                    }
                }
            }

            if (this.eobrun > 0)
            {
                for (; k <= end; k++)
                {
                    ref short coef = ref Unsafe.Add(ref blockDataRef, zigzag[k]);

                    if (coef != 0)
                    {
                        buffer.CheckBits();
                        if (GetBits(buffer, 1) != 0)
                        {
                            if ((coef & p1) == 0)
                            {
                                coef += (short)(coef >= 0 ? p1 : m1);
                            }
                        }
                    }
                }

                --this.eobrun;
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private void Reset()
        {
            for (int i = 0; i < this.components.Length; i++)
            {
                this.components[i].DcPredictor = 0;
            }

            this.eobrun = 0;
            this.scanBuffer.Reset();
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private bool HandleRestart()
        {
            if (this.restartInterval > 0 && (--this.todo) == 0)
            {
                this.todo = this.restartInterval;

                if (this.scanBuffer.HasRestart())
                {
                    this.Reset();
                    return true;
                }

                if (this.scanBuffer.Marker != JpegConstants.Markers.XFF)
                {
                    this.stream.Position = this.scanBuffer.MarkerPosition;
                    this.Reset();
                    return true;
                }
            }

            return false;
        }

        internal sealed class HuffmanScanBuffer
        {
            private readonly DoubleBufferedStreamReader stream;

            public HuffmanScanBuffer(DoubleBufferedStreamReader stream)
            {
                this.stream = stream;
                this.Reset();
            }

            /// <summary>
            /// Gets or sets the current, if any, marker in the input stream.
            /// </summary>
            public byte Marker { get; set; }

            /// <summary>
            /// Gets or sets the opening position of an identified marker.
            /// </summary>
            public long MarkerPosition { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether we have a bad marker, I.E. One that is not between RST0 and RST7
            /// </summary>
            public bool BadMarker { get; set; }

            /// <summary>
            /// Gets or sets the entropy encoded code buffer.
            /// </summary>
            public ulong Data { get; set; }

            /// <summary>
            /// Gets or sets the number of valid bits left to read in the buffer.
            /// </summary>
            public int Remain { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether there is more data to pull from the stream for the current mcu.
            /// </summary>
            public bool NoMore { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether we have prematurely reached the end of the file.
            /// </summary>
            public bool Eof { get; set; }

            [MethodImpl(InliningOptions.ShortMethod)]
            public void CheckBits()
            {
                if (this.Remain < 16)
                {
                    this.FillBuffer();
                }
            }

            [MethodImpl(InliningOptions.ShortMethod)]
            public void Reset()
            {
                this.Data = 0ul;
                this.Remain = 0;
                this.Marker = JpegConstants.Markers.XFF;
                this.MarkerPosition = 0;
                this.BadMarker = false;
                this.NoMore = false;
                this.Eof = false;
            }

            [MethodImpl(InliningOptions.ShortMethod)]
            public bool HasRestart()
            {
                byte m = this.Marker;
                return m >= JpegConstants.Markers.RST0 && m <= JpegConstants.Markers.RST7;
            }

            [MethodImpl(InliningOptions.ShortMethod)]
            public void FillBuffer()
            {
                // Attempt to load at least the minimum number of required bits into the buffer.
                // We fail to do so only if we hit a marker or reach the end of the input stream.
                // TODO: Investigate whether a faster path can be taken here.
                this.Remain += 48;
                this.Data = (this.Data << 48) | this.GetBytes();
            }

            [MethodImpl(InliningOptions.ColdPath)]
            private ulong GetBytes()
            {
                ulong temp = 0;
                for (int i = 0; i < 6; i++)
                {
                    int b = this.NoMore ? 0 : this.stream.ReadByte();

                    if (b == -1)
                    {
                        // We've encountered the end of the file stream which means there's no EOI marker in the image
                        // or the SOS marker has the wrong dimensions set.
                        this.Eof = true;
                        b = 0;
                    }

                    // Found a marker.
                    if (b == JpegConstants.Markers.XFF)
                    {
                        this.MarkerPosition = this.stream.Position - 1;
                        int c = this.stream.ReadByte();
                        while (c == JpegConstants.Markers.XFF)
                        {
                            c = this.stream.ReadByte();

                            if (c == -1)
                            {
                                this.Eof = true;
                                c = 0;
                                break;
                            }
                        }

                        if (c != 0)
                        {
                            this.Marker = (byte)c;
                            this.NoMore = true;
                            if (!this.HasRestart())
                            {
                                this.BadMarker = true;
                            }
                        }
                    }

                    temp = (temp << 8) | (ulong)(long)b;
                }

                return temp;
            }
        }
    }
}
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
        private readonly FastACTable[] fastACTables;

        private readonly JpegStreamReader stream;
        private readonly JpegBuffer jpegBuffer;
        private readonly JpegComponent[] components;
        private readonly ZigZag dctZigZag;

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
        // private int eobrun;

        /// <summary>
        /// Initializes a new instance of the <see cref="HuffmanScanDecoder"/> class.
        /// </summary>
        /// <param name="stream">The input stream.</param>
        /// <param name="frame">The image frame.</param>
        /// <param name="dcHuffmanTables">The DC Huffman tables.</param>
        /// <param name="acHuffmanTables">The AC Huffman tables.</param>
        /// <param name="fastACTables">The fast AC decoding tables.</param>
        /// <param name="componentsLength">The length of the components. Different to the array length.</param>
        /// <param name="restartInterval">The reset interval.</param>
        /// <param name="spectralStart">The spectral selection start.</param>
        /// <param name="spectralEnd">The spectral selection end.</param>
        /// <param name="successiveHigh">The successive approximation bit high end.</param>
        /// <param name="successiveLow">The successive approximation bit low end.</param>
        public HuffmanScanDecoder(
            JpegStreamReader stream,
            JpegFrame frame,
            HuffmanTable[] dcHuffmanTables,
            HuffmanTable[] acHuffmanTables,
            FastACTable[] fastACTables,
            int componentsLength,
            int restartInterval,
            int spectralStart,
            int spectralEnd,
            int successiveHigh,
            int successiveLow)
        {
            this.dctZigZag = ZigZag.CreateUnzigTable();
            this.stream = stream;
            this.jpegBuffer = new JpegBuffer(stream);
            this.frame = frame;
            this.dcHuffmanTables = dcHuffmanTables;
            this.acHuffmanTables = acHuffmanTables;
            this.fastACTables = fastACTables;
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
            // this.Reset();
            if (!this.frame.Progressive)
            {
                this.ParseBaselineData();
            }
            else
            {
                this.ParseProgressiveData();
            }

            if (this.jpegBuffer.BadMarker)
            {
                this.stream.Position = this.jpegBuffer.MarkerPosition;
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
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

                ref FastACTable fastAcTable = ref this.fastACTables[component.ACHuffmanTableId];
                fastAcTable.Derive(ref acHuffmanTable);
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
                        ref FastACTable fastAcTable = ref this.fastACTables[component.ACHuffmanTableId];
                        ref short fastACRef = ref fastAcTable.Lookahead[0];

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
                                if (this.jpegBuffer.Eof)
                                {
                                    return;
                                }

                                int blockCol = (mcuCol * h) + x;

                                this.DecodeBlockBaseline(
                                    component,
                                    ref Unsafe.Add(ref blockRef, blockCol),
                                    ref dcHuffmanTable,
                                    ref acHuffmanTable,
                                    ref fastACRef);
                            }
                        }
                    }

                    // After all interleaved components, that's an interleaved MCU,
                    // so now count down the restart interval
                    mcu++;
                    this.HandleRestart();

                    // if (!this.ContinueOnMcuComplete())
                    // {
                    //    return;
                    // }
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

            ref FastACTable fastAcTable = ref this.fastACTables[component.ACHuffmanTableId];
            fastAcTable.Derive(ref acHuffmanTable);
            ref short fastACRef = ref fastAcTable.Lookahead[0];

            int mcu = 0;
            for (int j = 0; j < h; j++)
            {
                Span<Block8x8> blockSpan = component.SpectralBlocks.GetRowSpan(j);
                ref Block8x8 blockRef = ref MemoryMarshal.GetReference(blockSpan);

                for (int i = 0; i < w; i++)
                {
                    if (this.jpegBuffer.Eof)
                    {
                        return;
                    }

                    this.DecodeBlockBaseline(
                        component,
                        ref Unsafe.Add(ref blockRef, i),
                        ref dcHuffmanTable,
                        ref acHuffmanTable,
                        ref fastACRef);

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
                                if (this.jpegBuffer.Eof)
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

                    // if (!this.ContinueOnMcuComplete())
                    // {
                    //    return;
                    // }
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
                        if (this.jpegBuffer.Eof)
                        {
                            return;
                        }

                        this.DecodeBlockProgressiveDC(
                            component,
                            ref Unsafe.Add(ref blockRef, i),
                            ref dcHuffmanTable);

                        // Every data block is an MCU, so countdown the restart interval
                        mcu++;
                        if (!this.ContinueOnMcuComplete())
                        {
                            return;
                        }
                    }
                }
            }
            else
            {
                ref HuffmanTable acHuffmanTable = ref this.acHuffmanTables[component.ACHuffmanTableId];
                acHuffmanTable.Configure();

                ref FastACTable fastAcTable = ref this.fastACTables[component.ACHuffmanTableId];
                fastAcTable.Derive(ref acHuffmanTable);
                ref short fastACRef = ref fastAcTable.Lookahead[0];

                int mcu = 0;
                for (int j = 0; j < h; j++)
                {
                    Span<Block8x8> blockSpan = component.SpectralBlocks.GetRowSpan(j);
                    ref Block8x8 blockRef = ref MemoryMarshal.GetReference(blockSpan);

                    for (int i = 0; i < w; i++)
                    {
                        if (this.jpegBuffer.Eof)
                        {
                            return;
                        }

                        this.DecodeBlockProgressiveAC(
                            ref Unsafe.Add(ref blockRef, i),
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
        }

        private void DecodeBlockBaseline(
            JpegComponent component,
            ref Block8x8 block,
            ref HuffmanTable dcTable,
            ref HuffmanTable acTable,
            ref short fastACRef)
        {
            ref short blockDataRef = ref Unsafe.As<Block8x8, short>(ref block);
            JpegBuffer buffer = this.jpegBuffer;

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
                    Unsafe.Add(ref blockDataRef, this.dctZigZag[i++]) = (short)s;
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
        private unsafe int DecodeHuffman(JpegBuffer buffer, ref HuffmanTable h)
        {
            buffer.CheckBits();
            int v = PeekBits(buffer, JpegHuffLookupBits);
            int symbol = h.LookaheadValue[v];
            int size = h.LookaheadSize[v];

            if (size == JpegHuffSlowBits)
            {
                ulong x = this.jpegBuffer.Data << (JpegRegisterSize - this.jpegBuffer.Remain);
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
        private static int Receive(JpegBuffer buffer, int nbits)
        {
            buffer.CheckBits();
            return Extend(GetBits(buffer, nbits), nbits);
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static int Extend(int v, int nbits) => v - ((((v + v) >> nbits) - 1) & ((1 << nbits) - 1));

        [MethodImpl(InliningOptions.ShortMethod)]
        private static int GetBits(JpegBuffer buffer, int nbits) => (int)ExtractBits(buffer.Data, buffer.Remain -= nbits, nbits);

        [MethodImpl(InliningOptions.ShortMethod)]
        private static int PeekBits(JpegBuffer buffer, int nbits) => (int)ExtractBits(buffer.Data, buffer.Remain - nbits, nbits);

        [MethodImpl(InliningOptions.ShortMethod)]
        private static ulong ExtractBits(ulong value, int offset, int size) => (value >> offset) & (ulong)((1 << size) - 1);

        private void DecodeBlockProgressiveDC(
            JpegComponent component,
            ref Block8x8 block,
            ref HuffmanTable dcTable)
        {
        }

        private void DecodeBlockProgressiveAC(
            ref Block8x8 block,
            ref HuffmanTable acTable,
            ref short fastACRef)
        {
        }

        private void DecodeBlockProgressiveACRefined(ref short blockDataRef, ref HuffmanTable acTable)
        {
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private bool ContinueOnMcuComplete()
        {
            // TODO: Delete me.
            if (--this.todo > 0)
            {
                return true;
            }

            // if (this.jpegBuffer.remain < 56)
            // {
            //    this.jpegBuffer.FillBuffer();
            // }
            //
            // If it's NOT a restart, then just bail, so we get corrupt data rather than no data.
            // Reset the stream to before any bad markers to ensure we can read successive segments.
            if (this.jpegBuffer.BadMarker)
            {
                this.stream.Position = this.jpegBuffer.MarkerPosition;
            }

            if (!this.jpegBuffer.HasRestart())
            {
                return false;
            }

            this.Reset();

            return true;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private void Reset()
        {
            for (int i = 0; i < this.components.Length; i++)
            {
                this.components[i].DcPredictor = 0;
            }

            // this.eobrun = 0;
            this.jpegBuffer.Reset();
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private bool HandleRestart()
        {
            if (this.restartInterval > 0 && (--this.todo) == 0)
            {
                this.todo = this.restartInterval;

                if (this.jpegBuffer.HasRestart())
                {
                    this.stream.Position = this.jpegBuffer.MarkerPosition + 2;
                    this.Reset();
                    return true;
                }

                if (this.jpegBuffer.Marker != JpegConstants.Markers.XFF)
                {
                    this.stream.Position = this.jpegBuffer.MarkerPosition;
                    this.Reset();
                    return true;
                }
            }

            return false;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static bool IsRestartMarker(byte m)
        {
            return m >= JpegConstants.Markers.RST0 && m <= JpegConstants.Markers.RST7;
        }

        internal class JpegBuffer
        {
            private readonly JpegStreamReader stream;

            public JpegBuffer(JpegStreamReader stream)
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
            private ulong Bytes(int n)
            {
                ulong temp = 0;
                long end = this.stream.Length - 1;
                long ptr = this.stream.Position;

                for (int i = 0; i < n; ++i)
                {
                    int a = 0;

                    if (ptr < end)
                    {
                        a = this.stream.ReadByte();
                        ptr++;
                    }

                    if (a == JpegConstants.Markers.XFF)
                    {
                        this.MarkerPosition = this.stream.Position - 1;
                        int b = 0;

                        if (ptr < end)
                        {
                            b = this.stream.ReadByte();
                            ptr++;
                        }

                        if (b != 0)
                        {
                            // Found a marker; keep returning zero until it has been processed
                            this.Marker = (byte)b;
                            ptr -= 2;
                            this.stream.Position -= 2;
                            a = 0;
                        }
                    }

                    temp = (temp << 8) | (ulong)(long)a;
                }

                return temp;
            }

            [MethodImpl(InliningOptions.ColdPath)]
            public void FillBuffer()
            {
                // Attempt to load at least the minimum number of required bits into the buffer.
                // We fail to do so only if we hit a marker or reach the end of the input stream.
                //
                // TODO: JpegStreamReader we could keep track of marker positions within the
                // stream and we could use that knowledge to do a fast cast of bytes to a ulong
                // avoiding this loop.
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

                this.Remain += 48;

                // ulong temp = this.Bytes(6);
                this.Data = (this.Data << 48) | temp;
            }
        }
    }
}
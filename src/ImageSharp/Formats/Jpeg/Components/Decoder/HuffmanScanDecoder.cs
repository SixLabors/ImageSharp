// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using SixLabors.ImageSharp.IO;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder
{
    /// <summary>
    /// Decodes the Huffman encoded spectral scan.
    /// Originally ported from <see href="https://github.com/t0rakka/mango"/>
    /// with additional fixes for both performance and common encoding errors.
    /// </summary>
    internal class HuffmanScanDecoder
    {
        private readonly JpegFrame frame;
        private readonly HuffmanTable[] dcHuffmanTables;
        private readonly HuffmanTable[] acHuffmanTables;
        private readonly BufferedReadStream stream;
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

        // The unzig data.
        private ZigZag dctZigZag;

        private HuffmanScanBuffer scanBuffer;

        private CancellationToken cancellationToken;

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
        /// <param name="cancellationToken">The token to monitor cancellation.</param>
        public HuffmanScanDecoder(
            BufferedReadStream stream,
            JpegFrame frame,
            HuffmanTable[] dcHuffmanTables,
            HuffmanTable[] acHuffmanTables,
            int componentsLength,
            int restartInterval,
            int spectralStart,
            int spectralEnd,
            int successiveHigh,
            int successiveLow,
            CancellationToken cancellationToken)
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
            this.cancellationToken = cancellationToken;
        }

        /// <summary>
        /// Decodes the entropy coded data.
        /// </summary>
        public void ParseEntropyCodedData()
        {
            this.cancellationToken.ThrowIfCancellationRequested();

            if (!this.frame.Progressive)
            {
                this.ParseBaselineData();
            }
            else
            {
                this.ParseProgressiveData();
            }

            if (this.scanBuffer.HasBadMarker())
            {
                this.stream.Position = this.scanBuffer.MarkerPosition;
            }
        }

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
            ref HuffmanScanBuffer buffer = ref this.scanBuffer;

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
                this.cancellationToken.ThrowIfCancellationRequested();

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
                                if (buffer.NoData)
                                {
                                    return;
                                }

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

        private void ParseBaselineDataNonInterleaved()
        {
            JpegComponent component = this.components[this.frame.ComponentOrder[0]];
            ref HuffmanScanBuffer buffer = ref this.scanBuffer;

            int w = component.WidthInBlocks;
            int h = component.HeightInBlocks;

            ref HuffmanTable dcHuffmanTable = ref this.dcHuffmanTables[component.DCHuffmanTableId];
            ref HuffmanTable acHuffmanTable = ref this.acHuffmanTables[component.ACHuffmanTableId];
            dcHuffmanTable.Configure();
            acHuffmanTable.Configure();

            for (int j = 0; j < h; j++)
            {
                this.cancellationToken.ThrowIfCancellationRequested();
                Span<Block8x8> blockSpan = component.SpectralBlocks.GetRowSpan(j);
                ref Block8x8 blockRef = ref MemoryMarshal.GetReference(blockSpan);

                for (int i = 0; i < w; i++)
                {
                    if (buffer.NoData)
                    {
                        return;
                    }

                    this.DecodeBlockBaseline(
                        component,
                        ref Unsafe.Add(ref blockRef, i),
                        ref dcHuffmanTable,
                        ref acHuffmanTable);

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
            ref HuffmanScanBuffer buffer = ref this.scanBuffer;

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
                                if (buffer.NoData)
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

        private void ParseProgressiveDataNonInterleaved()
        {
            JpegComponent component = this.components[this.frame.ComponentOrder[0]];
            ref HuffmanScanBuffer buffer = ref this.scanBuffer;

            int w = component.WidthInBlocks;
            int h = component.HeightInBlocks;

            if (this.spectralStart == 0)
            {
                ref HuffmanTable dcHuffmanTable = ref this.dcHuffmanTables[component.DCHuffmanTableId];
                dcHuffmanTable.Configure();

                for (int j = 0; j < h; j++)
                {
                    this.cancellationToken.ThrowIfCancellationRequested();

                    Span<Block8x8> blockSpan = component.SpectralBlocks.GetRowSpan(j);
                    ref Block8x8 blockRef = ref MemoryMarshal.GetReference(blockSpan);

                    for (int i = 0; i < w; i++)
                    {
                        if (buffer.NoData)
                        {
                            return;
                        }

                        this.DecodeBlockProgressiveDC(
                            component,
                            ref Unsafe.Add(ref blockRef, i),
                            ref dcHuffmanTable);

                        this.HandleRestart();
                    }
                }
            }
            else
            {
                ref HuffmanTable acHuffmanTable = ref this.acHuffmanTables[component.ACHuffmanTableId];
                acHuffmanTable.Configure();

                for (int j = 0; j < h; j++)
                {
                    this.cancellationToken.ThrowIfCancellationRequested();

                    Span<Block8x8> blockSpan = component.SpectralBlocks.GetRowSpan(j);
                    ref Block8x8 blockRef = ref MemoryMarshal.GetReference(blockSpan);

                    for (int i = 0; i < w; i++)
                    {
                        if (buffer.NoData)
                        {
                            return;
                        }

                        this.DecodeBlockProgressiveAC(
                            ref Unsafe.Add(ref blockRef, i),
                            ref acHuffmanTable);

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
            ref HuffmanScanBuffer buffer = ref this.scanBuffer;
            ref ZigZag zigzag = ref this.dctZigZag;

            // DC
            int t = buffer.DecodeHuffman(ref dcTable);
            if (t != 0)
            {
                t = buffer.Receive(t);
            }

            t += component.DcPredictor;
            component.DcPredictor = t;
            blockDataRef = (short)t;

            // AC
            for (int i = 1; i < 64;)
            {
                int s = buffer.DecodeHuffman(ref acTable);

                int r = s >> 4;
                s &= 15;

                if (s != 0)
                {
                    i += r;
                    s = buffer.Receive(s);
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

        private void DecodeBlockProgressiveDC(JpegComponent component, ref Block8x8 block, ref HuffmanTable dcTable)
        {
            ref short blockDataRef = ref Unsafe.As<Block8x8, short>(ref block);
            ref HuffmanScanBuffer buffer = ref this.scanBuffer;

            if (this.successiveHigh == 0)
            {
                // First scan for DC coefficient, must be first
                int s = buffer.DecodeHuffman(ref dcTable);
                if (s != 0)
                {
                    s = buffer.Receive(s);
                }

                s += component.DcPredictor;
                component.DcPredictor = s;
                blockDataRef = (short)(s << this.successiveLow);
            }
            else
            {
                // Refinement scan for DC coefficient
                buffer.CheckBits();
                blockDataRef |= (short)(buffer.GetBits(1) << this.successiveLow);
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

                ref HuffmanScanBuffer buffer = ref this.scanBuffer;
                ref ZigZag zigzag = ref this.dctZigZag;
                int start = this.spectralStart;
                int end = this.spectralEnd;
                int low = this.successiveLow;

                for (int i = start; i <= end; ++i)
                {
                    int s = buffer.DecodeHuffman(ref acTable);
                    int r = s >> 4;
                    s &= 15;

                    i += r;

                    if (s != 0)
                    {
                        s = buffer.Receive(s);
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
                                this.eobrun += buffer.GetBits(r);
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
            ref HuffmanScanBuffer buffer = ref this.scanBuffer;
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
                    int s = buffer.DecodeHuffman(ref acTable);
                    int r = s >> 4;
                    s &= 15;

                    if (s != 0)
                    {
                        buffer.CheckBits();
                        if (buffer.GetBits(1) != 0)
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
                                this.eobrun += buffer.GetBits(r);
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
                            if (buffer.GetBits(1) != 0)
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
                        if (buffer.GetBits(1) != 0)
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
                if (this.scanBuffer.Marker == JpegConstants.Markers.XFF)
                {
                    if (!this.scanBuffer.FindNextMarker())
                    {
                        return false;
                    }
                }

                this.todo = this.restartInterval;

                if (this.scanBuffer.HasRestartMarker())
                {
                    this.Reset();
                    return true;
                }

                if (this.scanBuffer.HasBadMarker())
                {
                    this.stream.Position = this.scanBuffer.MarkerPosition;
                    this.Reset();
                    return true;
                }
            }

            return false;
        }
    }
}

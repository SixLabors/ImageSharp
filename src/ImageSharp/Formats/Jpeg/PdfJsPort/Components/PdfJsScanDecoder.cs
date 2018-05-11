// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
#if DEBUG
using System.Diagnostics;
#endif
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Jpeg.Common;

namespace SixLabors.ImageSharp.Formats.Jpeg.PdfJsPort.Components
{
    /// <summary>
    /// Provides the means to decode a spectral scan
    /// </summary>
    internal struct PdfJsScanDecoder
    {
        private ZigZag dctZigZag;

        private byte[] markerBuffer;

        private int mcuToRead;

        private int mcusPerLine;

        private int mcu;

        private int bitsData;

        private int bitsCount;

        private int specStart;

        private int specEnd;

        private int eobrun;

        private int compIndex;

        private int successiveState;

        private int successiveACState;

        private int successiveACNextValue;

        private bool endOfStreamReached;

        private bool unexpectedMarkerReached;

        /// <summary>
        /// Decodes the spectral scan
        /// </summary>
        /// <param name="frame">The image frame</param>
        /// <param name="stream">The input stream</param>
        /// <param name="dcHuffmanTables">The DC Huffman tables</param>
        /// <param name="acHuffmanTables">The AC Huffman tables</param>
        /// <param name="components">The scan components</param>
        /// <param name="componentIndex">The component index within the array</param>
        /// <param name="componentsLength">The length of the components. Different to the array length</param>
        /// <param name="resetInterval">The reset interval</param>
        /// <param name="spectralStart">The spectral selection start</param>
        /// <param name="spectralEnd">The spectral selection end</param>
        /// <param name="successivePrev">The successive approximation bit high end</param>
        /// <param name="successive">The successive approximation bit low end</param>
        public void DecodeScan(
            PdfJsFrame frame,
            DoubleBufferedStreamReader stream,
            PdfJsHuffmanTables dcHuffmanTables,
            PdfJsHuffmanTables acHuffmanTables,
            PdfJsFrameComponent[] components,
            int componentIndex,
            int componentsLength,
            ushort resetInterval,
            int spectralStart,
            int spectralEnd,
            int successivePrev,
            int successive)
        {
            this.dctZigZag = ZigZag.CreateUnzigTable();
            this.markerBuffer = new byte[2];
            this.compIndex = componentIndex;
            this.specStart = spectralStart;
            this.specEnd = spectralEnd;
            this.successiveState = successive;
            this.endOfStreamReached = false;
            this.unexpectedMarkerReached = false;

            bool progressive = frame.Progressive;
            this.mcusPerLine = frame.McusPerLine;

            this.mcu = 0;
            int mcuExpected;
            if (componentsLength == 1)
            {
                mcuExpected = components[this.compIndex].WidthInBlocks * components[this.compIndex].HeightInBlocks;
            }
            else
            {
                mcuExpected = this.mcusPerLine * frame.McusPerColumn;
            }

            while (this.mcu < mcuExpected)
            {
                // Reset interval stuff
                this.mcuToRead = resetInterval != 0 ? Math.Min(mcuExpected - this.mcu, resetInterval) : mcuExpected;
                for (int i = 0; i < components.Length; i++)
                {
                    PdfJsFrameComponent c = components[i];
                    c.Pred = 0;
                }

                this.eobrun = 0;

                if (!progressive)
                {
                    this.DecodeScanBaseline(dcHuffmanTables, acHuffmanTables, components, componentsLength, stream);
                }
                else
                {
                    bool isAc = this.specStart != 0;
                    bool isFirst = successivePrev == 0;
                    PdfJsHuffmanTables huffmanTables = isAc ? acHuffmanTables : dcHuffmanTables;
                    this.DecodeScanProgressive(huffmanTables, isAc, isFirst, components, componentsLength, stream);
                }

                // Reset
                // TODO: I do not understand why these values are reset? We should surely be tracking the bits across mcu's?
                this.bitsCount = 0;
                this.bitsData = 0;
                this.unexpectedMarkerReached = false;

                // Some images include more scan blocks than expected, skip past those and
                // attempt to find the next valid marker
                PdfJsFileMarker fileMarker = PdfJsJpegDecoderCore.FindNextFileMarker(this.markerBuffer, stream);
                byte marker = fileMarker.Marker;

                // RSTn - We've already read the bytes and altered the position so no need to skip
                if (marker >= JpegConstants.Markers.RST0 && marker <= JpegConstants.Markers.RST7)
                {
                    continue;
                }

                if (!fileMarker.Invalid)
                {
                    // We've found a valid marker.
                    // Rewind the stream to the position of the marker and break
                    stream.Position = fileMarker.Position;
                    break;
                }

#if DEBUG
                Debug.WriteLine($"DecodeScan - Unexpected MCU data at {stream.Position}, next marker is: {fileMarker.Marker:X}");
#endif
            }
        }

        private void DecodeScanBaseline(
            PdfJsHuffmanTables dcHuffmanTables,
            PdfJsHuffmanTables acHuffmanTables,
            PdfJsFrameComponent[] components,
            int componentsLength,
            DoubleBufferedStreamReader stream)
        {
            if (componentsLength == 1)
            {
                PdfJsFrameComponent component = components[this.compIndex];
                ref short blockDataRef = ref MemoryMarshal.GetReference(MemoryMarshal.Cast<Block8x8, short>(component.SpectralBlocks.Span));
                ref PdfJsHuffmanTable dcHuffmanTable = ref dcHuffmanTables[component.DCHuffmanTableId];
                ref PdfJsHuffmanTable acHuffmanTable = ref acHuffmanTables[component.ACHuffmanTableId];

                for (int n = 0; n < this.mcuToRead; n++)
                {
                    if (this.endOfStreamReached || this.unexpectedMarkerReached)
                    {
                        continue;
                    }

                    this.DecodeBlockBaseline(ref dcHuffmanTable, ref acHuffmanTable, component, ref blockDataRef, stream);
                    this.mcu++;
                }
            }
            else
            {
                for (int n = 0; n < this.mcuToRead; n++)
                {
                    for (int i = 0; i < componentsLength; i++)
                    {
                        PdfJsFrameComponent component = components[i];
                        ref short blockDataRef = ref MemoryMarshal.GetReference(MemoryMarshal.Cast<Block8x8, short>(component.SpectralBlocks.Span));
                        ref PdfJsHuffmanTable dcHuffmanTable = ref dcHuffmanTables[component.DCHuffmanTableId];
                        ref PdfJsHuffmanTable acHuffmanTable = ref acHuffmanTables[component.ACHuffmanTableId];
                        int h = component.HorizontalSamplingFactor;
                        int v = component.VerticalSamplingFactor;

                        for (int j = 0; j < v; j++)
                        {
                            for (int k = 0; k < h; k++)
                            {
                                if (this.endOfStreamReached || this.unexpectedMarkerReached)
                                {
                                    continue;
                                }

                                this.DecodeMcuBaseline(ref dcHuffmanTable, ref acHuffmanTable, component, ref blockDataRef, j, k, stream);
                            }
                        }
                    }

                    this.mcu++;
                }
            }
        }

        private void DecodeScanProgressive(
            PdfJsHuffmanTables huffmanTables,
            bool isAC,
            bool isFirst,
            PdfJsFrameComponent[] components,
            int componentsLength,
            DoubleBufferedStreamReader stream)
        {
            if (componentsLength == 1)
            {
                PdfJsFrameComponent component = components[this.compIndex];
                ref short blockDataRef = ref MemoryMarshal.GetReference(MemoryMarshal.Cast<Block8x8, short>(component.SpectralBlocks.Span));
                ref PdfJsHuffmanTable huffmanTable = ref huffmanTables[isAC ? component.ACHuffmanTableId : component.DCHuffmanTableId];

                for (int n = 0; n < this.mcuToRead; n++)
                {
                    if (this.endOfStreamReached || this.unexpectedMarkerReached)
                    {
                        continue;
                    }

                    if (isAC)
                    {
                        if (isFirst)
                        {
                            this.DecodeBlockACFirst(ref huffmanTable, component, ref blockDataRef, stream);
                        }
                        else
                        {
                            this.DecodeBlockACSuccessive(ref huffmanTable, component, ref blockDataRef, stream);
                        }
                    }
                    else
                    {
                        if (isFirst)
                        {
                            this.DecodeBlockDCFirst(ref huffmanTable, component, ref blockDataRef, stream);
                        }
                        else
                        {
                            this.DecodeBlockDCSuccessive(component, ref blockDataRef, stream);
                        }
                    }

                    this.mcu++;
                }
            }
            else
            {
                for (int n = 0; n < this.mcuToRead; n++)
                {
                    for (int i = 0; i < componentsLength; i++)
                    {
                        PdfJsFrameComponent component = components[i];
                        ref short blockDataRef = ref MemoryMarshal.GetReference(MemoryMarshal.Cast<Block8x8, short>(component.SpectralBlocks.Span));
                        ref PdfJsHuffmanTable huffmanTable = ref huffmanTables[isAC ? component.ACHuffmanTableId : component.DCHuffmanTableId];
                        int h = component.HorizontalSamplingFactor;
                        int v = component.VerticalSamplingFactor;

                        for (int j = 0; j < v; j++)
                        {
                            for (int k = 0; k < h; k++)
                            {
                                // No need to continue here.
                                if (this.endOfStreamReached || this.unexpectedMarkerReached)
                                {
                                    break;
                                }

                                if (isAC)
                                {
                                    if (isFirst)
                                    {
                                        this.DecodeMcuACFirst(ref huffmanTable, component, ref blockDataRef, j, k, stream);
                                    }
                                    else
                                    {
                                        this.DecodeMcuACSuccessive(ref huffmanTable, component, ref blockDataRef, j, k, stream);
                                    }
                                }
                                else
                                {
                                    if (isFirst)
                                    {
                                        this.DecodeMcuDCFirst(ref huffmanTable, component, ref blockDataRef, j, k, stream);
                                    }
                                    else
                                    {
                                        this.DecodeMcuDCSuccessive(component, ref blockDataRef, j, k, stream);
                                    }
                                }
                            }
                        }
                    }

                    this.mcu++;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DecodeBlockBaseline(ref PdfJsHuffmanTable dcHuffmanTable, ref PdfJsHuffmanTable acHuffmanTable, PdfJsFrameComponent component, ref short blockDataRef, DoubleBufferedStreamReader stream)
        {
            int blockRow = this.mcu / component.WidthInBlocks;
            int blockCol = this.mcu % component.WidthInBlocks;
            int offset = component.GetBlockBufferOffset(blockRow, blockCol);
            this.DecodeBaseline(component, ref blockDataRef, offset, ref dcHuffmanTable, ref acHuffmanTable, stream);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DecodeMcuBaseline(ref PdfJsHuffmanTable dcHuffmanTable, ref PdfJsHuffmanTable acHuffmanTable, PdfJsFrameComponent component, ref short blockDataRef, int row, int col, DoubleBufferedStreamReader stream)
        {
            int mcuRow = this.mcu / this.mcusPerLine;
            int mcuCol = this.mcu % this.mcusPerLine;
            int blockRow = (mcuRow * component.VerticalSamplingFactor) + row;
            int blockCol = (mcuCol * component.HorizontalSamplingFactor) + col;
            int offset = component.GetBlockBufferOffset(blockRow, blockCol);
            this.DecodeBaseline(component, ref blockDataRef, offset, ref dcHuffmanTable, ref acHuffmanTable, stream);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DecodeBlockDCFirst(ref PdfJsHuffmanTable dcHuffmanTable, PdfJsFrameComponent component, ref short blockDataRef, DoubleBufferedStreamReader stream)
        {
            int blockRow = this.mcu / component.WidthInBlocks;
            int blockCol = this.mcu % component.WidthInBlocks;
            int offset = component.GetBlockBufferOffset(blockRow, blockCol);
            this.DecodeDCFirst(component, ref blockDataRef, offset, ref dcHuffmanTable, stream);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DecodeMcuDCFirst(ref PdfJsHuffmanTable dcHuffmanTable, PdfJsFrameComponent component, ref short blockDataRef, int row, int col, DoubleBufferedStreamReader stream)
        {
            int mcuRow = this.mcu / this.mcusPerLine;
            int mcuCol = this.mcu % this.mcusPerLine;
            int blockRow = (mcuRow * component.VerticalSamplingFactor) + row;
            int blockCol = (mcuCol * component.HorizontalSamplingFactor) + col;
            int offset = component.GetBlockBufferOffset(blockRow, blockCol);
            this.DecodeDCFirst(component, ref blockDataRef, offset, ref dcHuffmanTable, stream);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DecodeBlockDCSuccessive(PdfJsFrameComponent component, ref short blockDataRef, DoubleBufferedStreamReader stream)
        {
            int blockRow = this.mcu / component.WidthInBlocks;
            int blockCol = this.mcu % component.WidthInBlocks;
            int offset = component.GetBlockBufferOffset(blockRow, blockCol);
            this.DecodeDCSuccessive(component, ref blockDataRef, offset, stream);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DecodeMcuDCSuccessive(PdfJsFrameComponent component, ref short blockDataRef, int row, int col, DoubleBufferedStreamReader stream)
        {
            int mcuRow = this.mcu / this.mcusPerLine;
            int mcuCol = this.mcu % this.mcusPerLine;
            int blockRow = (mcuRow * component.VerticalSamplingFactor) + row;
            int blockCol = (mcuCol * component.HorizontalSamplingFactor) + col;
            int offset = component.GetBlockBufferOffset(blockRow, blockCol);
            this.DecodeDCSuccessive(component, ref blockDataRef, offset, stream);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DecodeBlockACFirst(ref PdfJsHuffmanTable acHuffmanTable, PdfJsFrameComponent component, ref short blockDataRef, DoubleBufferedStreamReader stream)
        {
            int blockRow = this.mcu / component.WidthInBlocks;
            int blockCol = this.mcu % component.WidthInBlocks;
            int offset = component.GetBlockBufferOffset(blockRow, blockCol);
            this.DecodeACFirst(ref blockDataRef, offset, ref acHuffmanTable, stream);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DecodeMcuACFirst(ref PdfJsHuffmanTable acHuffmanTable, PdfJsFrameComponent component, ref short blockDataRef, int row, int col, DoubleBufferedStreamReader stream)
        {
            int mcuRow = this.mcu / this.mcusPerLine;
            int mcuCol = this.mcu % this.mcusPerLine;
            int blockRow = (mcuRow * component.VerticalSamplingFactor) + row;
            int blockCol = (mcuCol * component.HorizontalSamplingFactor) + col;
            int offset = component.GetBlockBufferOffset(blockRow, blockCol);
            this.DecodeACFirst(ref blockDataRef, offset, ref acHuffmanTable, stream);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DecodeBlockACSuccessive(ref PdfJsHuffmanTable acHuffmanTable, PdfJsFrameComponent component, ref short blockDataRef, DoubleBufferedStreamReader stream)
        {
            int blockRow = this.mcu / component.WidthInBlocks;
            int blockCol = this.mcu % component.WidthInBlocks;
            int offset = component.GetBlockBufferOffset(blockRow, blockCol);
            this.DecodeACSuccessive(ref blockDataRef, offset, ref acHuffmanTable, stream);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DecodeMcuACSuccessive(ref PdfJsHuffmanTable acHuffmanTable, PdfJsFrameComponent component, ref short blockDataRef, int row, int col, DoubleBufferedStreamReader stream)
        {
            int mcuRow = this.mcu / this.mcusPerLine;
            int mcuCol = this.mcu % this.mcusPerLine;
            int blockRow = (mcuRow * component.VerticalSamplingFactor) + row;
            int blockCol = (mcuCol * component.HorizontalSamplingFactor) + col;
            int offset = component.GetBlockBufferOffset(blockRow, blockCol);
            this.DecodeACSuccessive(ref blockDataRef, offset, ref acHuffmanTable, stream);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryReadBit(DoubleBufferedStreamReader stream, out int bit)
        {
            if (this.bitsCount == 0)
            {
                if (!this.TryFillBits(stream))
                {
                    bit = 0;
                    return false;
                }
            }

            this.bitsCount--;
            bit = (this.bitsData >> this.bitsCount) & 1;
            return true;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool TryFillBits(DoubleBufferedStreamReader stream)
        {
            // TODO: Read more then 1 byte at a time.
            // In LibJpegTurbo this is be 25 bits (32-7) but I cannot get this to work
            // for some images, I'm assuming because I am crossing MCU boundaries and not maintining the correct buffer state.
            const int MinGetBits = 7;

            if (!this.unexpectedMarkerReached)
            {
                // Attempt to load to the minimum bit count.
                while (this.bitsCount < MinGetBits)
                {
                    int c = stream.ReadByte();

                    switch (c)
                    {
                        case -0x1:

                            // We've encountered the end of the file stream which means there's no EOI marker in the image.
                            this.endOfStreamReached = true;
                            return false;

                        case JpegConstants.Markers.XFF:
                            int nextByte = stream.ReadByte();

                            if (nextByte == -0x1)
                            {
                                this.endOfStreamReached = true;
                                return false;
                            }

                            if (nextByte != 0)
                            {
#if DEBUG
                                Debug.WriteLine($"DecodeScan - Unexpected marker {(c << 8) | nextByte:X} at {stream.Position}");
#endif

                                // We've encountered an unexpected marker. Reverse the stream and exit.
                                this.unexpectedMarkerReached = true;
                                stream.Position -= 2;

                                // TODO: double check we need this.
                                // Fill buffer with zero bits.
                                if (this.bitsCount == 0)
                                {
                                    this.bitsData <<= MinGetBits;
                                    this.bitsCount = MinGetBits;
                                }

                                return true;
                            }

                            break;
                    }

                    // OK, load the next byte into bitsData
                    this.bitsData = (this.bitsData << 8) | c;
                    this.bitsCount += 8;
                }
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int PeekBits(int count)
        {
            return this.bitsData >> (this.bitsCount - count) & ((1 << count) - 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DropBits(int count)
        {
            this.bitsCount -= count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryDecodeHuffman(ref PdfJsHuffmanTable tree, DoubleBufferedStreamReader stream, out short value)
        {
            value = -1;

            // TODO: Implement fast Huffman decoding.
            // In LibJpegTurbo a minimum of 25 bits (32-7) is collected from the stream
            // Then a LUT is used to avoid the loop when decoding the Huffman value.
            // using 3 methods: FillBits, PeekBits, and DropBits.
            // The LUT has been ported from LibJpegTurbo as has this code but it doesn't work.
            // this.TryFillBits(stream);
            //
            // const int LookAhead = 8;
            // int look = this.PeekBits(LookAhead);
            // look = tree.Lookahead[look];
            // int bits = look >> LookAhead;
            //
            // if (bits <= LookAhead)
            // {
            //    this.DropBits(bits);
            //    value = (short)(look & ((1 << LookAhead) - 1));
            //    return true;
            // }
            if (!this.TryReadBit(stream, out int bit))
            {
                return false;
            }

            short code = (short)bit;

            // "DECODE", section F.2.2.3, figure F.16, page 109 of T.81
            int i = 1;

            while (code > tree.MaxCode[i])
            {
                if (!this.TryReadBit(stream, out bit))
                {
                    return false;
                }

                code <<= 1;
                code |= (short)bit;
                i++;
            }

            int j = tree.ValOffset[i];
            value = tree.HuffVal[(j + code) & 0xFF];
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryReceive(int length, DoubleBufferedStreamReader stream, out int value)
        {
            value = 0;
            while (length > 0)
            {
                if (!this.TryReadBit(stream, out int bit))
                {
                    return false;
                }

                value = (value << 1) | bit;
                length--;
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryReceiveAndExtend(int length, DoubleBufferedStreamReader stream, out int value)
        {
            if (length == 1)
            {
                if (!this.TryReadBit(stream, out value))
                {
                    return false;
                }

                value = value == 1 ? 1 : -1;
            }
            else
            {
                if (!this.TryReceive(length, stream, out value))
                {
                    return false;
                }

                if (value < 1 << (length - 1))
                {
                    value += (-1 << length) + 1;
                }
            }

            return true;
        }

        private void DecodeBaseline(PdfJsFrameComponent component, ref short blockDataRef, int offset, ref PdfJsHuffmanTable dcHuffmanTable, ref PdfJsHuffmanTable acHuffmanTable, DoubleBufferedStreamReader stream)
        {
            if (!this.TryDecodeHuffman(ref dcHuffmanTable, stream, out short t))
            {
                return;
            }

            int diff = 0;
            if (t != 0)
            {
                if (!this.TryReceiveAndExtend(t, stream, out diff))
                {
                    return;
                }
            }

            Unsafe.Add(ref blockDataRef, offset) = (short)(component.Pred += diff);

            int k = 1;
            while (k < 64)
            {
                if (!this.TryDecodeHuffman(ref acHuffmanTable, stream, out short rs))
                {
                    return;
                }

                int s = rs & 15;
                int r = rs >> 4;

                if (s == 0)
                {
                    if (r < 15)
                    {
                        break;
                    }

                    k += 16;
                    continue;
                }

                k += r;

                byte z = this.dctZigZag[k];

                if (!this.TryReceiveAndExtend(s, stream, out int re))
                {
                    return;
                }

                Unsafe.Add(ref blockDataRef, offset + z) = (short)re;
                k++;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DecodeDCFirst(PdfJsFrameComponent component, ref short blockDataRef, int offset, ref PdfJsHuffmanTable dcHuffmanTable, DoubleBufferedStreamReader stream)
        {
            if (!this.TryDecodeHuffman(ref dcHuffmanTable, stream, out short t))
            {
                return;
            }

            int diff = 0;
            if (t != 0)
            {
                if (!this.TryReceiveAndExtend(t, stream, out diff))
                {
                    return;
                }
            }

            Unsafe.Add(ref blockDataRef, offset) = (short)(component.Pred += diff << this.successiveState);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DecodeDCSuccessive(PdfJsFrameComponent component, ref short blockDataRef, int offset, DoubleBufferedStreamReader stream)
        {
            if (!this.TryReadBit(stream, out int bit))
            {
                return;
            }

            Unsafe.Add(ref blockDataRef, offset) |= (short)(bit << this.successiveState);
        }

        private void DecodeACFirst(ref short blockDataRef, int offset, ref PdfJsHuffmanTable acHuffmanTable, DoubleBufferedStreamReader stream)
        {
            if (this.eobrun > 0)
            {
                this.eobrun--;
                return;
            }

            int k = this.specStart;
            int e = this.specEnd;
            while (k <= e)
            {
                if (!this.TryDecodeHuffman(ref acHuffmanTable, stream, out short rs))
                {
                    return;
                }

                int s = rs & 15;
                int r = rs >> 4;

                if (s == 0)
                {
                    if (r < 15)
                    {
                        if (!this.TryReceive(r, stream, out int eob))
                        {
                            return;
                        }

                        this.eobrun = eob + (1 << r) - 1;
                        break;
                    }

                    k += 16;
                    continue;
                }

                k += r;

                byte z = this.dctZigZag[k];

                if (!this.TryReceiveAndExtend(s, stream, out int v))
                {
                    return;
                }

                Unsafe.Add(ref blockDataRef, offset + z) = (short)(v * (1 << this.successiveState));
                k++;
            }
        }

        private void DecodeACSuccessive(ref short blockDataRef, int offset, ref PdfJsHuffmanTable acHuffmanTable, DoubleBufferedStreamReader stream)
        {
            int k = this.specStart;
            int e = this.specEnd;
            int r = 0;

            while (k <= e)
            {
                int offsetZ = offset + this.dctZigZag[k];
                ref short blockOffsetZRef = ref Unsafe.Add(ref blockDataRef, offsetZ);
                int sign = blockOffsetZRef < 0 ? -1 : 1;

                switch (this.successiveACState)
                {
                    case 0: // Initial state

                        if (!this.TryDecodeHuffman(ref acHuffmanTable, stream, out short rs))
                        {
                            return;
                        }

                        int s = rs & 15;
                        r = rs >> 4;
                        if (s == 0)
                        {
                            if (r < 15)
                            {
                                if (!this.TryReceive(r, stream, out int eob))
                                {
                                    return;
                                }

                                this.eobrun = eob + (1 << r);
                                this.successiveACState = 4;
                            }
                            else
                            {
                                r = 16;
                                this.successiveACState = 1;
                            }
                        }
                        else
                        {
                            if (s != 1)
                            {
                                throw new ImageFormatException("Invalid ACn encoding");
                            }

                            if (!this.TryReceiveAndExtend(s, stream, out int v))
                            {
                                return;
                            }

                            this.successiveACNextValue = v;
                            this.successiveACState = r > 0 ? 2 : 3;
                        }

                        continue;
                    case 1: // Skipping r zero items
                    case 2:
                        if (blockOffsetZRef != 0)
                        {
                            if (!this.TryReadBit(stream, out int bit))
                            {
                                return;
                            }

                            blockOffsetZRef += (short)(sign * (bit << this.successiveState));
                        }
                        else
                        {
                            r--;
                            if (r == 0)
                            {
                                this.successiveACState = this.successiveACState == 2 ? 3 : 0;
                            }
                        }

                        break;
                    case 3: // Set value for a zero item
                        if (blockOffsetZRef != 0)
                        {
                            if (!this.TryReadBit(stream, out int bit))
                            {
                                return;
                            }

                            blockOffsetZRef += (short)(sign * (bit << this.successiveState));
                        }
                        else
                        {
                            blockOffsetZRef = (short)(this.successiveACNextValue << this.successiveState);
                            this.successiveACState = 0;
                        }

                        break;
                    case 4: // Eob
                        if (blockOffsetZRef != 0)
                        {
                            if (!this.TryReadBit(stream, out int bit))
                            {
                                return;
                            }

                            blockOffsetZRef += (short)(sign * (bit << this.successiveState));
                        }

                        break;
                }

                k++;
            }

            if (this.successiveACState == 4)
            {
                this.eobrun--;
                if (this.eobrun == 0)
                {
                    this.successiveACState = 0;
                }
            }
        }
    }
}
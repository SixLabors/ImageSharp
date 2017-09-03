// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Jpeg.PdfJsPort.Components
{
    /// <summary>
    /// Provides the means to decode a spectral scan
    /// </summary>
    internal struct PdfJsScanDecoder
    {
        private byte[] markerBuffer;

        private int bitsData;

        private int bitsCount;

#pragma warning disable 414
        private int bitsUnRead;

        private int accumulator;
#pragma warning restore 414

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
            Stream stream,
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
            this.markerBuffer = new byte[2];
            this.compIndex = componentIndex;
            this.specStart = spectralStart;
            this.specEnd = spectralEnd;
            this.successiveState = successive;
            this.endOfStreamReached = false;
            this.unexpectedMarkerReached = false;

            bool progressive = frame.Progressive;
            int mcusPerLine = frame.McusPerLine;

            int mcu = 0;
            int mcuExpected;
            if (componentsLength == 1)
            {
                mcuExpected = components[this.compIndex].WidthInBlocks * components[this.compIndex].HeightInBlocks;
            }
            else
            {
                mcuExpected = mcusPerLine * frame.McusPerColumn;
            }

            PdfJsFileMarker fileMarker;
            while (mcu < mcuExpected)
            {
                // Reset interval stuff
                int mcuToRead = resetInterval != 0 ? Math.Min(mcuExpected - mcu, resetInterval) : mcuExpected;
                for (int i = 0; i < components.Length; i++)
                {
                    PdfJsFrameComponent c = components[i];
                    c.Pred = 0;
                }

                this.eobrun = 0;

                if (!progressive)
                {
                    this.DecodeScanBaseline(dcHuffmanTables, acHuffmanTables, components, componentsLength, mcusPerLine, mcuToRead, ref mcu, stream);
                }
                else
                {
                    if (this.specStart == 0)
                    {
                        if (successivePrev == 0)
                        {
                            this.DecodeScanDCFirst(dcHuffmanTables, components, componentsLength, mcusPerLine, mcuToRead, ref mcu, stream);
                        }
                        else
                        {
                            this.DecodeScanDCSuccessive(components, componentsLength, mcusPerLine, mcuToRead, ref mcu, stream);
                        }
                    }
                    else
                    {
                        if (successivePrev == 0)
                        {
                            this.DecodeScanACFirst(acHuffmanTables, components, componentsLength, mcusPerLine, mcuToRead, ref mcu, stream);
                        }
                        else
                        {
                            this.DecodeScanACSuccessive(acHuffmanTables, components, componentsLength, mcusPerLine, mcuToRead, ref mcu, stream);
                        }
                    }
                }

                // Find marker
                this.bitsCount = 0;
                this.accumulator = 0;
                this.bitsUnRead = 0;
                fileMarker = PdfJsJpegDecoderCore.FindNextFileMarker(this.markerBuffer, stream);

                // Some bad images seem to pad Scan blocks with e.g. zero bytes, skip past
                // those to attempt to find a valid marker (fixes issue4090.pdf) in original code.
                if (fileMarker.Invalid)
                {
#if DEBUG
                    Debug.WriteLine($"DecodeScan - Unexpected MCU data at {stream.Position}, next marker is: {fileMarker.Marker:X}");
#endif
                }

                ushort marker = fileMarker.Marker;

                // RSTn - We've alread read the bytes and altered the position so no need to skip
                if (marker >= PdfJsJpegConstants.Markers.RST0 && marker <= PdfJsJpegConstants.Markers.RST7)
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
            }

            fileMarker = PdfJsJpegDecoderCore.FindNextFileMarker(this.markerBuffer, stream);

            // Some images include more Scan blocks than expected, skip past those and
            // attempt to find the next valid marker (fixes issue8182.pdf) in original code.
            if (fileMarker.Invalid)
            {
#if DEBUG
                Debug.WriteLine($"DecodeScan - Unexpected MCU data at {stream.Position}, next marker is: {fileMarker.Marker:X}");
#endif
            }
            else
            {
                // We've found a valid marker.
                // Rewind the stream to the position of the marker
                stream.Position = fileMarker.Position;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DecodeScanBaseline(
            PdfJsHuffmanTables dcHuffmanTables,
            PdfJsHuffmanTables acHuffmanTables,
            PdfJsFrameComponent[] components,
            int componentsLength,
            int mcusPerLine,
            int mcuToRead,
            ref int mcu,
            Stream stream)
        {
            if (componentsLength == 1)
            {
                PdfJsFrameComponent component = components[this.compIndex];
                ref PdfJsHuffmanTable dcHuffmanTable = ref dcHuffmanTables[component.DCHuffmanTableId];
                ref PdfJsHuffmanTable acHuffmanTable = ref acHuffmanTables[component.ACHuffmanTableId];

                for (int n = 0; n < mcuToRead; n++)
                {
                    if (this.endOfStreamReached || this.unexpectedMarkerReached)
                    {
                        continue;
                    }

                    this.DecodeBlockBaseline(ref dcHuffmanTable, ref acHuffmanTable, component, mcu, stream);
                    mcu++;
                }
            }
            else
            {
                for (int n = 0; n < mcuToRead; n++)
                {
                    for (int i = 0; i < componentsLength; i++)
                    {
                        PdfJsFrameComponent component = components[i];
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

                                this.DecodeMcuBaseline(ref dcHuffmanTable, ref acHuffmanTable, component, mcusPerLine, mcu, j, k, stream);
                            }
                        }
                    }

                    mcu++;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DecodeScanDCFirst(
            PdfJsHuffmanTables dcHuffmanTables,
            PdfJsFrameComponent[] components,
            int componentsLength,
            int mcusPerLine,
            int mcuToRead,
            ref int mcu,
            Stream stream)
        {
            if (componentsLength == 1)
            {
                PdfJsFrameComponent component = components[this.compIndex];
                ref PdfJsHuffmanTable dcHuffmanTable = ref dcHuffmanTables[component.DCHuffmanTableId];

                for (int n = 0; n < mcuToRead; n++)
                {
                    if (this.endOfStreamReached || this.unexpectedMarkerReached)
                    {
                        continue;
                    }

                    this.DecodeBlockDCFirst(ref dcHuffmanTable, component, mcu, stream);
                    mcu++;
                }
            }
            else
            {
                for (int n = 0; n < mcuToRead; n++)
                {
                    for (int i = 0; i < componentsLength; i++)
                    {
                        PdfJsFrameComponent component = components[i];
                        ref PdfJsHuffmanTable dcHuffmanTable = ref dcHuffmanTables[component.DCHuffmanTableId];
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

                                this.DecodeMcuDCFirst(ref dcHuffmanTable, component, mcusPerLine, mcu, j, k, stream);
                            }
                        }
                    }

                    mcu++;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DecodeScanDCSuccessive(
            PdfJsFrameComponent[] components,
            int componentsLength,
            int mcusPerLine,
            int mcuToRead,
            ref int mcu,
            Stream stream)
        {
            if (componentsLength == 1)
            {
                PdfJsFrameComponent component = components[this.compIndex];
                for (int n = 0; n < mcuToRead; n++)
                {
                    if (this.endOfStreamReached || this.unexpectedMarkerReached)
                    {
                        continue;
                    }

                    this.DecodeBlockDCSuccessive(component, mcu, stream);
                    mcu++;
                }
            }
            else
            {
                for (int n = 0; n < mcuToRead; n++)
                {
                    for (int i = 0; i < componentsLength; i++)
                    {
                        PdfJsFrameComponent component = components[i];
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

                                this.DecodeMcuDCSuccessive(component, mcusPerLine, mcu, j, k, stream);
                            }
                        }
                    }

                    mcu++;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DecodeScanACFirst(
            PdfJsHuffmanTables acHuffmanTables,
            PdfJsFrameComponent[] components,
            int componentsLength,
            int mcusPerLine,
            int mcuToRead,
            ref int mcu,
            Stream stream)
        {
            if (componentsLength == 1)
            {
                PdfJsFrameComponent component = components[this.compIndex];
                ref PdfJsHuffmanTable acHuffmanTable = ref acHuffmanTables[component.ACHuffmanTableId];

                for (int n = 0; n < mcuToRead; n++)
                {
                    if (this.endOfStreamReached || this.unexpectedMarkerReached)
                    {
                        continue;
                    }

                    this.DecodeBlockACFirst(ref acHuffmanTable, component, mcu, stream);
                    mcu++;
                }
            }
            else
            {
                for (int n = 0; n < mcuToRead; n++)
                {
                    for (int i = 0; i < componentsLength; i++)
                    {
                        PdfJsFrameComponent component = components[i];
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

                                this.DecodeMcuACFirst(ref acHuffmanTable, component, mcusPerLine, mcu, j, k, stream);
                            }
                        }
                    }

                    mcu++;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DecodeScanACSuccessive(
            PdfJsHuffmanTables acHuffmanTables,
            PdfJsFrameComponent[] components,
            int componentsLength,
            int mcusPerLine,
            int mcuToRead,
            ref int mcu,
            Stream stream)
        {
            if (componentsLength == 1)
            {
                PdfJsFrameComponent component = components[this.compIndex];
                ref PdfJsHuffmanTable acHuffmanTable = ref acHuffmanTables[component.ACHuffmanTableId];

                for (int n = 0; n < mcuToRead; n++)
                {
                    if (this.endOfStreamReached || this.unexpectedMarkerReached)
                    {
                        continue;
                    }

                    this.DecodeBlockACSuccessive(ref acHuffmanTable, component, mcu, stream);
                    mcu++;
                }
            }
            else
            {
                for (int n = 0; n < mcuToRead; n++)
                {
                    for (int i = 0; i < componentsLength; i++)
                    {
                        PdfJsFrameComponent component = components[i];
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

                                this.DecodeMcuACSuccessive(ref acHuffmanTable, component, mcusPerLine, mcu, j, k, stream);
                            }
                        }
                    }

                    mcu++;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DecodeBlockBaseline(ref PdfJsHuffmanTable dcHuffmanTable, ref PdfJsHuffmanTable acHuffmanTable, PdfJsFrameComponent component, int mcu, Stream stream)
        {
            int blockRow = mcu / component.WidthInBlocks;
            int blockCol = mcu % component.WidthInBlocks;
            int offset = component.GetBlockBufferOffset(blockRow, blockCol);
            this.DecodeBaseline(component, offset, ref dcHuffmanTable, ref acHuffmanTable, stream);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DecodeMcuBaseline(ref PdfJsHuffmanTable dcHuffmanTable, ref PdfJsHuffmanTable acHuffmanTable, PdfJsFrameComponent component, int mcusPerLine, int mcu, int row, int col, Stream stream)
        {
            int mcuRow = mcu / mcusPerLine;
            int mcuCol = mcu % mcusPerLine;
            int blockRow = (mcuRow * component.VerticalSamplingFactor) + row;
            int blockCol = (mcuCol * component.HorizontalSamplingFactor) + col;
            int offset = component.GetBlockBufferOffset(blockRow, blockCol);
            this.DecodeBaseline(component, offset, ref dcHuffmanTable, ref acHuffmanTable, stream);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DecodeBlockDCFirst(ref PdfJsHuffmanTable dcHuffmanTable, PdfJsFrameComponent component, int mcu, Stream stream)
        {
            int blockRow = mcu / component.WidthInBlocks;
            int blockCol = mcu % component.WidthInBlocks;
            int offset = component.GetBlockBufferOffset(blockRow, blockCol);
            this.DecodeDCFirst(component, offset, ref dcHuffmanTable, stream);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DecodeMcuDCFirst(ref PdfJsHuffmanTable dcHuffmanTable, PdfJsFrameComponent component, int mcusPerLine, int mcu, int row, int col, Stream stream)
        {
            int mcuRow = mcu / mcusPerLine;
            int mcuCol = mcu % mcusPerLine;
            int blockRow = (mcuRow * component.VerticalSamplingFactor) + row;
            int blockCol = (mcuCol * component.HorizontalSamplingFactor) + col;
            int offset = component.GetBlockBufferOffset(blockRow, blockCol);
            this.DecodeDCFirst(component, offset, ref dcHuffmanTable, stream);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DecodeBlockDCSuccessive(PdfJsFrameComponent component, int mcu, Stream stream)
        {
            int blockRow = mcu / component.WidthInBlocks;
            int blockCol = mcu % component.WidthInBlocks;
            int offset = component.GetBlockBufferOffset(blockRow, blockCol);
            this.DecodeDCSuccessive(component, offset, stream);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DecodeMcuDCSuccessive(PdfJsFrameComponent component, int mcusPerLine, int mcu, int row, int col, Stream stream)
        {
            int mcuRow = mcu / mcusPerLine;
            int mcuCol = mcu % mcusPerLine;
            int blockRow = (mcuRow * component.VerticalSamplingFactor) + row;
            int blockCol = (mcuCol * component.HorizontalSamplingFactor) + col;
            int offset = component.GetBlockBufferOffset(blockRow, blockCol);
            this.DecodeDCSuccessive(component, offset, stream);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DecodeBlockACFirst(ref PdfJsHuffmanTable acHuffmanTable, PdfJsFrameComponent component, int mcu, Stream stream)
        {
            int blockRow = mcu / component.WidthInBlocks;
            int blockCol = mcu % component.WidthInBlocks;
            int offset = component.GetBlockBufferOffset(blockRow, blockCol);
            this.DecodeACFirst(component, offset, ref acHuffmanTable, stream);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DecodeMcuACFirst(ref PdfJsHuffmanTable acHuffmanTable, PdfJsFrameComponent component, int mcusPerLine, int mcu, int row, int col, Stream stream)
        {
            int mcuRow = mcu / mcusPerLine;
            int mcuCol = mcu % mcusPerLine;
            int blockRow = (mcuRow * component.VerticalSamplingFactor) + row;
            int blockCol = (mcuCol * component.HorizontalSamplingFactor) + col;
            int offset = component.GetBlockBufferOffset(blockRow, blockCol);
            this.DecodeACFirst(component, offset, ref acHuffmanTable, stream);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DecodeBlockACSuccessive(ref PdfJsHuffmanTable acHuffmanTable, PdfJsFrameComponent component, int mcu, Stream stream)
        {
            int blockRow = mcu / component.WidthInBlocks;
            int blockCol = mcu % component.WidthInBlocks;
            int offset = component.GetBlockBufferOffset(blockRow, blockCol);
            this.DecodeACSuccessive(component, offset, ref acHuffmanTable, stream);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DecodeMcuACSuccessive(ref PdfJsHuffmanTable acHuffmanTable, PdfJsFrameComponent component, int mcusPerLine, int mcu, int row, int col, Stream stream)
        {
            int mcuRow = mcu / mcusPerLine;
            int mcuCol = mcu % mcusPerLine;
            int blockRow = (mcuRow * component.VerticalSamplingFactor) + row;
            int blockCol = (mcuCol * component.HorizontalSamplingFactor) + col;
            int offset = component.GetBlockBufferOffset(blockRow, blockCol);
            this.DecodeACSuccessive(component, offset, ref acHuffmanTable, stream);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int ReadBit(Stream stream)
        {
            // TODO: I wonder if we can do this two bytes at a time; libjpeg turbo seems to do that?
            if (this.bitsCount > 0)
            {
                this.bitsCount--;
                return (this.bitsData >> this.bitsCount) & 1;
            }

            this.bitsData = stream.ReadByte();

            if (this.bitsData == -0x1)
            {
                // We've encountered the end of the file stream which means there's no EOI marker in the image
                this.endOfStreamReached = true;
            }

            if (this.bitsData == PdfJsJpegConstants.Markers.Prefix)
            {
                int nextByte = stream.ReadByte();
                if (nextByte != 0)
                {
#if DEBUG
                    Debug.WriteLine($"DecodeScan - Unexpected marker {(this.bitsData << 8) | nextByte:X} at {stream.Position}");
#endif

                    // We've encountered an unexpected marker. Reverse the stream and exit.
                    this.unexpectedMarkerReached = true;
                    stream.Position -= 2;
                }

                // Unstuff 0
            }

            this.bitsCount = 7;

            return this.bitsData >> 7;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private short DecodeHuffman(ref PdfJsHuffmanTable tree, Stream stream)
        {
            short code = -1;

            // TODO: Adding this code introduces error into the decoder.
            // NOTES # During investigation of the libjpeg implementation it appears that they pull 32bits at a time and operate on those bits
            //         using 3 methods: FillBits, PeekBits, and ReadBits. We should attempt to do the same.
            // It doesn't appear to speed anything up either.
            //  if (this.bitsUnRead < 8)
            //  {
            //    if (this.bitsCount <= 0)
            //    {
            //        code = (short)this.ReadBit(stream);
            //        if (this.endOfStreamReached || this.unexpectedMarkerReached)
            //        {
            //            return -1;
            //        }
            //
            //        this.bitsUnRead += 8;
            //    }
            //
            //    this.accumulator = (this.accumulator << 8) | this.bitsData;
            //    int lutIndex = (this.accumulator >> (8 - this.bitsUnRead)) & 0xFF;
            //    int v = tree.Lookahead[lutIndex];
            //    if (v != 0)
            //    {
            //        int nb = (v & 0xFF) - 1;
            //        this.bitsCount -= nb - 1;
            //        this.bitsUnRead -= nb;
            //        v = v >> 8;
            //        return (short)v;
            //    }
            //  }
            if (code == -1)
            {
                code = (short)this.ReadBit(stream);
                if (this.endOfStreamReached || this.unexpectedMarkerReached)
                {
                    return -1;
                }
            }

            // "DECODE", section F.2.2.3, figure F.16, page 109 of T.81
            int i = 1;

            while (code > tree.MaxCode[i])
            {
                code <<= 1;
                code |= (short)this.ReadBit(stream);

                if (this.endOfStreamReached || this.unexpectedMarkerReached)
                {
                    return -1;
                }

                i++;
            }

            int j = tree.ValOffset[i];
            return tree.HuffVal[(j + code) & 0xFF];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int Receive(int length, Stream stream)
        {
            int n = 0;
            while (length > 0)
            {
                int bit = this.ReadBit(stream);
                if (this.endOfStreamReached || this.unexpectedMarkerReached)
                {
                    return -1;
                }

                n = (n << 1) | bit;
                length--;
            }

            return n;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int ReceiveAndExtend(int length, Stream stream)
        {
            if (length == 1)
            {
                return this.ReadBit(stream) == 1 ? 1 : -1;
            }

            int n = this.Receive(length, stream);
            if (n >= 1 << (length - 1))
            {
                return n;
            }

            return n + (-1 << length) + 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DecodeBaseline(PdfJsFrameComponent component, int offset, ref PdfJsHuffmanTable dcHuffmanTable, ref PdfJsHuffmanTable acHuffmanTable, Stream stream)
        {
            int t = this.DecodeHuffman(ref dcHuffmanTable, stream);
            if (this.endOfStreamReached || this.unexpectedMarkerReached)
            {
                return;
            }

            int diff = t == 0 ? 0 : this.ReceiveAndExtend(t, stream);
            component.BlockData[offset] = (short)(component.Pred += diff);

            int k = 1;
            while (k < 64)
            {
                int rs = this.DecodeHuffman(ref acHuffmanTable, stream);
                if (this.endOfStreamReached || this.unexpectedMarkerReached)
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

                if (k > 63)
                {
                    break;
                }

                byte z = PdfJsQuantizationTables.DctZigZag[k];
                short re = (short)this.ReceiveAndExtend(s, stream);
                component.BlockData[offset + z] = re;
                k++;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DecodeDCFirst(PdfJsFrameComponent component, int offset, ref PdfJsHuffmanTable dcHuffmanTable, Stream stream)
        {
            int t = this.DecodeHuffman(ref dcHuffmanTable, stream);
            if (this.endOfStreamReached || this.unexpectedMarkerReached)
            {
                return;
            }

            int diff = t == 0 ? 0 : this.ReceiveAndExtend(t, stream) << this.successiveState;
            component.BlockData[offset] = (short)(component.Pred += diff);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DecodeDCSuccessive(PdfJsFrameComponent component, int offset, Stream stream)
        {
            int bit = this.ReadBit(stream);
            if (this.endOfStreamReached || this.unexpectedMarkerReached)
            {
                return;
            }

            component.BlockData[offset] |= (short)(bit << this.successiveState);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DecodeACFirst(PdfJsFrameComponent component, int offset, ref PdfJsHuffmanTable acHuffmanTable, Stream stream)
        {
            if (this.eobrun > 0)
            {
                this.eobrun--;
                return;
            }

            Span<short> componentBlockDataSpan = component.BlockData.Span;
            int k = this.specStart;
            int e = this.specEnd;
            while (k <= e)
            {
                short rs = this.DecodeHuffman(ref acHuffmanTable, stream);
                if (this.endOfStreamReached || this.unexpectedMarkerReached)
                {
                    return;
                }

                int s = rs & 15;
                int r = rs >> 4;

                if (s == 0)
                {
                    if (r < 15)
                    {
                        this.eobrun = this.Receive(r, stream) + (1 << r) - 1;
                        break;
                    }

                    k += 16;
                    continue;
                }

                k += r;
                byte z = PdfJsQuantizationTables.DctZigZag[k];
                componentBlockDataSpan[offset + z] = (short)(this.ReceiveAndExtend(s, stream) * (1 << this.successiveState));
                k++;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DecodeACSuccessive(PdfJsFrameComponent component, int offset, ref PdfJsHuffmanTable acHuffmanTable, Stream stream)
        {
            int k = this.specStart;
            int e = this.specEnd;
            int r = 0;
            Span<short> componentBlockDataSpan = component.BlockData.Span;
            while (k <= e)
            {
                byte z = PdfJsQuantizationTables.DctZigZag[k];
                switch (this.successiveACState)
                {
                    case 0: // Initial state
                        short rs = this.DecodeHuffman(ref acHuffmanTable, stream);
                        if (this.endOfStreamReached || this.unexpectedMarkerReached)
                        {
                            return;
                        }

                        int s = rs & 15;
                        r = rs >> 4;
                        if (s == 0)
                        {
                            if (r < 15)
                            {
                                this.eobrun = this.Receive(r, stream) + (1 << r);
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

                            this.successiveACNextValue = this.ReceiveAndExtend(s, stream);
                            this.successiveACState = r > 0 ? 2 : 3;
                        }

                        continue;
                    case 1: // Skipping r zero items
                    case 2:
                        if (componentBlockDataSpan[offset + z] != 0)
                        {
                            int bit = this.ReadBit(stream);
                            if (this.endOfStreamReached || this.unexpectedMarkerReached)
                            {
                                return;
                            }

                            componentBlockDataSpan[offset + z] += (short)(bit << this.successiveState);
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
                        if (componentBlockDataSpan[offset + z] != 0)
                        {
                            int bit = this.ReadBit(stream);
                            if (this.endOfStreamReached || this.unexpectedMarkerReached)
                            {
                                return;
                            }

                            componentBlockDataSpan[offset + z] += (short)(bit << this.successiveState);
                        }
                        else
                        {
                            componentBlockDataSpan[offset + z] = (short)(this.successiveACNextValue << this.successiveState);
                            this.successiveACState = 0;
                        }

                        break;
                    case 4: // Eob
                        if (componentBlockDataSpan[offset + z] != 0)
                        {
                            int bit = this.ReadBit(stream);
                            if (this.endOfStreamReached || this.unexpectedMarkerReached)
                            {
                                return;
                            }

                            componentBlockDataSpan[offset + z] += (short)(bit << this.successiveState);
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
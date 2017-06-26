// <copyright file="ScanDecoder.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats.Jpeg.Port.Components
{
    using System;
#if DEBUG
    using System.Diagnostics;
#endif
    using System.IO;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Encapsulates a decode method. TODO: This may well be a bottleneck
    /// </summary>
    /// <param name="component">The component</param>
    /// <param name="offset">The offset</param>
    /// <param name="dcHuffmanTables">The DC Huffman tables</param>
    /// <param name="acHuffmanTables">The AC Huffman tables</param>
    /// <param name="stream">The input stream</param>
    internal delegate void DecodeAction(ref FrameComponent component, int offset, HuffmanTables dcHuffmanTables, HuffmanTables acHuffmanTables, Stream stream);

    /// <summary>
    /// Provides the means to decode a spectral scan
    /// </summary>
    internal struct ScanDecoder
    {
        private int bitsData;

        private int bitsCount;

        private int specStart;

        private int specEnd;

        private int eobrun;

        private int successiveState;

        private int successiveACState;

        private int successiveACNextValue;

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
            Frame frame,
            Stream stream,
            HuffmanTables dcHuffmanTables,
            HuffmanTables acHuffmanTables,
            FrameComponent[] components,
            int componentIndex,
            int componentsLength,
            ushort resetInterval,
            int spectralStart,
            int spectralEnd,
            int successivePrev,
            int successive)
        {
            this.specStart = spectralStart;
            this.specEnd = spectralEnd;
            this.successiveState = successive;
            bool progressive = frame.Progressive;
            int mcusPerLine = frame.McusPerLine;

            // TODO: Delegate action will not be fast
            DecodeAction decodeFn;

            if (progressive)
            {
                if (this.specStart == 0)
                {
                    if (successivePrev == 0)
                    {
                        decodeFn = this.DecodeDCFirst;
                    }
                    else
                    {
                        decodeFn = this.DecodeDCSuccessive;
                    }

                    Debug.WriteLine(successivePrev == 0 ? "decodeDCFirst" : "decodeDCSuccessive");
                }
                else
                {
                    if (successivePrev == 0)
                    {
                        decodeFn = this.DecodeACFirst;
                    }
                    else
                    {
                        decodeFn = this.DecodeACSuccessive;
                    }

                    Debug.WriteLine(successivePrev == 0 ? "decodeACFirst" : "decodeACSuccessive");
                }
            }
            else
            {
                decodeFn = this.DecodeBaseline;
            }

            int mcu = 0;
            int mcuExpected;
            if (componentsLength == 1)
            {
                mcuExpected = components[componentIndex].BlocksPerLine * components[componentIndex].BlocksPerColumn;
            }
            else
            {
                mcuExpected = mcusPerLine * frame.McusPerColumn;
            }

            Debug.WriteLine("mcuExpected = " + mcuExpected);

            // FileMarker fileMarker;
            while (mcu < mcuExpected)
            {
                // Reset interval
                int mcuToRead = resetInterval > 0 ? Math.Min(mcuExpected - mcu, resetInterval) : mcuExpected;

                // TODO: We might just be able to loop here.
                if (componentsLength == 1)
                {
                    ref FrameComponent c = ref components[componentIndex];
                    c.Pred = 0;
                }
                else
                {
                    for (int i = 0; i < componentsLength; i++)
                    {
                        ref FrameComponent c = ref components[i];
                        c.Pred = 0;
                    }
                }

                this.eobrun = 0;

                if (componentsLength == 1)
                {
                    ref FrameComponent component = ref components[componentIndex];
                    for (int n = 0; n < mcuToRead; n++)
                    {
                        DecodeBlock(dcHuffmanTables, acHuffmanTables, ref component, decodeFn, mcu, stream);
                        mcu++;
                    }
                }
                else
                {
                    for (int n = 0; n < mcuToRead; n++)
                    {
                        for (int i = 0; i < componentsLength; i++)
                        {
                            ref FrameComponent component = ref components[i];
                            int h = component.HorizontalFactor;
                            int v = component.VerticalFactor;
                            for (int j = 0; j < v; j++)
                            {
                                for (int k = 0; k < h; k++)
                                {
                                    DecodeMcu(dcHuffmanTables, acHuffmanTables, ref component, decodeFn, mcusPerLine, mcu, j, k, stream);
                                }
                            }
                        }

                        mcu++;
                    }
                }

                // Find marker
                // this.bitsCount = 0;
                // // TODO: We need to make sure we are not overwriting anything here.
                //                fileMarker = JpegDecoderCore.FindNextFileMarker(stream);
                //                // Some bad images seem to pad Scan blocks with e.g. zero bytes, skip past
                //                // those to attempt to find a valid marker (fixes issue4090.pdf) in original code.
                //                if (fileMarker.Invalid)
                //                {
                // #if DEBUG
                //                    Debug.WriteLine("DecodeScan - Unexpected MCU data, next marker is: " + fileMarker.Marker.ToString("X"));
                // #endif
                //                }
                //                ushort marker = fileMarker.Marker;
                //                if (marker <= 0xFF00)
                //                {
                //                    throw new ImageFormatException("Marker was not found");
                //                }
                //                if (marker >= JpegConstants.Markers.RST0 && marker <= JpegConstants.Markers.RST7)
                //                {
                //                    // RSTx
                //                    stream.Skip(2);
                //                }
                //                else
                //                {
                //                    break;
                //                }
            }

            // fileMarker = JpegDecoderCore.FindNextFileMarker(stream);
            //            // Some images include more Scan blocks than expected, skip past those and
            //            // attempt to find the next valid marker (fixes issue8182.pdf) in original code.
            //            if (fileMarker.Invalid)
            //            {
            // #if DEBUG
            //                Debug.WriteLine("DecodeScan - Unexpected MCU data, next marker is: " + fileMarker.Marker.ToString("X"));
            // #endif
            //            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetBlockBufferOffset(ref FrameComponent component, int row, int col)
        {
            return 64 * (((component.BlocksPerLine + 1) * row) + col);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void DecodeMcu(HuffmanTables dcHuffmanTables, HuffmanTables acHuffmanTables, ref FrameComponent component, DecodeAction decode, int mcusPerLine, int mcu, int row, int col, Stream stream)
        {
            int mcuRow = (mcu / mcusPerLine) | 0;
            int mcuCol = mcu % mcusPerLine;
            int blockRow = (mcuRow * component.VerticalFactor) + row;
            int blockCol = (mcuCol * component.HorizontalFactor) + col;
            int offset = GetBlockBufferOffset(ref component, blockRow, blockCol);
            decode(ref component, offset, dcHuffmanTables, acHuffmanTables, stream);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void DecodeBlock(HuffmanTables dcHuffmanTables, HuffmanTables acHuffmanTables, ref FrameComponent component, DecodeAction decode, int mcu, Stream stream)
        {
            int blockRow = (mcu / component.BlocksPerLine) | 0;
            int blockCol = mcu % component.BlocksPerLine;
            int offset = GetBlockBufferOffset(ref component, blockRow, blockCol);
            decode(ref component, offset, dcHuffmanTables, acHuffmanTables, stream);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int ReadBit(Stream stream)
        {
            if (this.bitsCount > 0)
            {
                this.bitsCount--;
                return (this.bitsData >> this.bitsCount) & 1;
            }

            this.bitsData = stream.ReadByte();
            if (this.bitsData == JpegConstants.Markers.Prefix)
            {
                int nextByte = stream.ReadByte();
                if (nextByte > 0)
                {
                    throw new ImageFormatException($"Unexpected marker {(this.bitsData << 8) | nextByte}");
                }

                // Unstuff 0
            }

            this.bitsCount = 7;
            return this.bitsData >> 7;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private short DecodeHuffman(HuffmanBranch[] tree, Stream stream)
        {
            HuffmanBranch[] node = tree;
            while (true)
            {
                int index = this.ReadBit(stream);
                HuffmanBranch branch = node[index];
                node = branch.Children;

                if (branch.Value > -1)
                {
                    return branch.Value;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int Receive(int length, Stream stream)
        {
            int n = 0;
            while (length > 0)
            {
                n = (n << 1) | this.ReadBit(stream);
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
        private void DecodeBaseline(ref FrameComponent component, int offset, HuffmanTables dcHuffmanTables, HuffmanTables acHuffmanTables, Stream stream)
        {
            int t = this.DecodeHuffman(dcHuffmanTables[component.DCHuffmanTableId], stream);
            int diff = t == 0 ? 0 : this.ReceiveAndExtend(t, stream);
            component.BlockData[offset] = (short)(component.Pred += diff);

            int k = 1;
            while (k < 64)
            {
                int rs = this.DecodeHuffman(acHuffmanTables[component.ACHuffmanTableId], stream);
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
                byte z = QuantizationTables.DctZigZag[k];
                short re = (short)this.ReceiveAndExtend(s, stream);
                component.BlockData[offset + z] = re;
                k++;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DecodeDCFirst(ref FrameComponent component, int offset, HuffmanTables dcHuffmanTables, HuffmanTables acHuffmanTables, Stream stream)
        {
            int t = this.DecodeHuffman(dcHuffmanTables[component.DCHuffmanTableId], stream);
            int diff = t == 0 ? 0 : this.ReceiveAndExtend(t, stream) << this.successiveState;
            component.BlockData[offset] = (short)(component.Pred += diff);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DecodeDCSuccessive(ref FrameComponent component, int offset, HuffmanTables dcHuffmanTables, HuffmanTables acHuffmanTables, Stream stream)
        {
            component.BlockData[offset] |= (short)(this.ReadBit(stream) << this.successiveState);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DecodeACFirst(ref FrameComponent component, int offset, HuffmanTables dcHuffmanTables, HuffmanTables acHuffmanTables, Stream stream)
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
                short rs = this.DecodeHuffman(acHuffmanTables[component.ACHuffmanTableId], stream);
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
                byte z = QuantizationTables.DctZigZag[k];
                component.BlockData[offset + z] = (short)(this.ReceiveAndExtend(s, stream) * (1 << this.successiveState));
                k++;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DecodeACSuccessive(ref FrameComponent component, int offset, HuffmanTables dcHuffmanTables, HuffmanTables acHuffmanTables, Stream stream)
        {
            int k = this.specStart;
            int e = this.specEnd;
            int r = 0;
            while (k <= e)
            {
                byte z = QuantizationTables.DctZigZag[k];
                switch (this.successiveACState)
                {
                    case 0: // Initial state
                        short rs = this.DecodeHuffman(acHuffmanTables[component.ACHuffmanTableId], stream);
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
                        if (component.BlockData[offset + z] > 0)
                        {
                            component.BlockData[offset + z] += (short)(this.ReadBit(stream) << this.successiveState);
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
                        if (component.BlockData[offset + z] > 0)
                        {
                            component.BlockData[offset + z] += (short)(this.ReadBit(stream) << this.successiveState);
                        }
                        else
                        {
                            component.BlockData[offset + z] = (short)(this.successiveACNextValue << this.successiveState);
                            this.successiveACState = 0;
                        }

                        break;
                    case 4: // Eob
                        if (component.BlockData[offset + z] > 0)
                        {
                            component.BlockData[offset + z] += (short)(this.ReadBit(stream) << this.successiveState);
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
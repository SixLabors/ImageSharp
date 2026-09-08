// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Jxl.IO.Jpeg.Data;
using SixLabors.ImageSharp.Formats.Jxl.Processing;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;

namespace SixLabors.ImageSharp.Formats.Jxl.IO.Jpeg;

internal class JpegWriter
{
    private static void DCTCodingStateInit(JpegDctCodingState state)
    {
        state.EobRun = 0;
        state.CurAcHuff = null;
        state.RefinementBits.Clear();
        state.RefinementBitsCount = 0;
    }

    internal static void Flush(JpegDctCodingState state, JpegBitWriter bw)
    {
        if (state.EobRun > 0)
        {
            bw.Reserve(16);

            int nBits = JxlMath.FloorLog2Nonzero(state.EobRun);
            int symbol = nBits << 4;

            WriteSymbol(symbol, state.CurAcHuff!, bw);

            if (nBits > 0)
            {
                bw.WriteBits(
                    nBits,
                    (ulong)(state.EobRun & ((1 << nBits) - 1)));
            }

            state.EobRun = 0;
        }

        const int stride = 124;

        int numWords = state.RefinementBitsCount >> 4;
        int i = 0;

        while (i < numWords)
        {
            int limit = Math.Min(i + stride, numWords);

            bw.Reserve(512);

            for (; i < limit; ++i)
            {
                bw.WriteBits(16, state.RefinementBits[i]);
            }
        }

        bw.Reserve(16);

        int tail = state.RefinementBitsCount & 0xf;

        if (tail != 0)
        {
            bw.WriteBits(
                tail,
                state.RefinementBits[^1]);
        }

        state.RefinementBits.Clear();
        state.RefinementBitsCount = 0;
    }

    private static void WriteSymbol(int symbol, JpegHuffmanCodeTable table, JpegBitWriter bw) => bw.WriteBits(
            table.Depth[symbol],
            (ulong)table.Code[symbol]);

    private static void WriteSymbolBits(int symbol, JpegHuffmanCodeTable table, JpegBitWriter bw, int nBits, ulong bits) => bw.WriteBits(
            nBits + table.Depth[symbol],
            bits | ((ulong)table.Code[symbol] << nBits));

    private static void BufferEndOfBand(JpegDctCodingState state, JpegHuffmanCodeTable acHuff, ReadOnlySpan<int> newBitsArray, JpegBitWriter bw)
    {
        if (state.EobRun == 0)
        {
            state.CurAcHuff = acHuff;
        }

        ++state.EobRun;

        if (!newBitsArray.IsEmpty)
        {
            ulong newBits = 0;

            for (int i = 0; i < newBitsArray.Length; ++i)
            {
                newBits = (newBits << 1) | (uint)newBitsArray[i];
            }

            int tail = state.RefinementBitsCount & 0xf;

            if (tail != 0)
            {
                int stuffBitsCount = Math.Min(16 - tail, newBitsArray.Length);
                ushort stuffBits = (ushort)(newBits >> (newBitsArray.Length - stuffBitsCount));

                stuffBits &= (ushort)((1 << stuffBitsCount) - 1);

                int last = state.RefinementBits.Count - 1;

                state.RefinementBits[last] = (ushort)((state.RefinementBits[last] << stuffBitsCount) | stuffBits);

                state.RefinementBitsCount += stuffBitsCount;

                newBitsArray = newBitsArray[stuffBitsCount..];
            }

            int newBitsCount = newBitsArray.Length;

            while (newBitsCount >= 16)
            {
                state.RefinementBits.Add((ushort)(newBits >> (newBitsCount - 16)));

                newBitsCount -= 16;
                state.RefinementBitsCount += 16;
            }

            if (newBitsCount != 0)
            {
                state.RefinementBits.Add((ushort)(newBits & ((1u << newBitsCount) - 1)));

                state.RefinementBitsCount += newBitsCount;
            }
        }

        if (state.EobRun == 0x7fff)
        {
            Flush(state, bw);
        }
    }

    private static bool BuildJpegHuffmanCodeTable(JpegHuffmanCode huff, JpegHuffmanCodeTable table)
    {
        Span<int> huffCode = stackalloc int[JpegDataConstants.JpegHuffmanAlphabetSize];
        Span<uint> huffSize = stackalloc uint[JpegDataConstants.JpegHuffmanAlphabetSize + 1];

        int p = 0;

        for (int l = 1; l <= JpegDataConstants.JpegHuffmanMaxBitLength; ++l)
        {
            int count = huff.Counts[l];

            if (p + count > JpegDataConstants.JpegHuffmanAlphabetSize + 1)
            {
                return false;
            }

            while (count-- > 0)
            {
                huffSize[p++] = (uint)l;
            }
        }

        if (p == 0)
        {
            return true;
        }

        int lastP = p - 1;
        huffSize[lastP] = 0;

        int code = 0;
        uint si = huffSize[0];
        p = 0;

        while (huffSize[p] != 0)
        {
            while (huffSize[p] == si)
            {
                huffCode[p++] = code++;
            }

            code <<= 1;
            ++si;
        }

        for (p = 0; p < lastP; ++p)
        {
            int value = huff.Values[p];
            table.Depth[value] = (byte)huffSize[p];
            table.Code[value] = (short)huffCode[p];
        }

        return true;
    }

    private static bool EncodeSOI(JpegSerializationState state)
    {
        state.OutputQueue.Add([0xff, 0xd8]);
        return true;
    }

    private static bool EncodeEOI(JpegData jpg, JpegSerializationState state)
    {
        state.OutputQueue.Add([0xff, 0xd9]);
        state.OutputQueue.Add(jpg.TailData);
        return true;
    }

    private static bool EncodeRestart(byte marker, JpegSerializationState state)
    {
        state.OutputQueue.Add([0xff, marker]);
        return true;
    }

    private static bool EncodeSOF(JpegData jpg, byte marker, JpegSerializationState state)
    {
        if (marker <= 0xc2)
        {
            state.IsProgressive = marker == 0xc2;
        }

        int componentCount = jpg.Components.Count;
        int markerLength = 8 + (3 * componentCount);

        state.OutputQueue.Add([(byte)(markerLength + 2)]);

        Span<byte> data = CollectionsMarshal.AsSpan(state.OutputQueue[^1]);
        int pos = 0;

        data[pos++] = 0xff;
        data[pos++] = marker;
        data[pos++] = (byte)(markerLength >> 8);
        data[pos++] = (byte)markerLength;
        data[pos++] = JpegPrecision;
        data[pos++] = (byte)(jpg.Height >> 8);
        data[pos++] = (byte)jpg.Height;
        data[pos++] = (byte)(jpg.Width >> 8);
        data[pos++] = (byte)jpg.Width;
        data[pos++] = (byte)componentCount;

        for (int i = 0; i < componentCount; ++i)
        {
            JpegComponent component = jpg.Components[i];

            data[pos++] = (byte)component.Id;
            data[pos++] = (byte)((component.HorizontalSampleFactor << 4) | component.VerticalSampleFactor);

            int quantIndex = component.QuantIndex;

            if ((uint)quantIndex >= (uint)jpg.Quant.Count)
            {
                return false;
            }

            data[pos++] = (byte)jpg.Quant[quantIndex].Index;
        }

        return true;
    }

    private static bool EncodeSOS(JpegData jpg, JpegScanInfo scanInfo, JpegSerializationState state)
    {
        int scanCount = scanInfo.NumComponents;
        int markerLength = 6 + (2 * scanCount);

        state.OutputQueue.Add([(byte)(markerLength + 2)]);

        Span<byte> data = CollectionsMarshal.AsSpan(state.OutputQueue[^1]);
        int pos = 0;

        data[pos++] = 0xff;
        data[pos++] = 0xda;
        data[pos++] = (byte)(markerLength >> 8);
        data[pos++] = (byte)markerLength;
        data[pos++] = (byte)scanCount;

        for (int i = 0; i < scanCount; ++i)
        {
            JpegComponentScanInfo si = scanInfo.Components[i];

            if ((uint)si.ComponentIndex >= (uint)jpg.Components.Count)
            {
                return false;
            }

            data[pos++] = (byte)jpg.Components[si.ComponentIndex].Id;
            data[pos++] = (byte)((si.DcTableIndex << 4) + si.AcTableIndex);
        }

        data[pos++] = (byte)scanInfo.Ss;
        data[pos++] = (byte)scanInfo.Se;
        data[pos++] = (byte)((scanInfo.Ah << 4) | scanInfo.Al);

        return true;
    }

    private static bool EncodeDHT(JpegData jpg, JpegSerializationState state)
    {
        List<JpegHuffmanCode> huffmanCode = jpg.HuffmanCodes;

        int markerLength = 2;

        for (int i = state.DhtIndex; i < huffmanCode.Count; ++i)
        {
            JpegHuffmanCode huff = huffmanCode[i];

            for (int j = 0; j < 17; ++j)
            {
                markerLength += huff.Counts[j];
            }

            // Empty DHT marker.
            if (markerLength == 2)
            {
                break;
            }

            markerLength += JpegDataConstants.JpegHuffmanMaxBitLength;

            if (huff.IsLast)
            {
                break;
            }
        }

        state.OutputQueue.Add([(byte)(markerLength + 2)]);

        Span<byte> data = CollectionsMarshal.AsSpan(state.OutputQueue[^1]);
        int pos = 0;

        data[pos++] = 0xff;
        data[pos++] = 0xc4;
        data[pos++] = (byte)(markerLength >> 8);
        data[pos++] = (byte)markerLength;

        while (true)
        {
            int huffmanCodeIndex = state.DhtIndex++;

            if ((uint)huffmanCodeIndex >= (uint)huffmanCode.Count)
            {
                return false;
            }

            JpegHuffmanCode huff = huffmanCode[huffmanCodeIndex];

            int index = huff.SlotId;
            JpegHuffmanCodeTable huffTable = new();

            int totalCount = 0;
            int maxLength = 0;

            for (int i = 0; i < 17; ++i)
            {
                if (huff.Counts[i] != 0)
                {
                    maxLength = i;
                }

                totalCount += huff.Counts[i];
            }

            // Empty DHT marker.
            if (totalCount == 0)
            {
                break;
            }

            if ((index & 0x10) != 0)
            {
                index -= 0x10;
                huffTable = state.AcHuffTable[index];
            }
            else
            {
                huffTable = state.DcHuffTable[index];
            }

            huffTable.InitDepths(127);

            if (!BuildJpegHuffmanCodeTable(huff, huffTable))
            {
                return false;
            }

            huffTable.Initialized = true;

            --totalCount;

            data[pos++] = (byte)huff.SlotId;

            for (int i = 1; i <= JpegDataConstants.JpegHuffmanMaxBitLength; ++i)
            {
                data[pos++] = (byte)(i == maxLength ? huff.Counts[i] - 1 : huff.Counts[i]);
            }

            for (int i = 0; i < totalCount; ++i)
            {
                data[pos++] = (byte)huff.Values[i];
            }

            if (huff.IsLast)
            {
                break;
            }
        }

        return true;
    }

    private static bool EncodeDQT(JpegData jpg, JpegSerializationState state)
    {
        int markerLength = 2;

        for (int i = state.DqtIndex; i < jpg.Quant.Count; ++i)
        {
            JpegQuantizationTable table = jpg.Quant[i];

            markerLength += 1 + ((table.Precision != 0 ? 2 : 1) * JxlFrameDimensions.DctBlockSize);

            if (table.IsLast)
            {
                break;
            }
        }

        state.OutputQueue.Add([(byte)(markerLength + 2)]);

        Span<byte> data = CollectionsMarshal.AsSpan(state.OutputQueue[^1]);
        int pos = 0;

        data[pos++] = 0xff;
        data[pos++] = 0xdb;
        data[pos++] = (byte)(markerLength >> 8);
        data[pos++] = (byte)markerLength;

        while (true)
        {
            int index = state.DqtIndex++;

            if ((uint)index >= (uint)jpg.Quant.Count)
            {
                return false;
            }

            JpegQuantizationTable table = jpg.Quant[index];

            data[pos++] = (byte)((table.Precision << 4) + table.Index);

            for (int i = 0; i < JxlFrameDimensions.DctBlockSize; ++i)
            {
                int valueIndex = JpegDataConstants.JpegNaturalOrder[i];
                int value = table.Values[valueIndex];

                if (table.Precision != 0)
                {
                    data[pos++] = (byte)(value >> 8);
                }

                data[pos++] = (byte)value;
            }

            if (table.IsLast)
            {
                break;
            }
        }

        return true;
    }

    private static bool EncodeDRI(JpegData jpg, JpegSerializationState state)
    {
        state.SeenDriMarker = true;

        List<byte> driMarker =
        [
            0xff,
            0xdd,
            0,
            4,
            (byte)(jpg.RestartInterval >> 8),
            (byte)jpg.RestartInterval
        ];

        state.OutputQueue.Add(driMarker);
        return true;
    }

    private static bool EncodeAPP(JpegData jpg, byte marker, JpegSerializationState state)
    {
        // TODO: Check that marker corresponds to payload.
        _ = marker;

        int appIndex = state.AppIndex++;

        if ((uint)appIndex >= (uint)jpg.AppData.Count)
        {
            return false;
        }

        state.OutputQueue.Add([0xff]);
        state.OutputQueue.Add(jpg.AppData[appIndex]);

        return true;
    }

    private static bool EncodeCOM(JpegData jpg, JpegSerializationState state)
    {
        int comIndex = state.ComIndex++;

        if ((uint)comIndex >= (uint)jpg.ComData.Count)
        {
            return false;
        }

        state.OutputQueue.Add([0xff]);
        state.OutputQueue.Add(jpg.ComData[comIndex]);

        return true;
    }

    private static bool EncodeInterMarkerData(JpegData jpg, JpegSerializationState state)
    {
        int index = state.DataIndex++;

        if ((uint)index >= (uint)jpg.InterMarkerData.Count)
        {
            return false;
        }

        state.OutputQueue.Add(jpg.InterMarkerData[index]);

        return true;
    }

    private static bool EncodeDCTBlockSequential(ReadOnlySpan<int> coeffs, JpegHuffmanCodeTable dcHuff, JpegHuffmanCodeTable acHuff, int numZeroRuns, ref int lastDcCoeff, JpegBitWriter bw)
    {
        int temp2;
        int temp;
        int litmus = 0;

        temp2 = coeffs[0];
        temp = temp2 - lastDcCoeff;
        lastDcCoeff = temp2;

        temp2 = temp >> ((8 * sizeof(int)) - 1);
        temp += temp2;
        temp2 ^= temp;

        int dcNBits = temp2 == 0 ? 0 : JxlMath.FloorLog2Nonzero(temp2) + 1;

        WriteSymbol(dcNBits, dcHuff, bw);

        if (dcNBits != 0)
        {
            bw.WriteBits(dcNBits, (ulong)(temp & ((1 << dcNBits) - 1)));
        }

        int r = 0;

        for (int i = 1; i < JxlFrameDimensions.DctBlockSize; ++i)
        {
            temp = coeffs[JpegDataConstants.JpegNaturalOrder[i]];

            if (temp == 0)
            {
                ++r;
            }
            else
            {
                temp2 = temp >> ((8 * sizeof(int)) - 1);
                temp += temp2;
                temp2 ^= temp;

                if (r > 15)
                {
                    WriteSymbol(0xf0, acHuff, bw);
                    r -= 16;

                    if (r > 15)
                    {
                        WriteSymbol(0xf0, acHuff, bw);
                        r -= 16;
                    }

                    if (r > 15)
                    {
                        WriteSymbol(0xf0, acHuff, bw);
                        r -= 16;
                    }
                }

                litmus |= temp2;

                int acNBits = (int)JxlMath.FloorLog2Nonzero((uint)(ushort)temp2) + 1;
                int symbol = (r << 4) + acNBits;

                WriteSymbolBits(
                    symbol,
                    acHuff,
                    bw,
                    acNBits,
                    (ulong)(temp & ((1 << acNBits) - 1)));

                r = 0;
            }
        }

        for (int i = 0; i < numZeroRuns; ++i)
        {
            WriteSymbol(0xf0, acHuff, bw);
            r -= 16;
        }

        if (r > 0)
        {
            WriteSymbol(0, acHuff, bw);
        }

        return litmus >= 0;
    }

    private static bool EncodeDCTBlockProgressive(
        ReadOnlySpan<int> coeffs,
        JpegHuffmanCodeTable dcHuff,
        JpegHuffmanCodeTable acHuff,
        int ss,
        int se,
        int al,
        int numZeroRuns,
        JpegDctCodingState codingState,
        ref int lastDcCoeff,
        JpegBitWriter bw)
    {
        bool eobRunAllowed = ss > 0;

        int temp2;
        int temp;

        if (ss == 0)
        {
            temp2 = coeffs[0] >> al;
            temp = temp2 - lastDcCoeff;
            lastDcCoeff = temp2;

            temp2 = temp;

            if (temp < 0)
            {
                temp = -temp;

                if (temp < 0)
                {
                    return false;
                }

                --temp2;
            }

            int nBits = temp == 0
                ? 0
                : JxlMath.FloorLog2Nonzero(temp) + 1;

            WriteSymbol(nBits, dcHuff, bw);

            if (nBits != 0)
            {
                bw.WriteBits(nBits, (ulong)(temp2 & ((1 << nBits) - 1)));
            }

            ++ss;
        }

        if (ss > se)
        {
            return true;
        }

        int r = 0;

        for (int k = ss; k <= se; ++k)
        {
            temp = coeffs[JpegDataConstants.JpegNaturalOrder[k]];

            if (temp == 0)
            {
                ++r;
                continue;
            }

            if (temp < 0)
            {
                temp = -temp;

                if (temp < 0)
                {
                    return false;
                }

                temp >>= al;
                temp2 = ~temp;
            }
            else
            {
                temp >>= al;
                temp2 = temp;
            }

            if (temp == 0)
            {
                ++r;
                continue;
            }

            codingState.Flush(bw);

            while (r > 15)
            {
                WriteSymbol(0xf0, acHuff, bw);
                r -= 16;
            }

            int nBits = JxlMath.FloorLog2Nonzero(temp) + 1;
            int symbol = (r << 4) + nBits;

            WriteSymbol(symbol, acHuff, bw);
            bw.WriteBits(nBits, (ulong)(temp2 & ((1 << nBits) - 1)));

            r = 0;
        }

        if (numZeroRuns > 0)
        {
            codingState.Flush(bw);

            for (int i = 0; i < numZeroRuns; ++i)
            {
                WriteSymbol(0xf0, acHuff, bw);
                r -= 16;
            }
        }

        if (r > 0)
        {
            BufferEndOfBand(
                codingState,
                acHuff,
                [],
                bw);

            if (!eobRunAllowed)
            {
                codingState.Flush(bw);
            }
        }

        return true;
    }

    private static bool EncodeRefinementBits(
        ReadOnlySpan<int> coeffs,
        JpegHuffmanCodeTable acHuff,
        int ss,
        int se,
        int al,
        JpegDctCodingState codingState,
        JpegBitWriter bw)
    {
        bool eobRunAllowed = ss > 0;

        if (ss == 0)
        {
            // Emit next bit of DC component.
            bw.WriteBits(1, (ulong)((coeffs[0] >> al) & 1));

            ++ss;
        }

        if (ss > se)
        {
            return true;
        }

        Span<int> absValues = stackalloc int[JxlFrameDimensions.DctBlockSize];

        int eob = 0;

        for (int k = ss; k <= se; ++k)
        {
            int absValue = Numerics.Abs(coeffs[JpegDataConstants.JpegNaturalOrder[k]]);

            absValues[k] = absValue >> al;

            if (absValues[k] == 1)
            {
                eob = k;
            }
        }

        int r = 0;

        Span<int> refinementBits = stackalloc int[JxlFrameDimensions.DctBlockSize];

        int refinementBitsCount = 0;

        for (int k = ss; k <= se; ++k)
        {
            if (absValues[k] == 0)
            {
                ++r;
                continue;
            }

            while (r > 15 && k <= eob)
            {
                codingState.Flush(bw);

                WriteSymbol(0xf0, acHuff, bw);
                r -= 16;

                for (int i = 0; i < refinementBitsCount; ++i)
                {
                    bw.WriteBits(1, (ulong)refinementBits[i]);
                }

                refinementBitsCount = 0;
            }

            if (absValues[k] > 1)
            {
                refinementBits[refinementBitsCount++] =
                    absValues[k] & 1;

                continue;
            }

            codingState.Flush(bw);

            int symbol = (r << 4) + 1;

            int newNonZeroBit = coeffs[JpegDataConstants.JpegNaturalOrder[k]] < 0 ? 0 : 1;

            WriteSymbol(symbol, acHuff, bw);
            bw.WriteBits(1, (ulong)newNonZeroBit);

            for (int i = 0; i < refinementBitsCount; ++i)
            {
                bw.WriteBits(1, (ulong)refinementBits[i]);
            }

            refinementBitsCount = 0;
            r = 0;
        }

        if (r > 0 || refinementBitsCount != 0)
        {
            BufferEndOfBand(
                codingState,
                acHuff,
                refinementBits,
                bw);

            if (!eobRunAllowed)
            {
                codingState.Flush(bw);
            }
        }

        return true;
    }

    private static JpegSerializationStatus DoEncodeScan(
        JpegData jpg,
        JpegSerializationState state,
        int mode)
    {
        JpegScanInfo scanInfo = jpg.ScanInfos[state.ScanIndex];
        EncodeScanState ss = state.ScanState;

        int restartInterval = state.SeenDriMarker
            ? jpg.RestartInterval
            : 0;

        int GetNextExtraZeroRunIndex()
        {
            if (ss.ExtraZeroRunsPos < scanInfo.ExtraZeroRuns.Count)
            {
                return scanInfo.ExtraZeroRuns[ss.ExtraZeroRunsPos].BlockIdx;
            }

            return -1;
        }

        int GetNextResetPoint()
        {
            if (ss.NextResetPointPos < scanInfo.ResetPoints.Count)
            {
                return scanInfo.ResetPoints[ss.NextResetPointPos++];
            }

            return -1;
        }

        if (ss.Stage == EncodeScanState.StageHead)
        {
            if (!EncodeSOS(jpg, scanInfo, state))
            {
                return JpegSerializationStatus.Error;
            }

            ss.Bw = new JpegBitWriter();
            ss.CodingState = new JpegDctCodingState();
            ss.RestartsToGo = restartInterval;
            ss.NextRestartMarker = 0;
            ss.BlockScanIndex = 0;
            ss.ExtraZeroRunsPos = 0;
            ss.NextExtraZeroRunIndex = GetNextExtraZeroRunIndex();
            ss.NextResetPointPos = 0;
            ss.NextResetPoint = GetNextResetPoint();
            ss.McuY = 0;
            ss.LastDcCoeff.AsSpan().Clear();
            ss.Stage = EncodeScanState.StageBody;
        }

        JpegBitWriter bw = ss.Bw;
        JpegDctCodingState codingState = ss.CodingState;

        if (ss.Stage != EncodeScanState.StageBody)
        {
            return JpegSerializationStatus.Error;
        }

        bool isInterleaved = scanInfo.NumComponents > 1;

        jpg.CalculateMcuSize(scanInfo, out int mcusPerRow, out int mcuRows);

        bool isProgressive = state.IsProgressive;
        int al = isProgressive ? scanInfo.Al : 0;
        int ssValue = isProgressive ? scanInfo.Ss : 0;
        int se = isProgressive ? scanInfo.Se : 63;

        bool wantAc = ssValue != 0 || se != 0;
        bool wantDc = ssValue == 0;

        bool completeAc = true;
        bool hasAc = true;

        if (wantAc && !hasAc)
        {
            return JpegSerializationStatus.NeedsMoreInput;
        }

        bool completeDc = hasAc;
        bool complete = wantAc ? completeAc : completeDc;

        _ = complete;

        int lastMcuY = complete ? mcuRows : 0;

        for (; ss.McuY < lastMcuY; ++ss.McuY)
        {
            for (int mcuX = 0; mcuX < mcusPerRow; ++mcuX)
            {
                if (restartInterval > 0 && ss.RestartsToGo == 0)
                {
                    codingState.Flush(bw);

                    if (!bw.JumpToByteBoundary(ref state.PadBits))
                    {
                        return JpegSerializationStatus.Error;
                    }

                    bw.EmitMarker((byte)(0xd0 + ss.NextRestartMarker));

                    ss.NextRestartMarker++;
                    ss.NextRestartMarker &= 0x7;
                    ss.RestartsToGo = restartInterval;
                    ss.LastDcCoeff.AsSpan().Clear();
                }

                // Encode one MCU.
                for (int i = 0; i < scanInfo.NumComponents; ++i)
                {
                    JpegComponentScanInfo si = scanInfo.Components[i];
                    JpegComponent c = jpg.Components[si.ComponentIndex];

                    int dcTblIdx = si.DcTableIndex;
                    int acTblIdx = si.AcTableIndex;

                    JpegHuffmanCodeTable dcHuff = state.DcHuffTable[dcTblIdx];
                    JpegHuffmanCodeTable acHuff = state.AcHuffTable[acTblIdx];

                    if (wantDc && !dcHuff.Initialized)
                    {
                        return JpegSerializationStatus.Error;
                    }

                    if (wantAc && !acHuff.Initialized)
                    {
                        return JpegSerializationStatus.Error;
                    }

                    int nBlocksY = isInterleaved ? c.VerticalSampleFactor : 1;
                    int nBlocksX = isInterleaved ? c.HorizontalSampleFactor : 1;

                    for (int iy = 0; iy < nBlocksY; ++iy)
                    {
                        for (int ix = 0; ix < nBlocksX; ++ix)
                        {
                            int blockY = (ss.McuY * nBlocksY) + iy;
                            int blockX = (mcuX * nBlocksX) + ix;
                            int blockIdx = (blockY * c.WidthInBlocks) + blockX;

                            if (ss.BlockScanIndex == ss.NextResetPoint)
                            {
                                codingState.Flush(bw);
                                ss.NextResetPoint = GetNextResetPoint();
                            }

                            int numZeroRuns = 0;

                            if (ss.BlockScanIndex == ss.NextExtraZeroRunIndex)
                            {
                                numZeroRuns = scanInfo.ExtraZeroRuns[ss.ExtraZeroRunsPos].NumExtraZeroRuns;
                                ++ss.ExtraZeroRunsPos;
                                ss.NextExtraZeroRunIndex = GetNextExtraZeroRunIndex();
                            }

                            ReadOnlySpan<int> coeffs = CollectionsMarshal.AsSpan(c.Coefficients).Slice(blockIdx << 6, JxlFrameDimensions.DctBlockSize);

                            bool ok;

                            bw.Reserve(512);

                            if (mode == 0)
                            {
                                ok = EncodeDCTBlockSequential(
                                    coeffs,
                                    dcHuff,
                                    acHuff,
                                    numZeroRuns,
                                    ref ss.LastDcCoeff[si.ComponentIndex],
                                    bw);
                            }
                            else if (mode == 1)
                            {
                                ok = EncodeDCTBlockProgressive(
                                    coeffs,
                                    dcHuff,
                                    acHuff,
                                    ssValue,
                                    se,
                                    al,
                                    numZeroRuns,
                                    codingState,
                                    ref ss.LastDcCoeff[si.ComponentIndex],
                                    bw);
                            }
                            else
                            {
                                ok = EncodeRefinementBits(
                                    coeffs,
                                    acHuff,
                                    ssValue,
                                    se,
                                    al,
                                    codingState,
                                    bw);
                            }

                            if (!ok)
                            {
                                return JpegSerializationStatus.Error;
                            }

                            ++ss.BlockScanIndex;
                        }
                    }
                }

                --ss.RestartsToGo;
            }
        }

        if (ss.McuY < mcuRows)
        {
            if (!bw.Healthy)
            {
                return JpegSerializationStatus.Error;
            }

            return JpegSerializationStatus.NeedsMoreInput;
        }

        codingState.Flush(bw);

        if (!bw.JumpToByteBoundary(
                ref state.PadBits,
                state.PadBitsEnd))
        {
            return JpegSerializationStatus.Error;
        }

        bw.Finish();

        ss.Stage = EncodeScanState.StageHead;
        ++state.ScanIndex;

        if (!bw.Healthy)
        {
            return JpegSerializationStatus.Error;
        }

        return JpegSerializationStatus.Done;
    }

    private static JpegSerializationStatus EncodeScan(JpegData jpg, JpegSerializationState state)
    {
        JpegScanInfo scanInfo = jpg.ScanInfos[state.ScanIndex];

        bool isProgressive = state.IsProgressive;

        int al = isProgressive ? scanInfo.Al : 0;
        int ah = isProgressive ? scanInfo.Ah : 0;
        int ss = isProgressive ? scanInfo.Ss : 0;
        int se = isProgressive ? scanInfo.Se : 63;

        bool needSequential =
            !isProgressive ||
            (ah == 0 &&
             al == 0 &&
             ss == 0 &&
             se == 63);

        if (needSequential)
        {
            return DoEncodeScan(jpg, state, 0);
        }

        if (ah == 0)
        {
            return DoEncodeScan(jpg, state, 1);
        }

        return DoEncodeScan(jpg, state, 2);
    }

    private static JpegSerializationStatus SerializeSection(byte marker, JpegSerializationState state, JpegData jpg)
    {
        static JpegSerializationStatus ToStatus(bool result) =>
            result
                ? JpegSerializationStatus.Done
                : JpegSerializationStatus.Error;

        return marker switch
        {
            0xc0 or 0xc1 or 0xc2 or 0xc9 or 0xca => ToStatus(EncodeSOF(jpg, marker, state)),
            0xc4 => ToStatus(EncodeDHT(jpg, state)),
            0xd0 or 0xd1 or 0xd2 or 0xd3 or 0xd4 or 0xd5 or 0xd6 or 0xd7 => ToStatus(EncodeRestart(marker, state)),
            0xd9 => ToStatus(EncodeEOI(jpg, state)),
            0xda => EncodeScan(jpg, state),
            0xdb => ToStatus(EncodeDQT(jpg, state)),
            0xdd => ToStatus(EncodeDRI(jpg, state)),
            0xe0 or 0xe1 or 0xe2 or 0xe3 or 0xe4 or 0xe5 or 0xe6 or 0xe7 or 0xe8 or 0xe9 or 0xea or 0xeb or 0xec or 0xed or 0xee or 0xef => ToStatus(EncodeAPP(jpg, marker, state)),
            0xfe => ToStatus(EncodeCOM(jpg, state)),
            0xff => ToStatus(EncodeInterMarkerData(jpg, state)),
            _ => JpegSerializationStatus.Error,
        };
    }

    private static bool WriteJpegInternal(JpegData jpg, JpegOutput output, JpegSerializationState state)
    {
        bool MaybePushOutput()
        {
            if (state.Stage != JpegSerializationStage.Error)
            {
                while (state.OutputQueue.Count > 0)
                {
                    List<byte> chunk = state.OutputQueue[0];

                    int numWritten = output.Write(
                        chunk.Next,
                        chunk.Count);

                    if (numWritten == 0 && chunk.Count > 0)
                    {
                        return false;
                    }

                    if (chunk.Count - numWritten == 0)
                    {
                        state.OutputQueue.RemoveAt(0);
                    }
                }
            }

            return true;
        }

        while (true)
        {
            switch (state.Stage)
            {
                case JpegSerializationStage.Initialize:
                {
                    // Valid Brunsli requires at least the 0xD9 marker.
                    // This might happen on a corrupted stream or unconditioned JPEGData.
                    if (jpg.MarkerOrder.Count == 0)
                    {
                        state.Stage = JpegSerializationStage.Error;
                        break;
                    }

                    state.DcHuffTable = new JpegHuffmanCodeTable[JpegDataConstants.MaxHuffmanTables];
                    state.AcHuffTable = new JpegHuffmanCodeTable[JpegDataConstants.MaxHuffmanTables];

                    if (jpg.HasZeroPaddingBit)
                    {
                        state.PadBits = jpg.PaddingBits;
                    }

                    _ = EncodeSOI(state);

                    if (!MaybePushOutput())
                    {
                        return false;
                    }

                    state.Stage = JpegSerializationStage.SerializeSection;

                    break;
                }

                case JpegSerializationStage.SerializeSection:
                {
                    if (state.SectionIndex >= jpg.MarkerOrder.Count)
                    {
                        state.Stage = JpegSerializationStage.Done;
                        break;
                    }

                    byte marker = jpg.MarkerOrder[state.SectionIndex];

                    JpegSerializationStatus status =
                        SerializeSection(marker, state, jpg);

                    if (status == JpegSerializationStatus.Error)
                    {
                        state.Stage = JpegSerializationStage.Error;
                        break;
                    }

                    if (!MaybePushOutput())
                    {
                        return false;
                    }

                    if (status == JpegSerializationStatus.NeedsMoreInput)
                    {
                        return false;
                    }

                    if (status != JpegSerializationStatus.Done)
                    {
                        state.Stage = JpegSerializationStage.Error;
                        return false;
                    }

                    ++state.SectionIndex;
                    break;
                }

                case JpegSerializationStage.Done:
                {
                    if (state.OutputQueue.Count != 0)
                    {
                        return false;
                    }

                    if (state.PadBits.Length != 0)
                    {
                        return false;
                    }

                    return true;
                }

                case JpegSerializationStage.Error:
                    return false;

                default:
                    return false;
            }
        }
    }

    public static bool WriteJpeg(JpegData jpg, JpegOutput output)
    {
        JpegSerializationState state = new();
        return WriteJpegInternal(jpg, output, state);
    }
}

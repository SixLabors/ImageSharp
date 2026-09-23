// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Jxl.IO.Jpeg.Data;
using SixLabors.ImageSharp.Formats.Jxl.Processing;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;
using HuffmanTableEntry = SixLabors.ImageSharp.Formats.Jxl.IO.Jpeg.JpegHuffmanDecoder.HuffmanTableEntry;

namespace SixLabors.ImageSharp.Formats.Jxl.IO.Jpeg;

/// <summary>
/// JPEG file parser. The codec uses this parser as part of its
/// JPEG to JPEG XL lossless compression feature.
/// </summary>
internal static class JpegReader
{
    private const int BrunsliMaxSampling = 15;

    /// <summary>
    /// Gets a lookup used by <see cref="FindNextMarker(ReadOnlySpan{byte}, int)"/>
    /// </summary>
    private static ReadOnlySpan<byte> IsValidMarkerLookup =>
    [
        1, 1, 1, 0, 1, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0,
        1, 1, 1, 1, 1, 1, 1, 1,
        0, 1, 1, 1, 0, 1, 0, 0,
        1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1,
        0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 1, 0,
    ];

    private static int ReadUint8(ReadOnlySpan<byte> data, ref int pos) => data[pos++];

    private static int ReadUint16(ReadOnlySpan<byte> data, ref int pos)
    {
        int value = (data[pos] << 8) | data[pos + 1];
        pos += 2;
        return value;
    }

    private static void VerifyLength(int pos, int length, int needed)
    {
        if ((uint)needed > (uint)(length - pos))
        {
            throw new InvalidDataException($"Unexpected end of input: pos={pos} need={needed} len={length}");
        }
    }

    private static void VerifyInput(int value, int low, int high, string name)
    {
        if (value < low || value > high)
        {
            throw new InvalidDataException($"Invalid {name}: {value}");
        }
    }

    private static void VerifyMarkerEnd(int startPos, int markerLength, int pos)
    {
        if (startPos + markerLength != pos)
        {
            throw new InvalidDataException($"Invalid marker length: declared={markerLength} actual={pos - startPos}");
        }
    }

    private static void ExpectMarker(ReadOnlySpan<byte> data, int pos)
    {
        if (pos > data.Length - 2 || data[pos] != 0xff)
        {
            int value = pos < data.Length ? data[pos] : 0;

            throw new InvalidDataException($"Marker byte (0xff) expected, found: 0x{value:x2} pos={pos} len={data.Length}");
        }
    }

    private static void ProcessMarkerSOF(ReadOnlySpan<byte> data, int len, JpegReadMode mode, ref int pos, JpegData jpg)
    {
        if (jpg.Width != 0)
        {
            throw new InvalidDataException("Duplicate SOF marker");
        }

        int startPos = pos;
        VerifyLength(pos, len, 8);

        int markerLength = ReadUint16(data, ref pos);
        int precision = ReadUint8(data, ref pos);
        int height = ReadUint16(data, ref pos);
        int width = ReadUint16(data, ref pos);
        int numComponents = ReadUint8(data, ref pos);

        VerifyInput(precision, 8, 8, nameof(precision));
        VerifyInput(height, 1, JpegDataConstants.MaxDimPixels, nameof(height));
        VerifyInput(width, 1, JpegDataConstants.MaxDimPixels, nameof(width));
        VerifyInput(numComponents, 1, JpegDataConstants.MaxComponents, nameof(numComponents));

        VerifyLength(pos, len, 3 * numComponents);

        jpg.Height = height;
        jpg.Width = width;
        jpg.Components = new(numComponents);

        Span<bool> idsSeen = stackalloc bool[256];

        int maxHSampFactor = 1;
        int maxVSampFactor = 1;

        foreach (JpegComponent component in jpg.Components)
        {
            int id = ReadUint8(data, ref pos);

            if (idsSeen[id])
            {
                throw new InvalidDataException($"Duplicate ID {id} in SOF.");
            }

            idsSeen[id] = true;
            component.Id = id;

            int factor = ReadUint8(data, ref pos);
            int hSampFactor = factor >> 4;
            int vSampFactor = factor & 0xf;

            VerifyInput(hSampFactor, 1, BrunsliMaxSampling, nameof(hSampFactor));
            VerifyInput(vSampFactor, 1, BrunsliMaxSampling, nameof(vSampFactor));

            component.HorizontalSampleFactor = hSampFactor;
            component.VerticalSampleFactor = vSampFactor;
            component.QuantIndex = ReadUint8(data, ref pos);

            maxHSampFactor = Math.Max(maxHSampFactor, hSampFactor);
            maxVSampFactor = Math.Max(maxVSampFactor, vSampFactor);
        }

        int mcuRows = JxlMath.DivCeil(height, maxVSampFactor * 8);
        int mcuCols = JxlMath.DivCeil(width, maxHSampFactor * 8);

        foreach (JpegComponent component in jpg.Components)
        {
            if (maxHSampFactor % component.HorizontalSampleFactor != 0 ||
                maxVSampFactor % component.VerticalSampleFactor != 0)
            {
                throw new InvalidDataException("Non-integral subsampling ratios");
            }

            component.WidthInBlocks = mcuCols * component.HorizontalSampleFactor;
            component.HeightInBlocks = mcuRows * component.VerticalSampleFactor;
        }

        VerifyMarkerEnd(startPos, markerLength, pos);
    }

    private static void ProcessMarkerSOS(ReadOnlySpan<byte> data, int len, ref int pos, JpegData jpg)
    {
        int startPos = pos;

        VerifyLength(pos, len, 3);

        int markerLength = ReadUint16(data, ref pos);
        int componentsInScan = ReadUint8(data, ref pos);

        VerifyInput(
            componentsInScan,
            1,
            jpg.Components.Count,
            nameof(componentsInScan));

        JpegScanInfo scanInfo = new()
        {
            NumComponents = componentsInScan
        };

        VerifyLength(pos, len, 2 * componentsInScan);

        Span<bool> idsSeen = stackalloc bool[256];

        for (int i = 0; i < componentsInScan; i++)
        {
            int id = ReadUint8(data, ref pos);

            if (idsSeen[id])
            {
                throw new InvalidDataException($"Duplicate ID {id} in SOS.");
            }

            idsSeen[id] = true;

            int componentIndex = -1;

            for (int j = 0; j < jpg.Components.Count; j++)
            {
                if (jpg.Components[j].Id == id)
                {
                    componentIndex = j;
                    break;
                }
            }

            if (componentIndex < 0)
            {
                throw new InvalidDataException($"SOS marker: Could not find component with id {id}");
            }

            int c = ReadUint8(data, ref pos);

            int dcTableIndex = c >> 4;
            int acTableIndex = c & 0xf;

            VerifyInput(dcTableIndex, 0, 3, nameof(dcTableIndex));
            VerifyInput(acTableIndex, 0, 3, nameof(acTableIndex));

            scanInfo.Components[i].ComponentIndex = componentIndex;
            scanInfo.Components[i].DcTableIndex = dcTableIndex;
            scanInfo.Components[i].AcTableIndex = acTableIndex;
        }

        VerifyLength(pos, len, 3);

        scanInfo.Ss = ReadUint8(data, ref pos);
        scanInfo.Se = ReadUint8(data, ref pos);

        VerifyInput(scanInfo.Ss, 0, 63, nameof(scanInfo.Ss));
        VerifyInput(
            scanInfo.Se,
            scanInfo.Ss,
            63,
            nameof(scanInfo.Se));

        int c2 = ReadUint8(data, ref pos);

        scanInfo.Ah = c2 >> 4;
        scanInfo.Al = c2 & 0xf;

        if (scanInfo.Ah != 0 && scanInfo.Al != scanInfo.Ah - 1)
        {
            JpegDebug.LogWarning($"Invalid progressive parameters: Al={scanInfo.Al}, Ah={scanInfo.Ah}");
        }

        // Check that all Huffman tables needed for this scan are defined.
        for (int i = 0; i < componentsInScan; i++)
        {
            bool foundDcTable = false;
            bool foundAcTable = false;

            int dcTableIndex = scanInfo.Components[i].DcTableIndex;
            int acTableIndex = scanInfo.Components[i].AcTableIndex;

            foreach (JpegHuffmanCode code in jpg.HuffmanCodes)
            {
                int slotId = code.SlotId;

                if (slotId == dcTableIndex)
                {
                    foundDcTable = true;
                }
                else if (slotId == acTableIndex + 16)
                {
                    foundAcTable = true;
                }
            }

            if (scanInfo.Ss == 0 && !foundDcTable)
            {
                throw new InvalidDataException(
                    $"SOS marker: Could not find DC Huffman table " +
                    $"with index {dcTableIndex}");
            }

            if (scanInfo.Se > 0 && !foundAcTable)
            {
                throw new InvalidDataException(
                    $"SOS marker: Could not find AC Huffman table " +
                    $"with index {acTableIndex}");
            }
        }

        jpg.ScanInfos.Add(scanInfo);
        VerifyMarkerEnd(startPos, markerLength, pos);
    }

    private static void ProcessMarkerDHT(ReadOnlySpan<byte> data, int len, JpegReadMode mode, Span<HuffmanTableEntry> dcHuffLut, Span<HuffmanTableEntry> acHuffLut, ref int pos, JpegData jpg)
    {
        int startPos = pos;

        VerifyLength(pos, len, 2);

        int markerLength = ReadUint16(data, ref pos);

        if (markerLength == 2)
        {
            // Empty DHT marker. Useless, but does occur in the wild.
            // Represent it with a dummy all-zeroes Huffman table.
            JpegHuffmanCode huff = new()
            {
                IsLast = true
            };

            jpg.HuffmanCodes.Add(huff);
            return;
        }

        Span<bool> valuesSeen = stackalloc bool[256];

        while (pos < startPos + markerLength)
        {
            VerifyLength(pos, len, 1 + JpegDataConstants.JpegHuffmanMaxBitLength);

            JpegHuffmanCode huff = new()
            {
                SlotId = ReadUint8(data, ref pos)
            };

            int huffmanIndex = huff.SlotId;
            bool isAcTable = (huff.SlotId & 0x10) != 0;

            Span<HuffmanTableEntry> huffLut;

            if (isAcTable)
            {
                huffmanIndex -= 0x10;
                VerifyInput(huffmanIndex, 0, 3, nameof(huffmanIndex));

                huffLut = acHuffLut.Slice(
                    huffmanIndex * JpegHuffmanDecoder.LookupSize,
                    JpegHuffmanDecoder.LookupSize);
            }
            else
            {
                VerifyInput(huffmanIndex, 0, 3, nameof(huffmanIndex));

                huffLut = dcHuffLut.Slice(
                    huffmanIndex * JpegHuffmanDecoder.LookupSize,
                    JpegHuffmanDecoder.LookupSize);
            }

            huff.Counts[0] = 0;

            int totalCount = 0;
            int space = 1 << JpegDataConstants.JpegHuffmanMaxBitLength;
            int maxDepth = 1;

            for (int i = 1; i <= JpegDataConstants.JpegHuffmanMaxBitLength; i++)
            {
                int count = ReadUint8(data, ref pos);

                if (count != 0)
                {
                    maxDepth = i;
                }

                huff.Counts[i] = count;
                totalCount += count;

                space -= count * (1 << (JpegDataConstants.JpegHuffmanMaxBitLength - i));
            }

            VerifyInput(
                totalCount,
                0,
                isAcTable ? JpegDataConstants.JpegHuffmanAlphabetSize : JpegDataConstants.JpegDcAlphabetSize,
                nameof(totalCount));

            VerifyLength(pos, len, totalCount);

            valuesSeen.Clear(); // set everything to false

            for (int i = 0; i < totalCount; i++)
            {
                int value = ReadUint8(data, ref pos);

                if (!isAcTable)
                {
                    VerifyInput(
                        value,
                        0,
                        JpegDataConstants.JpegDcAlphabetSize - 1,
                        nameof(value));
                }

                if (valuesSeen[value])
                {
                    throw new InvalidDataException($"Duplicate Huffman code value {value}");
                }

                valuesSeen[value] = true;
                huff.Values[i] = value;
            }

            huff.Counts[maxDepth]++;
            huff.Values[totalCount] = JpegDataConstants.JpegHuffmanAlphabetSize;

            space -= 1 << (JpegDataConstants.JpegHuffmanMaxBitLength - maxDepth);

            if (space < 0)
            {
                throw new InvalidDataException("Invalid Huffman code lengths");
            }

            if (space > 0 && huffLut[0].Value != 0xffff)
            {
                for (int i = 0; i < JpegHuffmanDecoder.LookupSize; i++)
                {
                    huffLut[i].Bits = 0;
                    huffLut[i].Value = 0xffff;
                }
            }

            huff.IsLast = pos == startPos + markerLength;

            if (mode == JpegReadMode.ReadEverything)
            {
                JpegHuffmanDecoder.BuildJpegHuffmanTable(
                    huff.Counts,
                    huff.Values,
                    huffLut);
            }

            jpg.HuffmanCodes.Add(huff);
        }

        VerifyMarkerEnd(startPos, markerLength, pos);
    }

    private static void ProcessMarkerDQT(ReadOnlySpan<byte> data, int len, ref int pos, JpegData jpg)
    {
        int startPos = pos;
        VerifyLength(pos, len, 2);
        int markerLength = ReadUint16(data, ref pos);

        if (markerLength == 2)
        {
            throw new InvalidDataException("DQT marker: no quantization table found");
        }

        while (pos < startPos + markerLength && jpg.Quant.Count < JpegDataConstants.MaximumQuantizationTables)
        {
            VerifyLength(pos, len, 1);

            int quantTableIndex = ReadUint8(data, ref pos);
            int quantTablePrecision = quantTableIndex >> 4;

            VerifyInput(
                quantTablePrecision,
                0,
                1,
                nameof(quantTablePrecision));

            quantTableIndex &= 0xf;

            VerifyInput(
                quantTableIndex,
                0,
                3,
                nameof(quantTableIndex));

            VerifyLength(
                pos,
                len,
                (quantTablePrecision + 1) * JxlFrameDimensions.DctBlockSize);

            JpegQuantizationTable table = new()
            {
                Index = quantTableIndex,
                Precision = quantTablePrecision
            };

            for (int i = 0; i < JxlFrameDimensions.DctBlockSize; i++)
            {
                int quantValue = quantTablePrecision != 0
                    ? ReadUint16(data, ref pos)
                    : ReadUint8(data, ref pos);

                VerifyInput(
                    quantValue,
                    1,
                    65535,
                    nameof(quantValue));

                table.Values[JpegDataConstants.JpegNaturalOrder[i]] = quantValue;
            }

            table.IsLast = pos == startPos + markerLength;

            jpg.Quant.Add(table);
        }

        VerifyMarkerEnd(startPos, markerLength, pos);
    }

    private static void ProcessDRI(ReadOnlySpan<byte> data, int len, ref int pos, ref bool foundDri, JpegData jpg)
    {
        if (foundDri)
        {
            throw new InvalidDataException("Duplicate DRI marker.");
        }

        foundDri = true;
        int startPos = pos;

        VerifyLength(pos, len, 4);

        int markerLength = ReadUint16(data, ref pos);
        int restartInterval = ReadUint16(data, ref pos);

        jpg.RestartInterval = restartInterval;

        VerifyMarkerEnd(startPos, markerLength, pos);
    }

    private static void ProcessAPP(ReadOnlySpan<byte> data, int len, ref int pos, JpegData jpg)
    {
        VerifyLength(pos, len, 2);

        int markerLength = ReadUint16(data, ref pos);

        VerifyInput(markerLength, 2, 65535, nameof(markerLength));
        VerifyLength(pos, len, markerLength - 2);

        if (pos < 3)
        {
            throw new InvalidDataException("Invalid APP marker position");
        }

        ReadOnlySpan<byte> appData = data.Slice(pos - 3, markerLength + 1);
        jpg.AppData.Add([.. appData]);

        pos += markerLength - 2;
    }

    private static void ProcessMarkerCOM(ReadOnlySpan<byte> data, int len, ref int pos, JpegData jpg)
    {
        VerifyLength(pos, len, 2);

        int markerLength = ReadUint16(data, ref pos);

        VerifyInput(markerLength, 2, 65535, nameof(markerLength));
        VerifyLength(pos, len, markerLength - 2);

        if (pos < 3)
        {
            throw new InvalidDataException("Invalid COM marker position");
        }

        ReadOnlySpan<byte> comData = data.Slice(pos - 3, markerLength + 1);
        jpg.ComData.Add([.. comData]);

        pos += markerLength - 2;
    }

    private static int ReadSymbol(ReadOnlySpan<HuffmanTableEntry> table, ref BitReaderState br)
    {
        br.FillBitWindow();

        int value = (int)((br.Value >> (br.BitsLeft - 8)) & 0xff);
        HuffmanTableEntry entry = table[value];

        int bitCount = entry.Bits - 8;

        if (bitCount > 0)
        {
            br.BitsLeft -= 8;

            entry = table[entry.Value + (int)(
                (br.Value >> (br.BitsLeft - bitCount)) &
                ((1UL << bitCount) - 1))];
        }

        br.BitsLeft -= entry.Bits;
        return entry.Value;
    }

    private static int HuffExtend(int x, int s)
    {
        DebugGuard.MustBeGreaterThanOrEqualTo(s, 1, nameof(s));

        int half = 1 << (s - 1);

        if (x >= half)
        {
            if (x < (1 << s))
            {
                throw new InvalidOperationException("x is invalid");
            }

            return x;
        }

        return x - (1 << s) + 1;
    }

    private static bool DecodeDCTBlock(
        ReadOnlySpan<HuffmanTableEntry> dcHuff,
        ReadOnlySpan<HuffmanTableEntry> acHuff,
        int ss,
        int se,
        int al,
        ref int eobRun,
        ref bool resetState,
        ref int numZeroRuns,
        ref BitReaderState br,
        ref int lastDcCoeff,
        Span<int> coeffs)
    {
        int am = 1 << al;
        bool eobRunAllowed = ss > 0;

        if (ss == 0)
        {
            int s = ReadSymbol(dcHuff, ref br);

            if (s >= JpegDataConstants.JpegDcAlphabetSize)
            {
                return false;
            }

            int diff = 0;

            if (s > 0)
            {
                int bits = br.ReadBits(s);
                diff = HuffExtend(bits, s);
            }

            int coeff = diff + lastDcCoeff;
            int dcCoeff = coeff * am;

            coeffs[0] = dcCoeff;

            if (dcCoeff != coeffs[0])
            {
                return false;
            }

            lastDcCoeff = coeff;
            ss++;
        }

        if (ss > se)
        {
            return true;
        }

        if (eobRun > 0)
        {
            eobRun--;
            return true;
        }

        numZeroRuns = 0;

        for (int k = ss; k <= se; k++)
        {
            int sr = ReadSymbol(acHuff, ref br);

            if (sr >= JpegDataConstants.JpegHuffmanAlphabetSize)
            {
                return false;
            }

            int r = sr >> 4;
            int s = sr & 15;

            if (s > 0)
            {
                k += r;

                if (k > se)
                {
                    return false;
                }

                if (s + al >= JpegDataConstants.JpegDcAlphabetSize)
                {
                    return false;
                }

                int bits = br.ReadBits(s);
                int coeff = HuffExtend(bits, s);

                coeffs[JpegDataConstants.JpegNaturalOrder[k]] = coeff * am;
                numZeroRuns = 0;
            }
            else if (r == 15)
            {
                k += 15;
                numZeroRuns++;
            }
            else
            {
                if (eobRunAllowed && k == ss && eobRun == 0)
                {
                    // Two EOB runs immediately after each other signal that
                    // the JPEG encoder should force a state reset here.
                    resetState = true;
                }

                eobRun = 1 << r;

                if (r > 0)
                {
                    if (!eobRunAllowed)
                    {
                        return false;
                    }

                    eobRun += br.ReadBits(r);
                }

                break;
            }
        }

        eobRun--;
        return true;
    }

    private static bool RefineDCTBlock(
        ReadOnlySpan<HuffmanTableEntry> acHuff,
        int ss,
        int se,
        int al,
        ref int eobRun,
        ref bool resetState,
        ref BitReaderState br,
        Span<int> coeffs)
    {
        int am = 1 << al;
        bool eobRunAllowed = ss > 0;

        int s;
        if (ss == 0)
        {
            s = br.ReadBits(1);
            int dcCoeff = coeffs[0];

            dcCoeff |= s * am;
            coeffs[0] = dcCoeff;

            ++ss;
        }

        if (ss > se)
        {
            return true;
        }

        int p1 = am;
        int m1 = -am;
        int k = ss;
        bool inZeroRun = false;

        if (eobRun <= 0)
        {
            for (; k <= se; k++)
            {
                int symbol = ReadSymbol(acHuff, ref br);

                if (symbol >= JpegDataConstants.JpegHuffmanAlphabetSize)
                {
                    return false;
                }

                int r = symbol >> 4;
                s = symbol & 15;

                if (s != 0)
                {
                    if (s != 1)
                    {
                        return false;
                    }

                    s = br.ReadBits(1) != 0 ? p1 : m1;
                    inZeroRun = false;
                }
                else
                {
                    if (r != 15)
                    {
                        if (eobRunAllowed && k == ss && eobRun == 0)
                        {
                            resetState = true;
                        }

                        eobRun = 1 << r;

                        if (r > 0)
                        {
                            if (!eobRunAllowed)
                            {
                                return false;
                            }

                            eobRun += br.ReadBits(r);
                        }

                        break;
                    }

                    inZeroRun = true;
                }

                do
                {
                    int index = JpegDataConstants.JpegNaturalOrder[k];
                    int coefficient = coeffs[index];

                    if (coefficient != 0)
                    {
                        if (br.ReadBits(1) != 0)
                        {
                            if ((coefficient & p1) == 0)
                            {
                                coefficient += coefficient >= 0
                                    ? p1
                                    : m1;
                            }
                        }

                        coeffs[index] = coefficient;
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
                while (k <= se);

                if (s != 0)
                {
                    if (k > se)
                    {
                        return false;
                    }

                    coeffs[JpegDataConstants.JpegNaturalOrder[k]] = s;
                }
            }
        }

        if (inZeroRun)
        {
            return false;
        }

        if (eobRun > 0)
        {
            for (; k <= se; k++)
            {
                int index = JpegDataConstants.JpegNaturalOrder[k];
                int coefficient = coeffs[index];

                if (coefficient != 0)
                {
                    if (br.ReadBits(1) != 0)
                    {
                        if ((coefficient & p1) == 0)
                        {
                            coefficient += coefficient >= 0
                                ? p1
                                : m1;
                        }
                    }

                    coeffs[index] = coefficient;
                }
            }
        }

        --eobRun;
        return true;
    }

    private static bool ProcessRestart(ReadOnlySpan<byte> data, ref int nextRestartMarker, ref BitReaderState br, JpegData jpg)
    {
        int pos = 0;

        if (!br.FinishStream(jpg, ref pos))
        {
            return false;
        }

        int expectedMarker = 0xd0 + nextRestartMarker;
        ExpectMarker(data, pos);

        int marker = data[pos + 1];

        if (marker != expectedMarker)
        {
            return false;
        }

        br.Reset(pos + 2);

        nextRestartMarker = (nextRestartMarker + 1) & 0x7;

        return true;
    }

    private static bool ProcessScan(ReadOnlySpan<byte> data, int len, ReadOnlySpan<HuffmanTableEntry> dcHuffLut, ReadOnlySpan<HuffmanTableEntry> acHuffLut, Span<ushort> scanProgression, bool isProgressive, ref int pos, JpegData jpg)
    {
        ProcessMarkerSOS(data, len, ref pos, jpg);

        JpegScanInfo scanInfo = jpg.ScanInfos[^1];

        bool isInterleaved = scanInfo.NumComponents > 1;

        int maxHSampFactor = 1;
        int maxVSampFactor = 1;

        foreach (JpegComponent component in jpg.Components)
        {
            maxHSampFactor = Math.Max(maxHSampFactor, component.HorizontalSampleFactor);
            maxVSampFactor = Math.Max(maxVSampFactor, component.VerticalSampleFactor);
        }

        int mcuRows = JxlMath.DivCeil(jpg.Height, maxVSampFactor * 8);
        int mcusPerRow = JxlMath.DivCeil(jpg.Width, maxHSampFactor * 8);

        if (!isInterleaved)
        {
            JpegComponent c = jpg.Components[scanInfo.Components[0].ComponentIndex];

            mcusPerRow = JxlMath.DivCeil(
                jpg.Width * c.HorizontalSampleFactor,
                8 * maxHSampFactor);

            mcuRows = JxlMath.DivCeil(
                jpg.Height * c.VerticalSampleFactor,
                8 * maxVSampFactor);
        }

        Span<int> lastDcCoeff = stackalloc int[JpegDataConstants.MaxComponents];
        lastDcCoeff.Clear();

        BitReaderState br = new(data, pos);

        int restartsToGo = jpg.RestartInterval;
        int nextRestartMarker = 0;
        int eobRun = -1;
        int blockScanIndex = 0;

        int al = isProgressive ? scanInfo.Al : 0;
        int ah = isProgressive ? scanInfo.Ah : 0;
        int ss = isProgressive ? scanInfo.Ss : 0;
        int se = isProgressive ? scanInfo.Se : 63;

        ushort scanBitmask = ah == 0 ? (ushort)(0xffff << al) : (ushort)(1u << al);
        ushort refinementBitmask = (ushort)((1 << al) - 1);

        for (int i = 0; i < scanInfo.NumComponents; ++i)
        {
            int compIdx = scanInfo.Components[i].ComponentIndex;

            for (int k = ss; k <= se; ++k)
            {
                int progressionIndex = (compIdx * JxlFrameDimensions.DctBlockSize) + k;
                ushort previousMask = scanProgression[progressionIndex];

                if ((previousMask & scanBitmask) != 0)
                {
                    return false;
                }

                if ((previousMask & refinementBitmask) != 0)
                {
                    return false;
                }

                scanProgression[progressionIndex] |= scanBitmask;
            }
        }

        if (al > 10)
        {
            return false;
        }

        foreach (JpegComponent c in jpg.Components)
        {
            if (c.Coefficients.Count == 0)
            {
                int numBlocks = c.WidthInBlocks * c.HeightInBlocks;
                c.Coefficients = new List<int>(numBlocks * JxlFrameDimensions.DctBlockSize);
            }
        }

        for (int mcuY = 0; mcuY < mcuRows; ++mcuY)
        {
            for (int mcuX = 0; mcuX < mcusPerRow; ++mcuX)
            {
                if (jpg.RestartInterval > 0)
                {
                    if (restartsToGo == 0)
                    {
                        if (!ProcessRestart(data, ref nextRestartMarker, ref br, jpg))
                        {
                            return false;
                        }

                        restartsToGo = jpg.RestartInterval;
                        lastDcCoeff.Clear();

                        if (eobRun > 0)
                        {
                            return false;
                        }

                        eobRun = -1;
                    }

                    --restartsToGo;
                }

                for (int i = 0; i < scanInfo.NumComponents; ++i)
                {
                    JpegComponentScanInfo si = scanInfo.Components[i];
                    JpegComponent c = jpg.Components[si.ComponentIndex];

                    ReadOnlySpan<HuffmanTableEntry> dcLut =
                        dcHuffLut.Slice(
                            si.DcTableIndex * JpegHuffmanDecoder.LookupSize,
                            JpegHuffmanDecoder.LookupSize);

                    ReadOnlySpan<HuffmanTableEntry> acLut =
                        acHuffLut.Slice(
                            si.AcTableIndex * JpegHuffmanDecoder.LookupSize,
                            JpegHuffmanDecoder.LookupSize);

                    int nblocksY = isInterleaved ? c.VerticalSampleFactor : 1;
                    int nblocksX = isInterleaved ? c.HorizontalSampleFactor : 1;

                    for (int iy = 0; iy < nblocksY; ++iy)
                    {
                        for (int ix = 0; ix < nblocksX; ++ix)
                        {
                            int blockY = (mcuY * nblocksY) + iy;
                            int blockX = (mcuX * nblocksX) + ix;
                            int blockIdx = (blockY * c.WidthInBlocks) + blockX;

                            bool resetState = false;
                            int numZeroRuns = 0;

                            Span<int> coeffs = CollectionsMarshal.AsSpan(c.Coefficients).Slice(blockIdx * JxlFrameDimensions.DctBlockSize, JxlFrameDimensions.DctBlockSize);

                            bool success;

                            if (ah == 0)
                            {
                                success = DecodeDCTBlock(
                                    dcLut,
                                    acLut,
                                    ss,
                                    se,
                                    al,
                                    ref eobRun,
                                    ref resetState,
                                    ref numZeroRuns,
                                    ref br,
                                    ref lastDcCoeff[si.ComponentIndex],
                                    coeffs);
                            }
                            else
                            {
                                success = RefineDCTBlock(
                                    acLut,
                                    ss,
                                    se,
                                    al,
                                    ref eobRun,
                                    ref resetState,
                                    ref br,
                                    coeffs);
                            }

                            if (!success)
                            {
                                return false;
                            }

                            if (resetState)
                            {
                                scanInfo.ResetPoints.Add(blockScanIndex);
                            }

                            if (numZeroRuns > 0)
                            {
                                scanInfo.ExtraZeroRuns.Add(
                                    new JpegExtraZeroRunInfo
                                    {
                                        BlockIndex = blockScanIndex,
                                        NumExtraZeroRuns = numZeroRuns
                                    });
                            }

                            blockScanIndex++;
                        }
                    }
                }
            }
        }

        if (eobRun > 0)
        {
            return false;
        }

        if (!br.FinishStream(jpg, ref pos))
        {
            return false;
        }

        if (pos > len)
        {
            return false;
        }

        return true;
    }

    private static int FindNextMarker(ReadOnlySpan<byte> data, int pos)
    {
        int numSkipped = 0;

        while (pos + 1 < data.Length &&
               (data[pos] != 0xff ||
                data[pos + 1] < 0xc0 ||
                IsValidMarkerLookup[data[pos + 1] - 0xc0] == 0))
        {
            pos++;
            numSkipped++;
        }

        return numSkipped;
    }

    private static bool FixupIndexes(JpegData jpg)
    {
        for (int i = 0; i < jpg.Components.Count; ++i)
        {
            JpegComponent c = jpg.Components[i];
            bool foundIndex = false;

            for (int j = 0; j < jpg.Quant.Count; ++j)
            {
                if (jpg.Quant[j].Index == c.QuantIndex)
                {
                    c.QuantIndex = j;
                    foundIndex = true;
                    break;
                }
            }

            if (!foundIndex)
            {
                return false;
            }
        }

        return true;
    }

    public static bool ReadJpeg(ReadOnlySpan<byte> data, JpegReadMode mode, JpegData jpg)
    {
        int len = data.Length;
        int pos = 0;

        ExpectMarker(data, pos);

        int marker = data[pos + 1];
        pos += 2;

        if (marker != 0xd8)
        {
            return false;
        }

        int lutSize = JpegDataConstants.MaxHuffmanTables * JpegHuffmanDecoder.LookupSize;

        Span<HuffmanTableEntry> dcHuffLut = stackalloc HuffmanTableEntry[lutSize];
        Span<HuffmanTableEntry> acHuffLut = stackalloc HuffmanTableEntry[lutSize];

        bool foundSof = false;
        bool foundSos = false;
        bool foundDri = false;

        Span<ushort> scanProgression = stackalloc ushort[JpegDataConstants.MaxComponents * JxlFrameDimensions.DctBlockSize];

        scanProgression.Clear();

        jpg.PaddingBits.Clear();

        bool isProgressive = false;

        do
        {
            int numSkipped = FindNextMarker(data, pos);

            if (numSkipped > 0)
            {
                // Add a fake marker to indicate arbitrary
                // in-between-markers data.
                jpg.MarkerOrder.Add(0xff);
                jpg.InterMarkerData.Add([.. data.Slice(pos, numSkipped)]);

                pos += numSkipped;
            }

            ExpectMarker(data, pos);

            marker = data[pos + 1];
            pos += 2;

            switch (marker)
            {
                case 0xc0:
                case 0xc1:
                case 0xc2:
                    isProgressive = marker == 0xc2;

                    ProcessMarkerSOF(
                        data,
                        len,
                        mode,
                        ref pos,
                        jpg);

                    foundSof = true;
                    break;

                case 0xc4:
                    ProcessMarkerDHT(
                            data,
                            len,
                            mode,
                            dcHuffLut,
                            acHuffLut,
                            ref pos,
                            jpg);

                    break;

                case 0xd0:
                case 0xd1:
                case 0xd2:
                case 0xd3:
                case 0xd4:
                case 0xd5:
                case 0xd6:
                case 0xd7:
                    // RST markers do not have any data.
                    break;

                case 0xd9:
                    // Found end marker.
                    break;

                case 0xda:
                    if (mode == JpegReadMode.ReadEverything)
                    {
                        if (!ProcessScan(
                                data,
                                len,
                                dcHuffLut,
                                acHuffLut,
                                scanProgression,
                                isProgressive,
                                ref pos,
                                jpg))
                        {
                            return false;
                        }
                    }

                    foundSos = true;
                    break;

                case 0xdb:
                    ProcessMarkerDQT(data, len, ref pos, jpg);
                    break;

                case 0xdd:
                    ProcessDRI(data, len, ref pos, ref foundDri, jpg);
                    break;

                case >= 0xe0 and <= 0xef:
                    if (mode != JpegReadMode.ReadTables)
                    {
                        ProcessAPP(data, len, ref pos, jpg);
                    }

                    break;

                case 0xfe:
                    if (mode != JpegReadMode.ReadTables)
                    {
                        ProcessMarkerCOM(data, len, ref pos, jpg);
                    }

                    break;

                default:
                    return false;
            }

            jpg.MarkerOrder.Add((byte)marker);

            if (mode == JpegReadMode.ReadHeader && foundSof)
            {
                break;
            }
        }
        while (marker != 0xd9);

        if (!foundSof || !foundSos)
        {
            return false;
        }

        if (mode == JpegReadMode.ReadEverything)
        {
            if (pos < len)
            {
                jpg.TailData = [.. data[pos..]];
            }

            if (!FixupIndexes(jpg))
            {
                return false;
            }

            if (jpg.HuffmanCodes.Count == 0)
            {
                return false;
            }

            if (jpg.HuffmanCodes.Count >= JpegDataConstants.MaxDhtMarkers)
            {
                return false;
            }
        }

        return true;
    }

    private ref struct BitReaderState
    {
        private readonly ReadOnlySpan<byte> data;
        private readonly int length;

        private int pos;
        private ulong value;
        private int bitsLeft;
        private int nextMarkerPos;

        public BitReaderState(ReadOnlySpan<byte> data, int pos)
        {
            this.data = data;
            this.length = data.Length;

            this.pos = pos;
            this.value = 0;
            this.bitsLeft = 0;
            this.nextMarkerPos = this.length - 2;

            this.FillBitWindow();
        }

        public int BitsLeft
        {
            readonly get => this.bitsLeft;
            set => this.bitsLeft = value;
        }

        public ulong Value
        {
            readonly get => this.value;
            set => this.value = value;
        }

        public void Reset(int pos)
        {
            this.pos = pos;
            this.value = 0;
            this.bitsLeft = 0;
            this.nextMarkerPos = this.length - 2;

            this.FillBitWindow();
        }

        private byte GetNextByte()
        {
            if (this.pos >= this.nextMarkerPos)
            {
                ++this.pos;
                return 0;
            }

            byte c = this.data[this.pos++];

            if (c == 0xff)
            {
                byte escape = this.data[this.pos];

                if (escape == 0)
                {
                    ++this.pos;
                }
                else
                {
                    // 0xff followed by a non-zero byte means that we found
                    // the start of the next marker segment.
                    this.nextMarkerPos = this.pos - 1;
                }
            }

            return c;
        }

        public void FillBitWindow()
        {
            if (this.bitsLeft <= 16)
            {
                while (this.bitsLeft <= 56)
                {
                    this.value <<= 8;
                    this.value |= this.GetNextByte();
                    this.bitsLeft += 8;
                }
            }
        }

        public int ReadBits(int bitCount)
        {
            this.FillBitWindow();

            ulong result =
                (this.value >> (this.bitsLeft - bitCount)) &
                ((1UL << bitCount) - 1);

            this.bitsLeft -= bitCount;
            return (int)result;
        }

        public bool FinishStream(JpegData jpg, ref int pos)
        {
            int paddingBitCount = this.bitsLeft & 7;

            if (paddingBitCount > 0)
            {
                ulong paddingMask = (1UL << paddingBitCount) - 1;
                ulong paddingBits =
                    (this.value >> (this.bitsLeft - paddingBitCount)) &
                    paddingMask;

                if (paddingBits != paddingMask)
                {
                    jpg.HasZeroPaddingBit = true;
                }

                for (int i = paddingBitCount - 1; i >= 0; --i)
                {
                    jpg.PaddingBits.Add((byte)((paddingBits >> i) & 1));
                }
            }

            int unusedBytesLeft = this.bitsLeft >> 3;

            while (unusedBytesLeft-- > 0)
            {
                --this.pos;

                if (this.pos < this.nextMarkerPos &&
                    this.data[this.pos] == 0 &&
                    this.data[this.pos - 1] == 0xff)
                {
                    --this.pos;
                }
            }

            if (this.pos > this.nextMarkerPos)
            {
                return false;
            }

            pos = this.pos;
            return true;
        }
    }
}

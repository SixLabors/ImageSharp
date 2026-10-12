// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Jxl.Fields;
using SixLabors.ImageSharp.Formats.Jxl.Processing;

namespace SixLabors.ImageSharp.Formats.Jxl.IO.Jpeg.Data;

internal sealed class JpegData : IJxlFields
{
    private int restartInterval;
    private bool hasZeroPaddingBit;

    public int Width { get; set; }

    public int Height { get; set; }

    public int RestartInterval
    {
        get => this.restartInterval;
        set => this.restartInterval = value;
    }

    public bool HasZeroPaddingBit
    {
        get => this.hasZeroPaddingBit;
        set => this.hasZeroPaddingBit = value;
    }

    /// <summary>
    /// Gets or sets raw bytes of APP markers. Types
    /// of APP markers are specified by <see cref="AppMarkerTypes"/>.
    /// </summary>
    public List<List<byte>> AppData { get; set; } = [];

    /// <summary>
    /// Gets or sets kinds of APP markers for each marker data
    /// within <see cref="AppData"/>.
    /// </summary>
    public List<JpegAppMarkerType> AppMarkerTypes { get; set; } = [];

    /// <summary>
    /// Gets or sets raw bytes of COM markers.
    /// </summary>
    public List<List<byte>> ComData { get; set; } = [];

    /// <summary>
    /// Gets or sets quantization tables.
    /// </summary>
    public List<JpegQuantizationTable> Quant { get; set; } = [];

    /// <summary>
    /// Gets or sets definitions of Huffman codes.
    /// </summary>
    public List<JpegHuffmanCode> HuffmanCodes { get; set; } = [];

    /// <summary>
    /// Gets or sets JPEG components.
    /// </summary>
    public List<JpegComponent> Components { get; set; } = [];

    /// <summary>
    /// Gets or sets scan infos.
    /// </summary>
    public List<JpegScanInfo> ScanInfos { get; set; } = [];

    public List<byte> MarkerOrder { get; set; } = [];

    public List<List<byte>> InterMarkerData { get; set; } = [];

    public List<byte> TailData { get; set; } = [];

    public List<byte> PaddingBits { get; set; } = [];

    public void CalculateMcuSize(JpegScanInfo scan, out int mcusPerRow, out int mcuRows)
    {
        bool isInterleaved = scan.NumComponents > 1;
        JpegComponent baseComponent = this.Components[scan.Components[0].ComponentIndex];

        int horizontalGroup = isInterleaved ? 1 : baseComponent.HorizontalSampleFactor;
        int verticalGroup = isInterleaved ? 1 : baseComponent.VerticalSampleFactor;

        int maxHSampFactor = 1;
        int maxVSampFactor = 1;

        foreach (JpegComponent component in this.Components)
        {
            maxHSampFactor = Math.Max(component.HorizontalSampleFactor, maxHSampFactor);
            maxVSampFactor = Math.Max(component.VerticalSampleFactor, maxVSampFactor);
        }

        mcusPerRow = JxlMath.DivCeil(this.Width * horizontalGroup, 8 * maxHSampFactor);
        mcuRows = JxlMath.DivCeil(this.Height * verticalGroup, 8 * maxVSampFactor);
    }

    public static void SetJpegDataFromIcc(Span<byte> icc, JpegData jpegData)
    {
        int iccPos = 0;

        for (int i = 0; i < jpegData.AppData.Count; i++)
        {
            if (jpegData.AppMarkerTypes[i] != JpegAppMarkerType.Icc)
            {
                continue;
            }

            if (jpegData.AppData[i].Count < 17)
            {
                throw new InvalidOperationException("ICC APP marker too small: " + jpegData.AppData[i].Count);
            }

            int len = jpegData.AppData[i].Count - 17;
            if (iccPos > icc.Length - len)
            {
                throw new InvalidOperationException("ICC length is less than APP markers: requested " + len + " more bytes, " + (icc.Length - iccPos) + " available");
            }

            icc.Slice(iccPos, len).CopyTo(CollectionsMarshal.AsSpan(jpegData.AppData[i])[17..]);
            iccPos += len;
        }

        if (iccPos != icc.Length && iccPos != 0)
        {
            throw new InvalidOperationException("ICC length > APP markers");
        }
    }

    private static bool VisitMarker(ref byte marker, JxlVisitor visitor, ref JpegInfo info)
    {
        uint marker32 = marker - 0xC0u;

        if (!visitor.Bits(6, 0x00, ref marker32))
        {
            return false;
        }

        marker = (byte)(marker32 + 0xC0u);

        if ((marker & 0xf0) == 0xe0)
        {
            info.NumberOfAppMarkers++;
        }

        switch (marker)
        {
            case 0xFE:
                info.NumberOfComMarkers++;
                break;

            case 0xDA:
                info.NumberOfScans++;
                break;

            case 0xFF:
                info.NumberOfIntermarkers++;
                break;

            case 0xDD:
                info.HasDri = true;
                break;
        }

        return true;
    }

    /// <inheritdoc />
    public bool Visit(JxlVisitor visitor)
    {
        // The following is just JPEG parsing/writing code.
        // Nothing different.
        bool isGray = this.Components.Count == 1;

        if (!visitor.Boolean(false, ref isGray))
        {
            return false;
        }

        if (visitor.IsReading)
        {
            this.Components = new List<JpegComponent>(isGray ? 1 : 3);
        }

        JpegInfo info = default;

        if (visitor.IsReading)
        {
            byte marker = 0xC0;

            do
            {
                if (!VisitMarker(ref marker, visitor, ref info))
                {
                    return false;
                }

                this.MarkerOrder.Add(marker);

                if (this.MarkerOrder.Count > 16384)
                {
                    throw new InvalidOperationException("Too many markers: " + this.MarkerOrder.Count);
                }
            }
            while (marker != 0xD9);
        }
        else
        {
            if (this.MarkerOrder.Count > 16384)
            {
                throw new InvalidOperationException("Too many markers: " + this.MarkerOrder.Count);
            }

            Span<byte> markerData = CollectionsMarshal.AsSpan(this.MarkerOrder);

            for (int i = 0; i < markerData.Length; i++)
            {
                ref byte marker = ref markerData[i];

                if (!VisitMarker(ref marker, visitor, ref info))
                {
                    return false;
                }
            }

            if (this.MarkerOrder.Count > 0)
            {
                if (this.MarkerOrder[^1] != 0xD9)
                {
                    throw new InvalidOperationException("Last marker should always be EOI (0xD9) marker");
                }
            }
        }

        if (info.NumberOfScans == 0)
        {
            throw new InvalidOperationException("No JPEG scans");
        }

        if (visitor.IsReading)
        {
            this.AppData = new List<List<byte>>(info.NumberOfAppMarkers);
            this.AppMarkerTypes = new List<JpegAppMarkerType>(info.NumberOfAppMarkers);
            this.ComData = new List<List<byte>>(info.NumberOfAppMarkers);
            this.ScanInfos = new List<JpegScanInfo>(info.NumberOfAppMarkers);
        }

        if (this.AppData.Count != info.NumberOfAppMarkers ||
            this.AppMarkerTypes.Count != info.NumberOfAppMarkers ||
            this.ComData.Count != info.NumberOfComMarkers ||
            this.ScanInfos.Count != info.NumberOfScans)
        {
            throw new InvalidOperationException("Mismatch between number of APP markers and the actual APP marker count");
        }

        for (int i = 0; i < this.AppData.Count; i++)
        {
            uint uMarkerType = (uint)this.AppMarkerTypes[i];

            if (!visitor.U32(
                JxlFieldExpressions.Value(0),
                JxlFieldExpressions.Value(1),
                JxlFieldExpressions.BitsOffset(1, 2),
                JxlFieldExpressions.BitsOffset(2, 4),
                0,
                ref uMarkerType))
            {
                return false;
            }

            this.AppMarkerTypes[i] = (JpegAppMarkerType)uMarkerType;

            if (this.AppMarkerTypes[i] is not JpegAppMarkerType.Unknown and
                not JpegAppMarkerType.Icc and
                not JpegAppMarkerType.Exif and
                not JpegAppMarkerType.Xmp)
            {
                throw new InvalidOperationException("Unknown APP marker type: " + (uint)this.AppMarkerTypes[i]);
            }

            uint len = (uint)this.AppMarkerTypes.Count - 1;
            if (!visitor.Bits(16, 0, ref len))
            {
                return false;
            }

            if (visitor.IsReading)
            {
                this.AppData[i] = new List<byte>((int)len + 1);
                if (len + 1 < 3)
                {
                    throw new InvalidOperationException("Marker size is invalid");
                }
            }

            if (len + 1 < 3)
            {
                throw new InvalidOperationException("Marker size is invalid");
            }
        }

        for (int i = 0; i < this.ComData.Count; i++)
        {
            List<byte> com = this.ComData[i];

            uint len = (uint)com.Count - 1;

            if (!visitor.Bits(16, 0, ref len))
            {
                return false;
            }

            if (len + 1 < 3)
            {
                throw new InvalidOperationException("Marker size is invalid");
            }

            if (visitor.IsReading)
            {
                this.ComData[i] = new List<byte>((int)len + 1);

                if (len + 1 < 3)
                {
                    throw new InvalidOperationException("Marker size is invalid");
                }
            }

            if (len + 1 < 3)
            {
                throw new InvalidOperationException("Marker size is invalid");
            }
        }

        uint numQuantTables = (uint)this.Quant.Count;

        if (!visitor.U32(
            JxlFieldExpressions.Value(1),
            JxlFieldExpressions.Value(2),
            JxlFieldExpressions.Value(3),
            JxlFieldExpressions.Value(4),
            2,
            ref numQuantTables))
        {
            return false;
        }

        if (numQuantTables == 4)
        {
            throw new InvalidOperationException("Invalid number of quant tables");
        }

        if (visitor.IsReading)
        {
            this.Quant = new List<JpegQuantizationTable>((int)numQuantTables);
        }

        Span<JpegQuantizationTable> quantSpan = CollectionsMarshal.AsSpan(this.Quant);

        for (int i = 0; i < numQuantTables; i++)
        {
            ref JpegQuantizationTable quant = ref quantSpan[i];

            if (quant.Precision > 1)
            {
                throw new InvalidOperationException("Quant tables with more than 16 bits are not supported");
            }

            if (!visitor.Bits(1, 0, ref Unsafe.As<int, uint>(ref quant.Precision)))
            {
                return false;
            }

            if (!visitor.Bits(2, (uint)i, ref Unsafe.As<int, uint>(ref quant.Index)))
            {
                return false;
            }

            if (!visitor.Boolean(true, ref quant.IsLast))
            {
                return false;
            }
        }

        Span<JpegComponent> components = CollectionsMarshal.AsSpan(this.Components);

        JpegComponentType componentType =
          components.Length == 1 && components[0].Id == 1 ? JpegComponentType.Gray
          : components.Length == 3 && components[0].Id == 1 &&
                  components[1].Id == 2 && components[2].Id == 3
              ? JpegComponentType.YCbCr
          : components.Length == 3 && components[0].Id == 'R' &&
                  components[1].Id == 'G' && components[2].Id == 'B'
              ? JpegComponentType.Rgb
              : JpegComponentType.Custom;

        if (visitor.Bits(2, (uint)JpegComponentType.YCbCr, ref Unsafe.As<JpegComponentType, uint>(ref componentType)))
        {
            return false;
        }

        uint numberOfComponents;

        if (componentType == JpegComponentType.Gray)
        {
            numberOfComponents = 1;
        }
        else if (componentType != JpegComponentType.Custom)
        {
            numberOfComponents = 3;
        }
        else
        {
            numberOfComponents = (uint)components.Length;

            if (!visitor.U32(
                JxlFieldExpressions.Value(1),
                JxlFieldExpressions.Value(2),
                JxlFieldExpressions.Value(3),
                JxlFieldExpressions.Value(4),
                3,
                ref numberOfComponents))
            {
                return false;
            }

            if (numberOfComponents is not 1 and not 3)
            {
                throw new InvalidOperationException("Invalid number of components: " + numberOfComponents);
            }
        }

        if (visitor.IsReading)
        {
            this.Components = new List<JpegComponent>((int)numberOfComponents);

            // It's unsafe to assign a new List (or add/remove items to it)
            // while keeping a Span to it.
            components = CollectionsMarshal.AsSpan(this.Components);
        }

        if (componentType == JpegComponentType.Custom)
        {
            foreach (JpegComponent component in components)
            {
                if (!visitor.Bits(8, 0, ref Unsafe.As<int, uint>(ref component.Id)))
                {
                    return false;
                }
            }
        }
        else if (componentType == JpegComponentType.Gray)
        {
            components[0].Id = 1;
        }
        else if (componentType == JpegComponentType.Rgb)
        {
            components[0].Id = 'R';
            components[1].Id = 'G';
            components[2].Id = 'B';
        }
        else
        {
            components[0].Id = 1;
            components[1].Id = 2;
            components[2].Id = 3;
        }

        uint usedTables = 0;

        for (int i = 0; i < components.Length; i++)
        {
            if (!visitor.Bits(2, 0, ref Unsafe.As<int, uint>(ref components[i].QuantIndex)))
            {
                return false;
            }

            if (components[i].QuantIndex >= this.Quant.Count)
            {
                throw new InvalidOperationException("Invalid quant table for component " + components[i].QuantIndex);
            }

            usedTables |= 1u << components[i].QuantIndex;
        }

        for (int i = 0; i < this.Quant.Count; i++)
        {
            if ((usedTables & (1 << i)) != 0)
            {
                continue;
            }

            if (i == 0)
            {
                throw new InvalidOperationException("First quant table unused");
            }

            for (int j = 0; j < 64; j++)
            {
                if (this.Quant[i].Values[j] != this.Quant[i - 1].Values[j])
                {
                    throw new InvalidOperationException("Non-trivial unused quant table");
                }
            }
        }

        uint numHuff = (uint)this.HuffmanCodes.Count;

        if (!visitor.U32(
            JxlFieldExpressions.Value(4),
            JxlFieldExpressions.BitsOffset(3, 2),
            JxlFieldExpressions.BitsOffset(4, 10),
            JxlFieldExpressions.BitsOffset(6, 26),
            4,
            ref numHuff))
        {
            return false;
        }

        if (visitor.IsReading)
        {
            this.HuffmanCodes = new List<JpegHuffmanCode>((int)numHuff);
        }

        Span<JpegHuffmanCode> huffs = CollectionsMarshal.AsSpan(this.HuffmanCodes);

        for (int i = 0; i < huffs.Length; i++)
        {
            ref JpegHuffmanCode hc = ref huffs[i];

            bool isAc = (hc.SlotId >> 4) != 0;
            uint id = (uint)hc.SlotId & 0x0Fu;

            if (!visitor.Boolean(false, ref isAc))
            {
                return false;
            }

            if (!visitor.Bits(2, 0, ref id))
            {
                return false;
            }

            hc.SlotId = ((isAc ? 1 : 0) << 4) | (int)id;

            if (!visitor.Boolean(true, ref hc.IsLast))
            {
                return false;
            }

            int numSymbols = 0;

            for (int j = 0; j <= 16; j++)
            {
                if (!visitor.U32(
                    JxlFieldExpressions.Value(0),
                    JxlFieldExpressions.Value(1),
                    JxlFieldExpressions.BitsOffset(3, 2),
                    JxlFieldExpressions.Bits(8),
                    0,
                    ref Unsafe.As<int, uint>(ref hc.Counts[j])))
                {
                    return false;
                }

                numSymbols += hc.Counts[j];
            }

            if (numSymbols == 0)
            {
                // At least 2 symbols are required, since one of them is EOI.
                // This case is used to represent an empty DHT marker.
                continue;
            }

            if (numSymbols > 17)
            {
                throw new InvalidOperationException("Huffman code too large (" + numSymbols + ")");
            }

            InlineArray5<long> valueSlots = default;

            for (int j = 0; j < numSymbols; j++)
            {
                // Goes up to 256, included. Might have the same symbol appear twice.
                if (!visitor.U32(
                    JxlFieldExpressions.Bits(2),
                    JxlFieldExpressions.BitsOffset(2, 4),
                    JxlFieldExpressions.BitsOffset(4, 8),
                    JxlFieldExpressions.BitsOffset(8, 1),
                    0,
                    ref Unsafe.As<int, uint>(ref hc.Values[j])))
                {
                    return false;
                }

                valueSlots[hc.Values[j] >> 6] |= 1L << (hc.Values[j] & 0x3F);
            }

            if (hc.Values[numSymbols - 1] != JpegDataConstants.JpegHuffmanAlphabetSize)
            {
                throw new InvalidOperationException("Missing EOI symbol");
            }

            if (valueSlots[4] != 1)
            {
                return false;
            }

            int numValues = 1;

            for (int j = 0; j < 4; j++)
            {
                numValues += BitOperations.PopCount((uint)valueSlots[i]);
            }

            if (numValues != numSymbols)
            {
                throw new InvalidOperationException("Duplicate Huffman symbols");
            }

            if (!isAc)
            {
                bool onlyDC = ((valueSlots[0] >> JpegDataConstants.JpegDcAlphabetSize) | valueSlots[1] | valueSlots[2] | valueSlots[3]) == 0;

                if (!onlyDC)
                {
                    throw new InvalidOperationException("Huffman symbols out of DC range");
                }
            }
        }

        foreach (JpegScanInfo scan in this.ScanInfos)
        {
            if (!visitor.U32(
                JxlFieldExpressions.Value(1),
                JxlFieldExpressions.Value(2),
                JxlFieldExpressions.Value(3),
                JxlFieldExpressions.Value(4),
                1,
                ref Unsafe.As<int, uint>(ref scan.NumComponents)))
            {
                return false;
            }

            if (scan.NumComponents >= 4)
            {
                throw new InvalidOperationException("Invalid number of components in SOS marker");
            }

            if (!visitor.Bits(6, 0, ref Unsafe.As<int, uint>(ref scan.Ss)))
            {
                return false;
            }

            if (!visitor.Bits(6, 63, ref Unsafe.As<int, uint>(ref scan.Se)))
            {
                return false;
            }

            if (!visitor.Bits(4, 0, ref Unsafe.As<int, uint>(ref scan.Al)))
            {
                return false;
            }

            if (!visitor.Bits(4, 0, ref Unsafe.As<int, uint>(ref scan.Ah)))
            {
                return false;
            }

            for (int i = 0; i < scan.NumComponents; i++)
            {
                if (!visitor.Bits(2, 0, ref Unsafe.As<int, uint>(ref scan.Components[i].ComponentIndex)))
                {
                    return false;
                }

                if (scan.Components[i].ComponentIndex >= components.Length)
                {
                    throw new InvalidOperationException("Invalid component idx in SOS marker");
                }

                if (!visitor.Bits(2, 0, ref Unsafe.As<int, uint>(ref scan.Components[i].AcTableIndex)))
                {
                    return false;
                }

                if (!visitor.Bits(2, 0, ref Unsafe.As<int, uint>(ref scan.Components[i].DcTableIndex)))
                {
                    return false;
                }
            }

            if (!visitor.U32(
                JxlFieldExpressions.Value(0),
                JxlFieldExpressions.Value(1),
                JxlFieldExpressions.Value(2),
                JxlFieldExpressions.BitsOffset(3, 3),
                JxlShared.MaximumNumberOfPasses - 1,
                ref Unsafe.As<int, uint>(ref scan.LastNeededPass)))
            {
                return false;
            }
        }

        if (info.HasDri)
        {
            if (!visitor.Bits(16, 0, ref Unsafe.As<int, uint>(ref this.restartInterval)))
            {
                return false;
            }
        }

        foreach (JpegScanInfo scan in this.ScanInfos)
        {
            int numResetPoints = scan.ResetPoints.Count;

            if (!visitor.U32(
                JxlFieldExpressions.Value(0),
                JxlFieldExpressions.BitsOffset(2, 1),
                JxlFieldExpressions.BitsOffset(4, 4),
                JxlFieldExpressions.BitsOffset(16, 20),
                0,
                ref Unsafe.As<int, uint>(ref numResetPoints)))
            {
                return false;
            }

            if (visitor.IsReading)
            {
                scan.ResetPoints = new List<int>(numResetPoints);
            }

            int lastBlockIdx = -1;
            foreach (int blk in scan.ResetPoints)
            {
                int blockIdx = blk - (lastBlockIdx + 1);

                if (!visitor.U32(
                    JxlFieldExpressions.Value(0),
                    JxlFieldExpressions.BitsOffset(3, 1),
                    JxlFieldExpressions.BitsOffset(5, 9),
                    JxlFieldExpressions.BitsOffset(28, 41),
                    0,
                    ref Unsafe.As<int, uint>(ref blockIdx)))
                {
                    return false;
                }

                blockIdx += lastBlockIdx + 1;
                if (blockIdx >= (3u << 26))
                {
                    // At most 8K x 8K x num_channels blocks are possible in a JPEG.
                    // So valid block indices are below 3 * 2^26.
                    throw new InvalidOperationException("Invalid block ID: " + blockIdx);
                }

                lastBlockIdx = blockIdx;
            }

            int numExtraZeroRuns = scan.ExtraZeroRuns.Count;

            if (!visitor.U32(
                JxlFieldExpressions.Value(0),
                JxlFieldExpressions.BitsOffset(2, 1),
                JxlFieldExpressions.BitsOffset(4, 4),
                JxlFieldExpressions.BitsOffset(16, 20),
                0,
                ref Unsafe.As<int, uint>(ref numExtraZeroRuns)))
            {
                return false;
            }

            if (visitor.IsReading)
            {
                scan.ExtraZeroRuns = new List<JpegExtraZeroRunInfo>(numExtraZeroRuns);
            }

            lastBlockIdx = -1;

            Span<JpegExtraZeroRunInfo> extraZeroes = CollectionsMarshal.AsSpan(scan.ExtraZeroRuns);

            for (int i = 0; i < extraZeroes.Length; i++)
            {
                ref JpegExtraZeroRunInfo extraZeroRun = ref extraZeroes[i];

                ref int block_idx = ref extraZeroRun.BlockIndex;
                ref int extra_zero_runs = ref extraZeroRun.NumExtraZeroRuns;

                if (!visitor.U32(
                    JxlFieldExpressions.Value(1),
                    JxlFieldExpressions.BitsOffset(2, 2),
                    JxlFieldExpressions.BitsOffset(4, 5),
                    JxlFieldExpressions.BitsOffset(8, 20),
                    1,
                    ref Unsafe.As<int, uint>(ref extra_zero_runs)))
                {
                    return false;
                }

                block_idx -= lastBlockIdx + 1;

                if (!visitor.U32(
                    JxlFieldExpressions.Value(0),
                    JxlFieldExpressions.BitsOffset(3, 1),
                    JxlFieldExpressions.BitsOffset(5, 9),
                    JxlFieldExpressions.BitsOffset(28, 41),
                    0,
                    ref Unsafe.As<int, uint>(ref block_idx)))
                {
                    return false;
                }

                block_idx += lastBlockIdx + 1;

                if (extra_zero_runs > 4)
                {
                    throw new InvalidOperationException("Invalid number of extra zero runs: " + extra_zero_runs);
                }

                if (block_idx > (3u << 26))
                {
                    throw new InvalidOperationException("Invalid block ID: " + block_idx);
                }

                lastBlockIdx = block_idx;
            }
        }

        List<int> interMarkerDataSizes = new(info.NumberOfIntermarkers);

        for (int i = 0; i < info.NumberOfIntermarkers; ++i)
        {
            int len = visitor.IsReading ? 0 : this.InterMarkerData[i].Count;

            if (!visitor.Bits(16, 0, ref Unsafe.As<int, uint>(ref len)))
            {
                return false;
            }

            if (visitor.IsReading)
            {
                interMarkerDataSizes.Add(len);
            }
        }

        int tail_data_len = this.TailData.Count;

        if (visitor.IsReading && tail_data_len > 4260096)
        {
            throw new InvalidOperationException("Tail data too large (max size = 4260096, size = " + tail_data_len + ")");
        }

        if (!visitor.U32(
            JxlFieldExpressions.Value(0),
            JxlFieldExpressions.BitsOffset(8, 1),
            JxlFieldExpressions.BitsOffset(16, 257),
            JxlFieldExpressions.BitsOffset(22, 65793),
            0,
            ref Unsafe.As<int, uint>(ref tail_data_len)))
        {
            return false;
        }

        if (!visitor.Boolean(false, ref this.hasZeroPaddingBit))
        {
            return false;
        }

        if (this.hasZeroPaddingBit)
        {
            uint nbit = (uint)this.PaddingBits.Count;

            if (!visitor.Bits(24, 0, ref nbit))
            {
                return false;
            }

            if (visitor.IsReading)
            {
                this.PaddingBits = new List<byte>((int)Math.Min(1024u, nbit));

                for (int i = 0; i < nbit; i++)
                {
                    bool bbit = false;

                    if (!visitor.Boolean(false, ref bbit))
                    {
                        return false;
                    }

                    this.PaddingBits.Add(bbit ? (byte)1 : (byte)0);
                }
            }
            else
            {
                Span<byte> bits = CollectionsMarshal.AsSpan(this.PaddingBits);

                for (int i = 0; i < bits.Length; i++)
                {
                    ref byte bit = ref bits[i];
                    bool bbit = bit != 0;

                    if (!visitor.Boolean(false, ref bbit))
                    {
                        return false;
                    }

                    bit = bbit ? (byte)1 : (byte)0;
                }
            }
        }

        int dhtIndex = 0; // index of the Define Huffman Table
        int scanIndex = 0;
        bool isProgressive = false;

        InlineArray4<bool> acOk = default;
        InlineArray4<bool> dcOk = default;

        Span<byte> markerOrderSpan = CollectionsMarshal.AsSpan(this.MarkerOrder);

        for (int i = 0; i < markerOrderSpan.Length; i++)
        {
            byte marker = markerOrderSpan[i];

            if (marker == 0xC2)
            {
                isProgressive = true;
            }
            else if (marker == 0xC4)
            {
                Span<JpegHuffmanCode> huffmanCode = CollectionsMarshal.AsSpan(this.HuffmanCodes);

                for (; dhtIndex < huffmanCode.Length;)
                {
                    ref JpegHuffmanCode huff = ref huffmanCode[dhtIndex++];
                    int index = huff.SlotId;
                    if ((index & 0x10) != 0)
                    {
                        index -= 0x10;
                        acOk[index] = true;
                    }
                    else
                    {
                        dcOk[index] = true;
                    }

                    if (huff.IsLast)
                    {
                        break;
                    }
                }
            }
            else if (marker == 0xDA)
            {
                Span<JpegScanInfo> scanInfo = CollectionsMarshal.AsSpan(this.ScanInfos);
                JpegScanInfo si = scanInfo[scanIndex++];

                for (int j = 0; j < si.NumComponents; ++j)
                {
                    ref JpegComponentScanInfo csi = ref si.Components[j];

                    int dcTableIndex = csi.DcTableIndex;
                    int acTableIndex = csi.AcTableIndex;

                    bool wantDc = !isProgressive || (si.Ss == 0);

                    if (wantDc && !dcOk[dcTableIndex])
                    {
                        throw new InvalidOperationException("DC Huffman table used before defined");
                    }

                    bool wantAc = !isProgressive || (si.Ss != 0) || (si.Se != 0);

                    if (wantAc && !acOk[acTableIndex])
                    {
                        throw new InvalidOperationException("AC Huffman table used before defined");
                    }
                }
            }
        }

        // Apply postponed actions
        if (visitor.IsReading)
        {
            this.TailData = new List<byte>(tail_data_len);

            if (interMarkerDataSizes.Count != info.NumberOfIntermarkers)
            {
                return false;
            }

            this.InterMarkerData = new List<List<byte>>(info.NumberOfIntermarkers);

            for (int i = 0; i < info.NumberOfIntermarkers; ++i)
            {
                this.InterMarkerData.Add(new List<byte>(interMarkerDataSizes[i]));
            }
        }

        return true;
    }
}

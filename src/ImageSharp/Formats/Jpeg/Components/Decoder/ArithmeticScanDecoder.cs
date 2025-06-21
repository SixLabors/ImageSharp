// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.
#nullable disable

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder;

/// <summary>
/// Decodes a arithmetic encoded spectral scan.
/// Based on https://github.com/yigolden/JpegLibrary/blob/main/src/JpegLibrary/ScanDecoder/JpegArithmeticScanDecoder.cs
/// </summary>
internal class ArithmeticScanDecoder : IJpegScanDecoder
{
    private readonly BufferedReadStream stream;

    private int c;
    private int a;
    private int ct;

    /// <summary>
    /// <see cref="JpegFrame"/> instance containing decoding-related information.
    /// </summary>
    private JpegFrame frame;

    /// <summary>
    /// Shortcut for <see cref="frame"/>.Components.
    /// </summary>
    private IJpegComponent[] components;

    /// <summary>
    /// Number of component in the current scan.
    /// </summary>
    private int scanComponentCount;

    /// <summary>
    /// The reset interval determined by RST markers.
    /// </summary>
    private int restartInterval;

    /// <summary>
    /// How many mcu's are left to do.
    /// </summary>
    private int todo;

    private readonly SpectralConverter spectralConverter;

    private JpegBitReader scanBuffer;

    private ArithmeticDecodingTable[] dcDecodingTables;

    private ArithmeticDecodingTable[] acDecodingTables;

    // Don't make this a ReadOnlySpan<byte>, as the values need to get updated.
    private readonly byte[] fixedBin = [113, 0, 0, 0];

    private readonly CancellationToken cancellationToken;

    private static readonly int[] ArithmeticTable =
    [
        Pack(0x5a1d,   1,   1, 1),
        Pack(0x2586,  14,   2, 0),
        Pack(0x1114,  16,   3, 0),
        Pack(0x080b,  18,   4, 0),
        Pack(0x03d8,  20,   5, 0),
        Pack(0x01da,  23,   6, 0),
        Pack(0x00e5,  25,   7, 0),
        Pack(0x006f,  28,   8, 0),
        Pack(0x0036,  30,   9, 0),
        Pack(0x001a,  33,  10, 0),
        Pack(0x000d,  35,  11, 0),
        Pack(0x0006,   9,  12, 0),
        Pack(0x0003,  10,  13, 0),
        Pack(0x0001,  12,  13, 0),
        Pack(0x5a7f,  15,  15, 1),
        Pack(0x3f25,  36,  16, 0),
        Pack(0x2cf2,  38,  17, 0),
        Pack(0x207c,  39,  18, 0),
        Pack(0x17b9,  40,  19, 0),
        Pack(0x1182,  42,  20, 0),
        Pack(0x0cef,  43,  21, 0),
        Pack(0x09a1,  45,  22, 0),
        Pack(0x072f,  46,  23, 0),
        Pack(0x055c,  48,  24, 0),
        Pack(0x0406,  49,  25, 0),
        Pack(0x0303,  51,  26, 0),
        Pack(0x0240,  52,  27, 0),
        Pack(0x01b1,  54,  28, 0),
        Pack(0x0144,  56,  29, 0),
        Pack(0x00f5,  57,  30, 0),
        Pack(0x00b7,  59,  31, 0),
        Pack(0x008a,  60,  32, 0),
        Pack(0x0068,  62,  33, 0),
        Pack(0x004e,  63,  34, 0),
        Pack(0x003b,  32,  35, 0),
        Pack(0x002c,  33,   9, 0),
        Pack(0x5ae1,  37,  37, 1),
        Pack(0x484c,  64,  38, 0),
        Pack(0x3a0d,  65,  39, 0),
        Pack(0x2ef1,  67,  40, 0),
        Pack(0x261f,  68,  41, 0),
        Pack(0x1f33,  69,  42, 0),
        Pack(0x19a8,  70,  43, 0),
        Pack(0x1518,  72,  44, 0),
        Pack(0x1177,  73,  45, 0),
        Pack(0x0e74,  74,  46, 0),
        Pack(0x0bfb,  75,  47, 0),
        Pack(0x09f8,  77,  48, 0),
        Pack(0x0861,  78,  49, 0),
        Pack(0x0706,  79,  50, 0),
        Pack(0x05cd,  48,  51, 0),
        Pack(0x04de,  50,  52, 0),
        Pack(0x040f,  50,  53, 0),
        Pack(0x0363,  51,  54, 0),
        Pack(0x02d4,  52,  55, 0),
        Pack(0x025c,  53,  56, 0),
        Pack(0x01f8,  54,  57, 0),
        Pack(0x01a4,  55,  58, 0),
        Pack(0x0160,  56,  59, 0),
        Pack(0x0125,  57,  60, 0),
        Pack(0x00f6,  58,  61, 0),
        Pack(0x00cb,  59,  62, 0),
        Pack(0x00ab,  61,  63, 0),
        Pack(0x008f,  61,  32, 0),
        Pack(0x5b12,  65,  65, 1),
        Pack(0x4d04,  80,  66, 0),
        Pack(0x412c,  81,  67, 0),
        Pack(0x37d8,  82,  68, 0),
        Pack(0x2fe8,  83,  69, 0),
        Pack(0x293c,  84,  70, 0),
        Pack(0x2379,  86,  71, 0),
        Pack(0x1edf,  87,  72, 0),
        Pack(0x1aa9,  87,  73, 0),
        Pack(0x174e,  72,  74, 0),
        Pack(0x1424,  72,  75, 0),
        Pack(0x119c,  74,  76, 0),
        Pack(0x0f6b,  74,  77, 0),
        Pack(0x0d51,  75,  78, 0),
        Pack(0x0bb6,  77,  79, 0),
        Pack(0x0a40,  77,  48, 0),
        Pack(0x5832,  80,  81, 1),
        Pack(0x4d1c,  88,  82, 0),
        Pack(0x438e,  89,  83, 0),
        Pack(0x3bdd,  90,  84, 0),
        Pack(0x34ee,  91,  85, 0),
        Pack(0x2eae,  92,  86, 0),
        Pack(0x299a,  93,  87, 0),
        Pack(0x2516,  86,  71, 0),
        Pack(0x5570,  88,  89, 1),
        Pack(0x4ca9,  95,  90, 0),
        Pack(0x44d9,  96,  91, 0),
        Pack(0x3e22,  97,  92, 0),
        Pack(0x3824,  99,  93, 0),
        Pack(0x32b4,  99,  94, 0),
        Pack(0x2e17,  93,  86, 0),
        Pack(0x56a8,  95,  96, 1),
        Pack(0x4f46, 101,  97, 0),
        Pack(0x47e5, 102,  98, 0),
        Pack(0x41cf, 103,  99, 0),
        Pack(0x3c3d, 104, 100, 0),
        Pack(0x375e,  99,  93, 0),
        Pack(0x5231, 105, 102, 0),
        Pack(0x4c0f, 106, 103, 0),
        Pack(0x4639, 107, 104, 0),
        Pack(0x415e, 103,  99, 0),
        Pack(0x5627, 105, 106, 1),
        Pack(0x50e7, 108, 107, 0),
        Pack(0x4b85, 109, 103, 0),
        Pack(0x5597, 110, 109, 0),
        Pack(0x504f, 111, 107, 0),
        Pack(0x5a10, 110, 111, 1),
        Pack(0x5522, 112, 109, 0),
        Pack(0x59eb, 112, 111, 1),

        // This last entry is used for fixed probability estimate of 0.5
        // as suggested in Section 10.3 Table 5 of ITU-T Rec. T.851.
        Pack(0x5a1d, 113, 113, 0)
    ];

    private readonly List<ArithmeticStatistics> statistics = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="ArithmeticScanDecoder"/> class.
    /// </summary>
    /// <param name="stream">The input stream.</param>
    /// <param name="converter">Spectral to pixel converter.</param>
    /// <param name="cancellationToken">The token to monitor cancellation.</param>
    public ArithmeticScanDecoder(BufferedReadStream stream, SpectralConverter converter, CancellationToken cancellationToken)
    {
        this.stream = stream;
        this.spectralConverter = converter;
        this.cancellationToken = cancellationToken;

        this.c = 0;
        this.a = 0;
        this.ct = -16; // Force reading 2 initial bytes to fill C.
    }

    /// <inheritdoc/>
    public int ResetInterval
    {
        set
        {
            this.restartInterval = value;
            this.todo = value;
        }
    }

    /// <inheritdoc/>
    public int SpectralStart { get; set; }

    /// <inheritdoc/>
    public int SpectralEnd { get; set; }

    /// <inheritdoc/>
    public int SuccessiveHigh { get; set; }

    /// <inheritdoc/>
    public int SuccessiveLow { get; set; }

    public void InitDecodingTables(List<ArithmeticDecodingTable> arithmeticDecodingTables)
    {
        for (int i = 0; i < this.components.Length; i++)
        {
            ArithmeticDecodingComponent component = this.components[i] as ArithmeticDecodingComponent;
            this.dcDecodingTables[i] = GetArithmeticTable(arithmeticDecodingTables, true, component.DcTableId);
            component.DcStatistics = this.CreateOrGetStatisticsBin(true, component.DcTableId);
            this.acDecodingTables[i] = GetArithmeticTable(arithmeticDecodingTables, false, component.AcTableId);
            component.AcStatistics = this.CreateOrGetStatisticsBin(false, component.AcTableId);
        }
    }

    private ref byte GetFixedBinReference() => ref MemoryMarshal.GetArrayDataReference(this.fixedBin);

    /// <inheritdoc/>
    public void ParseEntropyCodedData(int scanComponentCount, IccProfile iccProfile)
    {
        this.cancellationToken.ThrowIfCancellationRequested();

        this.scanComponentCount = scanComponentCount;

        this.scanBuffer = new(this.stream);

        this.frame.AllocateComponents();

        if (this.frame.Progressive)
        {
            this.ParseProgressiveData();
        }
        else
        {
            this.ParseBaselineData(iccProfile);
        }

        if (this.scanBuffer.HasBadMarker())
        {
            this.stream.Position = this.scanBuffer.MarkerPosition;
        }
    }

    /// <inheritdoc/>
    public void InjectFrameData(JpegFrame frame, IRawJpegData jpegData)
    {
        this.frame = frame;
        this.components = frame.Components;

        this.dcDecodingTables = new ArithmeticDecodingTable[this.components.Length];
        this.acDecodingTables = new ArithmeticDecodingTable[this.components.Length];

        this.spectralConverter.InjectFrameData(frame, jpegData);
    }

    private static ArithmeticDecodingTable GetArithmeticTable(List<ArithmeticDecodingTable> arithmeticDecodingTables, bool isDcTable, int identifier)
    {
        int tableClass = isDcTable ? 0 : 1;

        foreach (ArithmeticDecodingTable item in arithmeticDecodingTables)
        {
            if (item.TableClass == tableClass && item.Identifier == identifier)
            {
                return item;
            }
        }

        return null;
    }

    private ArithmeticStatistics CreateOrGetStatisticsBin(bool dc, int identifier, bool reset = false)
    {
        foreach (ArithmeticStatistics item in this.statistics)
        {
            if (item.IsDcStatistics == dc && item.Identifier == identifier)
            {
                if (reset)
                {
                    item.Reset();
                }

                return item;
            }
        }

        ArithmeticStatistics statistic = new(dc, identifier);
        this.statistics.Add(statistic);
        return statistic;
    }

    private void ParseBaselineData(IccProfile iccProfile)
    {
        for (int i = 0; i < this.components.Length; i++)
        {
            ArithmeticDecodingComponent component = (ArithmeticDecodingComponent)this.components[i];
            component.DcPredictor = 0;
            component.DcContext = 0;
            component.DcStatistics?.Reset();
            component.AcStatistics?.Reset();
        }

        this.Reset();

        if (this.scanComponentCount != 1)
        {
            this.spectralConverter.PrepareForDecoding();
            this.ParseBaselineDataInterleaved(iccProfile);
            this.spectralConverter.CommitConversion();
        }
        else if (this.frame.ComponentCount == 1)
        {
            this.spectralConverter.PrepareForDecoding();
            this.ParseBaselineDataSingleComponent(iccProfile);
            this.spectralConverter.CommitConversion();
        }
        else
        {
            this.ParseBaselineDataNonInterleaved();
        }
    }

    private void ParseProgressiveData()
    {
        this.CheckProgressiveData();

        for (int i = 0; i < this.components.Length; i++)
        {
            ArithmeticDecodingComponent component = (ArithmeticDecodingComponent)this.components[i];
            if (this.SpectralStart == 0 && this.SuccessiveHigh == 0)
            {
                component.DcPredictor = 0;
                component.DcContext = 0;
                component.DcStatistics?.Reset();
            }

            if (this.SpectralStart != 0)
            {
                component.AcStatistics?.Reset();
            }
        }

        this.Reset();

        if (this.scanComponentCount == 1)
        {
            this.ParseProgressiveDataNonInterleaved();
        }
        else
        {
            this.ParseProgressiveDataInterleaved();
        }
    }

    private void CheckProgressiveData()
    {
        // Validate successive scan parameters.
        // Logic has been adapted from libjpeg.
        // See Table B.3 – Scan header parameter size and values. itu-t81.pdf
        bool invalid = false;
        if (this.SpectralStart == 0)
        {
            if (this.SpectralEnd != 0)
            {
                invalid = true;
            }
        }
        else
        {
            // Need not check Ss/Se < 0 since they came from unsigned bytes.
            if (this.SpectralEnd < this.SpectralStart || this.SpectralEnd > 63)
            {
                invalid = true;
            }

            // AC scans may have only one component.
            if (this.scanComponentCount != 1)
            {
                invalid = true;
            }
        }

        if (this.SuccessiveHigh != 0)
        {
            // Successive approximation refinement scan: must have Al = Ah-1.
            if (this.SuccessiveHigh - 1 != this.SuccessiveLow)
            {
                invalid = true;
            }
        }

        // TODO: How does this affect 12bit jpegs.
        // According to libjpeg the range covers 8bit only?
        if (this.SuccessiveLow > 13)
        {
            invalid = true;
        }

        if (invalid)
        {
            JpegThrowHelper.ThrowBadProgressiveScan(this.SpectralStart, this.SpectralEnd, this.SuccessiveHigh, this.SuccessiveLow);
        }
    }

    private void ParseBaselineDataInterleaved(IccProfile iccProfile)
    {
        int mcu = 0;
        int mcusPerColumn = this.frame.McusPerColumn;
        int mcusPerLine = this.frame.McusPerLine;
        ref JpegBitReader reader = ref this.scanBuffer;

        for (int j = 0; j < mcusPerColumn; j++)
        {
            this.cancellationToken.ThrowIfCancellationRequested();

            // Decode from binary to spectral.
            for (int i = 0; i < mcusPerLine; i++)
            {
                // Scan an interleaved mcu... process components in order.
                int mcuCol = mcu % mcusPerLine;
                for (int k = 0; k < this.scanComponentCount; k++)
                {
                    int order = this.frame.ComponentOrder[k];
                    ArithmeticDecodingComponent component = this.components[order] as ArithmeticDecodingComponent;

                    ref ArithmeticDecodingTable dcDecodingTable = ref this.dcDecodingTables[component.DcTableId];
                    ref ArithmeticDecodingTable acDecodingTable = ref this.acDecodingTables[component.AcTableId];

                    int h = component.HorizontalSamplingFactor;
                    int v = component.VerticalSamplingFactor;

                    // Scan out an mcu's worth of this component; that's just determined
                    // by the basic H and V specified for the component.
                    int mcuColMulh = mcuCol * h;
                    for (int y = 0; y < v; y++)
                    {
                        Span<Block8x8> blockSpan = component.SpectralBlocks.DangerousGetRowSpan(y);
                        ref Block8x8 blockRef = ref MemoryMarshal.GetReference(blockSpan);

                        for (int x = 0; x < h; x++)
                        {
                            if (reader.NoData)
                            {
                                // It is very likely that some spectral data was decoded before we've encountered 'end of scan'
                                // so we need to decode what's left and return (or maybe throw?)
                                this.spectralConverter.ConvertStrideBaseline(iccProfile);
                                return;
                            }

                            int blockCol = mcuColMulh + x;

                            this.DecodeBlockBaseline(
                                component,
                                ref Unsafe.Add(ref blockRef, (uint)blockCol),
                                ref acDecodingTable,
                                ref dcDecodingTable);
                        }
                    }
                }

                // After all interleaved components, that's an interleaved MCU,
                // so now count down the restart interval.
                mcu++;
                this.HandleRestart();
            }

            // Convert from spectral to actual pixels via given converter.
            this.spectralConverter.ConvertStrideBaseline(iccProfile);
        }
    }

    private void ParseBaselineDataSingleComponent(IccProfile iccProfile)
    {
        ArithmeticDecodingComponent component = this.frame.Components[0] as ArithmeticDecodingComponent;
        int mcuLines = this.frame.McusPerColumn;
        int w = component.WidthInBlocks;
        int h = component.SamplingFactors.Height;
        ref ArithmeticDecodingTable dcDecodingTable = ref this.dcDecodingTables[component.DcTableId];
        ref ArithmeticDecodingTable acDecodingTable = ref this.acDecodingTables[component.AcTableId];

        ref JpegBitReader reader = ref this.scanBuffer;

        for (int i = 0; i < mcuLines; i++)
        {
            this.cancellationToken.ThrowIfCancellationRequested();

            // Decode from binary to spectral.
            for (int j = 0; j < h; j++)
            {
                Span<Block8x8> blockSpan = component.SpectralBlocks.DangerousGetRowSpan(j);
                ref Block8x8 blockRef = ref MemoryMarshal.GetReference(blockSpan);

                for (int k = 0; k < w; k++)
                {
                    if (reader.NoData)
                    {
                        // It is very likely that some spectral data was decoded before we've encountered 'end of scan'
                        // so we need to decode what's left and return (or maybe throw?)
                        this.spectralConverter.ConvertStrideBaseline(iccProfile);
                        return;
                    }

                    this.DecodeBlockBaseline(
                        component,
                        ref Unsafe.Add(ref blockRef, (uint)k),
                        ref acDecodingTable,
                        ref dcDecodingTable);

                    this.HandleRestart();
                }
            }

            // Convert from spectral to actual pixels via given converter.
            this.spectralConverter.ConvertStrideBaseline(iccProfile);
        }
    }

    private void ParseBaselineDataNonInterleaved()
    {
        ArithmeticDecodingComponent component = (ArithmeticDecodingComponent)this.components[this.frame.ComponentOrder[0]];
        ref JpegBitReader reader = ref this.scanBuffer;

        int w = component.WidthInBlocks;
        int h = component.HeightInBlocks;

        ref ArithmeticDecodingTable dcDecodingTable = ref this.dcDecodingTables[component.DcTableId];
        ref ArithmeticDecodingTable acDecodingTable = ref this.acDecodingTables[component.AcTableId];

        for (int j = 0; j < h; j++)
        {
            this.cancellationToken.ThrowIfCancellationRequested();
            Span<Block8x8> blockSpan = component.SpectralBlocks.DangerousGetRowSpan(j);
            ref Block8x8 blockRef = ref MemoryMarshal.GetReference(blockSpan);

            for (int i = 0; i < w; i++)
            {
                if (reader.NoData)
                {
                    return;
                }

                this.DecodeBlockBaseline(
                    component,
                    ref Unsafe.Add(ref blockRef, (uint)i),
                    ref acDecodingTable,
                    ref dcDecodingTable);

                this.HandleRestart();
            }
        }
    }

    private void ParseProgressiveDataInterleaved()
    {
        int mcu = 0;
        int mcusPerColumn = this.frame.McusPerColumn;
        int mcusPerLine = this.frame.McusPerLine;
        ref JpegBitReader reader = ref this.scanBuffer;

        for (int j = 0; j < mcusPerColumn; j++)
        {
            for (int i = 0; i < mcusPerLine; i++)
            {
                // Scan an interleaved mcu... process components in order.
                int mcuRow = Math.DivRem(mcu, mcusPerLine, out int mcuCol);
                for (int k = 0; k < this.scanComponentCount; k++)
                {
                    int order = this.frame.ComponentOrder[k];
                    ArithmeticDecodingComponent component = this.components[order] as ArithmeticDecodingComponent;
                    ref ArithmeticDecodingTable dcDecodingTable = ref this.dcDecodingTables[component.DcTableId];

                    int h = component.HorizontalSamplingFactor;
                    int v = component.VerticalSamplingFactor;

                    // Scan out an mcu's worth of this component; that's just determined
                    // by the basic H and V specified for the component.
                    int mcuColMulh = mcuCol * h;
                    for (int y = 0; y < v; y++)
                    {
                        int blockRow = (mcuRow * v) + y;
                        Span<Block8x8> blockSpan = component.SpectralBlocks.DangerousGetRowSpan(blockRow);
                        ref Block8x8 blockRef = ref MemoryMarshal.GetReference(blockSpan);

                        for (int x = 0; x < h; x++)
                        {
                            if (reader.NoData)
                            {
                                return;
                            }

                            int blockCol = mcuColMulh + x;

                            this.DecodeBlockProgressiveDc(
                                component,
                                ref Unsafe.Add(ref blockRef, (uint)blockCol),
                                ref dcDecodingTable);
                        }
                    }
                }

                // After all interleaved components, that's an interleaved MCU,
                // so now count down the restart interval.
                mcu++;
                this.HandleRestart();
            }
        }
    }

    private void ParseProgressiveDataNonInterleaved()
    {
        ArithmeticDecodingComponent component = this.components[this.frame.ComponentOrder[0]] as ArithmeticDecodingComponent;
        ref JpegBitReader reader = ref this.scanBuffer;

        int w = component.WidthInBlocks;
        int h = component.HeightInBlocks;

        if (this.SpectralStart == 0)
        {
            ref ArithmeticDecodingTable dcDecodingTable = ref this.dcDecodingTables[component.DcTableId];

            for (int j = 0; j < h; j++)
            {
                this.cancellationToken.ThrowIfCancellationRequested();

                Span<Block8x8> blockSpan = component.SpectralBlocks.DangerousGetRowSpan(j);
                ref Block8x8 blockRef = ref MemoryMarshal.GetReference(blockSpan);

                for (int i = 0; i < w; i++)
                {
                    if (reader.NoData)
                    {
                        return;
                    }

                    this.DecodeBlockProgressiveDc(
                        component,
                        ref Unsafe.Add(ref blockRef, (uint)i),
                        ref dcDecodingTable);

                    this.HandleRestart();
                }
            }
        }
        else
        {
            ref ArithmeticDecodingTable acDecodingTable = ref this.acDecodingTables[component.AcTableId];

            for (int j = 0; j < h; j++)
            {
                this.cancellationToken.ThrowIfCancellationRequested();

                Span<Block8x8> blockSpan = component.SpectralBlocks.DangerousGetRowSpan(j);
                ref Block8x8 blockRef = ref MemoryMarshal.GetReference(blockSpan);

                for (int i = 0; i < w; i++)
                {
                    if (reader.NoData)
                    {
                        return;
                    }

                    this.DecodeBlockProgressiveAc(
                            component,
                            ref Unsafe.Add(ref blockRef, (uint)i),
                            ref acDecodingTable);

                    this.HandleRestart();
                }
            }
        }
    }

    private void DecodeBlockProgressiveDc(ArithmeticDecodingComponent component, ref Block8x8 block, ref ArithmeticDecodingTable dcTable)
    {
        if (dcTable == null)
        {
            JpegThrowHelper.ThrowInvalidImageContentException("DC table is missing");
        }

        ref JpegBitReader reader = ref this.scanBuffer;
        ref short blockDataRef = ref Unsafe.As<Block8x8, short>(ref block);

        if (this.SuccessiveHigh == 0)
        {
            // First scan
            // Sections F.2.4.1 & F.1.4.4.1: Decoding of DC coefficients.

            // Table F.4: Point to statistics bin S0 for DC coefficient coding.
            ref byte st = ref Unsafe.Add(ref component.DcStatistics.GetReference(), (uint)component.DcContext);

            // Figure F.19: Decode_DC_DIFF
            if (this.DecodeBinaryDecision(ref reader, ref st) == 0)
            {
                component.DcContext = 0;
            }
            else
            {
                // Figure F.21: Decoding nonzero value v.
                // Figure F.22: Decoding the sign of v.
                int sign = this.DecodeBinaryDecision(ref reader, ref Unsafe.Add(ref st, 1));
                st = ref Unsafe.Add(ref st, (uint)(2 + sign));

                // Figure F.23: Decoding the magnitude category of v.
                int m = this.DecodeBinaryDecision(ref reader, ref st);
                if (m != 0)
                {
                    st = ref component.DcStatistics.GetReference(20);
                    while (this.DecodeBinaryDecision(ref reader, ref st) != 0)
                    {
                        if ((m <<= 1) == 0x8000)
                        {
                            JpegThrowHelper.ThrowInvalidImageContentException("Invalid arithmetic code.");
                        }

                        st = ref Unsafe.Add(ref st, 1);
                    }
                }

                // Section F.1.4.4.1.2: Establish dc_context conditioning category.
                if (m < (int)((1L << dcTable.DcL) >> 1))
                {
                    component.DcContext = 0; // Zero diff category.
                }
                else if (m > (int)((1L << dcTable.DcU) >> 1))
                {
                    component.DcContext = 12 + (sign * 4); // Large diff category.
                }
                else
                {
                    component.DcContext = 4 + (sign * 4);  // Small diff category.
                }

                int v = m;

                // Figure F.24: Decoding the magnitude bit pattern of v.
                st = ref Unsafe.Add(ref st, 14);
                while ((m >>= 1) != 0)
                {
                    if (this.DecodeBinaryDecision(ref reader, ref st) != 0)
                    {
                        v |= m;
                    }
                }

                v++;
                if (sign != 0)
                {
                    v = -v;
                }

                component.DcPredictor = (short)(component.DcPredictor + v);
            }

            blockDataRef = (short)(component.DcPredictor << this.SuccessiveLow);
        }
        else
        {
            // Refinement scan.
            ref byte st = ref this.GetFixedBinReference();

            blockDataRef |= (short)(this.DecodeBinaryDecision(ref reader, ref st) << this.SuccessiveLow);
        }
    }

    private void DecodeBlockProgressiveAc(ArithmeticDecodingComponent component, ref Block8x8 block, ref ArithmeticDecodingTable acTable)
    {
        ref JpegBitReader reader = ref this.scanBuffer;
        ref short blockDataRef = ref Unsafe.As<Block8x8, short>(ref block);

        ArithmeticStatistics acStatistics = component.AcStatistics;
        if (acStatistics == null || acTable == null)
        {
            JpegThrowHelper.ThrowInvalidImageContentException("AC table is missing");
        }

        if (this.SuccessiveHigh == 0)
        {
            // Sections F.2.4.2 & F.1.4.4.2: Decoding of AC coefficients.

            // Figure F.20: Decode_AC_coefficients.
            int start = this.SpectralStart;
            int end = this.SpectralEnd;
            int low = this.SuccessiveLow;

            for (int k = start; k <= end; k++)
            {
                ref byte st = ref acStatistics.GetReference(3 * (k - 1));
                if (this.DecodeBinaryDecision(ref reader, ref st) != 0)
                {
                    break;
                }

                while (this.DecodeBinaryDecision(ref reader, ref Unsafe.Add(ref st, 1)) == 0)
                {
                    st = ref Unsafe.Add(ref st, 3);
                    k++;
                    if (k > 63)
                    {
                        JpegThrowHelper.ThrowInvalidImageContentException("Invalid arithmetic code.");
                    }
                }

                // Figure F.21: Decoding nonzero value v.
                // Figure F.22: Decoding the sign of v.
                int sign = this.DecodeBinaryDecision(ref reader, ref this.GetFixedBinReference());
                st = ref Unsafe.Add(ref st, 2);

                // Figure F.23: Decoding the magnitude category of v.
                int m = this.DecodeBinaryDecision(ref reader, ref st);
                if (m != 0)
                {
                    if (this.DecodeBinaryDecision(ref reader, ref st) != 0)
                    {
                        m <<= 1;
                        st = ref acStatistics.GetReference(k <= acTable.AcKx ? 189 : 217);
                        while (this.DecodeBinaryDecision(ref reader, ref st) != 0)
                        {
                            if ((m <<= 1) == 0x8000)
                            {
                                JpegThrowHelper.ThrowInvalidImageContentException("Invalid arithmetic code.");
                            }

                            st = ref Unsafe.Add(ref st, 1);
                        }
                    }
                }

                int v = m;

                // Figure F.24: Decoding the magnitude bit pattern of v.
                st = ref Unsafe.Add(ref st, 14);
                while ((m >>= 1) != 0)
                {
                    if (this.DecodeBinaryDecision(ref reader, ref st) != 0)
                    {
                        v |= m;
                    }
                }

                v++;
                if (sign != 0)
                {
                    v = -v;
                }

                Unsafe.Add(ref blockDataRef, ZigZag.TransposingOrder[k]) = (short)(v << low);
            }
        }
        else
        {
            // Refinement scan.
            this.ReadBlockProgressiveAcRefined(acStatistics, ref blockDataRef);
        }
    }

    private void ReadBlockProgressiveAcRefined(ArithmeticStatistics acStatistics, ref short blockDataRef)
    {
        ref JpegBitReader reader = ref this.scanBuffer;
        int start = this.SpectralStart;
        int end = this.SpectralEnd;

        int p1 = 1 << this.SuccessiveLow;
        int m1 = -1 << this.SuccessiveLow;

        // Establish EOBx (previous stage end-of-block) index.
        int kex = end;
        for (; kex > 0; kex--)
        {
            if (Unsafe.Add(ref blockDataRef, ZigZag.TransposingOrder[kex]) != 0)
            {
                break;
            }
        }

        for (int k = start; k <= end; k++)
        {
            ref byte st = ref acStatistics.GetReference(3 * (k - 1));
            if (k > kex)
            {
                if (this.DecodeBinaryDecision(ref reader, ref st) != 0)
                {
                    break;
                }
            }

            while (true)
            {
                ref short coef = ref Unsafe.Add(ref blockDataRef, ZigZag.TransposingOrder[k]);
                if (coef != 0)
                {
                    if (this.DecodeBinaryDecision(ref reader, ref Unsafe.Add(ref st, 2)) != 0)
                    {
                        coef = (short)(coef + (coef < 0 ? m1 : p1));
                    }

                    break;
                }

                if (this.DecodeBinaryDecision(ref reader, ref Unsafe.Add(ref st, 1)) != 0)
                {
                    bool flag = this.DecodeBinaryDecision(ref reader, ref this.GetFixedBinReference()) != 0;
                    coef = (short)(coef + (flag ? m1 : p1));

                    break;
                }

                st = ref Unsafe.Add(ref st, 3);
                k++;
                if (k > end)
                {
                    JpegThrowHelper.ThrowInvalidImageContentException("Invalid arithmetic code.");
                }
            }
        }
    }

    private void DecodeBlockBaseline(
        ArithmeticDecodingComponent component,
        ref Block8x8 destinationBlock,
        ref ArithmeticDecodingTable acTable,
        ref ArithmeticDecodingTable dcTable)
    {
        if (acTable is null)
        {
            JpegThrowHelper.ThrowInvalidImageContentException("AC table is missing.");
        }

        if (dcTable is null)
        {
            JpegThrowHelper.ThrowInvalidImageContentException("DC table is missing.");
        }

        ref JpegBitReader reader = ref this.scanBuffer;
        ref short destinationRef = ref Unsafe.As<Block8x8, short>(ref destinationBlock);

        // Sections F.2.4.1 & F.1.4.4.1: Decoding of DC coefficients.

        // Table F.4: Point to statistics bin S0 for DC coefficient coding.
        ref byte st = ref Unsafe.Add(ref component.DcStatistics.GetReference(), (uint)component.DcContext);

        /* Figure F.19: Decode_DC_DIFF */
        if (this.DecodeBinaryDecision(ref reader, ref st) == 0)
        {
            component.DcContext = 0;
        }
        else
        {
            // Figure F.21: Decoding nonzero value v
            // Figure F.22: Decoding the sign of v
            int sign = this.DecodeBinaryDecision(ref reader, ref Unsafe.Add(ref st, 1));
            st = ref Unsafe.Add(ref st, (uint)(2 + sign));

            // Figure F.23: Decoding the magnitude category of v.
            int m = this.DecodeBinaryDecision(ref reader, ref st);
            if (m != 0)
            {
                // Table F.4: X1 = 20
                st = ref component.DcStatistics.GetReference(20);
                while (this.DecodeBinaryDecision(ref reader, ref st) != 0)
                {
                    if ((m <<= 1) == 0x8000)
                    {
                        JpegThrowHelper.ThrowInvalidImageContentException("Invalid arithmetic code.");
                    }

                    st = ref Unsafe.Add(ref st, 1);
                }
            }

            // Section F.1.4.4.1.2: Establish dc_context conditioning category.
            if (m < (int)((1L << dcTable.DcL) >> 1))
            {
                component.DcContext = 0; // zero diff category
            }
            else if (m > (int)((1L << dcTable.DcU) >> 1))
            {
                component.DcContext = 12 + (sign * 4); // large diff category
            }
            else
            {
                component.DcContext = 4 + (sign * 4);  // small diff category
            }

            int v = m;

            // Figure F.24: Decoding the magnitude bit pattern of v.
            st = ref Unsafe.Add(ref st, 14);
            while ((m >>= 1) != 0)
            {
                if (this.DecodeBinaryDecision(ref reader, ref st) != 0)
                {
                    v |= m;
                }
            }

            v++;
            if (sign != 0)
            {
                v = -v;
            }

            component.DcPredictor = (short)(component.DcPredictor + v);
        }

        destinationRef = (short)component.DcPredictor;

        // Sections F.2.4.2 & F.1.4.4.2: Decoding of AC coefficients.
        ArithmeticStatistics acStatistics = component.AcStatistics;

        for (int k = 1; k <= 63; k++)
        {
            st = ref acStatistics.GetReference(3 * (k - 1));
            if (this.DecodeBinaryDecision(ref reader, ref st) != 0)
            {
                // EOB flag.
                break;
            }

            while (this.DecodeBinaryDecision(ref reader, ref Unsafe.Add(ref st, 1)) == 0)
            {
                st = ref Unsafe.Add(ref st, 3);
                k++;
                if (k > 63)
                {
                    JpegThrowHelper.ThrowInvalidImageContentException("Invalid arithmetic code.");
                }
            }

            // Figure F.21: Decoding nonzero value v.
            // Figure F.22: Decoding the sign of v.
            int sign = this.DecodeBinaryDecision(ref reader, ref this.GetFixedBinReference());
            st = ref Unsafe.Add(ref st, 2);

            // Figure F.23: Decoding the magnitude category of v.
            int m = this.DecodeBinaryDecision(ref reader, ref st);
            if (m != 0)
            {
                if (this.DecodeBinaryDecision(ref reader, ref st) != 0)
                {
                    m <<= 1;
                    st = ref acStatistics.GetReference(k <= acTable.AcKx ? 189 : 217);
                    while (this.DecodeBinaryDecision(ref reader, ref st) != 0)
                    {
                        if ((m <<= 1) == 0x8000)
                        {
                            JpegThrowHelper.ThrowInvalidImageContentException("Invalid arithmetic code.");
                        }

                        st = ref Unsafe.Add(ref st, 1);
                    }
                }
            }

            int v = m;

            // Figure F.24: Decoding the magnitude bit pattern of v.
            st = ref Unsafe.Add(ref st, 14);
            while ((m >>= 1) != 0)
            {
                if (this.DecodeBinaryDecision(ref reader, ref st) != 0)
                {
                    v |= m;
                }
            }

            v++;
            if (sign != 0)
            {
                v = -v;
            }

            Unsafe.Add(ref destinationRef, ZigZag.TransposingOrder[k]) = (short)v;
        }
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

            for (int i = 0; i < this.components.Length; i++)
            {
                ArithmeticDecodingComponent component = (ArithmeticDecodingComponent)this.components[i];
                component.DcPredictor = 0;
                component.DcContext = 0;
                component.DcStatistics?.Reset();
                component.AcStatistics?.Reset();
            }

            this.Reset();

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

    [MethodImpl(InliningOptions.ShortMethod)]
    private void Reset()
    {
        for (int i = 0; i < this.components.Length; i++)
        {
            ArithmeticDecodingComponent component = this.components[i] as ArithmeticDecodingComponent;
            component.DcPredictor = 0;
        }

        this.c = 0;
        this.a = 0;
        this.ct = -16; // Force reading 2 initial bytes to fill C.

        this.scanBuffer.Reset();
    }

    private int DecodeBinaryDecision(ref JpegBitReader reader, ref byte st)
    {
        // Renormalization & data input per section D.2.6
        while (this.a < 0x8000)
        {
            if (--this.ct < 0)
            {
                // Need to fetch next data byte.
                reader.CheckBits();
                int data = reader.GetBits(8);

                // Insert data into C register.
                this.c = (this.c << 8) | data;

                // Update bit shift counter.
                if ((this.ct += 8) < 0)
                {
                    // Need more initial bytes.
                    if (++this.ct == 0)
                    {
                        // Got 2 initial bytes -> re-init A and exit loop
                        this.a = 0x8000; // e->a = 0x10000L after loop exit
                    }
                }
            }

            this.a <<= 1;
        }

        // Fetch values from our compact representation of Table D.3(D.2):
        // Qe values and probability estimation state machine
        int sv = st;
        int qe = ArithmeticTable[sv & 0x7f];
        byte nl = (byte)qe;
        qe >>= 8;   // Next_Index_LPS + Switch_MPS
        byte nm = (byte)qe;
        qe >>= 8;    // Next_Index_MPS

        // Decode & estimation procedures per sections D.2.4 & D.2.5
        int temp = this.a - qe;
        this.a = temp;
        temp <<= this.ct;
        if (this.c >= temp)
        {
            this.c -= temp;

            // Conditional LPS (less probable symbol) exchange
            if (this.a < qe)
            {
                this.a = qe;
                st = (byte)((sv & 0x80) ^ nm); // Estimate_after_MPS
            }
            else
            {
                this.a = qe;
                st = (byte)((sv & 0x80) ^ nl); // Estimate_after_LPS
                sv ^= 0x80; // Exchange LPS/MPS
            }
        }
        else if (this.a < 0x8000)
        {
            // Conditional MPS (more probable symbol) exchange
            if (this.a < qe)
            {
                st = (byte)((sv & 0x80) ^ nl); // Estimate_after_LPS
                sv ^= 0x80; // Exchange LPS/MPS
            }
            else
            {
                st = (byte)((sv & 0x80) ^ nm); // Estimate_after_MPS
            }
        }

        return sv >> 7;
    }

    // The following function specifies the packing of the four components
    // into the compact INT32 representation.
    // Note that this formula must match the actual arithmetic encoder and decoder implementation. The implementation has to be changed
    // if this formula is changed.
    // The current organization is leaned on Markus Kuhn's JBIG implementation (jbig_tab.c).
    [MethodImpl(InliningOptions.ShortMethod)]
    private static int Pack(int a, int b, int c, int d)
        => (a << 16) | (c << 8) | (d << 7) | b;
}

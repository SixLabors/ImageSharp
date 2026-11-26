// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.
#nullable disable

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Jpeg.Components.Encoder;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder;

/// <summary>
/// Decodes the Huffman encoded spectral scan.
/// </summary>
internal class HuffmanScanDecoder : IJpegScanDecoder
{
    // A DHT table identifier is 0 to 3 (ITU T.81, B.2.4.2). JpegDecoderCore rejects larger identifiers in DHT and SOS.
    private const int MaxTables = 4;

    private readonly BufferedReadStream stream;

    private readonly MemoryAllocator allocator;

    /// <summary>
    /// The combined lookup tables of the Huffman tables. The decoder allocates them on first use.
    /// DC table i starts at entry i * FastSize. AC table i starts at entry (MaxTables + i) * FastSize.
    /// </summary>
    private IMemoryOwner<int> fastLookups;

    /// <summary>
    /// The byte buffer of the bit reader. The decoder allocates it on first use.
    /// </summary>
    private IMemoryOwner<byte> entropyBuffer;

    /// <summary>
    /// Keeps <see cref="entropyBuffer"/> pinned while the decoder owns it. The bit reader holds its pointer.
    /// </summary>
    private MemoryHandle entropyBufferHandle;

    /// <summary>
    /// <see cref="JpegFrame"/> instance containing decoding-related information.
    /// </summary>
    private JpegFrame frame;

    /// <summary>
    /// Shortcut for <see cref="frame"/>.Components.
    /// </summary>
    private JpegComponent[] components;

    /// <summary>
    /// Number of component in the current scan.
    /// </summary>
    private int scanComponentCount;

    /// <summary>
    /// The reset interval determined by RST markers.
    /// </summary>
    private int restartInterval;

    /// <summary>
    /// The End-Of-Block countdown for ending the sequence prematurely when the remaining coefficients are zero.
    /// </summary>
    private int eobrun;

    /// <summary>
    /// The DHT tables that the stream defined. <see cref="BuildHuffmanTable"/> sets bit (type * MaxTables) + index for each table.
    /// </summary>
    private int definedTables;

    /// <summary>
    /// The DC Huffman tables.
    /// </summary>
    private readonly HuffmanTable[] dcHuffmanTables;

    /// <summary>
    /// The AC Huffman tables.
    /// </summary>
    private readonly HuffmanTable[] acHuffmanTables;

    private JpegBitReader scanBuffer;

    private readonly SpectralConverter spectralConverter;

    private readonly CancellationToken cancellationToken;

    /// <summary>
    /// Initializes a new instance of the <see cref="HuffmanScanDecoder"/> class.
    /// </summary>
    /// <param name="stream">The input stream.</param>
    /// <param name="converter">Spectral to pixel converter.</param>
    /// <param name="allocator">The allocator for the combined Huffman lookup tables and the byte buffer of the bit reader.</param>
    /// <param name="cancellationToken">The token to monitor cancellation.</param>
    public HuffmanScanDecoder(
        BufferedReadStream stream,
        SpectralConverter converter,
        MemoryAllocator allocator,
        CancellationToken cancellationToken)
    {
        this.stream = stream;
        this.spectralConverter = converter;
        this.allocator = allocator;
        this.cancellationToken = cancellationToken;

        this.dcHuffmanTables = new HuffmanTable[MaxTables];
        this.acHuffmanTables = new HuffmanTable[MaxTables];
    }

    /// <inheritdoc/>
    public SegmentIntegrityHandling SegmentIntegrityHandling { get; set; }

    /// <summary>
    /// Sets reset interval determined by RST markers.
    /// </summary>
    public int ResetInterval
    {
        set => this.restartInterval = value;
    }

    // The spectral selection start.
    public int SpectralStart { get; set; }

    // The spectral selection end.
    public int SpectralEnd { get; set; }

    // The successive approximation high bit end.
    public int SuccessiveHigh { get; set; }

    // The successive approximation low bit end.
    public int SuccessiveLow { get; set; }

    /// <inheritdoc/>
    public void ParseEntropyCodedData(int scanComponentCount, IccProfile iccProfile)
    {
        this.cancellationToken.ThrowIfCancellationRequested();

        this.scanComponentCount = scanComponentCount;

        if (this.entropyBuffer is null)
        {
            this.entropyBuffer = this.allocator.Allocate<byte>(JpegBitReader.EntropyBufferSize);
            this.entropyBufferHandle = this.entropyBuffer.Memory.Pin();
        }

        unsafe
        {
            this.scanBuffer = new JpegBitReader(
                this.stream,
                (byte*)this.entropyBufferHandle.Pointer,
                JpegBitReader.EntropyBufferSize,
                this.SegmentIntegrityHandling,
                this.restartInterval);
        }

        this.frame.AllocateComponents();

        if (!this.frame.Progressive)
        {
            this.ParseBaselineData(iccProfile);
        }
        else
        {
            this.ParseProgressiveData();
        }

        // A bad Huffman code, or a scan that ends early or that a marker interrupts, is damaged data.
        this.scanBuffer.ThrowOrIgnoreIfDataDamaged();

        // The bit reader reads bytes before it needs them.
        // EndScan moves the stream to the marker that the reader found, or to the next byte that the reader did not decode.
        this.scanBuffer.EndScan();
    }

    /// <inheritdoc/>
    public void InjectFrameData(JpegFrame frame, IRawJpegData jpegData)
    {
        this.frame = frame;
        this.components = frame.Components;

        this.spectralConverter.InjectFrameData(frame, jpegData);
    }

    private void ParseBaselineData(IccProfile iccProfile)
    {
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

    private void ParseBaselineDataInterleaved(IccProfile iccProfile)
    {
        int mcusPerColumn = this.frame.McusPerColumn;
        int mcusPerLine = this.frame.McusPerLine;
        ref JpegBitReader buffer = ref this.scanBuffer;
        Span<int> fastLookups = this.GetFastLookups();

        for (int j = 0; j < mcusPerColumn; j++)
        {
            this.cancellationToken.ThrowIfCancellationRequested();

            // decode from binary to spectral
            for (int i = 0; i < mcusPerLine; i++)
            {
                // This loop decodes one interleaved MCU. It reads the components in scan order.
                // The MCU column is i. The MCU row is j, and the spectral buffer holds only this MCU row.
                for (int k = 0; k < this.scanComponentCount; k++)
                {
                    int order = this.frame.ComponentOrder[k];
                    JpegComponent component = this.components[order];

                    ref HuffmanTable dcHuffmanTable = ref this.dcHuffmanTables[component.DcTableId];
                    ref HuffmanTable acHuffmanTable = ref this.acHuffmanTables[component.AcTableId];
                    ref int dcLookup = ref GetFastLookup(fastLookups, 0, component.DcTableId);
                    ref int acLookup = ref GetFastLookup(fastLookups, 1, component.AcTableId);

                    int h = component.HorizontalSamplingFactor;
                    int v = component.VerticalSamplingFactor;

                    // Scan out an mcu's worth of this component; that's just determined
                    // by the basic H and V specified for the component
                    for (int y = 0; y < v; y++)
                    {
                        Span<Block8x8> blockSpan = component.SpectralBlocks.DangerousGetRowSpan(y);
                        ref Block8x8 blockRef = ref MemoryMarshal.GetReference(blockSpan);

                        for (int x = 0; x < h; x++)
                        {
                            if (buffer.NoData)
                            {
                                // It is very likely that some spectral data was decoded before we've encountered 'end of scan'
                                // so we need to decode what's left and return (or maybe throw?)
                                this.spectralConverter.ConvertStrideBaseline(iccProfile);
                                return;
                            }

                            int blockCol = (i * h) + x;

                            this.DecodeBlockBaseline(
                                component,
                                ref Unsafe.Add(ref blockRef, (uint)blockCol),
                                ref dcHuffmanTable,
                                ref dcLookup,
                                ref acHuffmanTable,
                                ref acLookup);
                        }
                    }
                }

                // After all interleaved components, that's an interleaved MCU,
                // so now count down the restart interval
                this.HandleRestart();
            }

            // Convert from spectral to actual pixels via given converter
            this.spectralConverter.ConvertStrideBaseline(iccProfile);
        }
    }

    private void ParseBaselineDataNonInterleaved()
    {
        JpegComponent component = this.components[this.frame.ComponentOrder[0]];
        ref JpegBitReader buffer = ref this.scanBuffer;

        int w = component.WidthInBlocks;
        int h = component.HeightInBlocks;

        ref HuffmanTable dcHuffmanTable = ref this.dcHuffmanTables[component.DcTableId];
        ref HuffmanTable acHuffmanTable = ref this.acHuffmanTables[component.AcTableId];
        Span<int> fastLookups = this.GetFastLookups();
        ref int dcLookup = ref GetFastLookup(fastLookups, 0, component.DcTableId);
        ref int acLookup = ref GetFastLookup(fastLookups, 1, component.AcTableId);

        for (int j = 0; j < h; j++)
        {
            this.cancellationToken.ThrowIfCancellationRequested();
            Span<Block8x8> blockSpan = component.SpectralBlocks.DangerousGetRowSpan(j);
            ref Block8x8 blockRef = ref MemoryMarshal.GetReference(blockSpan);

            for (int i = 0; i < w; i++)
            {
                if (buffer.NoData)
                {
                    return;
                }

                this.DecodeBlockBaseline(
                    component,
                    ref Unsafe.Add(ref blockRef, (uint)i),
                    ref dcHuffmanTable,
                    ref dcLookup,
                    ref acHuffmanTable,
                    ref acLookup);

                this.HandleRestart();
            }
        }
    }

    private void ParseBaselineDataSingleComponent(IccProfile iccProfile)
    {
        JpegComponent component = this.frame.Components[0];
        int mcuLines = this.frame.McusPerColumn;
        int w = component.WidthInBlocks;
        int h = component.SamplingFactors.Height;
        ref HuffmanTable dcHuffmanTable = ref this.dcHuffmanTables[component.DcTableId];
        ref HuffmanTable acHuffmanTable = ref this.acHuffmanTables[component.AcTableId];
        Span<int> fastLookups = this.GetFastLookups();
        ref int dcLookup = ref GetFastLookup(fastLookups, 0, component.DcTableId);
        ref int acLookup = ref GetFastLookup(fastLookups, 1, component.AcTableId);

        ref JpegBitReader buffer = ref this.scanBuffer;

        for (int i = 0; i < mcuLines; i++)
        {
            this.cancellationToken.ThrowIfCancellationRequested();

            // decode from binary to spectral
            for (int j = 0; j < h; j++)
            {
                Span<Block8x8> blockSpan = component.SpectralBlocks.DangerousGetRowSpan(j);
                ref Block8x8 blockRef = ref MemoryMarshal.GetReference(blockSpan);

                for (int k = 0; k < w; k++)
                {
                    if (buffer.NoData)
                    {
                        // It is very likely that some spectral data was decoded before we've encountered 'end of scan'
                        // so we need to decode what's left and return (or maybe throw?)
                        this.spectralConverter.ConvertStrideBaseline(iccProfile);
                        return;
                    }

                    this.DecodeBlockBaseline(
                        component,
                        ref Unsafe.Add(ref blockRef, (uint)k),
                        ref dcHuffmanTable,
                        ref dcLookup,
                        ref acHuffmanTable,
                        ref acLookup);

                    this.HandleRestart();
                }
            }

            // Convert from spectral to actual pixels via given converter
            this.spectralConverter.ConvertStrideBaseline(iccProfile);
        }
    }

    private void ParseProgressiveData()
    {
        if (this.scanComponentCount == 1)
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
        int mcusPerColumn = this.frame.McusPerColumn;
        int mcusPerLine = this.frame.McusPerLine;
        ref JpegBitReader buffer = ref this.scanBuffer;

        for (int j = 0; j < mcusPerColumn; j++)
        {
            for (int i = 0; i < mcusPerLine; i++)
            {
                // This loop decodes one interleaved MCU. It reads the components in scan order.
                // The MCU row is j, and the MCU column is i.
                for (int k = 0; k < this.scanComponentCount; k++)
                {
                    int order = this.frame.ComponentOrder[k];
                    JpegComponent component = this.components[order];
                    ref HuffmanTable dcHuffmanTable = ref this.dcHuffmanTables[component.DcTableId];

                    int h = component.HorizontalSamplingFactor;
                    int v = component.VerticalSamplingFactor;

                    // Scan out an mcu's worth of this component; that's just determined
                    // by the basic H and V specified for the component
                    for (int y = 0; y < v; y++)
                    {
                        int blockRow = (j * v) + y;
                        Span<Block8x8> blockSpan = component.SpectralBlocks.DangerousGetRowSpan(blockRow);
                        ref Block8x8 blockRef = ref MemoryMarshal.GetReference(blockSpan);

                        for (int x = 0; x < h; x++)
                        {
                            if (buffer.NoData)
                            {
                                return;
                            }

                            int blockCol = (i * h) + x;

                            this.DecodeBlockProgressiveDC(
                                component,
                                ref Unsafe.Add(ref blockRef, (uint)blockCol),
                                ref dcHuffmanTable);
                        }
                    }
                }

                // After all interleaved components, that's an interleaved MCU,
                // so now count down the restart interval
                this.HandleRestart();
            }
        }
    }

    private void ParseProgressiveDataNonInterleaved()
    {
        JpegComponent component = this.components[this.frame.ComponentOrder[0]];
        ref JpegBitReader buffer = ref this.scanBuffer;

        int w = component.WidthInBlocks;
        int h = component.HeightInBlocks;

        if (this.SpectralStart == 0)
        {
            ref HuffmanTable dcHuffmanTable = ref this.dcHuffmanTables[component.DcTableId];

            for (int j = 0; j < h; j++)
            {
                this.cancellationToken.ThrowIfCancellationRequested();

                Span<Block8x8> blockSpan = component.SpectralBlocks.DangerousGetRowSpan(j);
                ref Block8x8 blockRef = ref MemoryMarshal.GetReference(blockSpan);

                for (int i = 0; i < w; i++)
                {
                    if (buffer.NoData)
                    {
                        return;
                    }

                    this.DecodeBlockProgressiveDC(
                        component,
                        ref Unsafe.Add(ref blockRef, (uint)i),
                        ref dcHuffmanTable);

                    this.HandleRestart();
                }
            }
        }
        else
        {
            ref HuffmanTable acHuffmanTable = ref this.acHuffmanTables[component.AcTableId];

            for (int j = 0; j < h; j++)
            {
                this.cancellationToken.ThrowIfCancellationRequested();

                Span<Block8x8> blockSpan = component.SpectralBlocks.DangerousGetRowSpan(j);
                ref Block8x8 blockRef = ref MemoryMarshal.GetReference(blockSpan);

                for (int i = 0; i < w; i++)
                {
                    if (buffer.NoData)
                    {
                        return;
                    }

                    this.DecodeBlockProgressiveAC(
                        ref Unsafe.Add(ref blockRef, (uint)i),
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
        ref int dcLookup,
        ref HuffmanTable acTable,
        ref int acLookup)
    {
        ref short blockDataRef = ref Unsafe.As<Block8x8, short>(ref block);

        int t = this.scanBuffer.DecodeBlockBaseline(ref blockDataRef, ref dcTable, ref dcLookup, ref acTable, ref acLookup);

        t += component.DcPredictor;
        component.DcPredictor = t;
        blockDataRef = (short)t;
    }

    private void DecodeBlockProgressiveDC(JpegComponent component, ref Block8x8 block, ref HuffmanTable dcTable)
    {
        ref short blockDataRef = ref Unsafe.As<Block8x8, short>(ref block);
        ref JpegBitReader buffer = ref this.scanBuffer;

        if (this.SuccessiveHigh == 0)
        {
            // First scan for DC coefficient, must be first
            int s = buffer.DecodeHuffman(ref dcTable);
            if (s != 0)
            {
                s = buffer.Receive(s);
            }

            s += component.DcPredictor;
            component.DcPredictor = s;
            blockDataRef = (short)(s << this.SuccessiveLow);
        }
        else
        {
            // Refinement scan for DC coefficient
            buffer.CheckBits();
            blockDataRef |= (short)(buffer.GetBits(1) << this.SuccessiveLow);
        }
    }

    private void DecodeBlockProgressiveAC(ref Block8x8 block, ref HuffmanTable acTable)
    {
        ref short blockDataRef = ref Unsafe.As<Block8x8, short>(ref block);
        if (this.SuccessiveHigh == 0)
        {
            // MCU decoding for AC initial scan (either spectral selection,
            // or first pass of successive approximation).
            if (this.eobrun != 0)
            {
                --this.eobrun;
                return;
            }

            ref JpegBitReader buffer = ref this.scanBuffer;
            int start = this.SpectralStart;
            int end = this.SpectralEnd;
            int low = this.SuccessiveLow;

            for (int i = start; i <= end; ++i)
            {
                int s = buffer.DecodeHuffman(ref acTable);
                int r = s >> 4;
                s &= 15;

                i += r;

                if (s != 0)
                {
                    s = buffer.Receive(s);
                    Unsafe.Add(ref blockDataRef, ZigZag.TransposingOrder[i]) = (short)(s << low);
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
        ref JpegBitReader buffer = ref this.scanBuffer;
        int start = this.SpectralStart;
        int end = this.SpectralEnd;

        int p1 = 1 << this.SuccessiveLow;
        int m1 = (-1) << this.SuccessiveLow;

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
                    ref short coef = ref Unsafe.Add(ref blockDataRef, ZigZag.TransposingOrder[k]);
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
                    Unsafe.Add(ref blockDataRef, ZigZag.TransposingOrder[k]) = (short)s;
                }
            }
        }

        if (this.eobrun > 0)
        {
            for (; k <= end; k++)
            {
                ref short coef = ref Unsafe.Add(ref blockDataRef, ZigZag.TransposingOrder[k]);

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

    /// <summary>
    /// Counts down the restart interval after an MCU. At a restart marker, the method reports the damage in the interval and resets the decode state.
    /// </summary>
    [MethodImpl(InliningOptions.ShortMethod)]
    private void HandleRestart()
    {
        if (this.scanBuffer.HandleRestart())
        {
            // Reset clears the state of the bit reader. Thus the method reports the damage in the interval before the reset.
            this.scanBuffer.ThrowOrIgnoreIfDataDamaged();
            this.Reset();
        }
    }

    /// <summary>
    /// Build the Huffman table using code lengths and code values.
    /// </summary>
    /// <param name="type">Table type.</param>
    /// <param name="index">Table index.</param>
    /// <param name="codeLengths">Code lengths.</param>
    /// <param name="values">Code values.</param>
    /// <param name="workspace">The provided spare workspace memory, can be dirty.</param>
    [MethodImpl(InliningOptions.ShortMethod)]
    public void BuildHuffmanTable(int type, int index, ReadOnlySpan<byte> codeLengths, ReadOnlySpan<byte> values, Span<uint> workspace)
    {
        HuffmanTable[] tables = type == 0 ? this.dcHuffmanTables : this.acHuffmanTables;
        Span<int> fastLookup = this.GetFastLookups().Slice(((type * MaxTables) + index) * JpegConstants.Huffman.FastSize, JpegConstants.Huffman.FastSize);
        tables[index] = new HuffmanTable(codeLengths, values, workspace, fastLookup);
        this.definedTables |= 1 << ((type * MaxTables) + index);
    }

    /// <summary>
    /// Makes sure that each Huffman table that the next scan uses is defined. The method uses the scan parameters and the table selectors of the scan
    /// components. The decoder calls it before <see cref="ParseEntropyCodedData"/>.
    /// </summary>
    /// <remarks>
    /// A sequential scan uses the DC and AC tables of each component. A progressive scan uses the DC tables in a first DC scan and no table in a DC refinement
    /// scan. A progressive AC scan uses the AC table. A sequential decoder uses the standard table for an undefined table 0 or 1, as libjpeg-turbo does.
    /// Motion JPEG frames often omit these tables.
    /// </remarks>
    /// <param name="scanComponentCount">The number of components in the scan.</param>
    /// <returns><see langword="true"/> if each table of the scan is defined. Otherwise <see langword="false"/>.</returns>
    public bool TryInitDecodingTables(int scanComponentCount)
    {
        bool progressive = this.frame.Progressive;
        bool usesDc = !progressive || (this.SpectralStart == 0 && this.SuccessiveHigh == 0);
        bool usesAc = !progressive || this.SpectralStart != 0;
        for (int k = 0; k < scanComponentCount; k++)
        {
            JpegComponent component = this.components[this.frame.ComponentOrder[k]];
            if ((usesDc && !this.TryInitDecodingTable(0, component.DcTableId)) || (usesAc && !this.TryInitDecodingTable(1, component.AcTableId)))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.fastLookups?.Dispose();
        this.fastLookups = null;

        this.entropyBufferHandle.Dispose();
        this.entropyBuffer?.Dispose();
        this.entropyBuffer = null;
    }

    /// <summary>
    /// Gets the combined lookup tables of all Huffman tables. The decoder allocates the buffer on first use. The decoder does not clear the buffer. <see
    /// cref="HuffmanTable"/> fills all entries of a table when it builds the table. <see cref="TryInitDecodingTables"/> makes sure that a scan uses only built
    /// tables.
    /// </summary>
    /// <returns>The combined lookup tables.</returns>
    private Span<int> GetFastLookups()
        => (this.fastLookups ??= this.allocator.Allocate<int>(2 * MaxTables * JpegConstants.Huffman.FastSize)).Memory.Span;

    /// <summary>
    /// Gets the first combined lookup entry of a Huffman table.
    /// </summary>
    /// <param name="fastLookups">The combined lookup tables from <see cref="GetFastLookups"/>.</param>
    /// <param name="type">The table type. 0 is DC, 1 is AC.</param>
    /// <param name="index">The table index. JpegDecoderCore makes sure that it is less than <see cref="MaxTables"/>.</param>
    /// <returns>The first entry of the table.</returns>
    private static ref int GetFastLookup(Span<int> fastLookups, int type, int index)
        => ref Unsafe.Add(ref MemoryMarshal.GetReference(fastLookups), (uint)(((type * MaxTables) + index) * JpegConstants.Huffman.FastSize));

    /// <summary>
    /// Makes sure that a Huffman table is defined. For an undefined table 0 or 1 of a sequential frame, the method builds the standard table.
    /// </summary>
    /// <param name="type">The table type. 0 is DC, 1 is AC.</param>
    /// <param name="index">The table index. JpegDecoderCore makes sure that it is less than <see cref="MaxTables"/>.</param>
    /// <returns><see langword="true"/> if the table is defined. Otherwise <see langword="false"/>.</returns>
    private bool TryInitDecodingTable(int type, int index)
    {
        if ((this.definedTables & (1 << ((type * MaxTables) + index))) != 0)
        {
            return true;
        }

        // The decoder adds the standard tables only to a sequential frame, as libjpeg-turbo does. A progressive frame requires a DHT marker for each table.
        // Table 0 is for luminance and table 1 is for chrominance (ITU T.81, K.3).
        if (this.frame.Progressive || index > 1)
        {
            return false;
        }

        HuffmanSpec spec = type == 0
            ? index == 0 ? HuffmanSpec.LuminanceDC : HuffmanSpec.ChrominanceDC
            : index == 0 ? HuffmanSpec.LuminanceAC : HuffmanSpec.ChrominanceAC;

        // The table build reads the code length counts at indexes 1 to 16. The specification stores them at 0 to 15. The workspace is first in the buffer, so
        // it has the alignment of uint.
        const int codeLengthsByteSize = 17;
        using IMemoryOwner<byte> buffer = this.allocator.Allocate<byte>(HuffmanTable.WorkspaceByteSize + codeLengthsByteSize);
        Span<byte> bufferSpan = buffer.GetSpan();
        Span<uint> workspace = MemoryMarshal.Cast<byte, uint>(bufferSpan[..HuffmanTable.WorkspaceByteSize]);
        Span<byte> codeLengths = bufferSpan.Slice(HuffmanTable.WorkspaceByteSize, codeLengthsByteSize);
        spec.Count.CopyTo(codeLengths[1..]);

        this.BuildHuffmanTable(type, index, codeLengths, spec.Values, workspace);
        return true;
    }
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.
#nullable disable

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder;

/// <summary>
/// Decodes the Huffman encoded spectral scan.
/// Originally ported from <see href="https://github.com/t0rakka/mango"/>
/// with additional fixes for both performance and common encoding errors.
/// </summary>
internal class HuffmanScanDecoder : IJpegScanDecoder
{
    // A DHT table identifier is 0 to 3 (ITU T.81, B.2.4.2). JpegDecoderCore rejects larger identifiers in DHT and SOS.
    private const int MaxTables = 4;

    private readonly BufferedReadStream stream;

    private readonly MemoryAllocator allocator;

    /// <summary>
    /// The combined lookup tables of the Huffman tables, allocated on first use.
    /// DC table i starts at entry i * FastSize. AC table i starts at entry (MaxTables + i) * FastSize.
    /// </summary>
    private IMemoryOwner<int> fastLookups;

    /// <summary>
    /// The read-ahead byte buffer of the bit reader, allocated on first use.
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

    /// <summary>
    /// The End-Of-Block countdown for ending the sequence prematurely when the remaining coefficients are zero.
    /// </summary>
    private int eobrun;

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
    /// <param name="allocator">The allocator for the combined Huffman lookup tables and the read-ahead byte buffer.</param>
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
        set
        {
            this.restartInterval = value;
            this.todo = value;
        }
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
            this.scanBuffer = new JpegBitReader(this.stream, (byte*)this.entropyBufferHandle.Pointer, JpegBitReader.EntropyBufferSize);
        }

        this.frame.AllocateComponents();

        this.todo = this.restartInterval;

        if (!this.frame.Progressive)
        {
            this.ParseBaselineData(iccProfile);
        }
        else
        {
            this.ParseProgressiveData();
        }

        // The bit reader reads ahead. This moves the stream to the detected marker or to the next unread byte.
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
        int mcu = 0;
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
                // Scan an interleaved mcu... process components in order
                int mcuCol = mcu % mcusPerLine;
                for (int k = 0; k < this.scanComponentCount; k++)
                {
                    int order = this.frame.ComponentOrder[k];
                    JpegComponent component = this.components[order] as JpegComponent;

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

                            int blockCol = (mcuCol * h) + x;

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
                mcu++;
                this.HandleRestart();
            }

            // Convert from spectral to actual pixels via given converter
            this.spectralConverter.ConvertStrideBaseline(iccProfile);
        }
    }

    private void ParseBaselineDataNonInterleaved()
    {
        JpegComponent component = this.components[this.frame.ComponentOrder[0]] as JpegComponent;
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

    private void ParseProgressiveData()
    {
        this.CheckProgressiveData();

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
        int mcu = 0;
        int mcusPerColumn = this.frame.McusPerColumn;
        int mcusPerLine = this.frame.McusPerLine;
        ref JpegBitReader buffer = ref this.scanBuffer;

        for (int j = 0; j < mcusPerColumn; j++)
        {
            for (int i = 0; i < mcusPerLine; i++)
            {
                // Scan an interleaved mcu... process components in order
                int mcuRow = mcu / mcusPerLine;
                int mcuCol = mcu % mcusPerLine;
                for (int k = 0; k < this.scanComponentCount; k++)
                {
                    int order = this.frame.ComponentOrder[k];
                    JpegComponent component = this.components[order] as JpegComponent;
                    ref HuffmanTable dcHuffmanTable = ref this.dcHuffmanTables[component.DcTableId];

                    int h = component.HorizontalSamplingFactor;
                    int v = component.VerticalSamplingFactor;

                    // Scan out an mcu's worth of this component; that's just determined
                    // by the basic H and V specified for the component
                    for (int y = 0; y < v; y++)
                    {
                        int blockRow = (mcuRow * v) + y;
                        Span<Block8x8> blockSpan = component.SpectralBlocks.DangerousGetRowSpan(blockRow);
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
                                ref Unsafe.Add(ref blockRef, (uint)blockCol),
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
        JpegComponent component = this.components[this.frame.ComponentOrder[0]] as JpegComponent;
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

    [MethodImpl(InliningOptions.ShortMethod)]
    private bool HandleRestart()
    {
        if (this.restartInterval > 0 && (--this.todo) == 0)
        {
            // A reserved marker cannot complete a restart interval. The shared reader applies the recovery policy.
            if (this.scanBuffer.Marker == JpegConstants.Markers.XFF || this.scanBuffer.HasReservedMarker())
            {
                if (!this.scanBuffer.FindNextMarker(this.SegmentIntegrityHandling))
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
                this.scanBuffer.RewindToMarker();
                this.Reset();
                return true;
            }
        }

        return false;
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
    /// Gets the combined lookup tables of all Huffman tables. The buffer is allocated clean on first use.
    /// A table that the stream does not define then has only zero entries, so it decodes through the slow path,
    /// the same as its zeroed <see cref="HuffmanTable"/>.
    /// </summary>
    /// <returns>The combined lookup tables.</returns>
    private Span<int> GetFastLookups()
        => (this.fastLookups ??= this.allocator.Allocate<int>(2 * MaxTables * JpegConstants.Huffman.FastSize, AllocationOptions.Clean)).Memory.Span;

    /// <summary>
    /// Gets the first combined lookup entry of a Huffman table.
    /// </summary>
    /// <param name="fastLookups">The combined lookup tables from <see cref="GetFastLookups"/>.</param>
    /// <param name="type">The table type. 0 is DC, 1 is AC.</param>
    /// <param name="index">The table index. JpegDecoderCore has checked that it is less than <see cref="MaxTables"/>.</param>
    /// <returns>The first entry of the table.</returns>
    private static ref int GetFastLookup(Span<int> fastLookups, int type, int index)
        => ref Unsafe.Add(ref MemoryMarshal.GetReference(fastLookups), (uint)(((type * MaxTables) + index) * JpegConstants.Huffman.FastSize));
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.
#nullable disable

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.IO;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder;

/// <summary>
/// Decodes the Huffman encoded spectral scan.
/// Originally ported from <see href="https://github.com/t0rakka/mango"/>
/// with additional fixes for both performance and common encoding errors.
/// </summary>
internal class HuffmanScanDecoder : IJpegScanDecoder
{
    private readonly BufferedReadStream stream;

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
    /// <param name="cancellationToken">The token to monitor cancellation.</param>
    public HuffmanScanDecoder(
        BufferedReadStream stream,
        SpectralConverter converter,
        CancellationToken cancellationToken)
    {
        this.stream = stream;
        this.spectralConverter = converter;
        this.cancellationToken = cancellationToken;

        // TODO: this is actually a variable value depending on component count
        const int maxTables = 4;
        this.dcHuffmanTables = new HuffmanTable[maxTables];
        this.acHuffmanTables = new HuffmanTable[maxTables];
    }

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
    public void ParseEntropyCodedData(int scanComponentCount)
    {
        this.cancellationToken.ThrowIfCancellationRequested();

        this.scanComponentCount = scanComponentCount;

        this.scanBuffer = new JpegBitReader(this.stream);

        this.frame.AllocateComponents();

        this.todo = this.restartInterval;

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

    /// <inheritdoc/>
    public void InjectFrameData(JpegFrame frame, IRawJpegData jpegData)
    {
        this.frame = frame;
        this.components = frame.Components;

        this.spectralConverter.InjectFrameData(frame, jpegData);
    }

    private void ParseBaselineData()
    {
        if (this.scanComponentCount != 1)
        {
            this.spectralConverter.PrepareForDecoding();
            this.ParseBaselineDataInterleaved();
            this.spectralConverter.CommitConversion();
        }
        else if (this.frame.ComponentCount == 1)
        {
            this.spectralConverter.PrepareForDecoding();
            this.ParseBaselineDataSingleComponent();
            this.spectralConverter.CommitConversion();
        }
        else
        {
            this.ParseBaselineDataNonInterleaved();
        }
    }

    private void ParseBaselineDataInterleaved()
    {
        int mcu = 0;
        int mcusPerColumn = this.frame.McusPerColumn;
        int mcusPerLine = this.frame.McusPerLine;
        ref JpegBitReader buffer = ref this.scanBuffer;

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
                                this.spectralConverter.ConvertStrideBaseline();
                                return;
                            }

                            int blockCol = (mcuCol * h) + x;

                            this.DecodeBlockBaseline(
                                component,
                                ref Unsafe.Add(ref blockRef, (uint)blockCol),
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

            // Convert from spectral to actual pixels via given converter
            this.spectralConverter.ConvertStrideBaseline();
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
                    ref acHuffmanTable);

                this.HandleRestart();
            }
        }
    }

    private void ParseBaselineDataSingleComponent()
    {
        JpegComponent component = this.frame.Components[0];
        int mcuLines = this.frame.McusPerColumn;
        int w = component.WidthInBlocks;
        int h = component.SamplingFactors.Height;
        ref HuffmanTable dcHuffmanTable = ref this.dcHuffmanTables[component.DcTableId];
        ref HuffmanTable acHuffmanTable = ref this.acHuffmanTables[component.AcTableId];

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
                        this.spectralConverter.ConvertStrideBaseline();
                        return;
                    }

                    this.DecodeBlockBaseline(
                        component,
                        ref Unsafe.Add(ref blockRef, (uint)k),
                        ref dcHuffmanTable,
                        ref acHuffmanTable);

                    this.HandleRestart();
                }
            }

            // Convert from spectral to actual pixels via given converter
            this.spectralConverter.ConvertStrideBaseline();
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
        ref HuffmanTable acTable)
    {
        ref short blockDataRef = ref Unsafe.As<Block8x8, short>(ref block);
        ref JpegBitReader buffer = ref this.scanBuffer;

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
                Unsafe.Add(ref blockDataRef, ZigZag.TransposingOrder[i++]) = (short)s;
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

    /// <summary>
    /// Build the huffman table using code lengths and code values.
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
        tables[index] = new HuffmanTable(codeLengths, values, workspace);
    }
}

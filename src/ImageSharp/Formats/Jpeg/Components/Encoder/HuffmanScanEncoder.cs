// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Encoder;

internal class HuffmanScanEncoder
{
    /// <summary>
    /// Maximum number of bytes encoded jpeg 8x8 block can occupy.
    /// It's highly unlikely for block to occupy this much space - it's a theoretical limit.
    /// </summary>
    /// <remarks>
    /// Where 16 is maximum huffman code binary length according to itu
    /// specs. 10 is maximum value binary length, value comes from discrete
    /// cosine tranform with value range: [-1024..1023]. Block stores
    /// 8x8 = 64 values thus multiplication by 64. Then divided by 8 to get
    /// the number of bytes. This value is then multiplied by
    /// <see cref="MaxBytesPerBlockMultiplier"/> for performance reasons.
    /// </remarks>
    private const int MaxBytesPerBlock = (16 + 10) * 64 / 8 * MaxBytesPerBlockMultiplier;

    /// <summary>
    /// Multiplier used within cache buffers size calculation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Theoretically, <see cref="MaxBytesPerBlock"/> bytes buffer can fit
    /// exactly one minimal coding unit. In reality, coding blocks occupy much
    /// less space than the theoretical maximum - this can be exploited.
    /// If temporal buffer size is multiplied by at least 2, second half of
    /// the resulting buffer will be used as an overflow 'guard' if next
    /// block would occupy maximum number of bytes. While first half may fit
    /// many blocks before needing to flush.
    /// </para>
    /// <para>
    /// This is subject to change. This can be equal to 1 but recomended
    /// value is 2 or even greater - futher benchmarking needed.
    /// </para>
    /// </remarks>
    private const int MaxBytesPerBlockMultiplier = 2;

    /// <summary>
    /// <see cref="streamWriteBuffer"/> size multiplier.
    /// </summary>
    /// <remarks>
    /// Jpeg specification requiers to insert 'stuff' bytes after each
    /// 0xff byte value. Worst case scenarion is when all bytes are 0xff.
    /// While it's highly unlikely (if not impossible) to get such
    /// combination, it's theoretically possible so buffer size must be guarded.
    /// </remarks>
    private const int OutputBufferLengthMultiplier = 2;

    /// <summary>
    /// The DC Huffman tables.
    /// </summary>
    private readonly HuffmanLut[] dcHuffmanTables = new HuffmanLut[4];

    /// <summary>
    /// The AC Huffman tables.
    /// </summary>
    private readonly HuffmanLut[] acHuffmanTables = new HuffmanLut[4];

    /// <summary>
    /// Emitted bits 'micro buffer' before being transferred to the <see cref="emitBuffer"/>.
    /// </summary>
    private uint accumulatedBits;

    /// <summary>
    /// Buffer for temporal storage of huffman rle encoding bit data.
    /// </summary>
    /// <remarks>
    /// Encoding bits are assembled to 4 byte unsigned integers and then copied to this buffer.
    /// This process does NOT include inserting stuff bytes.
    /// </remarks>
    private readonly uint[] emitBuffer;

    /// <summary>
    /// Buffer for temporal storage which is then written to the output stream.
    /// </summary>
    /// <remarks>
    /// Encoding bits from <see cref="emitBuffer"/> are copied to this byte buffer including stuff bytes.
    /// </remarks>
    private readonly byte[] streamWriteBuffer;

    private readonly int restartInterval;

    /// <summary>
    /// Number of jagged bits stored in <see cref="accumulatedBits"/>
    /// </summary>
    private int bitCount;

    private int emitWriteIndex;

    /// <summary>
    /// The output stream. All attempted writes after the first error become no-ops.
    /// </summary>
    private readonly Stream target;

    /// <summary>
    /// Initializes a new instance of the <see cref="HuffmanScanEncoder"/> class.
    /// </summary>
    /// <param name="blocksPerCodingUnit">Amount of encoded 8x8 blocks per single jpeg macroblock.</param>
    /// <param name="restartInterval">Numbers of MCUs between restart markers.</param>
    /// <param name="outputStream">Output stream for saving encoded data.</param>
    public HuffmanScanEncoder(int blocksPerCodingUnit, int restartInterval, Stream outputStream)
    {
        int emitBufferByteLength = MaxBytesPerBlock * blocksPerCodingUnit;
        this.emitBuffer = new uint[emitBufferByteLength / sizeof(uint)];
        this.emitWriteIndex = this.emitBuffer.Length;

        this.restartInterval = restartInterval;

        this.streamWriteBuffer = new byte[emitBufferByteLength * OutputBufferLengthMultiplier];

        this.target = outputStream;
    }

    /// <summary>
    /// Gets a value indicating whether <see cref="emitBuffer"/> is full
    /// and must be flushed using <see cref="FlushToStream()"/>
    /// before encoding next 8x8 coding block.
    /// </summary>
    private bool IsStreamFlushNeeded
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.emitWriteIndex < (int)((uint)this.emitBuffer.Length / 2);
    }

    public void BuildHuffmanTable(JpegHuffmanTableConfig tableConfig)
    {
        HuffmanLut[] tables = tableConfig.Class == 0 ? this.dcHuffmanTables : this.acHuffmanTables;
        tables[tableConfig.DestinationIndex] = new HuffmanLut(tableConfig.Table);
    }

    /// <summary>
    /// Encodes scan in baseline interleaved mode.
    /// </summary>
    /// <param name="color">Output color space.</param>
    /// <param name="frame">Frame to encode.</param>
    /// <param name="converter">Converter from color to spectral.</param>
    /// <param name="cancellationToken">The token to request cancellation.</param>
    public void EncodeScanBaselineInterleaved<TPixel>(JpegColorType color, JpegFrame frame, SpectralConverter<TPixel> converter, CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        switch (color)
        {
            case JpegColorType.YCbCrRatio444:
            case JpegColorType.Rgb:
                this.EncodeThreeComponentBaselineInterleavedScanNoSubsampling(frame, converter, cancellationToken);
                break;
            default:
                this.EncodeScanBaselineInterleaved(frame, converter, cancellationToken);
                break;
        }
    }

    /// <summary>
    /// Encodes grayscale scan in baseline interleaved mode.
    /// </summary>
    /// <param name="component">Component with grayscale data.</param>
    /// <param name="converter">Converter from color to spectral.</param>
    /// <param name="cancellationToken">The token to request cancellation.</param>
    public void EncodeScanBaselineSingleComponent<TPixel>(Component component, SpectralConverter<TPixel> converter, CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        int h = component.HeightInBlocks;
        int w = component.WidthInBlocks;

        ref HuffmanLut dcHuffmanTable = ref this.dcHuffmanTables[component.DcTableId];
        ref HuffmanLut acHuffmanTable = ref this.acHuffmanTables[component.AcTableId];

        for (int i = 0; i < h; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Convert from pixels to spectral via given converter
            converter.ConvertStrideBaseline();

            // Encode spectral to binary
            Span<Block8x8> blockSpan = component.SpectralBlocks.DangerousGetRowSpan(y: 0);
            ref Block8x8 blockRef = ref MemoryMarshal.GetReference(blockSpan);

            for (nuint k = 0; k < (uint)w; k++)
            {
                this.WriteBlock(
                    component,
                    ref Unsafe.Add(ref blockRef, k),
                    ref dcHuffmanTable,
                    ref acHuffmanTable);

                if (this.IsStreamFlushNeeded)
                {
                    this.FlushToStream();
                }
            }
        }

        this.FlushRemainingBytes();
    }

    /// <summary>
    /// Encodes scan with a single component in baseline non-interleaved mode.
    /// </summary>
    /// <param name="component">Component with grayscale data.</param>
    /// <param name="cancellationToken">The token to request cancellation.</param>
    public void EncodeScanBaseline(Component component, CancellationToken cancellationToken)
    {
        int h = component.HeightInBlocks;
        int w = component.WidthInBlocks;

        ref HuffmanLut dcHuffmanTable = ref this.dcHuffmanTables[component.DcTableId];
        ref HuffmanLut acHuffmanTable = ref this.acHuffmanTables[component.AcTableId];

        int restarts = 0;
        int restartsToGo = this.restartInterval;

        for (int i = 0; i < h; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Encode spectral to binary
            Span<Block8x8> blockSpan = component.SpectralBlocks.DangerousGetRowSpan(y: i);
            ref Block8x8 blockRef = ref MemoryMarshal.GetReference(blockSpan);

            for (nuint k = 0; k < (uint)w; k++)
            {
                if (this.restartInterval > 0 && restartsToGo == 0)
                {
                    this.FlushRemainingBytes();
                    this.WriteRestart(restarts % 8);
                    component.DcPredictor = 0;
                }

                this.WriteBlock(
                    component,
                    ref Unsafe.Add(ref blockRef, k),
                    ref dcHuffmanTable,
                    ref acHuffmanTable);

                if (this.IsStreamFlushNeeded)
                {
                    this.FlushToStream();
                }

                if (this.restartInterval > 0)
                {
                    if (restartsToGo == 0)
                    {
                        restartsToGo = this.restartInterval;
                        restarts++;
                    }

                    restartsToGo--;
                }
            }
        }

        this.FlushRemainingBytes();
    }

    /// <summary>
    /// Encodes the DC coefficients for a given component's blocks in a scan.
    /// </summary>
    /// <param name="component">The component whose DC coefficients need to be encoded.</param>
    /// <param name="cancellationToken">The token to request cancellation.</param>
    public void EncodeDcScan(Component component, CancellationToken cancellationToken)
    {
        int h = component.HeightInBlocks;
        int w = component.WidthInBlocks;

        ref HuffmanLut dcHuffmanTable = ref this.dcHuffmanTables[component.DcTableId];

        int restarts = 0;
        int restartsToGo = this.restartInterval;

        for (int i = 0; i < h; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Span<Block8x8> blockSpan = component.SpectralBlocks.DangerousGetRowSpan(y: i);
            ref Block8x8 blockRef = ref MemoryMarshal.GetReference(blockSpan);

            for (nuint k = 0; k < (uint)w; k++)
            {
                if (this.restartInterval > 0 && restartsToGo == 0)
                {
                    this.FlushRemainingBytes();
                    this.WriteRestart(restarts % 8);
                    component.DcPredictor = 0;
                }

                this.WriteDc(
                    component,
                    ref Unsafe.Add(ref blockRef, k),
                    ref dcHuffmanTable);

                if (this.IsStreamFlushNeeded)
                {
                    this.FlushToStream();
                }

                if (this.restartInterval > 0)
                {
                    if (restartsToGo == 0)
                    {
                        restartsToGo = this.restartInterval;
                        restarts++;
                    }

                    restartsToGo--;
                }
            }
        }

        this.FlushRemainingBytes();
    }

    /// <summary>
    /// Encodes the AC coefficients for a specified range of blocks in a component's scan.
    /// </summary>
    /// <param name="component">The component whose AC coefficients need to be encoded.</param>
    /// <param name="start">The starting index of the AC coefficient range to encode.</param>
    /// <param name="end">The ending index of the AC coefficient range to encode.</param>
    /// <param name="cancellationToken">The token to request cancellation.</param>
    public void EncodeAcScan(Component component, nint start, nint end, CancellationToken cancellationToken)
    {
        int h = component.HeightInBlocks;
        int w = component.WidthInBlocks;

        int restarts = 0;
        int restartsToGo = this.restartInterval;

        ref HuffmanLut acHuffmanTable = ref this.acHuffmanTables[component.AcTableId];

        for (int i = 0; i < h; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Span<Block8x8> blockSpan = component.SpectralBlocks.DangerousGetRowSpan(y: i);
            ref Block8x8 blockRef = ref MemoryMarshal.GetReference(blockSpan);

            for (nuint k = 0; k < (uint)w; k++)
            {
                if (this.restartInterval > 0 && restartsToGo == 0)
                {
                    this.FlushRemainingBytes();
                    this.WriteRestart(restarts % 8);
                }

                this.WriteAcBlock(
                    ref Unsafe.Add(ref blockRef, k),
                    start,
                    end,
                    ref acHuffmanTable);

                if (this.IsStreamFlushNeeded)
                {
                    this.FlushToStream();
                }

                if (this.restartInterval > 0)
                {
                    if (restartsToGo == 0)
                    {
                        restartsToGo = this.restartInterval;
                        restarts++;
                    }

                    restartsToGo--;
                }
            }
        }

        this.FlushRemainingBytes();
    }

    /// <summary>
    /// Encodes scan in baseline interleaved mode for any amount of component with arbitrary sampling factors.
    /// </summary>
    /// <param name="frame">Frame to encode.</param>
    /// <param name="converter">Converter from color to spectral.</param>
    /// <param name="cancellationToken">The token to request cancellation.</param>
    private void EncodeScanBaselineInterleaved<TPixel>(JpegFrame frame, SpectralConverter<TPixel> converter, CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        int mcu = 0;
        int mcusPerColumn = frame.McusPerColumn;
        int mcusPerLine = frame.McusPerLine;

        int restarts = 0;
        int restartsToGo = this.restartInterval;

        for (int j = 0; j < mcusPerColumn; j++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Convert from pixels to spectral via given converter
            converter.ConvertStrideBaseline();

            // Encode spectral to binary
            for (int i = 0; i < mcusPerLine; i++)
            {
                if (this.restartInterval > 0 && restartsToGo == 0)
                {
                    this.FlushRemainingBytes();
                    this.WriteRestart(restarts % 8);
                    foreach (Component component in frame.Components)
                    {
                        component.DcPredictor = 0;
                    }
                }

                // Scan an interleaved mcu... process components in order
                int mcuCol = mcu % mcusPerLine;
                for (int k = 0; k < frame.Components.Length; k++)
                {
                    Component component = frame.Components[k];

                    ref HuffmanLut dcHuffmanTable = ref this.dcHuffmanTables[component.DcTableId];
                    ref HuffmanLut acHuffmanTable = ref this.acHuffmanTables[component.AcTableId];

                    int h = component.HorizontalSamplingFactor;
                    int v = component.VerticalSamplingFactor;

                    nuint blockColBase = (uint)(mcuCol * h);

                    // Scan out an mcu's worth of this component; that's just determined
                    // by the basic H and V specified for the component
                    for (int y = 0; y < v; y++)
                    {
                        Span<Block8x8> blockSpan = component.SpectralBlocks.DangerousGetRowSpan(y);
                        ref Block8x8 blockRef = ref MemoryMarshal.GetReference(blockSpan);

                        for (nuint x = 0; x < (uint)h; x++)
                        {
                            nuint blockCol = blockColBase + x;

                            this.WriteBlock(
                                component,
                                ref Unsafe.Add(ref blockRef, blockCol),
                                ref dcHuffmanTable,
                                ref acHuffmanTable);
                        }
                    }
                }

                // After all interleaved components, that's an interleaved MCU
                mcu++;
                if (this.IsStreamFlushNeeded)
                {
                    this.FlushToStream();
                }

                if (this.restartInterval > 0)
                {
                    if (restartsToGo == 0)
                    {
                        restartsToGo = this.restartInterval;
                        restarts++;
                    }

                    restartsToGo--;
                }
            }
        }

        this.FlushRemainingBytes();
    }

    /// <summary>
    /// Encodes scan in baseline interleaved mode with exactly 3 components with no subsampling.
    /// </summary>
    /// <param name="frame">Frame to encode.</param>
    /// <param name="converter">Converter from color to spectral.</param>
    /// <param name="cancellationToken">The token to request cancellation.</param>
    private void EncodeThreeComponentBaselineInterleavedScanNoSubsampling<TPixel>(JpegFrame frame, SpectralConverter<TPixel> converter, CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        nuint mcusPerColumn = (uint)frame.McusPerColumn;
        nuint mcusPerLine = (uint)frame.McusPerLine;

        Component c2 = frame.Components[2];
        Component c1 = frame.Components[1];
        Component c0 = frame.Components[0];

        ref HuffmanLut c0dcHuffmanTable = ref this.dcHuffmanTables[c0.DcTableId];
        ref HuffmanLut c0acHuffmanTable = ref this.acHuffmanTables[c0.AcTableId];
        ref HuffmanLut c1dcHuffmanTable = ref this.dcHuffmanTables[c1.DcTableId];
        ref HuffmanLut c1acHuffmanTable = ref this.acHuffmanTables[c1.AcTableId];
        ref HuffmanLut c2dcHuffmanTable = ref this.dcHuffmanTables[c2.DcTableId];
        ref HuffmanLut c2acHuffmanTable = ref this.acHuffmanTables[c2.AcTableId];

        ref Block8x8 c0BlockRef = ref MemoryMarshal.GetReference(c0.SpectralBlocks.DangerousGetRowSpan(y: 0));
        ref Block8x8 c1BlockRef = ref MemoryMarshal.GetReference(c1.SpectralBlocks.DangerousGetRowSpan(y: 0));
        ref Block8x8 c2BlockRef = ref MemoryMarshal.GetReference(c2.SpectralBlocks.DangerousGetRowSpan(y: 0));

        for (nuint j = 0; j < mcusPerColumn; j++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Convert from pixels to spectral via given converter
            converter.ConvertStrideBaseline();

            // Encode spectral to binary
            for (nuint i = 0; i < mcusPerLine; i++)
            {
                this.WriteBlock(
                    c0,
                    ref Unsafe.Add(ref c0BlockRef, i),
                    ref c0dcHuffmanTable,
                    ref c0acHuffmanTable);

                this.WriteBlock(
                    c1,
                    ref Unsafe.Add(ref c1BlockRef, i),
                    ref c1dcHuffmanTable,
                    ref c1acHuffmanTable);

                this.WriteBlock(
                    c2,
                    ref Unsafe.Add(ref c2BlockRef, i),
                    ref c2dcHuffmanTable,
                    ref c2acHuffmanTable);

                if (this.IsStreamFlushNeeded)
                {
                    this.FlushToStream();
                }
            }
        }

        this.FlushRemainingBytes();
    }

    private void WriteDc(
        Component component,
        ref Block8x8 block,
        ref HuffmanLut dcTable)
    {
        // Emit the DC delta.
        int dc = block[0];
        this.EmitHuffRLE(dcTable.Values, 0, dc - component.DcPredictor);
        component.DcPredictor = dc;
    }

    private void WriteAcBlock(
        ref Block8x8 block,
        nint start,
        nint end,
        ref HuffmanLut acTable)
    {
        // Emit the AC components.
        int[] acHuffTable = acTable.Values;

        int runLength = 0;
        ref short blockRef = ref Unsafe.As<Block8x8, short>(ref block);
        for (nint zig = start; zig < end; zig++)
        {
            const int zeroRun1 = 1 << 4;
            const int zeroRun16 = 16 << 4;

            int ac = Unsafe.Add(ref blockRef, zig);
            if (ac == 0)
            {
                runLength += zeroRun1;
            }
            else
            {
                while (runLength >= zeroRun16)
                {
                    this.EmitHuff(acHuffTable, 0xf0);
                    runLength -= zeroRun16;
                }

                this.EmitHuffRLE(acHuffTable, runLength, ac);
                runLength = 0;
            }
        }

        // if mcu block contains trailing zeros - we must write end of block (EOB) value indicating that current block is over
        if (runLength > 0)
        {
            this.EmitHuff(acHuffTable, 0x00);
        }
    }

    private void WriteBlock(
        Component component,
        ref Block8x8 block,
        ref HuffmanLut dcTable,
        ref HuffmanLut acTable)
    {
        this.WriteDc(component, ref block, ref dcTable);
        this.WriteAcBlock(ref block, 1, 64, ref acTable);
    }

    private void WriteRestart(int restart) =>
        this.target.Write([0xff, (byte)(JpegConstants.Markers.RST0 + restart)], 0, 2);

    /// <summary>
    /// Emits the most significant count of bits to the buffer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Supports up to 32 count of bits but, generally speaking, jpeg
    /// standard assures that there won't be more than 16 bits per single
    /// value.
    /// </para>
    /// <para>
    /// Emitting algorithm uses 3 intermediate buffers for caching before
    /// writing to the stream:
    /// <list type="number">
    /// <item>
    /// <term>uint32</term>
    /// <description>
    /// Bit buffer. Encoded spectral values can occupy up to 16 bits, bits
    /// are assembled to whole bytes via this intermediate buffer.
    /// </description>
    /// </item>
    /// <item>
    /// <term>uint32[]</term>
    /// <description>
    /// Assembled bytes from uint32 buffer are saved into this buffer.
    /// uint32 buffer values are saved using indices from the last to the first.
    /// As bytes are saved to the memory as 4-byte packages endianness matters:
    /// Jpeg stream is big-endian, indexing buffer bytes from the last index to the
    /// first eliminates all operations to extract separate bytes. This only works for
    /// little-endian machines (there are no known examples of big-endian users atm).
    /// For big-endians this approach is slower due to the separate byte extraction.
    /// </description>
    /// </item>
    /// <item>
    /// <term>byte[]</term>
    /// <description>
    /// Byte buffer used only during <see cref="FlushToStream(int)"/> method.
    /// </description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <param name="bits">Bits to emit, must be shifted to the left.</param>
    /// <param name="count">Bits count stored in the bits parameter.</param>
    [MethodImpl(InliningOptions.ShortMethod)]
    private void Emit(uint bits, int count)
    {
        this.accumulatedBits |= bits >> this.bitCount;

        count += this.bitCount;

        if (count >= 32)
        {
            this.emitBuffer[--this.emitWriteIndex] = this.accumulatedBits;
            this.accumulatedBits = bits << (32 - this.bitCount);

            count -= 32;
        }

        this.bitCount = count;
    }

    /// <summary>
    /// Emits the given value with the given Huffman table.
    /// </summary>
    /// <param name="table">Huffman table.</param>
    /// <param name="value">Value to encode.</param>
    [MethodImpl(InliningOptions.ShortMethod)]
    private void EmitHuff(int[] table, int value)
    {
        int x = table[value];
        this.Emit((uint)x & 0xffff_ff00u, x & 0xff);
    }

    /// <summary>
    /// Emits given value via huffman rle encoding.
    /// </summary>
    /// <param name="table">Huffman table.</param>
    /// <param name="runLength">The number of preceding zeroes, preshifted by 4 to the left.</param>
    /// <param name="value">Value to encode.</param>
    [MethodImpl(InliningOptions.ShortMethod)]
    private void EmitHuffRLE(int[] table, int runLength, int value)
    {
        DebugGuard.IsTrue((runLength & 0xf) == 0, $"{nameof(runLength)} parameter must be shifted to the left by 4 bits");

        int a = value;
        int b = value;
        if (a < 0)
        {
            a = -value;
            b = value - 1;
        }

        int valueLen = GetHuffmanEncodingLength((uint)a);

        // Huffman prefix code
        int huffPackage = table[runLength | valueLen];
        int prefixLen = huffPackage & 0xff;
        uint prefix = (uint)huffPackage & 0xffff_0000u;

        // Actual encoded value
        uint encodedValue = (uint)b << (32 - valueLen);

        // Doing two binary shifts to get rid of leading 1's in negative value case
        this.Emit(prefix | (encodedValue >> prefixLen), prefixLen + valueLen);
    }

    /// <summary>
    /// Calculates how many minimum bits needed to store given value for Huffman jpeg encoding.
    /// </summary>
    /// <remarks>
    /// This is an internal operation supposed to be used only in <see cref="HuffmanScanEncoder"/> class for jpeg encoding.
    /// </remarks>
    /// <param name="value">The value.</param>
    [MethodImpl(InliningOptions.ShortMethod)]
    internal static int GetHuffmanEncodingLength(uint value)
    {
        DebugGuard.IsTrue(value <= (1 << 16), "Huffman encoder is supposed to encode a value of 16bit size max");

        // This should have been implemented as (BitOperations.Log2(value) + 1) as in non-intrinsic implementation
        // But internal log2 is implemented like this: (31 - (int)Lzcnt.LeadingZeroCount(value))

        // BitOperations.Log2 implementation also checks if input value is zero for the convention 0->0
        // Lzcnt would return 32 for input value of 0 - no need to check that with branching
        // Fallback code if Lzcnt is not supported still use if-check
        // But most modern CPUs support this instruction so this should not be a problem
        return 32 - BitOperations.LeadingZeroCount(value);
    }

    /// <summary>
    /// General method for flushing cached spectral data bytes to
    /// the ouput stream respecting stuff bytes.
    /// </summary>
    /// <remarks>
    /// Bytes cached via <see cref="Emit"/> are stored in 4-bytes blocks
    /// which makes this method endianness dependent.
    /// </remarks>
    [MethodImpl(InliningOptions.ShortMethod)]
    private void FlushToStream(int endIndex)
    {
        Span<byte> emitBytes = MemoryMarshal.AsBytes(this.emitBuffer.AsSpan());

        int writeIdx = 0;
        int startIndex = emitBytes.Length - 1;

        // Some platforms may fail to eliminate this if-else branching
        // Even if it happens - buffer is flushed in big packs,
        // branching overhead shouldn't be noticeable
        if (BitConverter.IsLittleEndian)
        {
            // For little endian case bytes are ordered and can be
            // safely written to the stream with stuff bytes
            // First byte is cached on the most significant index
            // so we are going from the end of the array to its beginning:
            // ... [  double word #1   ] [  double word #0   ]
            // ... [idx3|idx2|idx1|idx0] [idx3|idx2|idx1|idx0]
            for (int i = startIndex; i >= endIndex; i--)
            {
                byte value = emitBytes[i];
                this.streamWriteBuffer[writeIdx++] = value;

                // Inserting stuff byte
                if (value == 0xff)
                {
                    this.streamWriteBuffer[writeIdx++] = 0x00;
                }
            }
        }
        else
        {
            // For big endian case bytes are ordered in 4-byte packs
            // which are ordered like bytes in the little endian case by in 4-byte packs:
            // ... [  double word #1   ] [  double word #0   ]
            // ... [idx0|idx1|idx2|idx3] [idx0|idx1|idx2|idx3]
            // So we must write each 4-bytes in 'natural order'
            for (int i = startIndex; i >= endIndex; i -= 4)
            {
                // This loop is caused by the nature of underlying byte buffer
                // implementation and indeed causes performace by somewhat 5%
                // compared to little endian scenario
                // Even with this performance drop this cached buffer implementation
                // is faster than individually writing bytes using binary shifts and binary and(s)
                for (int j = i - 3; j <= i; j++)
                {
                    byte value = emitBytes[j];
                    this.streamWriteBuffer[writeIdx++] = value;

                    // Inserting stuff byte
                    if (value == 0xff)
                    {
                        this.streamWriteBuffer[writeIdx++] = 0x00;
                    }
                }
            }
        }

        this.target.Write(this.streamWriteBuffer, 0, writeIdx);
        this.emitWriteIndex = this.emitBuffer.Length;
    }

    /// <summary>
    /// Flushes spectral data bytes after encoding all channel blocks
    /// in a single jpeg macroblock using <see cref="WriteBlock(Component, ref Block8x8, ref HuffmanLut, ref HuffmanLut)"/>.
    /// </summary>
    /// <remarks>
    /// This must be called only if <see cref="IsStreamFlushNeeded"/> is true
    /// only during the macroblocks encoding routine.
    /// </remarks>
    private void FlushToStream() =>
        this.FlushToStream(this.emitWriteIndex * 4);

    /// <summary>
    /// Flushes final cached bits to the stream padding 1's to
    /// complement full bytes.
    /// </summary>
    /// <remarks>
    /// This must be called only once at the end of the encoding routine.
    /// <see cref="IsStreamFlushNeeded"/> check is not needed.
    /// </remarks>
    [MethodImpl(InliningOptions.ShortMethod)]
    private void FlushRemainingBytes()
    {
        // Padding all 4 bytes with 1's while not corrupting initial bits stored in accumulatedBits
        // And writing only valuable count of bytes count we want to write to the output stream
        int valuableBytesCount = (int)Numerics.DivideCeil((uint)this.bitCount, 8);
        uint packedBytes = this.accumulatedBits | (uint.MaxValue >> this.bitCount);
        this.emitBuffer[this.emitWriteIndex - 1] = packedBytes;

        // Flush cached bytes to the output stream with padding bits
        int lastByteIndex = (this.emitWriteIndex * 4) - valuableBytesCount;
        this.FlushToStream(lastByteIndex);

        // Clear huffman register
        // This is needed for for images with multiples scans
        this.bitCount = 0;
        this.accumulatedBits = 0;
    }
}

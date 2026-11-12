// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
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
    /// <see cref="MaxBytesPerBlockMultiplier"/> as a safety margin.
    /// </remarks>
    private const int MaxBytesPerBlock = (16 + 10) * 64 / 8 * MaxBytesPerBlockMultiplier;

    /// <summary>
    /// Safety margin for <see cref="MaxBytesPerBlock"/>. The DC difference can have 11 value bits.
    /// </summary>
    private const int MaxBytesPerBlockMultiplier = 2;

    /// <summary>
    /// The number of emitted bytes after which the encoder writes the emit buffer to the output stream.
    /// </summary>
    /// <remarks>
    /// A large value makes stream writes and flush calls rare. The emit buffer holds this number of bytes,
    /// plus space for one more coding unit, plus the 8 bytes that each code store writes.
    /// </remarks>
    private const int FlushThreshold = 16 * 1024;

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
    /// The pending bits that do not make a complete byte yet, left-aligned at bit 63.
    /// </summary>
    private ulong accumulatedBits;

    /// <summary>
    /// The emitted bytes in stream order, without stuff bytes.
    /// </summary>
    /// <remarks>
    /// Each code store writes 8 bytes at <see cref="emitIndex"/>. Only the complete bytes advance the index.
    /// </remarks>
    private readonly byte[] emitBuffer;

    /// <summary>
    /// Buffer for temporal storage which is then written to the output stream.
    /// </summary>
    /// <remarks>
    /// Encoding bits from <see cref="emitBuffer"/> are copied to this byte buffer including stuff bytes.
    /// </remarks>
    private readonly byte[] streamWriteBuffer;

    private readonly int restartInterval;

    /// <summary>
    /// The number of pending bits in <see cref="accumulatedBits"/>: 0 to 7.
    /// </summary>
    private int bitCount;

    /// <summary>
    /// The number of complete bytes in <see cref="emitBuffer"/>.
    /// </summary>
    private int emitIndex;

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
        // The encoder flushes after a coding unit when emitIndex reaches the threshold.
        // Thus, the buffer holds the threshold, one more coding unit, and the 8 bytes of the last code store.
        this.emitBuffer = new byte[FlushThreshold + (MaxBytesPerBlock * blocksPerCodingUnit) + sizeof(ulong)];

        this.restartInterval = restartInterval;

        // The stuffing loop stores full vectors. The extra vector of space keeps the last store inside the buffer.
        this.streamWriteBuffer = new byte[(this.emitBuffer.Length * OutputBufferLengthMultiplier) + Vector512<byte>.Count];

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
        get => this.emitIndex >= FlushThreshold;
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
    public void EncodeScanBaselineInterleaved<TPixel>(
        JpegColorType color,
        JpegFrame frame,
        SpectralConverter<TPixel> converter,
        CancellationToken cancellationToken)
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

                // This loop encodes one interleaved MCU. It reads the components in scan order. The MCU column is i.
                for (int k = 0; k < frame.Components.Length; k++)
                {
                    Component component = frame.Components[k];

                    ref HuffmanLut dcHuffmanTable = ref this.dcHuffmanTables[component.DcTableId];
                    ref HuffmanLut acHuffmanTable = ref this.acHuffmanTables[component.AcTableId];

                    int h = component.HorizontalSamplingFactor;
                    int v = component.VerticalSamplingFactor;

                    nuint blockColBase = (uint)(i * h);

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
    private void EncodeThreeComponentBaselineInterleavedScanNoSubsampling<TPixel>(
        JpegFrame frame,
        SpectralConverter<TPixel> converter,
        CancellationToken cancellationToken)
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
        BitWriter writer = new(this);
        int dc = block[0];
        writer.EmitHuffRLE(dcTable.Values, 0, dc - component.DcPredictor);
        component.DcPredictor = dc;
        writer.Store(this);
    }

    private void WriteAcBlock(
        ref Block8x8 block,
        nint start,
        nint end,
        ref HuffmanLut acTable)
    {
        BitWriter writer = new(this);
        WriteAc(ref writer, ref block, start, end, acTable.Values);
        writer.Store(this);
    }

    private void WriteBlock(
        Component component,
        ref Block8x8 block,
        ref HuffmanLut dcTable,
        ref HuffmanLut acTable)
    {
        // One writer covers the full block, so the bit state stays in registers for all of its codes.
        BitWriter writer = new(this);
        int dc = block[0];
        writer.EmitHuffRLE(dcTable.Values, 0, dc - component.DcPredictor);
        component.DcPredictor = dc;
        WriteAc(ref writer, ref block, 1, 64, acTable.Values);
        writer.Store(this);
    }

    /// <summary>
    /// Emits the AC coefficients <paramref name="start"/> to <paramref name="end"/> - 1 of a block in zig-zag order.
    /// </summary>
    /// <param name="writer">The bit writer.</param>
    /// <param name="block">The quantized coefficients in zig-zag order.</param>
    /// <param name="start">The first coefficient: 1 to 63.</param>
    /// <param name="end">One more than the last coefficient: <paramref name="start"/> + 1 to 64.</param>
    /// <param name="acHuffTable">The AC Huffman table.</param>
    [MethodImpl(InliningOptions.ShortMethod)]
    private static void WriteAc(ref BitWriter writer, ref Block8x8 block, nint start, nint end, int[] acHuffTable)
    {
        // Bit k of the mask is 1 when coefficient k is not zero. The two shifts keep only start to end - 1.
        // The loop visits only the non-zero coefficients. A loop over all 63 coefficients has a branch
        // for each zero test, and that branch changes with the image data.
        ulong mask = block.GetNonZeroMask() & (ulong.MaxValue << (int)start) & (ulong.MaxValue >> (int)(64 - end));
        ref short coefficients = ref Unsafe.As<Block8x8, short>(ref block);

        nint last = start - 1;
        while (mask != 0)
        {
            nint k = BitOperations.TrailingZeroCount(mask);

            // The run is the number of zero coefficients before coefficient k. Each 16 zeros give one ZRL code (0xF0).
            nint run = k - last - 1;
            while (run >= 16)
            {
                writer.EmitHuff(acHuffTable, 0xf0);
                run -= 16;
            }

            writer.EmitHuffRLE(acHuffTable, (int)run << 4, Unsafe.Add(ref coefficients, k));
            last = k;

            // This clears the lowest set bit.
            mask &= mask - 1;
        }

        // Zero coefficients after the last non-zero coefficient end with the end-of-block code (EOB, 0x00).
        if (last < end - 1)
        {
            writer.EmitHuff(acHuffTable, 0x00);
        }
    }

    private void WriteRestart(int restart) =>
        this.target.Write([0xff, (byte)(JpegConstants.Markers.RST0 + restart)], 0, 2);

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
    /// Writes the complete emitted bytes to the output stream, with a stuff byte after each 0xFF byte.
    /// </summary>
    /// <remarks>
    /// The pending bits stay in <see cref="accumulatedBits"/>. The next code store writes them again at the start of the buffer.
    /// </remarks>
    private void FlushToStream()
    {
        int written = StuffBytes(this.emitBuffer.AsSpan(0, this.emitIndex), this.streamWriteBuffer);
        this.target.Write(this.streamWriteBuffer, 0, written);
        this.emitIndex = 0;
    }

    /// <summary>
    /// Flushes final cached bits to the stream padding 1's to
    /// complement full bytes.
    /// </summary>
    /// <remarks>
    /// This must be called only once at the end of the encoding routine.
    /// <see cref="IsStreamFlushNeeded"/> check is not needed.
    /// </remarks>
    private void FlushRemainingBytes()
    {
        // The writer pads the last byte with 1 bits (ITU T.81, F.1.2.3). Then it is complete, and the flush writes it.
        if (this.bitCount > 0)
        {
            BitWriter writer = new(this);
            int padding = 8 - this.bitCount;
            writer.Emit(uint.MaxValue << (32 - padding), padding);
            writer.Store(this);
        }

        this.FlushToStream();

        // Clear huffman register
        // This is needed for for images with multiples scans
        this.bitCount = 0;
        this.accumulatedBits = 0;
    }

    /// <summary>
    /// Copies entropy-coded bytes and puts a 0x00 stuff byte after each 0xFF byte (ITU T.81, B.1.1.5).
    /// </summary>
    /// <param name="source">The emitted bytes.</param>
    /// <param name="destination">
    /// The destination. It has space for at least two times the source length, plus one <see cref="Vector512{T}"/> of bytes.
    /// </param>
    /// <returns>The number of bytes written to <paramref name="destination"/>.</returns>
    private static int StuffBytes(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        ref byte src = ref MemoryMarshal.GetReference(source);
        ref byte dst = ref MemoryMarshal.GetReference(destination);
        nint length = source.Length;
        nint i = 0;
        nint w = 0;

        // Each step stores the full vector, then finds the first 0xFF byte. If the vector has no 0xFF byte, the step is complete.
        // Otherwise, the step keeps the bytes up to and including the 0xFF byte. Then it adds the stuff byte.
        // The next step starts after the 0xFF byte and writes the other bytes again.
        if (Vector512.IsHardwareAccelerated)
        {
            Vector512<byte> marker = Vector512.Create((byte)0xff);
            while (i <= length - Vector512<byte>.Count)
            {
                Vector512<byte> bytes = Vector512.LoadUnsafe(ref src, (nuint)i);
                bytes.StoreUnsafe(ref dst, (nuint)w);
                ulong hits = Vector512.Equals(bytes, marker).ExtractMostSignificantBits();
                if (hits == 0)
                {
                    i += Vector512<byte>.Count;
                    w += Vector512<byte>.Count;
                    continue;
                }

                nint kept = BitOperations.TrailingZeroCount(hits) + 1;
                i += kept;
                w += kept;
                Unsafe.Add(ref dst, w++) = 0x00;
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            Vector256<byte> marker = Vector256.Create((byte)0xff);
            while (i <= length - Vector256<byte>.Count)
            {
                Vector256<byte> bytes = Vector256.LoadUnsafe(ref src, (nuint)i);
                bytes.StoreUnsafe(ref dst, (nuint)w);
                uint hits = Vector256.Equals(bytes, marker).ExtractMostSignificantBits();
                if (hits == 0)
                {
                    i += Vector256<byte>.Count;
                    w += Vector256<byte>.Count;
                    continue;
                }

                nint kept = BitOperations.TrailingZeroCount(hits) + 1;
                i += kept;
                w += kept;
                Unsafe.Add(ref dst, w++) = 0x00;
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<byte> marker = Vector128.Create((byte)0xff);
            while (i <= length - Vector128<byte>.Count)
            {
                Vector128<byte> bytes = Vector128.LoadUnsafe(ref src, (nuint)i);
                bytes.StoreUnsafe(ref dst, (nuint)w);
                uint hits = Vector128.Equals(bytes, marker).ExtractMostSignificantBits();
                if (hits == 0)
                {
                    i += Vector128<byte>.Count;
                    w += Vector128<byte>.Count;
                    continue;
                }

                nint kept = BitOperations.TrailingZeroCount(hits) + 1;
                i += kept;
                w += kept;
                Unsafe.Add(ref dst, w++) = 0x00;
            }
        }

        for (; i < length; i++)
        {
            byte value = Unsafe.Add(ref src, i);
            Unsafe.Add(ref dst, w++) = value;
            if (value == 0xff)
            {
                Unsafe.Add(ref dst, w++) = 0x00;
            }
        }

        return (int)w;
    }

    /// <summary>
    /// Writes Huffman codes to the emit buffer of the encoder.
    /// </summary>
    /// <remarks>
    /// Each write method copies the bit state of the encoder to this struct. At the end, it writes the state back.
    /// Thus, the state stays in registers for all codes of the block, not in fields of the encoder object.
    /// </remarks>
    private ref struct BitWriter
    {
        private readonly ref byte buffer;
        private nint position;
        private ulong bits;
        private int count;

        /// <summary>
        /// Initializes a new instance of the <see cref="BitWriter"/> struct with the bit state of the encoder.
        /// </summary>
        /// <param name="encoder">The encoder.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BitWriter(HuffmanScanEncoder encoder)
        {
            this.buffer = ref MemoryMarshal.GetArrayDataReference(encoder.emitBuffer);
            this.position = encoder.emitIndex;
            this.bits = encoder.accumulatedBits;
            this.count = encoder.bitCount;
        }

        /// <summary>
        /// Writes the bit state back to the encoder.
        /// </summary>
        /// <param name="encoder">The encoder.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void Store(HuffmanScanEncoder encoder)
        {
            encoder.emitIndex = (int)this.position;
            encoder.accumulatedBits = this.bits;
            encoder.bitCount = this.count;
        }

        /// <summary>
        /// Emits the most significant <paramref name="length"/> bits of <paramref name="code"/>.
        /// </summary>
        /// <param name="code">The bits, left-aligned at bit 31.</param>
        /// <param name="length">The number of bits: 1 to 32.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Emit(uint code, int length)
        {
            // Fewer than 8 bits are pending, left-aligned at bit 63. The code goes directly below them.
            // At most 7 + 32 bits are then pending, so they fit in 64 bits.
            this.bits |= ((ulong)code << 32) >> this.count;
            this.count += length;

            // The writer stores all 8 bytes in stream order. The complete bytes are final. The next store writes the bytes
            // after them again, and the emit buffer has space for them. Thus, the store has no branch on the byte count.
            ulong value = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(this.bits) : this.bits;
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref this.buffer, this.position), value);

            // The writer removes the complete bytes. There are at most 4, so the shift is at most 32.
            int bytes = this.count >> 3;
            this.position += bytes;
            this.bits <<= bytes << 3;
            this.count &= 7;
        }

        /// <summary>
        /// Emits the given value with the given Huffman table.
        /// </summary>
        /// <param name="table">Huffman table.</param>
        /// <param name="value">Value to encode.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EmitHuff(int[] table, int value)
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EmitHuffRLE(int[] table, int runLength, int value)
        {
            DebugGuard.IsTrue((runLength & 0xf) == 0, $"{nameof(runLength)} parameter must be shifted to the left by 4 bits");

            // The sign of a coefficient changes with the image data, so a branch on it is often mispredicted.
            // sign is 0 for zero or a positive value, and -1 for a negative value. Then a is the magnitude, and b is the
            // value bits: the value for a positive value and value - 1 (the one's complement of the magnitude) otherwise.
            int sign = value >> 31;
            int a = (value ^ sign) - sign;
            int b = value + sign;

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
    }
}

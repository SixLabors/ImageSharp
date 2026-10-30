// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Jxl.IO.FrameHeader;
using SixLabors.ImageSharp.Formats.Jxl.Memory;
using SixLabors.ImageSharp.Formats.Jxl.Processing.AcStrategy;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Dct;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder.Ans;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder.Group.BlockLoader;

/// <summary>
/// Decodes coefficient blocks and owns the entropy readers for one group.
/// </summary>
internal ref struct JxlBitStreamBlockLoader : IJxlGetBlock, IDisposable
{
    private readonly JxlBlockContextMap blockCtxMap;

    /// <summary>
    /// The complete history borrowed once for this group, ordered by local pass, color channel and block row.
    /// </summary>
    private readonly Span<int> nonZeroCounts;

    /// <summary>
    /// The padded sample count between adjacent color-channel histories.
    /// </summary>
    private readonly int nonZeroChannelStride;
    private readonly ReadOnlySpan<int> rawQuantField;
    private readonly ReadOnlySpan<byte> quantDc;
    private readonly int quantFieldStride;
    private readonly int quantDcStride;
    private readonly JxlAnsSymbolReader[] decoders;
    private readonly JxlBitReader[] readers;
    private readonly List<JxlAnsCode> codes;
    private readonly ReadOnlySpan<int> coeffOrders;
    private readonly List<List<byte>> contextMaps;
    private readonly uint[] shiftForPass;
    private readonly int coeffOrderSize;
    private readonly int firstPass;
    private readonly int[] ctxOffset;
    private readonly int nzerosStride;
    private readonly Rectangle rect;
    private InlineArray3<int> hshift;
    private InlineArray3<int> vshift;
    private ReadOnlySpan<int> qfRow;
    private ReadOnlySpan<byte> quantDcRow;
    private int initializedDecoders;

    /// <summary>
    /// Initializes a new instance of the <see cref="JxlBitStreamBlockLoader"/> class.
    /// </summary>
    /// <param name="frameHeader">The parsed frame settings and pass shifts.</param>
    /// <param name="decoderState">The owner of coefficient orders, quantization planes, and entropy tables.</param>
    /// <param name="groupDecCache">The owner of the group's coefficient workspace.</param>
    /// <param name="readers">The borrowed bit reader for each requested pass.</param>
    /// <param name="firstPass">The first requested frame pass.</param>
    /// <param name="numPasses">The number of requested passes.</param>
    /// <param name="rect">The group's rectangle in the frame's block grid.</param>
    public JxlBitStreamBlockLoader(
        JxlFrameHeader frameHeader,
        JxlPassesDecoderContext decoderState,
        JxlGroupDecoderCache groupDecCache,
        JxlBitReader[] readers,
        int firstPass,
        int numPasses,
        Rectangle rect)
    {
        this.blockCtxMap = decoderState.Shared.BlockContextMap;
        this.nonZeroCounts = groupDecCache.NumberOfNonZeroes.GetRow(0);
        this.nonZeroChannelStride = groupDecCache.NonZeroChannelStride;
        this.rawQuantField = decoderState.Shared.RawQuantField.GetRow(0);
        this.quantDc = decoderState.Shared.QuantDc.GetRow(0);
        this.quantFieldStride = decoderState.Shared.RawQuantField.PixelsPerRow;
        this.quantDcStride = decoderState.Shared.QuantDc.PixelsPerRow;
        this.codes = decoderState.Code;
        this.coeffOrders = decoderState.Shared.CoeffOrders.Span;
        this.contextMaps = decoderState.ContextMap;
        this.shiftForPass = frameHeader.Passes.Shift;
        this.coeffOrderSize = decoderState.Shared.CoeffOrderSize;
        this.firstPass = firstPass;
        this.rect = rect;
        this.readers = readers;
        this.decoders = new JxlAnsSymbolReader[numPasses];
        this.ctxOffset = new int[numPasses];
        this.nzerosStride = groupDecCache.NumberOfNonZeroes.PixelsPerRow;

        // Frame tables and planes outlive this group. Borrow them instead of copying the coefficient orders
        // or allocating pass-shift storage. Only the entropy readers and their optional histories belong here.
        JxlYCbCrChromaSubsampling subsampling = frameHeader.ChromaSubsampling;
        for (int c = 0; c < 3; c++)
        {
            this.hshift[c] = subsampling.HShift(c);
            this.vshift[c] = subsampling.VShift(c);
        }
    }

    /// <summary>
    /// Gets the entropy readers after successful initialization.
    /// </summary>
    public ReadOnlySpan<JxlAnsSymbolReader> Decoders => this.decoders;

    /// <inheritdoc/>
    public void StartRow(int by)
    {
        // Quantization planes use frame coordinates; coefficient positions inside the group remain local.
        this.qfRow = this.rawQuantField[(((this.rect.Y + by) * this.quantFieldStride) + this.rect.X)..];
        this.quantDcRow = this.quantDc[(((this.rect.Y + by) * this.quantDcStride) + this.rect.X)..];
    }

    /// <inheritdoc/>
    public bool TryLoadBlock(
        int bx,
        int by,
        JxlAcStrategy acs,
        int size,
        int log2CoveredBlocks,
        scoped JxlDctAcPointer block0,
        scoped JxlDctAcPointer block1,
        scoped JxlDctAcPointer block2,
        JxlDctAcType acType)
    {
        // Quantization rows and frame coefficient orders stay fixed while this block's passes are decoded.
        ReadOnlySpan<byte> quantDcRow = this.quantDcRow;
        ReadOnlySpan<int> qfRow = this.qfRow;
        ReadOnlySpan<int> orders = this.coeffOrders;

        // Y is channel 1. Its nonzero counts must be decoded before X and B, as in the native block traversal.
        ReadOnlySpan<int> channelOrder = [1, 0, 2];
        foreach (int c in channelOrder)
        {
            int sbx = bx >> this.hshift[c];
            int sby = by >> this.vshift[c];

            if ((sbx << this.hshift[c]) != bx || (sby << this.vshift[c]) != by)
            {
                continue;
            }

            int historyRowOffset = sby * this.nzerosStride;

            for (int pass = 0; pass < this.decoders.Length; pass++)
            {
                int framePass = this.firstPass + pass;

                // History uses local pass indices, while coefficient orders and shifts use frame pass indices.
                // The current view includes later rows because one transform can write several covered block rows.
                int historyChannelOffset = ((pass * 3) + c) * this.nonZeroChannelStride;
                Span<int> rowNzeros = this.nonZeroCounts.Slice(
                    historyChannelOffset + historyRowOffset,
                    this.nonZeroChannelStride - historyRowOffset);

                ReadOnlySpan<int> rowNzerosTop = sby == 0
                    ? default
                    : this.nonZeroCounts.Slice(historyChannelOffset + historyRowOffset - this.nzerosStride, this.nzerosStride);

                JxlDctAcPointer block = c switch
                {
                    0 => block0,
                    1 => block1,
                    _ => block2
                };

                if (!JxlGroupDecoder.DecodeAcVarBlock(
                    acType,
                    this.decoders[pass].UsesLz77,
                    this.ctxOffset[pass],
                    log2CoveredBlocks,
                    rowNzeros,
                    rowNzerosTop,
                    this.nzerosStride,
                    c,
                    sbx,
                    bx,
                    acs,
                    orders[(framePass * this.coeffOrderSize)..],
                    this.readers[pass],
                    this.decoders[pass],
                    CollectionsMarshal.AsSpan(this.contextMaps[framePass]),
                    quantDcRow,
                    qfRow,
                    this.blockCtxMap,
                    block,
                    (int)this.shiftForPass[framePass]))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Reads each pass's histogram selector and creates its allocator-owned entropy history.
    /// </summary>
    /// <param name="configuration">The configuration whose allocator owns the histories.</param>
    /// <param name="histogramSelectorBits">The bit count used to select a histogram set.</param>
    /// <param name="numHistograms">The number of histogram sets declared by the frame.</param>
    /// <returns>Whether every encoded histogram selector is valid.</returns>
    public bool Init(Configuration configuration, int histogramSelectorBits, int numHistograms)
    {
        for (int pass = 0; pass < this.decoders.Length; pass++)
        {
            int histogram = histogramSelectorBits == 0 ? 0 : (int)this.readers[pass].ReadBits32((uint)histogramSelectorBits);
            if (histogram >= numHistograms)
            {
                return false;
            }

            this.ctxOffset[pass] = histogram * this.blockCtxMap.AcContextCount;

            // VarDCT uses direct distances, not the modular image-row distance codebook.
            // Count each completed reader so disposal also releases histories after a later pass fails.
            this.decoders[pass] = JxlAnsSymbolReader.Create(configuration, this.codes[this.firstPass + pass], this.readers[pass], 0);
            this.initializedDecoders++;
        }

        return true;
    }

    /// <summary>
    /// Releases the initialized entropy histories. Borrowed frame tables, planes, and bit readers remain owned by their callers.
    /// </summary>
    public void Dispose()
    {
        for (int pass = 0; pass < this.initializedDecoders; pass++)
        {
            this.decoders[pass].Dispose();
        }

        this.initializedDecoders = 0;
    }
}

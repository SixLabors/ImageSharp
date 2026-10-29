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
internal sealed class JxlBitStreamBlockLoader : IJxlGetBlock, IDisposable
{
    private readonly JxlGroupDecoderCache groupDecCache;
    private readonly JxlBlockContextMap blockCtxMap;
    private readonly JxlPlane<int> rawQuantField;
    private readonly JxlPlane<byte> quantDc;
    private readonly JxlAnsSymbolReader[] decoders;
    private readonly JxlBitReader[] readers;
    private readonly List<JxlAnsCode> codes;
    private readonly List<int> coeffOrders;
    private readonly List<List<byte>> contextMaps;
    private readonly uint[] shiftForPass;
    private readonly int coeffOrderSize;
    private readonly int firstPass;
    private readonly int[] ctxOffset;
    private readonly int nzerosStride;
    private readonly Rectangle rect;
    private InlineArray3<int> hshift;
    private InlineArray3<int> vshift;
    private Memory<int> qfRow;
    private Memory<byte> quantDcRow;
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
        JxlPassesDecoderState decoderState,
        JxlGroupDecoderCache groupDecCache,
        JxlBitReader[] readers,
        int firstPass,
        int numPasses,
        Rectangle rect)
    {
        this.groupDecCache = groupDecCache;
        this.blockCtxMap = decoderState.Shared.BlockContextMap;
        this.rawQuantField = decoderState.Shared.RawQuantField;
        this.quantDc = decoderState.Shared.QuantDc;
        this.codes = decoderState.Code;
        this.coeffOrders = decoderState.Shared.CoeffOrders;
        this.contextMaps = decoderState.ContextMap;
        this.shiftForPass = frameHeader.Passes.Shift;
        this.coeffOrderSize = decoderState.Shared.CoeffOrderSize;
        this.firstPass = firstPass;
        this.rect = rect;
        this.readers = readers;
        this.decoders = new JxlAnsSymbolReader[numPasses];
        this.ctxOffset = new int[numPasses];
        this.nzerosStride = groupDecCache.NumberOfNonZeroes[0].Plane(0).PixelsPerRow;

        // Frame tables and planes outlive this group. Borrow them instead of copying the coefficient orders
        // or allocating pass-shift storage. Only the entropy readers and their optional histories belong here.
        JxlYCbCrChromaSubsampling subsampling = frameHeader.ChromaSubsampling!;
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
        this.qfRow = this.rawQuantField.GetRowMemory(this.rect.Y + by)[this.rect.X..];
        this.quantDcRow = this.quantDc.GetRowMemory(this.rect.Y + by)[this.rect.X..];
    }

    /// <inheritdoc/>
    public bool TryLoadBlock(
        int bx,
        int by,
        JxlAcStrategy acs,
        int size,
        int log2CoveredBlocks,
        JxlDctAcPointer block0,
        JxlDctAcPointer block1,
        JxlDctAcPointer block2,
        JxlDctAcType acType)
    {
        // Quantization rows and frame coefficient orders stay fixed while this block's passes are decoded.
        ReadOnlySpan<byte> quantDcRow = this.quantDcRow.Span;
        ReadOnlySpan<int> qfRow = this.qfRow.Span;
        ReadOnlySpan<int> orders = CollectionsMarshal.AsSpan(this.coeffOrders);

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

            for (int pass = 0; pass < this.decoders.Length; pass++)
            {
                int framePass = this.firstPass + pass;
                Span<int> rowNzeros = this.groupDecCache.NumberOfNonZeroes[pass].PlaneRow(c, sby);
                ReadOnlySpan<int> rowNzerosTop = sby == 0 ? default : this.groupDecCache.NumberOfNonZeroes[pass].PlaneRow(c, sby - 1);
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

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Jxl.IO.FrameHeader;
using SixLabors.ImageSharp.Formats.Jxl.Memory.ImageTypes;
using SixLabors.ImageSharp.Formats.Jxl.Processing.AcStrategy;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Dct;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder.Ans;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder.Group.BlockLoader;

/// <summary>
/// Loads coefficient blocks from the JPEG XL bitstream.
/// </summary>
internal sealed class JxlBitStreamBlockLoader : IJxlGetBlock
{
    private readonly JxlGroupDecoderCache groupDecCache;
    private readonly JxlBlockContextMap blockCtxMap;
    private readonly JxlImageI rawQuantField;
    private readonly JxlImageB quantDc;
    private readonly JxlAnsSymbolReader[] decoders;
    private readonly JxlBitReader[] readers;

    private IMemoryOwner<uint>? shiftForPass;
    private byte[]? coeffOrders;
    private List<byte>[]? contextMaps;

    private int coeffOrderSize;
    private int numPasses;
    private int[] ctxOffset;
    private int nzerosStride;

    private InlineArray3<int> hshift;
    private InlineArray3<int> vshift;

    private int currentBy;
    private Memory<int> qfRow;
    private Memory<byte> quantDcRow;

    private Rectangle rect;

    // 11 because JxlShared.MaximumNumberOfPasses = 11
    private InlineArray3<InlineArray11<Memory<int>>> rowNZeroes;
    private InlineArray3<InlineArray11<Memory<int>>> rowNZeroesTop;

    public JxlBitStreamBlockLoader(
        JxlGroupDecoderCache groupDecCache,
        JxlBlockContextMap blockCtxMap,
        JxlImageI rawQuantField,
        JxlImageB quantDc,
        JxlAnsSymbolReader[] decoders,
        JxlBitReader[] readers)
    {
        this.groupDecCache = groupDecCache;
        this.blockCtxMap = blockCtxMap;
        this.rawQuantField = rawQuantField;
        this.quantDc = quantDc;
        this.decoders = decoders;
        this.readers = readers;
        this.shiftForPass = null;
        this.coeffOrders = null;
        this.coeffOrderSize = 0;
        this.numPasses = 0;
        this.ctxOffset = [];
        this.nzerosStride = 0;
        this.hshift = default;
        this.vshift = default;
        this.currentBy = 0;
        this.qfRow = Memory<int>.Empty;
        this.quantDcRow = Memory<byte>.Empty;
    }

    public JxlAnsSymbolReader[] Decoders => this.decoders;

    /// <inheritdoc/>
    public void StartRow(int by)
    {
        this.currentBy = by;
        this.qfRow = this.rawQuantField.GetRowMemory(by);

        for (int c = 0; c < 3; c++)
        {
            int sby = by >> this.vshift[c];
            this.quantDcRow = this.quantDc.GetRowMemory(this.rect.Y0() + by)[this.rect.X0()..];

            for (int i = 0; i < this.numPasses; i++)
            {
                this.rowNZeroes[i][c] = this.groupDecCache.NumberOfNonZeroes[i].PlaneRowMemory(c, sby);
                this.rowNZeroesTop[i][c] =
                    sby == 0
                        ? default
                        : this.groupDecCache.NumberOfNonZeroes[i].PlaneRowMemory(c, sby - 1);
            }
        }
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
        for (int c = 0; c < 3; c++)
        {
            int sbx = bx >> this.hshift[c];
            int sby = by >> this.vshift[c];

            if ((sbx << this.hshift[c]) != bx || (sby << this.vshift[c]) != by)
            {
                continue;
            }

            for (int pass = 0; pass < this.numPasses; pass++)
            {
                Span<int> rowNzeros = this.groupDecCache.NumberOfNonZeroes[pass].PlaneRow(c, sby);
                ReadOnlySpan<int> rowNzerosTop = sby == 0
                    ? default
                    : this.groupDecCache.NumberOfNonZeroes[pass].PlaneRow(c, sby - 1);

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
                    this.coeffOrders!.AsSpan()[(pass * this.coeffOrderSize)..],
                    this.readers[pass],
                    this.decoders[pass],
                    CollectionsMarshal.AsSpan(this.contextMaps![pass]),
                    this.quantDcRow,
                    this.qfRow,
                    this.blockCtxMap,
                    block,
                    (int)this.shiftForPass!.Memory.Span[pass]))
                {
                    return false;
                }
            }
        }

        return true;
    }

    public bool Init(
        JxlFrameHeader frameHeader,
        JxlBitReader[] readers,
        int numPasses,
        int groupIndex,
        int histogramSelectorBits,
        Rectangle rect,
        JxlPassesDecoderState decState,
        int firstPass)
    {
        for (int i = 0; i < 3; i++)
        {
            this.hshift[i] = frameHeader.ChromaSubsampling!.HShift(i);
            this.vshift[i] = frameHeader.ChromaSubsampling.VShift(i);
        }

        this.coeffOrderSize = decState.Shared.CoeffOrderSize;
        this.coeffOrders = decState.Shared.CoeffOrders.ToArray()[(firstPass * this.coeffOrderSize)..];

        this.contextMaps = new List<byte>[numPasses];
        for (int i = 0; i < numPasses; i++)
        {
            this.contextMaps[i] = decState.ContextMap[firstPass + i];
        }

        this.numPasses = numPasses;
        this.shiftForPass = decState.Shared.FrameHeader.Passes.Shift[firstPass..];
        this.nzerosStride = this.groupDecCache!.NumberOfNonZeroes[0].PixelsPerRow;

        this.ctxOffset = new int[numPasses];

        for (int pass = 0; pass < numPasses; pass++)
        {
            int currentHistogram = 0;

            if (histogramSelectorBits != 0)
            {
                currentHistogram = (int)readers[pass].ReadBits32((uint)histogramSelectorBits);
            }

            if (currentHistogram >= decState.Shared.NumHistograms)
            {
                return false;
            }

            this.ctxOffset[pass] = currentHistogram * this.blockCtxMap.AcContextCount;

            this.decoders[pass] =
                JxlAnsSymbolReader.Create(
                    decState.Code[pass + firstPass],
                    readers[pass]);
        }

        for (int i = 1; i < numPasses; i++)
        {
            if (this.nzerosStride != this.groupDecCache.NumberOfNonZeroes[i].PixelsPerRow)
            {
                return false;
            }
        }

        this.rect = rect;

        return true;
    }
}

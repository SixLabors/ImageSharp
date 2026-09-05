// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.ChromaFromLuma;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;

/// <summary>
/// Encodes AV1 tile syntax elements and transform coefficients with tile-local adaptive distributions.
/// </summary>
internal sealed class Av1SymbolEncoder : IDisposable
{
    /// <summary>
    /// The largest coefficient-context plane required after AV1 removes the uncoded half of 64-point transforms.
    /// </summary>
    private const int MaximumCoefficientContextCount = (Av1Constants.MaxTransformSize / 2) * (Av1Constants.MaxTransformSize / 2);

    /// <summary>
    /// Owns every mutable tile distribution and restores normative defaults without rebuilding the object graph.
    /// </summary>
    private readonly Av1FrameEntropyContext entropyContext;

    /// <summary>
    /// The tile-adaptive intra-block-copy distribution.
    /// </summary>
    private readonly Av1Distribution tileIntraBlockCopy;

    /// <summary>
    /// The tile-adaptive integer displacement-vector context.
    /// </summary>
    private readonly Av1MotionVectorContext displacementVector;

    /// <summary>
    /// The tile-adaptive normal inter motion-vector context.
    /// </summary>
    private readonly Av1MotionVectorContext motionVector;

    /// <summary>
    /// The tile-adaptive NEWMV branch distributions.
    /// </summary>
    private readonly Av1Distribution[] newMotionVector;

    /// <summary>
    /// The tile-adaptive GLOBALMV branch distributions.
    /// </summary>
    private readonly Av1Distribution[] zeroMotionVector;

    /// <summary>
    /// The tile-adaptive NEARESTMV branch distributions.
    /// </summary>
    private readonly Av1Distribution[] referenceMotionVector;

    /// <summary>
    /// The tile-adaptive dynamic-reference-list distributions.
    /// </summary>
    private readonly Av1Distribution[] dynamicReferenceList;

    /// <summary>
    /// The tile-adaptive partition-type distributions.
    /// </summary>
    private readonly Av1Distribution[] tilePartitionTypes;

    /// <summary>
    /// The tile-adaptive key-frame luma-mode distributions.
    /// </summary>
    private readonly Av1Distribution[][] keyFrameYMode;

    /// <summary>
    /// The tile-adaptive inter-frame intra luma-mode distributions.
    /// </summary>
    private readonly Av1Distribution[] frameYMode;

    /// <summary>
    /// The tile-adaptive intra-versus-inter distributions.
    /// </summary>
    private readonly Av1Distribution[] intraInter;

    /// <summary>
    /// The tile-adaptive single-reference branch distributions.
    /// </summary>
    private readonly Av1Distribution[][] singleReference;

    /// <summary>
    /// The tile-adaptive chroma intra-mode distributions.
    /// </summary>
    private readonly Av1Distribution[][] uvMode;

    /// <summary>
    /// The tile-adaptive transform-block skip distributions selected for the frame base quantizer.
    /// </summary>
    private readonly Av1Distribution[][] transformBlockSkip;

    /// <summary>
    /// The tile-adaptive end-of-block token distributions selected for the frame base quantizer.
    /// </summary>
    private readonly Av1Distribution[][][] endOfBlockFlag;

    /// <summary>
    /// The tile-adaptive coefficient base-range distributions selected for the frame base quantizer.
    /// </summary>
    private readonly Av1Distribution[][][] coefficientsBaseRange;

    /// <summary>
    /// The tile-adaptive coefficient base-level distributions selected for the frame base quantizer.
    /// </summary>
    private readonly Av1Distribution[][][] coefficientsBase;

    /// <summary>
    /// The tile-adaptive final-nonzero coefficient distributions selected for the frame base quantizer.
    /// </summary>
    private readonly Av1Distribution[][][] coefficientsBaseEndOfBlock;

    /// <summary>
    /// The tile-adaptive filter-intra enable distributions.
    /// </summary>
    private readonly Av1Distribution[] filterIntra;

    /// <summary>
    /// The tile-adaptive filter-intra mode distribution.
    /// </summary>
    private readonly Av1Distribution filterIntraMode;

    /// <summary>
    /// The tile-adaptive absolute quantizer delta distribution.
    /// </summary>
    private readonly Av1Distribution deltaQuantizerAbsolute;

    /// <summary>
    /// The tile-adaptive DC sign distributions selected for the frame base quantizer.
    /// </summary>
    private readonly Av1Distribution[][] dcSign;

    /// <summary>
    /// The tile-adaptive end-of-block extra-bit distributions selected for the frame base quantizer.
    /// </summary>
    private readonly Av1Distribution[][][] endOfBlockExtra;

    /// <summary>
    /// The tile-adaptive intra transform-type distributions.
    /// </summary>
    private readonly Av1Distribution[][][] intraExtendedTransform;

    /// <summary>
    /// The tile-adaptive inter transform-type distributions used by intra-block copy.
    /// </summary>
    private readonly Av1Distribution[][] interExtendedTransform;

    /// <summary>
    /// The tile-adaptive fixed transform-size distributions.
    /// </summary>
    private readonly Av1Distribution[][] transformSize;

    /// <summary>
    /// The tile-adaptive variable-transform partition distributions.
    /// </summary>
    private readonly Av1Distribution[] transformPartition;

    /// <summary>
    /// The tile-adaptive spatial segment-identifier distributions.
    /// </summary>
    private readonly Av1Distribution[] segmentId;

    /// <summary>
    /// The tile-adaptive directional angle-delta distributions.
    /// </summary>
    private readonly Av1Distribution[] angleDelta;

    /// <summary>
    /// The tile-adaptive transform-skip distributions.
    /// </summary>
    private readonly Av1Distribution[] skip;

    /// <summary>
    /// The tile-adaptive skip-mode distributions.
    /// </summary>
    private readonly Av1Distribution[] skipMode;

    /// <summary>
    /// The tile-adaptive joint chroma-from-luma sign distribution.
    /// </summary>
    private readonly Av1Distribution chromaFromLumaSign;

    /// <summary>
    /// The tile-adaptive chroma-from-luma alpha-magnitude distributions.
    /// </summary>
    private readonly Av1Distribution[] chromaFromLumaAlpha;

    /// <summary>
    /// Indicates whether the range writer has been disposed.
    /// </summary>
    private bool isDisposed;

    /// <summary>
    /// The reusable padded coefficient levels used to derive entropy contexts.
    /// </summary>
    private readonly Av1LevelBuffer levels;

    /// <summary>
    /// The reusable raster-order coefficient contexts for one transform.
    /// </summary>
    private readonly IMemoryOwner<sbyte> coefficientContexts;

    /// <summary>
    /// The range writer producing the current tile payload.
    /// </summary>
    private Av1SymbolWriter writer;

    /// <summary>
    /// The frame base quantizer used to select coefficient probability models.
    /// </summary>
    private readonly int baseQIndex;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1SymbolEncoder"/> class with reusable tile state.
    /// </summary>
    /// <param name="configuration">The configuration providing output and temporary memory.</param>
    /// <param name="bufferLength">The initial output capacity in bytes.</param>
    /// <param name="qIndex">The frame base quantizer index.</param>
    /// <param name="updateCdf">A value indicating whether encoded symbols adapt their tile distributions.</param>
    public Av1SymbolEncoder(Configuration configuration, int bufferLength, int qIndex, bool updateCdf)
    {
        this.entropyContext = new Av1FrameEntropyContext(qIndex);

        // Encoder and decoder now share the same mutable context shape. Every field aliases that single graph so
        // sequence samples can restore normative defaults without replacing any distribution or array.
        this.tileIntraBlockCopy = this.entropyContext.IntraBlockCopy;
        this.motionVector = this.entropyContext.MotionVector;
        this.displacementVector = this.entropyContext.DisplacementVector;
        this.tilePartitionTypes = this.entropyContext.PartitionTypes;
        this.keyFrameYMode = this.entropyContext.KeyFrameYMode;
        this.frameYMode = this.entropyContext.FrameYMode;
        this.intraInter = this.entropyContext.IntraInter;
        this.singleReference = this.entropyContext.SingleReference;
        this.newMotionVector = this.entropyContext.NewMv;
        this.zeroMotionVector = this.entropyContext.ZeroMv;
        this.referenceMotionVector = this.entropyContext.RefMv;
        this.dynamicReferenceList = this.entropyContext.Drl;
        this.uvMode = this.entropyContext.UvMode;
        this.filterIntra = this.entropyContext.FilterIntra;
        this.filterIntraMode = this.entropyContext.FilterIntraMode;
        this.deltaQuantizerAbsolute = this.entropyContext.DeltaQuantizerAbsolute;
        this.intraExtendedTransform = this.entropyContext.IntraExtendedTransform;
        this.interExtendedTransform = this.entropyContext.InterExtendedTransform;
        this.transformSize = this.entropyContext.TransformSize;
        this.transformPartition = this.entropyContext.TransformPartition;
        this.segmentId = this.entropyContext.SegmentId;
        this.angleDelta = this.entropyContext.AngleDelta;
        this.skip = this.entropyContext.Skip;
        this.skipMode = this.entropyContext.SkipMode;
        this.chromaFromLumaSign = this.entropyContext.ChromaFromLumaSign;
        this.chromaFromLumaAlpha = this.entropyContext.ChromaFromLumaAlpha;
        this.transformBlockSkip = this.entropyContext.TransformBlockSkip;
        this.endOfBlockFlag = this.entropyContext.EndOfBlockFlag;
        this.coefficientsBaseRange = this.entropyContext.CoefficientsBaseRange;
        this.coefficientsBase = this.entropyContext.CoefficientsBase;
        this.coefficientsBaseEndOfBlock = this.entropyContext.BaseEndOfBlock;
        this.dcSign = this.entropyContext.DcSign;
        this.endOfBlockExtra = this.entropyContext.EndOfBlockExtra;

        // Transform dimensions are bounded by the AV1 coefficient-coding rules, so the complete entropy scratch
        // is known with the tile output capacity and remains valid for every transform in every sequence sample.
        this.levels = new Av1LevelBuffer(configuration);
        try
        {
            this.coefficientContexts =
                configuration.MemoryAllocator.Allocate<sbyte>(MaximumCoefficientContextCount);

            this.writer = new(configuration, bufferLength, updateCdf);
            this.baseQIndex = qIndex;
        }
        catch
        {
            // The level buffer is already owned here; a later allocation failure cannot be unwound by the caller.
            this.coefficientContexts?.Dispose();
            this.levels.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Defines how shared coefficient-syntax helpers handle one adaptive symbol or literal bit field.
    /// </summary>
    private interface ICoefficientSymbolOperation
    {
        /// <summary>
        /// Handles one symbol from an adaptive distribution.
        /// </summary>
        /// <param name="writer">The tile range writer.</param>
        /// <param name="symbol">The zero-based symbol.</param>
        /// <param name="distribution">The symbol distribution.</param>
        /// <returns>The symbol's rate contribution.</returns>
        public static abstract int ProcessSymbol(
            ref Av1SymbolWriter writer,
            int symbol,
            Av1Distribution distribution);

        /// <summary>
        /// Handles one most-significant-bit-first literal field.
        /// </summary>
        /// <param name="writer">The tile range writer.</param>
        /// <param name="value">The low-order literal bits.</param>
        /// <param name="bitCount">The number of bits.</param>
        /// <returns>The literal's rate contribution.</returns>
        public static abstract int ProcessLiteral(
            ref Av1SymbolWriter writer,
            uint value,
            int bitCount);
    }

    /// <summary>
    /// Defines how the shared palette-map traversal handles its uniform first index and adaptive remaining indices.
    /// </summary>
    private interface IPaletteColorMapOperation
    {
        /// <summary>
        /// Handles the first uniformly coded palette index.
        /// </summary>
        /// <param name="encoder">The tile symbol encoder.</param>
        /// <param name="paletteSize">The number of colors in the palette.</param>
        /// <param name="colorIndex">The first palette index.</param>
        /// <returns>The index's rate contribution.</returns>
        public static abstract int ProcessFirstIndex(
            Av1SymbolEncoder encoder,
            int paletteSize,
            int colorIndex);

        /// <summary>
        /// Handles one context-adaptive palette color-order index.
        /// </summary>
        /// <param name="encoder">The tile symbol encoder.</param>
        /// <param name="paletteSize">The number of colors in the palette.</param>
        /// <param name="planeType">The luma or chroma plane class.</param>
        /// <param name="colorContext">The spatial color-index context.</param>
        /// <param name="colorOrderIndex">The index in the context-specific color order.</param>
        /// <returns>The index's rate contribution.</returns>
        public static abstract int ProcessColorIndex(
            Av1SymbolEncoder encoder,
            int paletteSize,
            Av1PlaneType planeType,
            int colorContext,
            int colorOrderIndex);
    }

    /// <summary>
    /// Restores the initial tile distributions and range coder while retaining their complete object graph and buffers.
    /// </summary>
    public void Reset()
    {
        this.entropyContext.ResetToDefaults(this.baseQIndex);
        this.writer.Reset();
    }

    /// <summary>
    /// Restores the initial tile distributions and begins the next tile at an offset in the retained output buffer.
    /// </summary>
    /// <param name="outputOffset">The first output byte available to the next tile.</param>
    public void Reset(int outputOffset)
    {
        this.entropyContext.ResetToDefaults(this.baseQIndex);
        this.writer.Reset(outputOffset);
    }

    /// <summary>
    /// Writes an unsigned fixed-width literal to the tile entropy stream.
    /// </summary>
    /// <param name="value">The low-order literal bits.</param>
    /// <param name="bitCount">The number of bits to write.</param>
    public void WriteLiteral(uint value, int bitCount)
    {
        ref Av1SymbolWriter w = ref this.writer;
        w.WriteLiteral(value, bitCount);
    }

    /// <summary>
    /// Writes a uniformly coded value from a non-power-of-two alphabet.
    /// </summary>
    /// <param name="valueCount">The number of possible values.</param>
    /// <param name="value">The value in the range from zero through <paramref name="valueCount"/> minus one.</param>
    public void WriteUniform(int valueCount, int value)
    {
        ref Av1SymbolWriter w = ref this.writer;
        int bitCount = Av1Math.Log2(valueCount) + 1;
        int threshold = (1 << bitCount) - valueCount;
        if (value < threshold)
        {
            // The lower values use the short prefix; every remaining value carries one final disambiguating bit.
            w.WriteLiteral((uint)value, bitCount - 1);
            return;
        }

        int offset = value - threshold;
        w.WriteLiteral((uint)(threshold + (offset >> 1)), bitCount - 1);
        w.WriteLiteral((uint)(offset & 1), 1);
    }

    /// <summary>
    /// Gets the fixed-point rate of a uniformly coded value.
    /// </summary>
    /// <param name="valueCount">The number of possible values.</param>
    /// <param name="value">The value in the range from zero through <paramref name="valueCount"/> minus one.</param>
    /// <returns>The rate cost in 1/512-bit units.</returns>
    public static int GetUniformCost(int valueCount, int value)
    {
        int bitCount = Av1Math.Log2(valueCount) + 1;
        int threshold = (1 << bitCount) - valueCount;
        return Av1ProbabilityCost.GetLiteralCost(value < threshold ? bitCount - 1 : bitCount);
    }

    /// <summary>
    /// Gets the current fixed-point cost of the luma palette-mode flag.
    /// </summary>
    /// <param name="usePalette">Indicates whether the block uses luma palette prediction.</param>
    /// <param name="blockSizeContext">The block-area context in the range from zero through six.</param>
    /// <param name="neighborContext">The number of available above and left luma neighbors that use palettes.</param>
    /// <returns>The rate cost in 1/512-bit units.</returns>
    public int GetPaletteYModeCost(bool usePalette, int blockSizeContext, int neighborContext)
    {
        return Av1ProbabilityCost.GetSymbolCost(
            this.entropyContext.PaletteYMode[blockSizeContext][neighborContext],
            usePalette ? 1 : 0);
    }

    /// <summary>
    /// Writes the luma palette-mode flag.
    /// </summary>
    /// <param name="usePalette">Indicates whether the block uses luma palette prediction.</param>
    /// <param name="blockSizeContext">The block-area context in the range from zero through six.</param>
    /// <param name="neighborContext">The number of available above and left luma neighbors that use palettes.</param>
    public void WritePaletteYMode(bool usePalette, int blockSizeContext, int neighborContext)
    {
        ref Av1SymbolWriter w = ref this.writer;
        w.WriteSymbol(usePalette, this.entropyContext.PaletteYMode[blockSizeContext][neighborContext]);
    }

    /// <summary>
    /// Gets the current fixed-point cost of the chroma palette-mode flag.
    /// </summary>
    /// <param name="usePalette">Indicates whether the block uses chroma palette prediction.</param>
    /// <param name="hasLumaPalette">Indicates whether the current block uses a luma palette.</param>
    /// <returns>The rate cost in 1/512-bit units.</returns>
    public int GetPaletteUvModeCost(bool usePalette, bool hasLumaPalette)
    {
        return Av1ProbabilityCost.GetSymbolCost(
            this.entropyContext.PaletteUvMode[hasLumaPalette ? 1 : 0],
            usePalette ? 1 : 0);
    }

    /// <summary>
    /// Writes the chroma palette-mode flag.
    /// </summary>
    /// <param name="usePalette">Indicates whether the block uses chroma palette prediction.</param>
    /// <param name="hasLumaPalette">Indicates whether the current block uses a luma palette.</param>
    public void WritePaletteUvMode(bool usePalette, bool hasLumaPalette)
    {
        ref Av1SymbolWriter w = ref this.writer;
        w.WriteSymbol(usePalette, this.entropyContext.PaletteUvMode[hasLumaPalette ? 1 : 0]);
    }

    /// <summary>
    /// Gets the current fixed-point cost of a palette-size symbol.
    /// </summary>
    /// <param name="paletteSize">The palette size in the range from two through eight.</param>
    /// <param name="blockSizeContext">The block-area context in the range from zero through six.</param>
    /// <param name="planeType">The luma or chroma plane class.</param>
    /// <returns>The rate cost in 1/512-bit units.</returns>
    public int GetPaletteSizeCost(int paletteSize, int blockSizeContext, Av1PlaneType planeType)
    {
        Av1Distribution distribution = planeType == Av1PlaneType.Y
            ? this.entropyContext.PaletteYSize[blockSizeContext]
            : this.entropyContext.PaletteUvSize[blockSizeContext];

        return Av1ProbabilityCost.GetSymbolCost(distribution, paletteSize - 2);
    }

    /// <summary>
    /// Writes a palette-size symbol.
    /// </summary>
    /// <param name="paletteSize">The palette size in the range from two through eight.</param>
    /// <param name="blockSizeContext">The block-area context in the range from zero through six.</param>
    /// <param name="planeType">The luma or chroma plane class.</param>
    public void WritePaletteSize(int paletteSize, int blockSizeContext, Av1PlaneType planeType)
    {
        ref Av1SymbolWriter w = ref this.writer;
        Av1Distribution distribution = planeType == Av1PlaneType.Y
            ? this.entropyContext.PaletteYSize[blockSizeContext]
            : this.entropyContext.PaletteUvSize[blockSizeContext];

        w.WriteSymbol(paletteSize - 2, distribution);
    }

    /// <summary>
    /// Gets the current fixed-point cost of a palette color-order index.
    /// </summary>
    /// <param name="colorOrderIndex">The index in the context-specific palette color order.</param>
    /// <param name="paletteSize">The number of colors in the palette.</param>
    /// <param name="colorContext">The color-index context derived from preceding spatial indices.</param>
    /// <param name="planeType">The luma or chroma plane class.</param>
    /// <returns>The rate cost in 1/512-bit units.</returns>
    public int GetPaletteColorIndexCost(
        int colorOrderIndex,
        int paletteSize,
        int colorContext,
        Av1PlaneType planeType)
    {
        Av1Distribution distribution = planeType == Av1PlaneType.Y
            ? this.entropyContext.PaletteYColorIndex[paletteSize - 2][colorContext]
            : this.entropyContext.PaletteUvColorIndex[paletteSize - 2][colorContext];

        return Av1ProbabilityCost.GetSymbolCost(distribution, colorOrderIndex);
    }

    /// <summary>
    /// Writes a palette color-order index.
    /// </summary>
    /// <param name="colorOrderIndex">The index in the context-specific palette color order.</param>
    /// <param name="paletteSize">The number of colors in the palette.</param>
    /// <param name="colorContext">The color-index context derived from preceding spatial indices.</param>
    /// <param name="planeType">The luma or chroma plane class.</param>
    public void WritePaletteColorIndex(
        int colorOrderIndex,
        int paletteSize,
        int colorContext,
        Av1PlaneType planeType)
    {
        ref Av1SymbolWriter w = ref this.writer;
        Av1Distribution distribution = planeType == Av1PlaneType.Y
            ? this.entropyContext.PaletteYColorIndex[paletteSize - 2][colorContext]
            : this.entropyContext.PaletteUvColorIndex[paletteSize - 2][colorContext];

        w.WriteSymbol(colorOrderIndex, distribution);
    }

    /// <summary>
    /// Gets the fixed-point rate of the luma palette colors.
    /// </summary>
    /// <param name="colorCache">The sorted unique colors inherited from eligible neighbors.</param>
    /// <param name="colors">The sorted luma palette colors.</param>
    /// <param name="bitDepth">The number of bits in each color sample.</param>
    /// <returns>The rate cost in 1/512-bit units.</returns>
    public static int GetPaletteYColorCost(
        ReadOnlySpan<ushort> colorCache,
        ReadOnlySpan<ushort> colors,
        int bitDepth)
    {
        Span<byte> cacheColorFound = stackalloc byte[Av1Constants.PaletteMaxSize * 2];
        Span<ushort> uncachedColors = stackalloc ushort[Av1Constants.PaletteMaxSize];
        int uncachedColorCount = IndexColorCache(
            colorCache,
            colors,
            cacheColorFound,
            uncachedColors);

        // Palette RD modeling charges every available cache flag even though emission can stop once all colors match.
        int bitCount = colorCache.Length +
            GetDeltaEncodedColorBitCount(uncachedColors[..uncachedColorCount], bitDepth, minimumDelta: 1);

        return Av1ProbabilityCost.GetLiteralCost(bitCount);
    }

    /// <summary>
    /// Writes the luma palette colors using neighboring cache selections followed by sorted deltas.
    /// </summary>
    /// <param name="colorCache">The sorted unique colors inherited from eligible neighbors.</param>
    /// <param name="colors">The sorted luma palette colors.</param>
    /// <param name="bitDepth">The number of bits in each color sample.</param>
    public void WritePaletteYColors(
        ReadOnlySpan<ushort> colorCache,
        ReadOnlySpan<ushort> colors,
        int bitDepth)
    {
        Span<byte> cacheColorFound = stackalloc byte[Av1Constants.PaletteMaxSize * 2];
        Span<ushort> uncachedColors = stackalloc ushort[Av1Constants.PaletteMaxSize];
        int uncachedColorCount = IndexColorCache(
            colorCache,
            colors,
            cacheColorFound,
            uncachedColors);

        int cachedColorCount = 0;
        for (int i = 0; i < colorCache.Length && cachedColorCount < colors.Length; i++)
        {
            byte found = cacheColorFound[i];
            this.WriteLiteral(found, 1);
            cachedColorCount += found;
        }

        this.WriteDeltaEncodedColors(uncachedColors[..uncachedColorCount], bitDepth, minimumDelta: 1);
    }

    /// <summary>
    /// Gets the fixed-point rate of the shared chroma palette colors.
    /// </summary>
    /// <param name="colorCache">The sorted unique U colors inherited from eligible neighbors.</param>
    /// <param name="uColors">The sorted U palette colors.</param>
    /// <param name="vColors">The V palette colors paired with <paramref name="uColors"/>.</param>
    /// <param name="bitDepth">The number of bits in each color sample.</param>
    /// <returns>The rate cost in 1/512-bit units.</returns>
    public static int GetPaletteUvColorCost(
        ReadOnlySpan<ushort> colorCache,
        ReadOnlySpan<ushort> uColors,
        ReadOnlySpan<ushort> vColors,
        int bitDepth)
    {
        Span<byte> cacheColorFound = stackalloc byte[Av1Constants.PaletteMaxSize * 2];
        Span<ushort> uncachedColors = stackalloc ushort[Av1Constants.PaletteMaxSize];
        int uncachedColorCount = IndexColorCache(
            colorCache,
            uColors,
            cacheColorFound,
            uncachedColors);

        // Palette RD modeling charges every available cache flag even though emission can stop once all colors match.
        int bitCount = colorCache.Length +
            GetDeltaEncodedColorBitCount(uncachedColors[..uncachedColorCount], bitDepth, minimumDelta: 0);

        int deltaBits = GetPaletteVDeltaBitCount(vColors, bitDepth, out int zeroCount, out int minimumBits);
        int deltaBitCount = 2 + bitDepth + ((deltaBits + 1) * (vColors.Length - 1)) - zeroCount;
        int rawBitCount = bitDepth * vColors.Length;
        bitCount += 1 + Math.Min(deltaBitCount, rawBitCount);
        return Av1ProbabilityCost.GetLiteralCost(bitCount);
    }

    /// <summary>
    /// Writes the shared chroma palette colors using cached U values and the cheaper V representation.
    /// </summary>
    /// <param name="colorCache">The sorted unique U colors inherited from eligible neighbors.</param>
    /// <param name="uColors">The sorted U palette colors.</param>
    /// <param name="vColors">The V palette colors paired with <paramref name="uColors"/>.</param>
    /// <param name="bitDepth">The number of bits in each color sample.</param>
    public void WritePaletteUvColors(
        ReadOnlySpan<ushort> colorCache,
        ReadOnlySpan<ushort> uColors,
        ReadOnlySpan<ushort> vColors,
        int bitDepth)
    {
        Span<byte> cacheColorFound = stackalloc byte[Av1Constants.PaletteMaxSize * 2];
        Span<ushort> uncachedColors = stackalloc ushort[Av1Constants.PaletteMaxSize];
        int uncachedColorCount = IndexColorCache(
            colorCache,
            uColors,
            cacheColorFound,
            uncachedColors);

        int cachedColorCount = 0;
        for (int i = 0; i < colorCache.Length && cachedColorCount < uColors.Length; i++)
        {
            byte found = cacheColorFound[i];
            this.WriteLiteral(found, 1);
            cachedColorCount += found;
        }

        this.WriteDeltaEncodedColors(uncachedColors[..uncachedColorCount], bitDepth, minimumDelta: 0);

        int deltaBits = GetPaletteVDeltaBitCount(vColors, bitDepth, out int zeroCount, out int minimumBits);
        int deltaBitCount = 2 + bitDepth + ((deltaBits + 1) * (vColors.Length - 1)) - zeroCount;
        int rawBitCount = bitDepth * vColors.Length;
        bool useDelta = deltaBitCount < rawBitCount;
        this.WriteLiteral(useDelta ? 1u : 0u, 1);
        if (!useDelta)
        {
            for (int i = 0; i < vColors.Length; i++)
            {
                this.WriteLiteral(vColors[i], bitDepth);
            }

            return;
        }

        this.WriteLiteral((uint)(deltaBits - minimumBits), 2);
        this.WriteLiteral(vColors[0], bitDepth);
        int sampleRange = 1 << bitDepth;
        for (int i = 1; i < vColors.Length; i++)
        {
            int signedDelta = vColors[i] - vColors[i - 1];
            int delta = Math.Abs(signedDelta);

            // Chroma wraps in its unsigned sample domain, so signal whichever circular direction has less magnitude.
            if (delta <= sampleRange - delta)
            {
                this.WriteLiteral((uint)delta, deltaBits);
                if (delta != 0)
                {
                    this.WriteLiteral(signedDelta < 0 ? 1u : 0u, 1);
                }
            }
            else
            {
                this.WriteLiteral((uint)(sampleRange - delta), deltaBits);
                this.WriteLiteral(signedDelta < 0 ? 0u : 1u, 1);
            }
        }
    }

    /// <summary>
    /// Gets the current fixed-point rate of a complete palette color-index map.
    /// </summary>
    /// <param name="paletteSize">The number of colors in the palette.</param>
    /// <param name="planeType">The luma or chroma plane class.</param>
    /// <param name="rows">The number of coded map rows.</param>
    /// <param name="columns">The number of coded map columns.</param>
    /// <param name="colorIndexMap">The complete row-addressable color-index map.</param>
    /// <returns>The rate cost in 1/512-bit units.</returns>
    public int GetPaletteColorMapCost(
        int paletteSize,
        Av1PlaneType planeType,
        int rows,
        int columns,
        Buffer2DRegion<byte> colorIndexMap)
        => this.ProcessPaletteColorMap<PaletteColorMapCostOperation>(
            paletteSize,
            planeType,
            rows,
            columns,
            colorIndexMap);

    /// <summary>
    /// Writes a complete palette color-index map in AV1 diagonal wavefront order.
    /// </summary>
    /// <param name="paletteSize">The number of colors in the palette.</param>
    /// <param name="planeType">The luma or chroma plane class.</param>
    /// <param name="rows">The number of coded map rows.</param>
    /// <param name="columns">The number of coded map columns.</param>
    /// <param name="colorIndexMap">The complete row-addressable color-index map.</param>
    public void WritePaletteColorMap(
        int paletteSize,
        Av1PlaneType planeType,
        int rows,
        int columns,
        Buffer2DRegion<byte> colorIndexMap)
    {
        _ = this.ProcessPaletteColorMap<PaletteColorMapWriteOperation>(
            paletteSize,
            planeType,
            rows,
            columns,
            colorIndexMap);
    }

    /// <summary>
    /// Writes the frame-local intra-block-copy flag.
    /// </summary>
    /// <param name="value">Indicates whether intra-block copy is selected.</param>
    public void WriteUseIntraBlockCopy(bool value)
    {
        ref Av1SymbolWriter w = ref this.writer;
        w.WriteSymbol(value, this.tileIntraBlockCopy);
    }

    /// <summary>
    /// Measures the frame-local intra-block-copy flag against the live distribution.
    /// </summary>
    /// <param name="value">Indicates whether intra-block copy is selected.</param>
    /// <returns>The syntax cost in 1/512-bit units.</returns>
    public int GetUseIntraBlockCopyCost(bool value)
        => Av1ProbabilityCost.GetSymbolCost(this.tileIntraBlockCopy, value ? 1 : 0);

    /// <summary>
    /// Writes an integer intra-block-copy displacement vector relative to a spatial reference.
    /// </summary>
    /// <param name="value">The displacement vector to encode.</param>
    /// <param name="reference">The spatially derived reference vector.</param>
    public void WriteDisplacementVector(Av1MotionVector value, Av1MotionVector reference)
        => this.displacementVector.Write(this.writer, value, reference, Av1MotionVectorPrecision.Integer);

    /// <summary>
    /// Measures an integer intra-block-copy displacement vector against the live distributions.
    /// </summary>
    /// <param name="value">The displacement vector to measure.</param>
    /// <param name="reference">The spatially derived reference vector.</param>
    /// <returns>The discounted syntax cost in 1/512-bit units.</returns>
    public int GetDisplacementVectorCost(Av1MotionVector value, Av1MotionVector reference)
    {
        const int DisplacementVectorCostWeight = 120;
        const int WeightShift = 7;
        int rate = this.displacementVector.GetCost(
            this.writer,
            value,
            reference,
            Av1MotionVectorPrecision.Integer);

        // Displacement syntax uses a 120/128 discount during mode search; adding half the divisor rounds to nearest.
        return ((rate * DisplacementVectorCostWeight) + (1 << (WeightShift - 1))) >> WeightShift;
    }

    /// <summary>
    /// Measures an integer intra-block-copy displacement vector for variance-domain motion search.
    /// </summary>
    /// <param name="value">The displacement vector to measure.</param>
    /// <param name="reference">The spatially derived reference vector.</param>
    /// <returns>The syntax cost in 1/512-bit units.</returns>
    public int GetDisplacementVectorSearchCost(Av1MotionVector value, Av1MotionVector reference)
        => this.displacementVector.GetCost(
            this.writer,
            value,
            reference,
            Av1MotionVectorPrecision.Integer);

    /// <summary>
    /// Measures one switchable interpolation filter against its live tile distribution.
    /// </summary>
    /// <param name="filter">The regular, smooth, or sharp filter.</param>
    /// <param name="context">The spatial filter context for the selected direction.</param>
    /// <returns>The syntax cost in 1/512-bit units.</returns>
    public int GetSwitchableInterpolationFilterCost(Av1InterpolationFilter filter, int context)
        => Av1ProbabilityCost.GetSymbolCost(this.entropyContext.SwitchableInterpolation[context], (int)filter);

    /// <summary>
    /// Writes one switchable interpolation filter and updates its live tile distribution.
    /// </summary>
    /// <param name="filter">The regular, smooth, or sharp filter.</param>
    /// <param name="context">The spatial filter context for the selected direction.</param>
    public void WriteSwitchableInterpolationFilter(Av1InterpolationFilter filter, int context)
        => this.writer.WriteSymbol((int)filter, this.entropyContext.SwitchableInterpolation[context]);

    /// <summary>
    /// Measures a single-reference inter mode against the live branch distributions.
    /// </summary>
    /// <param name="mode">The new, global, nearest, or near motion-vector mode.</param>
    /// <param name="modeContext">The packed context derived from the reference-vector stack.</param>
    /// <returns>The syntax cost in 1/512-bit units.</returns>
    public int GetInterModeCost(Av1PredictionMode mode, int modeContext)
    {
        bool isNotNew = mode != Av1PredictionMode.NewMotionVector;
        int rate = Av1ProbabilityCost.GetSymbolCost(
            this.newMotionVector[Av1SymbolContextHelper.GetNewMvContext(modeContext)],
            isNotNew ? 1 : 0);

        if (!isNotNew)
        {
            return rate;
        }

        bool isNotGlobal = mode != Av1PredictionMode.GlobalMotionVector;
        rate += Av1ProbabilityCost.GetSymbolCost(
            this.zeroMotionVector[Av1SymbolContextHelper.GetZeroMvContext(modeContext)],
            isNotGlobal ? 1 : 0);

        if (!isNotGlobal)
        {
            return rate;
        }

        return rate + Av1ProbabilityCost.GetSymbolCost(
            this.referenceMotionVector[Av1SymbolContextHelper.GetRefMvContext(modeContext)],
            mode == Av1PredictionMode.NearMotionVector ? 1 : 0);
    }

    /// <summary>
    /// Writes a single-reference inter mode through the NEWMV, GLOBALMV, and NEARESTMV branch tree.
    /// </summary>
    /// <param name="mode">The new, global, nearest, or near motion-vector mode.</param>
    /// <param name="modeContext">The packed context derived from the reference-vector stack.</param>
    public void WriteInterMode(Av1PredictionMode mode, int modeContext)
    {
        ref Av1SymbolWriter w = ref this.writer;
        bool isNotNew = mode != Av1PredictionMode.NewMotionVector;
        w.WriteSymbol(isNotNew, this.newMotionVector[Av1SymbolContextHelper.GetNewMvContext(modeContext)]);
        if (!isNotNew)
        {
            return;
        }

        bool isNotGlobal = mode != Av1PredictionMode.GlobalMotionVector;
        w.WriteSymbol(isNotGlobal, this.zeroMotionVector[Av1SymbolContextHelper.GetZeroMvContext(modeContext)]);
        if (!isNotGlobal)
        {
            return;
        }

        w.WriteSymbol(
            mode == Av1PredictionMode.NearMotionVector,
            this.referenceMotionVector[Av1SymbolContextHelper.GetRefMvContext(modeContext)]);
    }

    /// <summary>
    /// Measures one dynamic-reference-list advance decision.
    /// </summary>
    /// <param name="advance">Whether selection advances to the next candidate.</param>
    /// <param name="context">The candidate-weight context.</param>
    /// <returns>The syntax cost in 1/512-bit units.</returns>
    public int GetDynamicReferenceListCost(bool advance, int context)
        => Av1ProbabilityCost.GetSymbolCost(this.dynamicReferenceList[context], advance ? 1 : 0);

    /// <summary>
    /// Writes one dynamic-reference-list advance decision.
    /// </summary>
    /// <param name="advance">Whether selection advances to the next candidate.</param>
    /// <param name="context">The candidate-weight context.</param>
    public void WriteDynamicReferenceList(bool advance, int context)
    {
        ref Av1SymbolWriter w = ref this.writer;
        w.WriteSymbol(advance, this.dynamicReferenceList[context]);
    }

    /// <summary>
    /// Measures an inter motion vector relative to its selected stack reference.
    /// </summary>
    /// <param name="value">The selected motion vector.</param>
    /// <param name="reference">The differential reference from the candidate stack.</param>
    /// <param name="precision">The fractional precision selected by the frame header.</param>
    /// <returns>The syntax cost in 1/512-bit units.</returns>
    public int GetMotionVectorCost(
        Av1MotionVector value,
        Av1MotionVector reference,
        Av1MotionVectorPrecision precision)
        => this.motionVector.GetCost(this.writer, value, reference, precision);

    /// <summary>
    /// Writes an inter motion vector relative to its selected stack reference.
    /// </summary>
    /// <param name="value">The selected motion vector.</param>
    /// <param name="reference">The differential reference from the candidate stack.</param>
    /// <param name="precision">The fractional precision selected by the frame header.</param>
    public void WriteMotionVector(
        Av1MotionVector value,
        Av1MotionVector reference,
        Av1MotionVectorPrecision precision)
        => this.motionVector.Write(this.writer, value, reference, precision);

    /// <summary>
    /// Gets the current fixed-point cost of a complete block partition symbol.
    /// </summary>
    /// <param name="partitionType">The partition type to measure.</param>
    /// <param name="context">The partition probability context.</param>
    /// <returns>The rate cost in 1/512-bit units.</returns>
    public int GetPartitionTypeCost(Av1PartitionType partitionType, int context)
        => Av1ProbabilityCost.GetSymbolCost(this.tilePartitionTypes[context], (int)partitionType);

    /// <summary>
    /// Writes a complete block partition type using the selected partition context.
    /// </summary>
    /// <param name="partitionType">The partition type to encode.</param>
    /// <param name="context">The partition probability context.</param>
    public void WritePartitionType(Av1PartitionType partitionType, int context)
    {
        ref Av1SymbolWriter w = ref this.writer;
        w.WriteSymbol((int)partitionType, this.tilePartitionTypes[context]);
    }

    /// <summary>
    /// Writes the split-versus-horizontal boundary decision for a block clipped at the bottom tile edge.
    /// </summary>
    /// <param name="partitionType">The split or horizontal partition outcome.</param>
    /// <param name="blockSize">The current block size.</param>
    /// <param name="context">The partition probability context.</param>
    public void WriteSplitOrHorizontal(Av1PartitionType partitionType, Av1BlockSize blockSize, int context)
    {
        uint frequency = Av1SymbolDecoder.GetSplitOrHorizontalFrequency(this.tilePartitionTypes, blockSize, context);
        bool value = partitionType == Av1PartitionType.Split;
        ref Av1SymbolWriter w = ref this.writer;
        w.WriteBoolean(value, frequency);
    }

    /// <summary>
    /// Gets the current fixed-point cost of the split-versus-horizontal boundary decision.
    /// </summary>
    /// <param name="partitionType">The split or horizontal partition outcome.</param>
    /// <param name="blockSize">The current block size.</param>
    /// <param name="context">The partition probability context.</param>
    /// <returns>The rate cost in 1/512-bit units.</returns>
    public int GetSplitOrHorizontalCost(Av1PartitionType partitionType, Av1BlockSize blockSize, int context)
    {
        int frequency = (int)Av1SymbolDecoder.GetSplitOrHorizontalFrequency(
            this.tilePartitionTypes,
            blockSize,
            context);

        return Av1ProbabilityCost.GetSymbolCost(
            partitionType == Av1PartitionType.Split
                ? frequency
                : Av1Distribution.ProbabilityTop - frequency);
    }

    /// <summary>
    /// Writes the split-versus-vertical boundary decision for a block clipped at the right tile edge.
    /// </summary>
    /// <param name="partitionType">The split or vertical partition outcome.</param>
    /// <param name="blockSize">The current block size.</param>
    /// <param name="context">The partition probability context.</param>
    public void WriteSplitOrVertical(Av1PartitionType partitionType, Av1BlockSize blockSize, int context)
    {
        uint frequency = Av1SymbolDecoder.GetSplitOrVerticalFrequency(this.tilePartitionTypes, blockSize, context);
        bool value = partitionType == Av1PartitionType.Split;
        ref Av1SymbolWriter w = ref this.writer;
        w.WriteBoolean(value, frequency);
    }

    /// <summary>
    /// Gets the current fixed-point cost of the split-versus-vertical boundary decision.
    /// </summary>
    /// <param name="partitionType">The split or vertical partition outcome.</param>
    /// <param name="blockSize">The current block size.</param>
    /// <param name="context">The partition probability context.</param>
    /// <returns>The rate cost in 1/512-bit units.</returns>
    public int GetSplitOrVerticalCost(Av1PartitionType partitionType, Av1BlockSize blockSize, int context)
    {
        int frequency = (int)Av1SymbolDecoder.GetSplitOrVerticalFrequency(
            this.tilePartitionTypes,
            blockSize,
            context);

        return Av1ProbabilityCost.GetSymbolCost(
            partitionType == Av1PartitionType.Split
                ? frequency
                : Av1Distribution.ProbabilityTop - frequency);
    }

    /// <summary>
    /// Encodes one transform block's coefficient syntax using scan-order probability contexts.
    /// </summary>
    /// <param name="transformSize">The signaled transform size.</param>
    /// <param name="transformType">The transform type selecting the scan and context class.</param>
    /// <param name="intraDirection">The block's intra prediction mode.</param>
    /// <param name="coefficientBuffer">The raster-ordered signed coefficient levels.</param>
    /// <param name="componentType">The luma or chroma component category.</param>
    /// <param name="transformBlockContext">The neighboring skip and DC sign contexts.</param>
    /// <param name="endOfBlock">The one-based final nonzero scan position, or zero for an empty block.</param>
    /// <param name="useReducedTransformSet">Indicates whether the frame restricts transform choices.</param>
    /// <param name="filterIntraMode">The selected filter-intra mode, or the disabled sentinel.</param>
    /// <param name="usesInterTransformSet">Indicates whether inter rather than intra transform probabilities apply.</param>
    /// <returns>The packed coefficient context used by adjacent transform blocks.</returns>
    public int WriteCoefficients(
        Av1TransformSize transformSize,
        Av1TransformType transformType,
        Av1PredictionMode intraDirection,
        ReadOnlySpan<int> coefficientBuffer,
        Av1ComponentType componentType,
        Av1TransformBlockContext transformBlockContext,
        ushort endOfBlock,
        bool useReducedTransformSet,
        Av1FilterIntraMode filterIntraMode,
        bool usesInterTransformSet)
    {
        Av1TransformSize transformSizeContext = Av1SymbolContextHelper.GetTransformSizeContext(transformSize);

        DebugGuard.MustBeLessThan((int)transformSizeContext, (int)Av1TransformSize.AllSizes, nameof(transformSizeContext));

        _ = this.ProcessTransformBlockSkip<CoefficientWriteOperation>(
            endOfBlock == 0,
            transformSizeContext,
            transformBlockContext.SkipContext);

        if (endOfBlock == 0)
        {
            return 0;
        }

        Av1TransformSize adjustedTransformSize = transformSize.GetAdjusted();
        int width = adjustedTransformSize.GetWidth();
        int height = adjustedTransformSize.GetHeight();
        Av1TransformClass transformClass = transformType.ToClass();
        ReadOnlySpan<short> scan = Av1ScanOrderConstants.GetScanOrder(transformSize, transformType).Scan;
        Av1LevelBuffer levels = this.PrepareCoefficientScratch(
            width,
            height,
            clearLevels: true,
            out Span<sbyte> coefficientContexts);

        levels.Initialize(coefficientBuffer);
        if (componentType == Av1ComponentType.Luminance)
        {
            _ = this.ProcessTransformType<CoefficientWriteOperation>(
                transformType,
                transformSize,
                usesInterTransformSet,
                useReducedTransformSet,
                this.baseQIndex,
                filterIntraMode,
                intraDirection);
        }

        _ = this.ProcessEndOfBlockPosition<CoefficientWriteOperation>(
            endOfBlock,
            componentType,
            transformClass,
            transformSize,
            transformSizeContext);

        Av1SymbolContextHelper.GetNzMapContexts(levels, scan, endOfBlock, transformSize, transformClass, coefficientContexts);
        int limitedTransformSizeContext = Math.Min((int)transformSizeContext, (int)Av1TransformSize.Size32x32);
        ref Av1SymbolWriter w = ref this.writer;
        for (int c = endOfBlock - 1; c >= 0; --c)
        {
            short pos = scan[c];
            int value = coefficientBuffer[pos];
            short coefficientContext = coefficientContexts[pos];
            Point position = levels.GetPosition(pos);
            int level = Math.Abs(value);

            if (c == endOfBlock - 1)
            {
                w.WriteSymbol(
                    Math.Min(level, 3) - 1,
                    this.coefficientsBaseEndOfBlock[(int)transformSizeContext][(int)componentType][coefficientContext]);
            }
            else
            {
                w.WriteSymbol(
                    Math.Min(level, 3),
                    this.coefficientsBase[(int)transformSizeContext][(int)componentType][coefficientContext]);
            }

            if (level > Av1Constants.BaseLevelsCount)
            {
                // Base-range symbols extend levels above the two base levels in fixed-size chunks.
                int baseRange = level - 1 - Av1Constants.BaseLevelsCount;
                int baseRangeContext = Av1SymbolContextHelper.GetBaseRangeContext(levels, position, transformClass);
                for (int idx = 0; idx < Av1Constants.CoefficientBaseRange; idx += Av1Constants.BaseRangeSizeMinus1)
                {
                    int symbol = Math.Min(baseRange - idx, Av1Constants.BaseRangeSizeMinus1);
                    w.WriteSymbol(
                        symbol,
                        this.coefficientsBaseRange[limitedTransformSizeContext][(int)componentType][baseRangeContext]);

                    if (symbol < Av1Constants.BaseRangeSizeMinus1)
                    {
                        break;
                    }
                }
            }
        }

        // Signs follow every magnitude so the DC sign can use its neighboring context and AC signs remain literals.
        int culLevel = 0;
        for (int c = 0; c < endOfBlock; ++c)
        {
            short pos = scan[c];
            int value = coefficientBuffer[pos];
            int level = Math.Abs(value);
            culLevel += level;

            uint sign = value < 0 ? 1u : 0u;
            if (level > 0)
            {
                if (c == 0)
                {
                    w.WriteSymbol(
                        (int)sign,
                        this.dcSign[(int)componentType][transformBlockContext.DcSignContext]);
                }
                else
                {
                    w.WriteLiteral(sign, 1);
                }

                if (level > (Av1Constants.CoefficientBaseRange + Av1Constants.BaseLevelsCount))
                {
                    this.WriteGolomb(
                        level - Av1Constants.CoefficientBaseRange - 1 - Av1Constants.BaseLevelsCount);
                }
            }
        }

        culLevel = Math.Min(Av1Constants.CoefficientContextMask, culLevel);

        // The DC sign is packed above the magnitude bits so adjacent blocks can derive both contexts from one value.
        Av1SymbolContextHelper.SetDcSign(ref culLevel, coefficientBuffer[0]);
        return culLevel;
    }

    /// <summary>
    /// Gets the current fixed-point rate cost of one transform block's complete coefficient syntax.
    /// </summary>
    /// <param name="transformSize">The signaled transform size.</param>
    /// <param name="transformType">The transform type selecting the scan and context class.</param>
    /// <param name="intraDirection">The block's intra prediction mode.</param>
    /// <param name="coefficientBuffer">The raster-ordered signed coefficient levels.</param>
    /// <param name="componentType">The luma or chroma component category.</param>
    /// <param name="transformBlockContext">The neighboring skip and DC sign contexts.</param>
    /// <param name="endOfBlock">The one-based final nonzero scan position, or zero for an empty block.</param>
    /// <param name="useReducedTransformSet">Indicates whether the frame restricts transform choices.</param>
    /// <param name="filterIntraMode">The selected filter-intra mode, or the disabled sentinel.</param>
    /// <param name="usesInterTransformSet">Indicates whether inter rather than intra transform probabilities apply.</param>
    /// <returns>The rate cost in 1/512-bit units.</returns>
    public int GetCoefficientCost(
        Av1TransformSize transformSize,
        Av1TransformType transformType,
        Av1PredictionMode intraDirection,
        ReadOnlySpan<int> coefficientBuffer,
        Av1ComponentType componentType,
        Av1TransformBlockContext transformBlockContext,
        ushort endOfBlock,
        bool useReducedTransformSet,
        Av1FilterIntraMode filterIntraMode,
        bool usesInterTransformSet)
    {
        Av1TransformSize transformSizeContext = Av1SymbolContextHelper.GetTransformSizeContext(transformSize);

        DebugGuard.MustBeLessThan((int)transformSizeContext, (int)Av1TransformSize.AllSizes, nameof(transformSizeContext));

        int rate = this.ProcessTransformBlockSkip<CoefficientCostOperation>(
            endOfBlock == 0,
            transformSizeContext,
            transformBlockContext.SkipContext);

        if (endOfBlock == 0)
        {
            return rate;
        }

        Av1TransformSize adjustedTransformSize = transformSize.GetAdjusted();
        int width = adjustedTransformSize.GetWidth();
        int height = adjustedTransformSize.GetHeight();
        Av1TransformClass transformClass = transformType.ToClass();
        ReadOnlySpan<short> scan = Av1ScanOrderConstants.GetScanOrder(transformSize, transformType).Scan;
        bool needsLevelMap = endOfBlock > 1;
        Av1LevelBuffer levels = this.PrepareCoefficientScratch(
            width,
            height,
            needsLevelMap,
            out Span<sbyte> coefficientContexts);

        // The final coefficient uses scan-position contexts only. Earlier coefficients need the complete
        // forward-neighbor level map, so a one-coefficient candidate avoids initializing that plane.
        if (needsLevelMap)
        {
            levels.Initialize(coefficientBuffer);
        }

        if (componentType == Av1ComponentType.Luminance)
        {
            rate += this.GetTransformTypeCost(
                transformType,
                transformSize,
                useReducedTransformSet,
                this.baseQIndex,
                filterIntraMode,
                intraDirection,
                usesInterTransformSet);
        }

        rate += this.ProcessEndOfBlockPosition<CoefficientCostOperation>(
            endOfBlock,
            componentType,
            transformClass,
            transformSize,
            transformSizeContext);

        Av1SymbolContextHelper.GetNzMapContexts(levels, scan, endOfBlock, transformSize, transformClass, coefficientContexts);
        int limitedTransformSizeContext = Math.Min((int)transformSizeContext, (int)Av1TransformSize.Size32x32);
        int c = endOfBlock - 1;
        int pos = scan[c];
        int value = coefficientBuffer[pos];
        int level = Math.Abs(value);
        int coefficientContext = coefficientContexts[pos];
        rate += Av1ProbabilityCost.GetSymbolCost(
            this.coefficientsBaseEndOfBlock[(int)transformSizeContext][(int)componentType][coefficientContext],
            Math.Min(level, 3) - 1);

        if (level > Av1Constants.BaseLevelsCount)
        {
            int baseRangeContext = Av1SymbolContextHelper.GetBaseRangeContextEndOfBlock(
                levels.GetPosition(pos),
                transformClass);

            rate += GetBaseRangeCost(
                level,
                this.coefficientsBaseRange[limitedTransformSizeContext][(int)componentType][baseRangeContext]);
        }

        if (c == 0)
        {
            return rate + Av1ProbabilityCost.GetSymbolCost(
                this.dcSign[(int)componentType][transformBlockContext.DcSignContext],
                value < 0 ? 1 : 0);
        }

        rate += Av1ProbabilityCost.GetLiteralCost(1);
        for (c = endOfBlock - 2; c >= 1; --c)
        {
            pos = scan[c];
            value = coefficientBuffer[pos];
            level = Math.Abs(value);
            coefficientContext = coefficientContexts[pos];
            rate += Av1ProbabilityCost.GetSymbolCost(
                this.coefficientsBase[(int)transformSizeContext][(int)componentType][coefficientContext],
                Math.Min(level, 3));

            if (level == 0)
            {
                continue;
            }

            rate += Av1ProbabilityCost.GetLiteralCost(1);
            if (level > Av1Constants.BaseLevelsCount)
            {
                int baseRangeContext = Av1SymbolContextHelper.GetBaseRangeContext(
                    levels,
                    levels.GetPosition(pos),
                    transformClass);

                rate += GetBaseRangeCost(
                    level,
                    this.coefficientsBaseRange[limitedTransformSizeContext][(int)componentType][baseRangeContext]);
            }
        }

        pos = scan[0];
        value = coefficientBuffer[pos];
        level = Math.Abs(value);
        coefficientContext = coefficientContexts[pos];
        rate += Av1ProbabilityCost.GetSymbolCost(
            this.coefficientsBase[(int)transformSizeContext][(int)componentType][coefficientContext],
            Math.Min(level, 3));

        if (level > 0)
        {
            rate += Av1ProbabilityCost.GetSymbolCost(
                this.dcSign[(int)componentType][transformBlockContext.DcSignContext],
                value < 0 ? 1 : 0);

            if (level > Av1Constants.BaseLevelsCount)
            {
                int baseRangeContext = Av1SymbolContextHelper.GetBaseRangeContext(
                    levels,
                    levels.GetPosition(pos),
                    transformClass);

                rate += GetBaseRangeCost(
                    level,
                    this.coefficientsBaseRange[limitedTransformSizeContext][(int)componentType][baseRangeContext]);
            }
        }

        return rate;
    }

    private Av1LevelBuffer PrepareCoefficientScratch(
        int width,
        int height,
        bool clearLevels,
        out Span<sbyte> coefficientContexts)
    {
        // AV1 omits high-frequency coefficients beyond 32 samples on every 64-point transform dimension. The tile
        // creates maximum-sized workspaces once, then changes only the active views for subsequent transform blocks.
        this.levels.Reset(new Size(width, height), clearLevels);
        coefficientContexts = this.coefficientContexts.Memory.Span[..(width * height)];
        return this.levels;
    }

    /// <summary>
    /// Writes an end-of-block token and its context-coded and literal suffix bits.
    /// </summary>
    /// <param name="endOfBlock">The one-based final nonzero scan position.</param>
    /// <param name="componentType">The luma or chroma component category.</param>
    /// <param name="transformClass">The transform direction class.</param>
    /// <param name="transformSize">The signaled transform size selecting the token alphabet.</param>
    /// <param name="transformSizeContext">The square transform-size probability context.</param>
    public void WriteEndOfBlockPosition(ushort endOfBlock, Av1ComponentType componentType, Av1TransformClass transformClass, Av1TransformSize transformSize, Av1TransformSize transformSizeContext)
    {
        _ = this.ProcessEndOfBlockPosition<CoefficientWriteOperation>(
            endOfBlock,
            componentType,
            transformClass,
            transformSize,
            transformSizeContext);
    }

    private int ProcessEndOfBlockPosition<TOperation>(
        ushort endOfBlock,
        Av1ComponentType componentType,
        Av1TransformClass transformClass,
        Av1TransformSize transformSize,
        Av1TransformSize transformSizeContext)
        where TOperation : struct, ICoefficientSymbolOperation
    {
        short endOfBlockPosition = Av1SymbolContextHelper.GetEndOfBlockPosition(endOfBlock, out int eobExtra);
        int rate = this.ProcessEndOfBlockFlag<TOperation>(
            componentType,
            transformClass,
            transformSize,
            endOfBlockPosition);

        int eobOffsetBitCount = Av1SymbolContextHelper.EndOfBlockOffsetBits[endOfBlockPosition];
        if (eobOffsetBitCount > 0)
        {
            ref Av1SymbolWriter w = ref this.writer;
            int eobShift = eobOffsetBitCount - 1;
            int bit = Av1Math.GetBit(eobExtra, eobShift);

            // The local table retains placeholders for the first three tokens, unlike the reference decoder's compact table,
            // so the encoded token is also the distribution index.
            int endOfBlockContext = endOfBlockPosition;
            rate += TOperation.ProcessSymbol(
                ref w,
                bit,
                this.endOfBlockExtra[(int)transformSizeContext][(int)componentType][endOfBlockContext]);

            // The context-coded high bit has already been consumed. The literal writer emits the remaining
            // low-order suffix most-significant-bit first, preserving the AV1 syntax with one traversal call.
            rate += TOperation.ProcessLiteral(ref w, (uint)eobExtra, eobOffsetBitCount - 1);
        }

        return rate;
    }

    /// <summary>
    /// Gets the current fixed-point cost of the transform-block skip flag.
    /// </summary>
    /// <param name="skip">Indicates whether the transform block is empty.</param>
    /// <param name="transformSizeContext">The square transform-size probability context.</param>
    /// <param name="skipContext">The context derived from neighboring coefficient blocks.</param>
    /// <returns>The rate cost in 1/512-bit units.</returns>
    public int GetTransformBlockSkipCost(bool skip, Av1TransformSize transformSizeContext, int skipContext)
        => this.ProcessTransformBlockSkip<CoefficientCostOperation>(skip, transformSizeContext, skipContext);

    /// <summary>
    /// Writes whether a transform block has no coded coefficients.
    /// </summary>
    /// <param name="skip">Indicates whether the transform block is empty.</param>
    /// <param name="transformSizeContext">The square transform-size probability context.</param>
    /// <param name="skipContext">The context derived from neighboring coefficient blocks.</param>
    public void WriteTransformBlockSkip(bool skip, Av1TransformSize transformSizeContext, int skipContext)
    {
        _ = this.ProcessTransformBlockSkip<CoefficientWriteOperation>(skip, transformSizeContext, skipContext);
    }

    private int ProcessTransformBlockSkip<TOperation>(
        bool skip,
        Av1TransformSize transformSizeContext,
        int skipContext)
        where TOperation : struct, ICoefficientSymbolOperation
    {
        ref Av1SymbolWriter w = ref this.writer;
        return TOperation.ProcessSymbol(
            ref w,
            skip ? 1 : 0,
            this.transformBlockSkip[(int)transformSizeContext][skipContext]);
    }

    /// <summary>
    /// Gets the current fixed-point cost of a transform-size subdivision depth.
    /// </summary>
    /// <param name="blockSize">The block size defining the maximum transform.</param>
    /// <param name="transformSize">The selected transform size.</param>
    /// <param name="context">The neighboring transform-size context.</param>
    /// <returns>The rate cost in 1/512-bit units.</returns>
    public int GetTransformSizeCost(Av1BlockSize blockSize, Av1TransformSize transformSize, int context)
    {
        int selectedDepth = GetTransformSizeDepth(blockSize, transformSize, out int categoryDepth);
        return Av1ProbabilityCost.GetSymbolCost(this.transformSize[categoryDepth - 1][context], selectedDepth);
    }

    /// <summary>
    /// Writes the selected transform size as its subdivision depth from the block maximum.
    /// </summary>
    /// <param name="blockSize">The block size defining the maximum transform.</param>
    /// <param name="transformSize">The selected transform size.</param>
    /// <param name="context">The neighboring transform-size context.</param>
    public void WriteTransformSize(Av1BlockSize blockSize, Av1TransformSize transformSize, int context)
    {
        int selectedDepth = GetTransformSizeDepth(blockSize, transformSize, out int categoryDepth);
        ref Av1SymbolWriter w = ref this.writer;
        w.WriteSymbol(selectedDepth, this.transformSize[categoryDepth - 1][context]);
    }

    /// <summary>
    /// Gets the current fixed-point cost of one variable-transform partition decision.
    /// </summary>
    /// <param name="split">Indicates whether the current transform node is split.</param>
    /// <param name="context">The neighboring variable-transform context.</param>
    /// <returns>The rate cost in 1/512-bit units.</returns>
    public int GetTransformPartitionCost(bool split, int context)
        => Av1ProbabilityCost.GetSymbolCost(this.transformPartition[context], split ? 1 : 0);

    /// <summary>
    /// Writes one variable-transform partition decision.
    /// </summary>
    /// <param name="split">Indicates whether the current transform node is split.</param>
    /// <param name="context">The neighboring variable-transform context.</param>
    public void WriteTransformPartition(bool split, int context)
    {
        ref Av1SymbolWriter w = ref this.writer;
        w.WriteSymbol(split ? 1 : 0, this.transformPartition[context]);
    }

    private static int GetTransformSizeDepth(
        Av1BlockSize blockSize,
        Av1TransformSize transformSize,
        out int categoryDepth)
    {
        Av1TransformSize maximumTransformSize = blockSize.GetMaximumTransformSize();
        Av1TransformSize currentTransformSize = maximumTransformSize;
        categoryDepth = 0;
        while (currentTransformSize != Av1TransformSize.Size4x4)
        {
            categoryDepth++;
            currentTransformSize = currentTransformSize.GetSubSize();
        }

        int selectedDepth = 0;
        currentTransformSize = maximumTransformSize;
        while (currentTransformSize != transformSize && selectedDepth < Av1Constants.MaxVarTransform)
        {
            selectedDepth++;
            currentTransformSize = currentTransformSize.GetSubSize();
        }

        DebugGuard.IsTrue(currentTransformSize == transformSize, nameof(transformSize));
        return selectedDepth;
    }

    /// <summary>
    /// Finalizes the range-coded tile payload and returns an owned exact-length copy.
    /// </summary>
    /// <returns>The memory owner containing the encoded tile bytes.</returns>
    public IMemoryOwner<byte> Exit()
        => this.writer.Exit();

    /// <summary>
    /// Finalizes the range-coded tile payload and exposes its encoded prefix without copying.
    /// </summary>
    /// <param name="length">The number of encoded bytes in the returned memory.</param>
    /// <returns>The encoded prefix, valid until this encoder is reset or disposed.</returns>
    public ReadOnlyMemory<byte> Exit(out int length)
        => this.writer.Exit(out length);

    /// <summary>
    /// Exposes a prefix containing every consecutively encoded tile without copying their bytes.
    /// </summary>
    /// <param name="length">The number of bytes in the prefix.</param>
    /// <returns>The encoded prefix, valid until this encoder is reset or disposed.</returns>
    public ReadOnlyMemory<byte> GetOutput(int length)
        => this.writer.GetOutput(length);

    /// <summary>
    /// Releases the range-coder output buffer and coefficient scratch memory.
    /// </summary>
    public void Dispose()
    {
        if (!this.isDisposed)
        {
            this.coefficientContexts.Dispose();
            this.levels.Dispose();
            this.writer.Dispose();
            this.isDisposed = true;
        }
    }

    /// <summary>
    /// Writes the unsigned exponential-Golomb suffix used for coefficient levels beyond the base range.
    /// </summary>
    /// <param name="level">The nonnegative suffix value.</param>
    public void WriteGolomb(int level)
    {
        uint x = (uint)level + 1u;
        int length = GetGolombBitLength(level);
        ref Av1SymbolWriter w = ref this.writer;
        w.WriteLiteral(0u, length - 1);
        w.WriteLiteral(x, length);
    }

    private static int GetBaseRangeCost(int level, Av1Distribution distribution)
    {
        int baseRange = Math.Min(
            level - 1 - Av1Constants.BaseLevelsCount,
            Av1Constants.CoefficientBaseRange);

        int fullChunkCount = baseRange / Av1Constants.BaseRangeSizeMinus1;
        int rate = 0;
        if (fullChunkCount > 0)
        {
            rate = fullChunkCount * Av1ProbabilityCost.GetSymbolCost(
                distribution,
                Av1Constants.BaseRangeSizeMinus1);
        }

        // A partial range ends with its remainder symbol. Reaching the complete base range consumes four
        // maximum symbols and has no terminating remainder before the Golomb escape.
        if (baseRange < Av1Constants.CoefficientBaseRange)
        {
            int remainder = baseRange - (fullChunkCount * Av1Constants.BaseRangeSizeMinus1);
            rate += Av1ProbabilityCost.GetSymbolCost(distribution, remainder);
        }

        if (level > (Av1Constants.CoefficientBaseRange + Av1Constants.BaseLevelsCount))
        {
            int golombValue = level - Av1Constants.CoefficientBaseRange - 1 - Av1Constants.BaseLevelsCount;
            int length = GetGolombBitLength(golombValue);
            rate += Av1ProbabilityCost.GetLiteralCost((2 * length) - 1);
        }

        return rate;
    }

    private static int GetGolombBitLength(int level) => (int)Av1Math.Log2_32((uint)level + 1u) + 1;

    /// <summary>
    /// Writes the end-of-block token for a transform coefficient-count category.
    /// </summary>
    /// <param name="componentType">The luma or chroma component category.</param>
    /// <param name="transformClass">The transform direction class.</param>
    /// <param name="transformSize">The signaled transform size.</param>
    /// <param name="endOfBlockPosition">The one-based end-of-block token.</param>
    private int ProcessEndOfBlockFlag<TOperation>(
        Av1ComponentType componentType,
        Av1TransformClass transformClass,
        Av1TransformSize transformSize,
        int endOfBlockPosition)
        where TOperation : struct, ICoefficientSymbolOperation
    {
        int endOfBlockMultiSize = transformSize.GetLog2Minus4();
        int endOfBlockContext = transformClass == Av1TransformClass.Class2D ? 0 : 1;
        ref Av1SymbolWriter w = ref this.writer;
        return TOperation.ProcessSymbol(
            ref w,
            endOfBlockPosition - 1,
            this.endOfBlockFlag[endOfBlockMultiSize][(int)componentType][endOfBlockContext]);
    }

    /// <summary>
    /// Gets the current fixed-point rate cost of a transform type when the permitted transform set contains multiple choices.
    /// </summary>
    /// <param name="transformType">The transform type to cost.</param>
    /// <param name="transformSize">The signaled transform size.</param>
    /// <param name="useReducedTransformSet">Indicates whether the frame restricts transform choices.</param>
    /// <param name="baseQIndex">The active base quantizer index.</param>
    /// <param name="filterIntraMode">The filter-intra mode when enabled.</param>
    /// <param name="intraDirection">The ordinary intra prediction mode.</param>
    /// <param name="usesInterTransformSet">Indicates whether inter rather than intra transform probabilities apply.</param>
    /// <returns>The rate cost in 1/512-bit units.</returns>
    public int GetTransformTypeCost(
        Av1TransformType transformType,
        Av1TransformSize transformSize,
        bool useReducedTransformSet,
        int baseQIndex,
        Av1FilterIntraMode filterIntraMode,
        Av1PredictionMode intraDirection,
        bool usesInterTransformSet)
        => this.ProcessTransformType<CoefficientCostOperation>(
            transformType,
            transformSize,
            usesInterTransformSet,
            useReducedTransformSet,
            baseQIndex,
            filterIntraMode,
            intraDirection);

    /// <summary>
    /// Writes a transform type when the permitted transform set contains multiple choices.
    /// </summary>
    /// <param name="transformType">The transform type to encode.</param>
    /// <param name="transformSize">The signaled transform size.</param>
    /// <param name="useReducedTransformSet">Indicates whether the frame restricts transform choices.</param>
    /// <param name="baseQIndex">The active base quantizer index.</param>
    /// <param name="filterIntraMode">The filter-intra mode when enabled.</param>
    /// <param name="intraDirection">The ordinary intra prediction mode.</param>
    /// <param name="usesInterTransformSet">Indicates whether inter rather than intra transform probabilities apply.</param>
    public void WriteTransformType(
        Av1TransformType transformType,
        Av1TransformSize transformSize,
        bool useReducedTransformSet,
        int baseQIndex,
        Av1FilterIntraMode filterIntraMode,
        Av1PredictionMode intraDirection,
        bool usesInterTransformSet)
    {
        _ = this.ProcessTransformType<CoefficientWriteOperation>(
            transformType,
            transformSize,
            usesInterTransformSet,
            useReducedTransformSet,
            baseQIndex,
            filterIntraMode,
            intraDirection);
    }

    private int ProcessTransformType<TOperation>(
        Av1TransformType transformType,
        Av1TransformSize transformSize,
        bool usesInterTransformSet,
        bool useReducedTransformSet,
        int baseQIndex,
        Av1FilterIntraMode filterIntraMode,
        Av1PredictionMode intraDirection)
        where TOperation : struct, ICoefficientSymbolOperation
    {
        Av1TransformSetType transformSetType = Av1SymbolContextHelper.GetExtendedTransformSetType(
            transformSize,
            usesInterTransformSet,
            useReducedTransformSet);

        if (Av1SymbolContextHelper.GetExtendedTransformTypeCount(transformSetType) > 1 && baseQIndex > 0)
        {
            Av1TransformSize squareTransformSize = transformSize.GetSquareSize();
            DebugGuard.MustBeLessThanOrEqualTo((int)squareTransformSize, Av1Constants.ExtendedTransformCount, nameof(squareTransformSize));

            int extendedSet = Av1SymbolContextHelper.GetExtendedTransformSet(transformSetType, usesInterTransformSet);

            // Set zero contains only DCT-DCT, which was excluded by the multiple-choice condition above.
            DebugGuard.MustBeGreaterThan(extendedSet, 0, nameof(extendedSet));

            int transformIndex = Av1SymbolContextHelper.GetExtendedTransformIndex(transformSetType, transformType);
            ref Av1SymbolWriter w = ref this.writer;
            if (usesInterTransformSet)
            {
                // Inter transforms are conditioned only by the transform set and square size.
                return TOperation.ProcessSymbol(
                    ref w,
                    transformIndex,
                    this.interExtendedTransform[extendedSet][(int)squareTransformSize]);
            }

            Av1PredictionMode intraDirectionContext;
            if (filterIntraMode != Av1FilterIntraMode.AllFilterIntraModes)
            {
                intraDirectionContext = filterIntraMode.ToIntraDirection();
            }
            else
            {
                intraDirectionContext = intraDirection;
            }

            DebugGuard.MustBeLessThan((int)intraDirectionContext, 13, nameof(intraDirectionContext));
            DebugGuard.MustBeLessThan((int)squareTransformSize, 4, nameof(squareTransformSize));
            return TOperation.ProcessSymbol(
                ref w,
                transformIndex,
                this.intraExtendedTransform[extendedSet][(int)squareTransformSize][(int)intraDirectionContext]);
        }

        return 0;
    }

    /// <summary>
    /// Writes a spatially predicted segment identifier.
    /// </summary>
    /// <param name="segmentId">The segment identifier.</param>
    /// <param name="context">The context derived from neighboring segment identifiers.</param>
    public void WriteSegmentId(int segmentId, int context)
    {
        ref Av1SymbolWriter w = ref this.writer;
        w.WriteSymbol(segmentId, this.segmentId[context]);
    }

    /// <summary>
    /// Gets the current fixed-point cost of the transform-skip flag.
    /// </summary>
    /// <param name="skip">Indicates whether the block contains no coded transform coefficients.</param>
    /// <param name="context">The neighboring skip context.</param>
    /// <returns>The rate cost in 1/512-bit units.</returns>
    public int GetSkipCost(bool skip, int context)
        => Av1ProbabilityCost.GetSymbolCost(this.skip[context], skip ? 1 : 0);

    /// <summary>
    /// Writes the transform-skip flag from a neighboring skip context.
    /// </summary>
    /// <param name="skip">Indicates whether the block contains no coded transform coefficients.</param>
    /// <param name="context">The neighboring skip context.</param>
    public void WriteSkip(bool skip, int context)
    {
        ref Av1SymbolWriter w = ref this.writer;
        w.WriteSymbol(skip, this.skip[context]);
    }

    /// <summary>
    /// Writes the compound-reference skip-mode flag.
    /// </summary>
    /// <param name="skip">Indicates whether skip mode is selected.</param>
    /// <param name="context">The neighboring skip-mode context.</param>
    public void WriteSkipMode(bool skip, int context)
    {
        ref Av1SymbolWriter w = ref this.writer;
        w.WriteSymbol(skip, this.skipMode[context]);
    }

    /// <summary>
    /// Gets the current fixed-point cost of the filter-intra enable flag and selected mode.
    /// </summary>
    /// <param name="filterIntraMode">The selected filter-intra mode, or the disabled sentinel.</param>
    /// <param name="blockSize">The block size selecting the enable distribution.</param>
    /// <returns>The rate cost in 1/512-bit units.</returns>
    public int GetFilterIntraModeCost(Av1FilterIntraMode filterIntraMode, Av1BlockSize blockSize)
    {
        bool useFilter = filterIntraMode != Av1FilterIntraMode.AllFilterIntraModes;
        int cost = Av1ProbabilityCost.GetSymbolCost(this.filterIntra[(int)blockSize], useFilter ? 1 : 0);
        if (useFilter)
        {
            cost += Av1ProbabilityCost.GetSymbolCost(this.filterIntraMode, (int)filterIntraMode);
        }

        return cost;
    }

    /// <summary>
    /// Writes the filter-intra enable flag and, when enabled, its prediction mode.
    /// </summary>
    /// <param name="filterIntraMode">The selected filter-intra mode, or the disabled sentinel.</param>
    /// <param name="blockSize">The block size selecting the enable distribution.</param>
    public void WriteFilterIntraMode(Av1FilterIntraMode filterIntraMode, Av1BlockSize blockSize)
    {
        ref Av1SymbolWriter w = ref this.writer;
        bool useFilter = filterIntraMode != Av1FilterIntraMode.AllFilterIntraModes;
        w.WriteSymbol(useFilter, this.filterIntra[(int)blockSize]);
        if (useFilter)
        {
            w.WriteSymbol((int)filterIntraMode, this.filterIntraMode);
        }
    }

    /// <summary>
    /// Writes a signed quantizer-index delta value.
    /// </summary>
    /// <param name="deltaQindex">The signed quantizer-index delta.</param>
    public void WriteDeltaQuantizerIndex(int deltaQindex)
    {
        ref Av1SymbolWriter w = ref this.writer;
        bool sign = deltaQindex < 0;
        int abs = Math.Abs(deltaQindex);
        bool isSmallValue = abs < Av1Constants.DeltaQuantizerSmall;

        w.WriteSymbol(Math.Min(abs, Av1Constants.DeltaQuantizerSmall), this.deltaQuantizerAbsolute);

        if (!isSmallValue)
        {
            // Escape magnitudes encode their bit width first, followed by the offset within that width's range.
            int remainingBitCount = Av1Math.MostSignificantBit((uint)(abs - 1));
            int threshold = (1 << remainingBitCount) + 1;
            w.WriteLiteral((uint)(remainingBitCount - 1), 3);
            w.WriteLiteral((uint)(abs - threshold), remainingBitCount);
        }

        if (abs > 0)
        {
            w.WriteLiteral(sign);
        }
    }

    /// <summary>
    /// Gets the current fixed-point cost of a key-frame luma prediction mode.
    /// </summary>
    /// <param name="lumaMode">The luma prediction mode.</param>
    /// <param name="topContext">The reduced above-mode context.</param>
    /// <param name="leftContext">The reduced left-mode context.</param>
    /// <returns>The rate cost in 1/512-bit units.</returns>
    public int GetLumaModeCost(Av1PredictionMode lumaMode, byte topContext, byte leftContext)
        => Av1ProbabilityCost.GetSymbolCost(
            this.keyFrameYMode[topContext][leftContext],
            (int)lumaMode);

    /// <summary>
    /// Writes a key-frame luma prediction mode using the above and left mode contexts.
    /// </summary>
    /// <param name="lumaMode">The luma prediction mode.</param>
    /// <param name="topContext">The reduced above-mode context.</param>
    /// <param name="leftContext">The reduced left-mode context.</param>
    public void WriteLumaMode(Av1PredictionMode lumaMode, byte topContext, byte leftContext)
    {
        ref Av1SymbolWriter w = ref this.writer;
        w.WriteSymbol((int)lumaMode, this.keyFrameYMode[topContext][leftContext]);
    }

    /// <summary>
    /// Gets the cost of an intra luma mode coded inside an inter frame.
    /// </summary>
    /// <param name="lumaMode">The intra luma mode.</param>
    /// <param name="blockSize">The coding block size selecting the size group.</param>
    /// <returns>The syntax cost in 1/512-bit units.</returns>
    public int GetInterFrameLumaModeCost(Av1PredictionMode lumaMode, Av1BlockSize blockSize)
        => Av1ProbabilityCost.GetSymbolCost(this.frameYMode[blockSize.GetSizeGroup()], (int)lumaMode);

    /// <summary>
    /// Writes an intra luma mode coded inside an inter frame.
    /// </summary>
    /// <param name="lumaMode">The intra luma mode.</param>
    /// <param name="blockSize">The coding block size selecting the size group.</param>
    public void WriteInterFrameLumaMode(Av1PredictionMode lumaMode, Av1BlockSize blockSize)
    {
        ref Av1SymbolWriter w = ref this.writer;
        w.WriteSymbol((int)lumaMode, this.frameYMode[blockSize.GetSizeGroup()]);
    }

    /// <summary>
    /// Gets the cost of the prediction-domain decision for an inter-frame block.
    /// </summary>
    /// <param name="isInter">Whether the block uses a retained reference frame.</param>
    /// <param name="context">The neighboring prediction-domain context.</param>
    /// <returns>The syntax cost in 1/512-bit units.</returns>
    public int GetIsInterCost(bool isInter, int context)
        => Av1ProbabilityCost.GetSymbolCost(this.intraInter[context], isInter ? 1 : 0);

    /// <summary>
    /// Writes the prediction-domain decision for an inter-frame block.
    /// </summary>
    /// <param name="isInter">Whether the block uses a retained reference frame.</param>
    /// <param name="context">The neighboring prediction-domain context.</param>
    public void WriteIsInter(bool isInter, int context)
    {
        ref Av1SymbolWriter w = ref this.writer;
        w.WriteSymbol(isInter, this.intraInter[context]);
    }

    /// <summary>
    /// Gets the cost of selecting one reference from the single-reference branch tree.
    /// </summary>
    /// <param name="referenceFrame">The selected reference-frame label.</param>
    /// <param name="referenceCounts">The neighboring reference counts indexed by reference-frame label.</param>
    /// <returns>The syntax cost in 1/512-bit units.</returns>
    public int GetSingleReferenceCost(
        Av1ReferenceFrameType referenceFrame,
        ReadOnlySpan<byte> referenceCounts)
    {
        bool isBackward = referenceFrame >= Av1ReferenceFrameType.Backward;
        int context = Av1SymbolContextHelper.GetSingleReferenceBackwardContext(referenceCounts);
        int rate = Av1ProbabilityCost.GetSymbolCost(this.singleReference[context][0], isBackward ? 1 : 0);
        if (isBackward)
        {
            bool isAlternate = referenceFrame == Av1ReferenceFrameType.Alternate;
            context = Av1SymbolContextHelper.GetSingleReferenceAlternateContext(referenceCounts);
            rate += Av1ProbabilityCost.GetSymbolCost(this.singleReference[context][1], isAlternate ? 1 : 0);
            if (isAlternate)
            {
                return rate;
            }

            context = Av1SymbolContextHelper.GetSingleReferenceAlternate2Context(referenceCounts);
            return rate + Av1ProbabilityCost.GetSymbolCost(
                this.singleReference[context][5],
                referenceFrame == Av1ReferenceFrameType.Alternate2 ? 1 : 0);
        }

        bool isLast3OrGolden = referenceFrame is Av1ReferenceFrameType.Last3 or Av1ReferenceFrameType.Golden;
        context = Av1SymbolContextHelper.GetSingleReferenceLast3OrGoldenContext(referenceCounts);
        rate += Av1ProbabilityCost.GetSymbolCost(this.singleReference[context][2], isLast3OrGolden ? 1 : 0);
        if (isLast3OrGolden)
        {
            context = Av1SymbolContextHelper.GetSingleReferenceGoldenContext(referenceCounts);
            return rate + Av1ProbabilityCost.GetSymbolCost(
                this.singleReference[context][4],
                referenceFrame == Av1ReferenceFrameType.Golden ? 1 : 0);
        }

        context = Av1SymbolContextHelper.GetSingleReferenceLast2Context(referenceCounts);
        return rate + Av1ProbabilityCost.GetSymbolCost(
            this.singleReference[context][3],
            referenceFrame == Av1ReferenceFrameType.Last2 ? 1 : 0);
    }

    /// <summary>
    /// Writes one reference through the single-reference branch tree.
    /// </summary>
    /// <param name="referenceFrame">The selected reference-frame label.</param>
    /// <param name="referenceCounts">The neighboring reference counts indexed by reference-frame label.</param>
    public void WriteSingleReference(
        Av1ReferenceFrameType referenceFrame,
        ReadOnlySpan<byte> referenceCounts)
    {
        ref Av1SymbolWriter w = ref this.writer;
        bool isBackward = referenceFrame >= Av1ReferenceFrameType.Backward;
        int context = Av1SymbolContextHelper.GetSingleReferenceBackwardContext(referenceCounts);
        w.WriteSymbol(isBackward, this.singleReference[context][0]);
        if (isBackward)
        {
            bool isAlternate = referenceFrame == Av1ReferenceFrameType.Alternate;
            context = Av1SymbolContextHelper.GetSingleReferenceAlternateContext(referenceCounts);
            w.WriteSymbol(isAlternate, this.singleReference[context][1]);
            if (isAlternate)
            {
                return;
            }

            context = Av1SymbolContextHelper.GetSingleReferenceAlternate2Context(referenceCounts);
            w.WriteSymbol(
                referenceFrame == Av1ReferenceFrameType.Alternate2,
                this.singleReference[context][5]);

            return;
        }

        bool isLast3OrGolden = referenceFrame is Av1ReferenceFrameType.Last3 or Av1ReferenceFrameType.Golden;
        context = Av1SymbolContextHelper.GetSingleReferenceLast3OrGoldenContext(referenceCounts);
        w.WriteSymbol(isLast3OrGolden, this.singleReference[context][2]);
        if (isLast3OrGolden)
        {
            context = Av1SymbolContextHelper.GetSingleReferenceGoldenContext(referenceCounts);
            w.WriteSymbol(
                referenceFrame == Av1ReferenceFrameType.Golden,
                this.singleReference[context][4]);

            return;
        }

        context = Av1SymbolContextHelper.GetSingleReferenceLast2Context(referenceCounts);
        w.WriteSymbol(
            referenceFrame == Av1ReferenceFrameType.Last2,
            this.singleReference[context][3]);
    }

    /// <summary>
    /// Gets the current fixed-point cost of a directional angle-delta symbol.
    /// </summary>
    /// <param name="angleDelta">The signed angle delta offset by <see cref="Av1Constants.MaxAngleDelta"/>.</param>
    /// <param name="context">The directional prediction mode selecting the distribution.</param>
    /// <returns>The rate cost in 1/512-bit units.</returns>
    public int GetAngleDeltaCost(int angleDelta, Av1PredictionMode context)
        => Av1ProbabilityCost.GetSymbolCost(
            this.angleDelta[context - Av1PredictionMode.Vertical],
            angleDelta);

    /// <summary>
    /// Writes an unsigned directional angle-delta symbol.
    /// </summary>
    /// <param name="angleDelta">The signed angle delta offset by <see cref="Av1Constants.MaxAngleDelta"/>.</param>
    /// <param name="context">The directional prediction mode selecting the distribution.</param>
    public void WriteAngleDelta(int angleDelta, Av1PredictionMode context)
    {
        ref Av1SymbolWriter w = ref this.writer;
        w.WriteSymbol(angleDelta, this.angleDelta[context - Av1PredictionMode.Vertical]);
    }

    /// <summary>
    /// Writes a fixed-width CDEF strength index.
    /// </summary>
    /// <param name="cdefStrength">The CDEF strength index.</param>
    /// <param name="bitCount">The number of signaled bits.</param>
    public void WriteCdefStrength(int cdefStrength, int bitCount)
    {
        ref Av1SymbolWriter w = ref this.writer;
        w.WriteLiteral((uint)cdefStrength, bitCount);
    }

    /// <summary>
    /// Gets the current fixed-point cost of a chroma intra prediction mode.
    /// </summary>
    /// <param name="chromaMode">The chroma prediction mode.</param>
    /// <param name="isChromaFromLumaAllowed">Indicates whether chroma-from-luma is valid for the block.</param>
    /// <param name="lumaMode">The block's luma prediction mode.</param>
    /// <returns>The rate cost in 1/512-bit units.</returns>
    public int GetChromaModeCost(Av1ChromaPredictionMode chromaMode, bool isChromaFromLumaAllowed, Av1PredictionMode lumaMode)
    {
        int cflAllowed = isChromaFromLumaAllowed ? 1 : 0;
        return Av1ProbabilityCost.GetSymbolCost(this.uvMode[cflAllowed][(int)lumaMode], (int)chromaMode);
    }

    /// <summary>
    /// Gets the current fixed-point cost of joint chroma-from-luma alpha syntax.
    /// </summary>
    /// <param name="chromaFromLumaIndex">The packed U/V alpha-magnitude indices.</param>
    /// <param name="joinedSign">The joint U/V sign symbol.</param>
    /// <returns>The rate cost in 1/512-bit units.</returns>
    public int GetChromaFromLumaCost(int chromaFromLumaIndex, int joinedSign)
    {
        int cost = Av1ProbabilityCost.GetSymbolCost(this.chromaFromLumaSign, joinedSign);
        int signU = Av1ChromaFromLumaMath.SignU(joinedSign);
        if (signU != Av1ChromaFromLumaMath.SignZero)
        {
            int contextU = Av1ChromaFromLumaMath.ContextU(joinedSign);
            int indexU = Av1ChromaFromLumaMath.IndexU(chromaFromLumaIndex);
            cost += Av1ProbabilityCost.GetSymbolCost(this.chromaFromLumaAlpha[contextU], indexU);
        }

        int signV = Av1ChromaFromLumaMath.SignV(joinedSign);
        if (signV != Av1ChromaFromLumaMath.SignZero)
        {
            int contextV = Av1ChromaFromLumaMath.ContextV(joinedSign);
            int indexV = Av1ChromaFromLumaMath.IndexV(chromaFromLumaIndex);
            cost += Av1ProbabilityCost.GetSymbolCost(this.chromaFromLumaAlpha[contextV], indexV);
        }

        return cost;
    }

    /// <summary>
    /// Writes a chroma intra prediction mode conditioned on the luma mode and chroma-from-luma availability.
    /// </summary>
    /// <param name="chromaMode">The chroma prediction mode.</param>
    /// <param name="isChromaFromLumaAllowed">Indicates whether chroma-from-luma is valid for the block.</param>
    /// <param name="lumaMode">The block's luma prediction mode.</param>
    public void WriteChromaMode(Av1ChromaPredictionMode chromaMode, bool isChromaFromLumaAllowed, Av1PredictionMode lumaMode)
    {
        ref Av1SymbolWriter w = ref this.writer;
        int cflAllowed = isChromaFromLumaAllowed ? 1 : 0;
        w.WriteSymbol((int)chromaMode, this.uvMode[cflAllowed][(int)lumaMode]);
    }

    /// <summary>
    /// Writes the joint chroma-from-luma signs and the magnitude index for each nonzero plane.
    /// </summary>
    /// <param name="chromaFromLumaIndex">The packed U/V alpha-magnitude indices.</param>
    /// <param name="joinedSign">The joint U/V sign symbol.</param>
    public void WriteChromaFromLumaAlphas(int chromaFromLumaIndex, int joinedSign)
    {
        ref Av1SymbolWriter w = ref this.writer;
        w.WriteSymbol(joinedSign, this.chromaFromLumaSign);

        // Magnitudes are only signaled for nonzero signs; the shared helper keeps encoder and decoder mappings exact.
        int signU = Av1ChromaFromLumaMath.SignU(joinedSign);
        if (signU != Av1ChromaFromLumaMath.SignZero)
        {
            int contextU = Av1ChromaFromLumaMath.ContextU(joinedSign);
            int indexU = Av1ChromaFromLumaMath.IndexU(chromaFromLumaIndex);
            w.WriteSymbol(indexU, this.chromaFromLumaAlpha[contextU]);
        }

        int signV = Av1ChromaFromLumaMath.SignV(joinedSign);
        if (signV != Av1ChromaFromLumaMath.SignZero)
        {
            int contextV = Av1ChromaFromLumaMath.ContextV(joinedSign);
            int indexV = Av1ChromaFromLumaMath.IndexV(chromaFromLumaIndex);
            w.WriteSymbol(indexV, this.chromaFromLumaAlpha[contextV]);
        }
    }

    /// <summary>
    /// Traverses a palette color-index map once for either live rate costing or entropy emission.
    /// </summary>
    /// <typeparam name="TOperation">The closed map-symbol operation.</typeparam>
    /// <param name="paletteSize">The number of colors in the palette.</param>
    /// <param name="planeType">The luma or chroma plane class.</param>
    /// <param name="rows">The number of coded map rows.</param>
    /// <param name="columns">The number of coded map columns.</param>
    /// <param name="colorIndexMap">The complete row-addressable color-index map.</param>
    /// <returns>The rate cost in 1/512-bit units, or zero while writing.</returns>
    private int ProcessPaletteColorMap<TOperation>(
        int paletteSize,
        Av1PlaneType planeType,
        int rows,
        int columns,
        Buffer2DRegion<byte> colorIndexMap)
        where TOperation : struct, IPaletteColorMapOperation
    {
        int colorIndex = colorIndexMap.DangerousGetRowSpan(0)[0];
        int cost = TOperation.ProcessFirstIndex(this, paletteSize, colorIndex);
        Span<byte> colorOrder = stackalloc byte[Av1Constants.PaletteMaxSize];
        for (int diagonal = 1; diagonal < rows + columns - 1; diagonal++)
        {
            int firstColumn = Math.Min(diagonal, columns - 1);
            int lastColumn = Math.Max(0, diagonal - rows + 1);
            for (int column = firstColumn; column >= lastColumn; column--)
            {
                int row = diagonal - column;
                colorIndex = colorIndexMap.DangerousGetRowSpan(row)[column];
                int colorContext = Av1PaletteColorMap.GetContext(
                    colorIndexMap,
                    row,
                    column,
                    paletteSize,
                    colorIndex,
                    colorOrder,
                    out int colorOrderIndex);

                cost += TOperation.ProcessColorIndex(
                    this,
                    paletteSize,
                    planeType,
                    colorContext,
                    colorOrderIndex);
            }
        }

        return cost;
    }

    /// <summary>
    /// Separates palette colors selected from the neighbor cache from colors that require literal coding.
    /// </summary>
    /// <param name="colorCache">The sorted unique neighbor colors.</param>
    /// <param name="colors">The sorted palette colors.</param>
    /// <param name="cacheColorFound">The cache-selection flags.</param>
    /// <param name="uncachedColors">The destination for colors absent from the cache.</param>
    /// <returns>The number of uncached colors.</returns>
    private static int IndexColorCache(
        ReadOnlySpan<ushort> colorCache,
        ReadOnlySpan<ushort> colors,
        Span<byte> cacheColorFound,
        Span<ushort> uncachedColors)
    {
        cacheColorFound[..colorCache.Length].Clear();
        Span<byte> inCache = stackalloc byte[Av1Constants.PaletteMaxSize];
        inCache.Clear();

        // Cache-order flags drive the bitstream while palette-order flags preserve the sorted uncached output.
        int cachedColorCount = 0;
        for (int cacheIndex = 0; cacheIndex < colorCache.Length && cachedColorCount < colors.Length; cacheIndex++)
        {
            for (int colorIndex = 0; colorIndex < colors.Length; colorIndex++)
            {
                if (colors[colorIndex] == colorCache[cacheIndex])
                {
                    inCache[colorIndex] = 1;
                    cacheColorFound[cacheIndex] = 1;
                    cachedColorCount++;
                    break;
                }
            }
        }

        int uncachedColorCount = 0;
        for (int colorIndex = 0; colorIndex < colors.Length; colorIndex++)
        {
            if (inCache[colorIndex] == 0)
            {
                uncachedColors[uncachedColorCount++] = colors[colorIndex];
            }
        }

        return uncachedColorCount;
    }

    /// <summary>
    /// Gets the literal length of an ascending palette-color sequence.
    /// </summary>
    /// <param name="colors">The sorted colors.</param>
    /// <param name="bitDepth">The number of bits in each color sample.</param>
    /// <param name="minimumDelta">The minimum representable difference between adjacent colors.</param>
    /// <returns>The literal length in bits.</returns>
    private static int GetDeltaEncodedColorBitCount(
        ReadOnlySpan<ushort> colors,
        int bitDepth,
        int minimumDelta)
    {
        if (colors.IsEmpty)
        {
            return 0;
        }

        int bitCount = bitDepth;
        if (colors.Length == 1)
        {
            return bitCount;
        }

        int maximumDelta = 0;
        for (int i = 1; i < colors.Length; i++)
        {
            maximumDelta = Math.Max(maximumDelta, colors[i] - colors[i - 1]);
        }

        int minimumBits = bitDepth - 3;
        int bits = Math.Max(
            (int)Av1Math.CeilLog2((uint)(maximumDelta + 1 - minimumDelta)),
            minimumBits);

        int range = (1 << bitDepth) - colors[0] - minimumDelta;
        bitCount += 2;
        for (int i = 1; i < colors.Length; i++)
        {
            int delta = colors[i] - colors[i - 1];
            bitCount += bits;
            range -= delta;
            bits = Math.Min(bits, (int)Av1Math.CeilLog2((uint)range));
        }

        return bitCount;
    }

    /// <summary>
    /// Writes an ascending palette-color sequence as one literal followed by bounded deltas.
    /// </summary>
    /// <param name="colors">The sorted colors.</param>
    /// <param name="bitDepth">The number of bits in each color sample.</param>
    /// <param name="minimumDelta">The minimum representable difference between adjacent colors.</param>
    private void WriteDeltaEncodedColors(
        ReadOnlySpan<ushort> colors,
        int bitDepth,
        int minimumDelta)
    {
        if (colors.IsEmpty)
        {
            return;
        }

        this.WriteLiteral(colors[0], bitDepth);
        if (colors.Length == 1)
        {
            return;
        }

        int maximumDelta = 0;
        for (int i = 1; i < colors.Length; i++)
        {
            maximumDelta = Math.Max(maximumDelta, colors[i] - colors[i - 1]);
        }

        int minimumBits = bitDepth - 3;
        int bits = Math.Max(
            (int)Av1Math.CeilLog2((uint)(maximumDelta + 1 - minimumDelta)),
            minimumBits);

        this.WriteLiteral((uint)(bits - minimumBits), 2);
        int range = (1 << bitDepth) - colors[0] - minimumDelta;
        for (int i = 1; i < colors.Length; i++)
        {
            int delta = colors[i] - colors[i - 1];
            this.WriteLiteral((uint)(delta - minimumDelta), bits);
            range -= delta;
            bits = Math.Min(bits, (int)Av1Math.CeilLog2((uint)range));
        }
    }

    /// <summary>
    /// Gets the bit width required by wrapped V-plane palette deltas.
    /// </summary>
    /// <param name="colors">The V-plane colors in U-palette order.</param>
    /// <param name="bitDepth">The number of bits in each color sample.</param>
    /// <param name="zeroCount">The number of deltas that omit a sign bit.</param>
    /// <param name="minimumBits">The minimum permitted delta width.</param>
    /// <returns>The delta width in bits.</returns>
    private static int GetPaletteVDeltaBitCount(
        ReadOnlySpan<ushort> colors,
        int bitDepth,
        out int zeroCount,
        out int minimumBits)
    {
        int sampleRange = 1 << bitDepth;
        int maximumDelta = 0;
        zeroCount = 0;
        minimumBits = bitDepth - 4;
        for (int i = 1; i < colors.Length; i++)
        {
            int delta = Math.Abs(colors[i] - colors[i - 1]);
            int wrappedDelta = Math.Min(delta, sampleRange - delta);
            maximumDelta = Math.Max(maximumDelta, wrappedDelta);
            if (wrappedDelta == 0)
            {
                zeroCount++;
            }
        }

        return Math.Max((int)Av1Math.CeilLog2((uint)(maximumDelta + 1)), minimumBits);
    }

    /// <summary>
    /// Emits coefficient syntax and reports no estimated rate.
    /// </summary>
    private readonly struct CoefficientWriteOperation : ICoefficientSymbolOperation
    {
        public static int ProcessSymbol(
            ref Av1SymbolWriter writer,
            int symbol,
            Av1Distribution distribution)
        {
            writer.WriteSymbol(symbol, distribution);
            return 0;
        }

        public static int ProcessLiteral(
            ref Av1SymbolWriter writer,
            uint value,
            int bitCount)
        {
            writer.WriteLiteral(value, bitCount);
            return 0;
        }
    }

    /// <summary>
    /// Measures coefficient syntax against the live tile distributions without changing them.
    /// </summary>
    private readonly struct CoefficientCostOperation : ICoefficientSymbolOperation
    {
        public static int ProcessSymbol(
            ref Av1SymbolWriter writer,
            int symbol,
            Av1Distribution distribution)
            => Av1ProbabilityCost.GetSymbolCost(distribution, symbol);

        public static int ProcessLiteral(
            ref Av1SymbolWriter writer,
            uint value,
            int bitCount)
            => Av1ProbabilityCost.GetLiteralCost(bitCount);
    }

    /// <summary>
    /// Emits palette-map syntax and reports no estimated rate.
    /// </summary>
    private readonly struct PaletteColorMapWriteOperation : IPaletteColorMapOperation
    {
        public static int ProcessFirstIndex(
            Av1SymbolEncoder encoder,
            int paletteSize,
            int colorIndex)
        {
            encoder.WriteUniform(paletteSize, colorIndex);
            return 0;
        }

        public static int ProcessColorIndex(
            Av1SymbolEncoder encoder,
            int paletteSize,
            Av1PlaneType planeType,
            int colorContext,
            int colorOrderIndex)
        {
            encoder.WritePaletteColorIndex(
                colorOrderIndex,
                paletteSize,
                colorContext,
                planeType);

            return 0;
        }
    }

    /// <summary>
    /// Measures palette-map syntax against the live tile distributions without changing them.
    /// </summary>
    private readonly struct PaletteColorMapCostOperation : IPaletteColorMapOperation
    {
        public static int ProcessFirstIndex(
            Av1SymbolEncoder encoder,
            int paletteSize,
            int colorIndex)
            => GetUniformCost(paletteSize, colorIndex);

        public static int ProcessColorIndex(
            Av1SymbolEncoder encoder,
            int paletteSize,
            Av1PlaneType planeType,
            int colorContext,
            int colorOrderIndex)
            => encoder.GetPaletteColorIndexCost(
                colorOrderIndex,
                paletteSize,
                colorContext,
                planeType);
    }
}

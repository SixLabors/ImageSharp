// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.ChromaFromLuma;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;

/// <summary>
/// Decodes tile syntax elements and transform coefficients from an AV1 entropy-coded bitstream.
/// </summary>
internal ref struct Av1SymbolDecoder
{
    /// <summary>
    /// The independently adaptable distribution graph for the current tile.
    /// </summary>
    private readonly Av1FrameEntropyContext context;

    /// <summary>
    /// The configuration providing temporary coefficient-context memory.
    /// </summary>
    private readonly Configuration configuration;

    /// <summary>
    /// The range decoder over the current tile payload.
    /// </summary>
    private Av1SymbolReader reader;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1SymbolDecoder"/> struct for one AV1 tile.
    /// </summary>
    /// <param name="configuration">The configuration providing temporary memory.</param>
    /// <param name="tileData">The entropy-coded tile payload.</param>
    /// <param name="qIndex">The frame base quantizer index.</param>
    /// <param name="updateCdf">A value indicating whether decoded symbols adapt their tile distributions.</param>
    public Av1SymbolDecoder(Configuration configuration, Span<byte> tileData, int qIndex, bool updateCdf = true)
        : this(configuration, tileData, new Av1FrameEntropyContext(qIndex), updateCdf)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1SymbolDecoder"/> struct over a caller-owned tile entropy
    /// context.
    /// </summary>
    /// <param name="configuration">The configuration providing temporary memory.</param>
    /// <param name="tileData">The entropy-coded tile payload.</param>
    /// <param name="context">The independently adaptable context initialized for this tile.</param>
    /// <param name="updateCdf">A value indicating whether decoded symbols adapt their tile distributions.</param>
    public Av1SymbolDecoder(
        Configuration configuration,
        Span<byte> tileData,
        Av1FrameEntropyContext context,
        bool updateCdf)
    {
        // The context owner controls reset and publication. Holding one reference here keeps the range decoder small
        // and prevents a second set of aliases from becoming a competing source of entropy state.
        this.context = context;
        this.configuration = configuration;
        this.reader = new Av1SymbolReader(tileData, updateCdf);
    }

    /// <summary>
    /// Gets the reduced neighbor context for each intra prediction mode used by key-frame luma modes.
    /// </summary>
    private static ReadOnlySpan<int> IntraModeContext => [0, 1, 2, 3, 4, 4, 4, 4, 3, 0, 1, 2, 0];

    /// <summary>
    /// Validates that range decoding remained within the bounded tile payload and consumed the required trailing-one bit.
    /// </summary>
    public void ValidateTrailingBits()
        => this.reader.ValidateTrailingBits();

    /// <summary>
    /// Reads a fixed-width CDEF strength index.
    /// </summary>
    /// <param name="bitCount">The number of bits signaled for the strength index.</param>
    /// <returns>The decoded CDEF strength index.</returns>
    public int ReadCdfStrength(int bitCount)
    {
        ref Av1SymbolReader r = ref this.reader;
        return r.ReadLiteral(bitCount);
    }

    /// <summary>
    /// Reads an unsigned fixed-width literal from the tile entropy stream.
    /// </summary>
    /// <param name="bitCount">The number of literal bits to read.</param>
    /// <returns>The decoded literal.</returns>
    public int ReadLiteral(int bitCount)
    {
        ref Av1SymbolReader r = ref this.reader;
        return r.ReadLiteral(bitCount);
    }

    /// <summary>
    /// Reads a uniformly coded value from a non-power-of-two alphabet.
    /// </summary>
    /// <param name="valueCount">The number of possible values.</param>
    /// <returns>A value in the range from zero through <paramref name="valueCount"/> minus one.</returns>
    public int ReadUniform(int valueCount)
    {
        ref Av1SymbolReader r = ref this.reader;
        int bitCount = Av1Math.Log2(valueCount) + 1;
        int threshold = (1 << bitCount) - valueCount;
        int value = r.ReadLiteral(bitCount - 1);
        if (value < threshold)
        {
            // The short prefix covers the lower values; only the remaining prefixes consume a final bit.
            return value;
        }

        return (value << 1) - threshold + r.ReadLiteral(1);
    }

    /// <summary>
    /// Reads a finite subexponential value recentered around a preceding value.
    /// </summary>
    /// <param name="valueCount">The number of values in the coded domain.</param>
    /// <param name="k">The initial subexponential group-size exponent.</param>
    /// <param name="reference">The preceding value expressed in the zero-based coded domain.</param>
    /// <returns>The decoded zero-based value.</returns>
    public int ReadReferenceSubexponential(int valueCount, int k, int reference)
    {
        int value = this.ReadSubexponential(valueCount, k);
        if ((reference << 1) <= valueCount)
        {
            return InverseRecenter(reference, value);
        }

        return valueCount - 1 - InverseRecenter(valueCount - 1 - reference, value);
    }

    /// <summary>
    /// Reads the filter type selected for a switchable loop-restoration unit.
    /// </summary>
    /// <returns>The decoded unit filter type.</returns>
    public Av1RestorationFilterType ReadSwitchableRestorationType()
    {
        ref Av1SymbolReader r = ref this.reader;
        return (Av1RestorationFilterType)r.ReadSymbol(this.context.SwitchableRestoration);
    }

    /// <summary>
    /// Reads whether a Wiener loop-restoration unit applies its filter.
    /// </summary>
    /// <returns><see langword="true"/> when Wiener filtering is selected; otherwise, <see langword="false"/>.</returns>
    public bool ReadWienerRestoration()
    {
        ref Av1SymbolReader r = ref this.reader;
        return r.ReadSymbol(this.context.WienerRestoration) != 0;
    }

    /// <summary>
    /// Reads whether a self-guided loop-restoration unit applies its filter.
    /// </summary>
    /// <returns><see langword="true"/> when self-guided filtering is selected; otherwise, <see langword="false"/>.</returns>
    public bool ReadSgrProjectionRestoration()
    {
        ref Av1SymbolReader r = ref this.reader;
        return r.ReadSymbol(this.context.SgrProjectionRestoration) != 0;
    }

    /// <summary>
    /// Reads a finite subexponential code from the tile entropy stream.
    /// </summary>
    /// <param name="valueCount">The number of values in the coded domain.</param>
    /// <param name="k">The initial subexponential group-size exponent.</param>
    /// <returns>The decoded zero-based value.</returns>
    private int ReadSubexponential(int valueCount, int k)
    {
        int group = 0;
        int groupStart = 0;
        while (true)
        {
            int bitCount = group == 0 ? k : k + group - 1;
            int groupSize = 1 << bitCount;
            if (valueCount <= groupStart + (3 * groupSize))
            {
                // The final group absorbs the remaining alphabet through truncated-binary coding
                // once fewer than three full subexponential groups remain.
                return this.ReadUniform(valueCount - groupStart) + groupStart;
            }

            if (this.ReadLiteral(1) == 0)
            {
                return this.ReadLiteral(bitCount) + groupStart;
            }

            group++;
            groupStart += groupSize;
        }
    }

    /// <summary>
    /// Maps a non-negative recentered code back around its reference value.
    /// </summary>
    /// <param name="reference">The center of the coded value order.</param>
    /// <param name="value">The recentered non-negative value.</param>
    /// <returns>The value in its original non-negative domain.</returns>
    private static int InverseRecenter(int reference, int value)
    {
        if (value > (reference << 1))
        {
            return value;
        }

        // Even and odd codes alternate above and below the reference so nearby values receive
        // the shortest finite-subexponential representations.
        return (value & 1) == 0
            ? (value >> 1) + reference
            : reference - ((value + 1) >> 1);
    }

    /// <summary>
    /// Reads whether the current luma block uses palette prediction.
    /// </summary>
    /// <param name="blockSizeContext">The block-area context in the range from zero through six.</param>
    /// <param name="neighborContext">The number of available above and left luma neighbors that use palettes.</param>
    /// <returns><see langword="true"/> when luma palette prediction is selected; otherwise, <see langword="false"/>.</returns>
    public bool ReadPaletteYMode(int blockSizeContext, int neighborContext)
    {
        ref Av1SymbolReader r = ref this.reader;
        return r.ReadSymbol(this.context.PaletteYMode[blockSizeContext][neighborContext]) != 0;
    }

    /// <summary>
    /// Reads whether the current chroma block uses palette prediction.
    /// </summary>
    /// <param name="hasLumaPalette">A value indicating whether the current block uses a luma palette.</param>
    /// <returns><see langword="true"/> when chroma palette prediction is selected; otherwise, <see langword="false"/>.</returns>
    public bool ReadPaletteUvMode(bool hasLumaPalette)
    {
        ref Av1SymbolReader r = ref this.reader;
        return r.ReadSymbol(this.context.PaletteUvMode[hasLumaPalette ? 1 : 0]) != 0;
    }

    /// <summary>
    /// Reads a luma or chroma palette size.
    /// </summary>
    /// <param name="blockSizeContext">The block-area context in the range from zero through six.</param>
    /// <param name="planeType">The luma or chroma plane class.</param>
    /// <returns>The palette size in the range from two through eight.</returns>
    public int ReadPaletteSize(int blockSizeContext, Av1PlaneType planeType)
    {
        ref Av1SymbolReader r = ref this.reader;
        Av1Distribution distribution = planeType == Av1PlaneType.Y
            ? this.context.PaletteYSize[blockSizeContext]
            : this.context.PaletteUvSize[blockSizeContext];

        return r.ReadSymbol(distribution) + 2;
    }

    /// <summary>
    /// Reads a palette color-order index from the selected spatial context.
    /// </summary>
    /// <param name="paletteSize">The number of colors in the palette.</param>
    /// <param name="colorContext">The color-index context derived from decoded neighboring indices.</param>
    /// <param name="planeType">The luma or chroma plane class.</param>
    /// <returns>The decoded index into the context-specific color order.</returns>
    public int ReadPaletteColorIndex(int paletteSize, int colorContext, Av1PlaneType planeType)
    {
        ref Av1SymbolReader r = ref this.reader;
        Av1Distribution distribution = planeType == Av1PlaneType.Y
            ? this.context.PaletteYColorIndex[paletteSize - 2][colorContext]
            : this.context.PaletteUvColorIndex[paletteSize - 2][colorContext];

        return r.ReadSymbol(distribution);
    }

    /// <summary>
    /// Reads the frame-local intra-block-copy flag.
    /// </summary>
    /// <returns><see langword="true"/> when intra-block copy is selected.</returns>
    public bool ReadUseIntraBlockCopy()
    {
        ref Av1SymbolReader r = ref this.reader;
        return r.ReadSymbol(this.context.IntraBlockCopy) > 0;
    }

    /// <summary>
    /// Reads an integer intra-block-copy displacement vector relative to a spatial reference.
    /// </summary>
    /// <param name="reference">The spatially derived reference vector.</param>
    /// <returns>The decoded displacement vector in one-eighth-sample units.</returns>
    public Av1MotionVector ReadDisplacementVector(Av1MotionVector reference)
        => this.context.DisplacementVector.Read(ref this.reader, reference, Av1MotionVectorPrecision.Integer);

    /// <summary>
    /// Reads a normal inter-prediction motion vector relative to a selected reference candidate.
    /// </summary>
    /// <param name="reference">The selected reference motion vector.</param>
    /// <param name="precision">The fractional precision allowed by the current frame.</param>
    /// <returns>The decoded motion vector in one-eighth-sample units.</returns>
    public Av1MotionVector ReadMotionVector(Av1MotionVector reference, Av1MotionVectorPrecision precision)
        => this.context.MotionVector.Read(ref this.reader, reference, precision);

    /// <summary>
    /// Reads a complete block partition type from the selected partition context.
    /// </summary>
    /// <param name="context">The partition probability context.</param>
    /// <returns>The decoded partition type.</returns>
    public Av1PartitionType ReadPartitionType(int context)
    {
        ref Av1SymbolReader r = ref this.reader;
        return (Av1PartitionType)r.ReadSymbol(this.context.PartitionTypes[context]);
    }

    /// <summary>
    /// Reads the binary split-versus-horizontal decision used at a clipped bottom tile boundary.
    /// </summary>
    /// <param name="blockSize">The current block size.</param>
    /// <param name="context">The partition probability context.</param>
    /// <returns><see cref="Av1PartitionType.Split"/> or <see cref="Av1PartitionType.Horizontal"/>.</returns>
    public Av1PartitionType ReadSplitOrHorizontal(Av1BlockSize blockSize, int context)
    {
        uint frequency = GetSplitOrHorizontalFrequency(this.context.PartitionTypes, blockSize, context);
        ref Av1SymbolReader r = ref this.reader;
        return r.ReadBoolean(frequency) ? Av1PartitionType.Split : Av1PartitionType.Horizontal;
    }

    /// <summary>
    /// Reads the binary split-versus-vertical decision used at a clipped right tile boundary.
    /// </summary>
    /// <param name="blockSize">The current block size.</param>
    /// <param name="context">The partition probability context.</param>
    /// <returns><see cref="Av1PartitionType.Split"/> or <see cref="Av1PartitionType.Vertical"/>.</returns>
    public Av1PartitionType ReadSplitOrVertical(Av1BlockSize blockSize, int context)
    {
        uint frequency = GetSplitOrVerticalFrequency(this.context.PartitionTypes, blockSize, context);
        ref Av1SymbolReader r = ref this.reader;
        return r.ReadBoolean(frequency) ? Av1PartitionType.Split : Av1PartitionType.Vertical;
    }

    /// <summary>
    /// Reads a key-frame luma prediction mode using the available above and left modes.
    /// </summary>
    /// <param name="aboveModeInfo">The above block mode, or <see langword="null"/> at the frame boundary.</param>
    /// <param name="leftModeInfo">The left block mode, or <see langword="null"/> at the frame boundary.</param>
    /// <returns>The decoded luma prediction mode.</returns>
    public Av1PredictionMode ReadYMode(Av1BlockModeInfo? aboveModeInfo, Av1BlockModeInfo? leftModeInfo)
    {
        ref Av1SymbolReader r = ref this.reader;
        Av1PredictionMode aboveMode = Av1PredictionMode.DC;
        if (aboveModeInfo != null)
        {
            aboveMode = aboveModeInfo.YMode;
        }

        Av1PredictionMode leftMode = Av1PredictionMode.DC;
        if (leftModeInfo != null)
        {
            leftMode = leftModeInfo.YMode;
        }

        int aboveContext = IntraModeContext[(int)aboveMode];
        int leftContext = IntraModeContext[(int)leftMode];
        return (Av1PredictionMode)r.ReadSymbol(this.context.KeyFrameYMode[aboveContext][leftContext]);
    }

    /// <summary>
    /// Reads an intra luma prediction mode for a block coded inside an inter frame.
    /// </summary>
    /// <param name="blockSize">The decoded block size that selects the luma-mode distribution.</param>
    /// <returns>The decoded intra luma prediction mode.</returns>
    public Av1PredictionMode ReadInterFrameYMode(Av1BlockSize blockSize)
    {
        int sizeGroup = blockSize.GetSizeGroup();
        ref Av1SymbolReader r = ref this.reader;
        return (Av1PredictionMode)r.ReadSymbol(this.context.FrameYMode[sizeGroup]);
    }

    /// <summary>
    /// Reads whether a single-reference inter block uses inter-intra prediction.
    /// </summary>
    /// <param name="blockSize">The decoded block size that selects the inter-intra flag distribution.</param>
    /// <returns><see langword="true"/> when an intra predictor is blended with the inter predictor.</returns>
    public bool ReadIsInterIntra(Av1BlockSize blockSize)
    {
        int sizeGroup = blockSize.GetSizeGroup();
        ref Av1SymbolReader r = ref this.reader;
        return r.ReadSymbol(this.context.InterIntra[sizeGroup]) != 0;
    }

    /// <summary>
    /// Reads the motion model selected for an eligible single-reference inter block.
    /// </summary>
    /// <param name="blockSize">The decoded block size that selects the motion-mode distribution.</param>
    /// <param name="allowWarpedMotion">
    /// A value indicating whether the block may select Warped in addition to Simple Translation and OBMC.
    /// </param>
    /// <returns>The decoded motion mode.</returns>
    public Av1MotionMode ReadMotionMode(Av1BlockSize blockSize, bool allowWarpedMotion)
    {
        ref Av1SymbolReader r = ref this.reader;

        // AV1 uses a separate binary CDF when Warped is ineligible; reading the first two leaves from the three-way
        // CDF would use different probabilities and desynchronize the range decoder even when Simple is selected.
        return allowWarpedMotion
            ? (Av1MotionMode)r.ReadSymbol(this.context.MotionMode[(int)blockSize])
            : (Av1MotionMode)r.ReadSymbol(this.context.Obmc[(int)blockSize]);
    }

    /// <summary>
    /// Reads whether an inter-frame block uses inter prediction.
    /// </summary>
    /// <param name="context">The spatial intra/inter context in the inclusive range zero through three.</param>
    /// <returns><see langword="true"/> when the block uses inter prediction; otherwise, <see langword="false"/>.</returns>
    public bool ReadIsInter(int context)
    {
        ref Av1SymbolReader r = ref this.reader;
        return r.ReadSymbol(this.context.IntraInter[context]) != 0;
    }

    /// <summary>
    /// Reads whether an inter block uses compound-reference instead of single-reference prediction.
    /// </summary>
    /// <param name="context">The spatial block reference-mode context in the inclusive range zero through four.</param>
    /// <returns><see langword="true"/> for compound-reference prediction; otherwise, <see langword="false"/>.</returns>
    public bool ReadIsCompoundReference(int context)
    {
        ref Av1SymbolReader r = ref this.reader;

        return r.ReadSymbol(this.context.CompInter[context]) != 0;
    }

    /// <summary>
    /// Reads one per-block interpolation filter selected by a switchable frame.
    /// </summary>
    /// <param name="context">The reference, direction, and neighbor filter context.</param>
    /// <returns>The selected Regular, Smooth, or Sharp interpolation filter.</returns>
    public Av1InterpolationFilter ReadSwitchableInterpolationFilter(int context)
    {
        ref Av1SymbolReader r = ref this.reader;

        return (Av1InterpolationFilter)r.ReadSymbol(this.context.SwitchableInterpolation[context]);
    }

    /// <summary>
    /// Reads the prediction mode for a single-reference inter block.
    /// </summary>
    /// <param name="modeContext">The packed mode context produced by reference-motion-vector candidate analysis.</param>
    /// <returns>The selected new, global, nearest, or near motion-vector mode.</returns>
    public Av1PredictionMode ReadInterMode(int modeContext)
    {
        ref Av1SymbolReader r = ref this.reader;
        int newMvContext = Av1SymbolContextHelper.GetNewMvContext(modeContext);

        // AV1 assigns symbol zero to the NEWMV leaf and symbol one to the rest of the tree. Returning at the leaf is
        // required both for the selected mode and to avoid consuming the unrelated lower decisions.
        if (r.ReadSymbol(this.context.NewMv[newMvContext]) == 0)
        {
            return Av1PredictionMode.NewMotionVector;
        }

        int zeroMvContext = Av1SymbolContextHelper.GetZeroMvContext(modeContext);
        if (r.ReadSymbol(this.context.ZeroMv[zeroMvContext]) == 0)
        {
            return Av1PredictionMode.GlobalMotionVector;
        }

        // The final zero symbol selects the nearest spatial candidate; one selects the near candidate and may be
        // followed by dynamic-reference-list syntax when more than one near candidate is available.
        int refMvContext = Av1SymbolContextHelper.GetRefMvContext(modeContext);
        return r.ReadSymbol(this.context.RefMv[refMvContext]) == 0
            ? Av1PredictionMode.NearestMotionVector
            : Av1PredictionMode.NearMotionVector;
    }

    /// <summary>
    /// Reads one dynamic reference-list decision for adjacent motion-vector candidates.
    /// </summary>
    /// <param name="context">The candidate-weight context in the inclusive range zero through two.</param>
    /// <returns>
    /// <see langword="true"/> when selection advances past the current candidate; otherwise, <see langword="false"/>.
    /// </returns>
    public bool ReadDrl(int context)
    {
        ref Av1SymbolReader r = ref this.reader;
        return r.ReadSymbol(this.context.Drl[context]) != 0;
    }

    /// <summary>
    /// Reads whether a single-reference block selects the backward-reference group.
    /// </summary>
    /// <param name="context">The neighboring forward-versus-backward vote context.</param>
    /// <returns><see langword="true"/> for a backward reference; otherwise, <see langword="false"/>.</returns>
    public bool ReadSingleReferenceIsBackward(int context)
        => this.ReadSingleReferenceDecision(context, decision: 0);

    /// <summary>
    /// Reads whether a backward single-reference block selects Alternate.
    /// </summary>
    /// <param name="context">The neighboring Backward-or-Alternate2-versus-Alternate vote context.</param>
    /// <returns><see langword="true"/> for Alternate; otherwise, <see langword="false"/>.</returns>
    public bool ReadSingleReferenceIsAlternate(int context)
        => this.ReadSingleReferenceDecision(context, decision: 1);

    /// <summary>
    /// Reads whether a forward single-reference block selects the Last3-or-Golden group.
    /// </summary>
    /// <param name="context">The neighboring near-forward-versus-far-forward vote context.</param>
    /// <returns><see langword="true"/> for Last3 or Golden; otherwise, <see langword="false"/>.</returns>
    public bool ReadSingleReferenceIsLast3OrGolden(int context)
        => this.ReadSingleReferenceDecision(context, decision: 2);

    /// <summary>
    /// Reads whether a near-forward single-reference block selects Last2.
    /// </summary>
    /// <param name="context">The neighboring Last-versus-Last2 vote context.</param>
    /// <returns><see langword="true"/> for Last2; otherwise, <see langword="false"/>.</returns>
    public bool ReadSingleReferenceIsLast2(int context)
        => this.ReadSingleReferenceDecision(context, decision: 3);

    /// <summary>
    /// Reads whether a far-forward single-reference block selects Golden.
    /// </summary>
    /// <param name="context">The neighboring Last3-versus-Golden vote context.</param>
    /// <returns><see langword="true"/> for Golden; otherwise, <see langword="false"/>.</returns>
    public bool ReadSingleReferenceIsGolden(int context)
        => this.ReadSingleReferenceDecision(context, decision: 4);

    /// <summary>
    /// Reads whether a non-Alternate backward single-reference block selects Alternate2.
    /// </summary>
    /// <param name="context">The neighboring Backward-versus-Alternate2 vote context.</param>
    /// <returns><see langword="true"/> for Alternate2; otherwise, <see langword="false"/>.</returns>
    public bool ReadSingleReferenceIsAlternate2(int context)
        => this.ReadSingleReferenceDecision(context, decision: 5);

    /// <summary>
    /// Reads one binary decision from the single-reference selection tree.
    /// </summary>
    /// <param name="context">The neighboring reference-vote context.</param>
    /// <param name="decision">The zero-based tree decision matching one <c>single_ref_cdf</c> column.</param>
    /// <returns><see langword="true"/> when the decision selects symbol one; otherwise, <see langword="false"/>.</returns>
    private bool ReadSingleReferenceDecision(int context, int decision)
    {
        ref Av1SymbolReader r = ref this.reader;

        return r.ReadSymbol(this.context.SingleReference[context][decision]) != 0;
    }

    /// <summary>
    /// Reads a chroma intra prediction mode conditioned on the luma mode and chroma-from-luma availability.
    /// </summary>
    /// <param name="mode">The decoded luma prediction mode.</param>
    /// <param name="chromaFromLumaAllowed">Indicates whether chroma-from-luma is valid for the block.</param>
    /// <returns>The decoded chroma prediction mode.</returns>
    public Av1ChromaPredictionMode ReadIntraModeUv(Av1PredictionMode mode, bool chromaFromLumaAllowed)
    {
        int chromaForLumaIndex = chromaFromLumaAllowed ? 1 : 0;
        ref Av1SymbolReader r = ref this.reader;
        return (Av1ChromaPredictionMode)r.ReadSymbol(this.context.UvMode[chromaForLumaIndex][(int)mode]);
    }

    /// <summary>
    /// Reads the transform-skip flag from a neighboring skip context.
    /// </summary>
    /// <param name="ctx">The neighboring skip context.</param>
    /// <returns><see langword="true"/> when the block contains no coded transform coefficients.</returns>
    public bool ReadSkip(int ctx)
    {
        ref Av1SymbolReader r = ref this.reader;
        return r.ReadSymbol(this.context.Skip[ctx]) > 0;
    }

    /// <summary>
    /// Reads the compound-reference skip-mode flag.
    /// </summary>
    /// <param name="context">The neighboring skip-mode context.</param>
    /// <returns><see langword="true"/> when skip mode is selected.</returns>
    public bool ReadSkipMode(int context)
    {
        ref Av1SymbolReader r = ref this.reader;
        return r.ReadSymbol(this.context.SkipMode[context]) > 0;
    }

    /// <summary>
    /// Reads a signed loop-filter delta value.
    /// </summary>
    /// <returns>The decoded loop-filter delta.</returns>
    public int ReadDeltaLoopFilter()
    {
        ref Av1SymbolReader r = ref this.reader;
        int deltaLoopFilterAbsolute = r.ReadSymbol(this.context.DeltaLoopFilterAbsolute);
        if (deltaLoopFilterAbsolute == Av1Constants.DeltaLoopFilterSmall)
        {
            int deltaLoopFilterRemainingBits = r.ReadLiteral(3) + 1;
            int deltaLoopFilterAbsoluteBitCount = r.ReadLiteral(deltaLoopFilterRemainingBits);
            deltaLoopFilterAbsolute = deltaLoopFilterAbsoluteBitCount + (1 << deltaLoopFilterRemainingBits) + 1;
        }

        bool deltaLoopFilterSign = true;
        if (deltaLoopFilterAbsolute != 0)
        {
            deltaLoopFilterSign = r.ReadLiteral(1) > 0;
        }

        return deltaLoopFilterSign ? -deltaLoopFilterAbsolute : deltaLoopFilterAbsolute;
    }

    /// <summary>
    /// Reads a signed quantizer-index delta value.
    /// </summary>
    /// <returns>The decoded quantizer-index delta.</returns>
    public int ReadDeltaQuantizerIndex()
    {
        ref Av1SymbolReader r = ref this.reader;
        int deltaQuantizerAbsolute = r.ReadSymbol(this.context.DeltaQuantizerAbsolute);
        if (deltaQuantizerAbsolute == Av1Constants.DeltaQuantizerSmall)
        {
            int deltaQuantizerRemainingBits = r.ReadLiteral(3) + 1;
            int deltaQuantizerAbsoluteBase = r.ReadLiteral(deltaQuantizerRemainingBits);
            deltaQuantizerAbsolute = deltaQuantizerAbsoluteBase + (1 << deltaQuantizerRemainingBits) + 1;
        }

        bool deltaQuantizerSignBit = true;
        if (deltaQuantizerAbsolute != 0)
        {
            deltaQuantizerSignBit = r.ReadLiteral(1) > 0;
        }

        return deltaQuantizerSignBit ? -deltaQuantizerAbsolute : deltaQuantizerAbsolute;
    }

    /// <summary>
    /// Reads a spatially predicted segment identifier.
    /// </summary>
    /// <param name="context">The context derived from neighboring segment identifiers.</param>
    /// <returns>The decoded segment identifier.</returns>
    public int ReadSegmentId(int context)
    {
        ref Av1SymbolReader r = ref this.reader;
        return r.ReadSymbol(this.context.SegmentId[context]);
    }

    /// <summary>
    /// Reads whether the current segment identifier is predicted from the retained primary-frame map.
    /// </summary>
    /// <param name="context">The sum of the above and left blocks' temporal-prediction flags.</param>
    /// <returns><see langword="true"/> when the retained map supplies the segment identifier.</returns>
    public bool ReadSegmentIdPredicted(int context)
    {
        ref Av1SymbolReader r = ref this.reader;
        return r.ReadSymbol(this.context.SegmentIdPredicted[context]) > 0;
    }

    /// <summary>
    /// Reads the unsigned directional angle-delta symbol for a prediction mode.
    /// </summary>
    /// <param name="mode">The directional prediction mode.</param>
    /// <returns>The symbol in the range zero through twice the maximum signed angle delta.</returns>
    public int ReadAngleDelta(Av1PredictionMode mode)
    {
        ref Av1SymbolReader r = ref this.reader;
        return r.ReadSymbol(this.context.AngleDelta[(int)mode - 1]);
    }

    /// <summary>
    /// Reads the filter-intra enable flag and, when enabled, its prediction mode.
    /// </summary>
    /// <param name="blockSize">The block size selecting the enable distribution.</param>
    /// <returns>The selected mode, or <see cref="Av1FilterIntraMode.AllFilterIntraModes"/> when filter-intra is disabled.</returns>
    public Av1FilterIntraMode ReadFilterUltraMode(Av1BlockSize blockSize)
    {
        ref Av1SymbolReader r = ref this.reader;
        Av1FilterIntraMode filterIntraMode = Av1FilterIntraMode.AllFilterIntraModes;
        bool useFilterIntra = r.ReadSymbol(this.context.FilterIntra[(int)blockSize]) > 0;
        if (useFilterIntra)
        {
            filterIntraMode = (Av1FilterIntraMode)r.ReadSymbol(this.context.FilterIntraMode);
        }

        return filterIntraMode;
    }

    /// <summary>
    /// Reads a transform subdivision depth and resolves it to a transform size.
    /// </summary>
    /// <param name="blockSize">The block size defining the maximum transform.</param>
    /// <param name="context">The neighboring transform-size context.</param>
    /// <returns>The decoded transform size.</returns>
    public Av1TransformSize ReadTransformSize(Av1BlockSize blockSize, int context)
    {
        ref Av1SymbolReader r = ref this.reader;
        Av1TransformSize maxTransformSize = blockSize.GetMaximumTransformSize();
        int depth = 0;
        while (maxTransformSize != Av1TransformSize.Size4x4)
        {
            depth++;
            maxTransformSize = maxTransformSize.GetSubSize();
            DebugGuard.MustBeLessThan(depth, 10, nameof(depth));
        }

        DebugGuard.MustBeLessThanOrEqualTo(depth, Av1Constants.MaxTransformCategories, nameof(depth));
        int category = depth - 1;
        int value = r.ReadSymbol(this.context.TransformSize[category][context]);
        Av1TransformSize transformSize = blockSize.GetMaximumTransformSize();
        for (int d = 0; d < value; ++d)
        {
            transformSize = transformSize.GetSubSize();
        }

        return transformSize;
    }

    /// <summary>
    /// Reads a transform type from the transform set permitted for the block.
    /// </summary>
    /// <param name="transformSize">The coded transform size.</param>
    /// <param name="useReducedTransformSet">Indicates whether the frame restricts transform choices.</param>
    /// <param name="isInter">Indicates whether the block uses inter prediction.</param>
    /// <param name="useFilterIntra">Indicates whether filter-intra prediction selected the intra direction.</param>
    /// <param name="isLossless">Indicates whether the active segment uses lossless transforms.</param>
    /// <param name="filterIntraMode">The filter-intra mode when enabled.</param>
    /// <param name="intraDirection">The ordinary intra prediction mode.</param>
    /// <returns>The decoded transform type, or DCT-DCT when no transform type is signaled.</returns>
    public Av1TransformType ReadTransformType(
        Av1TransformSize transformSize,
        bool useReducedTransformSet,
        bool isInter,
        bool useFilterIntra,
        bool isLossless,
        Av1FilterIntraMode filterIntraMode,
        Av1PredictionMode intraDirection)
    {
        Av1TransformType transformType = Av1TransformType.DctDct;

        // A lossless segment selects DCT-DCT and carries no transform-type symbol.
        if (isLossless)
        {
            return transformType;
        }

        Av1TransformSetType transformSetType = Av1SymbolContextHelper.GetExtendedTransformSetType(transformSize, isInter, useReducedTransformSet);
        if (transformSetType > Av1TransformSetType.DctOnly)
        {
            int extendedSet = Av1SymbolContextHelper.GetExtendedTransformSet(transformSetType, isInter);
            Av1TransformSize squareTransformSize = transformSize.GetSquareSize();
            ref Av1SymbolReader r = ref this.reader;
            int symbol;
            if (isInter)
            {
                symbol = r.ReadSymbol(this.context.InterExtendedTransform[extendedSet][(int)squareTransformSize]);
            }
            else
            {
                Av1PredictionMode intraMode = useFilterIntra
                    ? filterIntraMode.ToIntraDirection()
                    : intraDirection;

                symbol = r.ReadSymbol(this.context.IntraExtendedTransform[extendedSet][(int)squareTransformSize][(int)intraMode]);
            }

            transformType = Av1SymbolContextHelper.GetExtendedTransformType(transformSetType, symbol);
        }

        return transformType;
    }

    /// <summary>
    /// Reads whether a transform block has no coded coefficients.
    /// </summary>
    /// <param name="transformSizeContext">The square transform-size probability context.</param>
    /// <param name="skipContext">The context derived from neighboring coefficient blocks.</param>
    /// <returns><see langword="true"/> when the transform block is empty.</returns>
    public bool ReadTransformBlockSkip(Av1TransformSize transformSizeContext, int skipContext)
    {
        ref Av1SymbolReader r = ref this.reader;
        return r.ReadSymbol(this.context.TransformBlockSkip[(int)transformSizeContext][skipContext]) > 0;
    }

    /// <summary>
    /// Reads the joint U/V sign symbol for chroma-from-luma alpha values.
    /// </summary>
    /// <returns>The joint sign symbol.</returns>
    public int ReadChromFromLumaSign()
    {
        ref Av1SymbolReader r = ref this.reader;
        return r.ReadSymbol(this.context.ChromaFromLumaSign);
    }

    /// <summary>
    /// Reads the U-plane chroma-from-luma alpha-magnitude symbol.
    /// </summary>
    /// <param name="jointSignPlus1">The one-based joint U/V sign symbol.</param>
    /// <returns>The U-plane alpha-magnitude symbol.</returns>
    public int ReadChromaFromLumaAlphaU(int jointSignPlus1)
    {
        ref Av1SymbolReader r = ref this.reader;
        int context = Av1ChromaFromLumaMath.ContextU(jointSignPlus1 - 1);
        return r.ReadSymbol(this.context.ChromaFromLumaAlpha[context]);
    }

    /// <summary>
    /// Reads the V-plane chroma-from-luma alpha-magnitude symbol.
    /// </summary>
    /// <param name="jointSignPlus1">The one-based joint U/V sign symbol.</param>
    /// <returns>The V-plane alpha-magnitude symbol.</returns>
    public int ReadChromaFromLumaAlphaV(int jointSignPlus1)
    {
        ref Av1SymbolReader r = ref this.reader;
        int context = Av1ChromaFromLumaMath.ContextV(jointSignPlus1 - 1);
        return r.ReadSymbol(this.context.ChromaFromLumaAlpha[context]);
    }

    /// <summary>
    /// Decodes one transform block's coefficient syntax and updates its neighboring entropy contexts.
    /// </summary>
    /// <param name="modeInfo">The current block prediction and segment modes.</param>
    /// <param name="blockPosition">The transform-block offset within the coding block in four-sample units.</param>
    /// <param name="aboveContexts">The above coefficient contexts for the current plane.</param>
    /// <param name="leftContexts">The left coefficient contexts for the current plane.</param>
    /// <param name="aboveOffset">The first tile-relative above context covered by the transform.</param>
    /// <param name="leftOffset">The first superblock-row-relative left context covered by the transform.</param>
    /// <param name="plane">The zero-based Y, U, or V plane index.</param>
    /// <param name="blocksWide">The available plane width in four-sample units.</param>
    /// <param name="blocksHigh">The available plane height in four-sample units.</param>
    /// <param name="transformBlockContext">The neighboring skip and DC sign contexts.</param>
    /// <param name="transformSize">The signaled transform size.</param>
    /// <param name="isLossless">Indicates whether the active segment is lossless.</param>
    /// <param name="useReducedTransformSet">Indicates whether the frame restricts transform choices.</param>
    /// <param name="lumaTransformType">The luma transform type shared by inter-predicted chroma.</param>
    /// <param name="transformInfo">The transform descriptor updated with the decoded type and coded-block flag.</param>
    /// <param name="modeBlocksToRightEdge">The signed distance from the mode block to the right frame edge.</param>
    /// <param name="modeBlocksToBottomEdge">The signed distance from the mode block to the bottom frame edge.</param>
    /// <param name="coefficientBuffer">The destination receiving the coefficient count followed by scan-ordered signed levels.</param>
    /// <returns>The one-based end-of-block position, or zero for an empty transform block.</returns>
    public int ReadCoefficients(
        Av1BlockModeInfo modeInfo,
        Point blockPosition,
        Span<int> aboveContexts,
        Span<int> leftContexts,
        int aboveOffset,
        int leftOffset,
        int plane,
        int blocksWide,
        int blocksHigh,
        Av1TransformBlockContext transformBlockContext,
        Av1TransformSize transformSize,
        bool isLossless,
        bool useReducedTransformSet,
        Av1TransformType lumaTransformType,
        Av1TransformInfo transformInfo,
        int modeBlocksToRightEdge,
        int modeBlocksToBottomEdge,
        Span<int> coefficientBuffer)
    {
        Av1TransformSize adjustedTransformSize = transformSize.GetAdjusted();
        int width = adjustedTransformSize.GetWidth();
        int height = adjustedTransformSize.GetHeight();
        Av1TransformSize transformSizeContext = Av1SymbolContextHelper.GetTransformSizeContext(transformSize);
        Av1PlaneType planeType = (Av1PlaneType)Math.Min(plane, 1);
        int culLevel = 0;

        // AV1 omits high-frequency coefficients beyond 32 samples on every 64-point transform dimension.
        using Av1LevelBuffer levels = new(this.configuration, new Size(width, height));

        bool allZero = this.ReadTransformBlockSkip(transformSizeContext, transformBlockContext.SkipContext);
        int endOfBlock;
        if (allZero)
        {
            if (plane == 0)
            {
                transformInfo.Type = Av1TransformType.DctDct;
                transformInfo.CodeBlockFlag = false;
            }

            UpdateCoefficientContext(aboveContexts, leftContexts, blocksWide, blocksHigh, transformSize, blockPosition, aboveOffset, leftOffset, culLevel, modeBlocksToRightEdge, modeBlocksToBottomEdge);
            return 0;
        }

        if (plane == (int)Av1Plane.Y)
        {
            transformInfo.Type = this.ReadTransformType(
                transformSize,
                useReducedTransformSet,
                modeInfo.UseIntraBlockCopy,
                modeInfo.UseFilterIntra,
                isLossless,
                modeInfo.FilterIntraMode,
                modeInfo.YMode);
        }

        transformInfo.Type = ComputeTransformType(
            planeType,
            modeInfo,
            isLossless,
            transformSize,
            lumaTransformType,
            transformInfo,
            useReducedTransformSet);
        Av1TransformClass transformClass = transformInfo.Type.ToClass();
        Av1ScanOrder scanOrder = Av1ScanOrderConstants.GetScanOrder(transformSize, transformInfo.Type);
        ReadOnlySpan<short> scan = scanOrder.Scan;

        endOfBlock = this.ReadEndOfBlockPosition(transformSize, transformClass, transformSizeContext, planeType);
        if (endOfBlock > 1)
        {
            levels.Clear();
        }

        this.ReadCoefficientsEndOfBlock(transformClass, endOfBlock, scan, levels, transformSizeContext, planeType);
        if (endOfBlock > 1)
        {
            if (transformClass == Av1TransformClass.Class2D)
            {
                this.ReadCoefficientsReverse2d(transformSize, 1, endOfBlock - 1 - 1, scan, levels, transformSizeContext, planeType);
                this.ReadCoefficientsReverse(transformSize, transformClass, 0, 0, scan, levels, transformSizeContext, planeType);
            }
            else
            {
                this.ReadCoefficientsReverse(transformSize, transformClass, 0, endOfBlock - 1 - 1, scan, levels, transformSizeContext, planeType);
            }
        }

        DebugGuard.MustBeGreaterThan(scan.Length, 0, nameof(scan));
        culLevel = this.ReadCoefficientsSign(coefficientBuffer, endOfBlock, scan, levels, transformBlockContext.DcSignContext, planeType);
        UpdateCoefficientContext(aboveContexts, leftContexts, blocksWide, blocksHigh, transformSize, blockPosition, aboveOffset, leftOffset, culLevel, modeBlocksToRightEdge, modeBlocksToBottomEdge);

        transformInfo.CodeBlockFlag = true;
        return endOfBlock;
    }

    /// <summary>
    /// Reads an end-of-block token and its literal suffix.
    /// </summary>
    /// <param name="transformSize">The signaled transform size selecting the token alphabet.</param>
    /// <param name="transformClass">The transform class selecting the two-dimensional or one-dimensional model.</param>
    /// <param name="transformSizeContext">The square transform-size probability context.</param>
    /// <param name="planeType">The luma or chroma plane category.</param>
    /// <returns>The one-based end-of-block coefficient position.</returns>
    public int ReadEndOfBlockPosition(Av1TransformSize transformSize, Av1TransformClass transformClass, Av1TransformSize transformSizeContext, Av1PlaneType planeType)
    {
        ref Av1SymbolReader r = ref this.reader;
        int endOfBlockExtra = 0;
        int endOfBlockPoint = this.ReadEndOfBlockFlag(planeType, transformClass, transformSize);
        int endOfBlockShift = Av1SymbolContextHelper.EndOfBlockOffsetBits[endOfBlockPoint];
        if (endOfBlockShift > 0)
        {
            // The local table retains placeholders for the first three tokens, unlike libaom's compact table,
            // so the decoded token is also the distribution index.
            int endOfBlockContext = endOfBlockPoint;
            bool bit = this.ReadEndOfBlockExtra(transformSizeContext, planeType, endOfBlockContext);
            if (bit)
            {
                Av1Math.SetBit(ref endOfBlockExtra, endOfBlockShift - 1);
            }

            for (int j = 1; j < endOfBlockShift; j++)
            {
                if (r.ReadLiteral(1) != 0)
                {
                    Av1Math.SetBit(ref endOfBlockExtra, endOfBlockShift - 1 - j);
                }
            }
        }

        return Av1SymbolContextHelper.RecordEndOfBlockPosition(endOfBlockPoint, endOfBlockExtra);
    }

    /// <summary>
    /// Decodes the mandatory nonzero coefficient at the end-of-block scan position.
    /// </summary>
    /// <param name="transformClass">The transform direction class.</param>
    /// <param name="endOfBlock">The one-based end-of-block position.</param>
    /// <param name="scan">The transform's scan-to-raster mapping.</param>
    /// <param name="levels">The padded absolute-coefficient level plane to update.</param>
    /// <param name="transformSizeContext">The square transform-size probability context.</param>
    /// <param name="planeType">The luma or chroma plane category.</param>
    public void ReadCoefficientsEndOfBlock(Av1TransformClass transformClass, int endOfBlock, ReadOnlySpan<short> scan, Av1LevelBuffer levels, Av1TransformSize transformSizeContext, Av1PlaneType planeType)
    {
        int i = endOfBlock - 1;
        Point position = levels.GetPosition(scan[i]);
        int coefficientContext = Av1SymbolContextHelper.GetLowerLevelContextEndOfBlock(levels, i);
        int level = this.ReadBaseEndOfBlock(transformSizeContext, planeType, coefficientContext) + 1;
        if (level > Av1Constants.BaseLevelsCount)
        {
            int baseRangeContext = Av1SymbolContextHelper.GetBaseRangeContextEndOfBlock(position, transformClass);
            this.ReadCoefficientsBaseRangeLoop(transformSizeContext, planeType, baseRangeContext, ref level);
        }

        levels.GetRow(position)[position.X] = (byte)level;
    }

    /// <summary>
    /// Decodes a reverse scan range using the specialized two-dimensional coefficient contexts.
    /// </summary>
    /// <param name="transformSize">The signaled transform size.</param>
    /// <param name="startScanIndex">The inclusive lowest scan index.</param>
    /// <param name="endScanIndex">The inclusive highest scan index.</param>
    /// <param name="scan">The transform's scan-to-raster mapping.</param>
    /// <param name="levels">The padded absolute-coefficient level plane to update.</param>
    /// <param name="transformSizeContext">The square transform-size probability context.</param>
    /// <param name="planeType">The luma or chroma plane category.</param>
    public void ReadCoefficientsReverse2d(Av1TransformSize transformSize, int startScanIndex, int endScanIndex, ReadOnlySpan<short> scan, Av1LevelBuffer levels, Av1TransformSize transformSizeContext, Av1PlaneType planeType)
    {
        for (int c = endScanIndex; c >= startScanIndex; --c)
        {
            Point position = levels.GetPosition(scan[c]);
            int coefficientContext = Av1SymbolContextHelper.GetLowerLevelsContext2d(levels, position, transformSize);
            int level = this.ReadCoefficientsBase(transformSizeContext, planeType, coefficientContext);
            if (level > Av1Constants.BaseLevelsCount)
            {
                int baseRangeContext = Av1SymbolContextHelper.GetBaseRangeContext2d(levels, position);
                this.ReadCoefficientsBaseRangeLoop(transformSizeContext, planeType, baseRangeContext, ref level);
            }

            levels.GetRow(position)[position.X] = (byte)level;
        }
    }

    /// <summary>
    /// Decodes a reverse scan range using transform-class-specific coefficient contexts.
    /// </summary>
    /// <param name="transformSize">The signaled transform size.</param>
    /// <param name="transformClass">The transform direction class.</param>
    /// <param name="startScanIndex">The inclusive lowest scan index.</param>
    /// <param name="endScanIndex">The inclusive highest scan index.</param>
    /// <param name="scan">The transform's scan-to-raster mapping.</param>
    /// <param name="levels">The padded absolute-coefficient level plane to update.</param>
    /// <param name="transformSizeContext">The square transform-size probability context.</param>
    /// <param name="planeType">The luma or chroma plane category.</param>
    public void ReadCoefficientsReverse(Av1TransformSize transformSize, Av1TransformClass transformClass, int startScanIndex, int endScanIndex, ReadOnlySpan<short> scan, Av1LevelBuffer levels, Av1TransformSize transformSizeContext, Av1PlaneType planeType)
    {
        for (int c = endScanIndex; c >= startScanIndex; --c)
        {
            int pos = scan[c];
            Point position = levels.GetPosition(pos);
            int coefficientContext = Av1SymbolContextHelper.GetLowerLevelsContext(levels, position, transformSize, transformClass);
            int level = this.ReadCoefficientsBase(transformSizeContext, planeType, coefficientContext);
            if (level > Av1Constants.BaseLevelsCount)
            {
                int baseRangeContext = Av1SymbolContextHelper.GetBaseRangeContext(levels, position, transformClass);
                this.ReadCoefficientsBaseRangeLoop(transformSizeContext, planeType, baseRangeContext, ref level);
            }

            levels.GetRow(position)[position.X] = (byte)level;
        }
    }

    /// <summary>
    /// Reads coefficient signs and Golomb extensions, then writes scan-ordered signed levels.
    /// </summary>
    /// <param name="coefficientBuffer">The destination receiving the coefficient count followed by signed levels.</param>
    /// <param name="endOfBlock">The one-based end-of-block position and coefficient count.</param>
    /// <param name="scan">The transform's scan-to-raster mapping.</param>
    /// <param name="levels">The decoded absolute-coefficient level plane.</param>
    /// <param name="dcSignContext">The neighboring DC sign context.</param>
    /// <param name="planeType">The luma or chroma plane category.</param>
    /// <returns>The packed coefficient context used by adjacent transform blocks.</returns>
    public int ReadCoefficientsSign(Span<int> coefficientBuffer, int endOfBlock, ReadOnlySpan<short> scan, Av1LevelBuffer levels, int dcSignContext, Av1PlaneType planeType)
    {
        ref Av1SymbolReader r = ref this.reader;
        int culLevel = 0;
        int dcValue = 0;
        coefficientBuffer[0] = endOfBlock;
        for (int c = 0; c < endOfBlock; c++)
        {
            int sign = 0;
            int pos = scan[c];
            Point position = levels.GetPosition(pos);
            int level = levels[position];
            if (level != 0)
            {
                if (c == 0)
                {
                    sign = this.ReadDcSign(planeType, dcSignContext);
                }
                else
                {
                    sign = r.ReadLiteral(1);
                }

                if (level >= Av1Constants.CoefficientBaseRange + Av1Constants.BaseLevelsCount + 1)
                {
                    level += this.ReadGolomb();
                }

                if (c == 0)
                {
                    dcValue = sign != 0 ? -level : level;
                }

                level &= 0xfffff;
                culLevel += level;
            }

            coefficientBuffer[c + 1] = sign != 0 ? -level : level;
        }

        culLevel = Math.Min(Av1Constants.CoefficientContextMask, culLevel);
        Av1SymbolContextHelper.SetDcSign(ref culLevel, dcValue);

        return culLevel;
    }

    /// <summary>
    /// Reads the end-of-block token for a transform coefficient-count category.
    /// </summary>
    /// <param name="planeType">The luma or chroma plane category.</param>
    /// <param name="transformClass">The transform direction class.</param>
    /// <param name="transformSize">The signaled transform size.</param>
    /// <returns>The one-based end-of-block token.</returns>
    private int ReadEndOfBlockFlag(Av1PlaneType planeType, Av1TransformClass transformClass, Av1TransformSize transformSize)
    {
        int endOfBlockContext = transformClass == Av1TransformClass.Class2D ? 0 : 1;
        int endOfBlockMultiSize = transformSize.GetLog2Minus4();
        ref Av1SymbolReader r = ref this.reader;
        return r.ReadSymbol(this.context.EndOfBlockFlag[endOfBlockMultiSize][(int)planeType][endOfBlockContext]) + 1;
    }

    /// <summary>
    /// Reads the most significant context-coded bit of an end-of-block suffix.
    /// </summary>
    /// <param name="transformSizeContext">The square transform-size probability context.</param>
    /// <param name="planeType">The luma or chroma plane category.</param>
    /// <param name="endOfBlockContext">The token-aligned extra-bit context in the padded local table.</param>
    /// <returns>The decoded suffix bit.</returns>
    private bool ReadEndOfBlockExtra(Av1TransformSize transformSizeContext, Av1PlaneType planeType, int endOfBlockContext)
    {
        ref Av1SymbolReader r = ref this.reader;
        return r.ReadSymbol(this.context.EndOfBlockExtra[(int)transformSizeContext][(int)planeType][endOfBlockContext]) > 0;
    }

    /// <summary>
    /// Reads one coefficient base-range symbol.
    /// </summary>
    /// <param name="transformSizeContext">The square transform-size probability context.</param>
    /// <param name="planeType">The luma or chroma plane category.</param>
    /// <param name="baseRangeContext">The coefficient base-range context.</param>
    /// <returns>The decoded base-range symbol.</returns>
    private int ReadCoefficientsBaseRange(Av1TransformSize transformSizeContext, Av1PlaneType planeType, int baseRangeContext)
    {
        ref Av1SymbolReader r = ref this.reader;
        return r.ReadSymbol(this.context.CoefficientsBaseRange[(int)transformSizeContext][(int)planeType][baseRangeContext]);
    }

    /// <summary>
    /// Reads the sign of a nonzero DC coefficient.
    /// </summary>
    /// <param name="planeType">The luma or chroma plane category.</param>
    /// <param name="dcSignContext">The neighboring DC sign context.</param>
    /// <returns>Zero for positive or one for negative.</returns>
    private int ReadDcSign(Av1PlaneType planeType, int dcSignContext)
    {
        ref Av1SymbolReader r = ref this.reader;
        return r.ReadSymbol(this.context.DcSign[(int)planeType][dcSignContext]);
    }

    /// <summary>
    /// Reads the base-level symbol for the final nonzero coefficient.
    /// </summary>
    /// <param name="transformSizeContext">The square transform-size probability context.</param>
    /// <param name="planeType">The luma or chroma plane category.</param>
    /// <param name="coefficientContext">The end-of-block coefficient context.</param>
    /// <returns>The zero-based base-level symbol.</returns>
    private int ReadBaseEndOfBlock(Av1TransformSize transformSizeContext, Av1PlaneType planeType, int coefficientContext)
    {
        ref Av1SymbolReader r = ref this.reader;
        return r.ReadSymbol(this.context.BaseEndOfBlock[(int)transformSizeContext][(int)planeType][coefficientContext]);
    }

    /// <summary>
    /// Reads the base-level symbol for a coefficient preceding end-of-block.
    /// </summary>
    /// <param name="transformSizeContext">The square transform-size probability context.</param>
    /// <param name="planeType">The luma or chroma plane category.</param>
    /// <param name="coefficientContext">The nonzero-map coefficient context.</param>
    /// <returns>The decoded base-level symbol.</returns>
    private int ReadCoefficientsBase(Av1TransformSize transformSizeContext, Av1PlaneType planeType, int coefficientContext)
    {
        ref Av1SymbolReader r = ref this.reader;
        return r.ReadSymbol(this.context.CoefficientsBase[(int)transformSizeContext][(int)planeType][coefficientContext]);
    }

    /// <summary>
    /// Accumulates coefficient base-range symbols until the terminal symbol or AV1 range limit is reached.
    /// </summary>
    /// <param name="transformSizeContext">The square transform-size probability context.</param>
    /// <param name="planeType">The luma or chroma plane category.</param>
    /// <param name="baseRangeContext">The coefficient base-range context.</param>
    /// <param name="level">The coefficient level to increment.</param>
    private void ReadCoefficientsBaseRangeLoop(Av1TransformSize transformSizeContext, Av1PlaneType planeType, int baseRangeContext, ref int level)
    {
        ref Av1SymbolReader r = ref this.reader;
        Av1TransformSize limitedTransformSizeContext = (Av1TransformSize)Math.Min((int)transformSizeContext, (int)Av1TransformSize.Size32x32);
        Av1Distribution distribution = this.context.CoefficientsBaseRange[(int)limitedTransformSizeContext][(int)planeType][baseRangeContext];
        for (int idx = 0; idx < Av1Constants.CoefficientBaseRange; idx += Av1Constants.BaseRangeSizeMinus1)
        {
            int coefficientBaseRange = r.ReadSymbol(distribution);
            level += coefficientBaseRange;
            if (coefficientBaseRange < Av1Constants.BaseRangeSizeMinus1)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Reads the unsigned exponential-Golomb suffix used for coefficient levels beyond the base range.
    /// </summary>
    /// <returns>The decoded nonnegative suffix value.</returns>
    /// <exception cref="InvalidImageContentException">The unary prefix exceeds the AV1 coefficient limit.</exception>
    public int ReadGolomb()
    {
        ref Av1SymbolReader r = ref this.reader;
        int x = 1;
        int length = 0;
        int i = 0;

        while (i == 0)
        {
            i = r.ReadLiteral(1);
            ++length;
            if (length > 20)
            {
                throw new InvalidImageContentException("The AV1 coefficient Golomb code exceeds its 20-bit limit.");
            }
        }

        for (i = 0; i < length - 1; ++i)
        {
            x <<= 1;
            x += r.ReadLiteral(1);
        }

        return x - 1;
    }

    /// <summary>
    /// Stores a transform block's packed coefficient context into the above and left neighbor arrays.
    /// </summary>
    /// <param name="aboveContexts">The above contexts for the current plane.</param>
    /// <param name="leftContexts">The left contexts for the current plane.</param>
    /// <param name="blocksWide">The available plane width in four-sample units.</param>
    /// <param name="blocksHigh">The available plane height in four-sample units.</param>
    /// <param name="transformSize">The signaled transform size.</param>
    /// <param name="blockPosition">The transform-block offset within the coding block in four-sample units.</param>
    /// <param name="aboveOffset">The first tile-relative above context covered by the transform.</param>
    /// <param name="leftOffset">The first superblock-row-relative left context covered by the transform.</param>
    /// <param name="culLevel">The packed coefficient magnitude and DC sign context.</param>
    /// <param name="modeBlockToRightEdge">The signed distance from the mode block to the right frame edge.</param>
    /// <param name="modeBlockToBottomEdge">The signed distance from the mode block to the bottom frame edge.</param>
    private static void UpdateCoefficientContext(
        Span<int> aboveContexts,
        Span<int> leftContexts,
        int blocksWide,
        int blocksHigh,
        Av1TransformSize transformSize,
        Point blockPosition,
        int aboveOffset,
        int leftOffset,
        int culLevel,
        int modeBlockToRightEdge,
        int modeBlockToBottomEdge)
    {
        int transformSizeWide = transformSize.Get4x4WideCount();
        int transformSizeHigh = transformSize.Get4x4HighCount();

        if (modeBlockToRightEdge < 0)
        {
            int aboveContextCount = Math.Min(transformSizeWide, blocksWide - blockPosition.X);
            aboveContexts.Slice(aboveOffset, aboveContextCount).Fill(culLevel);
            aboveContexts.Slice(aboveOffset + aboveContextCount, transformSizeWide - aboveContextCount).Clear();
        }
        else
        {
            aboveContexts.Slice(aboveOffset, transformSizeWide).Fill(culLevel);
        }

        if (modeBlockToBottomEdge < 0)
        {
            int leftContextCount = Math.Min(transformSizeHigh, blocksHigh - blockPosition.Y);
            leftContexts.Slice(leftOffset, leftContextCount).Fill(culLevel);
            leftContexts.Slice(leftOffset + leftContextCount, transformSizeHigh - leftContextCount).Clear();
        }
        else
        {
            leftContexts.Slice(leftOffset, transformSizeHigh).Fill(culLevel);
        }
    }

    /// <summary>
    /// Resolves the transform type permitted for a plane after lossless, size, prediction, and transform-set restrictions.
    /// </summary>
    /// <param name="planeType">The luma or chroma plane category.</param>
    /// <param name="modeInfo">The current block prediction modes.</param>
    /// <param name="isLossless">Indicates whether the active segment is lossless.</param>
    /// <param name="transformSize">The signaled transform size.</param>
    /// <param name="lumaTransformType">The luma transform type shared by inter-predicted chroma.</param>
    /// <param name="transformInfo">The transform descriptor containing the signaled luma type.</param>
    /// <param name="useReducedTransformSet">Indicates whether the frame restricts transform choices.</param>
    /// <returns>The transform type valid for the current plane.</returns>
    private static Av1TransformType ComputeTransformType(
        Av1PlaneType planeType,
        Av1BlockModeInfo modeInfo,
        bool isLossless,
        Av1TransformSize transformSize,
        Av1TransformType lumaTransformType,
        Av1TransformInfo transformInfo,
        bool useReducedTransformSet)
    {
        Av1TransformType transformType = Av1TransformType.DctDct;
        if (isLossless || transformSize.GetSquareUpSize() > Av1TransformSize.Size32x32)
        {
            transformType = Av1TransformType.DctDct;
        }
        else
        {
            if (planeType == Av1PlaneType.Y)
            {
                transformType = transformInfo.Type;
            }
            else if (modeInfo.UseIntraBlockCopy)
            {
                // Intra-block copy follows inter transform rules, so chroma reuses the luma transform type at the
                // corresponding luma-grid position rather than deriving a type from the DC chroma mode.
                transformType = lumaTransformType;
            }
            else
            {
                // Chroma has its own intra mode, so its implicit transform must be derived independently of luma.
                transformType = Av1SymbolContextHelper.ConvertIntraModeToTransformType(modeInfo, Av1PlaneType.Uv);
            }
        }

        Av1TransformSetType transformSetType = Av1SymbolContextHelper.GetExtendedTransformSetType(
            transformSize,
            modeInfo.UseIntraBlockCopy,
            useReducedTransformSet);

        if (!transformType.IsExtendedSetUsed(transformSetType))
        {
            transformType = Av1TransformType.DctDct;
        }

        return transformType;
    }

    /// <summary>
    /// Collapses a full partition distribution into the split-versus-horizontal boundary decision.
    /// </summary>
    /// <param name="inputs">The full partition distributions.</param>
    /// <param name="blockSize">The current block size.</param>
    /// <param name="context">The partition probability context.</param>
    /// <returns>The Q15 probability of the split outcome.</returns>
    public static uint GetSplitOrHorizontalFrequency(Av1Distribution[] inputs, Av1BlockSize blockSize, int context)
    {
        Av1Distribution input = inputs[context];

        // At the bottom edge, AV1 gathers every vertical-like partition mass into the split branch of the
        // temporary binary CDF. Reading the frequency directly avoids allocating an adaptive distribution.
        uint frequency = GetElementProbability(input, Av1PartitionType.Vertical);
        frequency += GetElementProbability(input, Av1PartitionType.Split);
        frequency += GetElementProbability(input, Av1PartitionType.HorizontalA);
        frequency += GetElementProbability(input, Av1PartitionType.VerticalA);
        frequency += GetElementProbability(input, Av1PartitionType.VerticalB);
        if (blockSize != Av1BlockSize.Block128x128)
        {
            frequency += GetElementProbability(input, Av1PartitionType.Vertical4);
        }

        return frequency;
    }

    /// <summary>
    /// Collapses a full partition distribution into the split-versus-vertical boundary decision.
    /// </summary>
    /// <param name="inputs">The full partition distributions.</param>
    /// <param name="blockSize">The current block size.</param>
    /// <param name="context">The partition probability context.</param>
    /// <returns>The Q15 probability of the split outcome.</returns>
    public static uint GetSplitOrVerticalFrequency(Av1Distribution[] inputs, Av1BlockSize blockSize, int context)
    {
        Av1Distribution input = inputs[context];

        // At the right edge, AV1 gathers every horizontal-like partition mass into the split branch of the
        // temporary binary CDF. Reading the frequency directly avoids allocating an adaptive distribution.
        uint frequency = GetElementProbability(input, Av1PartitionType.Horizontal);
        frequency += GetElementProbability(input, Av1PartitionType.Split);
        frequency += GetElementProbability(input, Av1PartitionType.HorizontalA);
        frequency += GetElementProbability(input, Av1PartitionType.HorizontalB);
        frequency += GetElementProbability(input, Av1PartitionType.VerticalA);
        if (blockSize != Av1BlockSize.Block128x128)
        {
            frequency += GetElementProbability(input, Av1PartitionType.Horizontal4);
        }

        return frequency;
    }

    /// <summary>
    /// Gets one symbol's probability mass from adjacent inverse-CDF thresholds.
    /// </summary>
    /// <param name="probability">The inverse cumulative distribution.</param>
    /// <param name="element">The partition symbol.</param>
    /// <returns>The symbol's probability mass.</returns>
    private static uint GetElementProbability(Av1Distribution probability, Av1PartitionType element)
        => probability[(int)element - 1] - probability[(int)element];
}

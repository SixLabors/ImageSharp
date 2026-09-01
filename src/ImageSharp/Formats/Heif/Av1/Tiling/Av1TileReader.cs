// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;
using SixLabors.ImageSharp.Formats.Heif.Av1.ReferenceFrames;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Parses partition, mode, transform, and coefficient syntax for one AV1 tile.
/// </summary>
internal sealed class Av1TileReader : IAv1TileReader, IDisposable
{
    /// <summary>
    /// The minimum value of the first self-guided projection coefficient.
    /// </summary>
    private const int SgrProjectionCoefficient0Minimum = -96;

    /// <summary>
    /// The minimum value of the second self-guided projection coefficient.
    /// </summary>
    private const int SgrProjectionCoefficient1Minimum = -32;

    /// <summary>
    /// The number of values in either self-guided projection coefficient domain.
    /// </summary>
    private const int SgrProjectionCoefficientValueCount = 128;

    /// <summary>
    /// The subexponential group-size exponent for self-guided projection coefficients.
    /// </summary>
    private const int SgrProjectionSubexponentialK = 4;

    /// <summary>
    /// The two self-guided filter radii selected by each parameter-set index.
    /// </summary>
    private static readonly int[][] SgrProjectionRadii =
    [
        [2, 1], [2, 1], [2, 1], [2, 1], [2, 1], [2, 1], [2, 1], [2, 1],
        [2, 1], [2, 1], [0, 1], [0, 1], [0, 1], [0, 1], [2, 0], [2, 0]
    ];

    /// <summary>
    /// Stores two preceding self-guided restoration coefficients for each of the three color planes, indexed by
    /// <c>(plane * 2) + coefficient</c>.
    /// </summary>
    private InlineArray6<int> referenceSgrXqd;

    /// <summary>
    /// Stores three preceding Wiener taps for both passes of each of the three color planes, indexed by
    /// <c>(((plane * 2) + pass) * 3) + tap</c>.
    /// </summary>
    private InlineArray18<int> referenceLrWiener;

    /// <summary>
    /// Tracks entropy, partition, and transform state above the current block.
    /// </summary>
    private readonly Av1ParseAboveNeighbor4x4Context aboveNeighborContext;

    /// <summary>
    /// Tracks entropy, partition, and transform state left of the current block.
    /// </summary>
    private readonly Av1ParseLeftNeighbor4x4Context leftNeighborContext;

    /// <summary>
    /// The quantizer index carried between delta-quantized blocks in the current tile.
    /// </summary>
    private int currentQuantizerIndex;

    /// <summary>
    /// Stores the loop-filter delta values carried between superblocks in the current tile.
    /// </summary>
    private InlineArray4<int> currentDeltaLoopFilter;

    /// <summary>
    /// Stores per-plane transform counts for each forced 64x64 residual region.
    /// </summary>
    private InlineArray4<InlineArray4<int>> transformUnitCount;

    /// <summary>
    /// Tracks the first unassigned transform-information index for luma and shared chroma storage.
    /// </summary>
    private InlineArray4<int> firstTransformOffset;

    /// <summary>
    /// Tracks the next coefficient slot for each color plane within the current superblock.
    /// </summary>
    private InlineArray4<int> coefficientIndex;

    /// <summary>
    /// Reusable padded coefficient-context storage for the sequential transform traversal.
    /// </summary>
    private readonly Av1LevelBuffer coefficientLevels;

    /// <summary>
    /// Reusable luma palette indices for the coding blocks in one superblock.
    /// </summary>
    private readonly Buffer2D<byte> lumaPaletteColorIndexMap;

    /// <summary>
    /// Reusable chroma palette indices for the coding blocks in one superblock.
    /// </summary>
    private readonly Buffer2D<byte> chromaPaletteColorIndexMap;

    /// <summary>
    /// Indicates whether this reader owns and disposes the palette maps.
    /// </summary>
    private readonly bool ownsPaletteColorIndexMaps;

    /// <summary>
    /// Reusable storage for the eight spatial displacement-vector candidates permitted by AV1.
    /// </summary>
    private InlineArray8<Av1MotionVector> displacementVectorCandidates;

    /// <summary>
    /// Reusable storage for the spatial weight associated with each displacement-vector candidate.
    /// </summary>
    private InlineArray8<int> displacementVectorWeights;

    /// <summary>
    /// Reusable counts of the canonical references selected by the immediately above and left blocks.
    /// </summary>
    private InlineArray8<byte> neighborReferenceCounts;

    /// <summary>
    /// Reusable fixed-capacity storage for one block's weighted reference-motion-vector candidates.
    /// </summary>
    private readonly Av1ReferenceMotionVectors referenceMotionVectors = new();

    /// <summary>
    /// Reusable fixed-capacity state for motion-mode eligibility and local warped-motion projection.
    /// </summary>
    private readonly Av1MotionVariationCandidates motionVariationCandidates = new();

    /// <summary>
    /// Provides allocator and decoder configuration to tile entropy decoding.
    /// </summary>
    private readonly Configuration configuration;

    /// <summary>
    /// The decoder-session entropy contexts reused by every tile in the current frame.
    /// </summary>
    private readonly Av1FrameEntropyContexts entropyContexts;

    /// <summary>
    /// The retained primary frame whose segment map supplies temporal segment-ID predictions.
    /// </summary>
    private readonly Av1FrameInfo.ReferenceState? primaryReferenceState;

    /// <summary>
    /// The retained reconstructed frames used to determine reference scaling during inter mode parsing.
    /// </summary>
    private readonly Av1ReferenceFrameStore? referenceFrames;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1TileReader"/> class for syntax parsing without reconstruction.
    /// </summary>
    /// <param name="configuration">The decoder configuration.</param>
    /// <param name="sequenceHeader">The active AV1 sequence header.</param>
    /// <param name="frameHeader">The frame header whose tiles will be parsed.</param>
    public Av1TileReader(Configuration configuration, ObuSequenceHeader sequenceHeader, ObuFrameHeader frameHeader)
        : this(configuration, sequenceHeader, frameHeader, new(frameHeader.QuantizationParameters.BaseQIndex), null, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1TileReader"/> class with decoder-session entropy state.
    /// </summary>
    /// <param name="configuration">The decoder configuration.</param>
    /// <param name="sequenceHeader">The active AV1 sequence header.</param>
    /// <param name="frameHeader">The frame header whose tiles will be parsed.</param>
    /// <param name="entropyContexts">The entropy contexts reused by the owning decoder session.</param>
    /// <param name="primaryReferenceContext">
    /// The retained primary-reference entropy context, or <see langword="null"/> when the frame selects defaults.
    /// </param>
    /// <param name="referenceFrames">
    /// The retained reconstructed frames, or <see langword="null"/> for the intra-only syntax reader.
    /// </param>
    public Av1TileReader(
        Configuration configuration,
        ObuSequenceHeader sequenceHeader,
        ObuFrameHeader frameHeader,
        Av1FrameEntropyContexts entropyContexts,
        Av1FrameEntropyContext? primaryReferenceContext,
        Av1ReferenceFrameStore? referenceFrames)
        : this(configuration, sequenceHeader, frameHeader, entropyContexts, primaryReferenceContext, referenceFrames, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1TileReader"/> class with decoder-session entropy and palette state.
    /// </summary>
    /// <param name="configuration">The decoder configuration.</param>
    /// <param name="sequenceHeader">The active AV1 sequence header.</param>
    /// <param name="frameHeader">The frame header whose tiles will be parsed.</param>
    /// <param name="entropyContexts">The entropy contexts reused by the owning decoder session.</param>
    /// <param name="primaryReferenceContext">
    /// The retained primary-reference entropy context, or <see langword="null"/> when the frame selects defaults.
    /// </param>
    /// <param name="referenceFrames">The retained reconstructed frames.</param>
    /// <param name="lumaPaletteColorIndexMap">The decoder-session luma palette scratch map.</param>
    /// <param name="chromaPaletteColorIndexMap">The decoder-session chroma palette scratch map.</param>
    public Av1TileReader(
        Configuration configuration,
        ObuSequenceHeader sequenceHeader,
        ObuFrameHeader frameHeader,
        Av1FrameEntropyContexts entropyContexts,
        Av1FrameEntropyContext? primaryReferenceContext,
        Av1ReferenceFrameStore referenceFrames,
        Buffer2D<byte> lumaPaletteColorIndexMap,
        Buffer2D<byte> chromaPaletteColorIndexMap)
        : this(
            configuration,
            sequenceHeader,
            frameHeader,
            entropyContexts,
            primaryReferenceContext,
            referenceFrames,
            new PaletteColorIndexMaps(lumaPaletteColorIndexMap, chromaPaletteColorIndexMap))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1TileReader"/> class with explicit palette-map ownership.
    /// </summary>
    /// <param name="configuration">The decoder configuration.</param>
    /// <param name="sequenceHeader">The active AV1 sequence header.</param>
    /// <param name="frameHeader">The frame header whose tiles will be parsed.</param>
    /// <param name="entropyContexts">The entropy contexts reused by the owning decoder session.</param>
    /// <param name="primaryReferenceContext">The retained primary-reference entropy context.</param>
    /// <param name="referenceFrames">The retained reconstructed frames.</param>
    /// <param name="sharedPaletteColorIndexMaps">
    /// Shared palette maps, or <see langword="null"/> when the reader allocates and owns both maps.
    /// </param>
    private Av1TileReader(
        Configuration configuration,
        ObuSequenceHeader sequenceHeader,
        ObuFrameHeader frameHeader,
        Av1FrameEntropyContexts entropyContexts,
        Av1FrameEntropyContext? primaryReferenceContext,
        Av1ReferenceFrameStore? referenceFrames,
        PaletteColorIndexMaps? sharedPaletteColorIndexMaps)
    {
        this.FrameHeader = frameHeader;
        this.configuration = configuration;
        this.SequenceHeader = sequenceHeader;
        this.entropyContexts = entropyContexts;
        this.referenceFrames = referenceFrames;
        this.ownsPaletteColorIndexMaps = sharedPaletteColorIndexMaps is null;
        this.entropyContexts.BeginFrame(frameHeader.QuantizationParameters.BaseQIndex, primaryReferenceContext);

        // FrameInfo owns traversal records for this coded frame and one superblock of coefficient scratch.
        this.FrameInfo = new(this.configuration, this.SequenceHeader, this.FrameHeader);
        if (referenceFrames is not null)
        {
            byte? primaryReferenceSlot = this.FrameHeader.PrimaryReferenceSlot;
            if (primaryReferenceSlot is not null)
            {
                // The uncompressed-header parser has already validated this slot. Segment samples remain in the
                // retained reference state and are copied only for whole-map inheritance.
                this.primaryReferenceState = referenceFrames.ResolveRequired(primaryReferenceSlot.Value).ReferenceState;
            }
        }

        try
        {
            this.FrameInfo.InitializeSegmentIds(this.FrameHeader, this.primaryReferenceState);
            this.FrameInfo.InitializeLoopRestoration(this.SequenceHeader, this.FrameHeader);
        }
        catch
        {
            // FrameInfo has already rented the active frame's syntax storage. Return every successful rent if
            // a later segmentation or restoration allocation prevents this reader from being constructed.
            this.FrameInfo.Dispose();
            throw;
        }

        // Above contexts span the aligned frame width, while left contexts are reused for each superblock row.
        int planesCount = sequenceHeader.ColorConfig.PlaneCount;
        int modeInfoWideColumnCount = Av1Math.AlignPowerOf2(
            frameHeader.ModeInfoColumnCount,
            sequenceHeader.SuperblockSizeLog2 - Av1Constants.ModeInfoSizeLog2);

        this.aboveNeighborContext = new Av1ParseAboveNeighbor4x4Context(configuration, planesCount, modeInfoWideColumnCount);
        try
        {
            this.leftNeighborContext = new Av1ParseLeftNeighbor4x4Context(configuration, planesCount, sequenceHeader.SuperblockModeInfoSize);
        }
        catch
        {
            // The reader is not returned when its second context allocation fails, so release the first rent here
            // rather than relying on an owner that the caller cannot reach.
            this.aboveNeighborContext.Dispose();
            throw;
        }

        try
        {
            this.coefficientLevels = new Av1LevelBuffer(configuration);
        }
        catch
        {
            // The coefficient scratch allocation follows both neighbor contexts. Unwind those successful rents when
            // construction cannot publish an owning tile reader.
            this.aboveNeighborContext.Dispose();
            this.leftNeighborContext.Dispose();
            this.FrameInfo.Dispose();
            throw;
        }

        if (sharedPaletteColorIndexMaps is null)
        {
            Buffer2D<byte>? ownedLumaPaletteColorIndexMap = null;
            Buffer2D<byte>? ownedChromaPaletteColorIndexMap = null;

            try
            {
                // Standalone syntax readers have no decoder-session owner. They still use the same fixed
                // maximum-superblock bound and return both maps when the reader is disposed.
                int paletteMapLength = 1 << Av1Constants.MaxSuperBlockSizeLog2;
                ownedLumaPaletteColorIndexMap = configuration.MemoryAllocator.Allocate2D<byte>(paletteMapLength, paletteMapLength);
                ownedChromaPaletteColorIndexMap = configuration.MemoryAllocator.Allocate2D<byte>(paletteMapLength, paletteMapLength);
                this.lumaPaletteColorIndexMap = ownedLumaPaletteColorIndexMap;
                this.chromaPaletteColorIndexMap = ownedChromaPaletteColorIndexMap;
            }
            catch
            {
                ownedChromaPaletteColorIndexMap?.Dispose();
                ownedLumaPaletteColorIndexMap?.Dispose();
                this.coefficientLevels.Dispose();
                this.leftNeighborContext.Dispose();
                this.aboveNeighborContext.Dispose();
                this.FrameInfo.Dispose();
                throw;
            }
        }
        else
        {
            PaletteColorIndexMaps paletteColorIndexMaps = sharedPaletteColorIndexMaps.Value;
            this.lumaPaletteColorIndexMap = paletteColorIndexMaps.Luma;
            this.chromaPaletteColorIndexMap = paletteColorIndexMaps.Chroma;
        }

        if (referenceFrames is not null)
        {
            try
            {
                // Motion storage is acquired after every other constructor allocation. If this final acquisition
                // fails, the catch can return every successfully created allocator-owned resource in one place.
                this.FrameInfo.InitializeMotionField(configuration, this.SequenceHeader, this.FrameHeader, referenceFrames);
            }
            catch
            {
                if (this.ownsPaletteColorIndexMaps)
                {
                    this.chromaPaletteColorIndexMap.Dispose();
                    this.lumaPaletteColorIndexMap.Dispose();
                }

                this.coefficientLevels.Dispose();
                this.leftNeighborContext.Dispose();
                this.aboveNeighborContext.Dispose();
                this.FrameInfo.Dispose();
                throw;
            }
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1TileReader"/> class that reconstructs parsed superblocks.
    /// </summary>
    /// <param name="configuration">The decoder configuration.</param>
    /// <param name="sequenceHeader">The active AV1 sequence header.</param>
    /// <param name="frameHeader">The frame header whose tiles will be parsed.</param>
    /// <param name="frameDecoder">The frame decoder that reconstructs each parsed superblock.</param>
    public Av1TileReader(Configuration configuration, ObuSequenceHeader sequenceHeader, ObuFrameHeader frameHeader, IAv1FrameDecoder frameDecoder)
        : this(configuration, sequenceHeader, frameHeader)
        => this.FrameDecoder = frameDecoder;

    /// <summary>
    /// Gets the default self-guided restoration projection coefficients for each color plane.
    /// </summary>
    private static ReadOnlySpan<int> SgrprojXqdMid => [-32, 31];

    /// <summary>
    /// Gets the default Wiener restoration taps retained between restoration units.
    /// </summary>
    private static ReadOnlySpan<int> WienerTapsMid => [3, -7, 15];

    /// <summary>
    /// Gets the minimum transmitted value for each independent Wiener coefficient.
    /// </summary>
    private static ReadOnlySpan<int> WienerCoefficientMinimum => [-5, -23, -17];

    /// <summary>
    /// Gets the number of possible transmitted values for each independent Wiener coefficient.
    /// </summary>
    private static ReadOnlySpan<int> WienerCoefficientValueCount => [16, 32, 64];

    /// <summary>
    /// Gets the subexponential group-size exponent for each independent Wiener coefficient.
    /// </summary>
    private static ReadOnlySpan<int> WienerCoefficientSubexponentialK => [1, 2, 3];

    /// <summary>
    /// Gets the signed DC-context contribution for each packed coefficient sign class.
    /// </summary>
    private static ReadOnlySpan<int> Signs => [0, -1, 1];

    /// <summary>
    /// Gets the AV1 DC-sign entropy context for each summed neighboring sign value.
    /// </summary>
    private static ReadOnlySpan<int> DcSignContexts => [
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0,
        2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2];

    /// <summary>
    /// Gets the color-index entropy context for each weighted palette-neighbor score hash.
    /// </summary>
    private static ReadOnlySpan<int> PaletteColorIndexContexts => [-1, -1, 0, -1, -1, 4, 3, 2, 1];

    /// <summary>
    /// Gets the frame header whose tile syntax is being parsed.
    /// </summary>
    public ObuFrameHeader FrameHeader { get; }

    /// <summary>
    /// Gets the sequence header governing the frame.
    /// </summary>
    public ObuSequenceHeader SequenceHeader { get; }

    /// <summary>
    /// Gets the frame-owned mode, motion, transform, coefficient, quantizer, and filter state populated by tile parsing.
    /// </summary>
    public Av1FrameInfo FrameInfo { get; }

    /// <summary>
    /// Gets or sets the optional decoder that reconstructs each superblock immediately after its syntax is parsed.
    /// </summary>
    public IAv1FrameDecoder? FrameDecoder { get; set; }

    /// <summary>
    /// Gets the completed frame entropy context selected by the context-update tile.
    /// </summary>
    /// <remarks>
    /// The context contains either normative defaults or the selected primary-reference state until the signaled
    /// update tile has decoded successfully. Callers that retain it beyond this reader's frame lifecycle must copy it
    /// through <see cref="Av1FrameEntropyContext.SnapshotTo"/>.
    /// </remarks>
    public Av1FrameEntropyContext FrameEntropyContext => this.entropyContexts.Published;

    /// <summary>
    /// Gets the decoder-session entropy owner that publishes the completed frame context.
    /// </summary>
    public Av1FrameEntropyContexts EntropyContexts => this.entropyContexts;

    /// <summary>
    /// Returns tile-neighbor storage and the reader's frame-state lease to the configured memory allocator.
    /// </summary>
    public void Dispose()
    {
        this.aboveNeighborContext.Dispose();
        this.leftNeighborContext.Dispose();
        this.coefficientLevels.Dispose();
        if (this.ownsPaletteColorIndexMaps)
        {
            this.lumaPaletteColorIndexMap.Dispose();
            this.chromaPaletteColorIndexMap.Dispose();
        }

        this.FrameInfo.Dispose();
    }

    /// <summary>
    /// Parses one tile's partition, mode, transform, coefficient, and filter syntax in superblock order.
    /// </summary>
    /// <param name="tileData">The entropy-coded tile payload.</param>
    /// <param name="tileNum">The zero-based tile index in row-major order.</param>
    public void ReadTile(Span<byte> tileData, int tileNum)
    {
        // AV1 tiles never inherit adaptation from another tile in the same frame. Reusing one graph is safe because
        // parsing is sequential and every entry is restored from the unchanged frame base before the range decoder is
        // constructed.
        this.entropyContexts.Working.CopyFrom(this.entropyContexts.Base);

        // The frame syntax exposes a disable flag, while the range reader follows the reference decoder's positive
        // allow_update_cdf convention.
        Av1SymbolDecoder reader = new(
            this.configuration,
            tileData,
            this.entropyContexts.Working,
            !this.FrameHeader.DisableCdfUpdate);

        int tileColumnIndex = tileNum % this.FrameHeader.TilesInfo.TileColumnCount;
        int tileRowIndex = tileNum / this.FrameHeader.TilesInfo.TileColumnCount;

        int modeInfoColumnStart = this.FrameHeader.TilesInfo.TileColumnStartModeInfo[tileColumnIndex];
        int modeInfoColumnEnd = this.FrameHeader.TilesInfo.TileColumnStartModeInfo[tileColumnIndex + 1];
        int modeInfoRowStart = this.FrameHeader.TilesInfo.TileRowStartModeInfo[tileRowIndex];
        int modeInfoRowEnd = this.FrameHeader.TilesInfo.TileRowStartModeInfo[tileRowIndex + 1];
        this.aboveNeighborContext.Clear(this.SequenceHeader, modeInfoColumnStart, modeInfoColumnEnd);
        this.currentQuantizerIndex = this.FrameHeader.QuantizationParameters.BaseQIndex;
        this.ClearLoopFilterDelta();
        int planesCount = this.SequenceHeader.ColorConfig.PlaneCount;

        // AV1 fixes restoration reference storage at three planes, two directions or projection coefficients, and
        // three transmitted Wiener taps. Populate the inline value storage in place so every tile starts from the
        // normative differential-coding defaults without constructing jagged arrays.
        Span<int> sgrReferences = this.referenceSgrXqd;
        Span<int> wienerReferences = this.referenceLrWiener;
        for (int plane = 0; plane < planesCount; plane++)
        {
            SgrprojXqdMid.CopyTo(sgrReferences.Slice(plane * 2, 2));
            for (int pass = 0; pass < 2; pass++)
            {
                int referenceOffset = ((plane * 2) + pass) * Av1Constants.WienerCoefficientCount;
                WienerTapsMid.CopyTo(wienerReferences.Slice(referenceOffset, Av1Constants.WienerCoefficientCount));
            }
        }

        Av1TileInfo tileInfo = new(tileRowIndex, tileColumnIndex, this.FrameHeader);
        Av1BlockSize superBlockSize = this.SequenceHeader.SuperblockSize;
        int superBlock4x4Size = this.SequenceHeader.SuperblockSize.Get4x4WideCount();
        int superBlockSizeLog2 = this.SequenceHeader.SuperblockSizeLog2;
        for (int row = modeInfoRowStart; row < modeInfoRowEnd; row += superBlock4x4Size)
        {
            int superBlockRow = (row << Av1Constants.ModeInfoSizeLog2) >> superBlockSizeLog2;
            this.leftNeighborContext.Clear(this.SequenceHeader);
            for (int column = modeInfoColumnStart; column < modeInfoColumnEnd; column += superBlock4x4Size)
            {
                int superBlockColumn = (column << Av1Constants.ModeInfoSizeLog2) >> superBlockSizeLog2;
                Point superblockPosition = new(superBlockColumn, superBlockRow);
                Av1SuperblockInfo superblockInfo = this.FrameInfo.GetSuperblock(superblockPosition);

                Point modeInfoPosition = new(column, row);
                superblockInfo.CoefficientsY.Clear();
                superblockInfo.CoefficientsU.Clear();
                superblockInfo.CoefficientsV.Clear();
                this.FrameInfo.ClearCdef(superblockPosition);
                this.firstTransformOffset[0] = 0;
                this.firstTransformOffset[1] = 0;
                this.coefficientIndex[..Av1Constants.MaxPlanes].Clear();
                this.ReadLoopRestoration(ref reader, modeInfoPosition, superBlockSize);
                this.ParsePartition(ref reader, modeInfoPosition, superBlockSize, superblockInfo, tileInfo);

                // Identify-only parsing omits a frame decoder but still populates the complete syntax model.
                this.FrameDecoder?.DecodeSuperblock(modeInfoPosition, superblockInfo, tileInfo);
            }
        }

        // Range decoding may read implicit zero padding while normalizing its final interval. Validate the logical
        // stopping position before publishing either pixels or adapted CDF state so a truncated tile cannot commit.
        reader.ValidateTrailingBits();

        if (!this.FrameHeader.DisableFrameEndUpdateCdf && tileNum == this.FrameHeader.TilesInfo.ContextUpdateTileId)
        {
            // the reference decoder publishes only context_update_tile_id after every tile has independently started from the frame
            // base, then clears its CDF counters. Snapshotting into a third reusable graph preserves the unchanged base
            // for tiles that follow the selected tile in bitstream order.
            this.entropyContexts.Working.SnapshotTo(this.entropyContexts.Published);
        }
    }

    /// <summary>
    /// Completes the current coded frame.
    /// </summary>
    public void CompleteFrame()
    {
        // Tile parsing and optional incremental superblock reconstruction finish inside ReadTile. The owning AV1
        // decoder uses this lifecycle boundary to assemble native planes. A directly created tile reader owns its
        // neighbor-context rents, so the same boundary must return them before ObuReader releases the frame instance.
        this.Dispose();
    }

    /// <summary>
    /// Resets the loop-filter delta predictors before parsing a tile.
    /// </summary>
    private void ClearLoopFilterDelta()
        => this.currentDeltaLoopFilter[..Av1Constants.FrameLoopFilterCount].Clear();

    /// <summary>
    /// Reads loop-restoration unit syntax that begins at a superblock location.
    /// </summary>
    /// <param name="reader">The tile symbol decoder.</param>
    /// <param name="modeInfoLocation">The superblock origin in 4x4 mode-information units.</param>
    /// <param name="superBlockSize">The superblock size.</param>
    private void ReadLoopRestoration(ref Av1SymbolDecoder reader, Point modeInfoLocation, Av1BlockSize superBlockSize)
    {
        ObuColorConfig colorConfig = this.SequenceHeader.ColorConfig;
        int planesCount = colorConfig.PlaneCount;
        for (int plane = 0; plane < planesCount; plane++)
        {
            ObuLoopRestorationItem item = this.FrameHeader.LoopRestorationParameters.Items[plane];
            if (item.Type == ObuRestorationType.None)
            {
                continue;
            }

            int subsamplingX = plane > 0 && colorConfig.SubSamplingX ? 1 : 0;
            int subsamplingY = plane > 0 && colorConfig.SubSamplingY ? 1 : 0;
            int planeHeight = Av1Math.DivideLog2Ceiling(this.FrameHeader.FrameSize.FrameHeight, subsamplingY);
            int unitColumnCount = this.FrameInfo.GetLoopRestorationUnitColumnCount(plane);
            int unitRowCount = Math.Max((planeHeight + (item.Size >> 1)) / item.Size, 1);
            int superblockModeInfoSize = superBlockSize.Get4x4WideCount();
            int modeInfoColumnEnd = modeInfoLocation.X + superblockModeInfoSize;
            int modeInfoRowEnd = modeInfoLocation.Y + superblockModeInfoSize;
            int modeInfoSampleWidth = (1 << Av1Constants.ModeInfoSizeLog2) >> subsamplingX;
            int modeInfoSampleHeight = (1 << Av1Constants.ModeInfoSizeLog2) >> subsamplingY;
            bool usesSuperResolution =
                this.FrameHeader.FrameSize.FrameWidth != this.FrameHeader.FrameSize.SuperResolutionUpscaledWidth;

            int columnNumeratorScale = usesSuperResolution
                ? modeInfoSampleWidth * this.FrameHeader.FrameSize.SuperResolutionDenominator
                : modeInfoSampleWidth;

            int columnDenominator = usesSuperResolution
                ? item.Size * Av1Constants.ScaleNumerator
                : item.Size;

            int rowDenominator = item.Size;

            // Restoration syntax is attached to the superblock containing each unit's upper-left
            // corner. Super-resolution changes only the horizontal corner conversion.
            int unitColumnStart = DivideCeiling(modeInfoLocation.X * columnNumeratorScale, columnDenominator);
            int unitColumnEnd = Math.Min(DivideCeiling(modeInfoColumnEnd * columnNumeratorScale, columnDenominator), unitColumnCount);
            int unitRowStart = DivideCeiling(modeInfoLocation.Y * modeInfoSampleHeight, rowDenominator);
            int unitRowEnd = Math.Min(DivideCeiling(modeInfoRowEnd * modeInfoSampleHeight, rowDenominator), unitRowCount);
            for (int unitRow = unitRowStart; unitRow < unitRowEnd; unitRow++)
            {
                for (int unitColumn = unitColumnStart; unitColumn < unitColumnEnd; unitColumn++)
                {
                    ref Av1LoopRestorationUnit unit = ref this.FrameInfo.GetLoopRestorationUnit(plane, unitRow, unitColumn);
                    this.ReadLoopRestorationUnit(ref reader, item.Type, plane, ref unit);
                }
            }
        }
    }

    /// <summary>
    /// Reads the filter selection and coefficients for one loop-restoration unit.
    /// </summary>
    /// <param name="reader">The tile symbol decoder.</param>
    /// <param name="frameType">The restoration mode allowed by the frame header.</param>
    /// <param name="plane">The zero-based color-plane index.</param>
    /// <param name="unit">The destination restoration-unit information.</param>
    private void ReadLoopRestorationUnit(
        ref Av1SymbolDecoder reader,
        ObuRestorationType frameType,
        int plane,
        ref Av1LoopRestorationUnit unit)
    {
        unit.FilterType = frameType switch
        {
            ObuRestorationType.Switchable => reader.ReadSwitchableRestorationType(),
            ObuRestorationType.Wiener => reader.ReadWienerRestoration()
                ? Av1RestorationFilterType.Wiener
                : Av1RestorationFilterType.None,
            ObuRestorationType.SgrProj => reader.ReadSgrProjectionRestoration()
                ? Av1RestorationFilterType.SgrProjection
                : Av1RestorationFilterType.None,
            _ => Av1RestorationFilterType.None,
        };

        if (unit.FilterType == Av1RestorationFilterType.Wiener)
        {
            this.ReadWienerFilter(ref reader, plane, ref unit);
        }
        else if (unit.FilterType == Av1RestorationFilterType.SgrProjection)
        {
            this.ReadSgrProjectionFilter(ref reader, plane, ref unit);
        }
    }

    /// <summary>
    /// Reads the symmetric vertical and horizontal Wiener coefficients for one restoration unit.
    /// </summary>
    /// <param name="reader">The tile symbol decoder.</param>
    /// <param name="plane">The zero-based color-plane index.</param>
    /// <param name="unit">The destination restoration-unit information.</param>
    private void ReadWienerFilter(ref Av1SymbolDecoder reader, int plane, ref Av1LoopRestorationUnit unit)
    {
        for (int pass = 0; pass < 2; pass++)
        {
            int firstCoefficient = plane == 0 ? 0 : 1;
            if (pass == 0)
            {
                unit.WienerVertical[0] = 0;
            }
            else
            {
                unit.WienerHorizontal[0] = 0;
            }

            for (int coefficient = firstCoefficient; coefficient < Av1Constants.WienerCoefficientCount; coefficient++)
            {
                int referenceIndex = (((plane * 2) + pass) * Av1Constants.WienerCoefficientCount) + coefficient;
                int minimum = WienerCoefficientMinimum[coefficient];
                int value = reader.ReadReferenceSubexponential(
                    WienerCoefficientValueCount[coefficient],
                    WienerCoefficientSubexponentialK[coefficient],
                    this.referenceLrWiener[referenceIndex] - minimum);

                value += minimum;
                if (pass == 0)
                {
                    unit.WienerVertical[coefficient] = value;
                }
                else
                {
                    unit.WienerHorizontal[coefficient] = value;
                }

                this.referenceLrWiener[referenceIndex] = value;
            }
        }
    }

    /// <summary>
    /// Reads the parameter-set index and projection coefficients for one self-guided restoration unit.
    /// </summary>
    /// <param name="reader">The tile symbol decoder.</param>
    /// <param name="plane">The zero-based color-plane index.</param>
    /// <param name="unit">The destination restoration-unit information.</param>
    private void ReadSgrProjectionFilter(ref Av1SymbolDecoder reader, int plane, ref Av1LoopRestorationUnit unit)
    {
        unit.SgrParameterSet = reader.ReadLiteral(4);
        int[] radii = SgrProjectionRadii[unit.SgrParameterSet];
        Span<int> coefficients = unit.SgrProjectionCoefficients;
        Span<int> allReferences = this.referenceSgrXqd;
        Span<int> references = allReferences.Slice(plane * 2, 2);
        if (radii[0] == 0)
        {
            coefficients[0] = 0;
            coefficients[1] = ReadSgrProjectionCoefficient(ref reader, references[1], SgrProjectionCoefficient1Minimum);
        }
        else if (radii[1] == 0)
        {
            coefficients[0] = ReadSgrProjectionCoefficient(ref reader, references[0], SgrProjectionCoefficient0Minimum);

            // When the second filter is disabled, AV1 derives the missing projection coefficient
            // so the combined projection retains its fixed seven-bit scale.
            coefficients[1] = Av1Math.Clip3(
                SgrProjectionCoefficient1Minimum,
                SgrProjectionCoefficient1Minimum + SgrProjectionCoefficientValueCount - 1,
                SgrProjectionCoefficientValueCount - coefficients[0]);
        }
        else
        {
            coefficients[0] = ReadSgrProjectionCoefficient(ref reader, references[0], SgrProjectionCoefficient0Minimum);
            coefficients[1] = ReadSgrProjectionCoefficient(ref reader, references[1], SgrProjectionCoefficient1Minimum);
        }

        coefficients.CopyTo(references);
    }

    /// <summary>
    /// Reads one differentially coded self-guided projection coefficient.
    /// </summary>
    /// <param name="reader">The tile symbol decoder.</param>
    /// <param name="reference">The preceding coefficient value for the plane.</param>
    /// <param name="minimum">The minimum value in the coefficient domain.</param>
    /// <returns>The decoded signed coefficient.</returns>
    private static int ReadSgrProjectionCoefficient(ref Av1SymbolDecoder reader, int reference, int minimum)
        => reader.ReadReferenceSubexponential(
            SgrProjectionCoefficientValueCount,
            SgrProjectionSubexponentialK,
            reference - minimum) + minimum;

    /// <summary>
    /// Divides a non-negative numerator by a positive denominator and rounds upward.
    /// </summary>
    /// <param name="numerator">The non-negative numerator.</param>
    /// <param name="denominator">The positive denominator.</param>
    /// <returns>The ceiling of the quotient.</returns>
    private static int DivideCeiling(int numerator, int denominator)
        => (numerator + denominator - 1) / denominator;

    /// <summary>
    /// Decodes AV1 partition syntax and recursively visits each resulting coding block.
    /// </summary>
    /// <param name="reader">The tile symbol decoder.</param>
    /// <param name="modeInfoLocation">The parent block origin in 4x4 mode-information units.</param>
    /// <param name="blockSize">The parent block size.</param>
    /// <param name="superblockInfo">The containing superblock.</param>
    /// <param name="tileInfo">The active tile boundaries.</param>
    /// <remarks>Implements AV1 section 5.11.4.</remarks>
    private void ParsePartition(ref Av1SymbolDecoder reader, Point modeInfoLocation, Av1BlockSize blockSize, Av1SuperblockInfo superblockInfo, Av1TileInfo tileInfo)
    {
        int columnIndex = modeInfoLocation.X;
        int rowIndex = modeInfoLocation.Y;
        if (modeInfoLocation.Y >= this.FrameHeader.ModeInfoRowCount || modeInfoLocation.X >= this.FrameHeader.ModeInfoColumnCount)
        {
            return;
        }

        int block4x4Size = blockSize.Get4x4WideCount();
        int halfBlock4x4Size = block4x4Size >> 1;
        int quarterBlock4x4Size = halfBlock4x4Size >> 1;
        bool hasRows = (modeInfoLocation.Y + halfBlock4x4Size) < this.FrameHeader.ModeInfoRowCount;
        bool hasColumns = (modeInfoLocation.X + halfBlock4x4Size) < this.FrameHeader.ModeInfoColumnCount;
        Av1PartitionType partitionType = Av1PartitionType.None;
        if (blockSize >= Av1BlockSize.Block8x8)
        {
            int ctx = this.GetPartitionPlaneContext(modeInfoLocation, blockSize, tileInfo, superblockInfo);
            partitionType = Av1PartitionType.Split;
            if (hasRows && hasColumns)
            {
                partitionType = reader.ReadPartitionType(ctx);
            }
            else if (hasColumns)
            {
                partitionType = reader.ReadSplitOrHorizontal(blockSize, ctx);
            }
            else if (hasRows)
            {
                partitionType = reader.ReadSplitOrVertical(blockSize, ctx);
            }
        }

        Av1BlockSize subSize = partitionType.GetBlockSubSize(blockSize);
        if (subSize == Av1BlockSize.Invalid)
        {
            throw new InvalidImageContentException($"The decoded AV1 partition type {partitionType} is invalid for block size {blockSize}.");
        }

        ObuColorConfig colorConfig = this.SequenceHeader.ColorConfig;
        if (subSize.GetSubsampled(colorConfig.SubSamplingX, colorConfig.SubSamplingY) == Av1BlockSize.Invalid)
        {
            // Luma partition syntax can describe a sub-8x8 shape that has no legal representation after chroma
            // subsampling. Reject it before any block state is published, matching the reference decoder's decode_partition boundary.
            throw new InvalidImageContentException($"The decoded AV1 block size {subSize} is invalid for the sequence chroma subsampling.");
        }

        Av1BlockSize splitSize = Av1PartitionType.Split.GetBlockSubSize(blockSize);

        // Partition syntax is depth-first. The visit order here is also the order in which mode,
        // transform, and coefficient records are appended to their frame-owned arrays.
        switch (partitionType)
        {
            case Av1PartitionType.Split:
                Point loc1 = new(modeInfoLocation.X + halfBlock4x4Size, modeInfoLocation.Y);
                Point loc2 = new(modeInfoLocation.X, modeInfoLocation.Y + halfBlock4x4Size);
                Point loc3 = new(modeInfoLocation.X + halfBlock4x4Size, modeInfoLocation.Y + halfBlock4x4Size);
                this.ParsePartition(ref reader, modeInfoLocation, subSize, superblockInfo, tileInfo);
                this.ParsePartition(ref reader, loc1, subSize, superblockInfo, tileInfo);
                this.ParsePartition(ref reader, loc2, subSize, superblockInfo, tileInfo);
                this.ParsePartition(ref reader, loc3, subSize, superblockInfo, tileInfo);
                break;
            case Av1PartitionType.None:
                this.ParseBlock(ref reader, modeInfoLocation, subSize, superblockInfo, tileInfo, Av1PartitionType.None);
                break;
            case Av1PartitionType.Horizontal:
                this.ParseBlock(ref reader, modeInfoLocation, subSize, superblockInfo, tileInfo, Av1PartitionType.Horizontal);
                if (hasRows)
                {
                    Point halfLocation = new(columnIndex, rowIndex + halfBlock4x4Size);
                    this.ParseBlock(ref reader, halfLocation, subSize, superblockInfo, tileInfo, Av1PartitionType.Horizontal);
                }

                break;
            case Av1PartitionType.Vertical:
                this.ParseBlock(ref reader, modeInfoLocation, subSize, superblockInfo, tileInfo, Av1PartitionType.Vertical);
                if (hasColumns)
                {
                    Point halfLocation = new(columnIndex + halfBlock4x4Size, rowIndex);
                    this.ParseBlock(ref reader, halfLocation, subSize, superblockInfo, tileInfo, Av1PartitionType.Vertical);
                }

                break;
            case Av1PartitionType.HorizontalA:
                this.ParseBlock(ref reader, modeInfoLocation, splitSize, superblockInfo, tileInfo, Av1PartitionType.HorizontalA);
                Point locHorA1 = new(columnIndex + halfBlock4x4Size, rowIndex);
                this.ParseBlock(ref reader, locHorA1, splitSize, superblockInfo, tileInfo, Av1PartitionType.HorizontalA);
                Point locHorA2 = new(columnIndex, rowIndex + halfBlock4x4Size);
                this.ParseBlock(ref reader, locHorA2, subSize, superblockInfo, tileInfo, Av1PartitionType.HorizontalA);
                break;
            case Av1PartitionType.HorizontalB:
                this.ParseBlock(ref reader, modeInfoLocation, subSize, superblockInfo, tileInfo, Av1PartitionType.HorizontalB);
                Point locHorB1 = new(columnIndex, rowIndex + halfBlock4x4Size);
                this.ParseBlock(ref reader, locHorB1, splitSize, superblockInfo, tileInfo, Av1PartitionType.HorizontalB);
                Point locHorB2 = new(columnIndex + halfBlock4x4Size, rowIndex + halfBlock4x4Size);
                this.ParseBlock(ref reader, locHorB2, splitSize, superblockInfo, tileInfo, Av1PartitionType.HorizontalB);
                break;
            case Av1PartitionType.VerticalA:
                this.ParseBlock(ref reader, modeInfoLocation, splitSize, superblockInfo, tileInfo, Av1PartitionType.VerticalA);
                Point locVertA1 = new(columnIndex, rowIndex + halfBlock4x4Size);
                this.ParseBlock(ref reader, locVertA1, splitSize, superblockInfo, tileInfo, Av1PartitionType.VerticalA);
                Point locVertA2 = new(columnIndex + halfBlock4x4Size, rowIndex);
                this.ParseBlock(ref reader, locVertA2, subSize, superblockInfo, tileInfo, Av1PartitionType.VerticalA);
                break;
            case Av1PartitionType.VerticalB:
                this.ParseBlock(ref reader, modeInfoLocation, subSize, superblockInfo, tileInfo, Av1PartitionType.VerticalB);
                Point locVertB1 = new(columnIndex + halfBlock4x4Size, rowIndex);
                this.ParseBlock(ref reader, locVertB1, splitSize, superblockInfo, tileInfo, Av1PartitionType.VerticalB);
                Point locVertB2 = new(columnIndex + halfBlock4x4Size, rowIndex + halfBlock4x4Size);
                this.ParseBlock(ref reader, locVertB2, splitSize, superblockInfo, tileInfo, Av1PartitionType.VerticalB);
                break;
            case Av1PartitionType.Horizontal4:
                for (int i = 0; i < 4; i++)
                {
                    int currentBlockRow = rowIndex + (i * quarterBlock4x4Size);
                    if (i > 0 && currentBlockRow >= this.FrameHeader.ModeInfoRowCount)
                    {
                        break;
                    }

                    Point currentLocation = new(modeInfoLocation.X, currentBlockRow);
                    this.ParseBlock(ref reader, currentLocation, subSize, superblockInfo, tileInfo, Av1PartitionType.Horizontal4);
                }

                break;
            case Av1PartitionType.Vertical4:
                for (int i = 0; i < 4; i++)
                {
                    int currentBlockColumn = columnIndex + (i * quarterBlock4x4Size);
                    if (i > 0 && currentBlockColumn >= this.FrameHeader.ModeInfoColumnCount)
                    {
                        break;
                    }

                    Point currentLocation = new(currentBlockColumn, modeInfoLocation.Y);
                    this.ParseBlock(ref reader, currentLocation, subSize, superblockInfo, tileInfo, Av1PartitionType.Vertical4);
                }

                break;
            default:
                throw new InvalidImageContentException($"The decoded AV1 partition type {partitionType} is invalid.");
        }

        this.UpdatePartitionContext(new Point(columnIndex, rowIndex), tileInfo, superblockInfo, subSize, blockSize, partitionType);
    }

    /// <summary>
    /// Parses all syntax associated with one final coding block and stores its frame mode information.
    /// </summary>
    /// <param name="reader">The tile symbol decoder.</param>
    /// <param name="modeInfoLocation">The block origin in 4x4 mode-information units.</param>
    /// <param name="blockSize">The final block size.</param>
    /// <param name="superblockInfo">The containing superblock.</param>
    /// <param name="tileInfo">The active tile boundaries.</param>
    /// <param name="partitionType">The partition type that produced the block.</param>
    private void ParseBlock(ref Av1SymbolDecoder reader, Point modeInfoLocation, Av1BlockSize blockSize, Av1SuperblockInfo superblockInfo, Av1TileInfo tileInfo, Av1PartitionType partitionType)
    {
        int rowIndex = modeInfoLocation.Y;
        int columnIndex = modeInfoLocation.X;
        int block4x4Width = blockSize.Get4x4WideCount();
        int block4x4Height = blockSize.Get4x4HighCount();
        Point superblockLocation = superblockInfo.Position * this.SequenceHeader.SuperblockModeInfoSize;
        Point locationInSuperblock = new Point(modeInfoLocation.X - superblockLocation.X, modeInfoLocation.Y - superblockLocation.Y);
        Av1BlockModeInfo blockModeInfo = new(blockSize, locationInSuperblock);
        blockModeInfo.PartitionType = partitionType;
        blockModeInfo.SetFirstTransformLocation(Av1PlaneType.Y, this.firstTransformOffset[0]);
        blockModeInfo.SetFirstTransformLocation(Av1PlaneType.Uv, this.firstTransformOffset[1]);
        bool hasChroma = HasChroma(this.SequenceHeader, modeInfoLocation, blockSize);
        Av1PartitionInfo partitionInfo = new(blockModeInfo, superblockInfo, hasChroma, partitionType);
        partitionInfo.ColumnIndex = columnIndex;
        partitionInfo.RowIndex = rowIndex;
        partitionInfo.ComputeBoundaryOffsets(this.SequenceHeader, this.FrameHeader, tileInfo);
        if (hasChroma)
        {
            if (this.SequenceHeader.ColorConfig.SubSamplingY && block4x4Height == 1)
            {
                partitionInfo.AvailableAboveForChroma = IsInside(tileInfo, rowIndex - 2, columnIndex);
            }

            if (this.SequenceHeader.ColorConfig.SubSamplingX && block4x4Width == 1)
            {
                partitionInfo.AvailableLeftForChroma = IsInside(tileInfo, rowIndex, columnIndex - 2);
            }
        }

        partitionInfo.PopulateModeInfoNeighbors(this.SequenceHeader.ColorConfig);

        this.ReadModeInfo(ref reader, ref partitionInfo, tileInfo);

        this.ReadPaletteTokens(ref reader, ref partitionInfo);
        this.ReadBlockTransformSize(ref reader, modeInfoLocation, ref partitionInfo, superblockInfo, tileInfo);

        if (partitionInfo.ModeInfo.Skip)
        {
            this.ResetSkipContext(ref partitionInfo, tileInfo);
        }

        this.Residual(ref reader, ref partitionInfo, superblockInfo, tileInfo, blockSize);

        // Record compact frame evidence before later frames release this frame's full mode-information graph.
        this.FrameInfo.RecordInterPredictionFeatures(partitionInfo.ModeInfo, this.FrameHeader);

        // Store the record only after all syntax has populated it, then map every covered 4x4 position.
        this.FrameInfo.UpdateModeInfo(partitionInfo.ModeInfo, superblockInfo);
    }

    /// <summary>
    /// Clears coefficient neighbor contexts across every plane of a skipped block.
    /// </summary>
    /// <param name="partitionInfo">The skipped block and its frame position.</param>
    /// <param name="tileInfo">The active tile boundaries.</param>
    /// <remarks>Implements AV1 section 5.11.37.</remarks>
    private void ResetSkipContext(ref Av1PartitionInfo partitionInfo, Av1TileInfo tileInfo)
    {
        // Subsampled 4x4 luma blocks can share chroma ownership with an adjacent luma block. A skipped block that is
        // not the chroma reference must preserve those shared coefficient contexts for the owning block.
        int planesCount = partitionInfo.IsChroma ? this.SequenceHeader.ColorConfig.PlaneCount : 1;
        for (int i = 0; i < planesCount; i++)
        {
            int subX = (i > 0 && this.SequenceHeader.ColorConfig.SubSamplingX) ? 1 : 0;
            int subY = (i > 0 && this.SequenceHeader.ColorConfig.SubSamplingY) ? 1 : 0;
            Av1BlockSize planeBlockSize = partitionInfo.ModeInfo.BlockSize.GetSubsampled(subX, subY);
            DebugGuard.IsTrue(planeBlockSize != Av1BlockSize.Invalid, nameof(planeBlockSize));
            int txsWide = planeBlockSize.GetWidth() >> 2;
            int txsHigh = planeBlockSize.GetHeight() >> 2;
            int aboveOffset = (partitionInfo.ColumnIndex - tileInfo.ModeInfoColumnStart) >> subX;
            int leftOffset = (partitionInfo.RowIndex - partitionInfo.SuperblockInfo.ModeInfoPosition.Y) >> subY;
            this.aboveNeighborContext.ClearContext(i, aboveOffset, txsWide);
            this.leftNeighborContext.ClearContext(i, leftOffset, txsHigh);
        }
    }

    /// <summary>
    /// Parses every luma and chroma transform block and its coefficients for a coding block.
    /// </summary>
    /// <param name="reader">The tile symbol decoder.</param>
    /// <param name="partitionInfo">The current coding block.</param>
    /// <param name="superblockInfo">The containing superblock and coefficient storage.</param>
    /// <param name="tileInfo">The active tile boundaries.</param>
    /// <param name="blockSize">The coding block size.</param>
    /// <remarks>Implements AV1 section 5.11.34.</remarks>
    private void Residual(
        ref Av1SymbolDecoder reader,
        ref Av1PartitionInfo partitionInfo,
        Av1SuperblockInfo superblockInfo,
        Av1TileInfo tileInfo,
        Av1BlockSize blockSize)
    {
        int maxBlocksWide = partitionInfo.GetMaxBlockWide(blockSize, false);
        int maxBlocksHigh = partitionInfo.GetMaxBlockHigh(blockSize, false);
        Av1BlockSize maxUnitSize = Av1BlockSize.Block64x64;
        int modeUnitBlocksWide = maxUnitSize.GetWidth() >> 2;
        int modeUnitBlocksHigh = maxUnitSize.GetHeight() >> 2;
        modeUnitBlocksWide = Math.Min(maxBlocksWide, modeUnitBlocksWide);
        modeUnitBlocksHigh = Math.Min(maxBlocksHigh, modeUnitBlocksHigh);
        int planeCount = this.SequenceHeader.ColorConfig.PlaneCount;
        bool isLossless = this.FrameHeader.LosslessArray[partitionInfo.ModeInfo.SegmentId];
        bool isLosslessBlock = isLossless && (blockSize >= Av1BlockSize.Block64x64) && (blockSize <= Av1BlockSize.Block128x128);
        int subSampling = (this.SequenceHeader.ColorConfig.SubSamplingX ? 1 : 0) + (this.SequenceHeader.ColorConfig.SubSamplingY ? 1 : 0);
        int chromaTransformUnitCount = isLosslessBlock
            ? (maxBlocksWide * maxBlocksHigh) >> subSampling
            : partitionInfo.ModeInfo.GetTransformUnitCount(Av1PlaneType.Uv);

        int lumaTransformInfoIndex = partitionInfo.ModeInfo.GetFirstTransformLocation(Av1PlaneType.Y);
        int chromaBlueTransformInfoIndex = partitionInfo.ModeInfo.GetFirstTransformLocation(Av1PlaneType.Uv);
        int chromaRedTransformInfoIndex = chromaBlueTransformInfoIndex + chromaTransformUnitCount;
        int forceSplitCount = 0;

        // AV1 forces residual traversal into at most 64x64 regions even when the coding block is larger.
        // transformUnitCount preserves the transform geometry generated for each such region and plane.
        for (int row = 0; row < maxBlocksHigh; row += modeUnitBlocksHigh)
        {
            for (int column = 0; column < maxBlocksWide; column += modeUnitBlocksWide)
            {
                for (int plane = 0; plane < planeCount; ++plane)
                {
                    int totalTransformUnitCount;
                    int transformUnitCount;
                    int subX = (plane > 0 && this.SequenceHeader.ColorConfig.SubSamplingX) ? 1 : 0;
                    int subY = (plane > 0 && this.SequenceHeader.ColorConfig.SubSamplingY) ? 1 : 0;
                    int transformInfoIndex = plane switch
                    {
                        0 => lumaTransformInfoIndex,
                        1 => chromaBlueTransformInfoIndex,
                        _ => chromaRedTransformInfoIndex,
                    };

                    if (plane != 0 && !partitionInfo.IsChroma)
                    {
                        continue;
                    }

                    Span<Av1TransformInfo> transformInfoSpan = (plane == 0) ? superblockInfo.GetTransformInfoY() : superblockInfo.GetTransformInfoUv();
                    if (isLosslessBlock)
                    {
                        // Lossless coding fixes transforms at 4x4, so count each clipped 4x4 unit
                        // directly after applying the plane's chroma subsampling.
                        int unitHeight = Av1Math.RoundPowerOf2(Math.Min(modeUnitBlocksHigh + row, maxBlocksHigh), 0);
                        int unitWidth = Av1Math.RoundPowerOf2(Math.Min(modeUnitBlocksWide + column, maxBlocksWide), 0);
                        DebugGuard.IsTrue(transformInfoSpan[transformInfoIndex].Size == Av1TransformSize.Size4x4, "Lossless frame shall have transform units of size 4x4.");
                        transformUnitCount = ((unitWidth - column) * (unitHeight - row)) >> (subX + subY);
                    }
                    else
                    {
                        totalTransformUnitCount = partitionInfo.ModeInfo.GetTransformUnitCount((Av1Plane)plane);
                        transformUnitCount = this.transformUnitCount[plane][forceSplitCount];

                        DebugGuard.IsFalse(totalTransformUnitCount == 0, nameof(totalTransformUnitCount), string.Empty);
                        DebugGuard.IsTrue(
                            totalTransformUnitCount ==
                                this.transformUnitCount[plane][0] + this.transformUnitCount[plane][1] +
                                this.transformUnitCount[plane][2] + this.transformUnitCount[plane][3],
                            nameof(totalTransformUnitCount),
                            string.Empty);
                    }

                    DebugGuard.IsFalse(transformUnitCount == 0, nameof(transformUnitCount), string.Empty);
                    for (int tu = 0; tu < transformUnitCount; tu++)
                    {
                        ref Av1TransformInfo transformInfo = ref transformInfoSpan[transformInfoIndex];
                        DebugGuard.MustBeLessThanOrEqualTo(transformInfo.OffsetX, maxBlocksWide, nameof(transformInfo));
                        DebugGuard.MustBeLessThanOrEqualTo(transformInfo.OffsetY, maxBlocksHigh, nameof(transformInfo));

                        int coefficientIndex = this.coefficientIndex[plane];
                        int endOfBlock = 0;
                        int blockColumn = transformInfo.OffsetX;
                        int blockRow = transformInfo.OffsetY;
                        int startX = (partitionInfo.ColumnIndex >> subX) + blockColumn;
                        int startY = (partitionInfo.RowIndex >> subY) + blockRow;

                        if (startX >= (this.FrameHeader.ModeInfoColumnCount >> subX) ||
                            startY >= (this.FrameHeader.ModeInfoRowCount >> subY))
                        {
                            return;
                        }

                        if (!partitionInfo.ModeInfo.Skip)
                        {
                            Span<int> coefficientBuffer = superblockInfo.GetCoefficients((Av1Plane)plane)[coefficientIndex..];
                            endOfBlock = this.ParseTransformBlock(
                                ref reader,
                                ref partitionInfo,
                                tileInfo,
                                coefficientBuffer,
                                ref transformInfo,
                                plane,
                                blockColumn,
                                blockRow,
                                startX,
                                startY,
                                transformInfo.Size,
                                subX != 0,
                                subY != 0);
                        }

                        if (endOfBlock != 0)
                        {
                            // Coefficients are stored as an end index followed by scan-order values, so the
                            // next transform begins after both the prefix and its decoded coefficient range.
                            this.coefficientIndex[plane] += endOfBlock + 1;
                            transformInfo.CodeBlockFlag = true;
                        }
                        else
                        {
                            transformInfo.CodeBlockFlag = false;
                        }

                        transformInfoIndex++;
                    }

                    // Each plane advances independently because its transform descriptors occupy a separate
                    // contiguous range. Scalar cursors avoid allocating a three-element array for every block.
                    switch (plane)
                    {
                        case 0:
                            lumaTransformInfoIndex = transformInfoIndex;
                            break;
                        case 1:
                            chromaBlueTransformInfoIndex = transformInfoIndex;
                            break;
                        default:
                            chromaRedTransformInfoIndex = transformInfoIndex;
                            break;
                    }
                }

                forceSplitCount++;
            }
        }
    }

    /// <summary>
    /// Determines whether a luma coding block owns chroma mode and residual syntax at its frame position.
    /// </summary>
    /// <param name="sequenceHeader">The sequence header describing chroma subsampling.</param>
    /// <param name="modeInfoLocation">The block origin in 4x4 luma mode-information units.</param>
    /// <param name="blockSize">The luma block size.</param>
    /// <returns><see langword="true"/> when the block is a chroma reference position; otherwise, <see langword="false"/>.</returns>
    public static bool HasChroma(ObuSequenceHeader sequenceHeader, Point modeInfoLocation, Av1BlockSize blockSize)
    {
        int blockWide = blockSize.Get4x4WideCount();
        int blockHigh = blockSize.Get4x4HighCount();
        bool subX = sequenceHeader.ColorConfig.SubSamplingX;
        bool subY = sequenceHeader.ColorConfig.SubSamplingY;
        bool hasChroma = ((modeInfoLocation.Y & 0x01) != 0 || (blockHigh & 0x01) == 0 || !subY) &&
            ((modeInfoLocation.X & 0x01) != 0 || (blockWide & 0x01) == 0 || !subX);
        return hasChroma;
    }

    /// <summary>
    /// Derives a transform block's entropy context and decodes its coefficient syntax.
    /// </summary>
    /// <param name="reader">The tile symbol decoder.</param>
    /// <param name="partitionInfo">The containing coding block.</param>
    /// <param name="tileInfo">The active tile boundaries.</param>
    /// <param name="coefficientBuffer">The destination beginning at this transform's coefficient slot.</param>
    /// <param name="transformInfo">The transform geometry and syntax state to populate.</param>
    /// <param name="plane">The zero-based color-plane index.</param>
    /// <param name="blockColumn">The transform's horizontal offset within the coding block in 4x4 units.</param>
    /// <param name="blockRow">The transform's vertical offset within the coding block in 4x4 units.</param>
    /// <param name="startX">The frame-relative transform column in 4x4 units of the target plane.</param>
    /// <param name="startY">The frame-relative transform row in 4x4 units of the target plane.</param>
    /// <param name="transformSize">The transform size.</param>
    /// <param name="subX">A value indicating whether the target plane is horizontally subsampled.</param>
    /// <param name="subY">A value indicating whether the target plane is vertically subsampled.</param>
    /// <returns>The decoded end-of-block coefficient position, or zero for an all-zero transform.</returns>
    /// <remarks>
    /// Implements AV1 section 5.11.35.
    /// </remarks>
    private int ParseTransformBlock(
        ref Av1SymbolDecoder reader,
        ref Av1PartitionInfo partitionInfo,
        Av1TileInfo tileInfo,
        Span<int> coefficientBuffer,
        ref Av1TransformInfo transformInfo,
        int plane,
        int blockColumn,
        int blockRow,
        int startX,
        int startY,
        Av1TransformSize transformSize,
        bool subX,
        bool subY)
    {
        int endOfBlock = 0;
        Av1BlockSize planeBlockSize = partitionInfo.ModeInfo.BlockSize.GetSubsampled(subX, subY);
        int transformBlockUnitWideCount = transformSize.Get4x4WideCount();
        int transformBlockUnitHighCount = transformSize.Get4x4HighCount();

        if (partitionInfo.ModeBlockToRightEdge < 0)
        {
            int blocksWide = partitionInfo.GetMaxBlockWide(planeBlockSize, subX);
            transformBlockUnitWideCount = Math.Min(transformBlockUnitWideCount, blocksWide - blockColumn);
        }

        if (partitionInfo.ModeBlockToBottomEdge < 0)
        {
            int blocksHigh = partitionInfo.GetMaxBlockHigh(planeBlockSize, subY);
            transformBlockUnitHighCount = Math.Min(transformBlockUnitHighCount, blocksHigh - blockRow);
        }

        int aboveContextOffset = startX - (tileInfo.ModeInfoColumnStart >> (subX ? 1 : 0));
        int superblockRow = partitionInfo.SuperblockInfo.ModeInfoPosition.Y >> (subY ? 1 : 0);
        int leftContextOffset = startY - superblockRow;

        // Above contexts are tile-column relative, while left contexts are reused from the start of each
        // superblock row. Slicing both arrays here gives the entropy derivation the same pointer bases as the reference decoder.
        Av1TransformBlockContext transformBlockContext = this.GetTransformBlockContext(
            transformSize,
            plane,
            planeBlockSize,
            transformBlockUnitHighCount,
            transformBlockUnitWideCount,
            aboveContextOffset,
            leftContextOffset);

        endOfBlock = this.ParseCoefficients(
            ref reader,
            ref partitionInfo,
            blockRow,
            blockColumn,
            aboveContextOffset,
            leftContextOffset,
            plane,
            transformBlockContext,
            transformSize,
            ref transformInfo,
            coefficientBuffer);

        if (plane == 0)
        {
            this.FrameInfo.RecordLumaTransformType(transformInfo.Type);
        }

        return endOfBlock;
    }

    /// <summary>
    /// Decodes transform coefficients and updates the coefficient neighbor contexts for one color plane.
    /// </summary>
    /// <param name="reader">The tile symbol decoder.</param>
    /// <param name="partitionInfo">The containing coding block.</param>
    /// <param name="blockRow">The transform row within the coding block in 4x4 units of the target plane.</param>
    /// <param name="blockColumn">The transform column within the coding block in 4x4 units of the target plane.</param>
    /// <param name="aboveOffset">The first tile-relative above context covered by the transform.</param>
    /// <param name="leftOffset">The first superblock-row-relative left context covered by the transform.</param>
    /// <param name="plane">The zero-based color-plane index.</param>
    /// <param name="transformBlockContext">The coefficient skip and DC-sign entropy contexts.</param>
    /// <param name="transformSize">The transform size.</param>
    /// <param name="transformInfo">The transform syntax state to populate.</param>
    /// <param name="coefficientBuffer">The destination beginning at this transform's coefficient slot.</param>
    /// <returns>The decoded end-of-block coefficient position, or zero for an all-zero transform.</returns>
    /// <remarks>
    /// Implements AV1 section 5.11.39.
    /// </remarks>
    private int ParseCoefficients(
        ref Av1SymbolDecoder reader,
        ref Av1PartitionInfo partitionInfo,
        int blockRow,
        int blockColumn,
        int aboveOffset,
        int leftOffset,
        int plane,
        Av1TransformBlockContext transformBlockContext,
        Av1TransformSize transformSize,
        ref Av1TransformInfo transformInfo,
        Span<int> coefficientBuffer)
    {
        int width = transformSize.GetWidth();
        int height = transformSize.GetHeight();
        Av1TransformSize transformSizeContext = Av1SymbolContextHelper.GetTransformSizeContext(transformSize);
        Av1PlaneType planeType = (Av1PlaneType)Math.Min(plane, 1);
        Point blockPosition = new(blockColumn, blockRow);
        bool isLossless = this.FrameHeader.LosslessArray[partitionInfo.ModeInfo.SegmentId];
        bool subX = plane > 0 && this.SequenceHeader.ColorConfig.SubSamplingX;
        bool subY = plane > 0 && this.SequenceHeader.ColorConfig.SubSamplingY;
        Av1BlockSize planeBlockSize = partitionInfo.ModeInfo.BlockSize.GetSubsampled(subX, subY);
        int blocksWide = partitionInfo.GetMaxBlockWide(planeBlockSize, subX);
        int blocksHigh = partitionInfo.GetMaxBlockHigh(planeBlockSize, subY);
        bool usesInterTransformSet = partitionInfo.ModeInfo.ReferenceFrames[0] >= Av1ReferenceFrameType.Last || partitionInfo.ModeInfo.UseIntraBlockCopy;
        Av1TransformType lumaTransformType = usesInterTransformSet && plane > 0 && !isLossless
            ? partitionInfo.GetLumaTransformType(blockPosition, subX, subY)
            : Av1TransformType.DctDct;

        return reader.ReadCoefficients(
            partitionInfo.ModeInfo,
            blockPosition,
            this.aboveNeighborContext.GetContext(plane),
            this.leftNeighborContext.GetContext(plane),
            aboveOffset,
            leftOffset,
            plane,
            blocksWide,
            blocksHigh,
            transformBlockContext,
            transformSize,
            isLossless,
            this.FrameHeader.UseReducedTransformSet,
            lumaTransformType,
            ref transformInfo,
            partitionInfo.ModeBlockToRightEdge,
            partitionInfo.ModeBlockToBottomEdge,
            this.coefficientLevels,
            coefficientBuffer);
    }

    /// <summary>
    /// Derives coefficient skip and DC-sign contexts from the transform block's above and left neighbors.
    /// </summary>
    /// <param name="transformSize">The transform size.</param>
    /// <param name="plane">The zero-based color-plane index.</param>
    /// <param name="planeBlockSize">The containing block size on the target plane.</param>
    /// <param name="transformBlockUnitHighCount">The transform height clipped to the frame in 4x4 units.</param>
    /// <param name="transformBlockUnitWideCount">The transform width clipped to the frame in 4x4 units.</param>
    /// <param name="aboveOffset">The first tile-relative above context covered by the transform.</param>
    /// <param name="leftOffset">The first superblock-row-relative left context covered by the transform.</param>
    /// <returns>The derived transform-block entropy contexts.</returns>
    private Av1TransformBlockContext GetTransformBlockContext(
        Av1TransformSize transformSize,
        int plane,
        Av1BlockSize planeBlockSize,
        int transformBlockUnitHighCount,
        int transformBlockUnitWideCount,
        int aboveOffset,
        int leftOffset)
    {
        Av1TransformBlockContext transformBlockContext = default;
        ReadOnlySpan<int> aboveContext = this.aboveNeighborContext.GetContext(plane)[aboveOffset..];
        ReadOnlySpan<int> leftContext = this.leftNeighborContext.GetContext(plane)[leftOffset..];
        int dcSign = 0;
        int k = 0;
        int mask = (1 << Av1Constants.CoefficientContextBitCount) - 1;

        // The high bits of each neighbor value encode its DC sign class. Summing both edges maps
        // negative, balanced, and positive neighborhoods to the AV1 DC-sign context.
        do
        {
            uint sign = (uint)aboveContext[k] >> Av1Constants.CoefficientContextBitCount;
            DebugGuard.MustBeLessThanOrEqualTo(sign, 2U, nameof(sign));
            dcSign += Signs[(int)sign];
        }
        while (++k < transformBlockUnitWideCount);

        k = 0;
        do
        {
            uint sign = (uint)leftContext[k] >> Av1Constants.CoefficientContextBitCount;
            DebugGuard.MustBeLessThanOrEqualTo(sign, 2U, nameof(sign));
            dcSign += Signs[(int)sign];
        }
        while (++k < transformBlockUnitHighCount);

        transformBlockContext.DcSignContext = DcSignContexts[dcSign + (Av1Constants.MaxTransformSizeUnit << 1)];

        if (plane == 0)
        {
            if (planeBlockSize == transformSize.ToBlockSize())
            {
                transformBlockContext.SkipContext = 0;
            }
            else
            {
                // Luma skip contexts preserve both the weakest neighboring level and whether either edge is stronger.
                int top = 0;
                int left = 0;

                k = 0;
                do
                {
                    top |= aboveContext[k];
                }
                while (++k < transformBlockUnitWideCount);
                top &= mask;

                k = 0;
                do
                {
                    left |= leftContext[k];
                }
                while (++k < transformBlockUnitHighCount);
                left &= mask;

                transformBlockContext.SkipContext = Av1SymbolContextHelper.GetTransformBlockSkipContext(top, left);
            }
        }
        else
        {
            // Chroma needs only the presence of nonzero levels on each edge, plus an offset that
            // distinguishes a transform smaller than its containing plane block.
            int contextBase = GetEntropyContext(transformSize, aboveContext, leftContext);
            int contextOffset = planeBlockSize.GetPelsLog2Count() > transformSize.ToBlockSize().GetPelsLog2Count() ? 10 : 7;
            transformBlockContext.SkipContext = contextBase + contextOffset;
        }

        return transformBlockContext;
    }

    /// <summary>
    /// Determines whether the above and left edges contain nonzero chroma coefficient contexts.
    /// </summary>
    /// <param name="transformSize">The transform size that selects how many edge entries to inspect.</param>
    /// <param name="above">The above coefficient contexts.</param>
    /// <param name="left">The left coefficient contexts.</param>
    /// <returns>The sum of the nonzero-above and nonzero-left flags.</returns>
    private static int GetEntropyContext(Av1TransformSize transformSize, ReadOnlySpan<int> above, ReadOnlySpan<int> left)
    {
        bool aboveEntropyContext = false;
        bool leftEntropyContext = false;
        int transformBlockUnitWideCount = transformSize.Get4x4WideCount();
        int transformBlockUnitHighCount = transformSize.Get4x4HighCount();

        // the reference decoder tests the context bytes through packed native loads. Enumerating the same transform-width and
        // transform-height entries avoids unaligned reads while preserving the required any-nonzero result.
        for (int i = 0; i < transformBlockUnitWideCount; i++)
        {
            if (above[i] != 0)
            {
                aboveEntropyContext = true;
                break;
            }
        }

        for (int i = 0; i < transformBlockUnitHighCount; i++)
        {
            if (left[i] != 0)
            {
                leftEntropyContext = true;
                break;
            }
        }

        return (aboveEntropyContext ? 1 : 0) + (leftEntropyContext ? 1 : 0);
    }

    /// <summary>
    /// Selects the transform size for a coding block from lossless, explicit-selection, or maximum-size rules.
    /// </summary>
    /// <param name="reader">The tile symbol decoder.</param>
    /// <param name="partitionInfo">The current coding block.</param>
    /// <param name="superblockInfo">The containing superblock.</param>
    /// <param name="tileInfo">The active tile boundaries.</param>
    /// <param name="allowSelect">A value indicating whether transform-size selection syntax is allowed at this node.</param>
    /// <returns>The selected transform size.</returns>
    /// <remarks>Implements AV1 section 5.11.15.</remarks>
    private Av1TransformSize ReadTransformSize(
        ref Av1SymbolDecoder reader,
        ref Av1PartitionInfo partitionInfo,
        Av1SuperblockInfo superblockInfo,
        Av1TileInfo tileInfo,
        bool allowSelect)
    {
        ref Av1BlockModeInfo modeInfo = ref partitionInfo.ModeInfo;
        if (this.FrameHeader.LosslessArray[modeInfo.SegmentId])
        {
            return Av1TransformSize.Size4x4;
        }

        if (modeInfo.BlockSize > Av1BlockSize.Block4x4 && allowSelect && this.FrameHeader.TransformMode == Av1TransformMode.Select)
        {
            return this.ReadSelectedTransformSize(ref reader, ref partitionInfo, superblockInfo, tileInfo);
        }

        return modeInfo.BlockSize.GetMaximumTransformSize();
    }

    /// <summary>
    /// Reads a transform size using the available above and left transform-size contexts.
    /// </summary>
    /// <param name="reader">The tile symbol decoder.</param>
    /// <param name="partitionInfo">The current coding block.</param>
    /// <param name="superblockInfo">The containing superblock.</param>
    /// <param name="tileInfo">The active tile boundaries.</param>
    /// <returns>The decoded transform size.</returns>
    private Av1TransformSize ReadSelectedTransformSize(
        ref Av1SymbolDecoder reader,
        ref Av1PartitionInfo partitionInfo,
        Av1SuperblockInfo superblockInfo,
        Av1TileInfo tileInfo)
    {
        int context = 0;
        Av1TransformSize maxTransformSize = partitionInfo.ModeInfo.BlockSize.GetMaximumTransformSize();
        int aboveWidth = this.aboveNeighborContext.AboveTransformWidth[partitionInfo.ColumnIndex - tileInfo.ModeInfoColumnStart];
        int above = (aboveWidth >= maxTransformSize.GetWidth()) ? 1 : 0;
        int leftHeight = this.leftNeighborContext.LeftTransformHeight[partitionInfo.RowIndex - superblockInfo.ModeInfoPosition.Y];
        int left = (leftHeight >= maxTransformSize.GetHeight()) ? 1 : 0;
        bool hasAbove = partitionInfo.AvailableAbove;
        bool hasLeft = partitionInfo.AvailableLeft;

        // Inter neighbors expose their coding-block extent here rather than their residual transform extent. This
        // keeps intra transform-size selection independent of whether the neighboring inter block split its tree.
        if (hasAbove)
        {
            Av1BlockModeInfo aboveModeInfo = superblockInfo.GetModeInfoAt(
                new Point(partitionInfo.ColumnIndex, partitionInfo.RowIndex - 1));

            if (aboveModeInfo.UseIntraBlockCopy || aboveModeInfo.ReferenceFrames[0] > Av1ReferenceFrameType.Intra)
            {
                above = aboveModeInfo.BlockSize.GetWidth() >= maxTransformSize.GetWidth() ? 1 : 0;
            }
        }

        if (hasLeft)
        {
            Av1BlockModeInfo leftModeInfo = superblockInfo.GetModeInfoAt(
                new Point(partitionInfo.ColumnIndex - 1, partitionInfo.RowIndex));

            if (leftModeInfo.UseIntraBlockCopy || leftModeInfo.ReferenceFrames[0] > Av1ReferenceFrameType.Intra)
            {
                left = leftModeInfo.BlockSize.GetHeight() >= maxTransformSize.GetHeight() ? 1 : 0;
            }
        }

        if (hasAbove && hasLeft)
        {
            context = above + left;
        }
        else if (hasAbove)
        {
            context = above;
        }
        else if (hasLeft)
        {
            context = left;
        }
        else
        {
            context = 0;
        }

        return reader.ReadTransformSize(partitionInfo.ModeInfo.BlockSize, context);
    }

    /// <summary>
    /// Reads a coding block's transform size, updates neighbor contexts, and creates its transform geometry records.
    /// </summary>
    /// <param name="reader">The tile symbol decoder.</param>
    /// <param name="modeInfoLocation">The block origin in 4x4 mode-information units.</param>
    /// <param name="partitionInfo">The current coding block.</param>
    /// <param name="superblockInfo">The containing superblock.</param>
    /// <param name="tileInfo">The active tile boundaries.</param>
    private void ReadBlockTransformSize(
        ref Av1SymbolDecoder reader,
        Point modeInfoLocation,
        ref Av1PartitionInfo partitionInfo,
        Av1SuperblockInfo superblockInfo,
        Av1TileInfo tileInfo)
    {
        Av1BlockSize blockSize = partitionInfo.ModeInfo.BlockSize;
        int block4x4Width = blockSize.Get4x4WideCount();
        int block4x4Height = blockSize.Get4x4HighCount();

        ref Av1BlockModeInfo modeInfo = ref partitionInfo.ModeInfo;
        bool usesInterTransformSyntax = modeInfo.ReferenceFrames[0] >= Av1ReferenceFrameType.Last || modeInfo.UseIntraBlockCopy;

        this.transformUnitCount[(int)Av1Plane.Y][..4].Clear();
        this.transformUnitCount[(int)Av1Plane.U][..4].Clear();
        this.transformUnitCount[(int)Av1Plane.V][..4].Clear();

        if (usesInterTransformSyntax &&
            !modeInfo.Skip &&
            this.FrameHeader.TransformMode == Av1TransformMode.Select &&
            blockSize > Av1BlockSize.Block4x4)
        {
            this.ReadVariableTransformInfo(
                ref reader,
                modeInfoLocation,
                ref partitionInfo,
                superblockInfo,
                tileInfo);

            return;
        }

        // A skipped inter block derives its maximum transform size without a symbol. Intra blocks still select a
        // transform size when the frame enables selection because skip_txfm does not suppress their size syntax.
        bool allowSelect = !usesInterTransformSyntax || !modeInfo.Skip;
        Av1TransformSize transformSize = this.ReadTransformSize(
            ref reader,
            ref partitionInfo,
            superblockInfo,
            tileInfo,
            allowSelect);

        bool skippedInterBlock = usesInterTransformSyntax && modeInfo.Skip;
        this.aboveNeighborContext.UpdateTransformation(modeInfoLocation, tileInfo, transformSize, blockSize, skippedInterBlock);
        this.leftNeighborContext.UpdateTransformation(modeInfoLocation, superblockInfo, transformSize, blockSize, skippedInterBlock);
        this.UpdateTransformInfo(ref partitionInfo, superblockInfo, blockSize, transformSize);
    }

    /// <summary>
    /// Reads the recursive luma transform partition used by a non-skipped inter block.
    /// </summary>
    /// <param name="reader">The tile symbol decoder.</param>
    /// <param name="modeInfoLocation">The coding-block origin in frame mode-information units.</param>
    /// <param name="partitionInfo">The current coding block.</param>
    /// <param name="superblockInfo">The containing superblock.</param>
    /// <param name="tileInfo">The active tile boundaries.</param>
    private void ReadVariableTransformInfo(
        ref Av1SymbolDecoder reader,
        Point modeInfoLocation,
        ref Av1PartitionInfo partitionInfo,
        Av1SuperblockInfo superblockInfo,
        Av1TileInfo tileInfo)
    {
        Av1BlockSize blockSize = partitionInfo.ModeInfo.BlockSize;
        Av1TransformSize maximumTransformSize = blockSize.GetMaximumTransformSize();
        int maximumBlocksWide = partitionInfo.GetMaxBlockWide(blockSize, false);
        int maximumBlocksHigh = partitionInfo.GetMaxBlockHigh(blockSize, false);
        int regionWidth = maximumTransformSize.Get4x4WideCount();
        int regionHeight = maximumTransformSize.Get4x4HighCount();
        int transformInfoIndex = partitionInfo.ModeInfo.GetFirstTransformLocation(Av1PlaneType.Y);
        int totalTransformUnitCount = 0;
        int regionIndex = 0;

        // Large blocks are visited as independent maximum-transform regions. Keeping the same region order as residual
        // parsing lets each region retain an exact transform count without a second map or temporary allocation.
        for (int blockRow = 0; blockRow < maximumBlocksHigh; blockRow += regionHeight)
        {
            for (int blockColumn = 0; blockColumn < maximumBlocksWide; blockColumn += regionWidth)
            {
                int firstRegionTransform = totalTransformUnitCount;
                this.ReadVariableTransformNode(
                    ref reader,
                    modeInfoLocation,
                    ref partitionInfo,
                    superblockInfo,
                    tileInfo,
                    maximumTransformSize,
                    depth: 0,
                    blockRow,
                    blockColumn,
                    ref transformInfoIndex,
                    ref totalTransformUnitCount);

                this.transformUnitCount[(int)Av1Plane.Y][regionIndex] = totalTransformUnitCount - firstRegionTransform;
                regionIndex++;
            }
        }

        this.UpdateTransformInfo(
            ref partitionInfo,
            superblockInfo,
            blockSize,
            maximumTransformSize,
            preserveLuma: true,
            existingLumaTransformUnitCount: totalTransformUnitCount);
    }

    /// <summary>
    /// Reads one node of the inter variable-transform tree and appends its leaf transform descriptors.
    /// </summary>
    private void ReadVariableTransformNode(
        ref Av1SymbolDecoder reader,
        Point modeInfoLocation,
        ref Av1PartitionInfo partitionInfo,
        Av1SuperblockInfo superblockInfo,
        Av1TileInfo tileInfo,
        Av1TransformSize transformSize,
        int depth,
        int blockRow,
        int blockColumn,
        ref int transformInfoIndex,
        ref int transformUnitCount)
    {
        Av1BlockSize blockSize = partitionInfo.ModeInfo.BlockSize;
        int maximumBlocksWide = partitionInfo.GetMaxBlockWide(blockSize, false);
        int maximumBlocksHigh = partitionInfo.GetMaxBlockHigh(blockSize, false);
        if (blockRow >= maximumBlocksHigh || blockColumn >= maximumBlocksWide)
        {
            return;
        }

        bool split = false;
        if (transformSize > Av1TransformSize.Size4x4 && depth < Av1Constants.MaxVarTransform)
        {
            int aboveOffset = modeInfoLocation.X - tileInfo.ModeInfoColumnStart + blockColumn;
            int leftOffset = modeInfoLocation.Y - superblockInfo.ModeInfoPosition.Y + blockRow;
            int transformWidth = transformSize.GetWidth();
            int transformHeight = transformSize.GetHeight();
            int above = this.aboveNeighborContext.AboveTransformWidth[aboveOffset] < transformWidth ? 1 : 0;
            int left = this.leftNeighborContext.LeftTransformHeight[leftOffset] < transformHeight ? 1 : 0;
            int maximumDimension = Math.Max(blockSize.GetWidth(), blockSize.GetHeight());
            Av1TransformSize maximumSquareTransform = maximumDimension switch
            {
                >= 64 => Av1TransformSize.Size64x64,
                >= 32 => Av1TransformSize.Size32x32,
                >= 16 => Av1TransformSize.Size16x16,
                _ => Av1TransformSize.Size8x8
            };

            int category = ((transformSize.GetSquareUpSize() != maximumSquareTransform && maximumSquareTransform > Av1TransformSize.Size8x8) ? 1 : 0) +
                ((((int)Av1TransformSize.SquareSizes - 1) - (int)maximumSquareTransform) * 2);

            int context = (category * 3) + above + left;
            split = reader.ReadTransformPartition(context);
        }

        if (split)
        {
            Av1TransformSize subTransformSize = transformSize.GetSubSize();
            int subWidth = subTransformSize.Get4x4WideCount();
            int subHeight = subTransformSize.Get4x4HighCount();
            int width = transformSize.Get4x4WideCount();
            int height = transformSize.Get4x4HighCount();
            for (int row = 0; row < height; row += subHeight)
            {
                for (int column = 0; column < width; column += subWidth)
                {
                    this.ReadVariableTransformNode(
                        ref reader,
                        modeInfoLocation,
                        ref partitionInfo,
                        superblockInfo,
                        tileInfo,
                        subTransformSize,
                        depth + 1,
                        blockRow + row,
                        blockColumn + column,
                        ref transformInfoIndex,
                        ref transformUnitCount);
                }
            }

            return;
        }

        Span<Av1TransformInfo> transformInfo = superblockInfo.GetTransformInfoY();
        transformInfo[transformInfoIndex] = new Av1TransformInfo(transformSize, blockColumn, blockRow);
        transformInfoIndex++;
        transformUnitCount++;

        Point transformLocation = new(modeInfoLocation.X + blockColumn, modeInfoLocation.Y + blockRow);
        Av1BlockSize transformBlockSize = transformSize.ToBlockSize();
        this.aboveNeighborContext.UpdateTransformation(transformLocation, tileInfo, transformSize, transformBlockSize, false);
        this.leftNeighborContext.UpdateTransformation(transformLocation, superblockInfo, transformSize, transformBlockSize, false);
    }

    /// <summary>
    /// Populates luma and chroma transform-information records in residual traversal order.
    /// </summary>
    /// <param name="partitionInfo">The current coding block.</param>
    /// <param name="superblockInfo">The containing superblock and transform storage.</param>
    /// <param name="blockSize">The coding block size.</param>
    /// <param name="transformSize">The selected luma transform size.</param>
    /// <param name="preserveLuma">Indicates whether variable-transform traversal already populated luma descriptors.</param>
    /// <param name="existingLumaTransformUnitCount">The number of luma descriptors already populated.</param>
    private unsafe void UpdateTransformInfo(
        ref Av1PartitionInfo partitionInfo,
        Av1SuperblockInfo superblockInfo,
        Av1BlockSize blockSize,
        Av1TransformSize transformSize,
        bool preserveLuma = false,
        int existingLumaTransformUnitCount = 0)
    {
        int transformInfoYIndex = partitionInfo.ModeInfo.GetFirstTransformLocation(Av1PlaneType.Y);
        int transformInfoUvIndex = partitionInfo.ModeInfo.GetFirstTransformLocation(Av1PlaneType.Uv);
        Span<Av1TransformInfo> lumaTransformInfo = superblockInfo.GetTransformInfoY();
        Span<Av1TransformInfo> chromaTransformInfo = superblockInfo.GetTransformInfoUv();
        int totalLumaTransformUnitCount = existingLumaTransformUnitCount;
        int totalChromaTransformUnitCount = 0;
        int forceSplitCount = 0;
        bool subX = this.SequenceHeader.ColorConfig.SubSamplingX;
        bool subY = this.SequenceHeader.ColorConfig.SubSamplingY;
        int maxBlockWide = partitionInfo.GetMaxBlockWide(blockSize, false);
        int maxBlockHigh = partitionInfo.GetMaxBlockHigh(blockSize, false);
        int width = 64 >> 2;
        int height = 64 >> 2;
        width = Math.Min(width, maxBlockWide);
        height = Math.Min(height, maxBlockHigh);

        bool isLossLess = this.FrameHeader.LosslessArray[partitionInfo.ModeInfo.SegmentId];
        Av1TransformSize transformSizeUv = isLossLess ? Av1TransformSize.Size4x4 : blockSize.GetMaxUvTransformSize(subX, subY);

        // Residual syntax visits at most 64x64 luma regions. Record transform geometry in the same
        // nested region/row/column order so coefficient parsing and reconstruction consume matching spans.
        for (int idy = 0; idy < maxBlockHigh; idy += height)
        {
            for (int idx = 0; idx < maxBlockWide; idx += width, forceSplitCount++)
            {
                int lumaTransformUnitCount = 0;
                int chromaTransformUnitCount = 0;

                int unitHeight = Av1Math.RoundPowerOf2(Math.Min(height + idy, maxBlockHigh), 0);
                int unitWidth = Av1Math.RoundPowerOf2(Math.Min(width + idx, maxBlockWide), 0);
                if (!preserveLuma)
                {
                    // Luma transform offsets remain relative to the coding block in 4x4 luma units.
                    int lumaStepColumn = transformSize.Get4x4WideCount();
                    int lumaStepRow = transformSize.Get4x4HighCount();
                    for (int blockRow = idy; blockRow < unitHeight; blockRow += lumaStepRow)
                    {
                        for (int blockColumn = idx; blockColumn < unitWidth; blockColumn += lumaStepColumn)
                        {
                            lumaTransformInfo[transformInfoYIndex] = new Av1TransformInfo(
                                transformSize, blockColumn, blockRow);
                            transformInfoYIndex++;
                            lumaTransformUnitCount++;
                            totalLumaTransformUnitCount++;
                        }
                    }

                    this.transformUnitCount[(int)Av1Plane.Y][forceSplitCount] = lumaTransformUnitCount;
                }

                if (this.SequenceHeader.ColorConfig.IsMonochrome || !partitionInfo.IsChroma)
                {
                    continue;
                }

                // Chroma geometry is rounded to the subsampling grid before stepping its transform size.
                int stepColumn = transformSizeUv.Get4x4WideCount();
                int stepRow = transformSizeUv.Get4x4HighCount();

                unitHeight = Av1Math.RoundPowerOf2(Math.Min(height + idy, maxBlockHigh), subY ? 1 : 0);
                unitWidth = Av1Math.RoundPowerOf2(Math.Min(width + idx, maxBlockWide), subX ? 1 : 0);
                int planeRow = idy >> (subY ? 1 : 0);
                int planeColumn = idx >> (subX ? 1 : 0);

                // The 64x64 region cursor is expressed on the luma grid. Chroma transform offsets use the
                // target plane's 4x4 grid, matching the reference decoder's row/column subsampling before transform traversal.
                for (int blockRow = planeRow; blockRow < unitHeight; blockRow += stepRow)
                {
                    for (int blockColumn = planeColumn; blockColumn < unitWidth; blockColumn += stepColumn)
                    {
                        chromaTransformInfo[transformInfoUvIndex] = new Av1TransformInfo(
                            transformSizeUv, blockColumn, blockRow);
                        transformInfoUvIndex++;
                        chromaTransformUnitCount++;
                        totalChromaTransformUnitCount++;
                    }
                }

                this.transformUnitCount[(int)Av1Plane.U][forceSplitCount] = chromaTransformUnitCount;
                this.transformUnitCount[(int)Av1Plane.V][forceSplitCount] = chromaTransformUnitCount;
            }
        }

        // U and V share transform geometry, so append a second copy for V after the complete U sequence.
        if (totalChromaTransformUnitCount != 0)
        {
            DebugGuard.IsTrue(
                (transformInfoUvIndex - totalChromaTransformUnitCount) ==
                partitionInfo.ModeInfo.GetFirstTransformLocation(Av1PlaneType.Uv),
                nameof(totalChromaTransformUnitCount));
            int originalIndex = transformInfoUvIndex - totalChromaTransformUnitCount;
            ref Av1TransformInfo originalInfo = ref chromaTransformInfo[originalIndex];
            ref Av1TransformInfo infoV = ref chromaTransformInfo[transformInfoUvIndex];
            for (int i = 0; i < totalChromaTransformUnitCount; i++)
            {
                // U and V share transform geometry, but their entropy state and coefficients remain independent.
                infoV = new Av1TransformInfo(originalInfo);
                originalInfo = ref Unsafe.Add(ref originalInfo, 1);
                infoV = ref Unsafe.Add(ref infoV, 1);
            }
        }

        partitionInfo.ModeInfo.SetTransformUnitCount(Av1PlaneType.Y, totalLumaTransformUnitCount);
        partitionInfo.ModeInfo.SetTransformUnitCount(Av1PlaneType.Uv, totalChromaTransformUnitCount);

        this.firstTransformOffset[(int)Av1PlaneType.Y] += totalLumaTransformUnitCount;
        this.firstTransformOffset[(int)Av1PlaneType.Uv] += totalChromaTransformUnitCount << 1;
    }

    /// <summary>
    /// Reads luma and chroma palette-map tokens when a block selects palette prediction.
    /// </summary>
    /// <param name="reader">The tile symbol decoder.</param>
    /// <param name="partitionInfo">The current coding block.</param>
    /// <remarks>Implements AV1 section 5.11.49.</remarks>
    private void ReadPaletteTokens(ref Av1SymbolDecoder reader, ref Av1PartitionInfo partitionInfo)
    {
        ref Av1BlockModeInfo modeInfo = ref partitionInfo.ModeInfo;
        if (modeInfo.GetPaletteSize(Av1PlaneType.Y) != 0)
        {
            GetPaletteMapDimensions(
                ref partitionInfo,
                Av1PlaneType.Y,
                this.SequenceHeader.ColorConfig,
                out int planeWidth,
                out int planeHeight,
                out int rows,
                out int columns);

            Point position = modeInfo.PositionInSuperblock;
            Rectangle bounds = new(position.X << Av1Constants.ModeInfoSizeLog2, position.Y << Av1Constants.ModeInfoSizeLog2, planeWidth, planeHeight);
            Buffer2DRegion<byte> colorIndexMap = new(this.lumaPaletteColorIndexMap, bounds);

            DecodePaletteColorMap(
                ref reader,
                modeInfo.GetPaletteSize(Av1PlaneType.Y),
                Av1PlaneType.Y,
                planeWidth,
                planeHeight,
                rows,
                columns,
                colorIndexMap);

            if (this.FrameDecoder is not null)
            {
                modeInfo.SetPaletteColorIndexMap(Av1PlaneType.Y, colorIndexMap.Bounds);
            }
        }

        if (modeInfo.GetPaletteSize(Av1PlaneType.Uv) != 0)
        {
            GetPaletteMapDimensions(
                ref partitionInfo,
                Av1PlaneType.Uv,
                this.SequenceHeader.ColorConfig,
                out int planeWidth,
                out int planeHeight,
                out int rows,
                out int columns);

            int subX = this.SequenceHeader.ColorConfig.SubSamplingX ? 1 : 0;
            int subY = this.SequenceHeader.ColorConfig.SubSamplingY ? 1 : 0;
            Point position = modeInfo.PositionInSuperblock;
            Rectangle bounds = new((position.X << Av1Constants.ModeInfoSizeLog2) >> subX, (position.Y << Av1Constants.ModeInfoSizeLog2) >> subY, planeWidth, planeHeight);
            Buffer2DRegion<byte> colorIndexMap = new(this.chromaPaletteColorIndexMap, bounds);

            DecodePaletteColorMap(
                ref reader,
                modeInfo.GetPaletteSize(Av1PlaneType.Uv),
                Av1PlaneType.Uv,
                planeWidth,
                planeHeight,
                rows,
                columns,
                colorIndexMap);

            if (this.FrameDecoder is not null)
            {
                modeInfo.SetPaletteColorIndexMap(Av1PlaneType.Uv, colorIndexMap.Bounds);
            }
        }
    }

    /// <summary>
    /// Reads the prediction, segmentation, skip, quantizer, and filter mode information for a coding block.
    /// </summary>
    /// <param name="reader">The tile symbol decoder.</param>
    /// <param name="partitionInfo">The current coding block.</param>
    /// <param name="tileInfo">The active tile boundaries.</param>
    /// <remarks>Implements the frame-type dispatch in AV1 section 5.11.6.</remarks>
    private void ReadModeInfo(ref Av1SymbolDecoder reader, ref Av1PartitionInfo partitionInfo, Av1TileInfo tileInfo)
    {
        if (this.FrameHeader.IsIntra)
        {
            this.ReadIntraFrameModeInfo(ref reader, ref partitionInfo, tileInfo);
        }
        else
        {
            this.ReadInterFrameModeInfo(ref reader, ref partitionInfo, tileInfo);
        }
    }

    /// <summary>
    /// Reads the common inter-frame block prefix and the supported intra or inter prediction branch in bitstream order.
    /// </summary>
    /// <param name="reader">The tile symbol decoder.</param>
    /// <param name="partitionInfo">The current coding block and its neighbors.</param>
    /// <param name="tileInfo">The active tile boundaries used by reference-motion-vector searches.</param>
    /// <remarks>Implements the prefix, intra, and translational inter branches of AV1 section 5.11.7.</remarks>
    public void ReadInterFrameModeInfo(ref Av1SymbolDecoder reader, ref Av1PartitionInfo partitionInfo, Av1TileInfo tileInfo)
    {
        ref Av1BlockModeInfo modeInfo = ref partitionInfo.ModeInfo;
        modeInfo.MotionVectors.Clear();
        this.ReadInterSegmentId(ref reader, ref partitionInfo, beforeSkip: true);

        modeInfo.SkipMode = this.ReadSkipMode(ref reader, ref partitionInfo);
        modeInfo.Skip = modeInfo.SkipMode || this.ReadSkip(ref reader, ref partitionInfo);

        if (!this.FrameHeader.SegmentationParameters.SegmentIdPrecedesSkip)
        {
            this.ReadInterSegmentId(ref reader, ref partitionInfo, beforeSkip: false);
        }

        this.ReadCdef(ref reader, ref partitionInfo);

        if (this.FrameHeader.DeltaQParameters.IsPresent)
        {
            this.ReadDeltaQuantizerIndex(ref reader, ref partitionInfo);
            this.ReadDeltaLoopFilter(ref reader, ref partitionInfo);
        }

        bool isInterBlock = modeInfo.SkipMode || this.ReadIsInter(ref reader, ref partitionInfo);
        if (isInterBlock)
        {
            modeInfo.SetPaletteSizes(0, 0);
            modeInfo.UvMode = Av1ChromaPredictionMode.DC;
            this.ReadReferenceFrames(ref reader, ref partitionInfo);

            Av1ReferenceFrameType referenceFrame = modeInfo.ReferenceFrames[0];
            Av1ReferenceFrameType secondaryReferenceFrame = modeInfo.ReferenceFrames[1];
            bool isCompound = secondaryReferenceFrame > Av1ReferenceFrameType.Intra;

            Av1ReferenceMotionVectors referenceMotionVectors = this.referenceMotionVectors;
            referenceMotionVectors.Build(
                ref partitionInfo,
                tileInfo,
                this.FrameInfo,
                this.SequenceHeader,
                this.FrameHeader,
                referenceFrame,
                secondaryReferenceFrame);

            ObuSegmentationParameters segmentationParameters = this.FrameHeader.SegmentationParameters;
            int segmentId = modeInfo.SegmentId;
            bool usesForcedGlobalMotion =
                segmentationParameters.IsFeatureActive(segmentId, ObuSegmentationLevelFeature.Skip) ||
                segmentationParameters.IsFeatureActive(segmentId, ObuSegmentationLevelFeature.GlobalMotionVector);

            modeInfo.ReferenceMotionVectorIndex = 0;
            modeInfo.YMode = modeInfo.SkipMode
                ? Av1PredictionMode.NearestNearestMotionVector
                : usesForcedGlobalMotion
                    ? Av1PredictionMode.GlobalMotionVector
                    : isCompound
                        ? reader.ReadInterCompoundMode(referenceMotionVectors.ModeContext)
                        : reader.ReadInterMode(referenceMotionVectors.ModeContext);

            bool modeIsCompound = modeInfo.YMode is >= Av1PredictionMode.CompoundInterModeStart and < Av1PredictionMode.CompoundInterModeEnd;
            if (isCompound != modeIsCompound)
            {
                throw new InvalidImageContentException("AV1 inter prediction mode does not match its reference-frame count.");
            }

            if (modeInfo.YMode is Av1PredictionMode.NewMotionVector or Av1PredictionMode.NewNewMotionVector)
            {
                // NEWMV and NEW_NEWMV can advance across candidates zero through two. Each transmitted one selects
                // the next candidate and exposes one further DRL decision when the stack contains it.
                for (int index = 0; index < 2 && referenceMotionVectors.Count > index + 1; index++)
                {
                    int context = Av1SymbolContextHelper.GetDrlContext(referenceMotionVectors.Weights, index);
                    bool advance = reader.ReadDrl(context);
                    modeInfo.ReferenceMotionVectorIndex = (byte)(index + (advance ? 1 : 0));
                    if (!advance)
                    {
                        break;
                    }
                }
            }
            else if (modeInfo.YMode is
                Av1PredictionMode.NearMotionVector or
                Av1PredictionMode.NearNearMotionVector or
                Av1PredictionMode.NearNewMotionVector or
                Av1PredictionMode.NewNearMotionVector)
            {
                // Modes containing NEARMV reserve candidate zero for NEARESTMV, so their two DRL decisions examine
                // pairs one/two and two/three while storing a zero-based offset from the first near candidate.
                for (int index = 1; index < 3 && referenceMotionVectors.Count > index + 1; index++)
                {
                    int context = Av1SymbolContextHelper.GetDrlContext(referenceMotionVectors.Weights, index);
                    bool advance = reader.ReadDrl(context);
                    modeInfo.ReferenceMotionVectorIndex = (byte)(index + (advance ? 1 : 0) - 1);
                    if (!advance)
                    {
                        break;
                    }
                }
            }

            Av1MotionVectorPrecision precision = this.FrameHeader.ForceIntegerMotionVector
                ? Av1MotionVectorPrecision.Integer
                : this.FrameHeader.AllowHighPrecisionMotionVector ? Av1MotionVectorPrecision.EighthSample : Av1MotionVectorPrecision.QuarterSample;

            Span<Av1MotionVector> motionVectors = modeInfo.MotionVectors;
            if (!isCompound)
            {
                motionVectors[0] = modeInfo.YMode switch
                {
                    Av1PredictionMode.NewMotionVector => reader.ReadMotionVector(
                        referenceMotionVectors.GetNewReference(modeInfo.ReferenceMotionVectorIndex),
                        precision),
                    Av1PredictionMode.NearestMotionVector => referenceMotionVectors.Nearest,
                    Av1PredictionMode.NearMotionVector => referenceMotionVectors.GetNearReference(modeInfo.ReferenceMotionVectorIndex),
                    Av1PredictionMode.GlobalMotionVector => this.FrameHeader.GetGlobalMotionParameters()[(int)referenceFrame - 1].GetMotionVector(
                        this.FrameHeader.AllowHighPrecisionMotionVector,
                        modeInfo.BlockSize,
                        new Point(partitionInfo.ColumnIndex, partitionInfo.RowIndex),
                        this.FrameHeader.ForceIntegerMotionVector),
                    _ => throw new InvalidImageContentException("Invalid single-reference AV1 inter mode.")
                };
            }
            else
            {
                int referenceMotionVectorIndex = modeInfo.ReferenceMotionVectorIndex;
                int newReferenceIndex = modeInfo.YMode is Av1PredictionMode.NearNewMotionVector or Av1PredictionMode.NewNearMotionVector
                    ? referenceMotionVectorIndex + 1
                    : referenceMotionVectorIndex;

                Av1MotionVector primaryNearest = referenceMotionVectors.GetCompoundNearestReference(0);
                Av1MotionVector secondaryNearest = referenceMotionVectors.GetCompoundNearestReference(1);
                Av1MotionVector primaryNear = referenceMotionVectors.GetCompoundNearReference(referenceMotionVectorIndex, 0);
                Av1MotionVector secondaryNear = referenceMotionVectors.GetCompoundNearReference(referenceMotionVectorIndex, 1);

                switch (modeInfo.YMode)
                {
                    case Av1PredictionMode.NearestNearestMotionVector:
                        motionVectors[0] = primaryNearest;
                        motionVectors[1] = secondaryNearest;
                        break;
                    case Av1PredictionMode.NearNearMotionVector:
                        motionVectors[0] = primaryNear;
                        motionVectors[1] = secondaryNear;
                        break;
                    case Av1PredictionMode.NearestNewMotionVector:
                        motionVectors[0] = primaryNearest;
                        motionVectors[1] = reader.ReadMotionVector(
                            referenceMotionVectors.GetCompoundNewReference(newReferenceIndex, 1),
                            precision);

                        break;
                    case Av1PredictionMode.NewNearestMotionVector:
                        motionVectors[0] = reader.ReadMotionVector(
                            referenceMotionVectors.GetCompoundNewReference(newReferenceIndex, 0),
                            precision);

                        motionVectors[1] = secondaryNearest;
                        break;
                    case Av1PredictionMode.NearNewMotionVector:
                        motionVectors[0] = primaryNear;
                        motionVectors[1] = reader.ReadMotionVector(
                            referenceMotionVectors.GetCompoundNewReference(newReferenceIndex, 1),
                            precision);

                        break;
                    case Av1PredictionMode.NewNearMotionVector:
                        motionVectors[0] = reader.ReadMotionVector(
                            referenceMotionVectors.GetCompoundNewReference(newReferenceIndex, 0),
                            precision);

                        motionVectors[1] = secondaryNear;
                        break;
                    case Av1PredictionMode.GlobalGlobalMotionVector:
                        motionVectors[0] = this.FrameHeader.GetGlobalMotionParameters()[(int)referenceFrame - 1].GetMotionVector(
                            this.FrameHeader.AllowHighPrecisionMotionVector,
                            modeInfo.BlockSize,
                            new Point(partitionInfo.ColumnIndex, partitionInfo.RowIndex),
                            this.FrameHeader.ForceIntegerMotionVector);

                        motionVectors[1] = this.FrameHeader.GetGlobalMotionParameters()[(int)secondaryReferenceFrame - 1].GetMotionVector(
                            this.FrameHeader.AllowHighPrecisionMotionVector,
                            modeInfo.BlockSize,
                            new Point(partitionInfo.ColumnIndex, partitionInfo.RowIndex),
                            this.FrameHeader.ForceIntegerMotionVector);

                        break;
                    case Av1PredictionMode.NewNewMotionVector:
                        motionVectors[0] = reader.ReadMotionVector(
                            referenceMotionVectors.GetCompoundNewReference(newReferenceIndex, 0),
                            precision);

                        motionVectors[1] = reader.ReadMotionVector(
                            referenceMotionVectors.GetCompoundNewReference(newReferenceIndex, 1),
                            precision);

                        break;
                    default:
                        throw new InvalidImageContentException("Invalid compound-reference AV1 inter mode.");
                }
            }

            for (int index = 0; index < (isCompound ? 2 : 1); index++)
            {
                if (!motionVectors[index].IsValid)
                {
                    throw new InvalidImageContentException("AV1 motion-vector component is outside the permitted range.");
                }
            }

            modeInfo.MotionMode = Av1MotionMode.SimpleTranslation;
            modeInfo.UseInterIntraWedge = false;
            modeInfo.InterIntraWedgeIndex = 0;

            int minimumBlockDimension = Math.Min(modeInfo.BlockSize.GetWidth(), modeInfo.BlockSize.GetHeight());
            if (!isCompound && !modeInfo.SkipMode &&
                this.SequenceHeader.EnableInterIntraCompound &&
                modeInfo.BlockSize is >= Av1BlockSize.Block8x8 and <= Av1BlockSize.Block32x32 &&
                reader.ReadIsInterIntra(modeInfo.BlockSize))
            {
                // The synthetic INTRA_FRAME second reference is part of the decoded mode state: it suppresses motion
                // variation syntax and lets reconstruction distinguish inter-intra from a regular single-reference block.
                modeInfo.ReferenceFrames[1] = Av1ReferenceFrameType.Intra;
                modeInfo.InterIntraMode = reader.ReadInterIntraMode(modeInfo.BlockSize);
                modeInfo.SetAngleDelta(Av1PlaneType.Y, 0);
                modeInfo.SetAngleDelta(Av1PlaneType.Uv, 0);
                modeInfo.UseFilterIntra = false;
                modeInfo.UseInterIntraWedge = reader.ReadUseInterIntraWedge(modeInfo.BlockSize);
                if (modeInfo.UseInterIntraWedge)
                {
                    modeInfo.InterIntraWedgeIndex = reader.ReadWedgeIndex(modeInfo.BlockSize);
                }
            }

            if (!isCompound &&
                modeInfo.ReferenceFrames[1] != Av1ReferenceFrameType.Intra &&
                this.FrameHeader.IsMotionModeSwitchable &&
                minimumBlockDimension >= 8 &&
                !modeInfo.SkipMode)
            {
                Av1MotionVariationCandidates candidates = this.motionVariationCandidates;
                candidates.Build(ref partitionInfo, tileInfo, this.SequenceHeader, this.FrameHeader, referenceFrame);

                Av1GlobalMotionParameters selectedGlobalMotion = this.FrameHeader.GetGlobalMotionParameters()[(int)referenceFrame - 1];
                bool hasFixedGlobalMotionMode =
                    !this.FrameHeader.ForceIntegerMotionVector &&
                    modeInfo.YMode == Av1PredictionMode.GlobalMotionVector &&
                    selectedGlobalMotion.Type > Av1GlobalMotionType.Translation;

                if (candidates.HasOverlappableNeighbor && !hasFixedGlobalMotionMode)
                {
                    bool allowWarpedMotion = false;
                    if (candidates.Count > 0 && this.FrameHeader.AllowWarpedMotion && !this.FrameHeader.ForceIntegerMotionVector)
                    {
                        int canonicalReferenceIndex = (int)referenceFrame - (int)Av1ReferenceFrameType.Last;
                        uint referenceSlot = this.FrameHeader.GetReferenceFrameIndices()[canonicalReferenceIndex];
                        Av1ReferenceFrameStore referenceFrames = this.referenceFrames
                            ?? throw new InvalidImageContentException("AV1 warped-motion syntax requires a reconstructed reference map.");

                        Av1FrameBuffer<byte> referenceFrameBuffer = referenceFrames.ResolveRequired((int)referenceSlot).FrameBuffer;

                        // Local warped motion is excluded for a scaled reference. Width and height equality are the
                        // identity-scale test because both dimensions form the decoder's reference scale factors.
                        allowWarpedMotion =
                            referenceFrameBuffer.Width == this.FrameHeader.FrameSize.FrameWidth &&
                            referenceFrameBuffer.Height == this.FrameHeader.FrameSize.FrameHeight;
                    }

                    modeInfo.MotionMode = reader.ReadMotionMode(modeInfo.BlockSize, allowWarpedMotion);
                    if (modeInfo.MotionMode == Av1MotionMode.Warped)
                    {
                        modeInfo.WarpedMotionParameters = Av1GlobalMotionParameters.DeriveLocalProjection(
                            candidates.SourcePoints,
                            candidates.ReferencePoints,
                            modeInfo.BlockSize,
                            motionVectors[0],
                            new Point(partitionInfo.ColumnIndex, partitionInfo.RowIndex));
                    }
                }
            }

            modeInfo.CompoundGroupIndex = false;
            modeInfo.CompoundIndex = true;
            modeInfo.CompoundType = Av1CompoundType.Average;
            modeInfo.CompoundWedgeIndex = 0;
            modeInfo.CompoundWedgeSign = false;
            modeInfo.DifferenceWeightedMaskType = Av1DifferenceWeightedMaskType.Type38;
            if (isCompound && !modeInfo.SkipMode)
            {
                bool maskedCompoundUsed = minimumBlockDimension >= 8 && this.SequenceHeader.EnableMaskedCompound;
                if (maskedCompoundUsed)
                {
                    int groupContext = Av1SymbolContextHelper.GetCompoundGroupIndexContext(
                        partitionInfo.AboveModeInfo,
                        partitionInfo.LeftModeInfo);

                    modeInfo.CompoundGroupIndex = reader.ReadCompoundGroupIndex(groupContext);
                }

                if (!modeInfo.CompoundGroupIndex)
                {
                    if (this.SequenceHeader.OrderHintInfo.EnableJointCompound)
                    {
                        int compoundIndexContext = Av1SymbolContextHelper.GetCompoundIndexContext(
                            this.SequenceHeader.OrderHintInfo,
                            this.FrameHeader,
                            modeInfo,
                            partitionInfo.AboveModeInfo,
                            partitionInfo.LeftModeInfo);

                        modeInfo.CompoundIndex = reader.ReadCompoundIndex(compoundIndexContext);
                        modeInfo.CompoundType = modeInfo.CompoundIndex
                            ? Av1CompoundType.Average
                            : Av1CompoundType.DistanceWeighted;
                    }
                }
                else
                {
                    bool supportsWedge = modeInfo.BlockSize is
                        Av1BlockSize.Block8x8 or
                        Av1BlockSize.Block8x16 or
                        Av1BlockSize.Block16x8 or
                        Av1BlockSize.Block16x16 or
                        Av1BlockSize.Block16x32 or
                        Av1BlockSize.Block32x16 or
                        Av1BlockSize.Block32x32 or
                        Av1BlockSize.Block8x32 or
                        Av1BlockSize.Block32x8;

                    modeInfo.CompoundType = supportsWedge
                        ? reader.ReadMaskedCompoundType(modeInfo.BlockSize)
                        : Av1CompoundType.DifferenceWeighted;

                    if (modeInfo.CompoundType == Av1CompoundType.Wedge)
                    {
                        modeInfo.CompoundWedgeIndex = reader.ReadWedgeIndex(modeInfo.BlockSize);
                        modeInfo.CompoundWedgeSign = reader.ReadLiteral(1) != 0;
                    }
                    else
                    {
                        modeInfo.DifferenceWeightedMaskType = (Av1DifferenceWeightedMaskType)reader.ReadLiteral(1);
                    }
                }
            }

            Span<Av1InterpolationFilter> interpolationFilters = modeInfo.InterpolationFilters;
            Av1InterpolationFilter frameInterpolationFilter = this.FrameHeader.InterpolationFilter;
            Av1GlobalMotionParameters globalMotion = this.FrameHeader.GetGlobalMotionParameters()[(int)referenceFrame - 1];
            bool usesNonTranslationalGlobalMotion =
                minimumBlockDimension >= 8 &&
                ((modeInfo.YMode == Av1PredictionMode.GlobalMotionVector && globalMotion.Type != Av1GlobalMotionType.Translation) ||
                 (modeInfo.YMode == Av1PredictionMode.GlobalGlobalMotionVector &&
                  globalMotion.Type != Av1GlobalMotionType.Translation &&
                  this.FrameHeader.GetGlobalMotionParameters()[(int)secondaryReferenceFrame - 1].Type != Av1GlobalMotionType.Translation));

            if (modeInfo.SkipMode || modeInfo.MotionMode == Av1MotionMode.Warped || usesNonTranslationalGlobalMotion)
            {
                // Blocks that do not use separable interpolation carry no filter symbols. A switchable frame falls
                // back to the regular family so every stored mode record contains an actual predictor selection.
                interpolationFilters.Fill(
                    frameInterpolationFilter == Av1InterpolationFilter.Switchable
                        ? Av1InterpolationFilter.Regular
                        : frameInterpolationFilter);
            }
            else if (frameInterpolationFilter != Av1InterpolationFilter.Switchable)
            {
                interpolationFilters.Fill(frameInterpolationFilter);
            }
            else
            {
                // Filter storage is vertical then horizontal. AV1 transmits in the same order and reuses the vertical
                // choice for both axes when the sequence disables independent dual-filter selection.
                int verticalContext = Av1SymbolContextHelper.GetSwitchableInterpolationContext(
                    modeInfo,
                    partitionInfo.AboveModeInfo,
                    partitionInfo.LeftModeInfo,
                    direction: 0);

                interpolationFilters[0] = reader.ReadSwitchableInterpolationFilter(verticalContext);
                if (this.SequenceHeader.EnableDualFilter)
                {
                    int horizontalContext = Av1SymbolContextHelper.GetSwitchableInterpolationContext(
                        modeInfo,
                        partitionInfo.AboveModeInfo,
                        partitionInfo.LeftModeInfo,
                        direction: 1);

                    interpolationFilters[1] = reader.ReadSwitchableInterpolationFilter(horizontalContext);
                }
                else
                {
                    interpolationFilters[1] = interpolationFilters[0];
                }
            }

            return;
        }

        modeInfo.ReferenceFrames[0] = Av1ReferenceFrameType.Intra;
        modeInfo.ReferenceFrames[1] = Av1ReferenceFrameType.None;
        modeInfo.SetPaletteSizes(0, 0);
        this.ReadConventionalIntraMode(ref reader, ref partitionInfo, reader.ReadInterFrameYMode(modeInfo.BlockSize));
    }

    /// <summary>
    /// Reads all intra-frame mode syntax for a coding block in bitstream order.
    /// </summary>
    /// <param name="reader">The tile symbol decoder.</param>
    /// <param name="partitionInfo">The current coding block and its neighbors.</param>
    /// <param name="tileInfo">The active tile boundaries.</param>
    /// <remarks>Implements AV1 section 5.11.7.</remarks>
    private void ReadIntraFrameModeInfo(ref Av1SymbolDecoder reader, ref Av1PartitionInfo partitionInfo, Av1TileInfo tileInfo)
    {
        if (this.FrameHeader.SegmentationParameters.SegmentIdPrecedesSkip)
        {
            this.IntraSegmentId(ref reader, ref partitionInfo);
        }

        partitionInfo.ModeInfo.Skip = this.ReadSkip(ref reader, ref partitionInfo);
        if (!this.FrameHeader.SegmentationParameters.SegmentIdPrecedesSkip)
        {
            this.IntraSegmentId(ref reader, ref partitionInfo);
        }

        this.ReadCdef(ref reader, ref partitionInfo);

        if (this.FrameHeader.DeltaQParameters.IsPresent)
        {
            this.ReadDeltaQuantizerIndex(ref reader, ref partitionInfo);
            this.ReadDeltaLoopFilter(ref reader, ref partitionInfo);
        }

        // Independently decodable still-image blocks reference only the current intra frame.
        partitionInfo.ReferenceFrames[0] = Av1ReferenceFrameType.Intra;
        partitionInfo.ReferenceFrames[1] = Av1ReferenceFrameType.None;
        partitionInfo.ModeInfo.SetPaletteSizes(0, 0);
        bool useIntraBlockCopy = false;
        if (this.AllowIntraBlockCopy())
        {
            useIntraBlockCopy = reader.ReadUseIntraBlockCopy();
        }

        if (useIntraBlockCopy)
        {
            partitionInfo.ModeInfo.UseIntraBlockCopy = true;
            partitionInfo.ModeInfo.YMode = Av1PredictionMode.DC;
            partitionInfo.ModeInfo.UvMode = Av1ChromaPredictionMode.DC;

            Av1MotionVector reference = Av1IntraBlockCopy.FindReference(
                ref partitionInfo,
                tileInfo,
                this.SequenceHeader.SuperblockModeInfoSize,
                this.displacementVectorCandidates,
                this.displacementVectorWeights);

            Av1MotionVector displacement = reader.ReadDisplacementVector(reference);

            if (!Av1IntraBlockCopy.IsValid(displacement, ref partitionInfo, tileInfo, this.SequenceHeader))
            {
                throw new InvalidImageContentException(
                    $"Invalid AV1 intra-block-copy displacement vector at ({partitionInfo.ColumnIndex}, {partitionInfo.RowIndex}) " +
                    $"for {partitionInfo.ModeInfo.BlockSize}: reference ({reference.Row}, {reference.Column}), " +
                    $"decoded ({displacement.Row}, {displacement.Column}).");
            }

            partitionInfo.ModeInfo.DisplacementVector = displacement;
        }
        else
        {
            Av1PredictionMode yMode = reader.ReadYMode(partitionInfo.AboveModeInfo, partitionInfo.LeftModeInfo);
            this.ReadConventionalIntraMode(ref reader, ref partitionInfo, yMode);
        }
    }

    /// <summary>
    /// Reads conventional luma and chroma intra-prediction details after the frame branch selects the luma mode CDF.
    /// </summary>
    /// <param name="reader">The tile symbol decoder.</param>
    /// <param name="partitionInfo">The current coding block.</param>
    /// <param name="yMode">The luma prediction mode selected by the frame-appropriate distribution.</param>
    private void ReadConventionalIntraMode(
        ref Av1SymbolDecoder reader,
        ref Av1PartitionInfo partitionInfo,
        Av1PredictionMode yMode)
    {
        ref Av1BlockModeInfo modeInfo = ref partitionInfo.ModeInfo;
        modeInfo.YMode = yMode;
        modeInfo.SetAngleDelta(Av1PlaneType.Y, IntraAngleInfo(ref reader, yMode, modeInfo.BlockSize));

        if (partitionInfo.IsChroma && !this.SequenceHeader.ColorConfig.IsMonochrome)
        {
            modeInfo.UvMode = reader.ReadIntraModeUv(yMode, this.IsChromaForLumaAllowed(ref partitionInfo));

            if (modeInfo.UvMode == Av1ChromaPredictionMode.ChromaFromLuma)
            {
                ReadChromaFromLumaAlphas(ref reader, ref modeInfo);
            }

            modeInfo.SetAngleDelta(
                Av1PlaneType.Uv,
                IntraAngleInfo(ref reader, modeInfo.UvMode.ToLumaMode(), modeInfo.BlockSize));
        }
        else
        {
            modeInfo.UvMode = Av1ChromaPredictionMode.DC;
        }

        if (modeInfo.BlockSize >= Av1BlockSize.Block8x8 &&
            modeInfo.BlockSize.GetWidth() <= 64 &&
            modeInfo.BlockSize.GetHeight() <= 64 &&
            this.FrameHeader.AllowScreenContentTools)
        {
            this.PaletteModeInfo(ref reader, ref partitionInfo);
        }

        this.FilterIntraModeInfo(ref reader, ref partitionInfo);
    }

    /// <summary>
    /// Determines whether the frame header permits intra block copy for an intra frame.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when the frame and sequence enable intra block copy; otherwise, <see langword="false"/>.
    /// </returns>
    private bool AllowIntraBlockCopy()
        => (this.FrameHeader.FrameType is ObuFrameType.KeyFrame or ObuFrameType.IntraOnlyFrame) &&
            (this.SequenceHeader.ForceScreenContentTools > 0) &&
            this.FrameHeader.AllowIntraBlockCopy;

    /// <summary>
    /// Determines whether chroma-from-luma prediction is available for a coding block.
    /// </summary>
    /// <param name="partitionInfo">The current coding block.</param>
    /// <returns>
    /// <see langword="true"/> when the lossless transform or block dimensions permit chroma-from-luma prediction;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    private bool IsChromaForLumaAllowed(ref Av1PartitionInfo partitionInfo)
    {
        if (this.FrameHeader.LosslessArray[partitionInfo.ModeInfo.SegmentId])
        {
            // Lossless mode fixes transforms at 4x4, so CfL is available only when the subsampled
            // plane block is itself 4x4 and therefore has no smaller transform partition.
            bool subX = this.SequenceHeader.ColorConfig.SubSamplingX;
            bool subY = this.SequenceHeader.ColorConfig.SubSamplingY;
            Av1BlockSize planeBlockSize = partitionInfo.ModeInfo.BlockSize.GetSubsampled(subX, subY);
            return planeBlockSize == Av1BlockSize.Block4x4;
        }

        // Outside lossless mode, AV1 limits CfL to luma blocks no larger than 32x32.
        return partitionInfo.ModeInfo.BlockSize.GetWidth() <= 32 && partitionInfo.ModeInfo.BlockSize.GetHeight() <= 32;
    }

    /// <summary>
    /// Reads filter-intra selection for an eligible DC-predicted luma block.
    /// </summary>
    /// <param name="reader">The tile symbol decoder.</param>
    /// <param name="partitionInfo">The current coding block.</param>
    private void FilterIntraModeInfo(ref Av1SymbolDecoder reader, ref Av1PartitionInfo partitionInfo)
    {
        partitionInfo.ModeInfo.UseFilterIntra = false;
        if (this.SequenceHeader.EnableFilterIntra &&
            partitionInfo.ModeInfo.YMode == Av1PredictionMode.DC &&
            partitionInfo.ModeInfo.GetPaletteSize(Av1PlaneType.Y) == 0 &&
            Math.Max(partitionInfo.ModeInfo.BlockSize.GetWidth(), partitionInfo.ModeInfo.BlockSize.GetHeight()) <= 32)
        {
            Av1FilterIntraMode filterIntraMode = reader.ReadFilterUltraMode(partitionInfo.ModeInfo.BlockSize);
            if (filterIntraMode != Av1FilterIntraMode.AllFilterIntraModes)
            {
                partitionInfo.ModeInfo.UseFilterIntra = true;
                partitionInfo.ModeInfo.FilterIntraMode = filterIntraMode;
            }
        }
    }

    /// <summary>
    /// Reads palette size and color syntax for an eligible screen-content block.
    /// </summary>
    /// <param name="reader">The tile symbol decoder.</param>
    /// <param name="partitionInfo">The current coding block.</param>
    /// <remarks>Implements AV1 section 5.11.46.</remarks>
    private void PaletteModeInfo(ref Av1SymbolDecoder reader, ref Av1PartitionInfo partitionInfo)
    {
        ref Av1BlockModeInfo modeInfo = ref partitionInfo.ModeInfo;
        Av1BlockSize blockSize = modeInfo.BlockSize;

        // The palette block-size context is the base-two block-area difference from an 8-by-8 block.
        int blockSizeContext = Av1Math.Log2(blockSize.GetWidth() * blockSize.GetHeight()) - 6;
        int yPaletteSize = 0;
        int uvPaletteSize = 0;
        int bitDepth = this.SequenceHeader.ColorConfig.BitDepth.GetBitCount();
        if (modeInfo.YMode == Av1PredictionMode.DC)
        {
            int neighborContext = 0;
            if (partitionInfo.AboveModeInfo is not null && partitionInfo.AboveModeInfo.Value.GetPaletteSize(Av1PlaneType.Y) != 0)
            {
                neighborContext++;
            }

            if (partitionInfo.LeftModeInfo is not null && partitionInfo.LeftModeInfo.Value.GetPaletteSize(Av1PlaneType.Y) != 0)
            {
                neighborContext++;
            }

            if (reader.ReadPaletteYMode(blockSizeContext, neighborContext))
            {
                yPaletteSize = reader.ReadPaletteSize(blockSizeContext, Av1PlaneType.Y);
                Span<ushort> yColors = stackalloc ushort[Av1Constants.PaletteMaxSize];
                ReadPaletteColorsY(ref reader, ref partitionInfo, yPaletteSize, bitDepth, yColors);
                modeInfo.SetPaletteColors(Av1Plane.Y, yColors[..yPaletteSize]);
            }
        }

        if (this.SequenceHeader.ColorConfig.PlaneCount > 1 &&
            modeInfo.UvMode == Av1ChromaPredictionMode.DC &&
            partitionInfo.IsChroma &&
            reader.ReadPaletteUvMode(yPaletteSize != 0))
        {
            uvPaletteSize = reader.ReadPaletteSize(blockSizeContext, Av1PlaneType.Uv);
            Span<ushort> uColors = stackalloc ushort[Av1Constants.PaletteMaxSize];
            Span<ushort> vColors = stackalloc ushort[Av1Constants.PaletteMaxSize];
            ReadPaletteColorsUv(ref reader, ref partitionInfo, uvPaletteSize, bitDepth, uColors, vColors);
            modeInfo.SetPaletteColors(Av1Plane.U, uColors[..uvPaletteSize]);
            modeInfo.SetPaletteColors(Av1Plane.V, vColors[..uvPaletteSize]);
        }

        modeInfo.SetPaletteSizes(yPaletteSize, uvPaletteSize);
    }

    /// <summary>
    /// Reads the sorted luma palette colors, including selections from neighboring palette caches.
    /// </summary>
    /// <param name="reader">The tile symbol decoder.</param>
    /// <param name="partitionInfo">The current coding block and its palette neighbors.</param>
    /// <param name="paletteSize">The number of luma palette colors.</param>
    /// <param name="bitDepth">The number of bits in each color sample.</param>
    /// <param name="colors">The destination palette-color buffer.</param>
    private static void ReadPaletteColorsY(
        ref Av1SymbolDecoder reader,
        ref Av1PartitionInfo partitionInfo,
        int paletteSize,
        int bitDepth,
        scoped Span<ushort> colors)
    {
        Span<ushort> colorCache = stackalloc ushort[Av1Constants.PaletteMaxSize * 2];
        Span<ushort> cachedColors = stackalloc ushort[Av1Constants.PaletteMaxSize];
        int cacheSize = GetPaletteCache(ref partitionInfo, Av1Plane.Y, colorCache);
        int colorIndex = 0;
        for (int i = 0; i < cacheSize && colorIndex < paletteSize; i++)
        {
            if (reader.ReadLiteral(1) != 0)
            {
                cachedColors[colorIndex++] = colorCache[i];
            }
        }

        if (colorIndex == paletteSize)
        {
            cachedColors[..paletteSize].CopyTo(colors);
            return;
        }

        int cachedColorCount = colorIndex;
        colors[colorIndex++] = (ushort)reader.ReadLiteral(bitDepth);
        if (colorIndex < paletteSize)
        {
            int bits = bitDepth - 3 + reader.ReadLiteral(2);
            int maximumColor = (1 << bitDepth) - 1;
            int range = maximumColor - colors[colorIndex - 1];
            for (; colorIndex < paletteSize; colorIndex++)
            {
                int delta = reader.ReadLiteral(bits) + 1;
                colors[colorIndex] = (ushort)Av1Math.Clip3(0, maximumColor, colors[colorIndex - 1] + delta);
                range -= colors[colorIndex] - colors[colorIndex - 1];
                bits = Math.Min(bits, (int)Av1Math.CeilLog2((uint)range));
            }
        }

        MergePaletteColors(colors, cachedColors, paletteSize, cachedColorCount);
    }

    /// <summary>
    /// Reads the U and V palette colors, including neighboring U colors and optional V delta coding.
    /// </summary>
    /// <param name="reader">The tile symbol decoder.</param>
    /// <param name="partitionInfo">The current coding block and its palette neighbors.</param>
    /// <param name="paletteSize">The number of chroma palette colors.</param>
    /// <param name="bitDepth">The number of bits in each color sample.</param>
    /// <param name="uColors">The destination U palette-color buffer.</param>
    /// <param name="vColors">The destination V palette-color buffer.</param>
    private static void ReadPaletteColorsUv(
        ref Av1SymbolDecoder reader,
        ref Av1PartitionInfo partitionInfo,
        int paletteSize,
        int bitDepth,
        scoped Span<ushort> uColors,
        scoped Span<ushort> vColors)
    {
        Span<ushort> colorCache = stackalloc ushort[Av1Constants.PaletteMaxSize * 2];
        Span<ushort> cachedColors = stackalloc ushort[Av1Constants.PaletteMaxSize];
        int cacheSize = GetPaletteCache(ref partitionInfo, Av1Plane.U, colorCache);
        int colorIndex = 0;
        for (int i = 0; i < cacheSize && colorIndex < paletteSize; i++)
        {
            if (reader.ReadLiteral(1) != 0)
            {
                cachedColors[colorIndex++] = colorCache[i];
            }
        }

        if (colorIndex < paletteSize)
        {
            int cachedColorCount = colorIndex;
            uColors[colorIndex++] = (ushort)reader.ReadLiteral(bitDepth);
            if (colorIndex < paletteSize)
            {
                int bits = bitDepth - 3 + reader.ReadLiteral(2);
                int maximumColor = (1 << bitDepth) - 1;
                int range = (1 << bitDepth) - uColors[colorIndex - 1];
                for (; colorIndex < paletteSize; colorIndex++)
                {
                    int delta = reader.ReadLiteral(bits);
                    uColors[colorIndex] = (ushort)Av1Math.Clip3(0, maximumColor, uColors[colorIndex - 1] + delta);
                    range -= uColors[colorIndex] - uColors[colorIndex - 1];
                    bits = Math.Min(bits, (int)Av1Math.CeilLog2((uint)range));
                }
            }

            MergePaletteColors(uColors, cachedColors, paletteSize, cachedColorCount);
        }
        else
        {
            cachedColors[..paletteSize].CopyTo(uColors);
        }

        if (reader.ReadLiteral(1) != 0)
        {
            // V deltas wrap in the unsigned sample domain so complementary chroma colors remain compact.
            int bits = bitDepth - 4 + reader.ReadLiteral(2);
            int maximumColorPlusOne = 1 << bitDepth;
            vColors[0] = (ushort)reader.ReadLiteral(bitDepth);
            for (int i = 1; i < paletteSize; i++)
            {
                int delta = reader.ReadLiteral(bits);
                if (delta != 0 && reader.ReadLiteral(1) != 0)
                {
                    delta = -delta;
                }

                int value = vColors[i - 1] + delta;
                if (value < 0)
                {
                    value += maximumColorPlusOne;
                }

                if (value >= maximumColorPlusOne)
                {
                    value -= maximumColorPlusOne;
                }

                vColors[i] = (ushort)value;
            }
        }
        else
        {
            for (int i = 0; i < paletteSize; i++)
            {
                vColors[i] = (ushort)reader.ReadLiteral(bitDepth);
            }
        }
    }

    /// <summary>
    /// Builds the sorted unique palette cache from the available above and left block palettes.
    /// </summary>
    /// <param name="partitionInfo">The current coding block and its decoded neighbors.</param>
    /// <param name="plane">The luma or U plane whose sorted base colors form the cache.</param>
    /// <param name="cache">The destination cache, which can hold both neighboring palettes.</param>
    /// <returns>The number of colors written to <paramref name="cache"/>.</returns>
    private static int GetPaletteCache(ref Av1PartitionInfo partitionInfo, Av1Plane plane, scoped Span<ushort> cache)
    {
        // AV1 deliberately excludes the block above at a 64-by-64 superblock-row boundary.
        int minimumSuperblockHeight = Av1BlockSize.Block64x64.Get4x4HighCount();
        Av1BlockModeInfo? aboveModeInfo = partitionInfo.RowIndex % minimumSuperblockHeight == 0
            ? null
            : partitionInfo.AboveModeInfo;
        Av1BlockModeInfo? leftModeInfo = partitionInfo.LeftModeInfo;

        Av1BlockModeInfo above = aboveModeInfo.GetValueOrDefault();
        Av1BlockModeInfo left = leftModeInfo.GetValueOrDefault();
        int abovePaletteSize = aboveModeInfo is null ? 0 : above.GetPaletteSize(plane);
        int leftPaletteSize = leftModeInfo is null ? 0 : left.GetPaletteSize(plane);
        ReadOnlySpan<ushort> aboveColors = aboveModeInfo is null ? [] : above.GetPaletteColors(plane);
        ReadOnlySpan<ushort> leftColors = leftModeInfo is null ? [] : left.GetPaletteColors(plane);
        int aboveIndex = 0;
        int leftIndex = 0;
        int count = 0;
        while (aboveIndex < abovePaletteSize && leftIndex < leftPaletteSize)
        {
            ushort aboveColor = aboveColors[aboveIndex];
            ushort leftColor = leftColors[leftIndex];
            if (leftColor < aboveColor)
            {
                AddPaletteCacheColor(cache, ref count, leftColor);
                leftIndex++;
            }
            else
            {
                AddPaletteCacheColor(cache, ref count, aboveColor);
                aboveIndex++;
                if (leftColor == aboveColor)
                {
                    leftIndex++;
                }
            }
        }

        while (aboveIndex < abovePaletteSize)
        {
            AddPaletteCacheColor(cache, ref count, aboveColors[aboveIndex++]);
        }

        while (leftIndex < leftPaletteSize)
        {
            AddPaletteCacheColor(cache, ref count, leftColors[leftIndex++]);
        }

        return count;
    }

    /// <summary>
    /// Appends a palette cache color unless it duplicates the preceding sorted value.
    /// </summary>
    /// <param name="cache">The sorted cache being populated.</param>
    /// <param name="count">The number of colors currently stored.</param>
    /// <param name="color">The next sorted color.</param>
    private static void AddPaletteCacheColor(Span<ushort> cache, ref int count, ushort color)
    {
        if (count == 0 || cache[count - 1] != color)
        {
            cache[count++] = color;
        }
    }

    /// <summary>
    /// Merges selected cached colors with the sorted transmitted colors in one prediction-order palette.
    /// </summary>
    /// <param name="colors">The transmitted colors beginning at <paramref name="cachedColorCount"/> and the merged output.</param>
    /// <param name="cachedColors">The selected cached colors in ascending order.</param>
    /// <param name="paletteSize">The total palette size.</param>
    /// <param name="cachedColorCount">The number of selected cached colors.</param>
    private static void MergePaletteColors(Span<ushort> colors, ReadOnlySpan<ushort> cachedColors, int paletteSize, int cachedColorCount)
    {
        if (cachedColorCount == 0)
        {
            return;
        }

        int cacheIndex = 0;
        int transmittedIndex = cachedColorCount;
        for (int i = 0; i < paletteSize; i++)
        {
            if (cacheIndex < cachedColorCount &&
                (transmittedIndex >= paletteSize || cachedColors[cacheIndex] <= colors[transmittedIndex]))
            {
                colors[i] = cachedColors[cacheIndex++];
            }
            else
            {
                colors[i] = colors[transmittedIndex++];
            }
        }
    }

    /// <summary>
    /// Computes the padded plane dimensions and the portion that lies inside the coded image.
    /// </summary>
    /// <param name="partitionInfo">The current coding block and frame-edge distances.</param>
    /// <param name="planeType">The luma or shared chroma plane class.</param>
    /// <param name="colorConfig">The sequence chroma-subsampling configuration.</param>
    /// <param name="planeWidth">The padded plane-block width in samples.</param>
    /// <param name="planeHeight">The padded plane-block height in samples.</param>
    /// <param name="rows">The number of plane-block rows inside the coded image.</param>
    /// <param name="columns">The number of plane-block columns inside the coded image.</param>
    private static void GetPaletteMapDimensions(
        ref Av1PartitionInfo partitionInfo,
        Av1PlaneType planeType,
        ObuColorConfig colorConfig,
        out int planeWidth,
        out int planeHeight,
        out int rows,
        out int columns)
    {
        int subX = planeType == Av1PlaneType.Uv && colorConfig.SubSamplingX ? 1 : 0;
        int subY = planeType == Av1PlaneType.Uv && colorConfig.SubSamplingY ? 1 : 0;
        int blockWidth = partitionInfo.ModeInfo.BlockSize.GetWidth();
        int blockHeight = partitionInfo.ModeInfo.BlockSize.GetHeight();
        int columnsInsideImage = partitionInfo.ModeBlockToRightEdge >= 0
            ? blockWidth
            : blockWidth + (partitionInfo.ModeBlockToRightEdge >> 3);
        int rowsInsideImage = partitionInfo.ModeBlockToBottomEdge >= 0
            ? blockHeight
            : blockHeight + (partitionInfo.ModeBlockToBottomEdge >> 3);

        planeWidth = blockWidth >> subX;
        planeHeight = blockHeight >> subY;
        columns = columnsInsideImage >> subX;
        rows = rowsInsideImage >> subY;
    }

    /// <summary>
    /// Decodes a padded palette color-index map in AV1 diagonal wavefront order.
    /// </summary>
    /// <param name="reader">The tile symbol decoder.</param>
    /// <param name="paletteSize">The number of palette colors.</param>
    /// <param name="planeType">The luma or shared chroma plane class.</param>
    /// <param name="planeWidth">The padded plane-block width.</param>
    /// <param name="planeHeight">The padded plane-block height.</param>
    /// <param name="rows">The number of rows inside the coded image.</param>
    /// <param name="columns">The number of columns inside the coded image.</param>
    /// <param name="colorIndexMap">The row-addressable destination map.</param>
    private static void DecodePaletteColorMap(
        ref Av1SymbolDecoder reader,
        int paletteSize,
        Av1PlaneType planeType,
        int planeWidth,
        int planeHeight,
        int rows,
        int columns,
        Buffer2DRegion<byte> colorIndexMap)
    {
        colorIndexMap.DangerousGetRowSpan(0)[0] = (byte)reader.ReadUniform(paletteSize);
        Span<byte> colorOrder = stackalloc byte[Av1Constants.PaletteMaxSize];
        for (int diagonal = 1; diagonal < rows + columns - 1; diagonal++)
        {
            int firstColumn = Math.Min(diagonal, columns - 1);
            int lastColumn = Math.Max(0, diagonal - rows + 1);
            for (int column = firstColumn; column >= lastColumn; column--)
            {
                int row = diagonal - column;
                int colorContext = GetPaletteColorIndexContext(
                    colorIndexMap,
                    row,
                    column,
                    paletteSize,
                    colorOrder);

                int colorOrderIndex = reader.ReadPaletteColorIndex(paletteSize, colorContext, planeType);
                colorIndexMap.DangerousGetRowSpan(row)[column] = colorOrder[colorOrderIndex];
            }
        }

        if (columns < planeWidth)
        {
            // Blocks clipped by the right image edge repeat their final coded column into the padded block area.
            for (int row = 0; row < rows; row++)
            {
                Span<byte> colorIndexRow = colorIndexMap.DangerousGetRowSpan(row);
                colorIndexRow.Slice(columns, planeWidth - columns)
                    .Fill(colorIndexRow[columns - 1]);
            }
        }

        // Blocks clipped by the bottom image edge repeat their final coded row for later transform reconstruction.
        ReadOnlySpan<byte> finalRow = colorIndexMap.DangerousGetRowSpan(rows - 1);
        for (int row = rows; row < planeHeight; row++)
        {
            finalRow.CopyTo(colorIndexMap.DangerousGetRowSpan(row));
        }
    }

    /// <summary>
    /// Derives the palette color order and entropy context from the left, upper-left, and above indices.
    /// </summary>
    /// <param name="colorIndexMap">The partially decoded color-index map.</param>
    /// <param name="row">The current map row.</param>
    /// <param name="column">The current map column.</param>
    /// <param name="paletteSize">The number of palette colors.</param>
    /// <param name="colorOrder">The destination color order for the current context.</param>
    /// <returns>The color-index entropy context in the range from zero through four.</returns>
    private static int GetPaletteColorIndexContext(
        Buffer2DRegion<byte> colorIndexMap,
        int row,
        int column,
        int paletteSize,
        Span<byte> colorOrder)
    {
        Span<int> neighborColors = stackalloc int[3];
        ReadOnlySpan<byte> currentRow = colorIndexMap.DangerousGetRowSpan(row);
        neighborColors[0] = column > 0 ? currentRow[column - 1] : -1;
        if (row > 0)
        {
            ReadOnlySpan<byte> aboveRow = colorIndexMap.DangerousGetRowSpan(row - 1);
            neighborColors[1] = column > 0 ? aboveRow[column - 1] : -1;
            neighborColors[2] = aboveRow[column];
        }
        else
        {
            neighborColors[1] = -1;
            neighborColors[2] = -1;
        }

        Span<int> scores = stackalloc int[Av1Constants.PaletteMaxSize];
        ReadOnlySpan<int> neighborWeights = [2, 1, 2];
        for (int i = 0; i < neighborColors.Length; i++)
        {
            if (neighborColors[i] >= 0)
            {
                scores[neighborColors[i]] += neighborWeights[i];
            }
        }

        for (int i = 0; i < colorOrder.Length; i++)
        {
            colorOrder[i] = (byte)i;
        }

        // Stable descending score order keeps lower palette indices ahead when neighboring scores tie.
        for (int i = 0; i < 3; i++)
        {
            int maximumScore = scores[i];
            int maximumIndex = i;
            for (int j = i + 1; j < paletteSize; j++)
            {
                if (scores[j] > maximumScore)
                {
                    maximumScore = scores[j];
                    maximumIndex = j;
                }
            }

            if (maximumIndex != i)
            {
                byte maximumColor = colorOrder[maximumIndex];
                for (int j = maximumIndex; j > i; j--)
                {
                    scores[j] = scores[j - 1];
                    colorOrder[j] = colorOrder[j - 1];
                }

                scores[i] = maximumScore;
                colorOrder[i] = maximumColor;
            }
        }

        int contextHash = scores[0] + (2 * scores[1]) + (2 * scores[2]);
        return PaletteColorIndexContexts[contextHash];
    }

    /// <summary>
    /// Reads the joint signs and nonzero alpha magnitudes for chroma-from-luma prediction.
    /// </summary>
    /// <param name="reader">The tile symbol decoder.</param>
    /// <param name="modeInfo">The block mode information to populate.</param>
    /// <remarks>Implements AV1 section 5.11.45.</remarks>
    private static void ReadChromaFromLumaAlphas(ref Av1SymbolDecoder reader, ref Av1BlockModeInfo modeInfo)
    {
        int jointSignPlus1 = reader.ReadChromFromLumaSign() + 1;
        int index = 0;
        if (jointSignPlus1 >= 3)
        {
            index = reader.ReadChromaFromLumaAlphaU(jointSignPlus1) << Av1Constants.ChromaFromLumaAlphabetSizeLog2;
        }

        if (jointSignPlus1 % 3 != 0)
        {
            index += reader.ReadChromaFromLumaAlphaV(jointSignPlus1);
        }

        modeInfo.ChromaFromLumaAlphaSign = jointSignPlus1 - 1;
        modeInfo.ChromaFromLumaAlphaIndex = index;
    }

    /// <summary>
    /// Reads a directional intra-prediction angle adjustment when the block and mode permit one.
    /// </summary>
    /// <param name="reader">The tile symbol decoder.</param>
    /// <param name="mode">The selected luma or chroma prediction mode.</param>
    /// <param name="blockSize">The block size.</param>
    /// <returns>The signed angle adjustment.</returns>
    /// <remarks>Implements AV1 sections 5.11.42 and 5.11.43.</remarks>
    private static int IntraAngleInfo(ref Av1SymbolDecoder reader, Av1PredictionMode mode, Av1BlockSize blockSize)
    {
        int angleDelta = 0;
        if (blockSize >= Av1BlockSize.Block8x8 && IsDirectionalMode(mode))
        {
            int symbol = reader.ReadAngleDelta(mode);
            angleDelta = symbol - Av1Constants.MaxAngleDelta;
        }

        return angleDelta;
    }

    /// <summary>
    /// Determines whether a prediction mode belongs to the AV1 directional-mode range.
    /// </summary>
    /// <param name="mode">The prediction mode.</param>
    /// <returns><see langword="true"/> for a directional mode; otherwise, <see langword="false"/>.</returns>
    private static bool IsDirectionalMode(Av1PredictionMode mode)
        => mode is >= Av1PredictionMode.Vertical and <= Av1PredictionMode.Directional67Degrees;

    /// <summary>
    /// Reads or inherits a segment identifier and writes it over every 4x4 position covered by the block.
    /// </summary>
    /// <param name="reader">The tile symbol decoder.</param>
    /// <param name="partitionInfo">The current coding block.</param>
    /// <remarks>Implements AV1 section 5.11.8.</remarks>
    private void IntraSegmentId(ref Av1SymbolDecoder reader, ref Av1PartitionInfo partitionInfo)
    {
        if (!this.FrameHeader.SegmentationParameters.Enabled)
        {
            // Disabled segmentation assigns the default ID without allocating or populating a retained map.
            return;
        }

        this.ReadSegmentId(ref reader, ref partitionInfo);
        Point modeInfoPosition = new(partitionInfo.ColumnIndex, partitionInfo.RowIndex);
        this.FrameInfo.SetSegmentId(partitionInfo.ModeInfo.BlockSize, modeInfoPosition, partitionInfo.ModeInfo.SegmentId);
    }

    /// <summary>
    /// Reads or inherits the segment identifier for one inter-frame block and updates its 4x4 map coverage.
    /// </summary>
    /// <param name="reader">The tile symbol decoder.</param>
    /// <param name="partitionInfo">The current coding block and its available neighbors.</param>
    /// <param name="beforeSkip">Whether this invocation precedes the block's residual-skip decision.</param>
    /// <remarks>
    /// Implements <c>read_inter_segment_id</c> from AV1 section 5.11.8.
    /// </remarks>
    public void ReadInterSegmentId(ref Av1SymbolDecoder reader, ref Av1PartitionInfo partitionInfo, bool beforeSkip)
    {
        ObuSegmentationParameters segmentationParameters = this.FrameHeader.SegmentationParameters;
        ref Av1BlockModeInfo modeInfo = ref partitionInfo.ModeInfo;

        if (!segmentationParameters.Enabled)
        {
            // Disabled segmentation has no allocated map and normatively assigns segment zero.
            modeInfo.SegmentId = 0;
            return;
        }

        Point modeInfoPosition = new(partitionInfo.ColumnIndex, partitionInfo.RowIndex);

        if (segmentationParameters.SegmentationUpdateMap == 0)
        {
            // The frame map was inherited as one contiguous copy during reader construction. Resolve the same clipped
            // minimum that the reference decoder obtains from last_frame_seg_map so block state and the already copied map agree.
            modeInfo.SegmentId = this.FrameInfo.GetPredictedSegmentId(this.primaryReferenceState, modeInfo.BlockSize, modeInfoPosition);
            return;
        }

        if (beforeSkip)
        {
            if (!segmentationParameters.SegmentIdPrecedesSkip)
            {
                // The caller invokes this once before skip for every inter block; post-skip segment syntax owns this case.
                return;
            }
        }
        else if (modeInfo.Skip)
        {
            if (segmentationParameters.SegmentationTemporalUpdate == 1)
            {
                // Skipped blocks use the spatial segment predictor and signal no temporal-prediction bit.
                modeInfo.SegmentIdPredicted = false;
            }

            this.ReadSegmentId(ref reader, ref partitionInfo);
            this.FrameInfo.SetSegmentId(modeInfo.BlockSize, modeInfoPosition, modeInfo.SegmentId);
            return;
        }

        if (segmentationParameters.SegmentationTemporalUpdate == 1)
        {
            // The binary context counts only neighboring blocks that themselves selected the retained map. Segment
            // values do not participate in this decision.
            int context = Av1SymbolContextHelper.GetSegmentIdPredictedContext(partitionInfo.AboveModeInfo, partitionInfo.LeftModeInfo);
            modeInfo.SegmentIdPredicted = reader.ReadSegmentIdPredicted(context);
            if (modeInfo.SegmentIdPredicted)
            {
                modeInfo.SegmentId = this.FrameInfo.GetPredictedSegmentId(this.primaryReferenceState, modeInfo.BlockSize, modeInfoPosition);
            }
            else
            {
                this.ReadSegmentId(ref reader, ref partitionInfo);
            }
        }
        else
        {
            this.ReadSegmentId(ref reader, ref partitionInfo);
        }

        this.FrameInfo.SetSegmentId(modeInfo.BlockSize, modeInfoPosition, modeInfo.SegmentId);
    }

    /// <summary>
    /// Predicts and, when required, decodes the spatially coded segment identifier for a block.
    /// </summary>
    /// <param name="reader">The tile symbol decoder.</param>
    /// <param name="partitionInfo">The current coding block and its available neighbors.</param>
    /// <remarks>Implements AV1 section 5.11.9.</remarks>
    private void ReadSegmentId(ref Av1SymbolDecoder reader, ref Av1PartitionInfo partitionInfo)
    {
        int predictor;
        int prevUL = -1;
        int prevU = -1;
        int prevL = -1;
        int columnIndex = partitionInfo.ColumnIndex;
        int rowIndex = partitionInfo.RowIndex;
        if (partitionInfo.AvailableAbove && partitionInfo.AvailableLeft)
        {
            prevUL = this.FrameInfo.GetSegmentId(rowIndex - 1, columnIndex - 1);
        }

        if (partitionInfo.AvailableAbove)
        {
            prevU = this.FrameInfo.GetSegmentId(rowIndex - 1, columnIndex);
        }

        if (partitionInfo.AvailableLeft)
        {
            prevL = this.FrameInfo.GetSegmentId(rowIndex, columnIndex - 1);
        }

        if (prevU == -1)
        {
            predictor = prevL == -1 ? 0 : prevL;
        }
        else if (prevL == -1)
        {
            predictor = prevU;
        }
        else
        {
            predictor = prevU == prevUL ? prevU : prevL;
        }

        if (partitionInfo.ModeInfo.Skip)
        {
            partitionInfo.ModeInfo.SegmentId = predictor;
        }
        else
        {
            // Any unavailable neighbor selects the edge context; otherwise, agreement among two
            // or three neighbors increases the specificity of the segment-ID distribution.
            int ctx = prevUL < 0 ? 0
                : prevUL == prevU && prevUL == prevL ? 2
                : prevUL == prevU || prevUL == prevL || prevU == prevL ? 1 : 0;
            int lastActiveSegmentId = this.FrameHeader.SegmentationParameters.LastActiveSegmentId;
            int segmentId = Av1SymbolContextHelper.NegativeDeinterleave(reader.ReadSegmentId(ctx), predictor, lastActiveSegmentId + 1);
            if (segmentId is < 0 || segmentId > lastActiveSegmentId)
            {
                // The coded alphabet always contains eight symbols, even when the frame activates fewer segments.
                // Validate the reconstructed ID at the same corruption boundary as the reference decoder's read_segment_id.
                throw new InvalidImageContentException("The decoded AV1 segment identifier exceeds the active segment range.");
            }

            partitionInfo.ModeInfo.SegmentId = segmentId;
        }
    }

    /// <summary>
    /// Reads the constrained directional enhancement filter strength for the block's 64x64 filter unit.
    /// </summary>
    /// <param name="reader">The tile symbol decoder.</param>
    /// <param name="partitionInfo">The current coding block.</param>
    private void ReadCdef(ref Av1SymbolDecoder reader, ref Av1PartitionInfo partitionInfo)
    {
        if (partitionInfo.ModeInfo.Skip || this.FrameHeader.CodedLossless || !this.SequenceHeader.EnableCdef || this.FrameHeader.AllowIntraBlockCopy)
        {
            return;
        }

        int cdefSize4 = Av1BlockSize.Block64x64.Get4x4WideCount();
        int superblockMask = this.SequenceHeader.SuperblockModeInfoSize - 1;
        int rowInSuperblock = partitionInfo.RowIndex & superblockMask;
        int columnInSuperblock = partitionInfo.ColumnIndex & superblockMask;
        int unitRow = rowInSuperblock / cdefSize4;
        int unitColumn = columnInSuperblock / cdefSize4;
        int index = this.SequenceHeader.SuperblockSize == Av1BlockSize.Block128x128 ? unitColumn + (unitRow << 1) : 0;
        Span<int> cdefStrength = partitionInfo.SuperblockInfo.CdefStrength;
        if (cdefStrength[index] == -1)
        {
            int cdefStrengthIndex = reader.ReadCdfStrength(this.FrameHeader.CdefParameters.BitCount);
            int blockWidth4 = partitionInfo.ModeInfo.BlockSize.Get4x4WideCount();
            int blockHeight4 = partitionInfo.ModeInfo.BlockSize.Get4x4HighCount();
            int lastUnitRow = (rowInSuperblock + blockHeight4 - 1) / cdefSize4;
            int lastUnitColumn = (columnInSuperblock + blockWidth4 - 1) / cdefSize4;

            // A coding block can cover the top-left cell of more than one 64x64 CDEF unit. the reference decoder
            // stores the index on shared mode information, so the frame-owned unit map must mirror it.
            for (int coveredUnitRow = unitRow; coveredUnitRow <= lastUnitRow; coveredUnitRow++)
            {
                for (int coveredUnitColumn = unitColumn; coveredUnitColumn <= lastUnitColumn; coveredUnitColumn++)
                {
                    int coveredIndex = this.SequenceHeader.SuperblockSize == Av1BlockSize.Block128x128
                        ? coveredUnitColumn + (coveredUnitRow << 1)
                        : 0;

                    cdefStrength[coveredIndex] = cdefStrengthIndex;
                }
            }
        }
    }

    /// <summary>
    /// Reads and accumulates the loop-filter delta values carried by a coding block.
    /// </summary>
    /// <param name="reader">The tile symbol decoder.</param>
    /// <param name="partitionInfo">The current coding block and superblock delta storage.</param>
    private void ReadDeltaLoopFilter(ref Av1SymbolDecoder reader, ref Av1PartitionInfo partitionInfo)
    {
        if (!this.FrameHeader.DeltaLoopFilterParameters.IsPresent || partitionInfo.ModeInfo.PositionInSuperblock != Point.Empty)
        {
            return;
        }

        Av1BlockSize superBlockSize = this.SequenceHeader.Use128x128Superblock ? Av1BlockSize.Block128x128 : Av1BlockSize.Block64x64;
        if (partitionInfo.ModeInfo.BlockSize != superBlockSize || !partitionInfo.ModeInfo.Skip)
        {
            int frameLoopFilterCount = 1;
            if (this.FrameHeader.DeltaLoopFilterParameters.IsMulti)
            {
                frameLoopFilterCount = this.SequenceHeader.ColorConfig.PlaneCount > 1 ? Av1Constants.FrameLoopFilterCount : Av1Constants.FrameLoopFilterCount - 2;
            }

            for (int i = 0; i < frameLoopFilterCount; i++)
            {
                int reducedDeltaLoopFilterLevel = reader.ReadDeltaLoopFilter();
                int deltaLoopFilterResolution = this.FrameHeader.DeltaLoopFilterParameters.Resolution;
                this.currentDeltaLoopFilter[i] = Av1Math.Clip3(
                    -Av1Constants.MaxLoopFilter,
                    Av1Constants.MaxLoopFilter,
                    this.currentDeltaLoopFilter[i] + (reducedDeltaLoopFilterLevel * deltaLoopFilterResolution));
            }
        }

        // Delta-LF values are predicted across superblocks within a tile, but every block in one superblock observes
        // the same resulting values. Snapshot the predictors so later filtering does not depend on parse order.
        this.currentDeltaLoopFilter[..Av1Constants.FrameLoopFilterCount].CopyTo(partitionInfo.SuperblockInfo.SuperblockDeltaLoopFilter);
    }

    /// <summary>
    /// Reads or infers the residual-skip flag for a coding block.
    /// </summary>
    /// <param name="reader">The tile symbol decoder.</param>
    /// <param name="partitionInfo">The current coding block and its available neighbors.</param>
    /// <returns><see langword="true"/> when the block omits residual coefficients; otherwise, <see langword="false"/>.</returns>
    private bool ReadSkip(ref Av1SymbolDecoder reader, ref Av1PartitionInfo partitionInfo)
    {
        int segmentId = partitionInfo.ModeInfo.SegmentId;
        if (this.FrameHeader.SegmentationParameters.SegmentIdPrecedesSkip &&
            this.FrameHeader.SegmentationParameters.IsFeatureActive(segmentId, ObuSegmentationLevelFeature.Skip))
        {
            return true;
        }
        else
        {
            int aboveSkip = partitionInfo.AboveModeInfo is not null && partitionInfo.AboveModeInfo.Value.Skip ? 1 : 0;
            int leftSkip = partitionInfo.LeftModeInfo is not null && partitionInfo.LeftModeInfo.Value.Skip ? 1 : 0;
            return reader.ReadSkip(aboveSkip + leftSkip);
        }
    }

    /// <summary>
    /// Reads compound skip-mode selection when the frame, segment, and block geometry permit it.
    /// </summary>
    /// <param name="reader">The tile symbol decoder.</param>
    /// <param name="partitionInfo">The current coding block and its available neighbors.</param>
    /// <returns><see langword="true"/> when the block selects the frame's derived skip-mode reference pair.</returns>
    private bool ReadSkipMode(ref Av1SymbolDecoder reader, ref Av1PartitionInfo partitionInfo)
    {
        ref Av1BlockModeInfo modeInfo = ref partitionInfo.ModeInfo;
        ObuSegmentationParameters segmentationParameters = this.FrameHeader.SegmentationParameters;
        int segmentId = modeInfo.SegmentId;

        if (!this.FrameHeader.SkipModeParameters.SkipModeFlag ||
            segmentationParameters.IsFeatureActive(segmentId, ObuSegmentationLevelFeature.Skip) ||
            Math.Min(modeInfo.BlockSize.GetWidth(), modeInfo.BlockSize.GetHeight()) < 8 ||
            segmentationParameters.IsFeatureActive(segmentId, ObuSegmentationLevelFeature.ReferenceFrame) ||
            segmentationParameters.IsFeatureActive(segmentId, ObuSegmentationLevelFeature.GlobalMotionVector))
        {
            // Segment reference and global-motion features force single-reference prediction, while skip mode always
            // selects the derived compound pair. The syntax therefore omits the skip-mode symbol in either case.
            return false;
        }

        int aboveSkipMode = partitionInfo.AboveModeInfo is not null && partitionInfo.AboveModeInfo.Value.SkipMode ? 1 : 0;
        int leftSkipMode = partitionInfo.LeftModeInfo is not null && partitionInfo.LeftModeInfo.Value.SkipMode ? 1 : 0;
        return reader.ReadSkipMode(aboveSkipMode + leftSkipMode);
    }

    /// <summary>
    /// Reads or infers whether an inter-frame coding block uses inter prediction.
    /// </summary>
    /// <param name="reader">The tile symbol decoder.</param>
    /// <param name="partitionInfo">The current coding block and its available neighbors.</param>
    /// <returns><see langword="true"/> for an inter-coded block; otherwise, <see langword="false"/>.</returns>
    private bool ReadIsInter(ref Av1SymbolDecoder reader, ref Av1PartitionInfo partitionInfo)
    {
        ObuSegmentationParameters segmentationParameters = this.FrameHeader.SegmentationParameters;
        int segmentId = partitionInfo.ModeInfo.SegmentId;

        if (segmentationParameters.IsFeatureActive(segmentId, ObuSegmentationLevelFeature.ReferenceFrame))
        {
            // Reference feature values use the same numeric labels as Av1ReferenceFrameType. INTRA_FRAME is zero;
            // every canonical inter reference begins at LAST_FRAME and therefore has a positive value.
            int referenceFrame = segmentationParameters.GetFeatureData(segmentId, (int)ObuSegmentationLevelFeature.ReferenceFrame);
            return referenceFrame >= (int)Av1ReferenceFrameType.Last;
        }

        if (segmentationParameters.IsFeatureActive(segmentId, ObuSegmentationLevelFeature.GlobalMotionVector))
        {
            return true;
        }

        int context = Av1SymbolContextHelper.GetIntraInterContext(partitionInfo.AboveModeInfo, partitionInfo.LeftModeInfo);
        return reader.ReadIsInter(context);
    }

    /// <summary>
    /// Reads or infers the retained reference-frame labels selected by an inter-coded block.
    /// </summary>
    /// <param name="reader">The tile symbol decoder.</param>
    /// <param name="partitionInfo">The current coding block and its available neighbors.</param>
    private void ReadReferenceFrames(ref Av1SymbolDecoder reader, ref Av1PartitionInfo partitionInfo)
    {
        ref Av1BlockModeInfo modeInfo = ref partitionInfo.ModeInfo;
        Span<Av1ReferenceFrameType> references = modeInfo.ReferenceFrames;
        if (modeInfo.SkipMode)
        {
            ObuSkipModeParameters skipModeParameters = this.FrameHeader.SkipModeParameters;
            references[0] = skipModeParameters.FirstReferenceFrame;
            references[1] = skipModeParameters.SecondReferenceFrame;
            return;
        }

        ObuSegmentationParameters segmentationParameters = this.FrameHeader.SegmentationParameters;
        int segmentId = modeInfo.SegmentId;
        if (segmentationParameters.IsFeatureActive(segmentId, ObuSegmentationLevelFeature.ReferenceFrame))
        {
            references[0] = (Av1ReferenceFrameType)segmentationParameters.GetFeatureData(
                segmentId,
                (int)ObuSegmentationLevelFeature.ReferenceFrame);

            references[1] = Av1ReferenceFrameType.None;
            return;
        }

        if (segmentationParameters.IsFeatureActive(segmentId, ObuSegmentationLevelFeature.Skip) ||
            segmentationParameters.IsFeatureActive(segmentId, ObuSegmentationLevelFeature.GlobalMotionVector))
        {
            references[0] = Av1ReferenceFrameType.Last;
            references[1] = Av1ReferenceFrameType.None;
            return;
        }

        Span<byte> referenceCounts = this.neighborReferenceCounts;
        Av1SymbolContextHelper.CollectNeighborReferenceCounts(
            partitionInfo.AboveModeInfo,
            partitionInfo.LeftModeInfo,
            referenceCounts);

        bool compoundReferenceAllowed = Math.Min(modeInfo.BlockSize.GetWidth(), modeInfo.BlockSize.GetHeight()) >= 8;
        if (compoundReferenceAllowed && this.FrameHeader.ReferenceMode == ObuReferenceMode.ReferenceModeSelect)
        {
            int context = Av1SymbolContextHelper.GetReferenceModeContext(
                partitionInfo.AboveModeInfo,
                partitionInfo.LeftModeInfo);

            if (reader.ReadIsCompoundReference(context))
            {
                int typeContext = Av1SymbolContextHelper.GetCompoundReferenceTypeContext(
                    partitionInfo.AboveModeInfo,
                    partitionInfo.LeftModeInfo);

                if (!reader.ReadCompoundReferenceIsBidirectional(typeContext))
                {
                    int directionContext = Av1SymbolContextHelper.GetUnidirectionalCompoundBackwardContext(referenceCounts);
                    if (reader.ReadUnidirectionalCompoundReference(directionContext, decision: 0))
                    {
                        references[0] = Av1ReferenceFrameType.Backward;
                        references[1] = Av1ReferenceFrameType.Alternate;
                    }
                    else
                    {
                        int unidirectionalForwardGroupContext = Av1SymbolContextHelper.GetUnidirectionalCompoundLast3OrGoldenContext(referenceCounts);
                        if (!reader.ReadUnidirectionalCompoundReference(unidirectionalForwardGroupContext, decision: 1))
                        {
                            references[0] = Av1ReferenceFrameType.Last;
                            references[1] = Av1ReferenceFrameType.Last2;
                        }
                        else
                        {
                            int forwardChoiceContext = Av1SymbolContextHelper.GetUnidirectionalCompoundGoldenContext(referenceCounts);
                            references[0] = Av1ReferenceFrameType.Last;
                            references[1] = reader.ReadUnidirectionalCompoundReference(forwardChoiceContext, decision: 2)
                                ? Av1ReferenceFrameType.Golden
                                : Av1ReferenceFrameType.Last3;
                        }
                    }

                    return;
                }

                int forwardGroupContext = Av1SymbolContextHelper.GetCompoundForwardLast3OrGoldenContext(referenceCounts);
                if (!reader.ReadCompoundForwardReference(forwardGroupContext, decision: 0))
                {
                    int forwardChoiceContext = Av1SymbolContextHelper.GetCompoundForwardLast2Context(referenceCounts);
                    references[0] = reader.ReadCompoundForwardReference(forwardChoiceContext, decision: 1)
                        ? Av1ReferenceFrameType.Last2
                        : Av1ReferenceFrameType.Last;
                }
                else
                {
                    int forwardChoiceContext = Av1SymbolContextHelper.GetCompoundForwardGoldenContext(referenceCounts);
                    references[0] = reader.ReadCompoundForwardReference(forwardChoiceContext, decision: 2)
                        ? Av1ReferenceFrameType.Golden
                        : Av1ReferenceFrameType.Last3;
                }

                int backwardGroupContext = Av1SymbolContextHelper.GetCompoundBackwardAlternateContext(referenceCounts);
                if (reader.ReadCompoundBackwardReference(backwardGroupContext, decision: 0))
                {
                    references[1] = Av1ReferenceFrameType.Alternate;
                }
                else
                {
                    int backwardChoiceContext = Av1SymbolContextHelper.GetCompoundBackwardAlternate2Context(referenceCounts);
                    references[1] = reader.ReadCompoundBackwardReference(backwardChoiceContext, decision: 1)
                        ? Av1ReferenceFrameType.Alternate2
                        : Av1ReferenceFrameType.Backward;
                }

                return;
            }
        }

        Av1ReferenceFrameType reference;
        if (reader.ReadSingleReferenceIsBackward(Av1SymbolContextHelper.GetSingleReferenceBackwardContext(referenceCounts)))
        {
            if (reader.ReadSingleReferenceIsAlternate(Av1SymbolContextHelper.GetSingleReferenceAlternateContext(referenceCounts)))
            {
                reference = Av1ReferenceFrameType.Alternate;
            }
            else
            {
                reference = reader.ReadSingleReferenceIsAlternate2(
                    Av1SymbolContextHelper.GetSingleReferenceAlternate2Context(referenceCounts))
                    ? Av1ReferenceFrameType.Alternate2
                    : Av1ReferenceFrameType.Backward;
            }
        }
        else if (reader.ReadSingleReferenceIsLast3OrGolden(
            Av1SymbolContextHelper.GetSingleReferenceLast3OrGoldenContext(referenceCounts)))
        {
            reference = reader.ReadSingleReferenceIsGolden(Av1SymbolContextHelper.GetSingleReferenceGoldenContext(referenceCounts))
                ? Av1ReferenceFrameType.Golden
                : Av1ReferenceFrameType.Last3;
        }
        else
        {
            reference = reader.ReadSingleReferenceIsLast2(Av1SymbolContextHelper.GetSingleReferenceLast2Context(referenceCounts))
                ? Av1ReferenceFrameType.Last2
                : Av1ReferenceFrameType.Last;
        }

        references[0] = reference;
        references[1] = Av1ReferenceFrameType.None;
    }

    /// <summary>
    /// Reads and accumulates a superblock quantizer-index delta when the block carries one.
    /// </summary>
    /// <param name="reader">The tile symbol decoder.</param>
    /// <param name="partitionInfo">The current coding block and superblock quantizer storage.</param>
    private void ReadDeltaQuantizerIndex(ref Av1SymbolDecoder reader, ref Av1PartitionInfo partitionInfo)
    {
        if (!this.FrameHeader.DeltaQParameters.IsPresent || partitionInfo.ModeInfo.PositionInSuperblock != Point.Empty)
        {
            return;
        }

        if (partitionInfo.ModeInfo.BlockSize != this.SequenceHeader.SuperblockSize || !partitionInfo.ModeInfo.Skip)
        {
            int reducedDeltaQuantizerIndex = reader.ReadDeltaQuantizerIndex();
            int deltaQuantizerResolution = this.FrameHeader.DeltaQParameters.Resolution;
            this.currentQuantizerIndex = Av1Math.Clip3(
                1,
                Av1Constants.MaxQ,
                this.currentQuantizerIndex + (reducedDeltaQuantizerIndex * deltaQuantizerResolution));
        }

        partitionInfo.SuperblockInfo.SuperblockQuantizerIndex = this.currentQuantizerIndex;
    }

    /// <summary>
    /// Determines whether a frame-relative mode-information position lies inside the active tile.
    /// </summary>
    /// <param name="tileInfo">The active tile boundaries.</param>
    /// <param name="rowIndex">The frame-relative mode-information row.</param>
    /// <param name="columnIndex">The frame-relative mode-information column.</param>
    /// <returns><see langword="true"/> when the position lies within the active tile; otherwise, <see langword="false"/>.</returns>
    private static bool IsInside(Av1TileInfo tileInfo, int rowIndex, int columnIndex) =>
        columnIndex >= tileInfo.ModeInfoColumnStart &&
        columnIndex < tileInfo.ModeInfoColumnEnd &&
        rowIndex >= tileInfo.ModeInfoRowStart &&
        rowIndex < tileInfo.ModeInfoRowEnd;

    /// <summary>
    /// Derives the partition entropy context from the current split bit of the above and left neighbors.
    /// </summary>
    /// <param name="location">The partition origin in 4x4 mode-information units.</param>
    /// <param name="blockSize">The square parent block size.</param>
    /// <param name="tileInfo">The active tile boundaries.</param>
    /// <param name="superblockInfo">The containing superblock.</param>
    /// <returns>The partition entropy context.</returns>
    private int GetPartitionPlaneContext(Point location, Av1BlockSize blockSize, Av1TileInfo tileInfo, Av1SuperblockInfo superblockInfo)
    {
        // The five stored split bits begin at the 8x8 partition point, so normalize the block-size log to that bit index.
        int aboveCtx = this.aboveNeighborContext.AbovePartitionWidth[location.X - tileInfo.ModeInfoColumnStart];
        int leftCtx = this.leftNeighborContext.LeftPartitionHeight[(location.Y - superblockInfo.ModeInfoPosition.Y) & Av1PartitionContext.Mask];
        int blockSizeLog = blockSize.Get4x4WidthLog2() - Av1BlockSize.Block8x8.Get4x4WidthLog2();
        int above = (aboveCtx >> blockSizeLog) & 0x1;
        int left = (leftCtx >> blockSizeLog) & 0x1;
        DebugGuard.IsTrue(blockSize.Get4x4WidthLog2() == blockSize.Get4x4HeightLog2(), "Blocks should be square.");
        DebugGuard.MustBeGreaterThanOrEqualTo(blockSizeLog, 0, nameof(blockSizeLog));
        return ((left << 1) + above) + (blockSizeLog * Av1Constants.PartitionProbabilitySet);
    }

    /// <summary>
    /// Publishes the decoded partition sizes to the above and left neighbor contexts.
    /// </summary>
    /// <param name="modeInfoLocation">The parent block origin in 4x4 mode-information units.</param>
    /// <param name="tileLoc">The active tile boundaries.</param>
    /// <param name="superblockInfo">The containing superblock.</param>
    /// <param name="subSize">The primary size produced by the partition.</param>
    /// <param name="blockSize">The parent block size.</param>
    /// <param name="partition">The decoded partition type.</param>
    private void UpdatePartitionContext(Point modeInfoLocation, Av1TileInfo tileLoc, Av1SuperblockInfo superblockInfo, Av1BlockSize subSize, Av1BlockSize blockSize, Av1PartitionType partition)
    {
        if (blockSize >= Av1BlockSize.Block8x8)
        {
            int hbs = blockSize.Get4x4WideCount() / 2;
            Av1BlockSize blockSize2 = Av1PartitionType.Split.GetBlockSubSize(blockSize);
            switch (partition)
            {
                case Av1PartitionType.Split:
                    if (blockSize != Av1BlockSize.Block8x8)
                    {
                        break;
                    }

                    goto PARTITIONS;
                case Av1PartitionType.None:
                case Av1PartitionType.Horizontal:
                case Av1PartitionType.Vertical:
                case Av1PartitionType.Horizontal4:
                case Av1PartitionType.Vertical4:
                    PARTITIONS:
                    this.aboveNeighborContext.UpdatePartition(modeInfoLocation, tileLoc, subSize, blockSize);
                    this.leftNeighborContext.UpdatePartition(modeInfoLocation, superblockInfo, subSize, blockSize);
                    break;
                case Av1PartitionType.HorizontalA:
                    this.aboveNeighborContext.UpdatePartition(modeInfoLocation, tileLoc, blockSize2, subSize);
                    this.leftNeighborContext.UpdatePartition(modeInfoLocation, superblockInfo, blockSize2, subSize);
                    Point locHorizontalA = new(modeInfoLocation.X, modeInfoLocation.Y + hbs);
                    this.aboveNeighborContext.UpdatePartition(locHorizontalA, tileLoc, subSize, subSize);
                    this.leftNeighborContext.UpdatePartition(locHorizontalA, superblockInfo, subSize, subSize);
                    break;
                case Av1PartitionType.HorizontalB:
                    this.aboveNeighborContext.UpdatePartition(modeInfoLocation, tileLoc, subSize, subSize);
                    this.leftNeighborContext.UpdatePartition(modeInfoLocation, superblockInfo, subSize, subSize);
                    Point locHorizontalB = new(modeInfoLocation.X, modeInfoLocation.Y + hbs);
                    this.aboveNeighborContext.UpdatePartition(locHorizontalB, tileLoc, blockSize2, subSize);
                    this.leftNeighborContext.UpdatePartition(locHorizontalB, superblockInfo, blockSize2, subSize);
                    break;
                case Av1PartitionType.VerticalA:
                    this.aboveNeighborContext.UpdatePartition(modeInfoLocation, tileLoc, blockSize2, subSize);
                    this.leftNeighborContext.UpdatePartition(modeInfoLocation, superblockInfo, blockSize2, subSize);
                    Point locVerticalA = new(modeInfoLocation.X + hbs, modeInfoLocation.Y);
                    this.aboveNeighborContext.UpdatePartition(locVerticalA, tileLoc, subSize, subSize);
                    this.leftNeighborContext.UpdatePartition(locVerticalA, superblockInfo, subSize, subSize);
                    break;
                case Av1PartitionType.VerticalB:
                    this.aboveNeighborContext.UpdatePartition(modeInfoLocation, tileLoc, subSize, subSize);
                    this.leftNeighborContext.UpdatePartition(modeInfoLocation, superblockInfo, subSize, subSize);
                    Point locVerticalB = new(modeInfoLocation.X + hbs, modeInfoLocation.Y);
                    this.aboveNeighborContext.UpdatePartition(locVerticalB, tileLoc, blockSize2, subSize);
                    this.leftNeighborContext.UpdatePartition(locVerticalB, superblockInfo, blockSize2, subSize);
                    break;
                default:
                    throw new InvalidImageContentException($"Unknown partition type: {partition}");
            }
        }
    }

    /// <summary>
    /// Carries the two shared palette maps as one valid constructor state.
    /// </summary>
    internal readonly struct PaletteColorIndexMaps(Buffer2D<byte> luma, Buffer2D<byte> chroma)
    {
        /// <summary>
        /// Gets the shared luma palette map.
        /// </summary>
        public Buffer2D<byte> Luma { get; } = luma;

        /// <summary>
        /// Gets the shared chroma palette map.
        /// </summary>
        public Buffer2D<byte> Chroma { get; } = chroma;
    }

    /// <summary>
    /// Provides inline storage for the two self-guided restoration coefficients of each of the three AV1 planes.
    /// </summary>
    /// <typeparam name="T">The stored value type.</typeparam>
    [InlineArray(6)]
    private struct InlineArray6<T>
    {
        /// <summary>
        /// The first element in the compiler-expanded inline buffer.
        /// </summary>
        private T element;
    }
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.ChromaFromLuma;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <content>
/// Provides live-probability chroma mode decisions for intra encoding.
/// </content>
internal static partial class Av1IntraSuperblockEncoder
{
    private static readonly ushort[] LumaDerivedChromaModeMasks =
    [
        0x2201, 0x2203, 0x2205, 0x2209, 0x2211, 0x2221, 0x2241,
        0x2281, 0x2301, 0x2201, 0x2601, 0x2A01, 0x3201
    ];

    /// <summary>
    /// Gets the spatial chroma mode evaluation order.
    /// </summary>
    private static ReadOnlySpan<Av1ChromaPredictionMode> ChromaModeSearchOrder =>
    [
        Av1ChromaPredictionMode.DC,
        Av1ChromaPredictionMode.Horizontal,
        Av1ChromaPredictionMode.Vertical,
        Av1ChromaPredictionMode.Smooth,
        Av1ChromaPredictionMode.Paeth,
        Av1ChromaPredictionMode.SmoothVertical,
        Av1ChromaPredictionMode.SmoothHorizontal,
        Av1ChromaPredictionMode.Directional135Degrees,
        Av1ChromaPredictionMode.Directional203Degrees,
        Av1ChromaPredictionMode.Directional157Degrees,
        Av1ChromaPredictionMode.Directional67Degrees,
        Av1ChromaPredictionMode.Directional113Degrees,
        Av1ChromaPredictionMode.Directional45Degrees
    ];

    /// <summary>
    /// Gets the chroma angle order with even neighbors evaluated before odd-angle pruning.
    /// </summary>
    private static ReadOnlySpan<sbyte> ChromaAngleSearchOrder => [0, 2, -2, 1, -1, 3, -3];

    internal partial struct ModeDecision<TSample, TOperator>
        where TSample : unmanaged
        where TOperator : struct, IBlockEncodingOperator<TSample>
    {
        /// <summary>
        /// Refines coefficients for the selected UV predictor without repeating mode or alpha search.
        /// Reference: the av1_txfm_uvrd() call of refine_winner_mode_tx() for an intra winner.
        /// </summary>
        private Av1RateDistortionStatistics RefineSelectedChroma(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ushort tileIndex,
            Av1MacroBlockModeInfo modeInfo,
            Av1EncoderBlockStruct block,
            Av1EncoderPaletteInfo paletteInfo,
            Av1RateDistortionStatistics previousStatistics)
        {
            if (!block.HasChroma)
            {
                return new(this.rateMultiplier, 0, 0);
            }

            Av1BlockSize blockSize = modeInfo.Block.BlockSize;
            int subX = this.source.ChromaSubsamplingX;
            int subY = this.source.ChromaSubsamplingY;
            Point origin = Av1TileWriter.GetChromaBlockOrigin(blockOrigin, subX, subY);
            Av1BlockSize planeSize = blockSize.GetSubsampled(subX != 0, subY != 0);
            Av1TransformSize transformSize = blockSize.GetMaxUvTransformSize(subX != 0, subY != 0);
            Av1BlockSize maximumUnit = Av1BlockSize.Block64x64.GetSubsampled(subX != 0, subY != 0);
            Size extent = GetCodedTransformExtent(macroBlock, planeSize, transformSize, subX, subY);
            int width = planeSize.GetWidth();
            int sampleCount = width * planeSize.GetHeight();
            int contextWidth = planeSize.Get4x4WideCount();
            int contextHeight = planeSize.Get4x4HighCount();
            int transformCount = sampleCount / transformSize.GetSize2d();
            Av1EncoderModeDecisionWorkspace<TSample> workspace = this.blockWorkspace.GetModeDecisionWorkspace<TSample>();
            bool usesChromaFromLuma = modeInfo.Block.UvMode == Av1ChromaPredictionMode.ChromaFromLuma;

            // Chroma-from-luma predicts from the luma of the chroma mode search, not from the refined luma: the
            // reference stores luma for it only before that search. Reference: refine_winner_mode_tx(), whose
            // av1_txfm_uvrd() predicts from the stored chroma-from-luma buffer.
            ReadOnlySpan<short> lumaQ3 = usesChromaFromLuma ? this.blockWorkspace.ChromaFromLumaSearchSamples : default;

            // The refined rate replaces the searched chroma rate, alpha included, by the coefficient rate alone.
            // Reference: the winner_rate_uv that refine_winner_mode_tx() removes is the token-only rate of
            // av1_rd_pick_intra_sbuv_mode(), which counts the alpha cost, and av1_txfm_uvrd() counts coefficients only.
            int rate = previousStatistics.Rate - previousStatistics.ResidualRate;
            int residualRate = 0;

            long distortion = 0;
            bool hasCoefficients = false;

            // Both chroma planes read the same rate tables and workspace buffers.
            Av1CoefficientTables tables = writer.GetCoefficientTables();
            Span<int> blockTransformCoefficients = this.blockWorkspace.TransformCoefficients;
            Span<int> blockDequantizedCoefficients = this.blockWorkspace.DequantizedCoefficients;
            Span<int> blockTransformWorkspace = this.blockWorkspace.TransformWorkspace;
            ReadOnlySpan<int> transformTypeProbabilities = this.blockWorkspace.TransformTypeProbabilities;
            Span<short> blockResidual = this.blockWorkspace.Residual;
            Span<int> searchCoefficients = this.blockWorkspace.SearchCoefficients;
            Span<int> searchDequantizedCoefficients = this.blockWorkspace.SearchDequantizedCoefficients;
            Span<int> searchReconstructions = this.blockWorkspace.SearchReconstructions;
            for (int planeIndex = 1; planeIndex < 3; planeIndex++)
            {
                Av1Plane plane = (Av1Plane)planeIndex;
                Av1PlaneRegion<TSample> source = this.source.GetPlane(plane);

                Av1PlaneRegion<TSample> reconstruction = this.reconstruction.GetPlane(plane);
                Span<TSample> samples = workspace.GetCandidateReconstruction(planeIndex - 1)[..sampleCount];
                Span<int> coefficients = workspace.GetCandidateCoefficients(planeIndex - 1)[..sampleCount];
                Span<Av1EncoderTransformBlockState> states = workspace.CandidateTransformBlocks[..transformCount];
                Span<byte> topContexts = workspace.TransformContexts[..contextWidth];
                Span<byte> leftContexts = workspace.TransformContexts.Slice(contextWidth, contextHeight);

                Av1NeighborEdges<byte> neighbors = plane == Av1Plane.U
                    ? this.picture.CbDcSignLevelCoefficientNeighbors[tileIndex].GetEdges()
                    : this.picture.CrDcSignLevelCoefficientNeighbors[tileIndex].GetEdges();

                neighbors.Top.Slice(neighbors.GetTopIndex(origin), contextWidth).CopyTo(topContexts);
                neighbors.Left.Slice(neighbors.GetLeftIndex(origin), contextHeight).CopyTo(leftContexts);
                int planeRate;
                if (usesChromaFromLuma)
                {
                    // CfL is a single chroma transform. Its centered luma remains in transient storage;
                    // DC prediction uses candidate pixels so it cannot overwrite those luma samples.
                    Span<TSample> above = workspace.GetReferenceSamples(0);

                    Span<TSample> left = workspace.GetReferenceSamples(1);
                    this.PrepareTransformReferenceSamples(
                        Av1TransformBlockEncoder.GetPlaneSpan(reconstruction, origin),
                        reconstruction.Stride,
                        blockOrigin,
                        blockSize,
                        macroBlock,
                        macroBlock.GetRelativeModeInfo(0).Block.PartitionType,
                        0,
                        0,
                        width,
                        transformSize,
                        subX,
                        subY,
                        samples,
                        above,
                        left,
                        out bool hasLeft,
                        out bool hasAbove);

                    int edgeCount = transformSize.GetWidth() + transformSize.GetHeight();
                    TOperator.PrepareChromaFromLumaDc(
                        samples,
                        above.Slice(1, edgeCount),
                        left.Slice(1, edgeCount),
                        hasLeft,
                        hasAbove,
                        transformSize,
                        this.bitDepth);

                    TSample dc = samples[0];
                    int jointSign = block.PredictionUnit.ChromaFromLumaSigns;
                    int packedIndex = block.PredictionUnit.ChromaFromLumaIndex;
                    int sign = plane == Av1Plane.U
                        ? Av1ChromaFromLumaMath.SignU(jointSign)
                        : Av1ChromaFromLumaMath.SignV(jointSign);

                    int magnitude = plane == Av1Plane.U
                        ? Av1ChromaFromLumaMath.IndexU(packedIndex)
                        : Av1ChromaFromLumaMath.IndexV(packedIndex);

                    int alpha = sign == Av1ChromaFromLumaMath.SignZero
                        ? 0
                        : (magnitude + 1) * (sign == Av1ChromaFromLumaMath.SignNegative ? -1 : 1);

                    Av1TransformBlockContext context = Av1TileWriter.GetTransformBlockContexts(
                        Av1ComponentType.Chroma, topContexts, leftContexts, planeSize, transformSize);

                    states[0] = default;
                    distortion += this.GetChromaFromLumaPlaneCost(
                        writer,
                        in tables,
                        blockTransformCoefficients,
                        blockDequantizedCoefficients,
                        blockTransformWorkspace,
                        transformTypeProbabilities,
                        blockResidual,
                        searchCoefficients,
                        searchDequantizedCoefficients,
                        searchReconstructions,
                        modeInfo.Block.Mode,
                        plane,
                        origin,
                        transformSize,
                        source,
                        dc,
                        context,
                        lumaQ3,
                        alpha,
                        samples,
                        reconstruction,
                        coefficients,
                        ref states[0],
                        out planeRate);
                }
                else
                {
                    int paletteSize = paletteInfo.PaletteSizes[(int)Av1PlaneType.Uv];
                    ReadOnlySpan<ushort> colors = paletteSize == 0 ? [] : paletteInfo.GetColors(plane)[..paletteSize];
                    Av1PlaneRegion<byte> map = paletteSize == 0
                        ? default
                        : this.superblock.Workspace.GetPaletteMaps().GetMap(Av1PlaneType.Uv, width, planeSize.GetHeight());

                    distortion += this.GetTiledPlaneCost(
                        writer,
                        in tables,
                        in workspace,
                        blockTransformCoefficients,
                        blockDequantizedCoefficients,
                        blockTransformWorkspace,
                        transformTypeProbabilities,
                        searchCoefficients,
                        searchDequantizedCoefficients,
                        searchReconstructions,
                        macroBlock,
                        blockOrigin,
                        origin,
                        blockSize,
                        planeSize,
                        transformSize,
                        maximumUnit,
                        subX,
                        subY,
                        modeInfo.Block.Mode,
                        modeInfo.Block.UvMode.ToLumaMode(),
                        block.PredictionUnit.AngleDelta[(int)Av1PlaneType.Uv],
                        plane,
                        source,
                        source.Samples,
                        reconstruction,
                        reconstruction.Samples,
                        colors,
                        map,
                        samples,
                        coefficients,
                        states,
                        topContexts,
                        leftContexts,
                        long.MaxValue,
                        out _,
                        out planeRate);
                }

                residualRate += planeRate;
                int activeTransforms = extent.Width * extent.Height / transformSize.GetSize2d();

                for (int index = 0; index < activeTransforms; index++)
                {
                    hasCoefficients |= states[index].EndOfBlock != 0;
                }

                Span<Av1EncoderTransformBlockState> committedStates = this.coefficientBuffer.GetTransformBlockSpan(this.superblock.Index, plane)[
                    (this.codedAreaChroma / Av1EncoderCoefficientBuffer.TransformBlockUnitCoefficientCount)..];

                CopyTiledCandidate(states, extent, transformSize, committedStates);
            }

            return new(this.rateMultiplier, rate + residualRate, distortion)
            {
                ResidualRate = residualRate,
                HasCoefficients = hasCoefficients
            };
        }

        private Av1ChromaPredictionMode SelectChromaMode(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Av1MacroBlockModeInfo modeInfo,
            Point lumaOrigin,
            Point chromaOrigin,
            Av1BlockSize blockSize,
            ushort tileIndex,
            Av1PredictionMode lumaMode,
            Av1TransformSize transformSize,
            Span<Av1EncoderTransformBlockState> retainedBlueStates,
            Span<Av1EncoderTransformBlockState> retainedRedStates,
            ref Av1EncoderPaletteInfo paletteInfo,
            out int selectedAngleDelta,
            out byte selectedChromaFromLumaIndex,
            out sbyte selectedChromaFromLumaSigns,
            out Av1RateDistortionStatistics selectedStatistics)
        {
            return this.SelectChromaModeCore(
                writer,
                macroBlock,
                modeInfo,
                lumaOrigin,
                chromaOrigin,
                blockSize,
                tileIndex,
                lumaMode,
                transformSize,
                retainedBlueStates,
                retainedRedStates,
                ref paletteInfo,
                out selectedAngleDelta,
                out selectedChromaFromLumaIndex,
                out selectedChromaFromLumaSigns,
                out selectedStatistics);
        }

        private Av1ChromaPredictionMode SelectChromaModeCore(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Av1MacroBlockModeInfo modeInfo,
            Point lumaOrigin,
            Point chromaOrigin,
            Av1BlockSize blockSize,
            ushort tileIndex,
            Av1PredictionMode lumaMode,
            Av1TransformSize transformSize,
            Span<Av1EncoderTransformBlockState> retainedBlueStates,
            Span<Av1EncoderTransformBlockState> retainedRedStates,
            ref Av1EncoderPaletteInfo paletteInfo,
            out int selectedAngleDelta,
            out byte selectedChromaFromLumaIndex,
            out sbyte selectedChromaFromLumaSigns,
            out Av1RateDistortionStatistics selectedStatistics)
        {
            Av1ChromaPredictionMode mode = this.SelectChromaPrediction(
                writer,
                macroBlock,
                modeInfo,
                lumaOrigin,
                chromaOrigin,
                blockSize,
                tileIndex,
                lumaMode,
                transformSize,
                retainedBlueStates,
                retainedRedStates,
                ref paletteInfo,
                out selectedAngleDelta,
                out selectedChromaFromLumaIndex,
                out selectedChromaFromLumaSigns,
                out selectedStatistics);

            // Palette covers the prediction block, independently of how its residual is split into
            // transforms. Evaluate it after ordinary prediction for both single and multiple transforms.
            if (Av1TileWriter.IsPaletteAllowed(this.picture.Parent.FrameHeader.AllowScreenContentTools, blockSize) &&
                this.SelectChromaPalette(
                    writer,
                    macroBlock,
                    modeInfo,
                    lumaOrigin,
                    chromaOrigin,
                    tileIndex,
                    lumaMode,
                    transformSize,
                    retainedBlueStates,
                    retainedRedStates,
                    ref selectedStatistics,
                    ref paletteInfo))
            {
                mode = Av1ChromaPredictionMode.DC;
                selectedAngleDelta = 0;
                selectedChromaFromLumaIndex = 0;
                selectedChromaFromLumaSigns = 0;
            }

            // Retained states use one entry per 4x4 coefficient slot. Only each transform's first
            // entry is live; inspect those entries so unused slots cannot mark an empty residual as coded.
            if (selectedStatistics.Cost != long.MaxValue)
            {
                int subX = this.source.ChromaSubsamplingX;
                int subY = this.source.ChromaSubsamplingY;
                Av1BlockSize planeSize = blockSize.GetSubsampled(subX != 0, subY != 0);
                Size extent = GetCodedTransformExtent(macroBlock, planeSize, transformSize, subX, subY);
                int stateStride = transformSize.GetSize2d() / Av1EncoderCoefficientBuffer.TransformBlockUnitCoefficientCount;
                int stateCount = extent.Width * extent.Height / Av1EncoderCoefficientBuffer.TransformBlockUnitCoefficientCount;
                for (int index = 0; index < stateCount; index += stateStride)
                {
                    selectedStatistics.HasCoefficients |=
                        retainedBlueStates[index].EndOfBlock != 0 || retainedRedStates[index].EndOfBlock != 0;
                }
            }

            return mode;
        }

        private Av1ChromaPredictionMode SelectChromaPrediction(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Av1MacroBlockModeInfo modeInfo,
            Point lumaOrigin,
            Point chromaOrigin,
            Av1BlockSize blockSize,
            ushort tileIndex,
            Av1PredictionMode lumaMode,
            Av1TransformSize transformSize,
            Span<Av1EncoderTransformBlockState> retainedBlueStates,
            Span<Av1EncoderTransformBlockState> retainedRedStates,
            ref Av1EncoderPaletteInfo paletteInfo,
            out int selectedAngleDelta,
            out byte selectedChromaFromLumaIndex,
            out sbyte selectedChromaFromLumaSigns,
            out Av1RateDistortionStatistics selectedStatistics)
        {
            return this.SelectChromaPredictionCore(
                writer,
                macroBlock,
                modeInfo,
                lumaOrigin,
                chromaOrigin,
                blockSize,
                tileIndex,
                lumaMode,
                transformSize,
                retainedBlueStates,
                retainedRedStates,
                ref paletteInfo,
                out selectedAngleDelta,
                out selectedChromaFromLumaIndex,
                out selectedChromaFromLumaSigns,
                out selectedStatistics);
        }

        private Av1ChromaPredictionMode SelectChromaPredictionCore(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Av1MacroBlockModeInfo modeInfo,
            Point lumaOrigin,
            Point chromaOrigin,
            Av1BlockSize blockSize,
            ushort tileIndex,
            Av1PredictionMode lumaMode,
            Av1TransformSize transformSize,
            Span<Av1EncoderTransformBlockState> retainedBlueStates,
            Span<Av1EncoderTransformBlockState> retainedRedStates,
            ref Av1EncoderPaletteInfo paletteInfo,
            out int selectedAngleDelta,
            out byte selectedChromaFromLumaIndex,
            out sbyte selectedChromaFromLumaSigns,
            out Av1RateDistortionStatistics selectedStatistics)
        {
            Av1EncoderModeDecisionWorkspace<TSample> workspace =
                this.blockWorkspace.GetModeDecisionWorkspace<TSample>();

            ObuColorConfig colorConfig = this.picture.Sequence.SequenceHeader.ColorConfig;
            int subsamplingX = colorConfig.SubSamplingX ? 1 : 0;
            int subsamplingY = colorConfig.SubSamplingY ? 1 : 0;
            int width = transformSize.GetWidth();
            int height = transformSize.GetHeight();
            int sampleCount = transformSize.GetSize2d();
            Av1BlockSize chromaBlockSize = blockSize.GetSubsampled(
                colorConfig.SubSamplingX,
                colorConfig.SubSamplingY);

            if (chromaBlockSize.GetWidth() > width || chromaBlockSize.GetHeight() > height)
            {
                return this.SelectTiledChromaMode(
                    writer,
                    macroBlock,
                    modeInfo,
                    lumaOrigin,
                    chromaOrigin,
                    blockSize,
                    chromaBlockSize,
                    tileIndex,
                    lumaMode,
                    transformSize,
                    retainedBlueStates,
                    retainedRedStates,
                    paletteInfo,
                    out selectedAngleDelta,
                    out selectedChromaFromLumaIndex,
                    out selectedChromaFromLumaSigns,
                    out selectedStatistics);
            }

            int modeInfoRow = lumaOrigin.Y >> Av1Constants.ModeInfoSizeLog2;
            int modeInfoColumn = lumaOrigin.X >> Av1Constants.ModeInfoSizeLog2;
            bool hasLeft = macroBlock.IsLeftAvailable;
            bool hasAbove = macroBlock.IsUpAvailable;

            // Subsampled chroma belongs to the bottom-right luma unit in its shared 8x8 region. Its external
            // references therefore begin before that region, not immediately beside the owning 4x4 luma block.
            if (subsamplingX != 0 && blockSize.Get4x4WideCount() < Av1BlockSize.Block8x8.Get4x4WideCount())
            {
                hasLeft = modeInfoColumn - 1 > macroBlock.Tile.ModeInfoColumnStart;
            }

            if (subsamplingY != 0 && blockSize.Get4x4HighCount() < Av1BlockSize.Block8x8.Get4x4HighCount())
            {
                hasAbove = modeInfoRow - 1 > macroBlock.Tile.ModeInfoRowStart;
            }

            bool rightAvailable = modeInfoColumn + (transformSize.Get4x4WideCount() << subsamplingX) < macroBlock.Tile.ModeInfoColumnEnd;

            // Samples below the transform exist only while coded rows remain below it.
            int rowsBelow = (macroBlock.ToBottomEdge >> (3 + subsamplingY)) + chromaBlockSize.GetHeight() - height;
            bool bottomAvailable = rowsBelow > 0 &&
                modeInfoRow + (transformSize.Get4x4HighCount() << subsamplingY) < macroBlock.Tile.ModeInfoRowEnd;

            Av1PartitionType partitionType = modeInfo.Block.PartitionType;

            // Availability tables describe prediction blocks. Subsampled chroma of a luma block narrower or
            // shorter than eight samples belongs to the enclosing 8x8 region, so its geometry uses that region.
            Av1BlockSize availabilityBlockSize = Av1IntraReferenceAvailability.ScaleChromaBlockSize(
                blockSize, subsamplingX != 0, subsamplingY != 0);

            bool hasTopRight = Av1IntraReferenceAvailability.HasTopRight(
                this.picture.Sequence.SequenceHeader.SuperblockSize,
                availabilityBlockSize,
                modeInfoRow,
                modeInfoColumn,
                hasAbove,
                rightAvailable,
                partitionType,
                transformSize,
                0,
                0,
                subsamplingX,
                subsamplingY);

            bool hasBottomLeft = Av1IntraReferenceAvailability.HasBottomLeft(
                this.picture.Sequence.SequenceHeader.SuperblockSize,
                availabilityBlockSize,
                modeInfoRow,
                modeInfoColumn,
                bottomAvailable,
                hasLeft,
                partitionType,
                transformSize,
                0,
                0,
                subsamplingX,
                subsamplingY);

            Av1PlaneRegion<TSample> blueSource = this.source.GetPlane(Av1Plane.U);
            Av1PlaneRegion<TSample> redSource = this.source.GetPlane(Av1Plane.V);
            Av1PlaneRegion<TSample> blueReconstruction = this.reconstruction.GetPlane(Av1Plane.U);
            Av1PlaneRegion<TSample> redReconstruction = this.reconstruction.GetPlane(Av1Plane.V);

            // Every chroma candidate reads the same source blocks and writes the same frame planes, so they are read once.
            ReadOnlySpan<TSample> blueSourceBlock = Av1TransformBlockEncoder.GetPlaneSpan(blueSource, chromaOrigin);
            ReadOnlySpan<TSample> redSourceBlock = Av1TransformBlockEncoder.GetPlaneSpan(redSource, chromaOrigin);
            Span<TSample> blueFrame = blueReconstruction.Samples;
            Span<TSample> redFrame = redReconstruction.Samples;
            bool smoothChromaEdges = this.UseSmoothIntraEdges(macroBlock, lumaOrigin, blockSize, Av1Plane.U);
            Span<TSample> blueAboveStorage = workspace.GetReferenceSamples(0);
            Span<TSample> blueLeftStorage = workspace.GetReferenceSamples(1);
            Span<TSample> redAboveStorage = workspace.GetReferenceSamples(2);
            Span<TSample> redLeftStorage = workspace.GetReferenceSamples(3);
            PrepareReferenceSamples(
                blueReconstruction,
                chromaOrigin,
                width,
                height,
                hasLeft,
                hasAbove,
                hasTopRight,
                hasBottomLeft,
                this.bitDepth,
                blueAboveStorage,
                blueLeftStorage);

            PrepareReferenceSamples(
                redReconstruction,
                chromaOrigin,
                width,
                height,
                hasLeft,
                hasAbove,
                hasTopRight,
                hasBottomLeft,
                this.bitDepth,
                redAboveStorage,
                redLeftStorage);

            ReadOnlySpan<TSample> blueAbove = blueAboveStorage.Slice(1, width + height);
            ReadOnlySpan<TSample> blueLeft = blueLeftStorage.Slice(1, width + height);
            ReadOnlySpan<TSample> redAbove = redAboveStorage.Slice(1, width + height);
            ReadOnlySpan<TSample> redLeft = redLeftStorage.Slice(1, width + height);
            Av1NeighborEdges<byte> blueEdges = this.picture.CbDcSignLevelCoefficientNeighbors[tileIndex].GetEdges();
            Av1NeighborEdges<byte> redEdges = this.picture.CrDcSignLevelCoefficientNeighbors[tileIndex].GetEdges();
            Av1TransformBlockContext blueContext = Av1TileWriter.GetTransformBlockContexts(
                Av1ComponentType.Chroma,
                in blueEdges,
                chromaOrigin,
                chromaBlockSize,
                transformSize);

            Av1TransformBlockContext redContext = Av1TileWriter.GetTransformBlockContexts(
                Av1ComponentType.Chroma,
                in redEdges,
                chromaOrigin,
                chromaBlockSize,
                transformSize);

            Span<TSample> candidateBlueReconstruction = workspace.GetCandidateReconstruction(0);
            Span<TSample> candidateRedReconstruction = workspace.GetCandidateReconstruction(1);
            Span<int> candidateBlueCoefficients = workspace.GetCandidateCoefficients(0);
            Span<int> candidateRedCoefficients = workspace.GetCandidateCoefficients(1);
            Av1RateDistortionStatistics bestStatistics = Av1RateDistortionStatistics.Invalid;
            Av1ChromaPredictionMode bestMode = Av1ChromaPredictionMode.DC;
            selectedAngleDelta = 0;
            selectedChromaFromLumaIndex = 0;
            selectedChromaFromLumaSigns = 0;
            Av1EncoderSpeedSettings speedSettings = this.picture.Parent.SpeedSettings;
            int hogLevel = speedSettings.ChromaHogPruningLevel;
            float hogThreshold = this.picture.Parent.FrameHeader.IsIntra
                ? hogLevel == 4 ? 0.4F : hogLevel == 3 ? -0.6F : -1.2F
                : hogLevel == 4 ? 1.2F : hogLevel == 1 ? -1.2F : 0F;

            ushort chromaModeMask = speedSettings.GetChromaModeMask(
                blockSize.GetMaxUvTransformSize(colorConfig.SubSamplingX, colorConfig.SubSamplingY));

            // Directional evidence excludes the replicated border, unlike block variance. Chroma's
            // normalized histogram is scaled by its sample area before evaluating the directional scores.
            byte directionalModeSkipMask = hogLevel != 0
                ? GetDirectionalModeSkipMask(
                    blueSource,
                    chromaOrigin,
                    (blockSize.GetHeight() + (Math.Min(0, macroBlock.ToBottomEdge) >> 3)) >> subsamplingY,
                    (blockSize.GetWidth() + (Math.Min(0, macroBlock.ToRightEdge) >> 3)) >> subsamplingX,
                    (1 + subsamplingX) * (1 + subsamplingY),
                    hogThreshold)
                : (byte)0;

            // Suppress smooth prediction only when both chroma planes have per-pixel variance below 20.
            // Variance is normalized to eight-bit precision after accumulating the full-precision differences.
            bool pruneSmooth = speedSettings.PruneChromaSmoothByVariance &&
                GetSourceVariance(blueSource, chromaOrigin, width, height, this.bitDepth) < 20 &&
                GetSourceVariance(redSource, chromaOrigin, width, height, this.bitDepth) < 20;

            // The rate tables and the workspace buffers of every chroma candidate of the block, read once.
            Av1CoefficientTables tables = writer.GetCoefficientTables();
            Av1ModeCosts modeCosts = tables.ModeCosts;
            Span<int> blockTransformCoefficients = this.blockWorkspace.TransformCoefficients;
            Span<int> blockDequantizedCoefficients = this.blockWorkspace.DequantizedCoefficients;
            Span<int> blockTransformWorkspace = this.blockWorkspace.TransformWorkspace;
            ReadOnlySpan<int> transformTypeProbabilities = this.blockWorkspace.TransformTypeProbabilities;
            Span<short> blockResidual = this.blockWorkspace.Residual;
            Span<int> searchCoefficients = this.blockWorkspace.SearchCoefficients;
            Span<int> searchDequantizedCoefficients = this.blockWorkspace.SearchDequantizedCoefficients;
            Span<int> searchReconstructions = this.blockWorkspace.SearchReconstructions;

            bool hasLumaPalette = paletteInfo.PaletteSizes[0] != 0;
            int paletteDisabledCost = Av1TileWriter.IsPaletteAllowed(
                this.picture.Parent.FrameHeader.AllowScreenContentTools,
                blockSize)
                ? Av1SymbolEncoder.GetPaletteUvModeCost(modeCosts, false, hasLumaPalette)
                : 0;

            bool chromaFromLumaAllowed = blockSize.AllowsChromaFromLuma(
                this.picture.Parent.FrameHeader.LosslessArray[modeInfo.Block.SegmentId],
                colorConfig.SubSamplingX,
                colorConfig.SubSamplingY);

            Span<long> angleCosts = stackalloc long[7];

            // Every spatial chroma candidate of the block uses the same planes, references, contexts and buffers.
            // Only the mode, the angle and the transform type change, so each plane is described once here.
            Av1IntraCandidatePlane<TSample> bluePlane = new()
            {
                Workspace = this.blockWorkspace,
                Writer = writer,
                Tables = tables,
                TransformCoefficients = blockTransformCoefficients,
                DequantizedCoefficients = blockDequantizedCoefficients,
                TransformWorkspace = blockTransformWorkspace,
                Context = blueContext,
                RateMultiplier = this.rateMultiplier,
                UseChromaWeights = this.picture.Sequence.SequenceHeader.IsStillPicture,
                Source = blueSourceBlock,
                SourceStride = blueSource.Stride,
                BlockOrigin = chromaOrigin,
                Reconstruction = candidateBlueReconstruction[..sampleCount],
                Frame = blueReconstruction,
                FrameSamples = blueFrame,
                Above = blueAbove,
                Left = blueLeft,
                HasLeft = hasLeft,
                HasAbove = hasAbove,
                EnableIntraEdgeFilter = this.picture.Sequence.SequenceHeader.EnableIntraEdgeFilter,
                SmoothIntraEdges = smoothChromaEdges,
                QuantizedCoefficients = candidateBlueCoefficients[..sampleCount],
                TransformSize = transformSize,
                Plane = Av1Plane.U,
                QIndex = this.blockQIndex,
                DcDeltaQ = this.quantization.DeltaQDc[(int)Av1Plane.U],
                AcDeltaQ = this.quantization.DeltaQAc[(int)Av1Plane.U],
                BitDepth = this.bitDepth,
                DistortionPolicy = Av1TransformBlockEncoder.GetDistortionPolicy(this.blockWorkspace, speedSettings),
                Residual = blockResidual
            };

            Av1IntraCandidatePlane<TSample> redPlane = new()
            {
                Workspace = this.blockWorkspace,
                Writer = writer,
                Tables = tables,
                TransformCoefficients = blockTransformCoefficients,
                DequantizedCoefficients = blockDequantizedCoefficients,
                TransformWorkspace = blockTransformWorkspace,
                Context = redContext,
                RateMultiplier = this.rateMultiplier,
                UseChromaWeights = bluePlane.UseChromaWeights,
                Source = redSourceBlock,
                SourceStride = redSource.Stride,
                BlockOrigin = chromaOrigin,
                Reconstruction = candidateRedReconstruction[..sampleCount],
                Frame = redReconstruction,
                FrameSamples = redFrame,
                Above = redAbove,
                Left = redLeft,
                HasLeft = hasLeft,
                HasAbove = hasAbove,
                EnableIntraEdgeFilter = bluePlane.EnableIntraEdgeFilter,
                SmoothIntraEdges = smoothChromaEdges,
                QuantizedCoefficients = candidateRedCoefficients[..sampleCount],
                TransformSize = transformSize,
                Plane = Av1Plane.V,
                QIndex = this.blockQIndex,
                DcDeltaQ = this.quantization.DeltaQDc[(int)Av1Plane.V],
                AcDeltaQ = this.quantization.DeltaQAc[(int)Av1Plane.V],
                BitDepth = this.bitDepth,
                DistortionPolicy = bluePlane.DistortionPolicy,
                Residual = bluePlane.Residual
            };

            for (int modeIndex = 0; modeIndex < ChromaModeSearchOrder.Length; modeIndex++)
            {
                // Chroma-from-luma follows DC so its complete cost bounds the remaining spatial modes.
                if (modeIndex == 1)
                {
                    if (chromaFromLumaAllowed &&
                        (chromaModeMask & (1 << (int)Av1ChromaPredictionMode.ChromaFromLuma)) != 0 &&
                        Av1RateDistortion.GetCost(
                            this.rateMultiplier,
                            Av1SymbolEncoder.GetChromaModeCost(modeCosts, Av1ChromaPredictionMode.ChromaFromLuma, true, lumaMode),
                            0) <= bestStatistics.Cost)
                    {
                        Span<short> lumaQ3 = workspace.ChromaFromLumaSamples;
                        Point chromaLumaOrigin = new(
                            chromaOrigin.X << subsamplingX,
                            chromaOrigin.Y << subsamplingY);

                        // CfL consumes the complete luma region represented by this chroma block, which starts before
                        // the bottom-right ownership point for subsampled 4x4 luma leaves.
                        Av1PlaneRegion<TSample> lumaReconstruction = this.reconstruction.GetPlane(Av1Plane.Y);
                        TOperator.PrepareChromaFromLuma(
                            Av1TransformBlockEncoder.GetPlaneSpan(lumaReconstruction, chromaLumaOrigin),
                            lumaReconstruction.Stride,
                            lumaQ3,
                            transformSize,
                            this.GetChromaFromLumaExtent(
                                macroBlock, lumaOrigin, blockSize, modeInfo.Block.TransformSize, subsamplingX, subsamplingY),
                            colorConfig.SubSamplingX,
                            colorConfig.SubSamplingY);

                        // The winner refinement of an intra block in an inter frame predicts from this luma again.
                        lumaQ3.CopyTo(this.blockWorkspace.ChromaFromLumaSearchSamples);

                        // Every alpha candidate uses the same constant DC predictor, so compute each plane once and
                        // refill the candidate block from its sample instead of rebuilding the identical edge average.
                        TOperator.PrepareChromaFromLumaDc(
                            candidateBlueReconstruction[..sampleCount],
                            blueAbove,
                            blueLeft,
                            hasLeft,
                            hasAbove,
                            transformSize,
                            this.bitDepth);

                        TSample blueDc = candidateBlueReconstruction[0];
                        TOperator.PrepareChromaFromLumaDc(
                            candidateRedReconstruction[..sampleCount],
                            redAbove,
                            redLeft,
                            hasLeft,
                            hasAbove,
                            transformSize,
                            this.bitDepth);

                        TSample redDc = candidateRedReconstruction[0];
                        Span<int> blueRates = workspace.GetChromaFromLumaRates(0);
                        Span<int> redRates = workspace.GetChromaFromLumaRates(1);
                        Span<long> blueDistortions = workspace.GetChromaFromLumaDistortions(0);
                        Span<long> redDistortions = workspace.GetChromaFromLumaDistortions(1);
                        int estimatedBlueCandidate = speedSettings.ChromaFromLumaSearchRange == Av1ChromaFromLumaMath.AlphaCandidateCount
                            ? Av1ChromaFromLumaMath.AlphaZeroIndex
                            : FindBestChromaFromLumaEstimate(
                            this.blockWorkspace,
                            Av1Plane.U,
                            blueSource,
                            chromaOrigin,
                            blueDc,
                            lumaQ3,
                            transformSize,
                            this.bitDepth,
                            candidateBlueReconstruction,
                            blueReconstruction,
                            workspace.Residual,
                            blockTransformCoefficients,
                            blockTransformWorkspace);

                        int estimatedRedCandidate = speedSettings.ChromaFromLumaSearchRange == Av1ChromaFromLumaMath.AlphaCandidateCount
                            ? Av1ChromaFromLumaMath.AlphaZeroIndex
                            : FindBestChromaFromLumaEstimate(
                            this.blockWorkspace,
                            Av1Plane.V,
                            redSource,
                            chromaOrigin,
                            redDc,
                            lumaQ3,
                            transformSize,
                            this.bitDepth,
                            candidateRedReconstruction,
                            redReconstruction,
                            workspace.Residual,
                            blockTransformCoefficients,
                            blockTransformWorkspace);

                        // Estimate each signed alpha from transform energy, then code only the nearby candidates.
                        // The two planes refine independently before their syntax costs are combined.
                        bool evaluateAlpha = true;
                        if (speedSettings.ChromaFromLumaSearchRange == 1)
                        {
                            int alphaU = Av1ChromaFromLumaMath.CandidateIndexToAlpha(estimatedBlueCandidate);
                            int alphaV = Av1ChromaFromLumaMath.CandidateIndexToAlpha(estimatedRedCandidate);
                            evaluateAlpha = alphaU != 0 || alphaV != 0;
                            if (evaluateAlpha)
                            {
                                int jointSign = Av1ChromaFromLumaMath.JointSign(
                                    Av1ChromaFromLumaMath.AlphaToSign(alphaU), Av1ChromaFromLumaMath.AlphaToSign(alphaV));

                                int packedIndex = Av1ChromaFromLumaMath.PackIndices(
                                    Av1ChromaFromLumaMath.AlphaToMagnitudeIndex(alphaU), Av1ChromaFromLumaMath.AlphaToMagnitudeIndex(alphaV));

                                int headerRate = Av1SymbolEncoder.GetChromaModeCost(modeCosts, Av1ChromaPredictionMode.ChromaFromLuma, true, lumaMode) +
                                    Av1SymbolEncoder.GetChromaFromLumaCost(modeCosts, packedIndex, jointSign);

                                evaluateAlpha = Av1RateDistortion.GetCost(this.rateMultiplier, headerRate, 0) <= bestStatistics.Cost;
                            }
                        }

                        int radius = speedSettings.ChromaFromLumaSearchRange - 1;
                        int firstBlueCandidate = Math.Max(0, estimatedBlueCandidate - radius);
                        int lastBlueCandidate = Math.Min(Av1ChromaFromLumaMath.AlphaCandidateCount, estimatedBlueCandidate + radius + 1);
                        int firstRedCandidate = Math.Max(0, estimatedRedCandidate - radius);
                        int lastRedCandidate = Math.Min(Av1ChromaFromLumaMath.AlphaCandidateCount, estimatedRedCandidate + radius + 1);

                        // Each plane codes the estimate first, then the candidates above it, then the candidates below it.
                        // Each trial writes its prediction into the frame in that order. Reference: cfl_pick_plane_rd().
                        for (int order = 0; evaluateAlpha && order <= 2 * radius; order++)
                        {
                            int alphaCandidateIndex = order <= radius ? estimatedBlueCandidate + order : estimatedBlueCandidate - (order - radius);
                            if (alphaCandidateIndex < firstBlueCandidate || alphaCandidateIndex >= lastBlueCandidate)
                            {
                                continue;
                            }

                            Av1EncoderTransformBlockState candidateBlueState = default;
                            blueDistortions[alphaCandidateIndex] = this.GetChromaFromLumaPlaneCost(
                                writer,
                                in tables,
                                blockTransformCoefficients,
                                blockDequantizedCoefficients,
                                blockTransformWorkspace,
                                transformTypeProbabilities,
                                blockResidual,
                                searchCoefficients,
                                searchDequantizedCoefficients,
                                searchReconstructions,
                                lumaMode,
                                Av1Plane.U,
                                chromaOrigin,
                                transformSize,
                                blueSource,
                                blueDc,
                                blueContext,
                                lumaQ3,
                                Av1ChromaFromLumaMath.CandidateIndexToAlpha(alphaCandidateIndex),
                                candidateBlueReconstruction[..sampleCount],
                                blueReconstruction,
                                candidateBlueCoefficients[..sampleCount],
                                ref candidateBlueState,
                                out blueRates[alphaCandidateIndex]);
                        }

                        for (int order = 0; evaluateAlpha && order <= 2 * radius; order++)
                        {
                            int alphaCandidateIndex = order <= radius ? estimatedRedCandidate + order : estimatedRedCandidate - (order - radius);
                            if (alphaCandidateIndex < firstRedCandidate || alphaCandidateIndex >= lastRedCandidate)
                            {
                                continue;
                            }

                            Av1EncoderTransformBlockState candidateRedState = default;
                            redDistortions[alphaCandidateIndex] = this.GetChromaFromLumaPlaneCost(
                                writer,
                                in tables,
                                blockTransformCoefficients,
                                blockDequantizedCoefficients,
                                blockTransformWorkspace,
                                transformTypeProbabilities,
                                blockResidual,
                                searchCoefficients,
                                searchDequantizedCoefficients,
                                searchReconstructions,
                                lumaMode,
                                Av1Plane.V,
                                chromaOrigin,
                                transformSize,
                                redSource,
                                redDc,
                                redContext,
                                lumaQ3,
                                Av1ChromaFromLumaMath.CandidateIndexToAlpha(alphaCandidateIndex),
                                candidateRedReconstruction[..sampleCount],
                                redReconstruction,
                                candidateRedCoefficients[..sampleCount],
                                ref candidateRedState,
                                out redRates[alphaCandidateIndex]);
                        }

                        int chromaFromLumaModeRate = Av1TileWriter.GetChromaModeCost(
                            modeCosts,
                            this.picture.Parent.FrameHeader,
                            colorConfig,
                            modeInfo,
                            blockSize,
                            lumaMode,
                            Av1ChromaPredictionMode.ChromaFromLuma,
                            0);

                        bool chromaFromLumaSelected = false;
                        int selectedBlueCandidateIndex = 0;
                        int selectedRedCandidateIndex = 0;
                        Av1RateDistortionStatistics bestAlphaStatistics = Av1RateDistortionStatistics.Invalid;
                        int bestAlphaBlueCandidateIndex = 0;
                        int bestAlphaRedCandidateIndex = 0;
                        int bestAlphaPackedIndex = 0;
                        int bestAlphaJointSign = 0;
                        for (int blueCandidateIndex = firstBlueCandidate;
                            evaluateAlpha && blueCandidateIndex < lastBlueCandidate;
                            blueCandidateIndex++)
                        {
                            int alphaU = Av1ChromaFromLumaMath.CandidateIndexToAlpha(blueCandidateIndex);
                            int signU = Av1ChromaFromLumaMath.AlphaToSign(alphaU);
                            int indexU = Av1ChromaFromLumaMath.AlphaToMagnitudeIndex(alphaU);
                            for (int redCandidateIndex = firstRedCandidate; redCandidateIndex < lastRedCandidate; redCandidateIndex++)
                            {
                                int alphaV = Av1ChromaFromLumaMath.CandidateIndexToAlpha(redCandidateIndex);
                                int signV = Av1ChromaFromLumaMath.AlphaToSign(alphaV);
                                if (signU == Av1ChromaFromLumaMath.SignZero && signV == Av1ChromaFromLumaMath.SignZero)
                                {
                                    continue;
                                }

                                int indexV = Av1ChromaFromLumaMath.AlphaToMagnitudeIndex(alphaV);
                                int jointSign = Av1ChromaFromLumaMath.JointSign(signU, signV);
                                int packedIndex = Av1ChromaFromLumaMath.PackIndices(indexU, indexV);

                                // Alpha syntax is part of the CfL residual decision; the UV mode symbol is separate.
                                int residualRate = blueRates[blueCandidateIndex] + redRates[redCandidateIndex] +
                                    Av1SymbolEncoder.GetChromaFromLumaCost(modeCosts, packedIndex, jointSign);

                                long distortion = blueDistortions[blueCandidateIndex] + redDistortions[redCandidateIndex];

                                // The alpha pair is chosen on its own cost, without the UV mode symbol, so rounding
                                // ties resolve as in libaom. Reference: the joint loop of cfl_rd_pick_alpha().
                                Av1RateDistortionStatistics alphaStatistics = new(this.rateMultiplier, residualRate, distortion);
                                if (alphaStatistics.Cost < bestAlphaStatistics.Cost)
                                {
                                    bestAlphaStatistics = alphaStatistics;
                                    bestAlphaBlueCandidateIndex = blueCandidateIndex;
                                    bestAlphaRedCandidateIndex = redCandidateIndex;
                                    bestAlphaPackedIndex = packedIndex;
                                    bestAlphaJointSign = jointSign;
                                }
                            }
                        }

                        // The best pair must beat the best mode before the mode symbol is added. Reference: the
                        // ref_best_rd test at the end of cfl_rd_pick_alpha(), then the this_rd test of
                        // av1_rd_pick_intra_sbuv_mode().
                        if (bestAlphaStatistics.Cost < bestStatistics.Cost)
                        {
                            Av1RateDistortionStatistics candidateStatistics = new(
                                this.rateMultiplier,
                                chromaFromLumaModeRate + bestAlphaStatistics.Rate,
                                bestAlphaStatistics.Distortion)
                            {
                                ResidualRate = bestAlphaStatistics.Rate
                            };

                            if (candidateStatistics.Cost < bestStatistics.Cost)
                            {
                                bestStatistics = candidateStatistics;
                                bestMode = Av1ChromaPredictionMode.ChromaFromLuma;
                                selectedAngleDelta = 0;
                                selectedBlueCandidateIndex = bestAlphaBlueCandidateIndex;
                                selectedRedCandidateIndex = bestAlphaRedCandidateIndex;
                                selectedChromaFromLumaIndex = (byte)bestAlphaPackedIndex;
                                selectedChromaFromLumaSigns = (sbyte)bestAlphaJointSign;
                                chromaFromLumaSelected = true;
                            }
                        }

                        if (chromaFromLumaSelected)
                        {
                            // The alpha tables keep only rate and distortion. Code the two selected planes again here to get
                            // their coefficients. This is not a libaom trial, so it does not write the frame.
                            Av1EncoderTransformBlockState candidateBlueState = default;
                            _ = this.GetChromaFromLumaPlaneCost(
                                writer,
                                in tables,
                                blockTransformCoefficients,
                                blockDequantizedCoefficients,
                                blockTransformWorkspace,
                                transformTypeProbabilities,
                                blockResidual,
                                searchCoefficients,
                                searchDequantizedCoefficients,
                                searchReconstructions,
                                lumaMode,
                                Av1Plane.U,
                                chromaOrigin,
                                transformSize,
                                blueSource,
                                blueDc,
                                blueContext,
                                lumaQ3,
                                Av1ChromaFromLumaMath.CandidateIndexToAlpha(selectedBlueCandidateIndex),
                                candidateBlueReconstruction[..sampleCount],
                                default,
                                candidateBlueCoefficients[..sampleCount],
                                ref candidateBlueState,
                                out _);

                            retainedBlueStates[0] = candidateBlueState;

                            Av1EncoderTransformBlockState candidateRedState = default;
                            _ = this.GetChromaFromLumaPlaneCost(
                                writer,
                                in tables,
                                blockTransformCoefficients,
                                blockDequantizedCoefficients,
                                blockTransformWorkspace,
                                transformTypeProbabilities,
                                blockResidual,
                                searchCoefficients,
                                searchDequantizedCoefficients,
                                searchReconstructions,
                                lumaMode,
                                Av1Plane.V,
                                chromaOrigin,
                                transformSize,
                                redSource,
                                redDc,
                                redContext,
                                lumaQ3,
                                Av1ChromaFromLumaMath.CandidateIndexToAlpha(selectedRedCandidateIndex),
                                candidateRedReconstruction[..sampleCount],
                                default,
                                candidateRedCoefficients[..sampleCount],
                                ref candidateRedState,
                                out _);

                            retainedRedStates[0] = candidateRedState;
                        }
                    }
                }

                Av1ChromaPredictionMode chromaMode = ChromaModeSearchOrder[modeIndex];
                if ((chromaModeMask & (1 << (int)chromaMode)) == 0 ||
                    (speedSettings.PruneChromaModesUsingLumaWinner &&
                    !ShouldSearchChromaMode(this.picture.Parent.EncodingSpeed, lumaMode, chromaMode)))
                {
                    continue;
                }

                if (chromaMode == Av1ChromaPredictionMode.Smooth && pruneSmooth)
                {
                    continue;
                }

                if (blockSize >= Av1BlockSize.Block8x8 &&
                    chromaMode is >= Av1ChromaPredictionMode.Vertical and <= Av1ChromaPredictionMode.Directional67Degrees &&
                    (directionalModeSkipMask & (1 << ((int)chromaMode - (int)Av1ChromaPredictionMode.Vertical))) != 0)
                {
                    continue;
                }

                int modeRate = Av1SymbolEncoder.GetChromaModeCost(modeCosts, chromaMode, chromaFromLumaAllowed, lumaMode);
                if (Av1RateDistortion.GetCost(this.rateMultiplier, modeRate, 0) > bestStatistics.Cost)
                {
                    continue;
                }

                bool directional = chromaMode is >= Av1ChromaPredictionMode.Vertical and <= Av1ChromaPredictionMode.Directional67Degrees;
                int angleCount = directional && blockSize >= Av1BlockSize.Block8x8 ? ChromaAngleSearchOrder.Length : 1;
                angleCosts.Fill(long.MaxValue);
                for (int angleIndex = 0; angleIndex < angleCount; angleIndex++)
                {
                    int angleDelta = ChromaAngleSearchOrder[angleIndex];
                    if ((angleDelta & 1) != 0)
                    {
                        long limit = bestStatistics.Cost == long.MaxValue ? long.MaxValue : bestStatistics.Cost + (bestStatistics.Cost >> 5);
                        long lowerCost = angleDelta == -3 ? long.MaxValue : angleCosts[angleDelta + 2];
                        long upperCost = angleDelta == 3 ? long.MaxValue : angleCosts[angleDelta + 4];
                        if (lowerCost > limit && upperCost > limit)
                        {
                            continue;
                        }
                    }

                    long costLimit = bestStatistics.Cost;
                    if (angleCount > 1 && (angleDelta & 1) == 0 && costLimit != long.MaxValue)
                    {
                        costLimit += costLimit >> (angleDelta == 0 ? 3 : 5);
                    }

                    Av1EncoderTransformBlockState candidateBlueState = default;
                    Av1EncoderTransformBlockState candidateRedState = default;

                    // A chroma mode and angle are shared by U and V, so neither plane can replace the
                    // retained result independently. Their complete rate and distortion compete jointly.
                    Av1RateDistortionStatistics candidateStatistics = this.GetChromaCandidateCost(
                        writer,
                        modeInfo,
                        lumaMode,
                        chromaMode,
                        angleDelta,
                        blockSize,
                        in bluePlane,
                        in redPlane,
                        paletteDisabledCost,
                        costLimit,
                        ref candidateBlueState,
                        ref candidateRedState);

                    if (angleDelta == 0 && candidateStatistics.Cost == long.MaxValue)
                    {
                        break;
                    }

                    this.PenalizeSmoothChromaMode(ref candidateStatistics, chromaMode);
                    angleCosts[angleDelta + 3] = candidateStatistics.Cost;
                    if (candidateStatistics.Cost < bestStatistics.Cost)
                    {
                        retainedBlueStates[0] = candidateBlueState;
                        retainedRedStates[0] = candidateRedState;
                        bestStatistics = candidateStatistics;
                        bestMode = chromaMode;
                        selectedAngleDelta = angleDelta;
                        selectedChromaFromLumaIndex = 0;
                        selectedChromaFromLumaSigns = 0;
                    }
                }
            }

            selectedStatistics = bestStatistics;
            return bestMode;
        }

        private Av1ChromaPredictionMode SelectTiledChromaMode(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Av1MacroBlockModeInfo modeInfo,
            Point lumaOrigin,
            Point chromaOrigin,
            Av1BlockSize blockSize,
            Av1BlockSize chromaBlockSize,
            ushort tileIndex,
            Av1PredictionMode lumaMode,
            Av1TransformSize transformSize,
            Span<Av1EncoderTransformBlockState> retainedBlueStates,
            Span<Av1EncoderTransformBlockState> retainedRedStates,
            Av1EncoderPaletteInfo paletteInfo,
            out int selectedAngleDelta,
            out byte selectedChromaFromLumaIndex,
            out sbyte selectedChromaFromLumaSigns,
            out Av1RateDistortionStatistics selectedStatistics)
        {
            Av1EncoderModeDecisionWorkspace<TSample> workspace =
                this.blockWorkspace.GetModeDecisionWorkspace<TSample>();

            ObuColorConfig colorConfig = this.picture.Sequence.SequenceHeader.ColorConfig;
            int subsamplingX = colorConfig.SubSamplingX ? 1 : 0;
            int subsamplingY = colorConfig.SubSamplingY ? 1 : 0;
            int blockWidth = chromaBlockSize.GetWidth();
            int blockHeight = chromaBlockSize.GetHeight();
            int blockSampleCount = blockWidth * blockHeight;
            int transformWidth = transformSize.GetWidth();
            int transformHeight = transformSize.GetHeight();
            int transformColumnCount = blockWidth / transformWidth;
            int transformRowCount = blockHeight / transformHeight;
            int transformBlockCount = transformColumnCount * transformRowCount;
            Av1BlockSize maximumUnitBlockSize =
                Av1BlockSize.Block64x64.GetSubsampled(colorConfig.SubSamplingX, colorConfig.SubSamplingY);

            Span<TSample> candidateBlueReconstruction =
                workspace.GetCandidateReconstruction(0)[..blockSampleCount];

            Span<TSample> candidateRedReconstruction =
                workspace.GetCandidateReconstruction(1)[..blockSampleCount];

            Span<int> candidateBlueCoefficients =
                workspace.GetCandidateCoefficients(0)[..blockSampleCount];

            Span<int> candidateRedCoefficients =
                workspace.GetCandidateCoefficients(1)[..blockSampleCount];

            Span<Av1EncoderTransformBlockState> candidateStates = workspace.CandidateTransformBlocks;
            Span<Av1EncoderTransformBlockState> candidateBlueStates =
                candidateStates[..transformBlockCount];

            Span<Av1EncoderTransformBlockState> candidateRedStates =
                candidateStates.Slice(transformBlockCount, transformBlockCount);

            int contextWidth = chromaBlockSize.Get4x4WideCount();
            int contextHeight = chromaBlockSize.Get4x4HighCount();
            Span<byte> contexts = workspace.TransformContexts;
            Span<byte> blueTopContexts = contexts[..contextWidth];
            Span<byte> blueLeftContexts = contexts.Slice(contextWidth, contextHeight);
            Span<byte> redTopContexts = contexts.Slice(contextWidth + contextHeight, contextWidth);
            Span<byte> redLeftContexts = contexts.Slice(
                (2 * contextWidth) + contextHeight,
                contextHeight);

            // Every candidate starts from the same tile edges, so they are read once.
            Av1NeighborEdges<byte> blueNeighbors = this.picture.CbDcSignLevelCoefficientNeighbors[tileIndex].GetEdges();
            Av1NeighborEdges<byte> redNeighbors = this.picture.CrDcSignLevelCoefficientNeighbors[tileIndex].GetEdges();
            int blueTopIndex = blueNeighbors.GetTopIndex(chromaOrigin);
            int blueLeftIndex = blueNeighbors.GetLeftIndex(chromaOrigin);
            int redTopIndex = redNeighbors.GetTopIndex(chromaOrigin);
            int redLeftIndex = redNeighbors.GetLeftIndex(chromaOrigin);
            Av1PlaneRegion<TSample> blueSource = this.source.GetPlane(Av1Plane.U);
            Av1PlaneRegion<TSample> redSource = this.source.GetPlane(Av1Plane.V);
            Av1PlaneRegion<TSample> blueReconstruction = this.reconstruction.GetPlane(Av1Plane.U);
            Av1PlaneRegion<TSample> redReconstruction = this.reconstruction.GetPlane(Av1Plane.V);
            ReadOnlySpan<TSample> blueSourceSamples = blueSource.Samples;
            ReadOnlySpan<TSample> redSourceSamples = redSource.Samples;
            Span<TSample> blueReconstructionSamples = blueReconstruction.Samples;
            Span<TSample> redReconstructionSamples = redReconstruction.Samples;
            Av1EncoderSpeedSettings speedSettings = this.picture.Parent.SpeedSettings;
            int hogLevel = speedSettings.ChromaHogPruningLevel;
            float hogThreshold = this.picture.Parent.FrameHeader.IsIntra
                ? hogLevel == 4 ? 0.4F : hogLevel == 3 ? -0.6F : -1.2F
                : hogLevel == 4 ? 1.2F : hogLevel == 1 ? -1.2F : 0F;

            ushort chromaModeMask = speedSettings.GetChromaModeMask(
                blockSize.GetMaxUvTransformSize(colorConfig.SubSamplingX, colorConfig.SubSamplingY));

            byte directionalModeSkipMask = hogLevel != 0
                ? GetDirectionalModeSkipMask(
                    blueSource,
                    chromaOrigin,
                    (blockSize.GetHeight() + (Math.Min(0, macroBlock.ToBottomEdge) >> 3)) >> subsamplingY,
                    (blockSize.GetWidth() + (Math.Min(0, macroBlock.ToRightEdge) >> 3)) >> subsamplingX,
                    (1 + subsamplingX) * (1 + subsamplingY),
                    hogThreshold)
                : (byte)0;

            bool pruneSmooth = speedSettings.PruneChromaSmoothByVariance &&
                GetSourceVariance(blueSource, chromaOrigin, blockWidth, blockHeight, this.bitDepth) < 20 &&
                GetSourceVariance(redSource, chromaOrigin, blockWidth, blockHeight, this.bitDepth) < 20;

            // Every chroma mode of the block reads the same rate tables and workspace buffers.
            Av1CoefficientTables tables = writer.GetCoefficientTables();
            Av1ModeCosts modeCosts = tables.ModeCosts;
            Span<int> blockTransformCoefficients = this.blockWorkspace.TransformCoefficients;
            Span<int> blockDequantizedCoefficients = this.blockWorkspace.DequantizedCoefficients;
            Span<int> blockTransformWorkspace = this.blockWorkspace.TransformWorkspace;
            ReadOnlySpan<int> transformTypeProbabilities = this.blockWorkspace.TransformTypeProbabilities;
            Span<int> searchCoefficients = this.blockWorkspace.SearchCoefficients;
            Span<int> searchDequantizedCoefficients = this.blockWorkspace.SearchDequantizedCoefficients;
            Span<int> searchReconstructions = this.blockWorkspace.SearchReconstructions;
            bool hasLumaPalette = paletteInfo.PaletteSizes[0] != 0;
            int paletteDisabledCost = Av1TileWriter.IsPaletteAllowed(
                this.picture.Parent.FrameHeader.AllowScreenContentTools,
                blockSize)
                ? Av1SymbolEncoder.GetPaletteUvModeCost(modeCosts, false, hasLumaPalette)
                : 0;

            Av1RateDistortionStatistics bestStatistics = Av1RateDistortionStatistics.Invalid;
            Av1ChromaPredictionMode bestMode = Av1ChromaPredictionMode.DC;
            selectedAngleDelta = 0;
            selectedChromaFromLumaIndex = 0;
            selectedChromaFromLumaSigns = 0;

            Span<long> angleCosts = stackalloc long[7];
            for (int modeIndex = 0; modeIndex < ChromaModeSearchOrder.Length; modeIndex++)
            {
                Av1ChromaPredictionMode chromaMode = ChromaModeSearchOrder[modeIndex];
                if ((chromaModeMask & (1 << (int)chromaMode)) == 0 ||
                    (speedSettings.PruneChromaModesUsingLumaWinner &&
                    !ShouldSearchChromaMode(this.picture.Parent.EncodingSpeed, lumaMode, chromaMode)))
                {
                    continue;
                }

                if (chromaMode == Av1ChromaPredictionMode.Smooth && pruneSmooth)
                {
                    continue;
                }

                if (blockSize >= Av1BlockSize.Block8x8 &&
                    chromaMode is >= Av1ChromaPredictionMode.Vertical and <= Av1ChromaPredictionMode.Directional67Degrees &&
                    (directionalModeSkipMask & (1 << ((int)chromaMode - (int)Av1ChromaPredictionMode.Vertical))) != 0)
                {
                    continue;
                }

                int modeRate = Av1SymbolEncoder.GetChromaModeCost(modeCosts, chromaMode, false, lumaMode);
                if (Av1RateDistortion.GetCost(this.rateMultiplier, modeRate, 0) > bestStatistics.Cost)
                {
                    continue;
                }

                bool directional = chromaMode is >= Av1ChromaPredictionMode.Vertical and <= Av1ChromaPredictionMode.Directional67Degrees;
                int angleCount = directional && blockSize >= Av1BlockSize.Block8x8 ? ChromaAngleSearchOrder.Length : 1;
                angleCosts.Fill(long.MaxValue);
                for (int angleIndex = 0; angleIndex < angleCount; angleIndex++)
                {
                    int angleDelta = ChromaAngleSearchOrder[angleIndex];
                    if ((angleDelta & 1) != 0)
                    {
                        long limit = bestStatistics.Cost == long.MaxValue ? long.MaxValue : bestStatistics.Cost + (bestStatistics.Cost >> 5);
                        long lowerCost = angleDelta == -3 ? long.MaxValue : angleCosts[angleDelta + 2];
                        long upperCost = angleDelta == 3 ? long.MaxValue : angleCosts[angleDelta + 4];
                        if (lowerCost > limit && upperCost > limit)
                        {
                            continue;
                        }
                    }

                    long costLimit = bestStatistics.Cost;
                    if (angleCount > 1 && (angleDelta & 1) == 0 && costLimit != long.MaxValue)
                    {
                        costLimit += costLimit >> (angleDelta == 0 ? 3 : 5);
                    }

                    blueNeighbors.Top.Slice(blueTopIndex, contextWidth).CopyTo(blueTopContexts);
                    blueNeighbors.Left.Slice(blueLeftIndex, contextHeight).CopyTo(blueLeftContexts);
                    redNeighbors.Top.Slice(redTopIndex, contextWidth).CopyTo(redTopContexts);
                    redNeighbors.Left.Slice(redLeftIndex, contextHeight).CopyTo(redLeftContexts);
                    Av1PredictionMode predictionMode = chromaMode.ToLumaMode();
                    long distortion = this.GetTiledPlaneCost(
                        writer,
                        in tables,
                        in workspace,
                        blockTransformCoefficients,
                        blockDequantizedCoefficients,
                        blockTransformWorkspace,
                        transformTypeProbabilities,
                        searchCoefficients,
                        searchDequantizedCoefficients,
                        searchReconstructions,
                        macroBlock,
                        lumaOrigin,
                        chromaOrigin,
                        blockSize,
                        chromaBlockSize,
                        transformSize,
                        maximumUnitBlockSize,
                        subsamplingX,
                        subsamplingY,
                        lumaMode,
                        predictionMode,
                        angleDelta,
                        Av1Plane.U,
                        blueSource,
                        blueSourceSamples,
                        blueReconstruction,
                        blueReconstructionSamples,
                        [],
                        default,
                        candidateBlueReconstruction,
                        candidateBlueCoefficients,
                        candidateBlueStates,
                        blueTopContexts,
                        blueLeftContexts,
                        costLimit,
                        out long bluePredictionDistortion,
                        out int blueRate);

                    // Inside a plane, block_rd_txfm scores an intra transform with its coefficient rate and
                    // distortion alone and stops the plane once the running cost passes the reference. After the
                    // plane, the running totals pass when either the coded cost or the cost of leaving the
                    // residual uncoded stays within the bound. Reference: av1_txfm_uvrd().
                    if (blueRate == int.MaxValue ||
                        Math.Min(
                            Av1RateDistortion.GetCost(this.rateMultiplier, blueRate, distortion),
                            Av1RateDistortion.GetCost(this.rateMultiplier, 0, bluePredictionDistortion)) > costLimit)
                    {
                        if (angleDelta == 0)
                        {
                            break;
                        }

                        continue;
                    }

                    long redDistortion = this.GetTiledPlaneCost(
                        writer,
                        in tables,
                        in workspace,
                        blockTransformCoefficients,
                        blockDequantizedCoefficients,
                        blockTransformWorkspace,
                        transformTypeProbabilities,
                        searchCoefficients,
                        searchDequantizedCoefficients,
                        searchReconstructions,
                        macroBlock,
                        lumaOrigin,
                        chromaOrigin,
                        blockSize,
                        chromaBlockSize,
                        transformSize,
                        maximumUnitBlockSize,
                        subsamplingX,
                        subsamplingY,
                        lumaMode,
                        predictionMode,
                        angleDelta,
                        Av1Plane.V,
                        redSource,
                        redSourceSamples,
                        redReconstruction,
                        redReconstructionSamples,
                        [],
                        default,
                        candidateRedReconstruction,
                        candidateRedCoefficients,
                        candidateRedStates,
                        redTopContexts,
                        redLeftContexts,
                        costLimit,
                        out long redPredictionDistortion,
                        out int redRate);

                    if (redRate == int.MaxValue ||
                        Math.Min(
                            Av1RateDistortion.GetCost(this.rateMultiplier, blueRate + redRate, distortion + redDistortion),
                            Av1RateDistortion.GetCost(this.rateMultiplier, 0, bluePredictionDistortion + redPredictionDistortion)) > costLimit)
                    {
                        if (angleDelta == 0)
                        {
                            break;
                        }

                        continue;
                    }

                    distortion += redDistortion;

                    int rate = Av1TileWriter.GetChromaModeCost(
                        modeCosts,
                        this.picture.Parent.FrameHeader,
                        colorConfig,
                        modeInfo,
                        blockSize,
                        lumaMode,
                        chromaMode,
                        angleDelta);

                    rate += blueRate + redRate;
                    if (chromaMode == Av1ChromaPredictionMode.DC)
                    {
                        rate += paletteDisabledCost;
                    }

                    Av1RateDistortionStatistics candidateStatistics = new(this.rateMultiplier, rate, distortion)
                    {
                        ResidualRate = blueRate + redRate
                    };

                    this.PenalizeSmoothChromaMode(ref candidateStatistics, chromaMode);
                    angleCosts[angleDelta + 3] = candidateStatistics.Cost;
                    if (candidateStatistics.Cost < bestStatistics.Cost)
                    {
                        Size codedExtent = GetCodedTransformExtent(macroBlock, chromaBlockSize, transformSize, subsamplingX, subsamplingY);
                        CopyTiledCandidate(candidateBlueStates, codedExtent, transformSize, retainedBlueStates);
                        CopyTiledCandidate(candidateRedStates, codedExtent, transformSize, retainedRedStates);

                        bestStatistics = candidateStatistics;
                        bestMode = chromaMode;
                        selectedAngleDelta = angleDelta;
                    }
                }
            }

            selectedStatistics = bestStatistics;
            return bestMode;
        }

        internal static bool ShouldSearchChromaMode(
            HeifEncodingSpeed speed,
            Av1PredictionMode lumaMode,
            Av1ChromaPredictionMode chromaMode)
            => speed < HeifEncodingSpeed.Level4 ||
                (LumaDerivedChromaModeMasks[(int)lumaMode] & (1 << (int)chromaMode)) != 0;

        /// <summary>
        /// Adds one quarter to the cost of a valid smooth chroma candidate at high bit depth sharpness 3, in all frame types.
        /// The higher cost decides the comparison and limits the later candidates. The rate and distortion do not change.
        /// Reference: is_smooth_uv_mode in av1_rd_pick_intra_sbuv_mode().
        /// </summary>
        /// <param name="statistics">The candidate statistics.</param>
        /// <param name="chromaMode">The candidate chroma mode.</param>
        private readonly void PenalizeSmoothChromaMode(ref Av1RateDistortionStatistics statistics, Av1ChromaPredictionMode chromaMode)
        {
            bool smoothMode =
                chromaMode is Av1ChromaPredictionMode.Smooth or Av1ChromaPredictionMode.SmoothVertical or Av1ChromaPredictionMode.SmoothHorizontal;

            if (statistics.Cost != long.MaxValue && this.UsesHighBitDepthSharpness && smoothMode)
            {
                statistics.Cost += statistics.Cost >> 2;
            }
        }

        /// <summary>
        /// Returns the per-sample variance of a chroma source block around the mid-gray level.
        /// </summary>
        /// <param name="source">The chroma source plane.</param>
        /// <param name="origin">The block origin in chroma samples.</param>
        /// <param name="width">The block width.</param>
        /// <param name="height">The block height.</param>
        /// <param name="bitDepth">The coded sample precision.</param>
        /// <returns>The rounded per-sample variance.</returns>
        private static int GetSourceVariance(
            Av1PlaneRegion<TSample> source,
            Point origin,
            int width,
            int height,
            Av1BitDepth bitDepth)
        {
            Span<TSample> midpoint = stackalloc TSample[width];
            return GetPerPixelVariance(source, origin, width, height, bitDepth, midpoint);
        }

        /// <summary>
        /// Codes one plane of an intra or palette candidate, one transform block at a time, in coding order.
        /// Each transform block writes its prediction into the frame, and then its reconstruction when recon_intra()
        /// writes it. Reference: av1_txfm_rd_in_plane() and block_rd_txfm() for an intra block.
        /// </summary>
        /// <param name="writer">The coefficient entropy costs.</param>
        /// <param name="tables">The rate tables and level storage of the writer, which the caller read once.</param>
        /// <param name="workspace">The mode decision workspace, which the caller read once.</param>
        /// <param name="transformCoefficients">The forward transform output buffer.</param>
        /// <param name="dequantizedCoefficients">The dequantized coefficient buffer of the candidate.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
        /// <param name="transformTypeProbabilities">The transform type probabilities of every update type and size.</param>
        /// <param name="searchCoefficients">The quantized coefficient buffer of the winner.</param>
        /// <param name="searchDequantizedCoefficients">The dequantized coefficient buffer of the winner.</param>
        /// <param name="searchReconstructions">The storage of the candidate and winner reconstructions of the type search.</param>
        /// <param name="macroBlock">The block neighborhood.</param>
        /// <param name="lumaOrigin">The block origin in luma samples.</param>
        /// <param name="chromaOrigin">The block origin in plane samples.</param>
        /// <param name="blockSize">The luma block size.</param>
        /// <param name="chromaBlockSize">The plane block size.</param>
        /// <param name="transformSize">The transform size.</param>
        /// <param name="maximumUnitBlockSize">The largest coding unit in plane samples.</param>
        /// <param name="subsamplingX">The horizontal subsampling of the plane.</param>
        /// <param name="subsamplingY">The vertical subsampling of the plane.</param>
        /// <param name="lumaMode">The luma prediction mode of the block.</param>
        /// <param name="predictionMode">The prediction mode of the plane.</param>
        /// <param name="angleDelta">The signed directional-angle adjustment.</param>
        /// <param name="plane">The plane.</param>
        /// <param name="source">The source plane.</param>
        /// <param name="sourceSamples">The samples of <paramref name="source"/>, which the caller reads once outside its loops.</param>
        /// <param name="reconstruction">The frame plane. It gives the edge samples and gets each trial write, as libaom pd->dst.</param>
        /// <param name="reconstructionSamples">The samples of <paramref name="reconstruction"/>, which the caller reads once outside its loops.</param>
        /// <param name="paletteColors">The palette colors; empty for an intra candidate.</param>
        /// <param name="colorIndexMap">The palette color index map; empty for an intra candidate.</param>
        /// <param name="candidateReconstruction">The contiguous candidate reconstruction of the plane block.</param>
        /// <param name="candidateCoefficients">The storage that the type search quantizes each candidate into.</param>
        /// <param name="candidateStates">The candidate transform states.</param>
        /// <param name="topContexts">The top coefficient contexts of the plane block.</param>
        /// <param name="leftContexts">The left coefficient contexts of the plane block.</param>
        /// <param name="costLimit">The cost limit for an early exit.</param>
        /// <param name="predictionDistortion">The residual energy of leaving the plane uncoded.</param>
        /// <param name="rate">The coefficient rate, or <see cref="int.MaxValue"/> after an early exit.</param>
        /// <returns>The distortion, or <see cref="long.MaxValue"/> after an early exit.</returns>
        private long GetTiledPlaneCost(
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            in Av1EncoderModeDecisionWorkspace<TSample> workspace,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> transformWorkspace,
            ReadOnlySpan<int> transformTypeProbabilities,
            Span<int> searchCoefficients,
            Span<int> searchDequantizedCoefficients,
            Span<int> searchReconstructions,
            Av1MacroBlockD macroBlock,
            Point lumaOrigin,
            Point chromaOrigin,
            Av1BlockSize blockSize,
            Av1BlockSize chromaBlockSize,
            Av1TransformSize transformSize,
            Av1BlockSize maximumUnitBlockSize,
            int subsamplingX,
            int subsamplingY,
            Av1PredictionMode lumaMode,
            Av1PredictionMode predictionMode,
            int angleDelta,
            Av1Plane plane,
            Av1PlaneRegion<TSample> source,
            ReadOnlySpan<TSample> sourceSamples,
            Av1PlaneRegion<TSample> reconstruction,
            Span<TSample> reconstructionSamples,
            ReadOnlySpan<ushort> paletteColors,
            Av1PlaneRegion<byte> colorIndexMap,
            Span<TSample> candidateReconstruction,
            Span<int> candidateCoefficients,
            Span<Av1EncoderTransformBlockState> candidateStates,
            Span<byte> topContexts,
            Span<byte> leftContexts,
            long costLimit,
            out long predictionDistortion,
            out int rate)
        {
            int blockWidth = chromaBlockSize.GetWidth();
            int blockHeight = chromaBlockSize.GetHeight();
            int transformWidth = transformSize.GetWidth();
            int transformHeight = transformSize.GetHeight();
            int transformSampleCount = transformSize.GetSize2d();
            int transformWidth4x4 = transformSize.Get4x4WideCount();
            int transformHeight4x4 = transformSize.Get4x4HighCount();
            Size frameContextSize = new(
                this.picture.Parent.FrameHeader.ModeInfoColumnCount >> subsamplingX,
                this.picture.Parent.FrameHeader.ModeInfoRowCount >> subsamplingY);

            Size codedExtent = GetCodedTransformExtent(macroBlock, chromaBlockSize, transformSize, subsamplingX, subsamplingY);
            int maximumUnitWidth = Math.Min(maximumUnitBlockSize.GetWidth(), codedExtent.Width);
            int maximumUnitHeight = Math.Min(maximumUnitBlockSize.GetHeight(), codedExtent.Height);
            Av1TransformType transformType = this.picture.Parent.FrameHeader.CodedLossless
                ? Av1TransformType.DctDct
                : Av1SymbolContextHelper.GetDefaultIntraTransformType(
                    predictionMode,
                    transformSize,
                    this.picture.Parent.FrameHeader.UseReducedTransformSet);

            Av1ComponentType componentType = plane == Av1Plane.Y
                ? Av1ComponentType.Luminance
                : Av1ComponentType.Chroma;

            // Palette samples and centroids remain live across size candidates. Its prediction scratch
            // is disjoint from those inputs, whereas ordinary prediction can use the transient workspace.
            Span<TSample> prediction = (paletteColors.IsEmpty ? workspace.Prediction : workspace.Palette.GetPrediction(0))[..transformSampleCount];
            Span<short> residual = (paletteColors.IsEmpty ? workspace.Residual : workspace.Palette.GetResidual(0))[..transformSampleCount];
            Span<TSample> aboveStorage = workspace.GetReferenceSamples(0);
            Span<TSample> leftStorage = workspace.GetReferenceSamples(1);
            int transformIndex = 0;
            long distortion = 0;
            long accumulatedCost = 0;
            predictionDistortion = 0;
            rate = 0;

            // Both candidate planes hold this block, so the type search swaps its candidate and winner through the
            // block workspace.
            Span<TSample> typeCandidateReconstruction =
                Av1EncoderBlockWorkspace.GetSearchReconstruction<TSample>(searchReconstructions, 0)[..transformSampleCount];

            Span<TSample> typeWinnerReconstruction =
                Av1EncoderBlockWorkspace.GetSearchReconstruction<TSample>(searchReconstructions, 1)[..transformSampleCount];


            // A predicted empty block is priced with the contexts at the block origin, before any transform block
            // of this plane updates them. Reference: av1_get_entropy_contexts() in predict_dc_only_block().
            Av1TransformBlockContext originContext = Av1TileWriter.GetTransformBlockContexts(
                componentType,
                topContexts[..transformWidth4x4],
                leftContexts[..transformHeight4x4],
                chromaBlockSize,
                transformSize);

            // The edge filter strength depends on the neighbors of the block alone, so every transform block shares it.
            bool smoothEdges = this.UseSmoothIntraEdges(macroBlock, lumaOrigin, blockSize, plane);
            ReadOnlySpan<TSample> reconstructionBlock = reconstructionSamples[reconstruction.GetOffset(chromaOrigin.X, chromaOrigin.Y)..];
            Av1PartitionType partitionType = macroBlock.GetRelativeModeInfo(0).Block.PartitionType;

            // Residual syntax completes each bounded 64x64 luma region, scaled for chroma, before
            // moving to the next region. Candidate coefficients and states must retain that exact order.
            for (int regionRow = 0; regionRow < codedExtent.Height; regionRow += maximumUnitHeight)
            {
                int unitBottom = Math.Min(regionRow + maximumUnitHeight, codedExtent.Height);
                for (int regionColumn = 0; regionColumn < codedExtent.Width; regionColumn += maximumUnitWidth)
                {
                    int unitRight = Math.Min(regionColumn + maximumUnitWidth, codedExtent.Width);
                    for (int rowOffset = regionRow; rowOffset < unitBottom; rowOffset += transformHeight)
                    {
                        int transformRow = rowOffset / transformHeight;
                        for (int columnOffset = regionColumn; columnOffset < unitRight; columnOffset += transformWidth)
                        {
                            int transformColumn = columnOffset / transformWidth;
                            int reconstructionOffset = (rowOffset * blockWidth) + columnOffset;
                            Point transformOrigin = chromaOrigin + new Size(columnOffset, rowOffset);
                            if (paletteColors.IsEmpty)
                            {
                                this.PrepareTransformReferenceSamples(
                                    reconstructionBlock,
                                    reconstruction.Stride,
                                    lumaOrigin,
                                    blockSize,
                                    macroBlock,
                                    partitionType,
                                    transformRow,
                                    transformColumn,
                                    blockWidth,
                                    transformSize,
                                    subsamplingX,
                                    subsamplingY,
                                    candidateReconstruction,
                                    aboveStorage,
                                    leftStorage,
                                    out bool hasLeft,
                                    out bool hasAbove);

                                TOperator.PrepareIntra(
                                    transformWorkspace,
                                    sourceSamples[source.GetOffset(transformOrigin.X, transformOrigin.Y)..],
                                    source.Stride,
                                    prediction,
                                    transformSize.GetWidth(),
                                    aboveStorage.Slice(1, transformWidth + transformHeight),
                                    leftStorage.Slice(1, transformWidth + transformHeight),
                                    hasLeft,
                                    hasAbove,
                                    predictionMode,
                                    angleDelta,
                                    this.picture.Sequence.SequenceHeader.EnableIntraEdgeFilter,
                                    smoothEdges,
                                    residual,
                                    transformSize,
                                    this.bitDepth);
                            }
                            else
                            {
                                // Each residual transform borrows exactly its part of the block's index map.
                                // Palette prediction needs no neighboring reconstructed reference samples.
                                TOperator.PreparePalette(
                                    sourceSamples[source.GetOffset(transformOrigin.X, transformOrigin.Y)..],
                                    source.Stride,
                                    paletteColors,
                                    colorIndexMap.GetSubRegion(columnOffset, rowOffset, transformWidth, transformHeight),
                                    prediction,
                                    residual,
                                    transformSize);
                            }

                            // The prediction goes into the frame. Reference: av1_predict_intra_block_facade() into pd->dst in block_rd_txfm().
                            Av1TransformBlockEncoder.WriteFrameSamples(
                                reconstruction, reconstructionSamples, transformOrigin, prediction, transformWidth, transformWidth, transformHeight);

                            Av1TransformBlockContext blockContext = Av1TileWriter.GetTransformBlockContexts(
                                componentType,
                                topContexts.Slice(transformColumn * transformWidth4x4, transformWidth4x4),
                                leftContexts.Slice(transformRow * transformHeight4x4, transformHeight4x4),
                                chromaBlockSize,
                                transformSize);

                            // The type search swaps its candidate and winner coefficients. Neither is kept after the
                            // context update: as in libaom, the block encode quantizes the selected mode again.
                            Span<TSample> candidateTransformReconstruction = typeCandidateReconstruction;
                            Span<TSample> bestTransformReconstruction = typeWinnerReconstruction;
                            Span<int> candidateTransformCoefficients = candidateCoefficients[..transformSampleCount];
                            Span<int> bestTransformCoefficients = searchCoefficients;
                            Span<int> candidateDequantized = dequantizedCoefficients;
                            Span<int> bestDequantized = searchDequantizedCoefficients;

                            // A later transform block of the plane block predicts from this one unless this one is the last.
                            // Reference: the position test of recon_intra().
                            bool lastTransformBlock = (rowOffset + transformHeight) >= blockHeight && (columnOffset + transformWidth) >= blockWidth;

                            // A chroma block of an intra mode searches the one type that it derives from its mode.
                            // Reference: the uv_tx_type of get_tx_mask(), from av1_get_tx_type().
                            TransformTypeSearchResult searchResult = this.SearchTransformType(
                                writer,
                                in tables,
                                transformCoefficients,
                                dequantizedCoefficients,
                                transformWorkspace,
                                transformTypeProbabilities,
                                plane,
                                false,
                                blockContext,
                                originContext,
                                sourceSamples[source.GetOffset(transformOrigin.X, transformOrigin.Y)..],
                                source.Stride,
                                transformOrigin,
                                transformSize,
                                lumaMode,
                                Av1FilterIntraMode.AllFilterIntraModes,
                                transformType,
                                false,
                                costLimit == long.MaxValue ? long.MaxValue : costLimit - accumulatedCost,
                                !lastTransformBlock,
                                prediction,
                                residual,
                                transformWidth,
                                ref candidateTransformReconstruction,
                                ref bestTransformReconstruction,
                                ref candidateTransformCoefficients,
                                ref bestTransformCoefficients,
                                ref candidateDequantized,
                                ref bestDequantized);

                            // The block and the frame get the reconstruction only when the block has coefficients and is not the last
                            // transform block of the plane block. Otherwise they keep the prediction, which is also the reconstruction
                            // of an empty block. Reference: the end of block and position tests of recon_intra(), which writes pd->dst.
                            bool publishReconstruction = searchResult.State.EndOfBlock != 0 && !lastTransformBlock;
                            ReadOnlySpan<TSample> publishedSamples = publishReconstruction ? bestTransformReconstruction : prediction;
                            for (int row = 0; row < transformHeight; row++)
                            {
                                publishedSamples.Slice(row * transformWidth, transformWidth)
                                    .CopyTo(candidateReconstruction.Slice(reconstructionOffset + (row * blockWidth), transformWidth));
                            }

                            if (publishReconstruction)
                            {
                                Av1TransformBlockEncoder.WriteFrameSamples(
                                    reconstruction,
                                    reconstructionSamples,
                                    transformOrigin,
                                    bestTransformReconstruction,
                                    transformWidth,
                                    transformWidth,
                                    transformHeight);
                            }

                            ref Av1EncoderTransformBlockState state = ref candidateStates[transformIndex++];
                            state = searchResult.State;
                            long transformDistortion = searchResult.Distortion;

                            // The uncoded cost uses the energy the transform search reports, which is measured in
                            // the transform domain whenever the distortion is. Reference: the sse of search_tx_type().
                            predictionDistortion += searchResult.Sse;
                            int transformRate = searchResult.Rate;

                            rate += transformRate;
                            distortion += transformDistortion;
                            accumulatedCost += Av1RateDistortion.GetCost(this.rateMultiplier, transformRate, transformDistortion);

                            // The block that takes the running cost over the bound invalidates the plane, even
                            // when it is the last. Reference: the exit_early test of block_rd_txfm().
                            if (accumulatedCost > costLimit)
                            {
                                rate = int.MaxValue;
                                return long.MaxValue;
                            }

                            byte coefficientContext = Av1SymbolContextHelper.GetCoefficientContext(
                                bestTransformCoefficients,
                                transformSize,
                                transformType,
                                state.EndOfBlock);

                            Av1TileWriter.UpdateCoefficientContexts(
                                topContexts.Slice(transformColumn * transformWidth4x4, transformWidth4x4),
                                leftContexts.Slice(transformRow * transformHeight4x4, transformHeight4x4),
                                coefficientContext,
                                transformOrigin,
                                frameContextSize);
                        }
                    }
                }
            }

            return distortion;
        }

        /// <summary>
        /// Keeps the transform states of a new best candidate that has many transform blocks.
        /// The frame samples and the coefficients do not change: libaom keeps a winner only in its mode info and
        /// transform type map, pd->dst keeps what the last trial wrote, and the block encode quantizes the winner again.
        /// </summary>
        /// <param name="candidateStates">The candidate transform states, one for each transform block.</param>
        /// <param name="codedExtent">The coded part of the plane block.</param>
        /// <param name="transformSize">The transform size of the candidate.</param>
        /// <param name="retainedStates">The transform states that the winner keeps, one for each coefficient unit.</param>
        private static void CopyTiledCandidate(
            ReadOnlySpan<Av1EncoderTransformBlockState> candidateStates,
            Size codedExtent,
            Av1TransformSize transformSize,
            Span<Av1EncoderTransformBlockState> retainedStates)
        {
            int blockSampleCount = codedExtent.Width * codedExtent.Height;
            int transformStateStride = transformSize.GetSize2d() / Av1EncoderCoefficientBuffer.TransformBlockUnitCoefficientCount;
            int transformCount = blockSampleCount / transformSize.GetSize2d();
            for (int transformIndex = 0; transformIndex < transformCount; transformIndex++)
            {
                retainedStates[transformIndex * transformStateStride] = candidateStates[transformIndex];
            }
        }

        /// <summary>
        /// Codes one chroma-from-luma alpha for one chroma plane, as one transform block at the block origin.
        /// Reference: cfl_compute_rd() with full transform search.
        /// </summary>
        /// <param name="writer">The tile symbol encoder that prices the syntax.</param>
        /// <param name="tables">The rate tables and level storage of the writer, which the caller read once.</param>
        /// <param name="transformCoefficients">The forward transform output buffer.</param>
        /// <param name="dequantizedCoefficients">The dequantized coefficient buffer of the candidate.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
        /// <param name="transformTypeProbabilities">The transform type probabilities of every update type and size.</param>
        /// <param name="residual">The residual buffer of one transform block.</param>
        /// <param name="searchCoefficients">The quantized coefficient buffer of the winner.</param>
        /// <param name="searchDequantizedCoefficients">The dequantized coefficient buffer of the winner.</param>
        /// <param name="searchReconstructions">The storage of the candidate and winner reconstructions of the type search.</param>
        /// <param name="lumaMode">The luma prediction mode of the block.</param>
        /// <param name="plane">The chroma plane.</param>
        /// <param name="chromaOrigin">The block origin in chroma samples.</param>
        /// <param name="transformSize">The chroma transform size.</param>
        /// <param name="source">The source plane.</param>
        /// <param name="dc">The DC prediction sample of the plane.</param>
        /// <param name="context">The coefficient context of the transform block.</param>
        /// <param name="lumaQ3">The zero-mean luma samples, in Q3.</param>
        /// <param name="alphaQ3">The signed alpha, in Q3.</param>
        /// <param name="reconstruction">
        /// The contiguous candidate samples, which get the prediction. A CfL block is one transform block, so no later block predicts from its
        /// reconstruction and the search does not build it. Reference: the last block test of recon_intra().
        /// </param>
        /// <param name="frame">The frame plane that gets the prediction, as libaom writes pd->dst; empty when the call is not a libaom trial.</param>
        /// <param name="coefficients">The storage that the type search quantizes the candidate into.</param>
        /// <param name="state">The candidate transform state.</param>
        /// <param name="rate">The coefficient rate.</param>
        /// <returns>The distortion.</returns>
        private long GetChromaFromLumaPlaneCost(
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> transformWorkspace,
            ReadOnlySpan<int> transformTypeProbabilities,
            Span<short> residual,
            Span<int> searchCoefficients,
            Span<int> searchDequantizedCoefficients,
            Span<int> searchReconstructions,
            Av1PredictionMode lumaMode,
            Av1Plane plane,
            Point chromaOrigin,
            Av1TransformSize transformSize,
            Av1PlaneRegion<TSample> source,
            TSample dc,
            Av1TransformBlockContext context,
            ReadOnlySpan<short> lumaQ3,
            int alphaQ3,
            Span<TSample> reconstruction,
            Av1PlaneRegion<TSample> frame,
            Span<int> coefficients,
            ref Av1EncoderTransformBlockState state,
            out int rate)
        {
            // CfL adds the scaled luma AC part to the DC prediction. The prediction is built in the candidate samples.
            int width = transformSize.GetWidth();
            int height = transformSize.GetHeight();
            int sampleCount = transformSize.GetSize2d();
            Span<TSample> prediction = reconstruction[..sampleCount];
            prediction.Fill(dc);
            TOperator.ApplyChromaFromLuma(lumaQ3, prediction, alphaQ3, transformSize, this.bitDepth);

            // The prediction goes into the frame. The block is one transform block, so the frame keeps the prediction.
            // Reference: av1_predict_intra_block_facade() into pd->dst in block_rd_txfm(), and the last block test of recon_intra().
            Av1TransformBlockEncoder.WriteFrameSamples(frame, frame.Samples, chromaOrigin, prediction, width, width, height);
            Span<short> blockResidual = residual[..sampleCount];
            TOperator.SubtractPrediction(Av1TransformBlockEncoder.GetPlaneSpan(source, chromaOrigin), source.Stride, prediction, blockResidual, width, height);

            Span<TSample> candidateReconstruction = Av1EncoderBlockWorkspace.GetSearchReconstruction<TSample>(searchReconstructions, 0)[..sampleCount];
            Span<TSample> bestReconstruction = Av1EncoderBlockWorkspace.GetSearchReconstruction<TSample>(searchReconstructions, 1)[..sampleCount];
            Span<int> candidateCoefficients = coefficients;
            Span<int> bestCoefficients = searchCoefficients;
            Span<int> candidateDequantized = dequantizedCoefficients;
            Span<int> bestDequantized = searchDequantizedCoefficients;

            // A CfL block is one transform at the block origin, and searches the DCT_DCT that its chroma mode
            // derives. Reference: av1_txfm_rd_in_plane() of cfl_compute_rd().
            TransformTypeSearchResult result = this.SearchTransformType(
                writer,
                in tables,
                transformCoefficients,
                dequantizedCoefficients,
                transformWorkspace,
                transformTypeProbabilities,
                plane,
                false,
                context,
                context,
                Av1TransformBlockEncoder.GetPlaneSpan(source, chromaOrigin),
                source.Stride,
                chromaOrigin,
                transformSize,
                lumaMode,
                Av1FilterIntraMode.AllFilterIntraModes,
                Av1TransformType.DctDct,
                false,
                long.MaxValue,
                false,
                prediction,
                blockResidual,
                width,
                ref candidateReconstruction,
                ref bestReconstruction,
                ref candidateCoefficients,
                ref bestCoefficients,
                ref candidateDequantized,
                ref bestDequantized);

            // The winner keeps its state only: as in libaom, the block encode quantizes the selected mode again.
            state = result.State;
            rate = result.Rate;
            return result.Distortion;
        }

        /// <summary>
        /// Estimates the best chroma-from-luma alpha of one plane from the transform energy of each prediction.
        /// Reference: cfl_pick_plane_parameter() with intra_model_rd().
        /// </summary>
        /// <param name="blockWorkspace">The block workspace.</param>
        /// <param name="plane">The chroma plane.</param>
        /// <param name="source">The source plane.</param>
        /// <param name="chromaOrigin">The block origin in chroma samples.</param>
        /// <param name="dc">The DC prediction sample of the plane.</param>
        /// <param name="lumaQ3">The zero-mean luma samples, in Q3.</param>
        /// <param name="transformSize">The chroma transform size.</param>
        /// <param name="bitDepth">The coded sample bit depth.</param>
        /// <param name="prediction">The contiguous prediction samples.</param>
        /// <param name="frame">The frame plane that gets each prediction, as libaom writes pd->dst.</param>
        /// <param name="residual">The residual samples.</param>
        /// <param name="coefficients">The transform coefficients.</param>
        /// <param name="transformWorkspace">The forward transform workspace.</param>
        /// <returns>The alpha candidate index of the estimate.</returns>
        private static int FindBestChromaFromLumaEstimate(
            Av1EncoderBlockWorkspace blockWorkspace,
            Av1Plane plane,
            Av1PlaneRegion<TSample> source,
            Point chromaOrigin,
            TSample dc,
            ReadOnlySpan<short> lumaQ3,
            Av1TransformSize transformSize,
            Av1BitDepth bitDepth,
            Span<TSample> prediction,
            Av1PlaneRegion<TSample> frame,
            Span<short> residual,
            Span<int> coefficients,
            Span<int> transformWorkspace)
        {
            return FindBestChromaFromLumaEstimateCore(
                blockWorkspace,
                plane,
                source,
                chromaOrigin,
                dc,
                lumaQ3,
                transformSize,
                bitDepth,
                prediction,
                frame,
                residual,
                coefficients,
                transformWorkspace);
        }

        /// <summary>
        /// Estimates the best chroma-from-luma alpha of one plane. It starts at zero, then walks up and then down,
        /// and stops each walk when the transform energy does not improve.
        /// </summary>
        /// <param name="blockWorkspace">The block workspace.</param>
        /// <param name="plane">The chroma plane.</param>
        /// <param name="source">The source plane.</param>
        /// <param name="chromaOrigin">The block origin in chroma samples.</param>
        /// <param name="dc">The DC prediction sample of the plane.</param>
        /// <param name="lumaQ3">The zero-mean luma samples, in Q3.</param>
        /// <param name="transformSize">The chroma transform size.</param>
        /// <param name="bitDepth">The coded sample bit depth.</param>
        /// <param name="prediction">The contiguous prediction samples.</param>
        /// <param name="frame">The frame plane that gets each prediction, as libaom writes pd->dst.</param>
        /// <param name="residual">The residual samples.</param>
        /// <param name="coefficients">The transform coefficients.</param>
        /// <param name="transformWorkspace">The forward transform workspace.</param>
        /// <returns>The alpha candidate index of the estimate.</returns>
        private static int FindBestChromaFromLumaEstimateCore(
            Av1EncoderBlockWorkspace blockWorkspace,
            Av1Plane plane,
            Av1PlaneRegion<TSample> source,
            Point chromaOrigin,
            TSample dc,
            ReadOnlySpan<short> lumaQ3,
            Av1TransformSize transformSize,
            Av1BitDepth bitDepth,
            Span<TSample> prediction,
            Av1PlaneRegion<TSample> frame,
            Span<short> residual,
            Span<int> coefficients,
            Span<int> transformWorkspace)
        {
            int sampleCount = transformSize.GetSize2d();
            long bestCost = long.MaxValue;
            int bestCandidate = Av1ChromaFromLumaMath.AlphaZeroIndex;
            Span<TSample> frameSamples = frame.Samples;
            ReadOnlySpan<TSample> sourceBlock = Av1TransformBlockEncoder.GetPlaneSpan(source, chromaOrigin);

            // Start at zero, then walk positive and negative alpha independently. Stop each
            // direction when its transform energy no longer improves the best estimate.
            for (int directionIndex = 0; directionIndex < 2; directionIndex++)
            {
                int direction = directionIndex == 0 ? 1 : -1;
                int firstStep = directionIndex == 0 ? 0 : 1;
                for (int step = firstStep; step <= Av1ChromaFromLumaMath.AlphaMagnitudeCount; step++)
                {
                    int candidate = Av1ChromaFromLumaMath.AlphaZeroIndex + (direction * step);
                    prediction[..sampleCount].Fill(dc);
                    TOperator.ApplyChromaFromLuma(lumaQ3, prediction, Av1ChromaFromLumaMath.CandidateIndexToAlpha(candidate), transformSize, bitDepth);

                    // The prediction goes into the frame. Reference: av1_predict_intra_block_facade() into pd->dst in intra_model_rd().
                    Av1TransformBlockEncoder.WriteFrameSamples(
                        frame, frameSamples, chromaOrigin, prediction, transformSize.GetWidth(), transformSize.GetWidth(), transformSize.GetHeight());

                    TOperator.SubtractPrediction(sourceBlock, source.Stride, prediction, residual, transformSize.GetWidth(), transformSize.GetHeight());

                    // intra_model_rd() subtracts with the border padding of the picture.
                    Av1TransformBlockEncoder.PadBorderResidual(
                        blockWorkspace,
                        plane,
                        chromaOrigin,
                        residual,
                        transformSize.GetWidth(),
                        transformSize.GetWidth(),
                        transformSize.GetHeight(),
                        Av1TransformType.DctDct);

                    Av1ForwardTransformer.Transform2d(
                        residual,
                        coefficients,
                        (uint)transformSize.GetWidth(),
                        Av1TransformType.DctDct,
                        transformSize,
                        bitDepth.GetBitCount(),
                        transformWorkspace);

                    long cost = Av1CoefficientMeasures.SumAbsolute(coefficients[..sampleCount]);

                    if (cost >= bestCost)
                    {
                        break;
                    }

                    bestCost = cost;
                    bestCandidate = candidate;
                }
            }

            return bestCandidate;
        }

        /// <summary>
        /// Derives the directional edge-filter class from the relevant neighboring coding blocks.
        /// </summary>
        private bool UseSmoothIntraEdges(Av1MacroBlockD macroBlock, Point lumaOrigin, Av1BlockSize blockSize, Av1Plane plane)
        {
            ObuColorConfig colorConfig = this.picture.Sequence.SequenceHeader.ColorConfig;
            int subX = plane == Av1Plane.Y ? 0 : colorConfig.SubSamplingX ? 1 : 0;
            int subY = plane == Av1Plane.Y ? 0 : colorConfig.SubSamplingY ? 1 : 0;
            int row = lumaOrigin.Y >> Av1Constants.ModeInfoSizeLog2;
            int column = lumaOrigin.X >> Av1Constants.ModeInfoSizeLog2;
            bool hasAbove = macroBlock.IsUpAvailable;
            bool hasLeft = macroBlock.IsLeftAvailable;
            if (subX != 0 && blockSize.Get4x4WideCount() < 2)
            {
                hasLeft = column - 1 > macroBlock.Tile.ModeInfoColumnStart;
            }

            if (subY != 0 && blockSize.Get4x4HighCount() < 2)
            {
                hasAbove = row - 1 > macroBlock.Tile.ModeInfoRowStart;
            }

            // Chroma may cover several luma units. Its neighbors are the bottom-right luma units in the
            // adjacent chroma regions, measured from the top-left unit covered by the current chroma block.
            int baseOffset = -((row & subY) * macroBlock.ModeInfoStride) - (column & subX);
            if (hasAbove && IsSmoothIntraNeighbor(
                macroBlock.GetRelativeModeInfo(baseOffset - macroBlock.ModeInfoStride + subX).Block, plane))
            {
                return true;
            }

            return hasLeft && IsSmoothIntraNeighbor(
                macroBlock.GetRelativeModeInfo(baseOffset + (subY * macroBlock.ModeInfoStride) - 1).Block, plane);
        }

        /// <summary>
        /// Determines whether a neighboring block supplies the smooth edge-filter class.
        /// </summary>
        private static bool IsSmoothIntraNeighbor(Av1EncoderBlockModeInfo modeInfo, Av1Plane plane)
        {
            if (plane == Av1Plane.Y)
            {
                return modeInfo.Mode is Av1PredictionMode.Smooth or Av1PredictionMode.SmoothVertical or Av1PredictionMode.SmoothHorizontal;
            }

            // An inter winner can retain the preceding intra trial's UV field. That field has no inter
            // meaning, so only an ordinary intra neighbor can select chroma smooth-edge thresholds.
            return !modeInfo.UseIntraBlockCopy && modeInfo.Mode < Av1PredictionMode.InterModeStart
                && modeInfo.UvMode is Av1ChromaPredictionMode.Smooth or Av1ChromaPredictionMode.SmoothVertical or Av1ChromaPredictionMode.SmoothHorizontal;
        }

        /// <summary>
        /// Measures the joint rate and distortion of one chroma mode on both chroma planes of a one-transform block.
        /// </summary>
        /// <param name="writer">The tile symbol encoder that prices the syntax.</param>
        /// <param name="modeInfo">The mode information of the block.</param>
        /// <param name="lumaMode">The luma mode, which selects the transform-type context.</param>
        /// <param name="chromaMode">The chroma mode.</param>
        /// <param name="angleDelta">The signed directional-angle adjustment.</param>
        /// <param name="blockSize">The luma block size.</param>
        /// <param name="blue">The values and buffers of the blue-difference plane that every candidate shares.</param>
        /// <param name="red">The values and buffers of the red-difference plane that every candidate shares.</param>
        /// <param name="paletteDisabledCost">The rate of signaling no chroma palette.</param>
        /// <param name="costLimit">The cost bound of the candidate.</param>
        /// <param name="candidateBlueState">The blue-difference transform state of the candidate.</param>
        /// <param name="candidateRedState">The red-difference transform state of the candidate.</param>
        /// <returns>The joint rate and distortion, or an invalid result when the candidate exceeds its bound.</returns>
        private Av1RateDistortionStatistics GetChromaCandidateCost(
            Av1SymbolEncoder writer,
            Av1MacroBlockModeInfo modeInfo,
            Av1PredictionMode lumaMode,
            Av1ChromaPredictionMode chromaMode,
            int angleDelta,
            Av1BlockSize blockSize,
            in Av1IntraCandidatePlane<TSample> blue,
            in Av1IntraCandidatePlane<TSample> red,
            int paletteDisabledCost,
            long costLimit,
            ref Av1EncoderTransformBlockState candidateBlueState,
            ref Av1EncoderTransformBlockState candidateRedState)
        {
            return this.GetChromaCandidateCostCore(
                writer,
                modeInfo,
                lumaMode,
                chromaMode,
                angleDelta,
                blockSize,
                in blue,
                in red,
                paletteDisabledCost,
                costLimit,
                ref candidateBlueState,
                ref candidateRedState);
        }

        /// <summary>
        /// Measures the joint rate and distortion of one chroma mode.
        /// </summary>
        /// <inheritdoc cref="GetChromaCandidateCost"/>
        private Av1RateDistortionStatistics GetChromaCandidateCostCore(
            Av1SymbolEncoder writer,
            Av1MacroBlockModeInfo modeInfo,
            Av1PredictionMode lumaMode,
            Av1ChromaPredictionMode chromaMode,
            int angleDelta,
            Av1BlockSize blockSize,
            in Av1IntraCandidatePlane<TSample> blue,
            in Av1IntraCandidatePlane<TSample> red,
            int paletteDisabledCost,
            long costLimit,
            ref Av1EncoderTransformBlockState candidateBlueState,
            ref Av1EncoderTransformBlockState candidateRedState)
        {
            Av1PredictionMode predictionMode = chromaMode.ToLumaMode();
            Av1TransformSize transformSize = blue.TransformSize;

            // Intra chroma derives one transform type from the shared UV prediction mode. The type is not
            // signaled independently for either chroma plane, so U and V must use the same legal fallback.
            Av1TransformType transformType = this.picture.Parent.FrameHeader.CodedLossless
                ? Av1TransformType.DctDct
                : Av1SymbolContextHelper.GetDefaultIntraTransformType(
                    predictionMode,
                    transformSize,
                    this.picture.Parent.FrameHeader.UseReducedTransformSet);

            long distortion = TOperator.EncodeCandidate(in blue, predictionMode, angleDelta, transformType, ref candidateBlueState, out long blueSse);
            int blueRate = writer.GetCoefficientCost(
                blue.Tables,
                transformSize,
                transformType,
                lumaMode,
                blue.QuantizedCoefficients,
                Av1ComponentType.Chroma,
                blue.Context,
                candidateBlueState.EndOfBlock,
                this.picture.Parent.FrameHeader.UseReducedTransformSet,
                Av1FilterIntraMode.AllFilterIntraModes,
                usesInterTransformSet: false);

            // The uncoded cost uses the energy the transform search reports, which is measured in the transform
            // domain whenever the distortion is. Reference: the sse of search_tx_type().
            long predictionDistortion = blueSse;

            // An intra plane whose transform blocks exceed the bound is invalid before the uncoded cost is
            // considered. Reference: the exit_early test of block_rd_txfm() within av1_txfm_rd_in_plane().
            long blueCost = Av1RateDistortion.GetCost(this.rateMultiplier, blueRate, distortion);
            if (blueCost > costLimit)
            {
                return Av1RateDistortionStatistics.Invalid;
            }

            if (Math.Min(
                blueCost,
                Av1RateDistortion.GetCost(this.rateMultiplier, 0, predictionDistortion)) > costLimit)
            {
                return Av1RateDistortionStatistics.Invalid;
            }

            long redDistortion = TOperator.EncodeCandidate(in red, predictionMode, angleDelta, transformType, ref candidateRedState, out long redSse);

            predictionDistortion += redSse;

            // The mode and angle are written once for the UV pair; coefficient syntax remains independent
            // because each plane has its own EOB, scan values, and neighboring coefficient context.
            int rate = Av1TileWriter.GetChromaModeCost(
                blue.Tables.ModeCosts,
                this.picture.Parent.FrameHeader,
                this.picture.Sequence.SequenceHeader.ColorConfig,
                modeInfo,
                blockSize,
                lumaMode,
                chromaMode,
                angleDelta);

            if (chromaMode == Av1ChromaPredictionMode.DC)
            {
                rate += paletteDisabledCost;
            }

            int redRate = writer.GetCoefficientCost(
                red.Tables,
                transformSize,
                transformType,
                lumaMode,
                red.QuantizedCoefficients,
                Av1ComponentType.Chroma,
                red.Context,
                candidateRedState.EndOfBlock,
                this.picture.Parent.FrameHeader.UseReducedTransformSet,
                Av1FilterIntraMode.AllFilterIntraModes,
                usesInterTransformSet: false);

            if (Av1RateDistortion.GetCost(this.rateMultiplier, redRate, redDistortion) > costLimit)
            {
                return Av1RateDistortionStatistics.Invalid;
            }

            distortion += redDistortion;

            // Each plane is checked on the running totals of both planes, and a total passes when either its
            // coded cost or the cost of leaving its residual uncoded stays within the bound.
            // Reference: the AOMMIN(this_rd, skip_txfm_rd) test of av1_txfm_uvrd().
            if (Math.Min(
                Av1RateDistortion.GetCost(this.rateMultiplier, blueRate + redRate, distortion),
                Av1RateDistortion.GetCost(this.rateMultiplier, 0, predictionDistortion)) > costLimit)
            {
                return Av1RateDistortionStatistics.Invalid;
            }

            return new(this.rateMultiplier, rate + blueRate + redRate, distortion)
            {
                PredictionDistortion = predictionDistortion,
                ResidualRate = blueRate + redRate,
                HasCoefficients = candidateBlueState.EndOfBlock != 0 || candidateRedState.EndOfBlock != 0
            };
        }

        /// <summary>
        /// Prepares the shared corner and extended top and left edges for intra prediction.
        /// </summary>
        /// <param name="reconstructionPlane">The previously reconstructed plane.</param>
        /// <param name="blockOrigin">The prediction block's origin in plane samples.</param>
        /// <param name="width">The transform width in samples.</param>
        /// <param name="height">The transform height in samples.</param>
        /// <param name="hasLeft">Whether the left edge is available.</param>
        /// <param name="hasAbove">Whether the top edge is available.</param>
        /// <param name="hasTopRight">Whether the adjacent top-right block is reconstructed.</param>
        /// <param name="hasBottomLeft">Whether the adjacent bottom-left block is reconstructed.</param>
        /// <param name="bitDepth">The sample precision used for unavailable edges.</param>
        /// <param name="aboveStorage">The corner followed by at least width plus height top-edge samples.</param>
        /// <param name="leftStorage">The corner followed by at least width plus height left-edge samples.</param>
        public static void PrepareReferenceSamples(
            Av1PlaneRegion<TSample> reconstructionPlane,
            Point blockOrigin,
            int width,
            int height,
            bool hasLeft,
            bool hasAbove,
            bool hasTopRight,
            bool hasBottomLeft,
            Av1BitDepth bitDepth,
            Span<TSample> aboveStorage,
            Span<TSample> leftStorage)
        {
            // A directional ray can reach width + height - 1 on either edge, including on rectangles.
            // Only one adjacent block supplies extension samples; the rest repeat its final sample.
            Span<TSample> above = aboveStorage.Slice(1, width + height);
            Span<TSample> left = leftStorage.Slice(1, width + height);

            // The plane is read once. The top edge starts at the sample above the block origin, and the left edge
            // walks down the column before it, one stride per row.
            ReadOnlySpan<TSample> planeSamples = reconstructionPlane.Samples;
            int stride = reconstructionPlane.Stride;
            int topOffset = reconstructionPlane.GetOffset(blockOrigin.X, blockOrigin.Y - 1);
            int leftOffset = reconstructionPlane.GetOffset(blockOrigin.X - 1, blockOrigin.Y);
            if (hasAbove)
            {
                int visibleTopCount = Math.Min(width, reconstructionPlane.Width - blockOrigin.X);
                planeSamples.Slice(topOffset, visibleTopCount).CopyTo(above);
                above[visibleTopCount..width].Fill(above[visibleTopCount - 1]);
            }

            if (hasLeft)
            {
                int visibleLeftCount = Math.Min(height, reconstructionPlane.Height - blockOrigin.Y);
                for (int row = 0; row < visibleLeftCount; row++)
                {
                    left[row] = planeSamples[leftOffset + (row * stride)];
                }

                left[visibleLeftCount..height].Fill(left[visibleLeftCount - 1]);
            }

            int midpoint = 128 << (bitDepth.GetBitCount() - 8);
            if (!hasAbove)
            {
                above[..width].Fill(hasLeft ? left[0] : TOperator.CreateSample(midpoint - 1));
            }

            if (!hasLeft)
            {
                left[..height].Fill(hasAbove ? above[0] : TOperator.CreateSample(midpoint + 1));
            }

            // Availability describes coding order, not the number of samples left at the frame boundary.
            // A partially present adjacent block contributes only its coded samples before endpoint repetition.
            int topRightCount = hasTopRight
                ? Math.Min(Math.Min(width, height), reconstructionPlane.Width - blockOrigin.X - width)
                : 0;

            if (hasTopRight)
            {
                planeSamples.Slice(topOffset + width, topRightCount).CopyTo(above[width..]);
            }

            int topCount = width + topRightCount;
            above[topCount..].Fill(above[topCount - 1]);

            int bottomLeftCount = hasBottomLeft
                ? Math.Min(Math.Min(height, width), reconstructionPlane.Height - blockOrigin.Y - height)
                : 0;

            for (int row = height; row < height + bottomLeftCount; row++)
            {
                left[row] = planeSamples[leftOffset + (row * stride)];
            }

            int leftCount = height + bottomLeftCount;
            left[leftCount..].Fill(left[leftCount - 1]);

            // Zone-two projection and Paeth address the common corner immediately before both edges.
            // Missing edges derive it from the closest coded sample or the bit-depth midpoint.
            TSample corner = hasAbove && hasLeft
                ? planeSamples[topOffset - 1]
                : hasAbove
                    ? above[0]
                    : hasLeft
                        ? left[0]
                        : TOperator.CreateSample(midpoint);

            aboveStorage[0] = corner;
            leftStorage[0] = corner;
        }
    }
}

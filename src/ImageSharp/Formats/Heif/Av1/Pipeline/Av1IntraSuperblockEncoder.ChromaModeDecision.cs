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
            ReadOnlySpan<short> lumaQ3 = default;
            if (usesChromaFromLuma)
            {
                TOperator.PrepareChromaFromLuma(
                    this.reconstruction.GetPlane(Av1Plane.Y),
                    new Point(origin.X << subX, origin.Y << subY),
                    workspace.ChromaFromLumaSamples,
                    transformSize,
                    this.GetChromaFromLumaExtent(macroBlock, blockOrigin, blockSize, modeInfo.Block.TransformSize, subX, subY),
                    subX != 0,
                    subY != 0);

                lumaQ3 = workspace.ChromaFromLumaSamples;
            }

            int rate = previousStatistics.Rate - previousStatistics.ResidualRate;
            int residualRate = usesChromaFromLuma
                ? writer.GetChromaFromLumaCost(block.PredictionUnit.ChromaFromLumaIndex, block.PredictionUnit.ChromaFromLumaSigns)
                : 0;

            long distortion = 0;
            bool hasCoefficients = false;
            for (int planeIndex = 1; planeIndex < 3; planeIndex++)
            {
                Av1Plane plane = (Av1Plane)planeIndex;
                Buffer2DRegion<TSample> source = this.source.GetPlane(plane);

                Buffer2DRegion<TSample> reconstruction = this.reconstruction.GetPlane(plane);
                Span<TSample> samples = workspace.GetCandidateReconstruction(planeIndex - 1)[..sampleCount];
                Span<int> coefficients = workspace.GetCandidateCoefficients(planeIndex - 1)[..sampleCount];
                Span<Av1EncoderTransformBlockState> states = workspace.CandidateTransformBlocks[..transformCount];
                Span<byte> topContexts = workspace.TransformContexts[..contextWidth];
                Span<byte> leftContexts = workspace.TransformContexts.Slice(contextWidth, contextHeight);

                Av1NeighborArrayUnit<byte> neighbors = plane == Av1Plane.U
                    ? this.picture.CbDcSignLevelCoefficientNeighbors[tileIndex]
                    : this.picture.CrDcSignLevelCoefficientNeighbors[tileIndex];

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
                        reconstruction,
                        blockOrigin,
                        origin,
                        blockSize,
                        macroBlock,
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
                        coefficients,
                        ref states[0],
                        out planeRate);
                }
                else
                {
                    int paletteSize = paletteInfo.PaletteSizes[(int)Av1PlaneType.Uv];
                    ReadOnlySpan<ushort> colors = paletteSize == 0 ? [] : paletteInfo.GetColors(plane)[..paletteSize];
                    Buffer2DRegion<byte> map = paletteSize == 0
                        ? default
                        : this.superblock.Workspace.GetPaletteMaps().GetMap(Av1PlaneType.Uv, width, planeSize.GetHeight());

                    distortion += this.GetTiledPlaneCost(
                        writer,
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
                        reconstruction,
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

                CopyTiledCandidate(
                    samples,
                    coefficients,
                    states,
                    reconstruction,
                    origin,
                    width,
                    extent,
                    transformSize,
                    this.coefficientBuffer.GetPlaneSpan(this.superblock.Index, plane)[this.codedAreaChroma..],
                    committedStates);
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
            Span<int> retainedBlueCoefficients,
            Span<int> retainedRedCoefficients,
            Span<Av1EncoderTransformBlockState> retainedBlueStates,
            Span<Av1EncoderTransformBlockState> retainedRedStates,
            ref Av1EncoderPaletteInfo paletteInfo,
            out int selectedAngleDelta,
            out byte selectedChromaFromLumaIndex,
            out sbyte selectedChromaFromLumaSigns,
            out Av1RateDistortionStatistics selectedStatistics)
        {
            long workStart = Av1WorkCounters.Start();
            Av1ChromaPredictionMode workResult = this.SelectChromaModeCore(writer, macroBlock, modeInfo, lumaOrigin, chromaOrigin, blockSize, tileIndex, lumaMode, transformSize, retainedBlueCoefficients, retainedRedCoefficients, retainedBlueStates, retainedRedStates, ref paletteInfo, out selectedAngleDelta, out selectedChromaFromLumaIndex, out selectedChromaFromLumaSigns, out selectedStatistics);
            Av1WorkCounters.Stop(Av1WorkCounters.IntraSbuv, workStart);
            return workResult;
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
            Span<int> retainedBlueCoefficients,
            Span<int> retainedRedCoefficients,
            Span<Av1EncoderTransformBlockState> retainedBlueStates,
            Span<Av1EncoderTransformBlockState> retainedRedStates,
            ref Av1EncoderPaletteInfo paletteInfo,
            out int selectedAngleDelta,
            out byte selectedChromaFromLumaIndex,
            out sbyte selectedChromaFromLumaSigns,
            out Av1RateDistortionStatistics selectedStatistics)
        {
            Av1WorkCounters.Count(Av1WorkCounters.IntraSbuv);
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
                retainedBlueCoefficients,
                retainedRedCoefficients,
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
                    retainedBlueCoefficients,
                    retainedRedCoefficients,
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
            Span<int> retainedBlueCoefficients,
            Span<int> retainedRedCoefficients,
            Span<Av1EncoderTransformBlockState> retainedBlueStates,
            Span<Av1EncoderTransformBlockState> retainedRedStates,
            ref Av1EncoderPaletteInfo paletteInfo,
            out int selectedAngleDelta,
            out byte selectedChromaFromLumaIndex,
            out sbyte selectedChromaFromLumaSigns,
            out Av1RateDistortionStatistics selectedStatistics)
        {
            long workStart = Av1WorkCounters.Start();
            Av1ChromaPredictionMode workResult = this.SelectChromaPredictionCore(writer, macroBlock, modeInfo, lumaOrigin, chromaOrigin, blockSize, tileIndex, lumaMode, transformSize, retainedBlueCoefficients, retainedRedCoefficients, retainedBlueStates, retainedRedStates, ref paletteInfo, out selectedAngleDelta, out selectedChromaFromLumaIndex, out selectedChromaFromLumaSigns, out selectedStatistics);
            Av1WorkCounters.Stop(Av1WorkCounters.ChromaPrediction, workStart);
            return workResult;
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
            Span<int> retainedBlueCoefficients,
            Span<int> retainedRedCoefficients,
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
                    retainedBlueCoefficients,
                    retainedRedCoefficients,
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

            Buffer2DRegion<TSample> blueSource = this.source.GetPlane(Av1Plane.U);
            Buffer2DRegion<TSample> redSource = this.source.GetPlane(Av1Plane.V);
            Buffer2DRegion<TSample> blueReconstruction = this.reconstruction.GetPlane(Av1Plane.U);
            Buffer2DRegion<TSample> redReconstruction = this.reconstruction.GetPlane(Av1Plane.V);
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
            Av1TransformBlockContext blueContext = Av1TileWriter.GetTransformBlockContexts(
                Av1ComponentType.Chroma,
                this.picture.CbDcSignLevelCoefficientNeighbors[tileIndex],
                chromaOrigin,
                chromaBlockSize,
                transformSize);

            Av1TransformBlockContext redContext = Av1TileWriter.GetTransformBlockContexts(
                Av1ComponentType.Chroma,
                this.picture.CrDcSignLevelCoefficientNeighbors[tileIndex],
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

            bool hasLumaPalette = paletteInfo.PaletteSizes[0] != 0;
            int paletteDisabledCost = Av1TileWriter.IsPaletteAllowed(
                this.picture.Parent.FrameHeader.AllowScreenContentTools,
                blockSize)
                ? writer.GetPaletteUvModeCost(false, hasLumaPalette)
                : 0;

            bool chromaFromLumaAllowed = blockSize.AllowsChromaFromLuma(
                this.picture.Parent.FrameHeader.LosslessArray[modeInfo.Block.SegmentId],
                colorConfig.SubSamplingX,
                colorConfig.SubSamplingY);

            Span<long> angleCosts = stackalloc long[7];
            for (int modeIndex = 0; modeIndex < ChromaModeSearchOrder.Length; modeIndex++)
            {
                // Chroma-from-luma follows DC so its complete cost bounds the remaining spatial modes.
                if (modeIndex == 1)
                {
                    if (chromaFromLumaAllowed &&
                        (chromaModeMask & (1 << (int)Av1ChromaPredictionMode.ChromaFromLuma)) != 0 &&
                        Av1RateDistortion.GetCost(
                            this.rateMultiplier,
                            writer.GetChromaModeCost(Av1ChromaPredictionMode.ChromaFromLuma, true, lumaMode),
                            0) <= bestStatistics.Cost)
                    {
                        Span<short> lumaQ3 = workspace.ChromaFromLumaSamples;
                        Point chromaLumaOrigin = new(
                            chromaOrigin.X << subsamplingX,
                            chromaOrigin.Y << subsamplingY);

                        // CfL consumes the complete luma region represented by this chroma block, which starts before
                        // the bottom-right ownership point for subsampled 4x4 luma leaves.
                        TOperator.PrepareChromaFromLuma(
                            this.reconstruction.GetPlane(Av1Plane.Y),
                            chromaLumaOrigin,
                            lumaQ3,
                            transformSize,
                            this.GetChromaFromLumaExtent(
                                macroBlock, lumaOrigin, blockSize, modeInfo.Block.TransformSize, subsamplingX, subsamplingY),
                            colorConfig.SubSamplingX,
                            colorConfig.SubSamplingY);

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
                            workspace.Residual,
                            this.blockWorkspace.TransformCoefficients,
                            this.blockWorkspace.TransformWorkspace);

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
                            workspace.Residual,
                            this.blockWorkspace.TransformCoefficients,
                            this.blockWorkspace.TransformWorkspace);

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

                                int headerRate = writer.GetChromaModeCost(Av1ChromaPredictionMode.ChromaFromLuma, true, lumaMode) +
                                    writer.GetChromaFromLumaCost(packedIndex, jointSign);

                                evaluateAlpha = Av1RateDistortion.GetCost(this.rateMultiplier, headerRate, 0) <= bestStatistics.Cost;
                            }
                        }

                        int radius = speedSettings.ChromaFromLumaSearchRange - 1;
                        int firstBlueCandidate = Math.Max(0, estimatedBlueCandidate - radius);
                        int lastBlueCandidate = Math.Min(Av1ChromaFromLumaMath.AlphaCandidateCount, estimatedBlueCandidate + radius + 1);
                        int firstRedCandidate = Math.Max(0, estimatedRedCandidate - radius);
                        int lastRedCandidate = Math.Min(Av1ChromaFromLumaMath.AlphaCandidateCount, estimatedRedCandidate + radius + 1);
                        for (int alphaCandidateIndex = firstBlueCandidate;
                            evaluateAlpha && alphaCandidateIndex < lastBlueCandidate;
                            alphaCandidateIndex++)
                        {
                            int alphaQ3 = Av1ChromaFromLumaMath.CandidateIndexToAlpha(alphaCandidateIndex);
                            Av1EncoderTransformBlockState candidateBlueState = default;
                            blueDistortions[alphaCandidateIndex] = this.GetChromaFromLumaPlaneCost(
                                writer,
                                lumaMode,
                                Av1Plane.U,
                                chromaOrigin,
                                transformSize,
                                blueSource,
                                blueDc,
                                blueContext,
                                lumaQ3,
                                alphaQ3,
                                candidateBlueReconstruction[..sampleCount],
                                candidateBlueCoefficients[..sampleCount],
                                ref candidateBlueState,
                                out blueRates[alphaCandidateIndex]);
                        }

                        for (int alphaCandidateIndex = firstRedCandidate;
                            evaluateAlpha && alphaCandidateIndex < lastRedCandidate;
                            alphaCandidateIndex++)
                        {
                            int alphaQ3 = Av1ChromaFromLumaMath.CandidateIndexToAlpha(alphaCandidateIndex);
                            Av1EncoderTransformBlockState candidateRedState = default;
                            redDistortions[alphaCandidateIndex] = this.GetChromaFromLumaPlaneCost(
                                writer,
                                lumaMode,
                                Av1Plane.V,
                                chromaOrigin,
                                transformSize,
                                redSource,
                                redDc,
                                redContext,
                                lumaQ3,
                                alphaQ3,
                                candidateRedReconstruction[..sampleCount],
                                candidateRedCoefficients[..sampleCount],
                                ref candidateRedState,
                                out redRates[alphaCandidateIndex]);
                        }

                        int chromaFromLumaModeRate = Av1TileWriter.GetChromaModeCost(
                            writer,
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
                                    writer.GetChromaFromLumaCost(packedIndex, jointSign);

                                int rate = chromaFromLumaModeRate + residualRate;

                                long distortion = blueDistortions[blueCandidateIndex] + redDistortions[redCandidateIndex];
                                Av1RateDistortionStatistics candidateStatistics = new(this.rateMultiplier, rate, distortion)
                                {
                                    ResidualRate = residualRate
                                };

                                Entropy.Av1SymbolWriter.DiagnosticSymbolTrace?.Add(
                                    $"UVCFL {lumaOrigin.X},{lumaOrigin.Y} {blockSize} ymode {(int)lumaMode} cfl {packedIndex}:{jointSign} rate {rate} resrate {residualRate} dist {distortion} cost {candidateStatistics.Cost} best {bestStatistics.Cost}");

                                if (candidateStatistics.Cost < bestStatistics.Cost)
                                {
                                    bestStatistics = candidateStatistics;
                                    bestMode = Av1ChromaPredictionMode.ChromaFromLuma;
                                    selectedAngleDelta = 0;
                                    selectedBlueCandidateIndex = blueCandidateIndex;
                                    selectedRedCandidateIndex = redCandidateIndex;
                                    selectedChromaFromLumaIndex = (byte)packedIndex;
                                    selectedChromaFromLumaSigns = (sbyte)jointSign;
                                    chromaFromLumaSelected = true;
                                }
                            }
                        }

                        if (chromaFromLumaSelected)
                        {
                            // The alpha tables retain only rate and distortion. Regenerate the two selected planes
                            // once here instead of copying reconstruction and coefficient blocks for all 66 trials.
                            Av1EncoderTransformBlockState candidateBlueState = default;
                            _ = this.GetChromaFromLumaPlaneCost(
                                writer,
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
                                candidateBlueCoefficients[..sampleCount],
                                ref candidateBlueState,
                                out _);

                            CopyCandidate(
                                candidateBlueReconstruction,
                                candidateBlueCoefficients,
                                blueReconstruction,
                                chromaOrigin,
                                retainedBlueCoefficients,
                                transformSize,
                                candidateBlueState,
                                ref retainedBlueStates[0]);

                            Av1EncoderTransformBlockState candidateRedState = default;
                            _ = this.GetChromaFromLumaPlaneCost(
                                writer,
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
                                candidateRedCoefficients[..sampleCount],
                                ref candidateRedState,
                                out _);

                            CopyCandidate(
                                candidateRedReconstruction,
                                candidateRedCoefficients,
                                redReconstruction,
                                chromaOrigin,
                                retainedRedCoefficients,
                                transformSize,
                                candidateRedState,
                                ref retainedRedStates[0]);
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

                int modeRate = writer.GetChromaModeCost(chromaMode, chromaFromLumaAllowed, lumaMode);
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
                        chromaOrigin,
                        transformSize,
                        blueSource,
                        redSource,
                        blueAbove,
                        blueLeft,
                        redAbove,
                        redLeft,
                        hasLeft,
                        hasAbove,
                        this.UseSmoothIntraEdges(macroBlock, lumaOrigin, blockSize, Av1Plane.U),
                        blueContext,
                        redContext,
                        paletteDisabledCost,
                        costLimit,
                        candidateBlueReconstruction[..sampleCount],
                        candidateRedReconstruction[..sampleCount],
                        candidateBlueCoefficients[..sampleCount],
                        candidateRedCoefficients[..sampleCount],
                        ref candidateBlueState,
                        ref candidateRedState);

                    if (angleDelta == 0 && candidateStatistics.Cost == long.MaxValue)
                    {
                        break;
                    }

                    Entropy.Av1SymbolWriter.DiagnosticSymbolTrace?.Add(
                        $"UVMODE {lumaOrigin.X},{lumaOrigin.Y} {blockSize} ymode {(int)lumaMode} uvmode {(int)chromaMode} angle {angleDelta} rate {candidateStatistics.Rate} resrate {candidateStatistics.ResidualRate} dist {candidateStatistics.Distortion} cost {candidateStatistics.Cost} best {bestStatistics.Cost}");

                    angleCosts[angleDelta + 3] = candidateStatistics.Cost;
                    if (candidateStatistics.Cost < bestStatistics.Cost)
                    {
                        CopyCandidate(
                            candidateBlueReconstruction,
                            candidateBlueCoefficients,
                            blueReconstruction,
                            chromaOrigin,
                            retainedBlueCoefficients,
                            transformSize,
                            candidateBlueState,
                            ref retainedBlueStates[0]);

                        CopyCandidate(
                            candidateRedReconstruction,
                            candidateRedCoefficients,
                            redReconstruction,
                            chromaOrigin,
                            retainedRedCoefficients,
                            transformSize,
                            candidateRedState,
                            ref retainedRedStates[0]);

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
            Span<int> retainedBlueCoefficients,
            Span<int> retainedRedCoefficients,
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

            Av1NeighborArrayUnit<byte> blueNeighbors =
                this.picture.CbDcSignLevelCoefficientNeighbors[tileIndex];

            Av1NeighborArrayUnit<byte> redNeighbors =
                this.picture.CrDcSignLevelCoefficientNeighbors[tileIndex];

            int blueTopIndex = blueNeighbors.GetTopIndex(chromaOrigin);
            int blueLeftIndex = blueNeighbors.GetLeftIndex(chromaOrigin);
            int redTopIndex = redNeighbors.GetTopIndex(chromaOrigin);
            int redLeftIndex = redNeighbors.GetLeftIndex(chromaOrigin);
            Buffer2DRegion<TSample> blueSource = this.source.GetPlane(Av1Plane.U);
            Buffer2DRegion<TSample> redSource = this.source.GetPlane(Av1Plane.V);
            Buffer2DRegion<TSample> blueReconstruction = this.reconstruction.GetPlane(Av1Plane.U);
            Buffer2DRegion<TSample> redReconstruction = this.reconstruction.GetPlane(Av1Plane.V);
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

            bool hasLumaPalette = paletteInfo.PaletteSizes[0] != 0;
            int paletteDisabledCost = Av1TileWriter.IsPaletteAllowed(
                this.picture.Parent.FrameHeader.AllowScreenContentTools,
                blockSize)
                ? writer.GetPaletteUvModeCost(false, hasLumaPalette)
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

                int modeRate = writer.GetChromaModeCost(chromaMode, false, lumaMode);
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
                        blueReconstruction,
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
                        redReconstruction,
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
                        writer,
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

                    Entropy.Av1SymbolWriter.DiagnosticSymbolTrace?.Add(
                        $"UVTILED {lumaOrigin.X},{lumaOrigin.Y} {blockSize} ymode {(int)lumaMode} uvmode {(int)chromaMode} angle {angleDelta} rate {candidateStatistics.Rate} resrate {candidateStatistics.ResidualRate} dist {candidateStatistics.Distortion} cost {candidateStatistics.Cost} best {bestStatistics.Cost}");

                    angleCosts[angleDelta + 3] = candidateStatistics.Cost;
                    if (candidateStatistics.Cost < bestStatistics.Cost)
                    {
                        CopyTiledCandidate(
                            candidateBlueReconstruction,
                            candidateBlueCoefficients,
                            candidateBlueStates,
                            blueReconstruction,
                            chromaOrigin,
                            blockWidth,
                            GetCodedTransformExtent(macroBlock, chromaBlockSize, transformSize, subsamplingX, subsamplingY),
                            transformSize,
                            retainedBlueCoefficients,
                            retainedBlueStates);

                        CopyTiledCandidate(
                            candidateRedReconstruction,
                            candidateRedCoefficients,
                            candidateRedStates,
                            redReconstruction,
                            chromaOrigin,
                            blockWidth,
                            GetCodedTransformExtent(macroBlock, chromaBlockSize, transformSize, subsamplingX, subsamplingY),
                            transformSize,
                            retainedRedCoefficients,
                            retainedRedStates);

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
        /// Returns the per-sample variance of a chroma source block around the mid-gray level.
        /// </summary>
        /// <param name="source">The chroma source plane.</param>
        /// <param name="origin">The block origin in chroma samples.</param>
        /// <param name="width">The block width.</param>
        /// <param name="height">The block height.</param>
        /// <param name="bitDepth">The coded sample precision.</param>
        /// <returns>The rounded per-sample variance.</returns>
        private static int GetSourceVariance(
            Buffer2DRegion<TSample> source,
            Point origin,
            int width,
            int height,
            Av1BitDepth bitDepth)
        {
            Span<TSample> midpoint = stackalloc TSample[width];
            return GetPerPixelVariance(source, origin, width, height, bitDepth, midpoint);
        }

        private long GetTiledPlaneCost(
            Av1SymbolEncoder writer,
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
            Buffer2DRegion<TSample> source,
            Buffer2DRegion<TSample> reconstruction,
            ReadOnlySpan<ushort> paletteColors,
            Buffer2DRegion<byte> colorIndexMap,
            Span<TSample> candidateReconstruction,
            Span<int> candidateCoefficients,
            Span<Av1EncoderTransformBlockState> candidateStates,
            Span<byte> topContexts,
            Span<byte> leftContexts,
            long costLimit,
            out long predictionDistortion,
            out int rate)
        {
            Av1EncoderModeDecisionWorkspace<TSample> workspace =
                this.blockWorkspace.GetModeDecisionWorkspace<TSample>();

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
            int coefficientOffset = 0;
            int transformIndex = 0;
            long distortion = 0;
            long accumulatedCost = 0;
            predictionDistortion = 0;
            rate = 0;

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
                                    reconstruction,
                                    lumaOrigin,
                                    chromaOrigin,
                                    blockSize,
                                    macroBlock,
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
                                    this.blockWorkspace,
                                    source,
                                    transformOrigin,
                                    prediction,
                                    transformSize.GetWidth(),
                                    aboveStorage.Slice(1, transformWidth + transformHeight),
                                    leftStorage.Slice(1, transformWidth + transformHeight),
                                    hasLeft,
                                    hasAbove,
                                    predictionMode,
                                    angleDelta,
                                    this.picture.Sequence.SequenceHeader.EnableIntraEdgeFilter,
                                    this.UseSmoothIntraEdges(macroBlock, lumaOrigin, blockSize, plane),
                                    residual,
                                    transformSize,
                                    this.bitDepth);
                            }
                            else
                            {
                                // Each residual transform borrows exactly its part of the block's index map.
                                // Palette prediction needs no neighboring reconstructed reference samples.
                                TOperator.PreparePalette(
                                    source,
                                    transformOrigin,
                                    paletteColors,
                                    colorIndexMap.GetSubRegion(columnOffset, rowOffset, transformWidth, transformHeight),
                                    prediction,
                                    residual,
                                    transformSize);
                            }

                            Av1TransformBlockContext blockContext = Av1TileWriter.GetTransformBlockContexts(
                                componentType,
                                topContexts.Slice(transformColumn * transformWidth4x4, transformWidth4x4),
                                leftContexts.Slice(transformRow * transformHeight4x4, transformHeight4x4),
                                chromaBlockSize,
                                transformSize);

                            Span<int> transformCoefficients = candidateCoefficients.Slice(
                                coefficientOffset,
                                transformSampleCount);

                            ref Av1EncoderTransformBlockState state = ref candidateStates[transformIndex++];
                            long transformDistortion = TOperator.EncodePredictionCandidate(
                                this.blockWorkspace,
                                writer,
                                blockContext,
                                this.rateMultiplier,
                                false,
                                this.picture.Sequence.SequenceHeader.IsStillPicture,
                                source,
                                transformOrigin,
                                prediction,
                                residual,
                                transformSize.GetWidth(),
                                candidateReconstruction[reconstructionOffset..],
                                blockWidth,
                                transformCoefficients,
                                transformSize,
                                transformType,
                                plane,
                                this.superblockQIndex,
                                this.quantization.DeltaQDc[(int)plane],
                                this.quantization.DeltaQAc[(int)plane],
                                this.bitDepth,
                                ref state,
                                out long transformSse);

                            // The uncoded cost uses the energy the transform search reports, which is measured in
                            // the transform domain whenever the distortion is. Reference: the sse of search_tx_type().
                            predictionDistortion += transformSse;
                            int transformRate = writer.GetCoefficientCost(
                                transformSize,
                                transformType,
                                lumaMode,
                                transformCoefficients,
                                componentType,
                                blockContext,
                                state.EndOfBlock,
                                this.picture.Parent.FrameHeader.UseReducedTransformSet,
                                Av1FilterIntraMode.AllFilterIntraModes,
                                usesInterTransformSet: false);

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
                                transformCoefficients,
                                transformSize,
                                transformType,
                                state.EndOfBlock);

                            Av1TileWriter.UpdateCoefficientContexts(
                                topContexts.Slice(transformColumn * transformWidth4x4, transformWidth4x4),
                                leftContexts.Slice(transformRow * transformHeight4x4, transformHeight4x4),
                                coefficientContext,
                                transformOrigin,
                                frameContextSize);

                            coefficientOffset += transformSampleCount;
                        }
                    }
                }
            }

            return distortion;
        }

        private static void CopyTiledCandidate(
            ReadOnlySpan<TSample> candidateReconstruction,
            ReadOnlySpan<int> candidateCoefficients,
            ReadOnlySpan<Av1EncoderTransformBlockState> candidateStates,
            Buffer2DRegion<TSample> reconstruction,
            Point blockOrigin,
            int blockWidth,
            Size codedExtent,
            Av1TransformSize transformSize,
            Span<int> retainedCoefficients,
            Span<Av1EncoderTransformBlockState> retainedStates)
        {
            int blockSampleCount = codedExtent.Width * codedExtent.Height;
            int transformStateStride =
                transformSize.GetSize2d() /
                Av1EncoderCoefficientBuffer.TransformBlockUnitCoefficientCount;

            candidateCoefficients[..blockSampleCount].CopyTo(retainedCoefficients);
            int transformCount = blockSampleCount / transformSize.GetSize2d();
            for (int transformIndex = 0; transformIndex < transformCount; transformIndex++)
            {
                retainedStates[transformIndex * transformStateStride] = candidateStates[transformIndex];
            }

            Span<TSample> destination = Av1TransformBlockEncoder.GetPlaneSpan(reconstruction, blockOrigin);
            for (int row = 0; row < codedExtent.Height; row++)
            {
                candidateReconstruction.Slice(row * blockWidth, codedExtent.Width)
                    .CopyTo(destination.Slice(row * reconstruction.Stride, codedExtent.Width));
            }
        }

        private long GetChromaFromLumaPlaneCost(
            Av1SymbolEncoder writer,
            Av1PredictionMode lumaMode,
            Av1Plane plane,
            Point chromaOrigin,
            Av1TransformSize transformSize,
            Buffer2DRegion<TSample> source,
            TSample dc,
            Av1TransformBlockContext context,
            ReadOnlySpan<short> lumaQ3,
            int alphaQ3,
            Span<TSample> reconstruction,
            Span<int> coefficients,
            ref Av1EncoderTransformBlockState state,
            out int rate)
        {
            long distortion = TOperator.EncodeChromaFromLumaCandidate(
                this.blockWorkspace,
                writer,
                context,
                this.rateMultiplier,
                this.picture.Sequence.SequenceHeader.IsStillPicture,
                source,
                chromaOrigin,
                reconstruction,
                dc,
                lumaQ3,
                alphaQ3,
                coefficients,
                transformSize,
                plane,
                this.superblockQIndex,
                this.quantization.DeltaQDc[(int)plane],
                this.quantization.DeltaQAc[(int)plane],
                this.bitDepth,
                ref state);

            rate = writer.GetCoefficientCost(
                transformSize,
                Av1TransformType.DctDct,
                lumaMode,
                coefficients,
                Av1ComponentType.Chroma,
                context,
                state.EndOfBlock,
                this.picture.Parent.FrameHeader.UseReducedTransformSet,
                Av1FilterIntraMode.AllFilterIntraModes,
                usesInterTransformSet: false);

            return distortion;
        }

        private static int FindBestChromaFromLumaEstimate(
            Av1EncoderBlockWorkspace blockWorkspace,
            Av1Plane plane,
            Buffer2DRegion<TSample> source,
            Point chromaOrigin,
            TSample dc,
            ReadOnlySpan<short> lumaQ3,
            Av1TransformSize transformSize,
            Av1BitDepth bitDepth,
            Span<TSample> prediction,
            Span<short> residual,
            Span<int> coefficients,
            Span<int> transformWorkspace)
        {
            long workStart = Av1WorkCounters.Start();
            int workResult = FindBestChromaFromLumaEstimateCore(blockWorkspace, plane, source, chromaOrigin, dc, lumaQ3, transformSize, bitDepth, prediction, residual, coefficients, transformWorkspace);
            Av1WorkCounters.Stop(Av1WorkCounters.CflEstimate, workStart);
            return workResult;
        }

        private static int FindBestChromaFromLumaEstimateCore(
            Av1EncoderBlockWorkspace blockWorkspace,
            Av1Plane plane,
            Buffer2DRegion<TSample> source,
            Point chromaOrigin,
            TSample dc,
            ReadOnlySpan<short> lumaQ3,
            Av1TransformSize transformSize,
            Av1BitDepth bitDepth,
            Span<TSample> prediction,
            Span<short> residual,
            Span<int> coefficients,
            Span<int> transformWorkspace)
        {
            int sampleCount = transformSize.GetSize2d();
            long bestCost = long.MaxValue;
            int bestCandidate = Av1ChromaFromLumaMath.AlphaZeroIndex;

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
                    TOperator.ApplyChromaFromLuma(
                        lumaQ3,
                        prediction,
                        Av1ChromaFromLumaMath.CandidateIndexToAlpha(candidate),
                        transformSize,
                        bitDepth);

                    TOperator.SubtractPrediction(source, chromaOrigin, prediction, residual, transformSize.GetWidth(), transformSize.GetHeight());

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

        private Av1RateDistortionStatistics GetChromaCandidateCost(
            Av1SymbolEncoder writer,
            Av1MacroBlockModeInfo modeInfo,
            Av1PredictionMode lumaMode,
            Av1ChromaPredictionMode chromaMode,
            int angleDelta,
            Av1BlockSize blockSize,
            Point chromaOrigin,
            Av1TransformSize transformSize,
            Buffer2DRegion<TSample> blueSource,
            Buffer2DRegion<TSample> redSource,
            ReadOnlySpan<TSample> blueAbove,
            ReadOnlySpan<TSample> blueLeft,
            ReadOnlySpan<TSample> redAbove,
            ReadOnlySpan<TSample> redLeft,
            bool hasLeft,
            bool hasAbove,
            bool smoothIntraEdges,
            Av1TransformBlockContext blueContext,
            Av1TransformBlockContext redContext,
            int paletteDisabledCost,
            long costLimit,
            Span<TSample> candidateBlueReconstruction,
            Span<TSample> candidateRedReconstruction,
            Span<int> candidateBlueCoefficients,
            Span<int> candidateRedCoefficients,
            ref Av1EncoderTransformBlockState candidateBlueState,
            ref Av1EncoderTransformBlockState candidateRedState)
        {
            long workStart = Av1WorkCounters.Start();
            Av1RateDistortionStatistics workResult = this.GetChromaCandidateCostCore(writer, modeInfo, lumaMode, chromaMode, angleDelta, blockSize, chromaOrigin, transformSize, blueSource, redSource, blueAbove, blueLeft, redAbove, redLeft, hasLeft, hasAbove, smoothIntraEdges, blueContext, redContext, paletteDisabledCost, costLimit, candidateBlueReconstruction, candidateRedReconstruction, candidateBlueCoefficients, candidateRedCoefficients, ref candidateBlueState, ref candidateRedState);
            Av1WorkCounters.Stop(Av1WorkCounters.ChromaCandidate, workStart);
            return workResult;
        }

        private Av1RateDistortionStatistics GetChromaCandidateCostCore(
            Av1SymbolEncoder writer,
            Av1MacroBlockModeInfo modeInfo,
            Av1PredictionMode lumaMode,
            Av1ChromaPredictionMode chromaMode,
            int angleDelta,
            Av1BlockSize blockSize,
            Point chromaOrigin,
            Av1TransformSize transformSize,
            Buffer2DRegion<TSample> blueSource,
            Buffer2DRegion<TSample> redSource,
            ReadOnlySpan<TSample> blueAbove,
            ReadOnlySpan<TSample> blueLeft,
            ReadOnlySpan<TSample> redAbove,
            ReadOnlySpan<TSample> redLeft,
            bool hasLeft,
            bool hasAbove,
            bool smoothIntraEdges,
            Av1TransformBlockContext blueContext,
            Av1TransformBlockContext redContext,
            int paletteDisabledCost,
            long costLimit,
            Span<TSample> candidateBlueReconstruction,
            Span<TSample> candidateRedReconstruction,
            Span<int> candidateBlueCoefficients,
            Span<int> candidateRedCoefficients,
            ref Av1EncoderTransformBlockState candidateBlueState,
            ref Av1EncoderTransformBlockState candidateRedState)
        {
            Av1WorkCounters.Count(Av1WorkCounters.ChromaUvrd);
            Av1PredictionMode predictionMode = chromaMode.ToLumaMode();

            // Intra chroma derives one transform type from the shared UV prediction mode. The type is not
            // signaled independently for either chroma plane, so U and V must use the same legal fallback.
            Av1TransformType transformType = this.picture.Parent.FrameHeader.CodedLossless
                ? Av1TransformType.DctDct
                : Av1SymbolContextHelper.GetDefaultIntraTransformType(
                    predictionMode,
                    transformSize,
                    this.picture.Parent.FrameHeader.UseReducedTransformSet);

            // search_tx_type is shared by both planes, so chroma follows the same distortion policy as luma.
            // The policy belongs to the active mode evaluation stage.
            (int Type, uint Threshold) distortionPolicy = Av1TransformBlockEncoder.GetDistortionPolicy(
                this.blockWorkspace, this.picture.Parent.SpeedSettings);

            long distortion = TOperator.EncodeCandidate(
                this.blockWorkspace,
                writer,
                blueContext,
                this.rateMultiplier,
                this.picture.Sequence.SequenceHeader.IsStillPicture,
                blueSource,
                chromaOrigin,
                candidateBlueReconstruction,
                blueAbove,
                blueLeft,
                hasLeft,
                hasAbove,
                predictionMode,
                angleDelta,
                this.picture.Sequence.SequenceHeader.EnableIntraEdgeFilter,
                smoothIntraEdges,
                candidateBlueCoefficients,
                transformSize,
                transformType,
                Av1Plane.U,
                this.superblockQIndex,
                this.quantization.DeltaQDc[(int)Av1Plane.U],
                this.quantization.DeltaQAc[(int)Av1Plane.U],
                this.bitDepth,
                distortionPolicy,
                ref candidateBlueState,
                out long blueSse);

            int blueRate = writer.GetCoefficientCost(
                transformSize,
                transformType,
                lumaMode,
                candidateBlueCoefficients,
                Av1ComponentType.Chroma,
                blueContext,
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

            long redDistortion = TOperator.EncodeCandidate(
                this.blockWorkspace,
                writer,
                redContext,
                this.rateMultiplier,
                this.picture.Sequence.SequenceHeader.IsStillPicture,
                redSource,
                chromaOrigin,
                candidateRedReconstruction,
                redAbove,
                redLeft,
                hasLeft,
                hasAbove,
                predictionMode,
                angleDelta,
                this.picture.Sequence.SequenceHeader.EnableIntraEdgeFilter,
                smoothIntraEdges,
                candidateRedCoefficients,
                transformSize,
                transformType,
                Av1Plane.V,
                this.superblockQIndex,
                this.quantization.DeltaQDc[(int)Av1Plane.V],
                this.quantization.DeltaQAc[(int)Av1Plane.V],
                this.bitDepth,
                distortionPolicy,
                ref candidateRedState,
                out long redSse);

            predictionDistortion += redSse;

            // The mode and angle are written once for the UV pair; coefficient syntax remains independent
            // because each plane has its own EOB, scan values, and neighboring coefficient context.
            int rate = Av1TileWriter.GetChromaModeCost(
                writer,
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
                transformSize,
                transformType,
                lumaMode,
                candidateRedCoefficients,
                Av1ComponentType.Chroma,
                redContext,
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
            Buffer2DRegion<TSample> reconstructionPlane,
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
            if (hasAbove)
            {
                ReadOnlySpan<TSample> topRow = reconstructionPlane.DangerousGetRowSpan(blockOrigin.Y - 1);
                int visibleTopCount = Math.Min(width, topRow.Length - blockOrigin.X);
                topRow.Slice(blockOrigin.X, visibleTopCount).CopyTo(above);
                above[visibleTopCount..width].Fill(above[visibleTopCount - 1]);
            }

            if (hasLeft)
            {
                int visibleLeftCount = Math.Min(height, reconstructionPlane.Height - blockOrigin.Y);
                for (int row = 0; row < visibleLeftCount; row++)
                {
                    left[row] = reconstructionPlane.DangerousGetRowSpan(blockOrigin.Y + row)[blockOrigin.X - 1];
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
                reconstructionPlane.DangerousGetRowSpan(blockOrigin.Y - 1)
                    .Slice(blockOrigin.X + width, topRightCount)
                    .CopyTo(above[width..]);
            }

            int topCount = width + topRightCount;
            above[topCount..].Fill(above[topCount - 1]);

            int bottomLeftCount = hasBottomLeft
                ? Math.Min(Math.Min(height, width), reconstructionPlane.Height - blockOrigin.Y - height)
                : 0;

            for (int row = height; row < height + bottomLeftCount; row++)
            {
                left[row] = reconstructionPlane.DangerousGetRowSpan(blockOrigin.Y + row)[blockOrigin.X - 1];
            }

            int leftCount = height + bottomLeftCount;
            left[leftCount..].Fill(left[leftCount - 1]);

            // Zone-two projection and Paeth address the common corner immediately before both edges.
            // Missing edges derive it from the closest coded sample or the bit-depth midpoint.
            TSample corner = hasAbove && hasLeft
                ? reconstructionPlane.DangerousGetRowSpan(blockOrigin.Y - 1)[blockOrigin.X - 1]
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

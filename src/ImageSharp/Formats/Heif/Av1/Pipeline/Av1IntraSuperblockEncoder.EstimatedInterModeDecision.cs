// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <content>
/// Provides prediction-based inter mode cost estimation.
/// </content>
internal static partial class Av1IntraSuperblockEncoder
{
    internal partial struct ModeDecision<TSample, TOperator>
        where TSample : unmanaged
        where TOperator : struct, IBlockEncodingOperator<TSample>
    {
        /// <summary>
        /// Selects and encodes a real-time inter-picture block using estimated candidate costs.
        /// </summary>
        /// <param name="writer">The current tile symbol costs.</param>
        /// <param name="macroBlock">The coding-block neighbors and frame edges.</param>
        /// <param name="origin">The luma block origin.</param>
        /// <param name="tileIndex">The containing tile context.</param>
        /// <param name="modeInfo">The selected block syntax.</param>
        /// <param name="block">The selected prediction-unit state.</param>
        /// <param name="palette">The retained palette, when selected.</param>
        private void EncodeEstimatedInterBlock(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point origin,
            ushort tileIndex,
            ref Av1MacroBlockModeInfo modeInfo,
            ref Av1EncoderBlockStruct block,
            ref Av1EncoderPaletteInfo palette)
        {
            Av1PictureParentControlSet parent = this.picture.Parent;
            Av1EncoderSpeedSettings settings = parent.SpeedSettings;
            Av1BlockSize blockSize = modeInfo.Block.BlockSize;
            ref Av1EstimatedInterSearchState state = ref this.blockWorkspace.EstimatedInterSearchState;
            state.Reset(
                writer,
                Av1TileWriter.GetIntraInterContext(macroBlock),
                Av1SymbolContextHelper.GetCompoundReferenceTypeContext(macroBlock),
                parent.FrameHeader.ReferenceMode == ObuReferenceMode.ReferenceModeSelect);

            bool forceZeroMotion = this.CanSkipEstimatedZeroMotionBlock(origin, blockSize);
            InlineArray2<byte> colorSensitivity = this.superblockColorSensitivity;
            bool measureSad = !forceZeroMotion &&
                (this.estimatedReferencePruning <= 2 || colorSensitivity[0] == 2 || colorSensitivity[1] == 2);
            InlineArray3<Av1ReferenceMotionVectors> referenceVectors = default;
            this.PrepareEstimatedReference(
                writer,
                macroBlock,
                origin,
                blockSize,
                modeInfo.Block.PartitionType,
                Av1ReferenceFrameType.Last,
                this.reference.GetPlane(Av1Plane.Y),
                measureSad,
                ref referenceVectors[0]);

            bool lowTemporalVariance = settings.UseEstimatedLowTemporalVariance && this.estimatedReferencePruning != 0 &&
                this.HasLowTemporalVariance(origin, blockSize);
            bool useGolden = this.hasDistinctGoldenReference;
            bool useAlternate = (parent.AvailableReferenceMask & (1 << (int)Av1ReferenceFrameType.Alternate)) != 0 &&
                (parent.EncodingSpeed == HeifEncodingSpeed.Level7 ||
                    (parent.EncodingSpeed == HeifEncodingSpeed.Level8 && Math.Min(this.source.Width, this.source.Height) >= 360));
            if (lowTemporalVariance || this.estimatedReferencePruning > 2 || forceZeroMotion ||
                (this.estimatedReferencePruning > 1 && blockSize > Av1BlockSize.Block64x64))
            {
                useGolden = false;
                useAlternate = false;
            }

            if ((parent.IsScreenContent && this.estimatedReferencePruning != 0) ||
                (this.interSourceVariance < 200 && this.sourceSadLevel >= Av1SourceSadLevel.Low))
            {
                useGolden &= this.goldenColorSensitivity[0] != 1 && this.goldenColorSensitivity[1] != 1;
                useAlternate &= this.alternateColorSensitivity[0] != 1 && this.alternateColorSensitivity[1] != 1;
            }

            if (!parent.IsScreenContent && !useGolden && !useAlternate && this.estimatedReferencePruning > 2 &&
                state.PredictorSad[(int)Av1ReferenceFrameType.Last] != int.MaxValue &&
                this.goldenColorSensitivity[0] == 0 && this.goldenColorSensitivity[1] == 0)
            {
                int normalizedSad = state.PredictorSad[(int)Av1ReferenceFrameType.Last] >>
                    (BitOperations.Log2((uint)(blockSize.GetWidth() * blockSize.GetHeight())) - 4);
                int threshold = parent.FrameHeader.FrameSize.FrameWidth * parent.FrameHeader.FrameSize.FrameHeight > 352 * 288 ? 100 : 150;
                useGolden = this.hasDistinctGoldenReference && normalizedSad > threshold;
            }

            state.UseReference[(int)Av1ReferenceFrameType.Last] = (parent.AvailableReferenceMask & (1 << (int)Av1ReferenceFrameType.Last)) != 0;
            state.UseReference[(int)Av1ReferenceFrameType.Alternate] = useAlternate;
            state.UseReference[(int)Av1ReferenceFrameType.Golden] = useGolden;
            if (useGolden)
            {
                this.PrepareEstimatedReference(
                    writer,
                    macroBlock,
                    origin,
                    blockSize,
                    modeInfo.Block.PartitionType,
                    Av1ReferenceFrameType.Golden,
                    this.goldenReference.GetPlane(Av1Plane.Y),
                    measureSad,
                    ref referenceVectors[1]);
            }

            if (useAlternate)
            {
                this.PrepareEstimatedReference(
                    writer,
                    macroBlock,
                    origin,
                    blockSize,
                    modeInfo.Block.PartitionType,
                    Av1ReferenceFrameType.Alternate,
                    this.references.Span[(int)Av1ReferenceFrameType.Alternate].CodedView.GetPlane(Av1Plane.Y),
                    measureSad,
                    ref referenceVectors[2]);
            }

            bool blockZeroSad = this.sourceSadLevel == Av1SourceSadLevel.Zero;
            if (parent.IsScreenContent && !forceZeroMotion && !blockZeroSad && this.interSourceVariance == 0 &&
                blockSize < this.picture.Sequence.SequenceHeader.SuperblockSize)
            {
                Buffer2DRegion<TSample> sourcePlane = this.source.GetPlane(Av1Plane.Y);
                Buffer2DRegion<TSample> referencePlane = this.reference.GetPlane(Av1Plane.Y);
                blockZeroSad = TOperator.SumAbsoluteDifferences(
                    Av1TransformBlockEncoder.GetPlaneSpan(sourcePlane, origin),
                    sourcePlane.Stride,
                    Av1TransformBlockEncoder.GetPlaneSpan(referencePlane, origin),
                    referencePlane.Stride,
                    blockSize.GetWidth(),
                    blockSize.GetHeight(),
                    1) == 0;
            }

            if (block.HasChroma && !forceZeroMotion)
            {
                if (parent.IsScreenContent && useGolden)
                {
                    for (int plane = 0; plane < 2; plane++)
                    {
                        if (this.goldenColorSensitivity[plane] == 1)
                        {
                            colorSensitivity[plane] = 1;
                        }
                    }
                }

                int lumaSad = state.NearestSad[(int)Av1ReferenceFrameType.Last];
                if (lumaSad != int.MaxValue)
                {
                    Av1MotionVector nearest = state.MotionVectors[(int)Av1PredictionMode.NearestMotionVector][(int)Av1ReferenceFrameType.Last];
                    Av1MotionVector near = state.MotionVectors[(int)Av1PredictionMode.NearMotionVector][(int)Av1ReferenceFrameType.Last];
                    if (state.NearSad[(int)Av1ReferenceFrameType.Last] != int.MaxValue &&
                        Math.Abs(near.Row) + Math.Abs(near.Column) < Math.Abs(nearest.Row) + Math.Abs(nearest.Column))
                    {
                        lumaSad = state.NearSad[(int)Av1ReferenceFrameType.Last];
                    }

                    this.SetEstimatedColorSensitivity(origin, blockSize, lumaSad, false, colorSensitivity);
                }
            }

            bool evaluateBlue = block.HasChroma && colorSensitivity[0] != 0;
            bool evaluateRed = block.HasChroma && colorSensitivity[1] != 0;
            bool rejectStationaryScreen = this.interSourceVariance == 0 &&
                ((this.superblockColorSensitivity[0] == 0 && this.superblockColorSensitivity[1] == 0) || parent.HighSourceSad);
            int filterPolicy = this.GetEstimatedFilterSearchPolicy(macroBlock, origin, blockSize, false, out Av1InterpolationFilter filter);
            Av1EncoderInterPredictionWorkspace<TSample> workspace = this.blockWorkspace.GetInterPredictionWorkspace<TSample>();
            Span<TSample> winningPrediction = workspace.SelectedLumaReconstruction;
            Span<TSample> prediction = workspace.LumaPrediction;
            Span<TSample> scratch = workspace.LumaCandidateReconstruction;
            Av1EncoderBlockModeInfo winner = modeInfo.Block;
            winner.SecondaryReferenceFrame = Av1ReferenceFrameType.None;
            bool checkGlobalMotion = true;
            uint zeroMotionError = uint.MaxValue;
            for (int index = 0; index < 3 && !state.EndSearch; index++)
            {
                Av1ReferenceFrameType reference = index == 0 ? Av1ReferenceFrameType.Last
                    : index == 1 ? Av1ReferenceFrameType.Golden : Av1ReferenceFrameType.Alternate;
                if (state.UseReference[(int)reference])
                {
                    this.SelectEstimatedReferenceModes(
                        writer,
                        macroBlock,
                        origin,
                        reference,
                        this.references.Span[(int)reference].CodedView,
                        in referenceVectors[index],
                        forceZeroMotion,
                        lowTemporalVariance,
                        blockZeroSad,
                        rejectStationaryScreen,
                        evaluateBlue,
                        evaluateRed,
                        filterPolicy,
                        filter,
                        parent.FramesSinceGolden,
                        ref checkGlobalMotion,
                        ref zeroMotionError,
                        ref winner,
                        ref winningPrediction,
                        ref prediction,
                        ref scratch);
                }
            }

            if (!forceZeroMotion)
            {
                this.SelectEstimatedCompoundModes(
                    writer, macroBlock, origin, evaluateBlue, evaluateRed, ref winner, ref winningPrediction, ref prediction, ref scratch);
            }

            int normalizationShift = BitOperations.Log2((uint)(blockSize.GetWidth() * blockSize.GetHeight())) - 4;
            bool forcePalette = parent.IsScreenContent && this.sourceSadLevel != Av1SourceSadLevel.Zero &&
                blockSize <= Av1BlockSize.Block16x16 &&
                (state.BestStatistics.Cost == long.MaxValue ||
                    (state.BestStatistics.PredictionDistortion >> normalizationShift) > (parent.HighSourceSad ? 15000 : 100000)) &&
                this.interSourceVariance > (parent.HighSourceSad ? 50 : 200);
            if (!forceZeroMotion)
            {
                this.SelectEstimatedIntraModes(
                    writer,
                    macroBlock,
                    origin,
                    evaluateBlue,
                    evaluateRed,
                    state.BestEarlyTermination,
                    state.BestInitialSkip,
                    false,
                    ref winner,
                    ref winningPrediction,
                    ref prediction);
            }

            Av1TransformType transformType = Av1TransformType.DctDct;
            bool paletteSelected = this.SelectEstimatedScreenModes(
                writer,
                macroBlock,
                origin,
                tileIndex,
                forceZeroMotion,
                forcePalette,
                evaluateBlue,
                evaluateRed,
                ref winner,
                ref palette,
                winningPrediction,
                ref transformType);

            modeInfo.Block = winner;

            // Filter intra and the dynamic-reference-list entry share one packed byte, so only an
            // intra winner may carry the filter-intra sentinel. An inter winner of this path always
            // takes the first list entry.
            block.ReferenceMotionVectorIndex = 0;
            if (winner.ReferenceFrame <= Av1ReferenceFrameType.Intra)
            {
                block.FilterIntraMode = Av1FilterIntraMode.AllFilterIntraModes;
            }

            block.PredictionUnit.AngleDelta[0] = 0;
            block.PredictionUnit.AngleDelta[1] = 0;
            Point position = new(origin.X >> Av1Constants.ModeInfoSizeLog2, origin.Y >> Av1Constants.ModeInfoSizeLog2);
            if (winner.ReferenceFrame > Av1ReferenceFrameType.Intra)
            {
                Av1MotionVector vector = state.WinningMotionVectors[(int)winner.Mode][(int)winner.ReferenceFrame];
                Av1EncoderFrame<TSample>.PlanarView reference = this.references.Span[(int)winner.ReferenceFrame].CodedView;
                bool compound = winner.SecondaryReferenceFrame > Av1ReferenceFrameType.Intra;
                Av1MotionVector secondaryVector = compound
                    ? state.WinningMotionVectors[(int)winner.Mode][(int)winner.SecondaryReferenceFrame] : default;
                Av1EncoderFrame<TSample>.PlanarView secondaryReference = compound
                    ? this.references.Span[(int)winner.SecondaryReferenceFrame].CodedView : reference;
                this.EncodeEstimatedInterWinner(
                    writer,
                    macroBlock,
                    tileIndex,
                    origin,
                    ref modeInfo.Block,
                    block,
                    reference,
                    secondaryReference,
                    vector,
                    secondaryVector,
                    winningPrediction,
                    transformType);
                this.picture.SetDisplacementVector(position, vector);
                if (compound)
                {
                    this.picture.SetSecondaryDisplacementVector(position, secondaryVector);
                }
            }
            else
            {
                // Final intra prediction consumes reconstructed neighbors. The prediction-only candidate
                // must not be copied here, because each transform now contributes its selected residual.
                modeInfo.Block.Skip = winner.Skip && !parent.FrameHeader.CodedLossless;
                if (!paletteSelected)
                {
                    this.EncodeSelectedIntraPlane(
                        writer,
                        tileIndex,
                        macroBlock,
                        origin,
                        blockSize,
                        Av1Plane.Y,
                        winner.Mode,
                        winner.TransformSize,
                        this.codedAreaLuma,
                        modeInfo.Block.Skip);
                }

                if (block.HasChroma)
                {
                    Av1TransformSize chromaTransform = parent.FrameHeader.CodedLossless ? Av1TransformSize.Size4x4
                        : blockSize.GetMaxUvTransformSize(this.source.ChromaSubsamplingX != 0, this.source.ChromaSubsamplingY != 0);
                    this.EncodeSelectedIntraPlane(
                        writer,
                        tileIndex,
                        macroBlock,
                        origin,
                        blockSize,
                        Av1Plane.U,
                        (Av1PredictionMode)winner.UvMode,
                        chromaTransform,
                        this.codedAreaChroma,
                        modeInfo.Block.Skip);
                    this.EncodeSelectedIntraPlane(
                        writer,
                        tileIndex,
                        macroBlock,
                        origin,
                        blockSize,
                        Av1Plane.V,
                        (Av1PredictionMode)winner.UvMode,
                        chromaTransform,
                        this.codedAreaChroma,
                        modeInfo.Block.Skip);
                }
            }

            if (winner.SecondaryReferenceFrame == Av1ReferenceFrameType.None && settings.AdaptiveModeThresholdLevel != 0)
            {
                Av1ModeThresholds.UpdateEstimated(
                    this.blockWorkspace.ModeThresholdFactors, blockSize, winner.ReferenceFrame, winner.Mode, settings.AdaptiveModeThresholdLevel);
            }

            Size lumaExtent = GetCodedTransformExtent(macroBlock, blockSize, modeInfo.Block.TransformSize, 0, 0);
            this.codedAreaLuma += lumaExtent.Width * lumaExtent.Height;
            if (block.HasChroma)
            {
                int subX = this.source.ChromaSubsamplingX;
                int subY = this.source.ChromaSubsamplingY;
                Av1TransformSize chromaTransform = parent.FrameHeader.CodedLossless ? Av1TransformSize.Size4x4
                    : blockSize.GetMaxUvTransformSize(subX != 0, subY != 0);
                Size chromaExtent = GetCodedTransformExtent(
                    macroBlock, blockSize.GetSubsampled(subX != 0, subY != 0), chromaTransform, subX, subY);
                this.codedAreaChroma += chromaExtent.Width * chromaExtent.Height;
            }

            this.SelectedBlockStatistics = state.BestStatistics;
        }

        /// <summary>
        /// Compares enabled compound predictors after the single-reference candidates.
        /// </summary>
        /// <param name="writer">The current tile symbol costs.</param>
        /// <param name="macroBlock">The block neighbors and frame boundaries.</param>
        /// <param name="origin">The luma block origin.</param>
        /// <param name="evaluateBlue">Whether blue-difference error participates in selection.</param>
        /// <param name="evaluateRed">Whether red-difference error participates in selection.</param>
        /// <param name="winner">The retained prediction syntax.</param>
        /// <param name="winningPrediction">The retained luma predictor.</param>
        /// <param name="prediction">The next candidate's luma predictor.</param>
        /// <param name="scratch">The alternate interpolation predictor storage.</param>
        private void SelectEstimatedCompoundModes(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point origin,
            bool evaluateBlue,
            bool evaluateRed,
            ref Av1EncoderBlockModeInfo winner,
            ref Span<TSample> winningPrediction,
            ref Span<TSample> prediction,
            ref Span<TSample> scratch)
        {
            Av1PictureParentControlSet parent = this.picture.Parent;
            ref Av1EstimatedInterSearchState state = ref this.blockWorkspace.EstimatedInterSearchState;
            Av1BlockSize blockSize = winner.BlockSize;
            int minimumDimension = Math.Min(this.source.Width, this.source.Height);
            if (state.EndSearch || parent.IsScreenContent || blockSize <= Av1BlockSize.Block16x16 ||
                parent.FrameHeader.ReferenceMode == ObuReferenceMode.SingleReference ||
                (parent.EncodingSpeed >= HeifEncodingSpeed.Level9 && minimumDimension < 360) ||
                (parent.AvailableReferenceMask & (1 << (int)Av1ReferenceFrameType.Alternate)) == 0 ||
                !state.UseReference[(int)Av1ReferenceFrameType.Last] || state.SkipCompoundByVariance(blockSize))
            {
                return;
            }

            if (this.interSourceVariance < 50 &&
                (this.alternateColorSensitivity[0] == 1 || this.alternateColorSensitivity[1] == 1))
            {
                return;
            }

            Point position = new(origin.X >> Av1Constants.ModeInfoSizeLog2, origin.Y >> Av1Constants.ModeInfoSizeLog2);
            ref Av1ReferenceMotionVectors vectors = ref this.blockWorkspace.ReferenceMotionVectors;
            vectors.Build(
                this.picture,
                macroBlock,
                position,
                blockSize,
                winner.PartitionType,
                this.picture.Sequence.SequenceHeader,
                parent.FrameHeader,
                Av1ReferenceFrameType.Last,
                Av1ReferenceFrameType.Alternate);
            Av1EncoderFrame<TSample>.PlanarView alternate = this.references.Span[(int)Av1ReferenceFrameType.Alternate].CodedView;
            bool globalOnly = minimumDimension < 360 || parent.EncodingSpeed >= HeifEncodingSpeed.Level9;
            _ = this.GetEstimatedFilterSearchPolicy(macroBlock, origin, blockSize, true, out Av1InterpolationFilter filter);
            for (int index = 0; index < (globalOnly ? 1 : 2); index++)
            {
                Av1PredictionMode mode = index == 0 ? Av1PredictionMode.GlobalGlobalMotionVector : Av1PredictionMode.NearestNearestMotionVector;
                Av1MotionVector primaryVector = index == 0 ? default : vectors.GetCompoundNearestReference(0);
                Av1MotionVector secondaryVector = index == 0 ? default : vectors.GetCompoundNearestReference(1);
                if (index != 0 && primaryVector.IsZero && secondaryVector.IsZero)
                {
                    continue;
                }

                state.MotionVectors[(int)mode][(int)Av1ReferenceFrameType.Last] = primaryVector;
                state.MotionVectors[(int)mode][(int)Av1ReferenceFrameType.Alternate] = secondaryVector;
                if (parent.EncodingSpeed == HeifEncodingSpeed.Level7 &&
                    state.SkipCompoundBySingleResults(mode, Av1ReferenceFrameType.Last, Av1ReferenceFrameType.Alternate))
                {
                    continue;
                }

                Av1EncoderBlockModeInfo candidate = winner;
                candidate.Mode = mode;
                candidate.ReferenceFrame = Av1ReferenceFrameType.Last;
                candidate.SecondaryReferenceFrame = Av1ReferenceFrameType.Alternate;
                candidate.CompoundGroupIndex = false;
                candidate.CompoundIndex = true;
                candidate.CompoundType = Av1CompoundType.Average;
                candidate.HorizontalInterpolationFilter = filter;
                candidate.VerticalInterpolationFilter = filter;
                Av1RateDistortionStatistics statistics = this.EvaluateEstimatedInterCandidate(
                    writer,
                    macroBlock,
                    origin,
                    ref candidate,
                    ref primaryVector,
                    secondaryVector,
                    this.reference,
                    alternate,
                    false,
                    evaluateBlue,
                    evaluateRed,
                    false,
                    in vectors,
                    false,
                    out _,
                    ref prediction,
                    ref scratch,
                    out _,
                    out uint squaredError,
                    out _,
                    out bool initialSkip);
                if (statistics.Cost == long.MaxValue)
                {
                    continue;
                }

                // The estimate charges the compound mode and the primary reference branch. Complete
                // compound-reference syntax is emitted only after the winning mode has been selected.
                int rate = writer.GetInterCompoundModeCost(mode, vectors.ModeContext) +
                    state.ReferenceCosts[(int)Av1ReferenceFrameType.Last];
                Av1RateDistortionStatistics syntax = new(this.rateMultiplier, rate, 0);
                statistics.Add(this.rateMultiplier, in syntax);
                if (statistics.Cost < state.BestStatistics.Cost)
                {
                    state.BestStatistics = statistics;
                    state.BestSquaredError = squaredError;
                    state.BestInitialSkip = initialSkip;
                    state.BestEarlyTermination = false;
                    state.WinningMotionVectors[(int)mode][(int)Av1ReferenceFrameType.Last] = primaryVector;
                    state.WinningMotionVectors[(int)mode][(int)Av1ReferenceFrameType.Alternate] = secondaryVector;
                    candidate.Skip = statistics.AllTransformsEmpty;
                    winner = candidate;
                    Span<TSample> previous = winningPrediction;
                    winningPrediction = prediction;
                    prediction = previous;
                }
            }
        }

        /// <summary>
        /// Compares screen-content identity residuals and a luma palette with the retained mode.
        /// </summary>
        /// <param name="writer">The tile symbol costs.</param>
        /// <param name="macroBlock">The coding-block neighbors and edges.</param>
        /// <param name="origin">The luma coding-block origin.</param>
        /// <param name="tileIndex">The containing tile context.</param>
        /// <param name="forceZeroMotion">Whether stationary residual skipping has already been selected.</param>
        /// <param name="forcePalette">Whether poor inter prediction requires a palette trial.</param>
        /// <param name="evaluateBlue">Whether blue-difference error participates in selection.</param>
        /// <param name="evaluateRed">Whether red-difference error participates in selection.</param>
        /// <param name="winner">The retained prediction syntax.</param>
        /// <param name="palette">The selected palette and its colors.</param>
        /// <param name="prediction">The retained inter luma predictor.</param>
        /// <param name="transformType">The selected inter transform family.</param>
        /// <returns>Whether palette reconstruction replaced the retained mode.</returns>
        private bool SelectEstimatedScreenModes(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point origin,
            ushort tileIndex,
            bool forceZeroMotion,
            bool forcePalette,
            bool evaluateBlue,
            bool evaluateRed,
            ref Av1EncoderBlockModeInfo winner,
            ref Av1EncoderPaletteInfo palette,
            ReadOnlySpan<TSample> prediction,
            ref Av1TransformType transformType)
        {
            Av1PictureParentControlSet parent = this.picture.Parent;
            ref Av1EstimatedInterSearchState state = ref this.blockWorkspace.EstimatedInterSearchState;
            Av1BlockSize blockSize = winner.BlockSize;
            bool skipScreenModes = (evaluateBlue || evaluateRed) && this.sourceSadLevel != Av1SourceSadLevel.Zero &&
                !parent.HighSourceSad && parent.FrameSourceSad < 1000;
            if (parent.IsScreenContent && this.bitDepth == Av1BitDepth.EightBit && !skipScreenModes && !forceZeroMotion &&
                winner.ReferenceFrame > Av1ReferenceFrameType.Intra &&
                (parent.EncodingSpeed < HeifEncodingSpeed.Level9 ||
                    (blockSize <= Av1BlockSize.Block32x32 && !winner.Skip && this.interSourceVariance > 200)))
            {
                Av1EncoderInterPredictionWorkspace<TSample> workspace = this.blockWorkspace.GetInterPredictionWorkspace<TSample>();
                int width = blockSize.GetWidth();
                int height = blockSize.GetHeight();
                TOperator.SubtractPrediction(this.source.GetPlane(Av1Plane.Y), origin, prediction, workspace.Residual, width, height);
                Size extent = new(
                    width + (Math.Min(0, macroBlock.ToRightEdge) >> 3),
                    height + (Math.Min(0, macroBlock.ToBottomEdge) >> 3));

                Av1IntraModeEstimator.Estimate(
                    this.blockWorkspace,
                    workspace.Residual,
                    width,
                    extent,
                    winner.TransformSize,
                    this.quantization.QIndex[0],
                    this.quantization.DeltaQDc[0],
                    this.quantization.DeltaQAc[0],
                    this.bitDepth,
                    true,
                    out int rate,
                    out long distortion,
                    out bool skip);

                long lumaCost = Av1RateDistortion.GetCost(this.rateMultiplier, rate, distortion);
                bool allowIdentity = true;
                if (evaluateBlue || evaluateRed)
                {
                    Av1MotionVector motion = state.WinningMotionVectors[(int)winner.Mode][(int)winner.ReferenceFrame];
                    Av1EncoderFrame<TSample>.PlanarView reference = this.references.Span[(int)winner.ReferenceFrame].CodedView;
                    for (int index = 1; index < 3; index++)
                    {
                        if (index == 1 ? evaluateBlue : evaluateRed)
                        {
                            Av1Plane plane = (Av1Plane)index;
                            this.PrepareInterPlanePrediction(
                                motion,
                                default,
                                plane,
                                winner.Mode,
                                winner.ReferenceFrame,
                                winner.SecondaryReferenceFrame,
                                false,
                                winner.CompoundType,
                                winner.CompoundWedgeIndex,
                                winner.CompoundWedgeSign,
                                winner.DifferenceWeightedMaskType,
                                winner.HorizontalInterpolationFilter,
                                winner.VerticalInterpolationFilter,
                                reference.GetPlane(plane),
                                reference.GetPlane(plane),
                                origin,
                                this.source.ChromaSubsamplingX,
                                this.source.ChromaSubsamplingY,
                                blockSize,
                                index == 1 ? workspace.BluePrediction : workspace.RedPrediction,
                                workspace.Residual);
                        }
                    }

                    Av1RateDistortionStatistics chroma = this.EstimateInterChroma(
                        origin, blockSize, workspace.BluePrediction, workspace.RedPrediction, evaluateBlue, evaluateRed);
                    state.MinimumChromaDistortion = Math.Min(state.MinimumChromaDistortion, chroma.Distortion);
                    rate += chroma.Rate;
                    distortion += chroma.Distortion;
                    skip &= chroma.AllTransformsEmpty;
                    allowIdentity = !(lumaCost == 0 && chroma.Distortion > 0 && this.interSourceVariance < 3000 &&
                        this.sourceSadLevel > Av1SourceSadLevel.Medium);
                }

                long cost = Av1RateDistortion.GetCost(this.rateMultiplier, rate, distortion);
                if (allowIdentity && cost < state.BestStatistics.Cost)
                {
                    // Identity changes the retained transform choice and comparison cost. Prediction
                    // syntax and the winning motion remain those of the already-selected inter mode.
                    transformType = Av1TransformType.Identity;
                    Av1RateDistortionStatistics statistics = state.BestStatistics;
                    statistics.Cost = cost;
                    state.BestStatistics = statistics;
                    winner.Skip = skip;
                }
            }

            bool tryPalette = !skipScreenModes && !forceZeroMotion &&
                Av1TileWriter.IsPaletteAllowed(parent.FrameHeader.AllowScreenContentTools, blockSize) &&
                (winner.ReferenceFrame == Av1ReferenceFrameType.Intra || forcePalette) && this.interSourceVariance > 0 &&
                (parent.HighSourceSad || this.interSourceVariance > 300);
            if (!tryPalette)
            {
                return false;
            }

            int colorThreshold = 64;
            if (parent.IsScreenContent && parent.HighSourceSad && this.interSourceVariance > 50)
            {
                long chromaDistortion = 0;
                if (evaluateBlue || evaluateRed)
                {
                    chromaDistortion = state.MinimumChromaDistortion >>
                        (BitOperations.Log2((uint)(blockSize.GetWidth() * blockSize.GetHeight())) - 4);
                    if (evaluateBlue && evaluateRed)
                    {
                        chromaDistortion >>= 1;
                    }
                }

                if (chromaDistortion < 8000)
                {
                    colorThreshold += 20;
                }
            }

            Span<int> coefficients = this.coefficientBuffer.GetPlaneSpan(this.superblock.Index, Av1Plane.Y)[this.codedAreaLuma..];
            Span<Av1EncoderTransformBlockState> states = this.coefficientBuffer.GetTransformBlockSpan(this.superblock.Index, Av1Plane.Y)[
                (this.codedAreaLuma / Av1EncoderCoefficientBuffer.TransformBlockUnitCoefficientCount)..];
            Av1TransformSize transformSize = winner.TransformSize;
            Av1RateDistortionStatistics paletteStatistics = state.BestStatistics;
            bool selected = this.SelectLumaPalette(
                writer,
                macroBlock,
                origin,
                blockSize,
                tileIndex,
                coefficients,
                states,
                colorThreshold,
                writer.GetInterFrameLumaModeCost(Av1PredictionMode.DC, blockSize),
                ref paletteStatistics,
                ref palette,
                ref transformSize);
            if (selected)
            {
                bool skip = !paletteStatistics.HasCoefficients;
                int rate = state.ReferenceCosts[(int)Av1ReferenceFrameType.Intra] +
                    (skip ? 0 : paletteStatistics.Rate) + writer.GetSkipCost(skip, Av1TileWriter.GetSkipContext(macroBlock));
                Av1RateDistortionStatistics statistics = new(this.rateMultiplier, rate, paletteStatistics.Distortion)
                {
                    HasCoefficients = !skip,
                    AllTransformsEmpty = skip
                };

                if (statistics.Cost < state.BestStatistics.Cost)
                {
                    state.BestStatistics = statistics;
                    winner.Mode = Av1PredictionMode.DC;
                    winner.UvMode = Av1ChromaPredictionMode.DC;
                    winner.ReferenceFrame = Av1ReferenceFrameType.Intra;
                    winner.SecondaryReferenceFrame = Av1ReferenceFrameType.None;
                    winner.TransformSize = transformSize;
                    winner.Skip = skip;
                    return true;
                }
            }

            palette = default;
            return false;
        }

        /// <summary>
        /// Applies the stationary-prediction skip test to a child of a temporally unchanged superblock.
        /// </summary>
        /// <param name="origin">The luma block origin.</param>
        /// <param name="blockSize">The coding-block geometry.</param>
        /// <returns>Whether all coded planes satisfy the stationary skip thresholds.</returns>
        private bool CanSkipEstimatedZeroMotionBlock(Point origin, Av1BlockSize blockSize)
        {
            if (this.forceZeroMotionLevel < 2)
            {
                return this.forceZeroMotionLevel != 0;
            }

            if (blockSize == this.picture.Sequence.SequenceHeader.SuperblockSize)
            {
                return false;
            }

            int sampleCount = blockSize.GetWidth() * blockSize.GetHeight();
            uint lumaThreshold = (uint)Math.Min((int)((10000 * Math.Sqrt(sampleCount / 16384D)) + 0.5), 4 * sampleCount);
            int planeCount = this.source.IsMonochrome ? 1 : 3;
            for (int index = 0; index < planeCount; index++)
            {
                int subX = index == 0 ? 0 : this.source.ChromaSubsamplingX;
                int subY = index == 0 ? 0 : this.source.ChromaSubsamplingY;
                Av1BlockSize size = blockSize.GetSubsampled(subX != 0, subY != 0);
                Point planeOrigin = index == 0 ? origin : Av1TileWriter.GetChromaBlockOrigin(origin, subX, subY);
                Buffer2DRegion<TSample> sourcePlane = this.source.GetPlane((Av1Plane)index);
                Buffer2DRegion<TSample> referencePlane = this.reference.GetPlane((Av1Plane)index);
                uint sad = (uint)(TOperator.SumAbsoluteDifferences(
                    Av1TransformBlockEncoder.GetPlaneSpan(sourcePlane, planeOrigin),
                    sourcePlane.Stride,
                    Av1TransformBlockEncoder.GetPlaneSpan(referencePlane, planeOrigin),
                    referencePlane.Stride,
                    size.GetWidth(),
                    size.GetHeight(),
                    1) >> (this.bitDepth.GetBitCount() - 8));

                if (sad >= (index == 0 ? lumaThreshold : (3 * lumaThreshold) >> 2))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Refines uncertain superblock color decisions using the current block's stationary chroma error.
        /// </summary>
        /// <param name="origin">The luma block origin.</param>
        /// <param name="blockSize">The coding-block geometry.</param>
        /// <param name="lumaSad">The spatial predictor SAD with the smallest displacement.</param>
        /// <param name="forceCheck">Whether partition selection requires a fresh block-level measurement.</param>
        /// <param name="colorSensitivity">The inherited color decisions, updated in place.</param>
        private void SetEstimatedColorSensitivity(
            Point origin, Av1BlockSize blockSize, int lumaSad, bool forceCheck, Span<byte> colorSensitivity)
        {
            if (blockSize == this.picture.Sequence.SequenceHeader.SuperblockSize && !forceCheck)
            {
                for (int index = 0; index < 2; index++)
                {
                    if (colorSensitivity[index] == 2)
                    {
                        colorSensitivity[index] = (byte)(this.sourceSadLevel >= Av1SourceSadLevel.Medium ? 1 : 0);
                    }
                }

                return;
            }

            Av1PictureParentControlSet parent = this.picture.Parent;
            int frameWidth = parent.FrameHeader.FrameSize.FrameWidth;
            bool highResolution = frameWidth * parent.FrameHeader.FrameSize.FrameHeight >= 640 * 360;
            int shift = this.sourceSadLevel >= Av1SourceSadLevel.Medium && this.interSourceVariance > 0 && highResolution ? 4 : 3;
            int spatialThreshold = 50;
            int lowVarianceSadThreshold = 100;
            int sadThreshold = 40;
            if (parent.IsScreenContent)
            {
                if (parent.HighSourceSad)
                {
                    shift = 6;
                }

                if (this.sourceSadLevel > Av1SourceSadLevel.Medium)
                {
                    spatialThreshold = 1200;
                    lowVarianceSadThreshold = 10;
                }

                if (parent.SourceMotionPercentage > 90 && parent.FrameSourceSad > 10000 && this.sourceSadLevel > Av1SourceSadLevel.Low)
                {
                    shift = 10;
                    lowVarianceSadThreshold = 0;
                    sadThreshold = 0;
                }
            }

            // These activity thresholds are expressed per 4x4 unit, rather than per pixel.
            // Keep the same scale for luma and subsampled chroma before comparing their errors.
            int normalizedLumaSad = lumaSad >> (BitOperations.Log2((uint)(blockSize.GetWidth() * blockSize.GetHeight())) - 4);
            if (!parent.IsScreenContent && this.interSourceVariance > (frameWidth > 1920 ? 5000 : 1000) && normalizedLumaSad < 50)
            {
                colorSensitivity.Clear();
                return;
            }

            int subX = this.source.ChromaSubsamplingX;
            int subY = this.source.ChromaSubsamplingY;
            Av1BlockSize chromaSize = blockSize.GetSubsampled(subX != 0, subY != 0);
            Point chromaOrigin = Av1TileWriter.GetChromaBlockOrigin(origin, subX, subY);
            int normalizationShift = BitOperations.Log2((uint)(chromaSize.GetWidth() * chromaSize.GetHeight())) - 4;
            for (int index = 0; index < 2; index++)
            {
                if (colorSensitivity[index] == 2 || forceCheck ||
                    (colorSensitivity[index] == 0 && this.sourceSadLevel >= Av1SourceSadLevel.Medium && highResolution))
                {
                    Av1Plane plane = (Av1Plane)(index + 1);
                    Buffer2DRegion<TSample> sourcePlane = this.source.GetPlane(plane);
                    Buffer2DRegion<TSample> referencePlane = this.reference.GetPlane(plane);
                    int sad = (int)(TOperator.SumAbsoluteDifferences(
                        Av1TransformBlockEncoder.GetPlaneSpan(sourcePlane, chromaOrigin),
                        sourcePlane.Stride,
                        Av1TransformBlockEncoder.GetPlaneSpan(referencePlane, chromaOrigin),
                        referencePlane.Stride,
                        chromaSize.GetWidth(),
                        chromaSize.GetHeight(),
                        1) >> (this.bitDepth.GetBitCount() - 8));

                    int normalizedSad = sad >> normalizationShift;
                    colorSensitivity[index] = (byte)(
                        ((sad > (lumaSad >> shift) && normalizedSad > sadThreshold) ||
                         (this.interSourceVariance < spatialThreshold && normalizedSad > lowVarianceSadThreshold)) ? 1 : 0);
                }
            }
        }

        /// <summary>
        /// Selects the interpolation search policy from neighboring filters and temporal activity.
        /// </summary>
        /// <param name="macroBlock">The reconstructed block neighbors.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The coding-block geometry.</param>
        /// <param name="boostedSegment">Whether cyclic refresh boosts the current segment.</param>
        /// <param name="filter">The concrete interpolation filter used when search is omitted.</param>
        /// <returns>Zero to omit search, one for mode-dependent search, or two to force eligible searches.</returns>
        private int GetEstimatedFilterSearchPolicy(
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            bool boostedSegment,
            out Av1InterpolationFilter filter)
        {
            filter = Av1InterpolationFilter.Regular;
            Av1PictureParentControlSet parent = this.picture.Parent;
            if (parent.IsScreenContent && parent.EncodingSpeed >= HeifEncodingSpeed.Level8)
            {
                return 0;
            }

            if (parent.IsScreenContent || !parent.SpeedSettings.UseEstimatedFilterChessboard || this.sourceSadLevel <= Av1SourceSadLevel.VeryLow)
            {
                return 1;
            }

            if (!macroBlock.IsUpAvailable || !macroBlock.IsLeftAvailable)
            {
                return 2;
            }

            Av1EncoderBlockModeInfo above = macroBlock.GetRelativeModeInfo(-macroBlock.ModeInfoStride).Block;
            Av1EncoderBlockModeInfo left = macroBlock.GetRelativeModeInfo(-1).Block;
            if (above.ReferenceFrame <= Av1ReferenceFrameType.Intra || left.ReferenceFrame <= Av1ReferenceFrameType.Intra ||
                above.HorizontalInterpolationFilter != left.HorizontalInterpolationFilter ||
                above.VerticalInterpolationFilter != left.VerticalInterpolationFilter ||
                left.HorizontalInterpolationFilter != Av1InterpolationFilter.Regular)
            {
                return 2;
            }

            // Order hints wrap at a power of two, preserving the frame parity used by this pattern.
            // A block alternates with its neighbors and changes phase on the following frame.
            int sizeLog2 = BitOperations.Log2((uint)blockSize.Get4x4WideCount());
            int position = ((blockOrigin.X + blockOrigin.Y) >> Av1Constants.ModeInfoSizeLog2) >> sizeLog2;
            return boostedSegment ? 1 : (position + (int)(parent.FrameHeader.OrderHint & 1)) & 1;
        }

        /// <summary>
        /// Evaluates the intra alternatives admitted after estimated inter search.
        /// </summary>
        /// <param name="writer">The current tile symbol costs.</param>
        /// <param name="macroBlock">The reconstructed block neighbors.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="evaluateBlue">Whether blue-difference distortion participates in selection.</param>
        /// <param name="evaluateRed">Whether red-difference distortion participates in selection.</param>
        /// <param name="bestEarlyTermination">Whether the inter winner established an early all-plane skip.</param>
        /// <param name="bestInitialSkip">Whether the inter winner was skippable before residual estimation.</param>
        /// <param name="boostedSegment">Whether cyclic refresh boosts the current segment.</param>
        /// <param name="winner">The retained prediction syntax.</param>
        /// <param name="winningPrediction">The retained luma prediction storage.</param>
        /// <param name="prediction">The reusable candidate prediction storage.</param>
        private void SelectEstimatedIntraModes(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            bool evaluateBlue,
            bool evaluateRed,
            bool bestEarlyTermination,
            bool bestInitialSkip,
            bool boostedSegment,
            ref Av1EncoderBlockModeInfo winner,
            ref Span<TSample> winningPrediction,
            ref Span<TSample> prediction)
        {
            Av1PictureParentControlSet parent = this.picture.Parent;
            Av1EncoderSpeedSettings speed = parent.SpeedSettings;
            ref Av1EstimatedInterSearchState state = ref this.blockWorkspace.EstimatedInterSearchState;
            Av1BlockSize blockSize = winner.BlockSize;
            bool screen = parent.IsScreenContent;
            bool screenChange = screen && parent.HighSourceSad;
            int dcQuant = Av1QuantizationLookup.GetDcQuant(
                this.quantization.QIndex[0], this.quantization.DeltaQDc[0], this.bitDepth);

            int shift = this.bitDepth.GetBitCount() - 8;
            int penalty = ((20 * dcQuant) + ((1 << shift) >> 1)) >> shift;
            int referenceRate = state.ReferenceCosts[(int)Av1ReferenceFrameType.Intra];
            int spatialThreshold = 50;
            int motionThreshold = 32;
            if ((screen || !speed.UseEstimatedAlternateReference) && speed.GetEstimatedReferencePruningLevel(screen) > 0)
            {
                spatialThreshold = 150;
                motionThreshold = 0;
            }

            bool performIntra = true;
            bool forceIntra = false;
            bool useThreshold = true;
            if (this.interSourceVariance < spatialThreshold)
            {
                Av1MotionVector motion = state.WinningMotionVectors[(int)winner.Mode][(int)winner.ReferenceFrame];
                if (state.BestStatistics.Cost != long.MaxValue &&
                    (winner.ReferenceFrame != Av1ReferenceFrameType.Last ||
                        Math.Abs(motion.Row) >= motionThreshold || Math.Abs(motion.Column) >= motionThreshold))
                {
                    penalty >>= 2;
                    useThreshold = false;
                }

                forceIntra = (this.interSourceVariance < Math.Max(50, spatialThreshold >> 1) && this.sourceSadLevel >= Av1SourceSadLevel.High) ||
                    (screen && this.interSourceVariance < 50 &&
                        ((blockSize >= Av1BlockSize.Block32x32 && this.sourceSadLevel != Av1SourceSadLevel.Zero) || evaluateBlue || evaluateRed));
                if (blockSize >= Av1BlockSize.Block32x32)
                {
                    bestEarlyTermination = false;
                }
            }
            else if (this.sourceSadLevel <= Av1SourceSadLevel.Low)
            {
                performIntra = false;
            }

            if (winner.Skip && bestInitialSkip &&
                (speed.EstimatedIntraSkipLevel == 2 || winner.Mode != Av1PredictionMode.NewMotionVector))
            {
                performIntra = false;
            }

            Av1BlockSize maximumSize = screenChange ? Av1BlockSize.Block128x128 : Av1BlockSize.Block32x32;
            if (!(state.BestStatistics.Cost == long.MaxValue || forceIntra ||
                (performIntra && !bestEarlyTermination && blockSize <= maximumSize)))
            {
                return;
            }

            long minimumCost = Av1RateDistortion.GetCost(this.rateMultiplier, referenceRate + penalty, 0);
            if ((screen ? (7 * minimumCost) >> 3 : minimumCost) > state.BestStatistics.Cost)
            {
                return;
            }

            Av1TransformSize transformSize = parent.FrameHeader.TransformMode == Av1TransformMode.Only4x4
                ? Av1TransformSize.Size4x4 : blockSize.GetMaximumTransformSize().GetSquareSize();
            if (transformSize > Av1TransformSize.Size16x16)
            {
                transformSize = Av1TransformSize.Size16x16;
            }

            if (screenChange && this.interSourceVariance > spatialThreshold && blockSize <= Av1BlockSize.Block16x16)
            {
                transformSize = Av1TransformSize.Size4x4;
            }

            int modeMask = speed.GetEstimatedIntraModeMask(blockSize, screenChange);
            foreach (Av1PredictionMode mode in EstimatedIntraModes)
            {
                // Forced intra keeps DC, vertical, and horizontal available even when the ordinary
                // speed mask admits DC only. Smooth remains subject to the mask and cost threshold.
                if (!forceIntra || mode == Av1PredictionMode.Smooth)
                {
                    if ((modeMask & (1 << (int)mode)) == 0)
                    {
                        continue;
                    }

                    if (mode != Av1PredictionMode.DC && !screen && parent.EncodingSpeed >= HeifEncodingSpeed.Level8 &&
                        !(evaluateBlue && evaluateRed &&
                            (parent.FrameSourceSad > 1.1 * parent.AverageSourceSad || boostedSegment || this.sourceSadLevel > Av1SourceSadLevel.Medium)))
                    {
                        continue;
                    }
                }

                if (screen &&
                    ((this.sourceSadLevel == Av1SourceSadLevel.Zero && this.interSourceVariance == 0 && mode != Av1PredictionMode.DC) ||
                        (blockSize > Av1BlockSize.Block32x32 && this.interSourceVariance > 50)))
                {
                    continue;
                }

                if ((useThreshold || mode == Av1PredictionMode.Smooth) && Av1ModeThresholds.ShouldSkip(
                    this.blockWorkspace.ModeThresholdFactors,
                    this.blockWorkspace.ModeThresholdQuantizerFactor,
                    4096,
                    blockSize,
                    mode,
                    Av1ReferenceFrameType.Intra,
                    Av1ReferenceFrameType.None,
                    state.BestStatistics.Cost,
                    false))
                {
                    continue;
                }

                Av1RateDistortionStatistics residual = this.EstimateInterFrameIntraCandidate(
                    macroBlock, blockOrigin, blockSize, mode, transformSize, evaluateBlue, evaluateRed, prediction);

                int rate = residual.Rate + referenceRate + penalty;
                if ((mode == Av1PredictionMode.Vertical || mode == Av1PredictionMode.Horizontal) && blockSize >= Av1BlockSize.Block8x8)
                {
                    rate += writer.GetAngleDeltaCost(Av1Constants.MaxAngleDelta, mode);
                }

                if (mode == Av1PredictionMode.DC &&
                    Av1TileWriter.IsFilterIntraAllowedBlockSize(this.picture.Sequence.SequenceHeader.EnableFilterIntra, blockSize))
                {
                    rate += writer.GetFilterIntraModeCost(Av1FilterIntraMode.AllFilterIntraModes, blockSize);
                }

                Av1RateDistortionStatistics statistics = new(this.rateMultiplier, rate, residual.Distortion)
                {
                    HasCoefficients = residual.HasCoefficients,
                    AllTransformsEmpty = residual.AllTransformsEmpty
                };

                if (screenChange && this.interSourceVariance < 800 && (evaluateBlue || evaluateRed))
                {
                    statistics.Cost = (7 * statistics.Cost) >> 3;
                }
                else if (screen && !parent.HighSourceSad && this.interSourceVariance > 0 &&
                    this.sourceSadLevel == Av1SourceSadLevel.Zero && !evaluateBlue && !evaluateRed)
                {
                    statistics.Cost = (3 * statistics.Cost) >> 1;
                }

                if (statistics.Cost < state.BestStatistics.Cost)
                {
                    state.BestStatistics = statistics;
                    winner.Mode = mode;
                    winner.UvMode = (Av1ChromaPredictionMode)mode;
                    winner.TransformSize = transformSize;
                    winner.ReferenceFrame = Av1ReferenceFrameType.Intra;
                    winner.SecondaryReferenceFrame = Av1ReferenceFrameType.None;
                    winner.Skip = statistics.AllTransformsEmpty;
                    Span<TSample> previous = winningPrediction;
                    winningPrediction = prediction;
                    prediction = previous;
                }
            }
        }

        /// <summary>
        /// Estimates an intra alternative using prediction-only internal edges and plane-specific residual models.
        /// </summary>
        /// <param name="macroBlock">The block's reconstructed neighbors and frame edges.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The coding-block geometry.</param>
        /// <param name="mode">The luma and chroma prediction mode.</param>
        /// <param name="transformSize">The luma residual estimation size.</param>
        /// <param name="evaluateBlue">Whether blue-difference distortion participates in selection.</param>
        /// <param name="evaluateRed">Whether red-difference distortion participates in selection.</param>
        /// <param name="lumaPrediction">The candidate's tightly packed prediction storage.</param>
        /// <returns>The modeled residual rate and distortion before mode syntax.</returns>
        private Av1RateDistortionStatistics EstimateInterFrameIntraCandidate(
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            Av1PredictionMode mode,
            Av1TransformSize transformSize,
            bool evaluateBlue,
            bool evaluateRed,
            Span<TSample> lumaPrediction)
        {
            Av1EncoderInterPredictionWorkspace<TSample> workspace = this.blockWorkspace.GetInterPredictionWorkspace<TSample>();
            Av1EncoderModeDecisionWorkspace<TSample> intraWorkspace = this.blockWorkspace.GetModeDecisionWorkspace<TSample>();
            Span<TSample> above = intraWorkspace.GetReferenceSamples(0);
            Span<TSample> left = intraWorkspace.GetReferenceSamples(1);
            Span<short> residual = workspace.Residual;
            int rate = 0;
            long distortion = 0;
            bool skip = false;
            int sampleShift = this.bitDepth.GetBitCount() - 8;
            for (int planeIndex = 0; planeIndex < 3; planeIndex++)
            {
                if ((planeIndex == 1 && !evaluateBlue) || (planeIndex == 2 && !evaluateRed))
                {
                    continue;
                }

                Av1Plane plane = (Av1Plane)planeIndex;
                int subX = planeIndex == 0 ? 0 : this.source.ChromaSubsamplingX;
                int subY = planeIndex == 0 ? 0 : this.source.ChromaSubsamplingY;
                Av1BlockSize planeSize = blockSize.GetSubsampled(subX != 0, subY != 0);
                int width = planeSize.GetWidth();
                Point origin = planeIndex == 0 ? blockOrigin : Av1TileWriter.GetChromaBlockOrigin(blockOrigin, subX, subY);
                Av1TransformSize predictionSize = planeIndex == 0
                    ? blockSize.GetMaximumTransformSize().GetSquareSize()
                    : this.picture.Parent.FrameHeader.CodedLossless
                        ? Av1TransformSize.Size4x4 : blockSize.GetMaxUvTransformSize(subX != 0, subY != 0);
                int transformWidth = predictionSize.GetWidth();
                int transformHeight = predictionSize.GetHeight();
                Size extent = GetCodedTransformExtent(macroBlock, planeSize, predictionSize, subX, subY);
                Span<TSample> prediction = planeIndex == 0 ? lumaPrediction
                    : planeIndex == 1 ? workspace.BluePrediction : workspace.RedPrediction;
                Buffer2DRegion<TSample> sourcePlane = this.source.GetPlane(plane);
                Buffer2DRegion<TSample> reconstructedPlane = this.reconstruction.GetPlane(plane);

                // Prediction units use their largest permitted transform, independently of the
                // smaller luma estimation tiles. Interior edges contain prediction, never residuals.
                int unitWidth = 64 >> subX;
                int unitHeight = 64 >> subY;
                for (int unitY = 0; unitY < extent.Height; unitY += unitHeight)
                {
                    for (int unitX = 0; unitX < extent.Width; unitX += unitWidth)
                    {
                        int bottom = Math.Min(unitY + unitHeight, extent.Height);
                        int right = Math.Min(unitX + unitWidth, extent.Width);
                        for (int y = unitY; y < bottom; y += transformHeight)
                        {
                            for (int x = unitX; x < right; x += transformWidth)
                            {
                                this.PrepareTransformReferenceSamples(
                                    reconstructedPlane,
                                    blockOrigin,
                                    origin,
                                    blockSize,
                                    macroBlock,
                                    y / transformHeight,
                                    x / transformWidth,
                                    width,
                                    predictionSize,
                                    subX,
                                    subY,
                                    prediction,
                                    above,
                                    left,
                                    out bool hasLeft,
                                    out bool hasAbove);

                                Point transformOrigin = origin + new Size(x, y);
                                Span<TSample> predictedTransform = prediction[((y * width) + x)..];
                                TOperator.PrepareIntra(
                                    this.blockWorkspace,
                                    sourcePlane,
                                    transformOrigin,
                                    predictedTransform,
                                    width,
                                    above.Slice(1, transformWidth + transformHeight),
                                    left.Slice(1, transformWidth + transformHeight),
                                    hasLeft,
                                    hasAbove,
                                    mode,
                                    0,
                                    this.picture.Sequence.SequenceHeader.EnableIntraEdgeFilter,
                                    this.UseSmoothIntraEdges(macroBlock, blockOrigin, blockSize, plane),
                                    residual,
                                    predictionSize,
                                    this.bitDepth);

                                if (planeIndex != 0)
                                {
                                    TOperator.GetMoments(
                                        Av1TransformBlockEncoder.GetPlaneSpan(sourcePlane, transformOrigin),
                                        sourcePlane.Stride,
                                        predictedTransform,
                                        width,
                                        transformWidth,
                                        transformHeight,
                                        out int sum,
                                        out long squares);

                                    sum = (sum + ((1 << sampleShift) >> 1)) >> sampleShift;
                                    squares = (squares + ((1L << (sampleShift * 2)) >> 1)) >> (sampleShift * 2);
                                    int countLog2 = BitOperations.Log2((uint)predictionSize.GetSize2d());
                                    long variance = Math.Max(0, squares - (((long)sum * sum) >> countLog2));
                                    int dcStep = Av1QuantizationLookup.GetDcQuant(
                                        this.quantization.QIndex[0], this.quantization.DeltaQDc[planeIndex], this.bitDepth) >> 3;

                                    int acStep = Av1QuantizationLookup.GetAcQuant(
                                        this.quantization.QIndex[0], this.quantization.DeltaQAc[planeIndex], this.bitDepth) >> 3;

                                    // Chroma is modeled per prediction unit. Summing its moments across
                                    // units first would change the DC energy and the skip comparison.
                                    Av1RateDistortion.EstimateLaplacian(
                                        squares - variance, countLog2, dcStep, out int dcRate, out long dcDistortion);

                                    Av1RateDistortion.EstimateLaplacian(
                                        variance, countLog2, acStep, out int acRate, out long acDistortion);

                                    int unitRate = (dcRate >> 1) + acRate;
                                    long unitDistortion = (dcDistortion << 3) + (acDistortion << 4);
                                    long predictionDistortion = squares << 4;
                                    if (Av1RateDistortion.GetCost(this.rateMultiplier, unitRate, unitDistortion) >=
                                        Av1RateDistortion.GetCost(this.rateMultiplier, 0, predictionDistortion))
                                    {
                                        unitRate = 0;
                                        unitDistortion = predictionDistortion;
                                    }

                                    rate += unitRate;
                                    distortion += unitDistortion;
                                }
                            }
                        }
                    }
                }

                if (planeIndex == 0)
                {
                    TOperator.SubtractPrediction(
                        sourcePlane, origin, prediction, residual, width, planeSize.GetHeight());

                    Size residualExtent = GetCodedTransformExtent(macroBlock, blockSize, transformSize, 0, 0);
                    Av1IntraModeEstimator.Estimate(
                        this.blockWorkspace,
                        residual,
                        width,
                        residualExtent,
                        transformSize,
                        this.quantization.QIndex[0],
                        this.quantization.DeltaQDc[0],
                        this.quantization.DeltaQAc[0],
                        this.bitDepth,
                        false,
                        out int lumaRate,
                        out long lumaDistortion,
                        out skip);

                    rate += lumaRate;
                    distortion += lumaDistortion;
                }
            }

            return new(this.rateMultiplier, rate, distortion)
            {
                AllTransformsEmpty = skip,
                HasCoefficients = !skip
            };
        }

        /// <summary>
        /// Encodes the selected inter predictor into the retained frame and coefficient storage.
        /// </summary>
        /// <param name="writer">The tile symbol costs.</param>
        /// <param name="macroBlock">The current block's neighbors and edges.</param>
        /// <param name="tileIndex">The containing tile's context index.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="modeInfo">The selected inter syntax.</param>
        /// <param name="block">The selected block's chroma and motion syntax.</param>
        /// <param name="primaryReference">The primary bordered reference planes.</param>
        /// <param name="secondaryReference">The secondary bordered reference planes.</param>
        /// <param name="vector">The primary displacement.</param>
        /// <param name="secondaryVector">The secondary displacement.</param>
        /// <param name="lumaPrediction">The retained luma predictor.</param>
        /// <param name="transformType">The selected transform family.</param>
        private void EncodeEstimatedInterWinner(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            ushort tileIndex,
            Point blockOrigin,
            ref Av1EncoderBlockModeInfo modeInfo,
            Av1EncoderBlockStruct block,
            Av1EncoderFrame<TSample>.PlanarView primaryReference,
            Av1EncoderFrame<TSample>.PlanarView secondaryReference,
            Av1MotionVector vector,
            Av1MotionVector secondaryVector,
            ReadOnlySpan<TSample> lumaPrediction,
            Av1TransformType transformType)
        {
            bool lossless = this.picture.Parent.FrameHeader.CodedLossless;
            modeInfo.Skip &= !lossless;
            modeInfo.TransformSize = lossless ? Av1TransformSize.Size4x4 : modeInfo.TransformSize;
            modeInfo.InterTransformSizes.Fill(modeInfo.TransformSize);
            Av1EncoderInterPredictionWorkspace<TSample> workspace = this.blockWorkspace.GetInterPredictionWorkspace<TSample>();
            int planeCount = block.HasChroma ? 3 : 1;
            int lumaContextWidth = modeInfo.BlockSize.Get4x4WideCount();
            Span<byte> lumaTypes = MemoryMarshal.AsBytes(workspace.SelectedLumaCoefficients)[
                ..(lumaContextWidth * modeInfo.BlockSize.Get4x4HighCount())];

            bool allTransformsEmpty = true;
            for (int planeIndex = 0; planeIndex < planeCount; planeIndex++)
            {
                Av1Plane plane = (Av1Plane)planeIndex;
                int subX = planeIndex == 0 ? 0 : this.source.ChromaSubsamplingX;
                int subY = planeIndex == 0 ? 0 : this.source.ChromaSubsamplingY;
                Point planeOrigin = planeIndex == 0 ? blockOrigin : Av1TileWriter.GetChromaBlockOrigin(blockOrigin, subX, subY);
                Av1BlockSize planeBlock = modeInfo.BlockSize.GetSubsampled(subX != 0, subY != 0);
                Av1TransformSize transformSize = lossless ? Av1TransformSize.Size4x4
                    : planeIndex == 0 ? modeInfo.TransformSize : modeInfo.BlockSize.GetMaxUvTransformSize(subX != 0, subY != 0);

                Av1TransformSetType transformSet = Av1SymbolContextHelper.GetExtendedTransformSetType(
                    transformSize, true, this.picture.Parent.FrameHeader.UseReducedTransformSet);

                int stride = planeBlock.GetWidth();
                ReadOnlySpan<TSample> prediction = lumaPrediction;
                if (planeIndex != 0)
                {
                    Span<TSample> chromaPrediction = planeIndex == 1 ? workspace.BluePrediction : workspace.RedPrediction;
                    this.PrepareInterPlanePrediction(
                        vector,
                        secondaryVector,
                        plane,
                        modeInfo.Mode,
                        modeInfo.ReferenceFrame,
                        modeInfo.SecondaryReferenceFrame,
                        false,
                        modeInfo.CompoundType,
                        modeInfo.CompoundWedgeIndex,
                        modeInfo.CompoundWedgeSign,
                        modeInfo.DifferenceWeightedMaskType,
                        modeInfo.HorizontalInterpolationFilter,
                        modeInfo.VerticalInterpolationFilter,
                        primaryReference.GetPlane(plane),
                        secondaryReference.GetPlane(plane),
                        new Point(planeOrigin.X << subX, planeOrigin.Y << subY),
                        subX,
                        subY,
                        modeInfo.BlockSize,
                        chromaPrediction,
                        workspace.Residual);

                    prediction = chromaPrediction;
                }
                else if (!modeInfo.Skip)
                {
                    TOperator.SubtractPrediction(
                        this.source.GetPlane(plane), planeOrigin, prediction, workspace.Residual, stride, planeBlock.GetHeight());
                }

                Buffer2DRegion<TSample> destinationPlane = this.reconstruction.GetPlane(plane);
                Span<TSample> destination = Av1TransformBlockEncoder.GetPlaneSpan(destinationPlane, planeOrigin);
                Size extent = GetCodedTransformExtent(macroBlock, planeBlock, transformSize, subX, subY);
                int width = transformSize.GetWidth();
                int height = transformSize.GetHeight();
                int sampleCount = transformSize.GetSize2d();
                int offset = planeIndex == 0 ? this.codedAreaLuma : this.codedAreaChroma;
                Span<int> coefficients = this.coefficientBuffer.GetPlaneSpan(this.superblock.Index, plane);
                Span<Av1EncoderTransformBlockState> states = this.coefficientBuffer.GetTransformBlockSpan(this.superblock.Index, plane);
                Av1NeighborArrayUnit<byte> neighbors = plane switch
                {
                    Av1Plane.Y => this.picture.LuminanceDcSignLevelCoefficientNeighbors[tileIndex],
                    Av1Plane.U => this.picture.CbDcSignLevelCoefficientNeighbors[tileIndex],
                    _ => this.picture.CrDcSignLevelCoefficientNeighbors[tileIndex]
                };

                int contextWidth = planeBlock.Get4x4WideCount();
                int contextHeight = planeBlock.Get4x4HighCount();
                Span<byte> top = workspace.TransformContexts[..contextWidth];
                Span<byte> left = workspace.TransformContexts.Slice(contextWidth, contextHeight);
                neighbors.Top.Slice(neighbors.GetTopIndex(planeOrigin), contextWidth).CopyTo(top);
                neighbors.Left.Slice(neighbors.GetLeftIndex(planeOrigin), contextHeight).CopyTo(left);
                Size contextSize = new(
                    this.picture.Parent.FrameHeader.ModeInfoColumnCount >> subX,
                    this.picture.Parent.FrameHeader.ModeInfoRowCount >> subY);

                // Prediction stays in block coordinates while coefficients follow transform coding
                // order, including the separate 64x64 regions of a larger coding block.
                Av1TransformSize rootSize = planeIndex == 0 ? planeBlock.GetMaximumTransformSize() : transformSize;
                int count = stride * planeBlock.GetHeight() / sampleCount;
                for (int index = 0; index < count; index++)
                {
                    Point local = rootSize.GetBlockPartitionOrigin(planeBlock, transformSize, index, subX, subY);
                    if (local.X >= extent.Width || local.Y >= extent.Height)
                    {
                        continue;
                    }

                    Span<TSample> output = destination[((local.Y * destinationPlane.Stride) + local.X)..];
                    int inputOffset = (local.Y * stride) + local.X;
                    for (int row = 0; row < height; row++)
                    {
                        prediction.Slice(inputOffset + (row * stride), width).CopyTo(
                            output.Slice(row * destinationPlane.Stride, width));
                    }

                    Span<byte> transformTop = top.Slice(local.X >> 2, width >> 2);
                    Span<byte> transformLeft = left.Slice(local.Y >> 2, height >> 2);
                    Av1ComponentType component = planeIndex == 0 ? Av1ComponentType.Luminance : Av1ComponentType.Chroma;
                    Av1TransformBlockContext context = Av1TileWriter.GetTransformBlockContexts(
                        component, transformTop, transformLeft, planeBlock, transformSize);

                    ref Av1EncoderTransformBlockState state = ref states[offset / Av1EncoderCoefficientBuffer.TransformBlockUnitCoefficientCount];
                    state = default;
                    state.EntropyContext = (byte)(context.SkipContext | (context.DcSignContext << 4));
                    Span<int> transformCoefficients = coefficients.Slice(offset, sampleCount);
                    Av1TransformType planeTransformType = planeIndex == 0 ? transformType :
                        (Av1TransformType)lumaTypes[(((local.Y << subY) >> 2) * lumaContextWidth) + ((local.X << subX) >> 2)];

                    if (lossless || !planeTransformType.IsExtendedSetUsed(transformSet))
                    {
                        planeTransformType = Av1TransformType.DctDct;
                    }

                    if (modeInfo.Skip)
                    {
                        transformCoefficients.Clear();
                    }
                    else
                    {
                        Av1TransformBlockEncoder.EncodeLossyCandidate(
                            this.blockWorkspace,
                            writer,
                            context,
                            workspace.Residual[inputOffset..],
                            stride,
                            transformCoefficients,
                            transformSize,
                            planeTransformType,
                            this.quantization.QIndex[0],
                            this.quantization.DeltaQDc[planeIndex],
                            this.quantization.DeltaQAc[planeIndex],
                            this.bitDepth,
                            component,
                            this.rateMultiplier,
                            true,
                            false,
                            true,
                            ref state);

                        if (state.EndOfBlock > 0)
                        {
                            TOperator.AddSelectedResidual(
                                this.blockWorkspace, output, destinationPlane.Stride, transformSize, plane, this.bitDepth, lossless, state);
                        }
                    }

                    allTransformsEmpty &= state.EndOfBlock == 0;
                    if (planeIndex == 0)
                    {
                        // Chroma inherits the type at its top-left luma position. Empty luma units
                        // imply DCT, even when identity won the block-level estimate. Reuse now-idle
                        // candidate coefficient storage for this byte map until both chroma planes finish.
                        byte type = (byte)(state.EndOfBlock == 0 ? Av1TransformType.DctDct : state.TransformType);
                        for (int row = 0; row < height >> 2; row++)
                        {
                            lumaTypes.Slice((((local.Y >> 2) + row) * lumaContextWidth) + (local.X >> 2), width >> 2).Fill(type);
                        }
                    }

                    byte coefficientContext = Av1SymbolContextHelper.GetCoefficientContext(
                        transformCoefficients, transformSize, state.TransformType, state.EndOfBlock);

                    Av1TileWriter.UpdateCoefficientContexts(
                        transformTop, transformLeft, coefficientContext, planeOrigin + new Size(local.X, local.Y), contextSize);

                    offset += sampleCount;
                }
            }

            modeInfo.Skip = !lossless && allTransformsEmpty;
        }

        /// <summary>
        /// Evaluates an admitted reference's motion modes in prediction order and retains its best candidate.
        /// </summary>
        /// <param name="writer">The tile symbol costs.</param>
        /// <param name="macroBlock">The current block's neighbors and edges.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="reference">The admitted reference label.</param>
        /// <param name="referencePlanes">The reference's bordered planes.</param>
        /// <param name="referenceVectors">The prepared spatial motion context.</param>
        /// <param name="forceZeroMotion">Whether block policy restricts prediction to stationary LAST motion.</param>
        /// <param name="forceLowTemporalSkip">Whether the partition's temporal variance permits early pruning.</param>
        /// <param name="blockZeroSad">Whether the current block is temporally unchanged.</param>
        /// <param name="screenZeroMotionDisallowed">Whether stationary LAST prediction is unsuitable for the flat screen block.</param>
        /// <param name="evaluateBlue">Whether blue-difference residuals participate in selection.</param>
        /// <param name="evaluateRed">Whether red-difference residuals participate in selection.</param>
        /// <param name="filterSearchPolicy">Zero disables filters, one applies reference pruning, and two forces the filter search.</param>
        /// <param name="defaultFilter">The block's preselected interpolation filter.</param>
        /// <param name="framesSinceGolden">The golden reference's age.</param>
        /// <param name="checkGlobalMotion">Whether explicit global motion still needs evaluation.</param>
        /// <param name="zeroMotionError">The retained normalized stationary LAST prediction error.</param>
        /// <param name="winner">The retained winning syntax.</param>
        /// <param name="winningPrediction">The retained luma predictor.</param>
        /// <param name="prediction">The current candidate's predictor.</param>
        /// <param name="scratch">The alternate filter predictor.</param>
        private void SelectEstimatedReferenceModes(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1ReferenceFrameType reference,
            Av1EncoderFrame<TSample>.PlanarView referencePlanes,
            in Av1ReferenceMotionVectors referenceVectors,
            bool forceZeroMotion,
            bool forceLowTemporalSkip,
            bool blockZeroSad,
            bool screenZeroMotionDisallowed,
            bool evaluateBlue,
            bool evaluateRed,
            int filterSearchPolicy,
            Av1InterpolationFilter defaultFilter,
            int framesSinceGolden,
            ref bool checkGlobalMotion,
            ref uint zeroMotionError,
            ref Av1EncoderBlockModeInfo winner,
            ref Span<TSample> winningPrediction,
            ref Span<TSample> prediction,
            ref Span<TSample> scratch)
        {
            ref Av1EstimatedInterSearchState state = ref this.blockWorkspace.EstimatedInterSearchState;
            Av1EncoderSpeedSettings settings = this.picture.Parent.SpeedSettings;
            Av1BlockSize blockSize = winner.BlockSize;
            int referencePruning = this.estimatedReferencePruning;
            bool screenContent = this.picture.Parent.IsScreenContent;
            bool useSuperblockMotion = this.usePartitionMotion && blockSize >=
                (this.picture.Sequence.SequenceHeader.SuperblockSize == Av1BlockSize.Block128x128
                    ? Av1BlockSize.Block64x64 : Av1BlockSize.Block32x32);

            for (int index = 0; index < 4 && !state.EndSearch; index++)
            {
                Av1PredictionMode mode = (Av1PredictionMode)((int)Av1PredictionMode.SingleInterModeStart + index);
                Av1MotionVector vector = state.MotionVectors[(int)mode][(int)reference];
                if (forceZeroMotion &&
                    (reference != Av1ReferenceFrameType.Last ||
                        (mode != Av1PredictionMode.GlobalMotionVector &&
                            (mode != Av1PredictionMode.NearestMotionVector || !vector.IsZero))))
                {
                    continue;
                }

                bool forceSuperblockMotion = useSuperblockMotion && reference == Av1ReferenceFrameType.Last &&
                    (mode == Av1PredictionMode.NewMotionVector ||
                        (mode is Av1PredictionMode.NearestMotionVector or Av1PredictionMode.NearMotionVector && vector == this.superblockMotion));

                if (!forceSuperblockMotion && (state.EvaluatedModes[(int)mode][(int)reference] != 0 ||
                    (mode == Av1PredictionMode.GlobalMotionVector && !checkGlobalMotion)))
                {
                    continue;
                }

                if (!forceSuperblockMotion && screenContent && !forceZeroMotion)
                {
                    if ((!vector.IsZero && this.sourceSadLevel == Av1SourceSadLevel.Zero) ||
                        (vector.IsZero && !blockZeroSad && reference == Av1ReferenceFrameType.Last && screenZeroMotionDisallowed) ||
                        (mode == Av1PredictionMode.NewMotionVector && this.interSourceVariance < 100) ||
                        (reference != Av1ReferenceFrameType.Last && this.interSourceVariance == 0 && (evaluateBlue || evaluateRed)))
                    {
                        continue;
                    }
                }

                if (!forceSuperblockMotion && (state.SkipSingleByActivity(
                    mode,
                    reference,
                    blockSize,
                    referencePruning,
                    zeroMotionError,
                    settings.AggressiveEstimatedModeSkip,
                    forceLowTemporalSkip,
                    this.sourceSadLevel) ||
                    state.SkipByPredictorSad(mode, reference, referencePruning) ||
                    Av1ModeThresholds.ShouldSkipEstimated(
                        this.blockWorkspace.ModeThresholdFactors,
                        this.blockWorkspace.ModeThresholdQuantizerFactor,
                        blockSize,
                        mode,
                        reference,
                        vector.IsZero,
                        framesSinceGolden,
                        state.BestStatistics.Cost,
                        state.BestStatistics.AllTransformsEmpty,
                        settings.AggressiveEstimatedModeSkip)))
                {
                    continue;
                }

                Av1EncoderBlockModeInfo candidate = winner;
                candidate.Mode = mode;
                candidate.ReferenceFrame = reference;
                candidate.SecondaryReferenceFrame = Av1ReferenceFrameType.None;
                candidate.CompoundGroupIndex = false;
                candidate.CompoundIndex = true;
                candidate.CompoundType = Av1CompoundType.Average;
                candidate.UseIntraBlockCopy = false;
                candidate.HorizontalInterpolationFilter = defaultFilter;
                candidate.VerticalInterpolationFilter = defaultFilter;
                bool searchFilters = filterSearchPolicy == 2 ||
                    (filterSearchPolicy != 0 && (reference == Av1ReferenceFrameType.Last || referencePruning == 0));

                Av1RateDistortionStatistics statistics = this.EvaluateEstimatedInterCandidate(
                    writer,
                    macroBlock,
                    blockOrigin,
                    ref candidate,
                    ref vector,
                    default,
                    referencePlanes,
                    referencePlanes,
                    searchFilters,
                    evaluateBlue,
                    evaluateRed,
                    forceZeroMotion,
                    in referenceVectors,
                    screenContent && !blockZeroSad && screenZeroMotionDisallowed,
                    out int motionRate,
                    ref prediction,
                    ref scratch,
                    out uint variance,
                    out uint squaredError,
                    out long chromaDistortion,
                    out bool initialSkip);

                if (reference == Av1ReferenceFrameType.Last && vector.IsZero && variance != uint.MaxValue)
                {
                    zeroMotionError = squaredError >> (BitOperations.Log2((uint)(blockSize.GetWidth() * blockSize.GetHeight())) - 4);
                }

                if (statistics.Cost == long.MaxValue)
                {
                    continue;
                }

                state.SuperblockMotionTested |= useSuperblockMotion && reference == Av1ReferenceFrameType.Last && vector == this.superblockMotion;
                if (state.CompleteSingleCandidate(
                    writer,
                    in referenceVectors,
                    this.rateMultiplier,
                    ref mode,
                    reference,
                    motionRate,
                    variance,
                    squaredError,
                    chromaDistortion,
                    ref statistics,
                    ref checkGlobalMotion))
                {
                    candidate.Mode = mode;
                    candidate.Skip = statistics.AllTransformsEmpty;
                    winner = candidate;
                    state.BestInitialSkip = initialSkip;
                    state.BestEarlyTermination = forceZeroMotion;

                    // The previous winner becomes reusable candidate storage. The third predictor
                    // remains available for filter trials, without copying or overwriting the winner.
                    Span<TSample> previous = winningPrediction;
                    winningPrediction = prediction;
                    prediction = previous;
                }

                state.EndSearch = state.BestEarlyTermination &&
                    (reference != Av1ReferenceFrameType.Last || index > 0 || settings.AggressiveEstimatedModeSkip) &&
                    (!useSuperblockMotion || state.SuperblockMotionTested);
            }
        }

        /// <summary>
        /// Builds one estimated-mode reference context and measures its spatial predictors.
        /// </summary>
        /// <param name="writer">The current tile symbol costs.</param>
        /// <param name="macroBlock">The coding-block neighbors.</param>
        /// <param name="blockOrigin">The luma coding-block origin.</param>
        /// <param name="blockSize">The coding-block geometry.</param>
        /// <param name="partition">The containing partition syntax.</param>
        /// <param name="reference">The reference label.</param>
        /// <param name="referencePlane">The bordered reference luma plane.</param>
        /// <param name="measureSad">Whether spatial-predictor errors are required.</param>
        /// <param name="referenceVectors">The retained motion-reference context.</param>
        private void PrepareEstimatedReference(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            Av1PartitionType partition,
            Av1ReferenceFrameType reference,
            Buffer2DRegion<TSample> referencePlane,
            bool measureSad,
            ref Av1ReferenceMotionVectors referenceVectors)
        {
            Point position = new(blockOrigin.X >> 2, blockOrigin.Y >> 2);
            referenceVectors.Build(
                this.picture,
                macroBlock,
                position,
                blockSize,
                partition,
                this.picture.Sequence.SequenceHeader,
                this.picture.Parent.FrameHeader,
                reference,
                Av1ReferenceFrameType.None);

            ref Av1EstimatedInterSearchState state = ref this.blockWorkspace.EstimatedInterSearchState;
            int slot = (int)reference;
            Av1MotionVector global = this.picture.Parent.FrameHeader.GetGlobalMotionParameters()[slot - 1].GetMotionVector(
                this.picture.Parent.FrameHeader.AllowHighPrecisionMotionVector,
                blockSize,
                position,
                this.picture.Parent.FrameHeader.ForceIntegerMotionVector);

            state.MotionVectors[(int)Av1PredictionMode.NearestMotionVector][slot] = referenceVectors.Nearest;
            state.MotionVectors[(int)Av1PredictionMode.NearMotionVector][slot] = referenceVectors.GetNearReference(0);
            state.MotionVectors[(int)Av1PredictionMode.GlobalMotionVector][slot] = global;
            state.MotionVectors[(int)Av1PredictionMode.NewMotionVector][slot] = new Av1MotionVector(short.MinValue, short.MinValue);
            for (int index = 0; index < 4; index++)
            {
                Av1PredictionMode mode = (Av1PredictionMode)((int)Av1PredictionMode.SingleInterModeStart + index);
                state.ModeCosts[index][slot] = writer.GetInterModeCost(mode, referenceVectors.ModeContext);
            }

            if (!measureSad)
            {
                return;
            }

            Buffer2DRegion<TSample> sourcePlane = this.source.GetPlane(Av1Plane.Y);
            ReadOnlySpan<TSample> source = Av1TransformBlockEncoder.GetPlaneSpan(sourcePlane, blockOrigin);
            ReadOnlySpan<TSample> referenceSamples = referencePlane.Buffer.DangerousGetSingleSpan();
            int referenceOrigin = ((referencePlane.Bounds.Y + blockOrigin.Y) * referencePlane.Stride) + referencePlane.Bounds.X + blockOrigin.X;
            int width = blockSize.GetWidth();
            int height = blockSize.GetHeight();
            int frameWidth = this.picture.Parent.Common.ModeInfoColumnCount << 2;
            int frameHeight = this.picture.Parent.Common.ModeInfoRowCount << 2;
            bool zeroSeen = false;
            Av1MotionVector first = referenceVectors.Count > 0 ? referenceVectors.Candidates[0] : global;
            Av1MotionVector second = referenceVectors.Count > 1 ? referenceVectors.Candidates[1] : global;
            int count = first == second ? 1 : 2;
            for (int index = 0; index < count; index++)
            {
                Av1MotionVector candidate = index == 0 ? first : second;
                int row = Math.Clamp(candidate.Row, -(blockOrigin.Y + height + 4) * 8, (frameHeight - blockOrigin.Y + 4) * 8);
                int column = Math.Clamp(candidate.Column, -(blockOrigin.X + width + 4) * 8, (frameWidth - blockOrigin.X + 4) * 8);

                // SAD uses the nearest whole sample, with half-sample ties rounded away from zero.
                // Two fractional predictors can therefore collapse to the same zero displacement.
                row = (row + 3 + (row >= 0 ? 1 : 0)) >> 3;
                column = (column + 3 + (column >= 0 ? 1 : 0)) >> 3;
                if (row == 0 && column == 0 && zeroSeen)
                {
                    continue;
                }

                zeroSeen |= row == 0 && column == 0;
                int sad = (int)(TOperator.SumAbsoluteDifferences(
                    source,
                    sourcePlane.Stride,
                    referenceSamples[(referenceOrigin + (row * referencePlane.Stride) + column)..],
                    referencePlane.Stride,
                    width,
                    height,
                    1) >> (this.bitDepth.GetBitCount() - 8));

                state.PredictorSad[slot] = Math.Min(state.PredictorSad[slot], sad);
                if (index == 0)
                {
                    state.NearestSad[slot] = sad;
                }
                else
                {
                    state.NearSad[slot] = sad;
                }
            }
        }

        /// <summary>
        /// Evaluates a prepared motion candidate with the estimated residual controller.
        /// </summary>
        /// <param name="writer">The tile symbol costs.</param>
        /// <param name="macroBlock">The coding-block neighbors and edges.</param>
        /// <param name="blockOrigin">The luma coding-block origin.</param>
        /// <param name="modeInfo">The candidate syntax and selected transform size.</param>
        /// <param name="vector">The primary motion vector.</param>
        /// <param name="secondaryVector">The secondary motion vector.</param>
        /// <param name="primaryReference">The primary bordered reference planes.</param>
        /// <param name="secondaryReference">The secondary bordered reference planes.</param>
        /// <param name="searchFilters">Whether this candidate searches interpolation filters.</param>
        /// <param name="evaluateBlue">Whether to include blue-difference residuals.</param>
        /// <param name="evaluateRed">Whether to include red-difference residuals.</param>
        /// <param name="earlyTermination">Whether block policy has already selected residual skipping.</param>
        /// <param name="referenceVectors">The retained single-reference motion context.</param>
        /// <param name="rejectZeroLastMotion">Whether a newly searched zero LAST vector is excluded by screen-content policy.</param>
        /// <param name="motionRate">The selected new-motion coding cost.</param>
        /// <param name="prediction">The selected luma prediction buffer.</param>
        /// <param name="scratch">The alternate luma prediction buffer.</param>
        /// <param name="variance">The candidate luma variance.</param>
        /// <param name="squaredError">The candidate luma squared error.</param>
        /// <param name="chromaDistortion">The modeled chroma distortion when evaluated.</param>
        /// <param name="initialSkip">Whether residual skipping required no coded alternative.</param>
        /// <returns>The residual cost, or an invalid result when prediction error rejects the candidate.</returns>
        private Av1RateDistortionStatistics EvaluateEstimatedInterCandidate(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ref Av1EncoderBlockModeInfo modeInfo,
            ref Av1MotionVector vector,
            Av1MotionVector secondaryVector,
            Av1EncoderFrame<TSample>.PlanarView primaryReference,
            Av1EncoderFrame<TSample>.PlanarView secondaryReference,
            bool searchFilters,
            bool evaluateBlue,
            bool evaluateRed,
            bool earlyTermination,
            in Av1ReferenceMotionVectors referenceVectors,
            bool rejectZeroLastMotion,
            out int motionRate,
            ref Span<TSample> prediction,
            ref Span<TSample> scratch,
            out uint variance,
            out uint squaredError,
            out long chromaDistortion,
            out bool initialSkip)
        {
            initialSkip = false;
            Av1BlockSize blockSize = modeInfo.BlockSize;
            Av1EncoderInterPredictionWorkspace<TSample> workspace = this.blockWorkspace.GetInterPredictionWorkspace<TSample>();
            chromaDistortion = long.MaxValue;
            variance = uint.MaxValue;
            squaredError = 0;
            motionRate = 0;
            ref Av1EstimatedInterSearchState searchState = ref this.blockWorkspace.EstimatedInterSearchState;
            bool useSuperblockMotion = this.usePartitionMotion && modeInfo.ReferenceFrame == Av1ReferenceFrameType.Last && blockSize >=
                (this.picture.Sequence.SequenceHeader.SuperblockSize == Av1BlockSize.Block128x128
                    ? Av1BlockSize.Block64x64 : Av1BlockSize.Block32x32);

            if (modeInfo.Mode == Av1PredictionMode.NewMotionVector && useSuperblockMotion)
            {
                vector = this.superblockMotion;
                searchState.MotionVectors[(int)modeInfo.Mode][(int)modeInfo.ReferenceFrame] = vector;
            }
            else if (modeInfo.Mode == Av1PredictionMode.NewMotionVector)
            {
                Buffer2DRegion<TSample> sourcePlane = this.source.GetPlane(Av1Plane.Y);
                Buffer2DRegion<TSample> referencePlane = primaryReference.GetPlane(Av1Plane.Y);
                int referenceOrigin = ((referencePlane.Bounds.Y + blockOrigin.Y) * referencePlane.Stride) +
                    referencePlane.Bounds.X + blockOrigin.X;

                Size frameSize = new(
                    this.picture.Parent.Common.ModeInfoColumnCount << Av1Constants.ModeInfoSizeLog2,
                    this.picture.Parent.Common.ModeInfoRowCount << Av1Constants.ModeInfoSizeLog2);

                Rectangle bounds = Av1MotionVector.GetFrameSearchBounds(
                    new Rectangle(blockOrigin, new Size(blockSize.GetWidth(), blockSize.GetHeight())),
                    frameSize,
                    Math.Min(referencePlane.Bounds.X, referencePlane.Bounds.Y));

                ObuFrameHeader header = this.picture.Parent.FrameHeader;
                int skipContext = Av1TileWriter.GetSkipContext(macroBlock);

                // The estimated motion entry measures prediction error and vector rate only. Coefficient
                // edges are unused here; residual modeling follows the final motion/filter selection.
                Av1MotionSearchBase.SingleReferenceSearch<TSample, TOperator> motionSearch = new(
                    Av1TransformBlockEncoder.GetPlaneSpan(sourcePlane, blockOrigin),
                    sourcePlane.Stride,
                    referencePlane.Buffer.DangerousGetSingleSpan(),
                    referencePlane.Stride,
                    referenceOrigin,
                    blockSize,
                    bounds,
                    this.blockWorkspace,
                    this.blockWorkspace.GetMotionSearchPrediction<TSample>(),
                    this.blockWorkspace.Residual,
                    workspace.PredictionScratch,
                    workspace.TransformCoefficients,
                    writer,
                    [],
                    [],
                    this.bitDepth,
                    this.quantization.QIndex[0],
                    this.quantization.DeltaQDc[0],
                    0,
                    header.CodedLossless,
                    this.rateMultiplier,
                    0,
                    writer.GetSkipCost(false, skipContext),
                    writer.GetSkipCost(true, skipContext),
                    modeInfo.HorizontalInterpolationFilter,
                    modeInfo.VerticalInterpolationFilter,
                    this.blockWorkspace.GetMotionVectorCosts(header.MotionVectorPrecision));

                if (!motionSearch.SearchEstimated(
                    this.picture.Parent.MotionSearchSettings,
                    this.picture.Parent.MotionSearchStepParameter,
                    referenceVectors.GetNewReference(0),
                    header.ForceIntegerMotionVector,
                    header.AllowHighPrecisionMotionVector,
                    this.picture.Parent.AverageFrameLowMotion,
                    this.sourceSadLevel,
                    (uint)this.interSourceVariance,
                    searchState.BestStatistics.Cost,
                    out motionRate,
                    out Av1MotionSearchBase.FractionalResult result))
                {
                    return Av1RateDistortionStatistics.Invalid;
                }

                vector = result.Vector;
                searchState.MotionVectors[(int)modeInfo.Mode][(int)modeInfo.ReferenceFrame] = vector;
            }

            if (modeInfo.Mode == Av1PredictionMode.NewMotionVector && rejectZeroLastMotion &&
                vector.IsZero && modeInfo.ReferenceFrame == Av1ReferenceFrameType.Last)
            {
                return Av1RateDistortionStatistics.Invalid;
            }

            if (modeInfo.SecondaryReferenceFrame == Av1ReferenceFrameType.None)
            {
                for (int index = 0; index < 4; index++)
                {
                    Av1PredictionMode previousMode = (Av1PredictionMode)((int)Av1PredictionMode.SingleInterModeStart + index);
                    if (previousMode != modeInfo.Mode &&
                        searchState.EvaluatedModes[(int)previousMode][(int)modeInfo.ReferenceFrame] != 0 &&
                        searchState.MotionVectors[(int)previousMode][(int)modeInfo.ReferenceFrame] == vector)
                    {
                        return Av1RateDistortionStatistics.Invalid;
                    }
                }
            }

            if (searchFilters && ((vector.Row | vector.Column) & 7) != 0)
            {
                this.SelectEstimatedInterFilter(
                    writer,
                    macroBlock,
                    blockOrigin,
                    blockSize,
                    ref modeInfo,
                    vector,
                    secondaryVector,
                    primaryReference.GetPlane(Av1Plane.Y),
                    secondaryReference.GetPlane(Av1Plane.Y),
                    evaluateBlue,
                    evaluateRed,
                    ref prediction,
                    ref scratch,
                    out variance,
                    out squaredError);
            }
            else
            {
                this.PrepareInterPlanePrediction(
                    vector,
                    secondaryVector,
                    Av1Plane.Y,
                    modeInfo.Mode,
                    modeInfo.ReferenceFrame,
                    modeInfo.SecondaryReferenceFrame,
                    false,
                    modeInfo.CompoundType,
                    modeInfo.CompoundWedgeIndex,
                    modeInfo.CompoundWedgeSign,
                    modeInfo.DifferenceWeightedMaskType,
                    modeInfo.HorizontalInterpolationFilter,
                    modeInfo.VerticalInterpolationFilter,
                    primaryReference.GetPlane(Av1Plane.Y),
                    secondaryReference.GetPlane(Av1Plane.Y),
                    blockOrigin,
                    0,
                    0,
                    blockSize,
                    prediction,
                    workspace.Residual);

                Buffer2DRegion<TSample> sourcePlane = this.source.GetPlane(Av1Plane.Y);
                int width = blockSize.GetWidth();
                int height = blockSize.GetHeight();
                TOperator.GetMoments(
                    Av1TransformBlockEncoder.GetPlaneSpan(sourcePlane, blockOrigin),
                    sourcePlane.Stride,
                    prediction,
                    width,
                    width,
                    height,
                    out int sum,
                    out long error);

                int shift = this.bitDepth.GetBitCount() - 8;
                sum = (sum + ((1 << shift) >> 1)) >> shift;
                squaredError = (uint)((error + ((1L << (2 * shift)) >> 1)) >> (2 * shift));
                variance = (uint)Math.Max(0, squaredError - (((long)sum * sum) >> BitOperations.Log2((uint)(width * height))));
                modeInfo.TransformSize = this.SelectEstimatedInterTransformSize(
                    blockSize, variance, squaredError, evaluateBlue, evaluateRed, false, out _);
            }

            if (modeInfo.SecondaryReferenceFrame == Av1ReferenceFrameType.None)
            {
                int modeIndex = (int)modeInfo.Mode - (int)Av1PredictionMode.SingleInterModeStart;
                searchState.Variances[modeIndex][(int)modeInfo.ReferenceFrame] = variance;
                if (vector.IsZero)
                {
                    int globalIndex = (int)Av1PredictionMode.GlobalMotionVector - (int)Av1PredictionMode.SingleInterModeStart;
                    searchState.Variances[globalIndex][(int)modeInfo.ReferenceFrame] = variance;
                }
            }

            if (searchState.SkipByPredictionError(
                this.picture.Parent.EncodingSpeed, blockSize, squaredError))
            {
                return Av1RateDistortionStatistics.Invalid;
            }

            // Prediction-error rejection precedes chroma work. Only color-sensitive planes need
            // predictors for this estimate; final reconstruction still writes every coded plane.
            if (!earlyTermination)
            {
                for (int planeIndex = 1; planeIndex <= 2; planeIndex++)
                {
                    if (!(planeIndex == 1 ? evaluateBlue : evaluateRed))
                    {
                        continue;
                    }

                    Av1Plane plane = (Av1Plane)planeIndex;
                    this.PrepareInterPlanePrediction(
                        vector,
                        secondaryVector,
                        plane,
                        modeInfo.Mode,
                        modeInfo.ReferenceFrame,
                        modeInfo.SecondaryReferenceFrame,
                        false,
                        modeInfo.CompoundType,
                        modeInfo.CompoundWedgeIndex,
                        modeInfo.CompoundWedgeSign,
                        modeInfo.DifferenceWeightedMaskType,
                        modeInfo.HorizontalInterpolationFilter,
                        modeInfo.VerticalInterpolationFilter,
                        primaryReference.GetPlane(plane),
                        secondaryReference.GetPlane(plane),
                        blockOrigin,
                        this.source.ChromaSubsamplingX,
                        this.source.ChromaSubsamplingY,
                        blockSize,
                        planeIndex == 1 ? workspace.BluePrediction : workspace.RedPrediction,
                        workspace.Residual);
                }
            }

            return this.EstimateInterResidual(
                writer,
                macroBlock,
                blockOrigin,
                blockSize,
                prediction,
                workspace.BluePrediction,
                workspace.RedPrediction,
                evaluateBlue,
                evaluateRed,
                modeInfo.TransformSize,
                earlyTermination,
                squaredError,
                out chromaDistortion,
                out initialSkip);
        }

        /// <summary>
        /// Selects a luma interpolation filter using prediction-error estimates.
        /// </summary>
        /// <param name="writer">The current tile syntax costs.</param>
        /// <param name="macroBlock">The current coding-block neighbors.</param>
        /// <param name="blockOrigin">The luma coding-block origin.</param>
        /// <param name="blockSize">The coding-block geometry.</param>
        /// <param name="modeInfo">The candidate syntax and selected transform size.</param>
        /// <param name="vector">The primary displacement.</param>
        /// <param name="secondaryVector">The secondary displacement.</param>
        /// <param name="primaryReference">The primary bordered luma plane.</param>
        /// <param name="secondaryReference">The secondary bordered luma plane.</param>
        /// <param name="evaluateBlue">Whether blue-difference residuals participate in mode selection.</param>
        /// <param name="evaluateRed">Whether red-difference residuals participate in mode selection.</param>
        /// <param name="prediction">The winning packed predictor on return.</param>
        /// <param name="scratch">The alternate packed predictor storage on return.</param>
        /// <param name="variance">The winning normalized prediction variance.</param>
        /// <param name="squaredError">The winning normalized squared error.</param>
        private void SelectEstimatedInterFilter(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            ref Av1EncoderBlockModeInfo modeInfo,
            Av1MotionVector vector,
            Av1MotionVector secondaryVector,
            Buffer2DRegion<TSample> primaryReference,
            Buffer2DRegion<TSample> secondaryReference,
            bool evaluateBlue,
            bool evaluateRed,
            ref Span<TSample> prediction,
            ref Span<TSample> scratch,
            out uint variance,
            out uint squaredError)
        {
            Av1EncoderInterPredictionWorkspace<TSample> workspace = this.blockWorkspace.GetInterPredictionWorkspace<TSample>();
            Buffer2DRegion<TSample> sourcePlane = this.source.GetPlane(Av1Plane.Y);
            int width = blockSize.GetWidth();
            int height = blockSize.GetHeight();
            int sampleCount = width * height;
            int sampleCountLog2 = BitOperations.Log2((uint)sampleCount);
            int normalizationShift = this.bitDepth.GetBitCount() - 8;
            int acQuantizer = Av1QuantizationLookup.GetAcQuant(
                this.quantization.QIndex[0], this.quantization.DeltaQAc[0], this.bitDepth);

            int context = Av1SymbolContextHelper.GetSwitchableInterpolationContext(modeInfo, macroBlock, 0);
            long bestCost = long.MaxValue;
            Av1InterpolationFilter bestFilter = Av1InterpolationFilter.Regular;
            Av1TransformSize bestTransform = Av1TransformSize.Size4x4;
            variance = 0;
            squaredError = 0;
            ReadOnlySpan<Av1InterpolationFilter> filters =
            [
                Av1InterpolationFilter.Regular,
                Av1InterpolationFilter.Smooth,
                Av1InterpolationFilter.Sharp
            ];

            foreach (Av1InterpolationFilter filter in filters)
            {
                this.PrepareInterPlanePrediction(
                    vector,
                    secondaryVector,
                    Av1Plane.Y,
                    modeInfo.Mode,
                    modeInfo.ReferenceFrame,
                    modeInfo.SecondaryReferenceFrame,
                    false,
                    modeInfo.CompoundType,
                    modeInfo.CompoundWedgeIndex,
                    modeInfo.CompoundWedgeSign,
                    modeInfo.DifferenceWeightedMaskType,
                    filter,
                    filter,
                    primaryReference,
                    secondaryReference,
                    blockOrigin,
                    0,
                    0,
                    blockSize,
                    scratch,
                    workspace.Residual);

                TOperator.GetMoments(
                    Av1TransformBlockEncoder.GetPlaneSpan(sourcePlane, blockOrigin),
                    sourcePlane.Stride,
                    scratch,
                    width,
                    width,
                    height,
                    out int sum,
                    out long error);

                sum = (sum + ((1 << normalizationShift) >> 1)) >> normalizationShift;
                error = (error + ((1L << (2 * normalizationShift)) >> 1)) >> (2 * normalizationShift);
                uint currentVariance = (uint)Math.Max(0, error - (((long)sum * sum) >> sampleCountLog2));
                Av1TransformSize transformSize = this.SelectEstimatedInterTransformSize(
                    blockSize, currentVariance, (uint)error, evaluateBlue, evaluateRed, false, out bool forceSkip);

                int rate;
                long distortion;
                if (forceSkip)
                {
                    rate = 0;
                    distortion = error << 4;
                }
                else
                {
                    Av1RateDistortion.ModelPredictionError(
                        blockSize, error, sampleCount, acQuantizer, this.bitDepth, this.rateMultiplier, out rate, out distortion);
                }

                rate += writer.GetSwitchableInterpolationFilterCost(filter, context);
                long cost = Av1RateDistortion.GetCost(this.rateMultiplier, rate, distortion);
                if (cost < bestCost)
                {
                    bestCost = cost;
                    bestFilter = filter;
                    bestTransform = transformSize;
                    variance = currentVariance;
                    squaredError = (uint)error;

                    // Exchange borrowed views, not their contents. The winning predictor remains intact
                    // while the next filter writes into the other worker buffer.
                    Span<TSample> previous = prediction;
                    prediction = scratch;
                    scratch = previous;
                }
            }

            modeInfo.HorizontalInterpolationFilter = bestFilter;
            modeInfo.VerticalInterpolationFilter = bestFilter;
            modeInfo.TransformSize = bestTransform;
        }

        /// <summary>
        /// Models the chroma residuals selected by the block's color-sensitivity decision.
        /// </summary>
        /// <param name="blockOrigin">The luma coding-block origin.</param>
        /// <param name="blockSize">The luma coding-block geometry.</param>
        /// <param name="bluePrediction">The tightly packed blue-difference predictor.</param>
        /// <param name="redPrediction">The tightly packed red-difference predictor.</param>
        /// <param name="evaluateBlue">Whether blue-difference distortion participates in the mode decision.</param>
        /// <param name="evaluateRed">Whether red-difference distortion participates in the mode decision.</param>
        /// <returns>The combined chroma estimate, including the prediction-only alternative.</returns>
        private Av1RateDistortionStatistics EstimateInterChroma(
            Point blockOrigin,
            Av1BlockSize blockSize,
            ReadOnlySpan<TSample> bluePrediction,
            ReadOnlySpan<TSample> redPrediction,
            bool evaluateBlue,
            bool evaluateRed)
        {
            int subX = this.source.ChromaSubsamplingX;
            int subY = this.source.ChromaSubsamplingY;
            Av1BlockSize chromaSize = blockSize.GetSubsampled(subX != 0, subY != 0);
            int width = chromaSize.GetWidth();
            int height = chromaSize.GetHeight();
            int sampleCountLog2 = BitOperations.Log2((uint)(width * height));
            Point origin = Av1TileWriter.GetChromaBlockOrigin(blockOrigin, subX, subY);
            int normalizationShift = this.bitDepth.GetBitCount() - 8;
            int rate = 0;
            long distortion = 0;
            long predictionDistortion = 0;
            for (int planeIndex = 1; planeIndex <= 2; planeIndex++)
            {
                if (!(planeIndex == 1 ? evaluateBlue : evaluateRed))
                {
                    continue;
                }

                Buffer2DRegion<TSample> sourcePlane = this.source.GetPlane((Av1Plane)planeIndex);
                ReadOnlySpan<TSample> prediction = planeIndex == 1 ? bluePrediction : redPrediction;
                TOperator.GetMoments(
                    Av1TransformBlockEncoder.GetPlaneSpan(sourcePlane, origin),
                    sourcePlane.Stride,
                    prediction,
                    width,
                    width,
                    height,
                    out int sum,
                    out long squaredError);

                // Normalize moments independently before removing the mean. The coded block includes
                // its extended edge samples; using only the visible rectangle would change the model.
                sum = (sum + ((1 << normalizationShift) >> 1)) >> normalizationShift;
                squaredError = (squaredError + ((1L << (2 * normalizationShift)) >> 1)) >> (2 * normalizationShift);
                long variance = Math.Max(0, squaredError - (((long)sum * sum) >> sampleCountLog2));
                predictionDistortion += squaredError << 4;
                int dcStep = Av1QuantizationLookup.GetDcQuant(
                    this.quantization.QIndex[0], this.quantization.DeltaQDc[planeIndex], this.bitDepth) >> 3;

                int acStep = Av1QuantizationLookup.GetAcQuant(
                    this.quantization.QIndex[0], this.quantization.DeltaQAc[planeIndex], this.bitDepth) >> 3;

                // DC energy uses half the modeled rate and half the AC distortion scale.
                // Removing the transform's factor of eight from both steps keeps the model in pixel units.
                Av1RateDistortion.EstimateLaplacian(
                    squaredError - variance, sampleCountLog2, dcStep, out int dcRate, out long dcDistortion);

                Av1RateDistortion.EstimateLaplacian(
                    variance, sampleCountLog2, acStep, out int acRate, out long acDistortion);

                rate += (dcRate >> 1) + acRate;
                distortion += (dcDistortion << 3) + (acDistortion << 4);
            }

            if (Av1RateDistortion.GetCost(this.rateMultiplier, rate, distortion) >=
                Av1RateDistortion.GetCost(this.rateMultiplier, 0, predictionDistortion))
            {
                rate = 0;
                distortion = predictionDistortion;
            }

            return new(this.rateMultiplier, rate, distortion)
            {
                PredictionDistortion = predictionDistortion,
                ResidualRate = rate,
                HasCoefficients = rate != 0,
                AllTransformsEmpty = rate == 0
            };
        }

        /// <summary>
        /// Estimates luma transform cost and combines the active chroma planes without losing a rejected skip alternative.
        /// </summary>
        /// <param name="writer">The current symbol-cost state.</param>
        /// <param name="macroBlock">The coding-block neighbors and frame edges.</param>
        /// <param name="blockOrigin">The luma coding-block origin.</param>
        /// <param name="blockSize">The coding-block geometry.</param>
        /// <param name="lumaPrediction">The tightly packed luma predictor.</param>
        /// <param name="bluePrediction">The tightly packed blue-difference predictor.</param>
        /// <param name="redPrediction">The tightly packed red-difference predictor.</param>
        /// <param name="evaluateBlue">Whether blue-difference distortion participates in selection.</param>
        /// <param name="evaluateRed">Whether red-difference distortion participates in selection.</param>
        /// <param name="transformSize">The square luma estimation transform.</param>
        /// <param name="earlyTermination">Whether all-plane skip has already been established.</param>
        /// <param name="lumaSquaredError">The normalized luma prediction error before transform scaling.</param>
        /// <param name="chromaDistortion">The modeled chroma distortion, or the maximum value when chroma is not evaluated.</param>
        /// <param name="initialSkip">Whether the selected skip required no coded luma alternative.</param>
        /// <returns>The residual estimate, including transform-skip syntax.</returns>
        private Av1RateDistortionStatistics EstimateInterResidual(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            ReadOnlySpan<TSample> lumaPrediction,
            ReadOnlySpan<TSample> bluePrediction,
            ReadOnlySpan<TSample> redPrediction,
            bool evaluateBlue,
            bool evaluateRed,
            Av1TransformSize transformSize,
            bool earlyTermination,
            long lumaSquaredError,
            out long chromaDistortion,
            out bool initialSkip)
        {
            initialSkip = earlyTermination;
            chromaDistortion = long.MaxValue;
            int skipContext = Av1TileWriter.GetSkipContext(macroBlock);
            int skipRate = writer.GetSkipCost(true, skipContext);
            long predictionDistortion = lumaSquaredError << 4;
            if (earlyTermination)
            {
                return new(this.rateMultiplier, skipRate, predictionDistortion)
                {
                    PredictionDistortion = predictionDistortion,
                    AllTransformsEmpty = true
                };
            }

            int width = blockSize.GetWidth();
            int height = blockSize.GetHeight();
            Av1EncoderInterPredictionWorkspace<TSample> workspace = this.blockWorkspace.GetInterPredictionWorkspace<TSample>();
            Span<short> residual = workspace.Residual;
            TOperator.SubtractPrediction(
                this.source.GetPlane(Av1Plane.Y), blockOrigin, lumaPrediction, residual, width, height);

            // Estimation visits transforms whose origins remain inside the coded frame. It still
            // transforms the full padded block at each edge, matching the predictor's sample extent.
            Size extent = new(
                width + (Math.Min(0, macroBlock.ToRightEdge) >> 3),
                height + (Math.Min(0, macroBlock.ToBottomEdge) >> 3));

            Av1IntraModeEstimator.Estimate(
                this.blockWorkspace,
                residual,
                width,
                extent,
                transformSize,
                this.quantization.QIndex[0],
                this.quantization.DeltaQDc[0],
                this.quantization.DeltaQAc[0],
                this.bitDepth,
                false,
                out int rate,
                out long distortion,
                out bool skip);

            int codedRate = rate + writer.GetSkipCost(false, skipContext);
            long codedDistortion = distortion;
            bool retainCodedAlternative = false;
            if (skip || Av1RateDistortion.GetCost(this.rateMultiplier, rate, distortion) >=
                Av1RateDistortion.GetCost(this.rateMultiplier, 0, predictionDistortion))
            {
                retainCodedAlternative = !skip;
                rate = skipRate;
                distortion = predictionDistortion;
                skip = true;
            }
            else
            {
                rate = codedRate;
            }

            if (evaluateBlue || evaluateRed)
            {
                Av1RateDistortionStatistics chroma = this.EstimateInterChroma(
                    blockOrigin, blockSize, bluePrediction, redPrediction, evaluateBlue, evaluateRed);

                chromaDistortion = chroma.Distortion;
                ref Av1EstimatedInterSearchState state = ref this.blockWorkspace.EstimatedInterSearchState;
                state.MinimumChromaDistortion = Math.Min(state.MinimumChromaDistortion, chromaDistortion);
                predictionDistortion += chroma.PredictionDistortion;
                if (skip && chroma.HasCoefficients && retainCodedAlternative)
                {
                    // Chroma can require residual syntax even when luma alone favors skipping.
                    // Restore the coded luma estimate before adding chroma to avoid mixing alternatives.
                    rate = codedRate;
                    distortion = codedDistortion;
                    predictionDistortion = lumaSquaredError << 4;
                }

                rate += chroma.Rate;
                distortion += chroma.Distortion;
                skip &= !chroma.HasCoefficients;
            }

            initialSkip = skip && !retainCodedAlternative;
            return new(this.rateMultiplier, rate, distortion)
            {
                PredictionDistortion = predictionDistortion,
                HasCoefficients = !skip,
                AllTransformsEmpty = skip
            };
        }

        /// <summary>
        /// Selects a square estimation transform from residual energy and quantizer thresholds.
        /// </summary>
        /// <param name="blockSize">The coding-block geometry.</param>
        /// <param name="variance">The normalized residual variance.</param>
        /// <param name="squaredError">The normalized residual squared error.</param>
        /// <param name="evaluateBlue">Whether blue-difference distortion must be evaluated.</param>
        /// <param name="evaluateRed">Whether red-difference distortion must be evaluated.</param>
        /// <param name="boostedSegment">Whether cyclic refresh boosts the current segment.</param>
        /// <param name="forceSkip">Whether low residual energy permits transform skipping.</param>
        /// <returns>The square transform size used to estimate the luma residual.</returns>
        private Av1TransformSize SelectEstimatedInterTransformSize(
            Av1BlockSize blockSize,
            uint variance,
            uint squaredError,
            bool evaluateBlue,
            bool evaluateRed,
            bool boostedSegment,
            out bool forceSkip)
        {
            Av1TransformMode transformMode = this.picture.Parent.FrameHeader.TransformMode;
            Av1TransformSize maximumSize = transformMode == Av1TransformMode.Only4x4
                ? Av1TransformSize.Size4x4
                : blockSize.GetMaximumTransformSize().GetSquareSize();

            Av1TransformSize size = maximumSize;
            forceSkip = false;
            if (transformMode == Av1TransformMode.Select)
            {
                uint multiplier = 8;
                uint varianceThreshold = 0;
                bool highVariance = true;
                int level = this.picture.Parent.SpeedSettings.EstimatedTransformQuantizerLevel;
                if (level != 0)
                {
                    multiplier -= (uint)this.quantization.QIndex[0] >> 6;
                    int step = Av1QuantizationLookup.GetAcQuant(
                        this.quantization.QIndex[0], this.quantization.DeltaQAc[0], this.bitDepth) >> (this.bitDepth.GetBitCount() - 5);

                    uint squaredStep = (uint)(step * step);
                    varianceThreshold = 2 * squaredStep;
                    if (level >= 2)
                    {
                        // Skip estimation only when both spatial and prediction errors are small and
                        // neither chroma plane can invalidate the luma-only skip decision.
                        forceSkip = squaredError < squaredStep && this.interSourceVariance < squaredStep && !evaluateBlue && !evaluateRed;
                        highVariance = variance >= varianceThreshold;
                    }
                }

                // Dominant DC or weak AC residuals favor the largest permitted square transform.
                // A boosted high-variance segment instead retains the smaller estimation transform.
                size = squaredError > ((variance * multiplier) >> 2) || variance < varianceThreshold
                    ? maximumSize
                    : Av1TransformSize.Size8x8;

                if (boostedSegment && highVariance)
                {
                    size = Av1TransformSize.Size8x8;
                }
            }

            if (transformMode != Av1TransformMode.Only4x4 && blockSize > Av1BlockSize.Block32x32)
            {
                size = Av1TransformSize.Size16x16;
            }

            return size > Av1TransformSize.Size16x16 ? Av1TransformSize.Size16x16 : size;
        }
    }
}

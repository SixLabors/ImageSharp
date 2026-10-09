// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
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
        /// <param name="tables">The rate tables of the tile.</param>
        /// <param name="modeWorkspace">The mode decision buffers of the block.</param>
        /// <param name="transformCoefficients">The forward transform output buffer.</param>
        /// <param name="dequantizedCoefficients">The dequantized coefficient buffer.</param>
        /// <param name="searchDequantizedCoefficients">The dequantized coefficients of one estimation transform.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
        /// <param name="transformTypeProbabilities">The transform type probabilities of every update type and size.</param>
        /// <param name="blockResidual">The residual buffer of the motion search.</param>
        /// <param name="estimationRowCoefficients">The coefficients of one row of estimation transforms.</param>
        /// <param name="interWorkspace">The inter prediction buffers of the block.</param>
        /// <param name="motionSearchPrediction">The prediction buffer of the motion search.</param>
        /// <param name="motionVectorCosts">The motion vector rates of the frame precision.</param>
        /// <param name="firstIntermediate">The compound intermediate of the first reference.</param>
        /// <param name="secondIntermediate">The compound intermediate of the second reference.</param>
        /// <param name="compoundMask">The blend mask of a masked compound prediction.</param>
        /// <param name="transformEdges">The transform size context edges of the tile.</param>
        /// <param name="paletteEdges">The palette color context edges of the tile.</param>
        /// <param name="lumaCoefficientEdges">The luma coefficient context edges of the tile.</param>
        /// <param name="blueCoefficientEdges">The blue-difference coefficient context edges of the tile.</param>
        /// <param name="redCoefficientEdges">The red-difference coefficient context edges of the tile.</param>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="displacementVectors">The displacement vectors of the picture, one per allocation entry.</param>
        /// <param name="referenceContexts">The motion vector reference contexts of the picture, one per allocation entry.</param>
        /// <param name="encoderSegmentMap">The segment identifiers that the encoder keeps for the frame.</param>
        /// <param name="searchSegmentMap">The segment map that the frame buffer holds while the frame is searched.</param>
        /// <param name="previousSegmentMap">The segment map of the primary reference frame, or an empty map.</param>
        /// <param name="superblockCoefficients">The coefficients and transform block states of the superblock.</param>
        /// <param name="workspaceStorage">The storage of the block workspace, which holds the search buffers of every block.</param>
        /// <param name="sourceLuma">The samples of the complete source luma plane, read once per frame pass.</param>
        /// <param name="sourceBlue">The samples of the complete source blue-difference plane, read once per frame pass.</param>
        /// <param name="sourceRed">The samples of the complete source red-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionLuma">The samples of the complete reconstructed luma plane, read once per frame pass.</param>
        /// <param name="reconstructionBlue">The samples of the complete reconstructed blue-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionRed">The samples of the complete reconstructed red-difference plane, read once per frame pass.</param>
        /// <param name="macroBlock">The coding-block neighbors and frame edges.</param>
        /// <param name="origin">The luma block origin.</param>
        /// <param name="modeInfo">The selected block syntax.</param>
        /// <param name="block">The selected prediction-unit state.</param>
        /// <param name="palette">The retained palette, when selected.</param>
        private void EncodeEstimatedInterBlock(
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            in Av1EncoderModeDecisionWorkspace<TSample> modeWorkspace,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> searchDequantizedCoefficients,
            Span<int> transformWorkspace,
            ReadOnlySpan<int> transformTypeProbabilities,
            Span<short> blockResidual,
            Span<int> estimationRowCoefficients,
            in Av1EncoderInterPredictionWorkspace<TSample> interWorkspace,
            Span<TSample> motionSearchPrediction,
            in Av1MotionVectorCosts motionVectorCosts,
            Span<ushort> firstIntermediate,
            Span<ushort> secondIntermediate,
            Span<byte> compoundMask,
            in Av1NeighborEdges<byte> transformEdges,
            in Av1NeighborEdges<Av1EncoderPaletteInfo> paletteEdges,
            in Av1NeighborEdges<byte> lumaCoefficientEdges,
            in Av1NeighborEdges<byte> blueCoefficientEdges,
            in Av1NeighborEdges<byte> redCoefficientEdges,
            ReadOnlySpan<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            Span<Av1EncoderDisplacementVector> displacementVectors,
            Span<Av1EncoderReferenceContext> referenceContexts,
            Span<byte> encoderSegmentMap,
            Span<byte> searchSegmentMap,
            ReadOnlySpan<byte> previousSegmentMap,
            Span<int> superblockCoefficients,
            Span<int> workspaceStorage,
            ReadOnlySpan<TSample> sourceLuma,
            ReadOnlySpan<TSample> sourceBlue,
            ReadOnlySpan<TSample> sourceRed,
            Span<TSample> reconstructionLuma,
            Span<TSample> reconstructionBlue,
            Span<TSample> reconstructionRed,
            Av1MacroBlockD macroBlock,
            Point origin,
            ref Av1MacroBlockModeInfo modeInfo,
            ref Av1EncoderBlockStruct block,
            ref Av1EncoderPaletteInfo palette)
        {
            Av1PictureParentControlSet parent = this.picture.Parent;
            Av1EncoderSpeedSettings settings = parent.SpeedSettings;
            Av1BlockSize blockSize = modeInfo.Block.BlockSize;
            ref Av1EstimatedInterSearchState state = ref this.blockWorkspace.EstimatedInterSearchState;

            // The search predicts a reference of another size from its resized copy, and the selected block from
            // the reference itself.
            this.predictsFromSearchReferences = true;
            state.Reset(
                tables.ModeCosts,
                Av1TileWriter.GetIntraInterContext(modeInfoGrid, modeInfoAllocation, macroBlock),
                Av1SymbolContextHelper.GetCompoundReferenceTypeContext(modeInfoGrid, modeInfoAllocation, macroBlock),
                parent.FrameHeader.ReferenceMode == ObuReferenceMode.ReferenceModeSelect);

            state.SetMotionVectorBias(
                settings.IsRealtime && parent.EncoderOptions.UsesConstantBitRate,
                blockSize,
                parent.EncodingSpeed,
                this.interSourceVariance,
                this.sourceSadLevel == Av1SourceSadLevel.High,
                this.GetNeighborMotionVector(modeInfoGrid, modeInfoAllocation, displacementVectors, macroBlock, origin, above: true),
                this.GetNeighborMotionVector(modeInfoGrid, modeInfoAllocation, displacementVectors, macroBlock, origin, above: false));

            InlineArray2<byte> colorSensitivity = this.superblockColorSensitivity;
            this.blockColorSensitivity = colorSensitivity;
            bool measureSad = this.estimatedReferencePruning <= 2 || colorSensitivity[0] == 2 || colorSensitivity[1] == 2;

            InlineArray3<Av1ReferenceMotionVectors> referenceVectorStorage = default;
            Span<Av1ReferenceMotionVectors> referenceVectors = referenceVectorStorage;
            this.PrepareEstimatedReference(
                in tables,
                modeInfoGrid,
                modeInfoAllocation,
                displacementVectors,
                referenceContexts,
                sourceLuma,
                sourceBlue,
                sourceRed,
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

            if (lowTemporalVariance || this.estimatedReferencePruning > 2 ||
                (this.estimatedReferencePruning > 1 && blockSize > Av1BlockSize.Block64x64))
            {
                useGolden = false;
                useAlternate = false;
            }

            if (this.interSourceVariance < 200 && this.sourceSadLevel >= Av1SourceSadLevel.Low)
            {
                useGolden &= this.goldenColorSensitivity[0] != 1 && this.goldenColorSensitivity[1] != 1;
                useAlternate &= this.alternateColorSensitivity[0] != 1 && this.alternateColorSensitivity[1] != 1;
            }

            if (!useGolden && !useAlternate && this.estimatedReferencePruning > 2 &&
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
                    in tables,
                    modeInfoGrid,
                    modeInfoAllocation,
                    displacementVectors,
                    referenceContexts,
                    sourceLuma,
                    sourceBlue,
                    sourceRed,
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
                    in tables,
                    modeInfoGrid,
                    modeInfoAllocation,
                    displacementVectors,
                    referenceContexts,
                    sourceLuma,
                    sourceBlue,
                    sourceRed,
                    macroBlock,
                    origin,
                    blockSize,
                    modeInfo.Block.PartitionType,
                    Av1ReferenceFrameType.Alternate,
                    this.searchReferences.Span[(int)Av1ReferenceFrameType.Alternate].CodedView.GetPlane(Av1Plane.Y),
                    measureSad,
                    ref referenceVectors[2]);
            }

            if (block.HasChroma)
            {
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

                    this.SetEstimatedColorSensitivity(sourceLuma, sourceBlue, sourceRed, origin, blockSize, lumaSad, false, colorSensitivity);
                }
            }

            this.blockColorSensitivity = colorSensitivity;
            bool evaluateBlue = block.HasChroma && colorSensitivity[0] != 0;
            bool evaluateRed = block.HasChroma && colorSensitivity[1] != 0;
            int filterPolicy = this.GetEstimatedFilterSearchPolicy(
                modeInfoGrid,
                modeInfoAllocation,
                macroBlock,
                origin,
                blockSize,
                this.IsCyclicRefreshBoosted,
                out Av1InterpolationFilter filter);

            Span<TSample> winningPrediction = interWorkspace.SelectedLumaReconstruction;
            this.lastLumaPredictionBuffer = -1;
            Span<TSample> prediction = interWorkspace.LumaPrediction;
            Span<TSample> alternatePrediction = interWorkspace.LumaCandidateReconstruction;
            Av1EncoderBlockModeInfo winner = modeInfo.Block;
            winner.SecondaryReferenceFrame = Av1ReferenceFrameType.None;
            bool checkGlobalMotion = true;
            uint zeroMotionError = uint.MaxValue;
            ReadOnlySpan<Av1EncoderFrame<TSample>> searchReferences = this.searchReferences.Span;
            for (int index = 0; index < 3 && !state.EndSearch; index++)
            {
                Av1ReferenceFrameType reference = index == 0 ? Av1ReferenceFrameType.Last
                    : index == 1 ? Av1ReferenceFrameType.Golden : Av1ReferenceFrameType.Alternate;

                if (state.UseReference[(int)reference])
                {
                    this.SelectEstimatedReferenceModes(
                        writer,
                        in tables,
                        searchDequantizedCoefficients,
                        transformWorkspace,
                        blockResidual,
                        estimationRowCoefficients,
                        in interWorkspace,
                        motionSearchPrediction,
                        in motionVectorCosts,
                        firstIntermediate,
                        secondIntermediate,
                        compoundMask,
                        modeInfoGrid,
                        modeInfoAllocation,
                        displacementVectors,
                        workspaceStorage,
                        sourceLuma,
                        sourceBlue,
                        sourceRed,
                        macroBlock,
                        origin,
                        reference,
                        searchReferences[(int)reference].CodedView,
                        referenceVectors[index],
                        lowTemporalVariance,
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
                        ref alternatePrediction);
                }
            }

            this.SelectEstimatedCompoundModes(
                writer,
                in tables,
                searchDequantizedCoefficients,
                transformWorkspace,
                blockResidual,
                estimationRowCoefficients,
                in interWorkspace,
                motionSearchPrediction,
                in motionVectorCosts,
                firstIntermediate,
                secondIntermediate,
                compoundMask,
                modeInfoGrid,
                modeInfoAllocation,
                displacementVectors,
                referenceContexts,
                sourceLuma,
                sourceBlue,
                sourceRed,
                macroBlock,
                origin,
                evaluateBlue,
                evaluateRed,
                ref winner,
                ref winningPrediction,
                ref prediction,
                ref alternatePrediction);

            this.SelectEstimatedIntraModes(
                writer,
                in tables,
                searchDequantizedCoefficients,
                transformWorkspace,
                estimationRowCoefficients,
                in interWorkspace,
                modeInfoGrid,
                modeInfoAllocation,
                workspaceStorage,
                sourceLuma,
                sourceBlue,
                sourceRed,
                reconstructionLuma,
                reconstructionBlue,
                reconstructionRed,
                macroBlock,
                origin,
                evaluateBlue,
                evaluateRed,
                state.BestEarlyTermination,
                state.BestInitialSkip,
                this.IsCyclicRefreshBoosted,
                ref winner,
                ref winningPrediction,
                ref prediction);

            if (winner.ReferenceFrame > Av1ReferenceFrameType.Intra)
            {
                // The intra estimate borrows the storage that holds the inter search prediction, so the prediction of the winner is built
                // again.
                Av1ReferenceFrameType secondaryReferenceFrame = winner.SecondaryReferenceFrame;
                bool compoundWinner = secondaryReferenceFrame > Av1ReferenceFrameType.Intra;
                Av1EncoderFrame<TSample>.PlanarView primaryPlanes = this.searchReferences.Span[(int)winner.ReferenceFrame].CodedView;
                Av1EncoderFrame<TSample>.PlanarView secondaryPlanes = compoundWinner
                    ? this.searchReferences.Span[(int)secondaryReferenceFrame].CodedView : primaryPlanes;

                this.PrepareInterPlanePrediction(
                    state.WinningMotionVectors[(int)winner.Mode][(int)winner.ReferenceFrame],
                    compoundWinner ? state.WinningMotionVectors[(int)winner.Mode][(int)secondaryReferenceFrame] : default,
                    Av1Plane.Y,
                    winner.Mode,
                    winner.ReferenceFrame,
                    secondaryReferenceFrame,
                    false,
                    winner.CompoundType,
                    winner.CompoundWedgeIndex,
                    winner.CompoundWedgeSign,
                    winner.DifferenceWeightedMaskType,
                    winner.HorizontalInterpolationFilter,
                    winner.VerticalInterpolationFilter,
                    primaryPlanes.GetPlane(Av1Plane.Y),
                    secondaryPlanes.GetPlane(Av1Plane.Y),
                    origin,
                    0,
                    0,
                    blockSize,
                    winningPrediction,
                    interWorkspace.Residual,
                    interWorkspace.FilterRows,
                    firstIntermediate,
                    secondIntermediate,
                    compoundMask,
                    modeInfoGrid,
                    modeInfoAllocation,
                    displacementVectors,
                    sourceLuma,
                    sourceBlue,
                    sourceRed);
            }

            bool paletteSelected = this.SelectEstimatedPalette(
                writer,
                in tables,
                in modeWorkspace,
                transformCoefficients,
                dequantizedCoefficients,
                transformWorkspace,
                transformTypeProbabilities,
                in transformEdges,
                in paletteEdges,
                in lumaCoefficientEdges,
                modeInfoGrid,
                modeInfoAllocation,
                superblockCoefficients,
                sourceLuma,
                sourceBlue,
                sourceRed,
                reconstructionLuma,
                reconstructionBlue,
                reconstructionRed,
                macroBlock,
                origin,
                evaluateBlue,
                evaluateRed,
                ref winner,
                ref palette);

            this.predictsFromSearchReferences = false;
            modeInfo.Block = winner;

            // The selected block updates its cyclic refresh segment, which sets the quantizer that codes it. An output encode prices its
            // segment syntax. The estimated search never sets the skip flag that the state update tests, so every output block is priced.
            Av1RateDistortionStatistics selectedStatistics = state.BestStatistics;
            bool cyclicRefreshEncode = this.UpdatesCyclicRefreshSegment(selectedStatistics, out bool countSegments);
            if (cyclicRefreshEncode)
            {
                Av1MotionVector firstVector = winner.ReferenceFrame > Av1ReferenceFrameType.Intra
                    ? state.WinningMotionVectors[(int)winner.Mode][(int)winner.ReferenceFrame]
                    : default;

                this.UpdateCyclicRefreshSegment(
                    encoderSegmentMap, searchSegmentMap, origin, ref modeInfo.Block, ref block, firstVector, selectedStatistics, countSegments);

                if (countSegments)
                {
                    this.AddSegmentPredictionCosts(in tables, searchSegmentMap, previousSegmentMap, macroBlock, origin, blockSize, modeInfo.Block.SegmentId);
                }
            }

            // Filter intra and the dynamic reference list entry share one packed byte, so only an intra winner can carry the filter-intra
            // sentinel. An inter winner of this path always takes the first list entry.
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
                    in tables,
                    transformCoefficients,
                    dequantizedCoefficients,
                    transformWorkspace,
                    in interWorkspace,
                    firstIntermediate,
                    secondIntermediate,
                    compoundMask,
                    in lumaCoefficientEdges,
                    in blueCoefficientEdges,
                    in redCoefficientEdges,
                    modeInfoGrid,
                    modeInfoAllocation,
                    displacementVectors,
                    superblockCoefficients,
                    sourceLuma,
                    sourceBlue,
                    sourceRed,
                    reconstructionLuma,
                    reconstructionBlue,
                    reconstructionRed,
                    macroBlock,
                    origin,
                    ref modeInfo.Block,
                    block,
                    reference,
                    secondaryReference,
                    vector,
                    secondaryVector,
                    winningPrediction);

                this.picture.SetDisplacementVector(displacementVectors, position, vector);
                if (compound)
                {
                    this.picture.SetSecondaryDisplacementVector(referenceContexts, position, secondaryVector);
                }
            }
            else
            {
                // The final intra prediction reads reconstructed neighbors. The prediction-only candidate must not be copied here, because
                // each transform now adds its selected residual. An intra block never signals skip. The flag is clear before the search,
                // and neither the search nor the block encode sets it for an intra winner.
                modeInfo.Block.Skip = false;
                this.EncodeSelectedIntraPlane(
                    writer,
                    in tables,
                    in modeWorkspace,
                    transformCoefficients,
                    dequantizedCoefficients,
                    transformWorkspace,
                    in lumaCoefficientEdges,
                    modeInfoGrid,
                    modeInfoAllocation,
                    superblockCoefficients,
                    sourceLuma,
                    sourceBlue,
                    sourceRed,
                    reconstructionLuma,
                    reconstructionBlue,
                    reconstructionRed,
                    macroBlock,
                    origin,
                    blockSize,
                    Av1Plane.Y,
                    winner.Mode,
                    winner.TransformSize,
                    this.codedAreaLuma,
                    modeInfo.Block.Skip,
                    paletteSelected ? palette.GetColors(Av1Plane.Y) : default);

                if (block.HasChroma)
                {
                    Av1TransformSize chromaTransform = this.BlockLossless ? Av1TransformSize.Size4x4
                        : blockSize.GetMaxUvTransformSize(this.source.ChromaSubsamplingX != 0, this.source.ChromaSubsamplingY != 0);

                    this.EncodeSelectedIntraPlane(
                        writer,
                        in tables,
                        in modeWorkspace,
                        transformCoefficients,
                        dequantizedCoefficients,
                        transformWorkspace,
                        in blueCoefficientEdges,
                        modeInfoGrid,
                        modeInfoAllocation,
                        superblockCoefficients,
                        sourceLuma,
                        sourceBlue,
                        sourceRed,
                        reconstructionLuma,
                        reconstructionBlue,
                        reconstructionRed,
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
                        in tables,
                        in modeWorkspace,
                        transformCoefficients,
                        dequantizedCoefficients,
                        transformWorkspace,
                        in redCoefficientEdges,
                        modeInfoGrid,
                        modeInfoAllocation,
                        superblockCoefficients,
                        sourceLuma,
                        sourceBlue,
                        sourceRed,
                        reconstructionLuma,
                        reconstructionBlue,
                        reconstructionRed,
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

            if (this.estimatedLeafEncode == EstimatedLeafEncode.Output)
            {
                Av1MotionVector codedVector = winner.ReferenceFrame > Av1ReferenceFrameType.Intra
                    ? state.WinningMotionVectors[(int)winner.Mode][(int)winner.ReferenceFrame]
                    : default;

                this.CountNoiseStillBlock(origin, modeInfo.Block, codedVector);
            }

            if (cyclicRefreshEncode)
            {
                this.ResetCyclicRefreshSkip(
                    encoderSegmentMap,
                    searchSegmentMap,
                    macroBlock,
                    origin,
                    ref modeInfo.Block,
                    ref block,
                    modeInfo.Block.Skip || this.encodedWithoutCoefficients,
                    countSegments);
            }

            if (winner.SecondaryReferenceFrame == Av1ReferenceFrameType.None && settings.AdaptiveModeThresholdLevel != 0)
            {
                Av1ModeThresholds.UpdateEstimated(
                    this.blockWorkspace.GetModeThresholdFactors(workspaceStorage),
                    blockSize,
                    winner.ReferenceFrame,
                    winner.Mode,
                    settings.AdaptiveModeThresholdLevel);
            }

            Size lumaExtent = GetCodedTransformExtent(macroBlock, blockSize, modeInfo.Block.TransformSize, 0, 0);
            this.codedAreaLuma += lumaExtent.Width * lumaExtent.Height;
            if (block.HasChroma)
            {
                int subX = this.source.ChromaSubsamplingX;
                int subY = this.source.ChromaSubsamplingY;
                Av1TransformSize chromaTransform = this.BlockLossless ? Av1TransformSize.Size4x4
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
        /// <param name="tables">The rate tables of the tile.</param>
        /// <param name="searchDequantizedCoefficients">The dequantized coefficients of one estimation transform.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
        /// <param name="blockResidual">The residual buffer of the motion search.</param>
        /// <param name="estimationRowCoefficients">The coefficients of one row of estimation transforms.</param>
        /// <param name="interWorkspace">The inter prediction buffers of the block.</param>
        /// <param name="motionSearchPrediction">The prediction buffer of the motion search.</param>
        /// <param name="motionVectorCosts">The motion vector rates of the frame precision.</param>
        /// <param name="firstIntermediate">The compound intermediate of the first reference.</param>
        /// <param name="secondIntermediate">The compound intermediate of the second reference.</param>
        /// <param name="compoundMask">The blend mask of a masked compound prediction.</param>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="displacementVectors">The displacement vectors of the picture, one per allocation entry.</param>
        /// <param name="referenceContexts">The motion vector reference contexts of the picture, one per allocation entry.</param>
        /// <param name="sourceLuma">The samples of the complete source luma plane, read once per frame pass.</param>
        /// <param name="sourceBlue">The samples of the complete source blue-difference plane, read once per frame pass.</param>
        /// <param name="sourceRed">The samples of the complete source red-difference plane, read once per frame pass.</param>
        /// <param name="macroBlock">The block neighbors and frame boundaries.</param>
        /// <param name="origin">The luma block origin.</param>
        /// <param name="evaluateBlue">Whether blue-difference error participates in selection.</param>
        /// <param name="evaluateRed">Whether red-difference error participates in selection.</param>
        /// <param name="winner">The retained prediction syntax.</param>
        /// <param name="winningPrediction">The retained luma predictor.</param>
        /// <param name="prediction">The next candidate's luma predictor.</param>
        /// <param name="alternatePrediction">The alternate interpolation predictor storage.</param>
        private void SelectEstimatedCompoundModes(
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            Span<int> searchDequantizedCoefficients,
            Span<int> transformWorkspace,
            Span<short> blockResidual,
            Span<int> estimationRowCoefficients,
            in Av1EncoderInterPredictionWorkspace<TSample> interWorkspace,
            Span<TSample> motionSearchPrediction,
            in Av1MotionVectorCosts motionVectorCosts,
            Span<ushort> firstIntermediate,
            Span<ushort> secondIntermediate,
            Span<byte> compoundMask,
            ReadOnlySpan<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            ReadOnlySpan<Av1EncoderDisplacementVector> displacementVectors,
            ReadOnlySpan<Av1EncoderReferenceContext> referenceContexts,
            ReadOnlySpan<TSample> sourceLuma,
            ReadOnlySpan<TSample> sourceBlue,
            ReadOnlySpan<TSample> sourceRed,
            Av1MacroBlockD macroBlock,
            Point origin,
            bool evaluateBlue,
            bool evaluateRed,
            ref Av1EncoderBlockModeInfo winner,
            ref Span<TSample> winningPrediction,
            ref Span<TSample> prediction,
            ref Span<TSample> alternatePrediction)
        {
            Av1PictureParentControlSet parent = this.picture.Parent;
            ref Av1EstimatedInterSearchState state = ref this.blockWorkspace.EstimatedInterSearchState;
            Av1BlockSize blockSize = winner.BlockSize;
            int minimumDimension = Math.Min(this.source.Width, this.source.Height);
            if (state.EndSearch || blockSize <= Av1BlockSize.Block16x16 ||
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
                modeInfoGrid,
                modeInfoAllocation,
                displacementVectors,
                referenceContexts,
                macroBlock,
                position,
                blockSize,
                winner.PartitionType,
                this.picture.Sequence.SequenceHeader,
                parent.FrameHeader,
                Av1ReferenceFrameType.Last,
                Av1ReferenceFrameType.Alternate);

            Av1EncoderFrame<TSample>.PlanarView alternate = this.searchReferences.Span[(int)Av1ReferenceFrameType.Alternate].CodedView;
            bool globalOnly = minimumDimension < 360 || parent.EncodingSpeed >= HeifEncodingSpeed.Level9;
            bool useSuperblockMotion = this.usePartitionMotion && blockSize >=
                (this.picture.Sequence.SequenceHeader.SuperblockSize == Av1BlockSize.Block128x128
                    ? Av1BlockSize.Block64x64 : Av1BlockSize.Block32x32);

            _ = this.GetEstimatedFilterSearchPolicy(modeInfoGrid, modeInfoAllocation, macroBlock, origin, blockSize, true, out Av1InterpolationFilter filter);
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
                    in tables,
                    searchDequantizedCoefficients,
                    transformWorkspace,
                    blockResidual,
                    estimationRowCoefficients,
                    in interWorkspace,
                    motionSearchPrediction,
                    in motionVectorCosts,
                    firstIntermediate,
                    secondIntermediate,
                    compoundMask,
                    modeInfoGrid,
                    modeInfoAllocation,
                    displacementVectors,
                    sourceLuma,
                    sourceBlue,
                    sourceRed,
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
                    vectors,
                    out _,
                    ref prediction,
                    ref alternatePrediction,
                    out _,
                    out uint squaredError,
                    out _,
                    out bool initialSkip,
                    out bool compoundEarlyTermination);

                if (statistics.Cost == long.MaxValue)
                {
                    continue;
                }

                // The estimate charges the compound mode and the primary reference branch. Complete
                // compound-reference syntax is emitted only after the winning mode has been selected.
                int rate = Av1SymbolEncoder.GetInterCompoundModeCost(tables.ModeCosts, mode, vectors.ModeContext) +
                    state.ReferenceCosts[(int)Av1ReferenceFrameType.Last];

                Av1RateDistortionStatistics syntax = new(this.rateMultiplier, rate, 0);
                statistics.Add(this.rateMultiplier, syntax);
                if (statistics.Cost < state.BestStatistics.Cost)
                {
                    state.BestStatistics = statistics;
                    state.BestSquaredError = squaredError;
                    state.BestInitialSkip = initialSkip;
                    state.BestEarlyTermination = compoundEarlyTermination;
                    state.WinningMotionVectors[(int)mode][(int)Av1ReferenceFrameType.Last] = primaryVector;
                    state.WinningMotionVectors[(int)mode][(int)Av1ReferenceFrameType.Alternate] = secondaryVector;
                    candidate.Skip = statistics.AllTransformsEmpty;
                    winner = candidate;
                    Span<TSample> previous = winningPrediction;
                    winningPrediction = prediction;
                    prediction = previous;
                }

                // A compound candidate always follows the single modes. Thus a best mode with early termination ends the search, unless
                // the superblock motion is still untested.
                if (state.BestEarlyTermination && (!useSuperblockMotion || state.SuperblockMotionTested))
                {
                    state.EndSearch = true;
                    return;
                }
            }
        }

        /// <summary>
        /// Compares a luma palette with the retained mode when an intra mode won the estimated search.
        /// </summary>
        /// <param name="writer">The tile symbol costs.</param>
        /// <param name="tables">The rate tables of the tile.</param>
        /// <param name="modeWorkspace">The mode decision buffers of the block.</param>
        /// <param name="transformCoefficients">The forward transform output buffer.</param>
        /// <param name="dequantizedCoefficients">The dequantized coefficient buffer.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
        /// <param name="transformTypeProbabilities">The transform type probabilities of every update type and size.</param>
        /// <param name="transformEdges">The transform size context edges of the tile.</param>
        /// <param name="paletteEdges">The palette color context edges of the tile.</param>
        /// <param name="lumaCoefficientEdges">The luma coefficient context edges of the tile.</param>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="superblockCoefficients">The coefficients and transform block states of the superblock.</param>
        /// <param name="sourceLuma">The samples of the complete source luma plane, read once per frame pass.</param>
        /// <param name="sourceBlue">The samples of the complete source blue-difference plane, read once per frame pass.</param>
        /// <param name="sourceRed">The samples of the complete source red-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionLuma">The samples of the complete reconstructed luma plane, read once per frame pass.</param>
        /// <param name="reconstructionBlue">The samples of the complete reconstructed blue-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionRed">The samples of the complete reconstructed red-difference plane, read once per frame pass.</param>
        /// <param name="macroBlock">The coding-block neighbors and edges.</param>
        /// <param name="origin">The luma coding-block origin.</param>
        /// <param name="evaluateBlue">Whether blue-difference error participates in selection.</param>
        /// <param name="evaluateRed">Whether red-difference error participates in selection.</param>
        /// <param name="winner">The retained prediction syntax.</param>
        /// <param name="palette">The selected palette and its colors.</param>
        /// <returns>Whether palette reconstruction replaced the retained mode.</returns>
        private bool SelectEstimatedPalette(
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            in Av1EncoderModeDecisionWorkspace<TSample> modeWorkspace,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> transformWorkspace,
            ReadOnlySpan<int> transformTypeProbabilities,
            in Av1NeighborEdges<byte> transformEdges,
            in Av1NeighborEdges<Av1EncoderPaletteInfo> paletteEdges,
            in Av1NeighborEdges<byte> lumaCoefficientEdges,
            ReadOnlySpan<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            Span<int> superblockCoefficients,
            ReadOnlySpan<TSample> sourceLuma,
            ReadOnlySpan<TSample> sourceBlue,
            ReadOnlySpan<TSample> sourceRed,
            Span<TSample> reconstructionLuma,
            Span<TSample> reconstructionBlue,
            Span<TSample> reconstructionRed,
            Av1MacroBlockD macroBlock,
            Point origin,
            bool evaluateBlue,
            bool evaluateRed,
            ref Av1EncoderBlockModeInfo winner,
            ref Av1EncoderPaletteInfo palette)
        {
            Av1PictureParentControlSet parent = this.picture.Parent;
            ref Av1EstimatedInterSearchState state = ref this.blockWorkspace.EstimatedInterSearchState;
            Av1BlockSize blockSize = winner.BlockSize;

            // A color-sensitive block in a frame with little change skips the palette.
            bool skipPalette = (evaluateBlue || evaluateRed) && this.sourceSadLevel != Av1SourceSadLevel.Zero &&
                !parent.HighSourceSad && parent.FrameSourceSad < 1000;

            bool tryPalette = !skipPalette &&
                Av1TileWriter.IsPaletteAllowed(parent.FrameHeader.AllowScreenContentTools, blockSize) &&
                winner.ReferenceFrame == Av1ReferenceFrameType.Intra && this.interSourceVariance > 0 &&
                (parent.HighSourceSad || this.interSourceVariance > 300);

            if (!tryPalette)
            {
                return false;
            }

            const int colorThreshold = 64;
            Span<Av1EncoderTransformBlockState> states = this.coefficientBuffer.GetTransformBlockSpan(superblockCoefficients, Av1Plane.Y)[
                (this.codedAreaLuma / Av1EncoderCoefficientBuffer.TransformBlockUnitCoefficientCount)..];

            Av1TransformSize transformSize = winner.TransformSize;
            Av1RateDistortionStatistics paletteStatistics = state.BestStatistics;

            // The estimated search runs its palette through the complete search at the default stage. The transform size search uses the
            // settings of the default stage.
            Av1EncoderEvaluationStage previousStage = this.blockWorkspace.EvaluationStage;
            this.blockWorkspace.EvaluationStage = Av1EncoderEvaluationStage.Default;
            Av1ModeCosts modeCosts = tables.ModeCosts;
            bool selected = this.SelectLumaPalette(
                writer,
                in tables,
                in modeWorkspace,
                transformCoefficients,
                dequantizedCoefficients,
                transformWorkspace,
                transformTypeProbabilities,
                in transformEdges,
                in paletteEdges,
                in lumaCoefficientEdges,
                modeInfoGrid,
                modeInfoAllocation,
                sourceLuma,
                sourceBlue,
                sourceRed,
                reconstructionLuma,
                reconstructionBlue,
                reconstructionRed,
                macroBlock,
                origin,
                blockSize,
                states,
                colorThreshold,
                Av1SymbolEncoder.GetInterFrameLumaModeCost(modeCosts, Av1PredictionMode.DC, blockSize),
                ref paletteStatistics,
                ref palette,
                ref transformSize);

            this.blockWorkspace.EvaluationStage = previousStage;
            if (selected)
            {
                bool skip = !paletteStatistics.HasCoefficients;
                int skipContext = Av1TileWriter.GetSkipContext(modeInfoGrid, modeInfoAllocation, macroBlock);
                int rate = state.ReferenceCosts[(int)Av1ReferenceFrameType.Intra] +
                    (skip ? 0 : paletteStatistics.Rate) + Av1SymbolEncoder.GetSkipCost(modeCosts, skip, skipContext);

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
        /// Refines uncertain superblock color decisions using the current block's stationary chroma error.
        /// </summary>
        /// <param name="sourceLuma">The samples of the complete source luma plane, read once per frame pass.</param>
        /// <param name="sourceBlue">The samples of the complete source blue-difference plane, read once per frame pass.</param>
        /// <param name="sourceRed">The samples of the complete source red-difference plane, read once per frame pass.</param>
        /// <param name="origin">The luma block origin.</param>
        /// <param name="blockSize">The coding-block geometry.</param>
        /// <param name="lumaSad">The spatial predictor SAD with the smallest displacement.</param>
        /// <param name="forceCheck">Whether partition selection requires a fresh block-level measurement.</param>
        /// <param name="colorSensitivity">The inherited color decisions, updated in place.</param>
        private void SetEstimatedColorSensitivity(
            ReadOnlySpan<TSample> sourceLuma,
            ReadOnlySpan<TSample> sourceBlue,
            ReadOnlySpan<TSample> sourceRed,
            Point origin,
            Av1BlockSize blockSize,
            int lumaSad,
            bool forceCheck,
            Span<byte> colorSensitivity)
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
            const int spatialThreshold = 50;
            const int lowVarianceSadThreshold = 100;
            const int sadThreshold = 40;

            // These activity thresholds are per 4x4 unit, not per sample. Luma and subsampled chroma use the same scale before their errors
            // are compared. The first test turns the chroma check off, but only at a low noise level.
            int normalizedLumaSad = lumaSad >> (BitOperations.Log2((uint)(blockSize.GetWidth() * blockSize.GetHeight())) - 4);
            if (parent.RunningNoiseLevel == Av1NoiseEstimate.LowLevel &&
                this.interSourceVariance > (frameWidth > 1920 ? 5000 : 1000) && normalizedLumaSad < 50)
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
                    Av1PlaneRegion<TSample> sourcePlane = this.source.GetPlane(plane);
                    Av1PlaneRegion<TSample> referencePlane = this.reference.GetPlane(plane);
                    int sad = (int)(TOperator.SumAbsoluteDifferences(
                        Av1TransformBlockEncoder.GetPlaneSpan(SelectPlane(plane, sourceLuma, sourceBlue, sourceRed), sourcePlane, chromaOrigin),
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
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="macroBlock">The reconstructed block neighbors.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The coding-block geometry.</param>
        /// <param name="boostedSegment">Whether cyclic refresh boosts the current segment.</param>
        /// <param name="filter">The concrete interpolation filter used when search is omitted.</param>
        /// <returns>Zero to omit search, one for mode-dependent search, or two to force eligible searches.</returns>
        private int GetEstimatedFilterSearchPolicy(
            ReadOnlySpan<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            bool boostedSegment,
            out Av1InterpolationFilter filter)
        {
            filter = Av1InterpolationFilter.Regular;
            Av1PictureParentControlSet parent = this.picture.Parent;
            if (!parent.SpeedSettings.UseEstimatedFilterChessboard || this.sourceSadLevel <= Av1SourceSadLevel.VeryLow)
            {
                return 1;
            }

            if (!macroBlock.IsUpAvailable || !macroBlock.IsLeftAvailable)
            {
                return 2;
            }

            Av1EncoderBlockModeInfo above = macroBlock.GetRelativeModeInfo(modeInfoGrid, modeInfoAllocation, -macroBlock.ModeInfoStride).Block;
            Av1EncoderBlockModeInfo left = macroBlock.GetRelativeModeInfo(modeInfoGrid, modeInfoAllocation, -1).Block;
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
        /// <param name="tables">The rate tables of the tile.</param>
        /// <param name="searchDequantizedCoefficients">The dequantized coefficients of one estimation transform.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
        /// <param name="estimationRowCoefficients">The coefficients of one row of estimation transforms.</param>
        /// <param name="interWorkspace">The inter prediction buffers of the block.</param>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="workspaceStorage">The storage of the block workspace, which holds the search buffers of every block.</param>
        /// <param name="sourceLuma">The samples of the complete source luma plane, read once per frame pass.</param>
        /// <param name="sourceBlue">The samples of the complete source blue-difference plane, read once per frame pass.</param>
        /// <param name="sourceRed">The samples of the complete source red-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionLuma">The samples of the complete reconstructed luma plane, read once per frame pass.</param>
        /// <param name="reconstructionBlue">The samples of the complete reconstructed blue-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionRed">The samples of the complete reconstructed red-difference plane, read once per frame pass.</param>
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
            in Av1CoefficientTables tables,
            Span<int> searchDequantizedCoefficients,
            Span<int> transformWorkspace,
            Span<int> estimationRowCoefficients,
            in Av1EncoderInterPredictionWorkspace<TSample> interWorkspace,
            ReadOnlySpan<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            Span<int> workspaceStorage,
            ReadOnlySpan<TSample> sourceLuma,
            ReadOnlySpan<TSample> sourceBlue,
            ReadOnlySpan<TSample> sourceRed,
            Span<TSample> reconstructionLuma,
            Span<TSample> reconstructionBlue,
            Span<TSample> reconstructionRed,
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
            int dcQuant = Av1QuantizationLookup.GetDcQuant(
                this.quantization.QIndex[0], this.quantization.DeltaQDc[0], this.bitDepth);

            int shift = this.bitDepth.GetBitCount() - 8;
            int penalty = ((20 * dcQuant) + ((1 << shift) >> 1)) >> shift;
            int referenceRate = state.ReferenceCosts[(int)Av1ReferenceFrameType.Intra];
            int spatialThreshold = 50;
            int motionThreshold = 32;
            if (!speed.UseEstimatedAlternateReference && speed.GetEstimatedReferencePruningLevel() > 0)
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

                forceIntra = this.interSourceVariance < Math.Max(50, spatialThreshold >> 1) && this.sourceSadLevel >= Av1SourceSadLevel.High;

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

            if (!(state.BestStatistics.Cost == long.MaxValue || forceIntra ||
                (performIntra && !bestEarlyTermination && blockSize <= Av1BlockSize.Block32x32)))
            {
                return;
            }

            if (Av1RateDistortion.GetCost(this.rateMultiplier, referenceRate + penalty, 0) > state.BestStatistics.Cost)
            {
                return;
            }

            Av1TransformSize transformSize = parent.FrameHeader.TransformMode == Av1TransformMode.Only4x4
                ? Av1TransformSize.Size4x4 : blockSize.GetMaximumTransformSize().GetSquareSize();

            if (transformSize > Av1TransformSize.Size16x16)
            {
                transformSize = Av1TransformSize.Size16x16;
            }

            int modeMask = speed.GetEstimatedIntraModeMask(blockSize);
            Av1PredictionMode chromaMode = Av1PredictionMode.DC;
            Span<int> modeThresholdFactors = this.blockWorkspace.GetModeThresholdFactors(workspaceStorage);

            // Every intra mode of the block reads the same mode rates.
            Av1ModeCosts modeCosts = tables.ModeCosts;
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

                    if (mode != Av1PredictionMode.DC && parent.EncodingSpeed >= HeifEncodingSpeed.Level8 &&
                        !(evaluateBlue && evaluateRed &&
                            (parent.FrameSourceSad > 1.1 * parent.AverageSourceSad || boostedSegment || this.sourceSadLevel > Av1SourceSadLevel.Medium)))
                    {
                        continue;
                    }
                }

                if ((useThreshold || mode == Av1PredictionMode.Smooth) && Av1ModeThresholds.ShouldSkip(
                    modeThresholdFactors,
                    this.ModeThresholdQuantizerFactor,
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
                    searchDequantizedCoefficients,
                    transformWorkspace,
                    estimationRowCoefficients,
                    in interWorkspace,
                    modeInfoGrid,
                    modeInfoAllocation,
                    sourceLuma,
                    sourceBlue,
                    sourceRed,
                    reconstructionLuma,
                    reconstructionBlue,
                    reconstructionRed,
                    macroBlock,
                    blockOrigin,
                    blockSize,
                    mode,
                    chromaMode,
                    transformSize,
                    evaluateBlue,
                    evaluateRed,
                    prediction);

                this.RecordLumaPrediction(in interWorkspace, prediction);

                int rate = residual.Rate + referenceRate + penalty;
                if ((mode == Av1PredictionMode.Vertical || mode == Av1PredictionMode.Horizontal) && blockSize >= Av1BlockSize.Block8x8)
                {
                    rate += Av1SymbolEncoder.GetAngleDeltaCost(modeCosts, Av1Constants.MaxAngleDelta, mode);
                }

                if (mode == Av1PredictionMode.DC &&
                    Av1TileWriter.IsFilterIntraAllowedBlockSize(this.picture.Sequence.SequenceHeader.EnableFilterIntra, blockSize))
                {
                    rate += Av1SymbolEncoder.GetFilterIntraModeCost(modeCosts, Av1FilterIntraMode.AllFilterIntraModes, blockSize);
                }

                Av1RateDistortionStatistics statistics = new(this.rateMultiplier, rate, residual.Distortion)
                {
                    HasCoefficients = residual.HasCoefficients,
                    AllTransformsEmpty = residual.AllTransformsEmpty
                };

                if (statistics.Cost < state.BestStatistics.Cost)
                {
                    state.BestStatistics = statistics;
                    winner.Mode = mode;
                    winner.UvMode = (Av1ChromaPredictionMode)mode;
                    chromaMode = mode;
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
        /// <param name="searchDequantizedCoefficients">The dequantized coefficients of one estimation transform.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
        /// <param name="estimationRowCoefficients">The coefficients of one row of estimation transforms.</param>
        /// <param name="interWorkspace">The inter prediction buffers of the block.</param>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="sourceLuma">The samples of the complete source luma plane, read once per frame pass.</param>
        /// <param name="sourceBlue">The samples of the complete source blue-difference plane, read once per frame pass.</param>
        /// <param name="sourceRed">The samples of the complete source red-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionLuma">The samples of the complete reconstructed luma plane, read once per frame pass.</param>
        /// <param name="reconstructionBlue">The samples of the complete reconstructed blue-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionRed">The samples of the complete reconstructed red-difference plane, read once per frame pass.</param>
        /// <param name="macroBlock">The block's reconstructed neighbors and frame edges.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The coding-block geometry.</param>
        /// <param name="mode">The luma prediction mode.</param>
        /// <param name="chromaMode">
        /// The chroma prediction mode. This is the mode of the best intra candidate so far, or DC before any mode becomes the best.
        /// </param>
        /// <param name="transformSize">The luma residual estimation size.</param>
        /// <param name="evaluateBlue">Whether blue-difference distortion participates in selection.</param>
        /// <param name="evaluateRed">Whether red-difference distortion participates in selection.</param>
        /// <param name="lumaPrediction">The candidate's tightly packed prediction storage.</param>
        /// <returns>The modeled residual rate and distortion before mode syntax.</returns>
        private Av1RateDistortionStatistics EstimateInterFrameIntraCandidate(
            Span<int> searchDequantizedCoefficients,
            Span<int> transformWorkspace,
            Span<int> estimationRowCoefficients,
            in Av1EncoderInterPredictionWorkspace<TSample> interWorkspace,
            ReadOnlySpan<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            ReadOnlySpan<TSample> sourceLuma,
            ReadOnlySpan<TSample> sourceBlue,
            ReadOnlySpan<TSample> sourceRed,
            Span<TSample> reconstructionLuma,
            Span<TSample> reconstructionBlue,
            Span<TSample> reconstructionRed,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            Av1PredictionMode mode,
            Av1PredictionMode chromaMode,
            Av1TransformSize transformSize,
            bool evaluateBlue,
            bool evaluateRed,
            Span<TSample> lumaPrediction)
        {
            // The intra mode-decision workspace shares storage with the inter predictions that are live here, so
            // the reference edges use their own storage.
            const int edgeLength = (2 * Av1Constants.MaxTransformSize) + 1;
            Span<TSample> edges = stackalloc TSample[2 * edgeLength];
            Span<TSample> above = edges[..edgeLength];
            Span<TSample> left = edges[edgeLength..];
            Span<short> residual = interWorkspace.Residual;
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
                    : this.BlockLossless
                        ? Av1TransformSize.Size4x4 : blockSize.GetMaxUvTransformSize(subX != 0, subY != 0);

                int transformWidth = predictionSize.GetWidth();
                int transformHeight = predictionSize.GetHeight();
                Size extent = GetCodedTransformExtent(macroBlock, planeSize, predictionSize, subX, subY);
                Span<TSample> prediction = planeIndex == 0 ? lumaPrediction
                    : planeIndex == 1 ? interWorkspace.BluePrediction : interWorkspace.RedPrediction;

                Av1PlaneRegion<TSample> sourcePlane = this.source.GetPlane(plane);
                Av1PlaneRegion<TSample> reconstructedPlane = this.reconstruction.GetPlane(plane);
                ReadOnlySpan<TSample> sourceSamples = SelectPlane(plane, sourceLuma, sourceBlue, sourceRed);
                ReadOnlySpan<TSample> reconstructedBlock = Av1TransformBlockEncoder.GetPlaneSpan(
                    SelectPlane(plane, reconstructionLuma, reconstructionBlue, reconstructionRed), reconstructedPlane, origin);

                Av1PartitionType partitionType = macroBlock.GetRelativeModeInfo(modeInfoGrid, modeInfoAllocation, 0).Block.PartitionType;
                bool smoothEdges = this.UseSmoothIntraEdges(modeInfoGrid, modeInfoAllocation, macroBlock, blockOrigin, blockSize, plane);

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
                                    reconstructedBlock,
                                    reconstructedPlane.Stride,
                                    blockOrigin,
                                    blockSize,
                                    macroBlock,
                                    partitionType,
                                    y / transformHeight,
                                    x / transformWidth,
                                    width,
                                    predictionSize,
                                    subX,
                                    subY,
                                    prediction,
                                    above,
                                    left,
                                    true,
                                    out bool hasLeft,
                                    out bool hasAbove);

                                Point transformOrigin = origin + new Size(x, y);
                                Span<TSample> predictedTransform = prediction[((y * width) + x)..];
                                ReadOnlySpan<TSample> sourceTransform = sourceSamples[sourcePlane.GetOffset(transformOrigin.X, transformOrigin.Y)..];
                                Av1TransformBlockEncoder.PrepareIntraPrediction<TSample, TOperator>(
                                    transformWorkspace,
                                    sourceTransform,
                                    sourcePlane.Stride,
                                    predictedTransform,
                                    width,
                                    above.Slice(1, transformWidth + transformHeight),
                                    left.Slice(1, transformWidth + transformHeight),
                                    hasLeft,
                                    hasAbove,
                                    planeIndex == 0 ? mode : chromaMode,
                                    0,
                                    this.picture.Sequence.SequenceHeader.EnableIntraEdgeFilter,
                                    smoothEdges,
                                    residual,
                                    predictionSize,
                                    this.bitDepth);

                                if (planeIndex != 0)
                                {
                                    TOperator.GetMoments(
                                        sourceTransform,
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
                                        this.blockQIndex, this.quantization.DeltaQDc[planeIndex], this.bitDepth) >> 3;

                                    int acStep = Av1QuantizationLookup.GetAcQuant(
                                        this.blockQIndex, this.quantization.DeltaQAc[planeIndex], this.bitDepth) >> 3;

                                    // Chroma is modeled per prediction unit. A sum of its moments across units first changes the DC energy and
                                    // the skip comparison.
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

                                    rate = (int)Math.Min((long)rate + unitRate, int.MaxValue / 2);
                                    distortion += unitDistortion;
                                }
                            }
                        }
                    }
                }

                if (planeIndex == 0)
                {
                    TOperator.SubtractPrediction(
                        sourceSamples[sourcePlane.GetOffset(origin.X, origin.Y)..],
                        sourcePlane.Stride,
                        prediction,
                        width,
                        residual,
                        width,
                        planeSize.GetHeight());

                    Size residualExtent = GetCodedTransformExtent(macroBlock, blockSize, transformSize, 0, 0);
                    Av1IntraModeEstimator.Estimate(
                        this.blockWorkspace,
                        estimationRowCoefficients,
                        searchDequantizedCoefficients,
                        transformWorkspace,
                        residual,
                        width,
                        residualExtent,
                        transformSize,
                        this.blockQIndex,
                        this.quantization.DeltaQDc[0],
                        this.quantization.DeltaQAc[0],
                        this.bitDepth,
                        out int lumaRate,
                        out long lumaDistortion,
                        out skip);

                    // Each addition clamps at half of the maximum integer, so it stays below the invalid-rate value.
                    rate = (int)Math.Min((long)rate + lumaRate, int.MaxValue / 2);
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
        /// <param name="tables">The rate tables of the tile.</param>
        /// <param name="transformCoefficients">The forward transform output buffer.</param>
        /// <param name="dequantizedCoefficients">The dequantized coefficient buffer.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
        /// <param name="interWorkspace">The inter prediction buffers of the block.</param>
        /// <param name="firstIntermediate">The compound intermediate of the first reference.</param>
        /// <param name="secondIntermediate">The compound intermediate of the second reference.</param>
        /// <param name="compoundMask">The blend mask of a masked compound prediction.</param>
        /// <param name="lumaCoefficientEdges">The luma coefficient context edges of the tile.</param>
        /// <param name="blueCoefficientEdges">The blue-difference coefficient context edges of the tile.</param>
        /// <param name="redCoefficientEdges">The red-difference coefficient context edges of the tile.</param>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="displacementVectors">The displacement vectors of the picture, one per allocation entry.</param>
        /// <param name="superblockCoefficients">The coefficients and transform block states of the superblock.</param>
        /// <param name="sourceLuma">The samples of the complete source luma plane, read once per frame pass.</param>
        /// <param name="sourceBlue">The samples of the complete source blue-difference plane, read once per frame pass.</param>
        /// <param name="sourceRed">The samples of the complete source red-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionLuma">The samples of the complete reconstructed luma plane, read once per frame pass.</param>
        /// <param name="reconstructionBlue">The samples of the complete reconstructed blue-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionRed">The samples of the complete reconstructed red-difference plane, read once per frame pass.</param>
        /// <param name="macroBlock">The current block's neighbors and edges.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="modeInfo">The selected inter syntax.</param>
        /// <param name="block">The selected block's chroma and motion syntax.</param>
        /// <param name="primaryReference">The primary bordered reference planes.</param>
        /// <param name="secondaryReference">The secondary bordered reference planes.</param>
        /// <param name="vector">The primary displacement.</param>
        /// <param name="secondaryVector">The secondary displacement.</param>
        /// <param name="lumaPrediction">The retained luma predictor.</param>
        private void EncodeEstimatedInterWinner(
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> transformWorkspace,
            in Av1EncoderInterPredictionWorkspace<TSample> interWorkspace,
            Span<ushort> firstIntermediate,
            Span<ushort> secondIntermediate,
            Span<byte> compoundMask,
            in Av1NeighborEdges<byte> lumaCoefficientEdges,
            in Av1NeighborEdges<byte> blueCoefficientEdges,
            in Av1NeighborEdges<byte> redCoefficientEdges,
            ReadOnlySpan<int> modeInfoGrid,
            ReadOnlySpan<Av1MacroBlockModeInfo> modeInfoAllocation,
            ReadOnlySpan<Av1EncoderDisplacementVector> displacementVectors,
            Span<int> superblockCoefficients,
            ReadOnlySpan<TSample> sourceLuma,
            ReadOnlySpan<TSample> sourceBlue,
            ReadOnlySpan<TSample> sourceRed,
            Span<TSample> reconstructionLuma,
            Span<TSample> reconstructionBlue,
            Span<TSample> reconstructionRed,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ref Av1EncoderBlockModeInfo modeInfo,
            Av1EncoderBlockStruct block,
            Av1EncoderFrame<TSample>.PlanarView primaryReference,
            Av1EncoderFrame<TSample>.PlanarView secondaryReference,
            Av1MotionVector vector,
            Av1MotionVector secondaryVector,
            ReadOnlySpan<TSample> lumaPrediction)
        {
            bool lossless = this.BlockLossless;
            modeInfo.Skip &= !lossless;
            modeInfo.TransformSize = lossless ? Av1TransformSize.Size4x4 : modeInfo.TransformSize;
            modeInfo.InterTransformSizes.Fill(modeInfo.TransformSize);
            int planeCount = block.HasChroma ? 3 : 1;

            // The search predicted a reference of another size from its resized copy and keeps no prediction of it. Thus the luma
            // prediction is built again from the original reference.
            if (this.IsScaledReference(modeInfo.ReferenceFrame) || this.IsScaledReference(modeInfo.SecondaryReferenceFrame))
            {
                Span<TSample> rebuilt = interWorkspace.SpareLumaReconstruction;
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
                    modeInfo.BlockSize,
                    rebuilt,
                    interWorkspace.Residual,
                    interWorkspace.FilterRows,
                    firstIntermediate,
                    secondIntermediate,
                    compoundMask,
                    modeInfoGrid,
                    modeInfoAllocation,
                    displacementVectors,
                    sourceLuma,
                    sourceBlue,
                    sourceRed);

                lumaPrediction = rebuilt;
            }

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

                int stride = planeBlock.GetWidth();
                ReadOnlySpan<TSample> prediction = lumaPrediction;
                if (planeIndex != 0)
                {
                    Span<TSample> chromaPrediction = planeIndex == 1 ? interWorkspace.BluePrediction : interWorkspace.RedPrediction;
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
                        interWorkspace.Residual,
                        interWorkspace.FilterRows,
                        firstIntermediate,
                        secondIntermediate,
                        compoundMask,
                        modeInfoGrid,
                        modeInfoAllocation,
                        displacementVectors,
                        sourceLuma,
                        sourceBlue,
                        sourceRed);

                    prediction = chromaPrediction;
                }
                else if (!modeInfo.Skip)
                {
                    Av1PlaneRegion<TSample> sourcePlane = this.source.GetPlane(plane);
                    TOperator.SubtractPrediction(
                        Av1TransformBlockEncoder.GetPlaneSpan(SelectPlane(plane, sourceLuma, sourceBlue, sourceRed), sourcePlane, planeOrigin),
                        sourcePlane.Stride,
                        prediction,
                        stride,
                        interWorkspace.Residual,
                        stride,
                        planeBlock.GetHeight());
                }

                Av1PlaneRegion<TSample> destinationPlane = this.reconstruction.GetPlane(plane);
                Span<TSample> destination = Av1TransformBlockEncoder.GetPlaneSpan(
                    SelectPlane(plane, reconstructionLuma, reconstructionBlue, reconstructionRed), destinationPlane, planeOrigin);

                Size extent = GetCodedTransformExtent(macroBlock, planeBlock, transformSize, subX, subY);
                int width = transformSize.GetWidth();
                int height = transformSize.GetHeight();
                int sampleCount = transformSize.GetSize2d();
                int offset = planeIndex == 0 ? this.codedAreaLuma : this.codedAreaChroma;
                Span<int> coefficients = this.coefficientBuffer.GetPlaneSpan(superblockCoefficients, plane);
                Span<Av1EncoderTransformBlockState> states = this.coefficientBuffer.GetTransformBlockSpan(superblockCoefficients, plane);
                Av1NeighborEdges<byte> neighbors = plane switch
                {
                    Av1Plane.Y => lumaCoefficientEdges,
                    Av1Plane.U => blueCoefficientEdges,
                    _ => redCoefficientEdges
                };

                int contextWidth = planeBlock.Get4x4WideCount();
                int contextHeight = planeBlock.Get4x4HighCount();
                Span<byte> transformContexts = interWorkspace.TransformContexts;
                Span<byte> top = transformContexts[..contextWidth];
                Span<byte> left = transformContexts.Slice(contextWidth, contextHeight);
                neighbors.Top.Slice(neighbors.GetTopIndex(planeOrigin), contextWidth).CopyTo(top);
                neighbors.Left.Slice(neighbors.GetLeftIndex(planeOrigin), contextHeight).CopyTo(left);
                Size contextSize = new(
                    this.picture.Parent.FrameHeader.ModeInfoColumnCount >> subX,
                    this.picture.Parent.FrameHeader.ModeInfoRowCount >> subY);

                // The prediction stays in block coordinates, but the coefficients follow the transform coding order. That order includes the
                // separate 64x64 regions of a larger coding block. A lossless block codes its 4x4 transforms in raster order, without the
                // luma transform tree, because the largest transform of a lossless segment is 4x4.
                Av1TransformSize rootSize = planeIndex == 0 && !lossless ? planeBlock.GetMaximumTransformSize() : transformSize;
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
                    Span<int> blockCoefficients = coefficients.Slice(offset, sampleCount);
                    if (modeInfo.Skip)
                    {
                        blockCoefficients.Clear();
                    }
                    else
                    {
                        Av1TransformBlockEncoder.EncodeLossyCandidate(
                            this.blockWorkspace,
                            writer,
                            in tables,
                            transformCoefficients,
                            dequantizedCoefficients,
                            transformWorkspace,
                            context,
                            interWorkspace.Residual[inputOffset..],
                            stride,
                            blockCoefficients,
                            transformSize,
                            Av1TransformType.DctDct,
                            this.blockQIndex,
                            this.BlockLossless,
                            this.quantization.DeltaQDc[planeIndex],
                            this.quantization.DeltaQAc[planeIndex],
                            this.bitDepth,
                            component,
                            this.rateMultiplier,
                            true,
                            false,
                            true,
                            0,
                            ref state);

                        // The inverse transform matches the forward transform, which the segment lossless flag selects.
                        if (state.EndOfBlock > 0)
                        {
                            TOperator.AddSelectedResidual(
                                dequantizedCoefficients,
                                transformWorkspace,
                                output,
                                destinationPlane.Stride,
                                transformSize,
                                plane,
                                this.bitDepth,
                                this.BlockLossless,
                                state);
                        }
                    }

                    allTransformsEmpty &= state.EndOfBlock == 0;
                    byte coefficientContext = Av1SymbolContextHelper.GetCoefficientContext(
                        blockCoefficients, transformSize, state.TransformType, state.EndOfBlock);

                    Av1TileWriter.UpdateCoefficientContexts(
                        transformTop, transformLeft, coefficientContext, planeOrigin + new Size(local.X, local.Y), contextSize);

                    offset += sampleCount;
                }
            }

            modeInfo.Skip = !lossless && allTransformsEmpty;
            if (modeInfo.Skip)
            {
                // A skipped inter block codes the largest transform. This size also sets how many coefficient positions the block owns where
                // the frame edge crosses it.
                modeInfo.TransformSize = modeInfo.BlockSize.GetMaximumTransformSize();
                modeInfo.InterTransformSizes.Fill(modeInfo.TransformSize);
            }
        }

        /// <summary>
        /// Evaluates an admitted reference's motion modes in prediction order and retains its best candidate.
        /// </summary>
        /// <param name="writer">The tile symbol costs.</param>
        /// <param name="tables">The rate tables of the tile.</param>
        /// <param name="searchDequantizedCoefficients">The dequantized coefficients of one estimation transform.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
        /// <param name="blockResidual">The residual buffer of the motion search.</param>
        /// <param name="estimationRowCoefficients">The coefficients of one row of estimation transforms.</param>
        /// <param name="interWorkspace">The inter prediction buffers of the block.</param>
        /// <param name="motionSearchPrediction">The prediction buffer of the motion search.</param>
        /// <param name="motionVectorCosts">The motion vector rates of the frame precision.</param>
        /// <param name="firstIntermediate">The compound intermediate of the first reference.</param>
        /// <param name="secondIntermediate">The compound intermediate of the second reference.</param>
        /// <param name="compoundMask">The blend mask of a masked compound prediction.</param>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="displacementVectors">The displacement vectors of the picture, one per allocation entry.</param>
        /// <param name="workspaceStorage">The storage of the block workspace, which holds the search buffers of every block.</param>
        /// <param name="sourceLuma">The samples of the complete source luma plane, read once per frame pass.</param>
        /// <param name="sourceBlue">The samples of the complete source blue-difference plane, read once per frame pass.</param>
        /// <param name="sourceRed">The samples of the complete source red-difference plane, read once per frame pass.</param>
        /// <param name="macroBlock">The current block's neighbors and edges.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="reference">The admitted reference label.</param>
        /// <param name="referencePlanes">The reference's bordered planes.</param>
        /// <param name="referenceVectors">The prepared spatial motion context.</param>
        /// <param name="forceLowTemporalSkip">Whether the partition's temporal variance permits early pruning.</param>
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
        /// <param name="alternatePrediction">The alternate filter predictor.</param>
        private void SelectEstimatedReferenceModes(
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            Span<int> searchDequantizedCoefficients,
            Span<int> transformWorkspace,
            Span<short> blockResidual,
            Span<int> estimationRowCoefficients,
            in Av1EncoderInterPredictionWorkspace<TSample> interWorkspace,
            Span<TSample> motionSearchPrediction,
            in Av1MotionVectorCosts motionVectorCosts,
            Span<ushort> firstIntermediate,
            Span<ushort> secondIntermediate,
            Span<byte> compoundMask,
            ReadOnlySpan<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            ReadOnlySpan<Av1EncoderDisplacementVector> displacementVectors,
            Span<int> workspaceStorage,
            ReadOnlySpan<TSample> sourceLuma,
            ReadOnlySpan<TSample> sourceBlue,
            ReadOnlySpan<TSample> sourceRed,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1ReferenceFrameType reference,
            Av1EncoderFrame<TSample>.PlanarView referencePlanes,
            Av1ReferenceMotionVectors referenceVectors,
            bool forceLowTemporalSkip,
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
            ref Span<TSample> alternatePrediction)
        {
            ref Av1EstimatedInterSearchState state = ref this.blockWorkspace.EstimatedInterSearchState;
            Av1EncoderSpeedSettings settings = this.picture.Parent.SpeedSettings;
            Av1BlockSize blockSize = winner.BlockSize;
            int referencePruning = this.estimatedReferencePruning;
            bool useSuperblockMotion = this.usePartitionMotion && blockSize >=
                (this.picture.Sequence.SequenceHeader.SuperblockSize == Av1BlockSize.Block128x128
                    ? Av1BlockSize.Block64x64 : Av1BlockSize.Block32x32);

            Span<int> modeThresholdFactors = this.blockWorkspace.GetModeThresholdFactors(workspaceStorage);
            for (int index = 0; index < 4 && !state.EndSearch; index++)
            {
                Av1PredictionMode mode = (Av1PredictionMode)((int)Av1PredictionMode.SingleInterModeStart + index);
                Av1MotionVector vector = state.MotionVectors[(int)mode][(int)reference];
                bool forceSuperblockMotion = useSuperblockMotion && reference == Av1ReferenceFrameType.Last &&
                    (mode == Av1PredictionMode.NewMotionVector ||
                        (mode is Av1PredictionMode.NearestMotionVector or Av1PredictionMode.NearMotionVector && vector == this.superblockMotion));

                if (!forceSuperblockMotion && (state.EvaluatedModes[(int)mode][(int)reference] != 0 ||
                    (mode == Av1PredictionMode.GlobalMotionVector && !checkGlobalMotion)))
                {
                    continue;
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
                    state.SkipByPredictorSad(mode, reference, settings.GetEstimatedReferencePruningLevel()) ||
                    Av1ModeThresholds.ShouldSkipEstimated(
                        modeThresholdFactors,
                        this.ModeThresholdQuantizerFactor,
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
                    in tables,
                    searchDequantizedCoefficients,
                    transformWorkspace,
                    blockResidual,
                    estimationRowCoefficients,
                    in interWorkspace,
                    motionSearchPrediction,
                    in motionVectorCosts,
                    firstIntermediate,
                    secondIntermediate,
                    compoundMask,
                    modeInfoGrid,
                    modeInfoAllocation,
                    displacementVectors,
                    sourceLuma,
                    sourceBlue,
                    sourceRed,
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
                    referenceVectors,
                    out int motionRate,
                    ref prediction,
                    ref alternatePrediction,
                    out uint variance,
                    out uint squaredError,
                    out long chromaDistortion,
                    out bool initialSkip,
                    out bool candidateEarlyTermination);

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
                    tables.ModeCosts,
                    referenceVectors,
                    this.rateMultiplier,
                    ref mode,
                    reference,
                    motionRate,
                    UsesMotionModeSearch(candidate, vector, searchFilters) ? uint.MaxValue : variance,
                    squaredError,
                    chromaDistortion,
                    ref statistics,
                    ref checkGlobalMotion))
                {
                    candidate.Mode = mode;
                    candidate.Skip = statistics.AllTransformsEmpty;
                    winner = candidate;
                    state.BestInitialSkip = initialSkip;
                    state.BestEarlyTermination = candidateEarlyTermination;

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
        /// <param name="tables">The rate tables of the tile.</param>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="displacementVectors">The displacement vectors of the picture, one per allocation entry.</param>
        /// <param name="referenceContexts">The motion vector reference contexts of the picture, one per allocation entry.</param>
        /// <param name="sourceLuma">The samples of the complete source luma plane, read once per frame pass.</param>
        /// <param name="sourceBlue">The samples of the complete source blue-difference plane, read once per frame pass.</param>
        /// <param name="sourceRed">The samples of the complete source red-difference plane, read once per frame pass.</param>
        /// <param name="macroBlock">The coding-block neighbors.</param>
        /// <param name="blockOrigin">The luma coding-block origin.</param>
        /// <param name="blockSize">The coding-block geometry.</param>
        /// <param name="partition">The containing partition syntax.</param>
        /// <param name="reference">The reference label.</param>
        /// <param name="referencePlane">The bordered reference luma plane.</param>
        /// <param name="measureSad">Whether spatial-predictor errors are required.</param>
        /// <param name="referenceVectors">The retained motion-reference context.</param>
        private void PrepareEstimatedReference(
            in Av1CoefficientTables tables,
            ReadOnlySpan<int> modeInfoGrid,
            ReadOnlySpan<Av1MacroBlockModeInfo> modeInfoAllocation,
            ReadOnlySpan<Av1EncoderDisplacementVector> displacementVectors,
            ReadOnlySpan<Av1EncoderReferenceContext> referenceContexts,
            ReadOnlySpan<TSample> sourceLuma,
            ReadOnlySpan<TSample> sourceBlue,
            ReadOnlySpan<TSample> sourceRed,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            Av1PartitionType partition,
            Av1ReferenceFrameType reference,
            Av1PlaneRegion<TSample> referencePlane,
            bool measureSad,
            ref Av1ReferenceMotionVectors referenceVectors)
        {
            Point position = new(blockOrigin.X >> 2, blockOrigin.Y >> 2);
            referenceVectors.Build(
                this.picture,
                modeInfoGrid,
                modeInfoAllocation,
                displacementVectors,
                referenceContexts,
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
                state.ModeCosts[index][slot] = Av1SymbolEncoder.GetInterModeCost(tables.ModeCosts, mode, referenceVectors.ModeContext);
            }

            // A reference of another size measures no predictor error.
            if (!measureSad || this.IsResizedReference(reference))
            {
                return;
            }

            Av1PlaneRegion<TSample> sourcePlane = this.source.GetPlane(Av1Plane.Y);
            ReadOnlySpan<TSample> source = Av1TransformBlockEncoder.GetPlaneSpan(sourceLuma, sourcePlane, blockOrigin);
            ReadOnlySpan<TSample> referenceSamples = referencePlane.Samples;
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
        /// <param name="tables">The rate tables of the tile.</param>
        /// <param name="searchDequantizedCoefficients">The dequantized coefficients of one estimation transform.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
        /// <param name="blockResidual">The residual buffer of the motion search.</param>
        /// <param name="estimationRowCoefficients">The coefficients of one row of estimation transforms.</param>
        /// <param name="interWorkspace">The inter prediction buffers of the block.</param>
        /// <param name="motionSearchPrediction">The prediction buffer of the motion search.</param>
        /// <param name="motionVectorCosts">The motion vector rates of the frame precision.</param>
        /// <param name="firstIntermediate">The compound intermediate of the first reference.</param>
        /// <param name="secondIntermediate">The compound intermediate of the second reference.</param>
        /// <param name="compoundMask">The blend mask of a masked compound prediction.</param>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="displacementVectors">The displacement vectors of the picture, one per allocation entry.</param>
        /// <param name="sourceLuma">The samples of the complete source luma plane, read once per frame pass.</param>
        /// <param name="sourceBlue">The samples of the complete source blue-difference plane, read once per frame pass.</param>
        /// <param name="sourceRed">The samples of the complete source red-difference plane, read once per frame pass.</param>
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
        /// <param name="referenceVectors">The retained single-reference motion context.</param>
        /// <param name="motionRate">The selected new-motion coding cost.</param>
        /// <param name="prediction">The selected luma prediction buffer.</param>
        /// <param name="alternatePrediction">The alternate luma prediction buffer.</param>
        /// <param name="variance">The candidate luma variance.</param>
        /// <param name="squaredError">The candidate luma squared error.</param>
        /// <param name="chromaDistortion">The modeled chroma distortion when evaluated.</param>
        /// <param name="initialSkip">Whether residual skipping required no coded alternative.</param>
        /// <param name="modelEarlyTermination">Whether the large-block model ended the residual estimate.</param>
        /// <returns>The residual cost, or an invalid result when prediction error rejects the candidate.</returns>
        private Av1RateDistortionStatistics EvaluateEstimatedInterCandidate(
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            Span<int> searchDequantizedCoefficients,
            Span<int> transformWorkspace,
            Span<short> blockResidual,
            Span<int> estimationRowCoefficients,
            in Av1EncoderInterPredictionWorkspace<TSample> interWorkspace,
            Span<TSample> motionSearchPrediction,
            in Av1MotionVectorCosts motionVectorCosts,
            Span<ushort> firstIntermediate,
            Span<ushort> secondIntermediate,
            Span<byte> compoundMask,
            ReadOnlySpan<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            ReadOnlySpan<Av1EncoderDisplacementVector> displacementVectors,
            ReadOnlySpan<TSample> sourceLuma,
            ReadOnlySpan<TSample> sourceBlue,
            ReadOnlySpan<TSample> sourceRed,
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
            Av1ReferenceMotionVectors referenceVectors,
            out int motionRate,
            ref Span<TSample> prediction,
            ref Span<TSample> alternatePrediction,
            out uint variance,
            out uint squaredError,
            out long chromaDistortion,
            out bool initialSkip,
            out bool modelEarlyTermination)
        {
            initialSkip = false;
            modelEarlyTermination = false;
            Av1BlockSize blockSize = modeInfo.BlockSize;
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
                Av1PlaneRegion<TSample> sourcePlane = this.source.GetPlane(Av1Plane.Y);
                Av1PlaneRegion<TSample> referencePlane = primaryReference.GetPlane(Av1Plane.Y);
                ReadOnlySpan<TSample> referenceSamples = referencePlane.Samples;
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
                int skipContext = Av1TileWriter.GetSkipContext(modeInfoGrid, modeInfoAllocation, macroBlock);

                // The estimated motion entry measures only the prediction error and the vector rate. The coefficient edges are not used
                // here. The residual model runs after the final motion and filter selection. The estimated path gives no frame, so this
                // search does not write the frame.
                Av1ModeCosts modeCosts = tables.ModeCosts;
                Av1MotionSearchBase.SingleReferenceSearch<TSample, TOperator> motionSearch = new(
                    Av1TransformBlockEncoder.GetPlaneSpan(sourceLuma, sourcePlane, blockOrigin),
                    sourcePlane.Stride,
                    referenceSamples,
                    referencePlane.Stride,
                    referenceOrigin,
                    blockSize,
                    blockOrigin,
                    bounds,
                    new Size(header.FrameSize.FrameWidth, header.FrameSize.FrameHeight),
                    this.blockWorkspace,
                    motionSearchPrediction,
                    default,
                    blockResidual,
                    interWorkspace.FilterRows,
                    interWorkspace.TransformCoefficients,
                    writer,
                    [],
                    [],
                    this.bitDepth,
                    this.blockQIndex,
                    this.quantization.DeltaQDc[0],
                    this.blockWorkspace.EncoderOptions.Sharpness,
                    this.BlockLossless,
                    this.rateMultiplier,
                    0,
                    Av1SymbolEncoder.GetSkipCost(modeCosts, false, skipContext),
                    Av1SymbolEncoder.GetSkipCost(modeCosts, true, skipContext),
                    motionVectorCosts);

                Av1MotionSearchBase.FractionalResult result;
                if (this.UsesProjectionMotionSearch(modeInfo.ReferenceFrame))
                {
                    // In constant-bitrate usage, the motion of GOLDEN and ALTREF comes from row and column projections. The search stops
                    // below 16x16, or when the projection error is more than the predicted-vector error of LAST. Otherwise it refines the
                    // estimate to fractional precision.
                    if (blockSize < Av1BlockSize.Block16x16)
                    {
                        return Av1RateDistortionStatistics.Invalid;
                    }

                    Point integerVector = Point.Empty;
                    uint projectionSad;
                    ReadOnlySpan<TSample> sourceBlock = Av1TransformBlockEncoder.GetPlaneSpan(sourceLuma, sourcePlane, blockOrigin);
                    if (this.bitDepth == Av1BitDepth.EightBit)
                    {
                        projectionSad = Av1MotionSearchBase.SearchProjection(
                            MemoryMarshal.Cast<TSample, byte>(sourceBlock),
                            sourcePlane.Stride,
                            MemoryMarshal.Cast<TSample, byte>(referenceSamples),
                            referencePlane.Stride,
                            referenceOrigin,
                            new Rectangle(blockOrigin, new Size(blockSize.GetWidth(), blockSize.GetHeight())),
                            new Size(sourcePlane.Width, sourcePlane.Height),
                            this.picture.Parent.EncoderBorder,
                            blockSize.GetWidth() >> 1,
                            blockSize.GetHeight() >> 1,
                            false,
                            default(Av1MotionVector).GetFullPixelSearchBounds(bounds),
                            interWorkspace.FilterRows,
                            out Av1MotionVector projected,
                            out _);

                        integerVector = new Point(projected.Column >> 3, projected.Row >> 3);
                    }
                    else
                    {
                        // Above 8 bits, the projection search keeps zero motion.
                        projectionSad = (uint)TOperator.SumAbsoluteDifferences(
                            sourceBlock,
                            sourcePlane.Stride,
                            referenceSamples[referenceOrigin..],
                            referencePlane.Stride,
                            blockSize.GetWidth(),
                            blockSize.GetHeight(),
                            1);
                    }

                    if (projectionSad > (long)searchState.PredictorSad[(int)Av1ReferenceFrameType.Last] ||
                        !motionSearch.RefineProjection(
                            this.picture.Parent.MotionSearchSettings,
                            integerVector,
                            referenceVectors.GetNewReference(0),
                            header.AllowHighPrecisionMotionVector,
                            this.picture.Parent.AverageFrameLowMotion,
                            this.sourceSadLevel,
                            (uint)this.interSourceVariance,
                            out motionRate,
                            out result))
                    {
                        return Av1RateDistortionStatistics.Invalid;
                    }
                }
                else if (!motionSearch.SearchEstimated(
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
                    out result))
                {
                    return Av1RateDistortionStatistics.Invalid;
                }

                vector = result.Vector;
                searchState.MotionVectors[(int)modeInfo.Mode][(int)modeInfo.ReferenceFrame] = vector;
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

            bool searchMotionMode = UsesMotionModeSearch(modeInfo, vector, searchFilters);
            if (searchFilters && ((vector.Row | vector.Column) & 7) != 0)
            {
                this.SelectEstimatedInterFilter(
                    in tables,
                    in interWorkspace,
                    firstIntermediate,
                    secondIntermediate,
                    compoundMask,
                    modeInfoGrid,
                    modeInfoAllocation,
                    displacementVectors,
                    sourceLuma,
                    sourceBlue,
                    sourceRed,
                    macroBlock,
                    blockOrigin,
                    blockSize,
                    ref modeInfo,
                    vector,
                    secondaryVector,
                    primaryReference,
                    secondaryReference,
                    evaluateBlue,
                    evaluateRed,
                    ref prediction,
                    ref alternatePrediction,
                    out variance,
                    out squaredError,
                    out modelEarlyTermination);
            }
            else
            {
                if (searchMotionMode)
                {
                    // The motion-mode search evaluates simple translation with the regular filter. The search prunes warped motion, so
                    // simple translation is its only candidate.
                    modeInfo.HorizontalInterpolationFilter = Av1InterpolationFilter.Regular;
                    modeInfo.VerticalInterpolationFilter = Av1InterpolationFilter.Regular;
                }

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
                    interWorkspace.Residual,
                    interWorkspace.FilterRows,
                    firstIntermediate,
                    secondIntermediate,
                    compoundMask,
                    modeInfoGrid,
                    modeInfoAllocation,
                    displacementVectors,
                    sourceLuma,
                    sourceBlue,
                    sourceRed);

                Av1PlaneRegion<TSample> sourcePlane = this.source.GetPlane(Av1Plane.Y);
                int width = blockSize.GetWidth();
                int height = blockSize.GetHeight();
                TOperator.GetMoments(
                    Av1TransformBlockEncoder.GetPlaneSpan(sourceLuma, sourcePlane, blockOrigin),
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

                // The search drops a compound candidate of the large-block model when its variance is more than the variance of either
                // single-reference mode. The large-block skip test does not run for that candidate.
                if (modeInfo.SecondaryReferenceFrame > Av1ReferenceFrameType.Intra &&
                    this.picture.Parent.SpeedSettings.PrunesCompoundBySingleVariance &&
                    this.UsesLargeBlockModel(blockSize) &&
                    variance > searchState.GetCompoundVarianceThreshold(
                        modeInfo.Mode, modeInfo.ReferenceFrame, modeInfo.SecondaryReferenceFrame))
                {
                    this.RecordLumaPrediction(in interWorkspace, prediction);
                    return Av1RateDistortionStatistics.Invalid;
                }

                modeInfo.TransformSize = this.SelectEstimatedInterTransformSize(
                    blockSize, variance, squaredError, evaluateBlue, evaluateRed, this.IsCyclicRefreshBoosted, out bool forceSkip);

                if (this.UsesLargeBlockModel(blockSize))
                {
                    // A forced skip ends the candidate. Otherwise the unit test decides. On the default path, the unit test does not run when
                    // the squared error already rejects the candidate.
                    modelEarlyTermination = forceSkip;
                    if (!forceSkip &&
                        (searchMotionMode || !searchState.SkipByPredictionError(this.picture.Parent.EncodingSpeed, blockSize, squaredError)))
                    {
                        modelEarlyTermination = this.TestLargeBlockSkip(
                            in interWorkspace,
                            firstIntermediate,
                            secondIntermediate,
                            compoundMask,
                            modeInfoGrid,
                            modeInfoAllocation,
                            displacementVectors,
                            sourceLuma,
                            sourceBlue,
                            sourceRed,
                            blockOrigin,
                            modeInfo,
                            vector,
                            secondaryVector,
                            primaryReference,
                            secondaryReference,
                            prediction,
                            modeInfo.TransformSize,
                            variance,
                            squaredError,
                            sum,
                            evaluateBlue,
                            evaluateRed);
                    }
                }
            }

            this.RecordLumaPrediction(in interWorkspace, prediction);
            if (modeInfo.SecondaryReferenceFrame == Av1ReferenceFrameType.None)
            {
                // The motion-mode model does not report the variance, so the mode keeps the unset value.
                uint reportedVariance = searchMotionMode ? uint.MaxValue : variance;
                int modeIndex = (int)modeInfo.Mode - (int)Av1PredictionMode.SingleInterModeStart;
                searchState.Variances[modeIndex][(int)modeInfo.ReferenceFrame] = reportedVariance;
                if (vector.IsZero)
                {
                    int globalIndex = (int)Av1PredictionMode.GlobalMotionVector - (int)Av1PredictionMode.SingleInterModeStart;
                    searchState.Variances[globalIndex][(int)modeInfo.ReferenceFrame] = reportedVariance;
                }
            }

            if (searchState.SkipByPredictionError(
                this.picture.Parent.EncodingSpeed, blockSize, squaredError))
            {
                return Av1RateDistortionStatistics.Invalid;
            }

            // The prediction error test runs before the chroma work. Only color-sensitive planes need predictors for this estimate. The
            // final reconstruction still writes every coded plane.
            if (!modelEarlyTermination)
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
                        planeIndex == 1 ? interWorkspace.BluePrediction : interWorkspace.RedPrediction,
                        interWorkspace.Residual,
                        interWorkspace.FilterRows,
                        firstIntermediate,
                        secondIntermediate,
                        compoundMask,
                        modeInfoGrid,
                        modeInfoAllocation,
                        displacementVectors,
                        sourceLuma,
                        sourceBlue,
                        sourceRed);
                }
            }

            return this.EstimateInterResidual(
                writer,
                in tables,
                searchDequantizedCoefficients,
                transformWorkspace,
                estimationRowCoefficients,
                in interWorkspace,
                modeInfoGrid,
                modeInfoAllocation,
                sourceLuma,
                sourceBlue,
                sourceRed,
                macroBlock,
                blockOrigin,
                blockSize,
                prediction,
                interWorkspace.BluePrediction,
                interWorkspace.RedPrediction,
                evaluateBlue,
                evaluateRed,
                modeInfo.TransformSize,
                modelEarlyTermination,
                squaredError,
                out chromaDistortion,
                out initialSkip);
        }

        /// <summary>
        /// Returns whether the motion-mode search predicts a single-reference candidate, and not the default path. This applies to a
        /// NEWMV candidate whose vector does not reach the filter search. Warped motion is enabled by default, so this test does not read it.
        /// </summary>
        /// <param name="modeInfo">The candidate syntax.</param>
        /// <param name="vector">The candidate motion vector.</param>
        /// <param name="searchFilters">Whether the filter search is enabled for the candidate.</param>
        /// <returns><see langword="true"/> when the motion-mode search predicts the candidate.</returns>
        private static bool UsesMotionModeSearch(Av1EncoderBlockModeInfo modeInfo, Av1MotionVector vector, bool searchFilters)
            => modeInfo.Mode == Av1PredictionMode.NewMotionVector &&
                modeInfo.SecondaryReferenceFrame == Av1ReferenceFrameType.None &&
                !(searchFilters && ((vector.Row | vector.Column) & 7) != 0);

        /// <summary>
        /// Selects a luma interpolation filter using prediction-error estimates.
        /// </summary>
        /// <param name="tables">The rate tables of the tile.</param>
        /// <param name="interWorkspace">The inter prediction buffers of the block.</param>
        /// <param name="firstIntermediate">The compound intermediate of the first reference.</param>
        /// <param name="secondIntermediate">The compound intermediate of the second reference.</param>
        /// <param name="compoundMask">The blend mask of a masked compound prediction.</param>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="displacementVectors">The displacement vectors of the picture, one per allocation entry.</param>
        /// <param name="sourceLuma">The samples of the complete source luma plane, read once per frame pass.</param>
        /// <param name="sourceBlue">The samples of the complete source blue-difference plane, read once per frame pass.</param>
        /// <param name="sourceRed">The samples of the complete source red-difference plane, read once per frame pass.</param>
        /// <param name="macroBlock">The current coding-block neighbors.</param>
        /// <param name="blockOrigin">The luma coding-block origin.</param>
        /// <param name="blockSize">The coding-block geometry.</param>
        /// <param name="modeInfo">The candidate syntax and selected transform size.</param>
        /// <param name="vector">The primary displacement.</param>
        /// <param name="secondaryVector">The secondary displacement.</param>
        /// <param name="primaryReferencePlanes">The primary bordered reference planes.</param>
        /// <param name="secondaryReferencePlanes">The secondary bordered reference planes.</param>
        /// <param name="evaluateBlue">Whether blue-difference residuals participate in mode selection.</param>
        /// <param name="evaluateRed">Whether red-difference residuals participate in mode selection.</param>
        /// <param name="prediction">The winning packed predictor on return.</param>
        /// <param name="alternatePrediction">The alternate packed predictor storage on return.</param>
        /// <param name="variance">The winning normalized prediction variance.</param>
        /// <param name="squaredError">The winning normalized squared error.</param>
        /// <param name="earlyTermination">Whether the winning filter's residual estimate ended early.</param>
        private void SelectEstimatedInterFilter(
            in Av1CoefficientTables tables,
            in Av1EncoderInterPredictionWorkspace<TSample> interWorkspace,
            Span<ushort> firstIntermediate,
            Span<ushort> secondIntermediate,
            Span<byte> compoundMask,
            ReadOnlySpan<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            ReadOnlySpan<Av1EncoderDisplacementVector> displacementVectors,
            ReadOnlySpan<TSample> sourceLuma,
            ReadOnlySpan<TSample> sourceBlue,
            ReadOnlySpan<TSample> sourceRed,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            ref Av1EncoderBlockModeInfo modeInfo,
            Av1MotionVector vector,
            Av1MotionVector secondaryVector,
            Av1EncoderFrame<TSample>.PlanarView primaryReferencePlanes,
            Av1EncoderFrame<TSample>.PlanarView secondaryReferencePlanes,
            bool evaluateBlue,
            bool evaluateRed,
            ref Span<TSample> prediction,
            ref Span<TSample> alternatePrediction,
            out uint variance,
            out uint squaredError,
            out bool earlyTermination)
        {
            Av1PlaneRegion<TSample> primaryReference = primaryReferencePlanes.GetPlane(Av1Plane.Y);
            Av1PlaneRegion<TSample> secondaryReference = secondaryReferencePlanes.GetPlane(Av1Plane.Y);
            bool largeBlockModel = this.UsesLargeBlockModel(blockSize);
            earlyTermination = false;
            Av1PlaneRegion<TSample> sourcePlane = this.source.GetPlane(Av1Plane.Y);
            int width = blockSize.GetWidth();
            int height = blockSize.GetHeight();
            int sampleCount = width * height;
            int sampleCountLog2 = BitOperations.Log2((uint)sampleCount);
            int normalizationShift = this.bitDepth.GetBitCount() - 8;
            int acQuantizer = Av1QuantizationLookup.GetAcQuant(
                this.blockQIndex, this.quantization.DeltaQAc[0], this.bitDepth);

            int context = Av1SymbolContextHelper.GetSwitchableInterpolationContext(modeInfo, modeInfoGrid, modeInfoAllocation, macroBlock, 0);
            long bestCost = long.MaxValue;
            Av1InterpolationFilter bestFilter = Av1InterpolationFilter.Regular;
            Av1TransformSize bestTransform = Av1TransformSize.Size4x4;
            variance = 0;
            squaredError = 0;

            // The filter search tries the first two filters, regular and smooth, in both directions. Without dual filtering, only the
            // equal pairs remain.
            ReadOnlySpan<Av1InterpolationFilter> filters =
            [
                Av1InterpolationFilter.Regular,
                Av1InterpolationFilter.Smooth
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
                    alternatePrediction,
                    interWorkspace.Residual,
                    interWorkspace.FilterRows,
                    firstIntermediate,
                    secondIntermediate,
                    compoundMask,
                    modeInfoGrid,
                    modeInfoAllocation,
                    displacementVectors,
                    sourceLuma,
                    sourceBlue,
                    sourceRed);

                TOperator.GetMoments(
                    Av1TransformBlockEncoder.GetPlaneSpan(sourceLuma, sourcePlane, blockOrigin),
                    sourcePlane.Stride,
                    alternatePrediction,
                    width,
                    width,
                    height,
                    out int sum,
                    out long error);

                sum = (sum + ((1 << normalizationShift) >> 1)) >> normalizationShift;
                error = (error + ((1L << (2 * normalizationShift)) >> 1)) >> (2 * normalizationShift);
                uint currentVariance = (uint)Math.Max(0, error - (((long)sum * sum) >> sampleCountLog2));
                Av1TransformSize transformSize = this.SelectEstimatedInterTransformSize(
                    blockSize, currentVariance, (uint)error, evaluateBlue, evaluateRed, this.IsCyclicRefreshBoosted, out bool forceSkip);

                // The filter search models a large block with the large-block skip test. A passing unit test ends the residual estimate of
                // the filter, as a forced skip does.
                bool filterEarlyTermination = false;
                if (largeBlockModel)
                {
                    Av1EncoderBlockModeInfo filterMode = modeInfo;
                    filterMode.HorizontalInterpolationFilter = filter;
                    filterMode.VerticalInterpolationFilter = filter;
                    filterEarlyTermination = forceSkip || this.TestLargeBlockSkip(
                        in interWorkspace,
                        firstIntermediate,
                        secondIntermediate,
                        compoundMask,
                        modeInfoGrid,
                        modeInfoAllocation,
                        displacementVectors,
                        sourceLuma,
                        sourceBlue,
                        sourceRed,
                        blockOrigin,
                        filterMode,
                        vector,
                        secondaryVector,
                        primaryReferencePlanes,
                        secondaryReferencePlanes,
                        alternatePrediction,
                        transformSize,
                        currentVariance,
                        (uint)error,
                        sum,
                        evaluateBlue,
                        evaluateRed);
                }

                int rate;
                long distortion;
                if (forceSkip || filterEarlyTermination)
                {
                    rate = 0;
                    distortion = error << 4;
                }
                else
                {
                    Av1RateDistortion.ModelPredictionError(
                        blockSize, error, sampleCount, acQuantizer, this.bitDepth, this.rateMultiplier, out rate, out distortion);
                }

                rate += Av1SymbolEncoder.GetSwitchableInterpolationFilterCost(tables.ModeCosts, filter, context);
                long cost = Av1RateDistortion.GetCost(this.rateMultiplier, rate, distortion);
                if (cost < bestCost)
                {
                    bestCost = cost;
                    bestFilter = filter;
                    bestTransform = transformSize;
                    variance = currentVariance;
                    squaredError = (uint)error;
                    earlyTermination = filterEarlyTermination;

                    // Exchange borrowed views, not their contents. The winning predictor remains intact
                    // while the next filter writes into the other worker buffer.
                    Span<TSample> previous = prediction;
                    prediction = alternatePrediction;
                    alternatePrediction = previous;
                }
            }

            modeInfo.HorizontalInterpolationFilter = bestFilter;
            modeInfo.VerticalInterpolationFilter = bestFilter;
            modeInfo.TransformSize = bestTransform;
        }

        /// <summary>
        /// Records which inter-prediction buffer holds the luma prediction built last. With prediction reuse off,
        /// the reference builds every candidate in the frame buffer, so that buffer holds the last prediction after
        /// the search.
        /// </summary>
        /// <param name="interWorkspace">The inter prediction buffers of the block.</param>
        /// <param name="prediction">The buffer that received the prediction.</param>
        private void RecordLumaPrediction(in Av1EncoderInterPredictionWorkspace<TSample> interWorkspace, ReadOnlySpan<TSample> prediction)
        {
            ref TSample start = ref MemoryMarshal.GetReference(prediction);
            this.lastLumaPredictionBuffer =
                Unsafe.AreSame(ref start, ref MemoryMarshal.GetReference(interWorkspace.SelectedLumaReconstruction)) ? 0
                : Unsafe.AreSame(ref start, ref MemoryMarshal.GetReference(interWorkspace.LumaPrediction)) ? 1
                : Unsafe.AreSame(ref start, ref MemoryMarshal.GetReference(interWorkspace.LumaCandidateReconstruction)) ? 2
                : -1;
        }

        /// <summary>
        /// Gets the luma prediction built last by the estimated search.
        /// </summary>
        /// <param name="interWorkspace">The inter prediction buffers of the block.</param>
        /// <returns>The packed prediction.</returns>
        private readonly ReadOnlySpan<TSample> GetLastLumaPrediction(in Av1EncoderInterPredictionWorkspace<TSample> interWorkspace)
        {
            return this.lastLumaPredictionBuffer switch
            {
                0 => interWorkspace.SelectedLumaReconstruction,
                1 => interWorkspace.LumaPrediction,
                _ => interWorkspace.LumaCandidateReconstruction
            };
        }

        /// <summary>
        /// Gets the first vector of the above or left block, or <see langword="null"/> when that block is unavailable or intra coded.
        /// </summary>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="displacementVectors">The displacement vectors of the picture, one per allocation entry.</param>
        /// <param name="macroBlock">The coding-block neighbors.</param>
        /// <param name="origin">The luma block origin.</param>
        /// <param name="above"><see langword="true"/> for the above block, or <see langword="false"/> for the left block.</param>
        /// <returns>The first vector of the neighbor, or <see langword="null"/>.</returns>
        private Av1MotionVector? GetNeighborMotionVector(
            ReadOnlySpan<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            ReadOnlySpan<Av1EncoderDisplacementVector> displacementVectors,
            Av1MacroBlockD macroBlock,
            Point origin,
            bool above)
        {
            // The neighbor is read only when it is available.
            int neighborOffset = above ? -macroBlock.ModeInfoStride : -1;
            if (!(above ? macroBlock.IsUpAvailable : macroBlock.IsLeftAvailable) ||
                macroBlock.GetRelativeModeInfo(modeInfoGrid, modeInfoAllocation, neighborOffset).Block.ReferenceFrame <= Av1ReferenceFrameType.Intra)
            {
                return null;
            }

            Point position = new(
                (origin.X >> Av1Constants.ModeInfoSizeLog2) - (above ? 0 : 1),
                (origin.Y >> Av1Constants.ModeInfoSizeLog2) - (above ? 1 : 0));

            return this.picture.GetDisplacementVector(modeInfoGrid, displacementVectors, position);
        }

        /// <summary>
        /// Returns whether the NEWMV motion of a reference comes from the projection search. This is true for a reference after LAST in
        /// real-time constant-bitrate coding, while LAST is available.
        /// </summary>
        /// <param name="reference">The candidate reference.</param>
        /// <returns><see langword="true"/> when the projection search estimates the motion.</returns>
        private bool UsesProjectionMotionSearch(Av1ReferenceFrameType reference)
            => reference > Av1ReferenceFrameType.Last &&
                this.picture.Parent.SpeedSettings.IsRealtime &&
                this.picture.Parent.EncoderOptions.UsesConstantBitRate &&
                (this.picture.Parent.AvailableReferenceMask & (1 << (int)Av1ReferenceFrameType.Last)) != 0;

        /// <summary>
        /// Returns whether a candidate uses the large-block model, which can end the mode search early. The model applies only to
        /// real-time constant-bitrate coding at 8 bits.
        /// </summary>
        /// <param name="blockSize">The coding-block geometry.</param>
        /// <returns>
        /// <see langword="true"/> for 32x32 and larger blocks outside a boosted cyclic refresh segment, at a nonzero
        /// quantizer and 8 bits.
        /// </returns>
        private bool UsesLargeBlockModel(Av1BlockSize blockSize)
            => this.picture.Parent.SpeedSettings.IsRealtime &&
                this.picture.Parent.EncoderOptions.UsesConstantBitRate &&
                blockSize >= Av1BlockSize.Block32x32 &&
                !this.IsCyclicRefreshBoosted &&
                this.picture.Parent.FrameHeader.QuantizationParameters.BaseQIndex != 0 &&
                this.bitDepth == Av1BitDepth.EightBit;

        /// <summary>
        /// Tests whether every luma test unit and the color-sensitive chroma planes of a candidate quantize to zero. If they do, the
        /// residual estimate of the candidate ends.
        /// </summary>
        /// <param name="interWorkspace">The inter prediction buffers of the block.</param>
        /// <param name="firstIntermediate">The compound intermediate of the first reference.</param>
        /// <param name="secondIntermediate">The compound intermediate of the second reference.</param>
        /// <param name="compoundMask">The blend mask of a masked compound prediction.</param>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="displacementVectors">The displacement vectors of the picture, one per allocation entry.</param>
        /// <param name="sourceLuma">The samples of the complete source luma plane, read once per frame pass.</param>
        /// <param name="sourceBlue">The samples of the complete source blue-difference plane, read once per frame pass.</param>
        /// <param name="sourceRed">The samples of the complete source red-difference plane, read once per frame pass.</param>
        /// <param name="blockOrigin">The luma coding-block origin.</param>
        /// <param name="modeInfo">The candidate syntax, whose filters build the chroma predictions.</param>
        /// <param name="vector">The primary displacement.</param>
        /// <param name="secondaryVector">The secondary displacement.</param>
        /// <param name="primaryReference">The primary reference planes.</param>
        /// <param name="secondaryReference">The secondary reference planes.</param>
        /// <param name="lumaPrediction">The packed luma predictor.</param>
        /// <param name="transformSize">The estimation transform, which sets the 8x8 or 16x16 test units.</param>
        /// <param name="variance">The luma prediction variance.</param>
        /// <param name="squaredError">The luma prediction squared error.</param>
        /// <param name="sum">The luma prediction error sum.</param>
        /// <param name="evaluateBlue">Whether blue-difference prediction is color sensitive.</param>
        /// <param name="evaluateRed">Whether red-difference prediction is color sensitive.</param>
        /// <returns><see langword="true"/> when the candidate can skip its residual.</returns>
        private bool TestLargeBlockSkip(
            in Av1EncoderInterPredictionWorkspace<TSample> interWorkspace,
            Span<ushort> firstIntermediate,
            Span<ushort> secondIntermediate,
            Span<byte> compoundMask,
            ReadOnlySpan<int> modeInfoGrid,
            ReadOnlySpan<Av1MacroBlockModeInfo> modeInfoAllocation,
            ReadOnlySpan<Av1EncoderDisplacementVector> displacementVectors,
            ReadOnlySpan<TSample> sourceLuma,
            ReadOnlySpan<TSample> sourceBlue,
            ReadOnlySpan<TSample> sourceRed,
            Point blockOrigin,
            Av1EncoderBlockModeInfo modeInfo,
            Av1MotionVector vector,
            Av1MotionVector secondaryVector,
            Av1EncoderFrame<TSample>.PlanarView primaryReference,
            Av1EncoderFrame<TSample>.PlanarView secondaryReference,
            ReadOnlySpan<TSample> lumaPrediction,
            Av1TransformSize transformSize,
            uint variance,
            uint squaredError,
            int sum,
            bool evaluateBlue,
            bool evaluateRed)
        {
            Av1BlockSize blockSize = modeInfo.BlockSize;
            int width = blockSize.GetWidth();
            int height = blockSize.GetHeight();
            int sizeLog2 = BitOperations.Log2((uint)width) + BitOperations.Log2((uint)height) - 4;
            long dcQuant = Av1QuantizationLookup.GetDcQuant(this.blockQIndex, this.quantization.DeltaQDc[0], this.bitDepth);
            long acQuant = Av1QuantizationLookup.GetAcQuant(this.blockQIndex, this.quantization.DeltaQAc[0], this.bitDepth);
            long dcThreshold = (dcQuant * dcQuant) >> 6;
            long acThreshold = (acQuant * acQuant) >> 6;

            // At speed 8 and faster, a block with a small mean error raises the AC threshold. The increase is larger at 640x480 and below.
            int normalizedSum = Math.Abs(sum) >> sizeLog2;
            ObuFrameSize frameSize = this.picture.Parent.FrameHeader.FrameSize;
            if (this.picture.Parent.EncodingSpeed >= HeifEncodingSpeed.Level8 && normalizedSum < 5)
            {
                acThreshold *= frameSize.FrameWidth <= 640 && frameSize.FrameHeight <= 480 ? 4 : 2;
            }

            if (frameSize.FrameWidth * frameSize.FrameHeight >= 1280 * 720 &&
                this.sourceSadLevel > Av1SourceSadLevel.Low &&
                (squaredError >> sizeLog2) > 1000)
            {
                dcThreshold >>= 4;
                acThreshold >>= 4;
            }

            // The units are the 16x16 transforms of a block larger than 32x32 or of a 16x16 estimation transform,
            // and 8x8 units otherwise.
            Av1PlaneRegion<TSample> sourcePlane = this.source.GetPlane(Av1Plane.Y);
            ReadOnlySpan<TSample> sourceSpan = Av1TransformBlockEncoder.GetPlaneSpan(sourceLuma, sourcePlane, blockOrigin);
            int unit = transformSize == Av1TransformSize.Size16x16 ? 16 : 8;
            int unitLog2 = unit == 16 ? 8 : 6;
            for (int y = 0; y < height; y += unit)
            {
                for (int x = 0; x < width; x += unit)
                {
                    TOperator.GetMoments(
                        sourceSpan[((y * sourcePlane.Stride) + x)..],
                        sourcePlane.Stride,
                        lumaPrediction[((y * width) + x)..],
                        width,
                        unit,
                        unit,
                        out int unitSum,
                        out long unitError);

                    long unitVariance = unitError - (((long)unitSum * unitSum) >> unitLog2);
                    if (!(unitVariance < acThreshold || variance == 0))
                    {
                        return false;
                    }

                    if (!(unitError - unitVariance < dcThreshold || squaredError == variance))
                    {
                        return false;
                    }
                }
            }

            // The transform skipping test in the chroma planes that are color sensitive.
            int subX = this.source.ChromaSubsamplingX;
            int subY = this.source.ChromaSubsamplingY;
            Av1BlockSize chromaSize = blockSize.GetSubsampled(subX != 0, subY != 0);
            int chromaWidth = chromaSize.GetWidth();
            int chromaHeight = chromaSize.GetHeight();
            int chromaLog2 = BitOperations.Log2((uint)(chromaWidth * chromaHeight));
            Point chromaOrigin = Av1TileWriter.GetChromaBlockOrigin(blockOrigin, subX, subY);
            for (int planeIndex = 1; planeIndex <= 2; planeIndex++)
            {
                if (!(planeIndex == 1 ? evaluateBlue : evaluateRed))
                {
                    continue;
                }

                Av1Plane plane = (Av1Plane)planeIndex;
                long chromaDc = Av1QuantizationLookup.GetDcQuant(this.blockQIndex, this.quantization.DeltaQDc[planeIndex], this.bitDepth);
                long chromaAc = Av1QuantizationLookup.GetAcQuant(this.blockQIndex, this.quantization.DeltaQAc[planeIndex], this.bitDepth);
                long chromaDcThreshold = (chromaDc * chromaDc) >> 3;
                long chromaAcThreshold = (chromaAc * chromaAc) >> 3;
                Span<TSample> chromaPrediction = planeIndex == 1 ? interWorkspace.BluePrediction : interWorkspace.RedPrediction;
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
                    subX,
                    subY,
                    blockSize,
                    chromaPrediction,
                    interWorkspace.Residual,
                    interWorkspace.FilterRows,
                    firstIntermediate,
                    secondIntermediate,
                    compoundMask,
                    modeInfoGrid,
                    modeInfoAllocation,
                    displacementVectors,
                    sourceLuma,
                    sourceBlue,
                    sourceRed);

                Av1PlaneRegion<TSample> chromaSource = this.source.GetPlane(plane);
                TOperator.GetMoments(
                    Av1TransformBlockEncoder.GetPlaneSpan(SelectPlane(plane, sourceLuma, sourceBlue, sourceRed), chromaSource, chromaOrigin),
                    chromaSource.Stride,
                    chromaPrediction,
                    chromaWidth,
                    chromaWidth,
                    chromaHeight,
                    out int chromaSum,
                    out long chromaError);

                long chromaVariance = chromaError - (((long)chromaSum * chromaSum) >> chromaLog2);
                if (!((chromaVariance < chromaAcThreshold || chromaVariance == 0) &&
                    (chromaError - chromaVariance < chromaDcThreshold || chromaError == chromaVariance)))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Models the chroma residuals selected by the block's color-sensitivity decision.
        /// </summary>
        /// <param name="sourceLuma">The samples of the complete source luma plane, read once per frame pass.</param>
        /// <param name="sourceBlue">The samples of the complete source blue-difference plane, read once per frame pass.</param>
        /// <param name="sourceRed">The samples of the complete source red-difference plane, read once per frame pass.</param>
        /// <param name="blockOrigin">The luma coding-block origin.</param>
        /// <param name="blockSize">The luma coding-block geometry.</param>
        /// <param name="bluePrediction">The tightly packed blue-difference predictor.</param>
        /// <param name="redPrediction">The tightly packed red-difference predictor.</param>
        /// <param name="evaluateBlue">Whether blue-difference distortion participates in the mode decision.</param>
        /// <param name="evaluateRed">Whether red-difference distortion participates in the mode decision.</param>
        /// <returns>The combined chroma estimate, including the prediction-only alternative.</returns>
        private Av1RateDistortionStatistics EstimateInterChroma(
            ReadOnlySpan<TSample> sourceLuma,
            ReadOnlySpan<TSample> sourceBlue,
            ReadOnlySpan<TSample> sourceRed,
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

                Av1PlaneRegion<TSample> sourcePlane = this.source.GetPlane((Av1Plane)planeIndex);
                ReadOnlySpan<TSample> prediction = planeIndex == 1 ? bluePrediction : redPrediction;
                TOperator.GetMoments(
                    Av1TransformBlockEncoder.GetPlaneSpan(SelectPlane((Av1Plane)planeIndex, sourceLuma, sourceBlue, sourceRed), sourcePlane, origin),
                    sourcePlane.Stride,
                    prediction,
                    width,
                    width,
                    height,
                    out int sum,
                    out long squaredError);

                // The moments are normalized separately before the mean is removed. The coded block includes its extended edge samples.
                // A model of only the visible rectangle gives other results.
                sum = (sum + ((1 << normalizationShift) >> 1)) >> normalizationShift;
                squaredError = (squaredError + ((1L << (2 * normalizationShift)) >> 1)) >> (2 * normalizationShift);
                long variance = Math.Max(0, squaredError - (((long)sum * sum) >> sampleCountLog2));
                predictionDistortion += squaredError << 4;
                int dcStep = Av1QuantizationLookup.GetDcQuant(
                    this.blockQIndex, this.quantization.DeltaQDc[planeIndex], this.bitDepth) >> 3;

                int acStep = Av1QuantizationLookup.GetAcQuant(
                    this.blockQIndex, this.quantization.DeltaQAc[planeIndex], this.bitDepth) >> 3;

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
        /// <param name="tables">The rate tables of the tile.</param>
        /// <param name="searchDequantizedCoefficients">The dequantized coefficients of one estimation transform.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
        /// <param name="estimationRowCoefficients">The coefficients of one row of estimation transforms.</param>
        /// <param name="interWorkspace">The inter prediction buffers of the block.</param>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="sourceLuma">The samples of the complete source luma plane, read once per frame pass.</param>
        /// <param name="sourceBlue">The samples of the complete source blue-difference plane, read once per frame pass.</param>
        /// <param name="sourceRed">The samples of the complete source red-difference plane, read once per frame pass.</param>
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
            in Av1CoefficientTables tables,
            Span<int> searchDequantizedCoefficients,
            Span<int> transformWorkspace,
            Span<int> estimationRowCoefficients,
            in Av1EncoderInterPredictionWorkspace<TSample> interWorkspace,
            ReadOnlySpan<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            ReadOnlySpan<TSample> sourceLuma,
            ReadOnlySpan<TSample> sourceBlue,
            ReadOnlySpan<TSample> sourceRed,
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
            Av1ModeCosts modeCosts = tables.ModeCosts;
            int skipContext = Av1TileWriter.GetSkipContext(modeInfoGrid, modeInfoAllocation, macroBlock);
            int skipRate = Av1SymbolEncoder.GetSkipCost(modeCosts, true, skipContext);
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
            Span<short> residual = interWorkspace.Residual;
            Av1PlaneRegion<TSample> lumaSource = this.source.GetPlane(Av1Plane.Y);
            TOperator.SubtractPrediction(
                Av1TransformBlockEncoder.GetPlaneSpan(sourceLuma, lumaSource, blockOrigin),
                lumaSource.Stride,
                lumaPrediction,
                width,
                residual,
                width,
                height);

            // Estimation visits transforms whose origins remain inside the coded frame. It still
            // transforms the full padded block at each edge, matching the predictor's sample extent.
            Size extent = new(
                width + (Math.Min(0, macroBlock.ToRightEdge) >> 3),
                height + (Math.Min(0, macroBlock.ToBottomEdge) >> 3));

            Av1IntraModeEstimator.Estimate(
                this.blockWorkspace,
                estimationRowCoefficients,
                searchDequantizedCoefficients,
                transformWorkspace,
                residual,
                width,
                extent,
                transformSize,
                this.blockQIndex,
                this.quantization.DeltaQDc[0],
                this.quantization.DeltaQAc[0],
                this.bitDepth,
                out int rate,
                out long distortion,
                out bool skip);

            int codedRate = rate + Av1SymbolEncoder.GetSkipCost(modeCosts, false, skipContext);
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
                    sourceLuma, sourceBlue, sourceRed, blockOrigin, blockSize, bluePrediction, redPrediction, evaluateBlue, evaluateRed);

                chromaDistortion = chroma.Distortion;
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
                    multiplier -= (uint)this.blockQIndex >> 6;
                    int step = Av1QuantizationLookup.GetAcQuant(
                        this.blockQIndex, this.quantization.DeltaQAc[0], this.bitDepth) >> (this.bitDepth.GetBitCount() - 5);

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

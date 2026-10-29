// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Tpl;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.ChromaFromLuma;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <content>
/// Binds shared search control to the existing typed sample workers and workspace views.
/// </content>
internal static partial class Av1IntraSuperblockEncoder
{
    /// <summary>
    /// Runs search decisions against one typed worker and the existing frame and block storage.
    /// </summary>
    /// <remarks>
    /// Only the active worker span contains an element. That element is the caller's worker, not a copy.
    /// Both constructors reference its search state. Recursive trials therefore see exactly the state
    /// left by their preceding trials. The spans and the state reference live only for this call.
    /// Sample spans in this controller count bytes. Only typed workers interpret their component values.
    /// Casting a view changes neither its storage nor its ownership. Worker loops retain native sample strides.
    /// </remarks>
    internal readonly ref partial struct SearchControl
    {
        private readonly Span<ModeDecision<byte, ByteOperator>> byteWorker;
        private readonly Span<ModeDecision<ushort, UInt16Operator>> uint16Worker;
        private readonly ref SearchState state;

        /// <summary>Initializes a new instance of the <see cref="SearchControl"/> struct for eight-bit samples.</summary>
        /// <param name="worker">The active typed worker.</param>
        public SearchControl(ref ModeDecision<byte, ByteOperator> worker)
        {
            this.byteWorker = MemoryMarshal.CreateSpan(ref worker, 1);
            this.uint16Worker = default;
            this.state = ref worker.State;
        }

        /// <summary>Initializes a new instance of the <see cref="SearchControl"/> struct for high-bit-depth samples.</summary>
        /// <param name="worker">The active typed worker.</param>
        public SearchControl(ref ModeDecision<ushort, UInt16Operator> worker)
        {
            this.byteWorker = default;
            this.uint16Worker = MemoryMarshal.CreateSpan(ref worker, 1);
            this.state = ref worker.State;
        }

        /// <summary>Gets a value indicating whether cyclic refresh boosts the active segment.</summary>
        private bool IsCyclicRefreshBoosted =>
            this.state.Picture.Parent.CyclicRefresh is not null &&
            this.state.Picture.Parent.FrameHeader.SegmentationParameters.Enabled &&
            Av1CyclicRefresh.IsBoosted(this.state.BlockSegmentId);

        /// <summary>Gets a value indicating whether the inter search uses prediction estimates.</summary>
        private bool UsesEstimatedInterSearch => this.state.UsesEstimatedInterSearch;

        /// <summary>Gets a value indicating whether high-bit-depth sharpness is active.</summary>
        private bool UsesHighBitDepthSharpness => this.state.UsesHighBitDepthSharpness;

        /// <summary>Gets a value indicating whether the search prices smoothing loss.</summary>
        private bool ChargesSmoothing => this.state.ChargesSmoothing;

        /// <summary>Gets a value indicating whether the inter search prices high-bit-depth texture loss.</summary>
        private bool ChargesHighBitDepthTextureLoss => this.state.ChargesHighBitDepthTextureLoss;

        /// <summary>Gets a value indicating whether the motion search uses the fine interval.</summary>
        private bool UsesFineSearchInterval => this.state.UsesFineSearchInterval;

        /// <summary>Gets the segment quantizer factor used to prune modes.</summary>
        private int ModeThresholdQuantizerFactor => this.state.ModeThresholdQuantizerFactor;

        /// <summary>Gets the configured cost multiplier used to prune skippable modes.</summary>
        private int ModeThresholdSkipMultiplier => this.state.ModeThresholdSkipMultiplier;

        /// <summary>Gets a value indicating whether the active segment uses lossless transforms.</summary>
        private bool BlockLossless => this.state.BlockLossless;

        /// <summary>Gets a byte view of one typed reconstruction buffer without changing its layout.</summary>
        /// <param name="workspace">The original aligned mode storage.</param>
        /// <param name="index">The existing candidate slot.</param>
        /// <returns>The same candidate storage, measured in bytes.</returns>
        private Span<byte> GetCandidateReconstruction(in Av1EncoderModeDecisionWorkspace<byte> workspace, int index)
            => !this.byteWorker.IsEmpty
                ? MemoryMarshal.AsBytes(workspace.GetCandidateReconstruction(index))
                : MemoryMarshal.AsBytes(workspace.Cast<ushort>().GetCandidateReconstruction(index));

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.SavePartitionTrialContexts"/>
        private void SavePartitionTrialContexts(
            Span<int> workspaceStorage,
            in Av1NeighborEdges<Av1PartitionContext> partitionEdges,
            in Av1NeighborEdges<byte> transformEdges,
            in Av1NeighborEdges<Av1EncoderPaletteInfo> paletteEdges,
            in Av1NeighborEdges<byte> lumaCoefficientEdges,
            in Av1NeighborEdges<byte> blueCoefficientEdges,
            in Av1NeighborEdges<byte> redCoefficientEdges,
            Point blockOrigin,
            Av1BlockSize blockSize)
        {
            if (!this.byteWorker.IsEmpty)
            {
                this.byteWorker[0].SavePartitionTrialContexts(
                    workspaceStorage,
                    in partitionEdges,
                    in transformEdges,
                    in paletteEdges,
                    in lumaCoefficientEdges,
                    in blueCoefficientEdges,
                    in redCoefficientEdges,
                    blockOrigin,
                    blockSize);

                return;
            }

            this.uint16Worker[0].SavePartitionTrialContexts(
                workspaceStorage,
                in partitionEdges,
                in transformEdges,
                in paletteEdges,
                in lumaCoefficientEdges,
                in blueCoefficientEdges,
                in redCoefficientEdges,
                blockOrigin,
                blockSize);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.IsPartitionCandidateAllowed"/>
        private bool IsPartitionCandidateAllowed(
            Point blockOrigin,
            Av1BlockSize blockSize,
            Av1PartitionType partitionType)
        {
            if (!this.byteWorker.IsEmpty)
            {
                return this.byteWorker[0].IsPartitionCandidateAllowed(blockOrigin, blockSize, partitionType);
            }

            return this.uint16Worker[0].IsPartitionCandidateAllowed(blockOrigin, blockSize, partitionType);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.PruneIntraPartitions"/>
        private bool PruneIntraPartitions(
            Span<int> workspaceStorage,
            ReadOnlySpan<byte> sourceLuma,
            ReadOnlySpan<byte> sourceBlue,
            ReadOnlySpan<byte> sourceRed,
            Point blockOrigin,
            Av1BlockSize blockSize,
            int level,
            ref bool allowNone,
            ref bool allowSplit,
            ref bool allowRectangles)
        {
            if (!this.byteWorker.IsEmpty)
            {
                return this.byteWorker[0].PruneIntraPartitions(
                    workspaceStorage,
                    MemoryMarshal.Cast<byte, byte>(sourceLuma),
                    MemoryMarshal.Cast<byte, byte>(sourceBlue),
                    MemoryMarshal.Cast<byte, byte>(sourceRed),
                    blockOrigin,
                    blockSize,
                    level,
                    ref allowNone,
                    ref allowSplit,
                    ref allowRectangles);
            }

            return this.uint16Worker[0].PruneIntraPartitions(
                workspaceStorage,
                MemoryMarshal.Cast<byte, ushort>(sourceLuma),
                MemoryMarshal.Cast<byte, ushort>(sourceBlue),
                MemoryMarshal.Cast<byte, ushort>(sourceRed),
                blockOrigin,
                blockSize,
                level,
                ref allowNone,
                ref allowSplit,
                ref allowRectangles);
        }

        /// <summary>
        /// Returns the per-sample variance of the source luma block around the mid-gray level.
        /// </summary>
        /// <param name="sourceLuma">The samples of the complete source luma plane, read once per frame pass.</param>
        /// <param name="sourceBlue">The samples of the complete source blue-difference plane, read once per frame pass.</param>
        /// <param name="sourceRed">The samples of the complete source red-difference plane, read once per frame pass.</param>
        /// <param name="midpoint">Storage for one row of mid-gray samples, at least one block row long.</param>
        /// <param name="blockOrigin">The block origin in luma samples.</param>
        /// <param name="blockSize">The block size.</param>
        /// <returns>The rounded per-sample variance.</returns>
        private int GetSourceVariance(
            ReadOnlySpan<byte> sourceLuma,
            ReadOnlySpan<byte> sourceBlue,
            ReadOnlySpan<byte> sourceRed,
            Span<byte> midpoint,
            Point blockOrigin,
            Av1BlockSize blockSize)
        {
            if (!this.byteWorker.IsEmpty)
            {
                return this.byteWorker[0].GetSourceVariance(
                    MemoryMarshal.Cast<byte, byte>(sourceLuma),
                    MemoryMarshal.Cast<byte, byte>(sourceBlue),
                    MemoryMarshal.Cast<byte, byte>(sourceRed),
                    MemoryMarshal.Cast<byte, byte>(midpoint),
                    blockOrigin,
                    blockSize);
            }

            return this.uint16Worker[0].GetSourceVariance(
                MemoryMarshal.Cast<byte, ushort>(sourceLuma),
                MemoryMarshal.Cast<byte, ushort>(sourceBlue),
                MemoryMarshal.Cast<byte, ushort>(sourceRed),
                MemoryMarshal.Cast<byte, ushort>(midpoint),
                blockOrigin,
                blockSize);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.SearchSimpleMotion"/>
        private Av1MotionVector SearchSimpleMotion(
            Span<int> workspaceStorage,
            ReadOnlySpan<byte> sourceLuma,
            ReadOnlySpan<byte> sourceBlue,
            ReadOnlySpan<byte> sourceRed,
            Span<byte> reconstructionLuma,
            Span<byte> reconstructionBlue,
            Span<byte> reconstructionRed,
            Span<byte> motionSearchPrediction,
            Span<short> filterRows,
            in Av1MotionVectorCosts motionVectorCosts,
            Point blockOrigin,
            Av1BlockSize blockSize,
            Av1MotionVector start,
            bool useSubpixel,
            out int squaredError,
            out int variance)
        {
            if (!this.byteWorker.IsEmpty)
            {
                return this.byteWorker[0].SearchSimpleMotion(
                    workspaceStorage,
                    MemoryMarshal.Cast<byte, byte>(sourceLuma),
                    MemoryMarshal.Cast<byte, byte>(sourceBlue),
                    MemoryMarshal.Cast<byte, byte>(sourceRed),
                    MemoryMarshal.Cast<byte, byte>(reconstructionLuma),
                    MemoryMarshal.Cast<byte, byte>(reconstructionBlue),
                    MemoryMarshal.Cast<byte, byte>(reconstructionRed),
                    MemoryMarshal.Cast<byte, byte>(motionSearchPrediction),
                    filterRows,
                    in motionVectorCosts,
                    blockOrigin,
                    blockSize,
                    start,
                    useSubpixel,
                    out squaredError,
                    out variance);
            }

            return this.uint16Worker[0].SearchSimpleMotion(
                workspaceStorage,
                MemoryMarshal.Cast<byte, ushort>(sourceLuma),
                MemoryMarshal.Cast<byte, ushort>(sourceBlue),
                MemoryMarshal.Cast<byte, ushort>(sourceRed),
                MemoryMarshal.Cast<byte, ushort>(reconstructionLuma),
                MemoryMarshal.Cast<byte, ushort>(reconstructionBlue),
                MemoryMarshal.Cast<byte, ushort>(reconstructionRed),
                MemoryMarshal.Cast<byte, ushort>(motionSearchPrediction),
                filterRows,
                in motionVectorCosts,
                blockOrigin,
                blockSize,
                start,
                useSubpixel,
                out squaredError,
                out variance);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.UpdatePickedReferenceFrames"/>
        private void UpdatePickedReferenceFrames(Point blockOrigin, Av1BlockSize blockSize, Av1EncoderBlockModeInfo mode)
        {
            if (!this.byteWorker.IsEmpty)
            {
                this.byteWorker[0].UpdatePickedReferenceFrames(blockOrigin, blockSize, mode);
                return;
            }

            this.uint16Worker[0].UpdatePickedReferenceFrames(blockOrigin, blockSize, mode);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.ResetPartitionTrial"/>
        private void ResetPartitionTrial(
            Span<int> workspaceStorage,
            in Av1NeighborEdges<Av1PartitionContext> partitionEdges,
            in Av1NeighborEdges<byte> transformEdges,
            in Av1NeighborEdges<Av1EncoderPaletteInfo> paletteEdges,
            in Av1NeighborEdges<byte> lumaCoefficientEdges,
            in Av1NeighborEdges<byte> blueCoefficientEdges,
            in Av1NeighborEdges<byte> redCoefficientEdges,
            Point blockOrigin,
            Av1BlockSize blockSize,
            int savedLumaArea,
            int savedChromaArea)
        {
            if (!this.byteWorker.IsEmpty)
            {
                this.byteWorker[0].ResetPartitionTrial(
                    workspaceStorage,
                    in partitionEdges,
                    in transformEdges,
                    in paletteEdges,
                    in lumaCoefficientEdges,
                    in blueCoefficientEdges,
                    in redCoefficientEdges,
                    blockOrigin,
                    blockSize,
                    savedLumaArea,
                    savedChromaArea);

                return;
            }

            this.uint16Worker[0].ResetPartitionTrial(
                workspaceStorage,
                in partitionEdges,
                in transformEdges,
                in paletteEdges,
                in lumaCoefficientEdges,
                in blueCoefficientEdges,
                in redCoefficientEdges,
                blockOrigin,
                blockSize,
                savedLumaArea,
                savedChromaArea);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.GetCodedBlockSegment"/>
        private int GetCodedBlockSegment(
            ReadOnlySpan<byte> encoderSegmentMap,
            ReadOnlySpan<byte> previousSegmentMap,
            Point blockOrigin,
            Av1BlockSize blockSize,
            int segmentId)
        {
            if (!this.byteWorker.IsEmpty)
            {
                return this.byteWorker[0].GetCodedBlockSegment(encoderSegmentMap, previousSegmentMap, blockOrigin, blockSize, segmentId);
            }

            return this.uint16Worker[0].GetCodedBlockSegment(encoderSegmentMap, previousSegmentMap, blockOrigin, blockSize, segmentId);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.UpdatesCyclicRefreshSegment"/>
        private bool UpdatesCyclicRefreshSegment(Av1RateDistortionStatistics statistics, out bool countBlocks)
        {
            if (!this.byteWorker.IsEmpty)
            {
                return this.byteWorker[0].UpdatesCyclicRefreshSegment(statistics, out countBlocks);
            }

            return this.uint16Worker[0].UpdatesCyclicRefreshSegment(statistics, out countBlocks);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.UpdateCyclicRefreshSegment"/>
        private void UpdateCyclicRefreshSegment(
            Span<byte> encoderSegmentMap,
            Span<byte> searchSegmentMap,
            Point blockOrigin,
            ref Av1EncoderBlockModeInfo modeInfo,
            ref Av1EncoderBlockStruct block,
            Av1MotionVector vector,
            Av1RateDistortionStatistics statistics,
            bool countBlocks)
        {
            if (!this.byteWorker.IsEmpty)
            {
                this.byteWorker[0].UpdateCyclicRefreshSegment(
                    encoderSegmentMap,
                    searchSegmentMap,
                    blockOrigin,
                    ref modeInfo,
                    ref block,
                    vector,
                    statistics,
                    countBlocks);

                return;
            }

            this.uint16Worker[0].UpdateCyclicRefreshSegment(
                encoderSegmentMap,
                searchSegmentMap,
                blockOrigin,
                ref modeInfo,
                ref block,
                vector,
                statistics,
                countBlocks);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.AddSegmentPredictionCosts"/>
        private void AddSegmentPredictionCosts(
            in Av1CoefficientTables tables,
            ReadOnlySpan<byte> searchSegmentMap,
            ReadOnlySpan<byte> previousSegmentMap,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            int segmentId)
        {
            if (!this.byteWorker.IsEmpty)
            {
                this.byteWorker[0].AddSegmentPredictionCosts(in tables, searchSegmentMap, previousSegmentMap, macroBlock, blockOrigin, blockSize, segmentId);
                return;
            }

            this.uint16Worker[0].AddSegmentPredictionCosts(in tables, searchSegmentMap, previousSegmentMap, macroBlock, blockOrigin, blockSize, segmentId);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.ReconstructSelectedInterBlock"/>
        private bool ReconstructSelectedInterBlock(
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> transformWorkspace,
            in Av1EncoderInterPredictionWorkspace<byte> interWorkspace,
            Span<byte> transformPrediction,
            Span<byte> interIntraAbove,
            Span<byte> interIntraLeft,
            Span<ushort> firstIntermediate,
            Span<ushort> secondIntermediate,
            Span<byte> compoundMask,
            in Av1NeighborEdges<byte> lumaCoefficientEdges,
            in Av1NeighborEdges<byte> blueCoefficientEdges,
            in Av1NeighborEdges<byte> redCoefficientEdges,
            ReadOnlySpan<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            ReadOnlySpan<Av1EncoderDisplacementVector> displacementVectors,
            ReadOnlySpan<byte> encoderSegmentMap,
            ReadOnlySpan<byte> previousSegmentMap,
            Span<int> superblockCoefficients,
            ReadOnlySpan<byte> sourceLuma,
            ReadOnlySpan<byte> sourceBlue,
            ReadOnlySpan<byte> sourceRed,
            Span<byte> reconstructionLuma,
            Span<byte> reconstructionBlue,
            Span<byte> reconstructionRed,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1MacroBlockModeInfo modeInfo,
            Av1EncoderBlockStruct block,
            Av1MotionVector vector,
            Av1MotionVector secondaryVector,
            ReadOnlySpan<Av1EncoderTransformBlockState> states)
        {
            if (!this.byteWorker.IsEmpty)
            {
                return this.byteWorker[0].ReconstructSelectedInterBlock(
                    writer,
                    in tables,
                    transformCoefficients,
                    dequantizedCoefficients,
                    transformWorkspace,
                    interWorkspace.Cast<byte>(),
                    MemoryMarshal.Cast<byte, byte>(transformPrediction),
                    MemoryMarshal.Cast<byte, byte>(interIntraAbove),
                    MemoryMarshal.Cast<byte, byte>(interIntraLeft),
                    firstIntermediate,
                    secondIntermediate,
                    compoundMask,
                    in lumaCoefficientEdges,
                    in blueCoefficientEdges,
                    in redCoefficientEdges,
                    modeInfoGrid,
                    modeInfoAllocation,
                    displacementVectors,
                    encoderSegmentMap,
                    previousSegmentMap,
                    superblockCoefficients,
                    MemoryMarshal.Cast<byte, byte>(sourceLuma),
                    MemoryMarshal.Cast<byte, byte>(sourceBlue),
                    MemoryMarshal.Cast<byte, byte>(sourceRed),
                    MemoryMarshal.Cast<byte, byte>(reconstructionLuma),
                    MemoryMarshal.Cast<byte, byte>(reconstructionBlue),
                    MemoryMarshal.Cast<byte, byte>(reconstructionRed),
                    macroBlock,
                    blockOrigin,
                    modeInfo,
                    block,
                    vector,
                    secondaryVector,
                    states);
            }

            return this.uint16Worker[0].ReconstructSelectedInterBlock(
                writer,
                in tables,
                transformCoefficients,
                dequantizedCoefficients,
                transformWorkspace,
                interWorkspace.Cast<ushort>(),
                MemoryMarshal.Cast<byte, ushort>(transformPrediction),
                MemoryMarshal.Cast<byte, ushort>(interIntraAbove),
                MemoryMarshal.Cast<byte, ushort>(interIntraLeft),
                firstIntermediate,
                secondIntermediate,
                compoundMask,
                in lumaCoefficientEdges,
                in blueCoefficientEdges,
                in redCoefficientEdges,
                modeInfoGrid,
                modeInfoAllocation,
                displacementVectors,
                encoderSegmentMap,
                previousSegmentMap,
                superblockCoefficients,
                MemoryMarshal.Cast<byte, ushort>(sourceLuma),
                MemoryMarshal.Cast<byte, ushort>(sourceBlue),
                MemoryMarshal.Cast<byte, ushort>(sourceRed),
                MemoryMarshal.Cast<byte, ushort>(reconstructionLuma),
                MemoryMarshal.Cast<byte, ushort>(reconstructionBlue),
                MemoryMarshal.Cast<byte, ushort>(reconstructionRed),
                macroBlock,
                blockOrigin,
                modeInfo,
                block,
                vector,
                secondaryVector,
                states);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.ReconstructSelectedIntraBlock"/>
        private void ReconstructSelectedIntraBlock(
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            in Av1EncoderModeDecisionWorkspace<byte> modeWorkspace,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> transformWorkspace,
            in Av1NeighborEdges<byte> lumaCoefficientEdges,
            in Av1NeighborEdges<byte> blueCoefficientEdges,
            in Av1NeighborEdges<byte> redCoefficientEdges,
            ReadOnlySpan<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            ReadOnlySpan<byte> encoderSegmentMap,
            ReadOnlySpan<byte> previousSegmentMap,
            Span<int> superblockCoefficients,
            ReadOnlySpan<byte> sourceLuma,
            ReadOnlySpan<byte> sourceBlue,
            ReadOnlySpan<byte> sourceRed,
            Span<byte> reconstructionLuma,
            Span<byte> reconstructionBlue,
            Span<byte> reconstructionRed,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1EncoderPartitionTree.ModeContext context)
        {
            if (!this.byteWorker.IsEmpty)
            {
                this.byteWorker[0].ReconstructSelectedIntraBlock(
                    writer,
                    in tables,
                    modeWorkspace.Cast<byte>(),
                    transformCoefficients,
                    dequantizedCoefficients,
                    transformWorkspace,
                    in lumaCoefficientEdges,
                    in blueCoefficientEdges,
                    in redCoefficientEdges,
                    modeInfoGrid,
                    modeInfoAllocation,
                    encoderSegmentMap,
                    previousSegmentMap,
                    superblockCoefficients,
                    MemoryMarshal.Cast<byte, byte>(sourceLuma),
                    MemoryMarshal.Cast<byte, byte>(sourceBlue),
                    MemoryMarshal.Cast<byte, byte>(sourceRed),
                    MemoryMarshal.Cast<byte, byte>(reconstructionLuma),
                    MemoryMarshal.Cast<byte, byte>(reconstructionBlue),
                    MemoryMarshal.Cast<byte, byte>(reconstructionRed),
                    macroBlock,
                    blockOrigin,
                    context);

                return;
            }

            this.uint16Worker[0].ReconstructSelectedIntraBlock(
                writer,
                in tables,
                modeWorkspace.Cast<ushort>(),
                transformCoefficients,
                dequantizedCoefficients,
                transformWorkspace,
                in lumaCoefficientEdges,
                in blueCoefficientEdges,
                in redCoefficientEdges,
                modeInfoGrid,
                modeInfoAllocation,
                encoderSegmentMap,
                previousSegmentMap,
                superblockCoefficients,
                MemoryMarshal.Cast<byte, ushort>(sourceLuma),
                MemoryMarshal.Cast<byte, ushort>(sourceBlue),
                MemoryMarshal.Cast<byte, ushort>(sourceRed),
                MemoryMarshal.Cast<byte, ushort>(reconstructionLuma),
                MemoryMarshal.Cast<byte, ushort>(reconstructionBlue),
                MemoryMarshal.Cast<byte, ushort>(reconstructionRed),
                macroBlock,
                blockOrigin,
                context);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.CountNoiseStillBlock"/>
        private void CountNoiseStillBlock(Point blockOrigin, Av1EncoderBlockModeInfo modeInfo, Av1MotionVector vector)
        {
            if (!this.byteWorker.IsEmpty)
            {
                this.byteWorker[0].CountNoiseStillBlock(blockOrigin, modeInfo, vector);
                return;
            }

            this.uint16Worker[0].CountNoiseStillBlock(blockOrigin, modeInfo, vector);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.ResetCyclicRefreshSkip"/>
        private void ResetCyclicRefreshSkip(
            Span<byte> encoderSegmentMap,
            Span<byte> searchSegmentMap,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ref Av1EncoderBlockModeInfo modeInfo,
            ref Av1EncoderBlockStruct block,
            bool codedSkip,
            bool countBlocks)
        {
            if (!this.byteWorker.IsEmpty)
            {
                this.byteWorker[0].ResetCyclicRefreshSkip(
                    encoderSegmentMap,
                    searchSegmentMap,
                    macroBlock,
                    blockOrigin,
                    ref modeInfo,
                    ref block,
                    codedSkip,
                    countBlocks);

                return;
            }

            this.uint16Worker[0].ResetCyclicRefreshSkip(
                encoderSegmentMap,
                searchSegmentMap,
                macroBlock,
                blockOrigin,
                ref modeInfo,
                ref block,
                codedSkip,
                countBlocks);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.SelectLumaMode"/>
        private Av1PredictionMode SelectLumaMode(
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            in Av1EncoderModeDecisionWorkspace<byte> modeWorkspace,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> transformWorkspace,
            ReadOnlySpan<int> transformTypeProbabilities,
            in Av1NeighborEdges<byte> transformEdges,
            in Av1NeighborEdges<Av1EncoderPaletteInfo> paletteEdges,
            in Av1NeighborEdges<byte> lumaCoefficientEdges,
            in Av1NeighborEdges<byte> blueCoefficientEdges,
            in Av1NeighborEdges<byte> redCoefficientEdges,
            ReadOnlySpan<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            ReadOnlySpan<byte> encoderSegmentMap,
            ReadOnlySpan<byte> previousSegmentMap,
            Span<int> superblockCoefficients,
            Span<int> workspaceStorage,
            ReadOnlySpan<byte> sourceLuma,
            ReadOnlySpan<byte> sourceBlue,
            ReadOnlySpan<byte> sourceRed,
            Span<byte> reconstructionLuma,
            Span<byte> reconstructionBlue,
            Span<byte> reconstructionRed,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            Span<Av1EncoderTransformBlockState> retainedStates,
            long interCostLimit,
            ref Av1EncoderPaletteInfo paletteInfo,
            out int selectedAngleDelta,
            out Av1FilterIntraMode selectedFilterIntraMode,
            out Av1TransformSize selectedTransformSize,
            out Av1RateDistortionStatistics selectedStatistics)
        {
            if (!this.byteWorker.IsEmpty)
            {
                return this.byteWorker[0].SelectLumaMode(
                    writer,
                    in tables,
                    modeWorkspace.Cast<byte>(),
                    transformCoefficients,
                    dequantizedCoefficients,
                    transformWorkspace,
                    transformTypeProbabilities,
                    in transformEdges,
                    in paletteEdges,
                    in lumaCoefficientEdges,
                    in blueCoefficientEdges,
                    in redCoefficientEdges,
                    modeInfoGrid,
                    modeInfoAllocation,
                    encoderSegmentMap,
                    previousSegmentMap,
                    superblockCoefficients,
                    workspaceStorage,
                    MemoryMarshal.Cast<byte, byte>(sourceLuma),
                    MemoryMarshal.Cast<byte, byte>(sourceBlue),
                    MemoryMarshal.Cast<byte, byte>(sourceRed),
                    MemoryMarshal.Cast<byte, byte>(reconstructionLuma),
                    MemoryMarshal.Cast<byte, byte>(reconstructionBlue),
                    MemoryMarshal.Cast<byte, byte>(reconstructionRed),
                    macroBlock,
                    blockOrigin,
                    blockSize,
                    retainedStates,
                    interCostLimit,
                    ref paletteInfo,
                    out selectedAngleDelta,
                    out selectedFilterIntraMode,
                    out selectedTransformSize,
                    out selectedStatistics);
            }

            return this.uint16Worker[0].SelectLumaMode(
                writer,
                in tables,
                modeWorkspace.Cast<ushort>(),
                transformCoefficients,
                dequantizedCoefficients,
                transformWorkspace,
                transformTypeProbabilities,
                in transformEdges,
                in paletteEdges,
                in lumaCoefficientEdges,
                in blueCoefficientEdges,
                in redCoefficientEdges,
                modeInfoGrid,
                modeInfoAllocation,
                encoderSegmentMap,
                previousSegmentMap,
                superblockCoefficients,
                workspaceStorage,
                MemoryMarshal.Cast<byte, ushort>(sourceLuma),
                MemoryMarshal.Cast<byte, ushort>(sourceBlue),
                MemoryMarshal.Cast<byte, ushort>(sourceRed),
                MemoryMarshal.Cast<byte, ushort>(reconstructionLuma),
                MemoryMarshal.Cast<byte, ushort>(reconstructionBlue),
                MemoryMarshal.Cast<byte, ushort>(reconstructionRed),
                macroBlock,
                blockOrigin,
                blockSize,
                retainedStates,
                interCostLimit,
                ref paletteInfo,
                out selectedAngleDelta,
                out selectedFilterIntraMode,
                out selectedTransformSize,
                out selectedStatistics);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.SelectChromaMode"/>
        private Av1ChromaPredictionMode SelectChromaMode(
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            in Av1EncoderModeDecisionWorkspace<byte> modeWorkspace,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> searchDequantizedCoefficients,
            Span<int> transformWorkspace,
            ReadOnlySpan<int> transformTypeProbabilities,
            Span<short> blockResidual,
            Span<int> searchCoefficients,
            Span<int> searchReconstructions,
            in Av1NeighborEdges<Av1EncoderPaletteInfo> paletteEdges,
            in Av1NeighborEdges<byte> blueCoefficientEdges,
            in Av1NeighborEdges<byte> redCoefficientEdges,
            ReadOnlySpan<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            ReadOnlySpan<byte> sourceLuma,
            ReadOnlySpan<byte> sourceBlue,
            ReadOnlySpan<byte> sourceRed,
            Span<byte> reconstructionLuma,
            Span<byte> reconstructionBlue,
            Span<byte> reconstructionRed,
            Av1MacroBlockD macroBlock,
            Av1MacroBlockModeInfo modeInfo,
            Point lumaOrigin,
            Point chromaOrigin,
            Av1BlockSize blockSize,
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
            if (!this.byteWorker.IsEmpty)
            {
                return this.byteWorker[0].SelectChromaMode(
                    writer,
                    in tables,
                    modeWorkspace.Cast<byte>(),
                    transformCoefficients,
                    dequantizedCoefficients,
                    searchDequantizedCoefficients,
                    transformWorkspace,
                    transformTypeProbabilities,
                    blockResidual,
                    searchCoefficients,
                    searchReconstructions,
                    in paletteEdges,
                    in blueCoefficientEdges,
                    in redCoefficientEdges,
                    modeInfoGrid,
                    modeInfoAllocation,
                    MemoryMarshal.Cast<byte, byte>(sourceLuma),
                    MemoryMarshal.Cast<byte, byte>(sourceBlue),
                    MemoryMarshal.Cast<byte, byte>(sourceRed),
                    MemoryMarshal.Cast<byte, byte>(reconstructionLuma),
                    MemoryMarshal.Cast<byte, byte>(reconstructionBlue),
                    MemoryMarshal.Cast<byte, byte>(reconstructionRed),
                    macroBlock,
                    modeInfo,
                    lumaOrigin,
                    chromaOrigin,
                    blockSize,
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

            return this.uint16Worker[0].SelectChromaMode(
                writer,
                in tables,
                modeWorkspace.Cast<ushort>(),
                transformCoefficients,
                dequantizedCoefficients,
                searchDequantizedCoefficients,
                transformWorkspace,
                transformTypeProbabilities,
                blockResidual,
                searchCoefficients,
                searchReconstructions,
                in paletteEdges,
                in blueCoefficientEdges,
                in redCoefficientEdges,
                modeInfoGrid,
                modeInfoAllocation,
                MemoryMarshal.Cast<byte, ushort>(sourceLuma),
                MemoryMarshal.Cast<byte, ushort>(sourceBlue),
                MemoryMarshal.Cast<byte, ushort>(sourceRed),
                MemoryMarshal.Cast<byte, ushort>(reconstructionLuma),
                MemoryMarshal.Cast<byte, ushort>(reconstructionBlue),
                MemoryMarshal.Cast<byte, ushort>(reconstructionRed),
                macroBlock,
                modeInfo,
                lumaOrigin,
                chromaOrigin,
                blockSize,
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

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.GetRegularBlockCost"/>
        private Av1RateDistortionStatistics GetRegularBlockCost(
            in Av1CoefficientTables tables,
            ReadOnlySpan<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            Av1MacroBlockD macroBlock,
            Av1RateDistortionStatistics modeStatistics)
        {
            if (!this.byteWorker.IsEmpty)
            {
                return this.byteWorker[0].GetRegularBlockCost(in tables, modeInfoGrid, modeInfoAllocation, macroBlock, modeStatistics);
            }

            return this.uint16Worker[0].GetRegularBlockCost(in tables, modeInfoGrid, modeInfoAllocation, macroBlock, modeStatistics);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.ChargeTextureLoss"/>
        private void ChargeTextureLoss(
            ReadOnlySpan<byte> sourceLuma,
            ReadOnlySpan<byte> sourceBlue,
            ReadOnlySpan<byte> sourceRed,
            ref Av1RateDistortionStatistics statistics,
            long currentCost,
            long offset,
            Point blockOrigin,
            Av1BlockSize blockSize,
            bool isInter,
            bool isCompound,
            bool smoothIntra)
        {
            if (!this.byteWorker.IsEmpty)
            {
                this.byteWorker[0].ChargeTextureLoss(
                    MemoryMarshal.Cast<byte, byte>(sourceLuma),
                    MemoryMarshal.Cast<byte, byte>(sourceBlue),
                    MemoryMarshal.Cast<byte, byte>(sourceRed),
                    ref statistics,
                    currentCost,
                    offset,
                    blockOrigin,
                    blockSize,
                    isInter,
                    isCompound,
                    smoothIntra);

                return;
            }

            this.uint16Worker[0].ChargeTextureLoss(
                MemoryMarshal.Cast<byte, ushort>(sourceLuma),
                MemoryMarshal.Cast<byte, ushort>(sourceBlue),
                MemoryMarshal.Cast<byte, ushort>(sourceRed),
                ref statistics,
                currentCost,
                offset,
                blockOrigin,
                blockSize,
                isInter,
                isCompound,
                smoothIntra);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.RetainModeContext"/>
        private void RetainModeContext(
            Span<int> superblockCoefficients,
            Av1EncoderPartitionTree.ModeContext context,
            int lumaArea,
            int chromaArea,
            int lumaCount,
            int chromaCount)
        {
            if (!this.byteWorker.IsEmpty)
            {
                this.byteWorker[0].RetainModeContext(superblockCoefficients, context, lumaArea, chromaArea, lumaCount, chromaCount);
                return;
            }

            this.uint16Worker[0].RetainModeContext(superblockCoefficients, context, lumaArea, chromaArea, lumaCount, chromaCount);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.RefineLumaMode"/>
        private Av1PredictionMode RefineLumaMode(
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            in Av1EncoderModeDecisionWorkspace<byte> modeWorkspace,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> transformWorkspace,
            ReadOnlySpan<int> transformTypeProbabilities,
            in Av1NeighborEdges<byte> transformEdges,
            in Av1NeighborEdges<Av1EncoderPaletteInfo> paletteEdges,
            in Av1NeighborEdges<byte> lumaCoefficientEdges,
            ReadOnlySpan<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            Span<int> workspaceStorage,
            ReadOnlySpan<byte> sourceLuma,
            ReadOnlySpan<byte> sourceBlue,
            ReadOnlySpan<byte> sourceRed,
            Span<byte> reconstructionLuma,
            Span<byte> reconstructionBlue,
            Span<byte> reconstructionRed,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            Span<Av1EncoderTransformBlockState> retainedStates,
            Av1PredictionMode mode,
            ref Av1EncoderPaletteInfo paletteInfo,
            ref int selectedAngleDelta,
            ref Av1FilterIntraMode selectedFilterIntraMode,
            ref Av1TransformSize selectedTransformSize,
            ref Av1RateDistortionStatistics selectedStatistics)
        {
            if (!this.byteWorker.IsEmpty)
            {
                return this.byteWorker[0].RefineLumaMode(
                    writer,
                    in tables,
                    modeWorkspace.Cast<byte>(),
                    transformCoefficients,
                    dequantizedCoefficients,
                    transformWorkspace,
                    transformTypeProbabilities,
                    in transformEdges,
                    in paletteEdges,
                    in lumaCoefficientEdges,
                    modeInfoGrid,
                    modeInfoAllocation,
                    workspaceStorage,
                    MemoryMarshal.Cast<byte, byte>(sourceLuma),
                    MemoryMarshal.Cast<byte, byte>(sourceBlue),
                    MemoryMarshal.Cast<byte, byte>(sourceRed),
                    MemoryMarshal.Cast<byte, byte>(reconstructionLuma),
                    MemoryMarshal.Cast<byte, byte>(reconstructionBlue),
                    MemoryMarshal.Cast<byte, byte>(reconstructionRed),
                    macroBlock,
                    blockOrigin,
                    blockSize,
                    retainedStates,
                    mode,
                    ref paletteInfo,
                    ref selectedAngleDelta,
                    ref selectedFilterIntraMode,
                    ref selectedTransformSize,
                    ref selectedStatistics);
            }

            return this.uint16Worker[0].RefineLumaMode(
                writer,
                in tables,
                modeWorkspace.Cast<ushort>(),
                transformCoefficients,
                dequantizedCoefficients,
                transformWorkspace,
                transformTypeProbabilities,
                in transformEdges,
                in paletteEdges,
                in lumaCoefficientEdges,
                modeInfoGrid,
                modeInfoAllocation,
                workspaceStorage,
                MemoryMarshal.Cast<byte, ushort>(sourceLuma),
                MemoryMarshal.Cast<byte, ushort>(sourceBlue),
                MemoryMarshal.Cast<byte, ushort>(sourceRed),
                MemoryMarshal.Cast<byte, ushort>(reconstructionLuma),
                MemoryMarshal.Cast<byte, ushort>(reconstructionBlue),
                MemoryMarshal.Cast<byte, ushort>(reconstructionRed),
                macroBlock,
                blockOrigin,
                blockSize,
                retainedStates,
                mode,
                ref paletteInfo,
                ref selectedAngleDelta,
                ref selectedFilterIntraMode,
                ref selectedTransformSize,
                ref selectedStatistics);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.ChargeRefinedIntraTextureLoss"/>
        private void ChargeRefinedIntraTextureLoss(
            ReadOnlySpan<byte> sourceLuma,
            ReadOnlySpan<byte> sourceBlue,
            ReadOnlySpan<byte> sourceRed,
            Span<byte> reconstructionLuma,
            Span<byte> reconstructionBlue,
            Span<byte> reconstructionRed,
            ref Av1RateDistortionStatistics statistics,
            Point blockOrigin,
            Av1BlockSize blockSize,
            Av1PredictionMode mode)
        {
            if (!this.byteWorker.IsEmpty)
            {
                this.byteWorker[0].ChargeRefinedIntraTextureLoss(
                    MemoryMarshal.Cast<byte, byte>(sourceLuma),
                    MemoryMarshal.Cast<byte, byte>(sourceBlue),
                    MemoryMarshal.Cast<byte, byte>(sourceRed),
                    MemoryMarshal.Cast<byte, byte>(reconstructionLuma),
                    MemoryMarshal.Cast<byte, byte>(reconstructionBlue),
                    MemoryMarshal.Cast<byte, byte>(reconstructionRed),
                    ref statistics,
                    blockOrigin,
                    blockSize,
                    mode);

                return;
            }

            this.uint16Worker[0].ChargeRefinedIntraTextureLoss(
                MemoryMarshal.Cast<byte, ushort>(sourceLuma),
                MemoryMarshal.Cast<byte, ushort>(sourceBlue),
                MemoryMarshal.Cast<byte, ushort>(sourceRed),
                MemoryMarshal.Cast<byte, ushort>(reconstructionLuma),
                MemoryMarshal.Cast<byte, ushort>(reconstructionBlue),
                MemoryMarshal.Cast<byte, ushort>(reconstructionRed),
                ref statistics,
                blockOrigin,
                blockSize,
                mode);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.HasSkipModeReferenceLists"/>
        private bool HasSkipModeReferenceLists(Span<int> workspaceStorage, Av1ReferenceFrameType first, Av1ReferenceFrameType second)
        {
            if (!this.byteWorker.IsEmpty)
            {
                return this.byteWorker[0].HasSkipModeReferenceLists(workspaceStorage, first, second);
            }

            return this.uint16Worker[0].HasSkipModeReferenceLists(workspaceStorage, first, second);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.IsScaledReference"/>
        private bool IsScaledReference(Av1ReferenceFrameType referenceFrame)
        {
            if (!this.byteWorker.IsEmpty)
            {
                return this.byteWorker[0].IsScaledReference(referenceFrame);
            }

            return this.uint16Worker[0].IsScaledReference(referenceFrame);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.GetInterFilterModelCost"/>
        private Av1RateDistortionStatistics GetInterFilterModelCost(
            in Av1EncoderInterPredictionWorkspace<byte> interWorkspace,
            Span<ushort> firstIntermediate,
            Span<ushort> secondIntermediate,
            Span<byte> compoundMask,
            ReadOnlySpan<int> modeInfoGrid,
            ReadOnlySpan<Av1MacroBlockModeInfo> modeInfoAllocation,
            ReadOnlySpan<Av1EncoderDisplacementVector> displacementVectors,
            ReadOnlySpan<byte> sourceLuma,
            ReadOnlySpan<byte> sourceBlue,
            ReadOnlySpan<byte> sourceRed,
            Av1MotionVector vector,
            Av1MotionVector secondaryVector,
            Av1EncoderBlockModeInfo modeInfo,
            Point blockOrigin,
            Av1BlockSize blockSize,
            bool modelChroma,
            Av1InterpolationFilter horizontalFilter,
            Av1InterpolationFilter verticalFilter,
            int filterRate,
            long bestCost,
            int comparisonScale,
            int skipPlanes,
            Span<byte> lumaPrediction,
            Span<byte> bluePrediction,
            Span<byte> redPrediction,
            Span<Av1RateDistortionStatistics> planeStatistics,
            ref long lumaSquaredError)
        {
            if (!this.byteWorker.IsEmpty)
            {
                return this.byteWorker[0].GetInterFilterModelCost(
                    interWorkspace.Cast<byte>(),
                    firstIntermediate,
                    secondIntermediate,
                    compoundMask,
                    modeInfoGrid,
                    modeInfoAllocation,
                    displacementVectors,
                    MemoryMarshal.Cast<byte, byte>(sourceLuma),
                    MemoryMarshal.Cast<byte, byte>(sourceBlue),
                    MemoryMarshal.Cast<byte, byte>(sourceRed),
                    vector,
                    secondaryVector,
                    modeInfo,
                    blockOrigin,
                    blockSize,
                    modelChroma,
                    horizontalFilter,
                    verticalFilter,
                    filterRate,
                    bestCost,
                    comparisonScale,
                    skipPlanes,
                    MemoryMarshal.Cast<byte, byte>(lumaPrediction),
                    MemoryMarshal.Cast<byte, byte>(bluePrediction),
                    MemoryMarshal.Cast<byte, byte>(redPrediction),
                    planeStatistics,
                    ref lumaSquaredError);
            }

            return this.uint16Worker[0].GetInterFilterModelCost(
                interWorkspace.Cast<ushort>(),
                firstIntermediate,
                secondIntermediate,
                compoundMask,
                modeInfoGrid,
                modeInfoAllocation,
                displacementVectors,
                MemoryMarshal.Cast<byte, ushort>(sourceLuma),
                MemoryMarshal.Cast<byte, ushort>(sourceBlue),
                MemoryMarshal.Cast<byte, ushort>(sourceRed),
                vector,
                secondaryVector,
                modeInfo,
                blockOrigin,
                blockSize,
                modelChroma,
                horizontalFilter,
                verticalFilter,
                filterRate,
                bestCost,
                comparisonScale,
                skipPlanes,
                MemoryMarshal.Cast<byte, ushort>(lumaPrediction),
                MemoryMarshal.Cast<byte, ushort>(bluePrediction),
                MemoryMarshal.Cast<byte, ushort>(redPrediction),
                planeStatistics,
                ref lumaSquaredError);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.WriteInterLumaDestination"/>
        private void WriteInterLumaDestination(Span<byte> lumaFrame, Point blockOrigin, Av1BlockSize blockSize, ReadOnlySpan<byte> prediction)
        {
            if (!this.byteWorker.IsEmpty)
            {
                this.byteWorker[0].WriteInterLumaDestination(
                    MemoryMarshal.Cast<byte, byte>(lumaFrame),
                    blockOrigin,
                    blockSize,
                    MemoryMarshal.Cast<byte, byte>(prediction));

                return;
            }

            this.uint16Worker[0].WriteInterLumaDestination(
                MemoryMarshal.Cast<byte, ushort>(lumaFrame),
                blockOrigin,
                blockSize,
                MemoryMarshal.Cast<byte, ushort>(prediction));
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.SetPredictionSse"/>
        private void SetPredictionSse(Av1ReferenceFrameType reference, long squaredError)
        {
            if (!this.byteWorker.IsEmpty)
            {
                this.byteWorker[0].SetPredictionSse(reference, squaredError);
                return;
            }

            this.uint16Worker[0].SetPredictionSse(reference, squaredError);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.PrunesInterModeByTpl"/>
        private bool PrunesInterModeByTpl(
            long bestCost,
            Av1ReferenceFrameType first,
            Av1ReferenceFrameType second,
            int referenceIndex,
            Av1PredictionMode mode)
        {
            if (!this.byteWorker.IsEmpty)
            {
                return this.byteWorker[0].PrunesInterModeByTpl(bestCost, first, second, referenceIndex, mode);
            }

            return this.uint16Worker[0].PrunesInterModeByTpl(bestCost, first, second, referenceIndex, mode);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.GetSimpleTranslationRate"/>
        private int GetSimpleTranslationRate(
            in Av1CoefficientTables tables,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            Av1ReferenceFrameType referenceFrame,
            Av1PredictionMode mode)
        {
            if (!this.byteWorker.IsEmpty)
            {
                return this.byteWorker[0].GetSimpleTranslationRate(in tables, macroBlock, blockOrigin, blockSize, referenceFrame, mode);
            }

            return this.uint16Worker[0].GetSimpleTranslationRate(in tables, macroBlock, blockOrigin, blockSize, referenceFrame, mode);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.PrepareInterIntraPrediction"/>
        private void PrepareInterIntraPrediction(
            Span<int> transformWorkspace,
            in Av1EncoderInterPredictionWorkspace<byte> interWorkspace,
            Span<byte> transformPrediction,
            Span<byte> interIntraAbove,
            Span<byte> interIntraLeft,
            Span<byte> compoundMask,
            ReadOnlySpan<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            ReadOnlySpan<byte> sourceLuma,
            ReadOnlySpan<byte> sourceBlue,
            ReadOnlySpan<byte> sourceRed,
            Span<byte> reconstructionLuma,
            Span<byte> reconstructionBlue,
            Span<byte> reconstructionRed,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            bool hasChroma,
            Av1PredictionMode mode,
            Av1ReferenceFrameType referenceFrame,
            Av1MotionVector vector,
            Av1InterpolationFilter horizontalFilter,
            Av1InterpolationFilter verticalFilter,
            Av1InterIntraMode interIntraMode,
            bool useWedge,
            int wedgeIndex)
        {
            if (!this.byteWorker.IsEmpty)
            {
                this.byteWorker[0].PrepareInterIntraPrediction(
                    transformWorkspace,
                    interWorkspace.Cast<byte>(),
                    MemoryMarshal.Cast<byte, byte>(transformPrediction),
                    MemoryMarshal.Cast<byte, byte>(interIntraAbove),
                    MemoryMarshal.Cast<byte, byte>(interIntraLeft),
                    compoundMask,
                    modeInfoGrid,
                    modeInfoAllocation,
                    MemoryMarshal.Cast<byte, byte>(sourceLuma),
                    MemoryMarshal.Cast<byte, byte>(sourceBlue),
                    MemoryMarshal.Cast<byte, byte>(sourceRed),
                    MemoryMarshal.Cast<byte, byte>(reconstructionLuma),
                    MemoryMarshal.Cast<byte, byte>(reconstructionBlue),
                    MemoryMarshal.Cast<byte, byte>(reconstructionRed),
                    macroBlock,
                    blockOrigin,
                    blockSize,
                    hasChroma,
                    mode,
                    referenceFrame,
                    vector,
                    horizontalFilter,
                    verticalFilter,
                    interIntraMode,
                    useWedge,
                    wedgeIndex);

                return;
            }

            this.uint16Worker[0].PrepareInterIntraPrediction(
                transformWorkspace,
                interWorkspace.Cast<ushort>(),
                MemoryMarshal.Cast<byte, ushort>(transformPrediction),
                MemoryMarshal.Cast<byte, ushort>(interIntraAbove),
                MemoryMarshal.Cast<byte, ushort>(interIntraLeft),
                compoundMask,
                modeInfoGrid,
                modeInfoAllocation,
                MemoryMarshal.Cast<byte, ushort>(sourceLuma),
                MemoryMarshal.Cast<byte, ushort>(sourceBlue),
                MemoryMarshal.Cast<byte, ushort>(sourceRed),
                MemoryMarshal.Cast<byte, ushort>(reconstructionLuma),
                MemoryMarshal.Cast<byte, ushort>(reconstructionBlue),
                MemoryMarshal.Cast<byte, ushort>(reconstructionRed),
                macroBlock,
                blockOrigin,
                blockSize,
                hasChroma,
                mode,
                referenceFrame,
                vector,
                horizontalFilter,
                verticalFilter,
                interIntraMode,
                useWedge,
                wedgeIndex);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.CreateSingleReferenceSearch"/>
        private SingleReferenceMotionSearch CreateSingleReferenceSearch(
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            ReadOnlySpan<byte> sourceLuma,
            Span<byte> motionSearchPrediction,
            Span<short> blockResidual,
            Av1EncoderInterPredictionWorkspace<byte> interWorkspace,
            in Av1NeighborEdges<byte> lumaCoefficientEdges,
            in Av1MotionVectorCosts motionVectorCosts,
            Point blockOrigin,
            Av1BlockSize blockSize,
            Av1ReferenceFrameType referenceFrame,
            int transformPartitionRate,
            int skipContext,
            out Size frameSize,
            out Rectangle frameBounds)
        {
            if (!this.byteWorker.IsEmpty)
            {
                return this.byteWorker[0].CreateSingleReferenceSearch(
                    writer,
                    in tables,
                    MemoryMarshal.Cast<byte, byte>(sourceLuma),
                    MemoryMarshal.Cast<byte, byte>(motionSearchPrediction),
                    blockResidual,
                    interWorkspace.Cast<byte>(),
                    in lumaCoefficientEdges,
                    in motionVectorCosts,
                    blockOrigin,
                    blockSize,
                    referenceFrame,
                    transformPartitionRate,
                    skipContext,
                    out frameSize,
                    out frameBounds);
            }

            return this.uint16Worker[0].CreateSingleReferenceSearch(
                writer,
                in tables,
                MemoryMarshal.Cast<byte, ushort>(sourceLuma),
                MemoryMarshal.Cast<byte, ushort>(motionSearchPrediction),
                blockResidual,
                interWorkspace.Cast<ushort>(),
                in lumaCoefficientEdges,
                in motionVectorCosts,
                blockOrigin,
                blockSize,
                referenceFrame,
                transformPartitionRate,
                skipContext,
                out frameSize,
                out frameBounds);
        }

        /// <summary>Reads one prediction or reconstruction slot in its original native sample width.</summary>
        /// <param name="workspace">The existing inter workspace, viewed as bytes.</param>
        /// <param name="index">The existing sample slot.</param>
        /// <returns>The same sample storage, measured in bytes.</returns>
        private Span<byte> GetInterSamples(in Av1EncoderInterPredictionWorkspace<byte> workspace, int index)
            => !this.byteWorker.IsEmpty
                ? workspace.GetSamples(index)
                : MemoryMarshal.AsBytes(workspace.Cast<ushort>().GetSamples(index));

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.SetBlockSegment"/>
        private void SetBlockSegment(ReadOnlySpan<byte> encoderSegmentMap, ReadOnlySpan<byte> previousSegmentMap, Point blockOrigin, Av1BlockSize blockSize)
        {
            if (!this.byteWorker.IsEmpty)
            {
                this.byteWorker[0].SetBlockSegment(encoderSegmentMap, previousSegmentMap, blockOrigin, blockSize);
                return;
            }

            this.uint16Worker[0].SetBlockSegment(encoderSegmentMap, previousSegmentMap, blockOrigin, blockSize);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.GetBlockRateMultiplier"/>
        private int GetBlockRateMultiplier(Point blockOrigin, Av1BlockSize blockSize, int segmentId)
        {
            if (!this.byteWorker.IsEmpty)
            {
                return this.byteWorker[0].GetBlockRateMultiplier(blockOrigin, blockSize, segmentId);
            }

            return this.uint16Worker[0].GetBlockRateMultiplier(blockOrigin, blockSize, segmentId);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.SetupBlockRateMultiplier"/>
        private int SetupBlockRateMultiplier(Point blockOrigin, Av1BlockSize blockSize, int segmentId)
        {
            if (!this.byteWorker.IsEmpty)
            {
                return this.byteWorker[0].SetupBlockRateMultiplier(blockOrigin, blockSize, segmentId);
            }

            return this.uint16Worker[0].SetupBlockRateMultiplier(blockOrigin, blockSize, segmentId);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.GetLogBlockVariance"/>
        private double GetLogBlockVariance(
            Span<int> workspaceStorage,
            ReadOnlySpan<byte> sourceLuma,
            ReadOnlySpan<byte> sourceBlue,
            ReadOnlySpan<byte> sourceRed,
            Point blockOrigin,
            Av1BlockSize blockSize)
        {
            if (!this.byteWorker.IsEmpty)
            {
                return this.byteWorker[0].GetLogBlockVariance(
                    workspaceStorage,
                    MemoryMarshal.Cast<byte, byte>(sourceLuma),
                    MemoryMarshal.Cast<byte, byte>(sourceBlue),
                    MemoryMarshal.Cast<byte, byte>(sourceRed),
                    blockOrigin,
                    blockSize);
            }

            return this.uint16Worker[0].GetLogBlockVariance(
                workspaceStorage,
                MemoryMarshal.Cast<byte, ushort>(sourceLuma),
                MemoryMarshal.Cast<byte, ushort>(sourceBlue),
                MemoryMarshal.Cast<byte, ushort>(sourceRed),
                blockOrigin,
                blockSize);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.GetVarianceSegmentId"/>
        private int GetVarianceSegmentId(
            Span<int> workspaceStorage,
            ReadOnlySpan<byte> sourceLuma,
            ReadOnlySpan<byte> sourceBlue,
            ReadOnlySpan<byte> sourceRed,
            Point blockOrigin,
            Av1BlockSize blockSize)
        {
            if (!this.byteWorker.IsEmpty)
            {
                return this.byteWorker[0].GetVarianceSegmentId(
                    workspaceStorage,
                    MemoryMarshal.Cast<byte, byte>(sourceLuma),
                    MemoryMarshal.Cast<byte, byte>(sourceBlue),
                    MemoryMarshal.Cast<byte, byte>(sourceRed),
                    blockOrigin,
                    blockSize);
            }

            return this.uint16Worker[0].GetVarianceSegmentId(
                workspaceStorage,
                MemoryMarshal.Cast<byte, ushort>(sourceLuma),
                MemoryMarshal.Cast<byte, ushort>(sourceBlue),
                MemoryMarshal.Cast<byte, ushort>(sourceRed),
                blockOrigin,
                blockSize);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.SelectComplexitySegment"/>
        private void SelectComplexitySegment(
            Span<byte> encoderSegmentMap,
            Span<int> workspaceStorage,
            ReadOnlySpan<byte> sourceLuma,
            ReadOnlySpan<byte> sourceBlue,
            ReadOnlySpan<byte> sourceRed,
            Point blockOrigin,
            Av1BlockSize blockSize,
            int projectedRate)
        {
            if (!this.byteWorker.IsEmpty)
            {
                this.byteWorker[0].SelectComplexitySegment(
                    encoderSegmentMap,
                    workspaceStorage,
                    MemoryMarshal.Cast<byte, byte>(sourceLuma),
                    MemoryMarshal.Cast<byte, byte>(sourceBlue),
                    MemoryMarshal.Cast<byte, byte>(sourceRed),
                    blockOrigin,
                    blockSize,
                    projectedRate);

                return;
            }

            this.uint16Worker[0].SelectComplexitySegment(
                encoderSegmentMap,
                workspaceStorage,
                MemoryMarshal.Cast<byte, ushort>(sourceLuma),
                MemoryMarshal.Cast<byte, ushort>(sourceBlue),
                MemoryMarshal.Cast<byte, ushort>(sourceRed),
                blockOrigin,
                blockSize,
                projectedRate);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.IsBlockOriginInsideFrame"/>
        private bool IsBlockOriginInsideFrame(Point blockOrigin)
        {
            if (!this.byteWorker.IsEmpty)
            {
                return this.byteWorker[0].IsBlockOriginInsideFrame(blockOrigin);
            }

            return this.uint16Worker[0].IsBlockOriginInsideFrame(blockOrigin);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.GetLeafCostLimit"/>
        private long GetLeafCostLimit(
            Av1RateDistortionStatistics remainingCost,
            Point leafOrigin,
            Av1BlockSize leafSize,
            bool searchesOwnPartition)
        {
            if (!this.byteWorker.IsEmpty)
            {
                return this.byteWorker[0].GetLeafCostLimit(remainingCost, leafOrigin, leafSize, searchesOwnPartition);
            }

            return this.uint16Worker[0].GetLeafCostLimit(remainingCost, leafOrigin, leafSize, searchesOwnPartition);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.ReconstructPartitionLeaf"/>
        private Av1RateDistortionStatistics ReconstructPartitionLeaf(
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            in Av1EncoderModeDecisionWorkspace<byte> modeWorkspace,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> transformWorkspace,
            in Av1EncoderInterPredictionWorkspace<byte> interWorkspace,
            Span<byte> transformPrediction,
            Span<byte> interIntraAbove,
            Span<byte> interIntraLeft,
            Span<ushort> firstIntermediate,
            Span<ushort> secondIntermediate,
            Span<byte> compoundMask,
            in Av1NeighborEdges<byte> transformEdges,
            in Av1NeighborEdges<Av1EncoderPaletteInfo> paletteEdges,
            in Av1NeighborEdges<byte> lumaCoefficientEdges,
            in Av1NeighborEdges<byte> blueCoefficientEdges,
            in Av1NeighborEdges<byte> redCoefficientEdges,
            Span<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            Span<Av1EncoderDisplacementVector> displacementVectors,
            Span<Av1EncoderReferenceContext> referenceContexts,
            ReadOnlySpan<byte> encoderSegmentMap,
            ReadOnlySpan<byte> previousSegmentMap,
            Span<int> superblockCoefficients,
            ReadOnlySpan<byte> sourceLuma,
            ReadOnlySpan<byte> sourceBlue,
            ReadOnlySpan<byte> sourceRed,
            Span<byte> reconstructionLuma,
            Span<byte> reconstructionBlue,
            Span<byte> reconstructionRed,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1EncoderPartitionTree.ModeContext context,
            bool publishContexts,
            bool modeSearchReused,
            bool encodeFollows)
        {
            if (!this.byteWorker.IsEmpty)
            {
                return this.byteWorker[0].ReconstructPartitionLeaf(
                    writer,
                    in tables,
                    modeWorkspace.Cast<byte>(),
                    transformCoefficients,
                    dequantizedCoefficients,
                    transformWorkspace,
                    interWorkspace.Cast<byte>(),
                    MemoryMarshal.Cast<byte, byte>(transformPrediction),
                    MemoryMarshal.Cast<byte, byte>(interIntraAbove),
                    MemoryMarshal.Cast<byte, byte>(interIntraLeft),
                    firstIntermediate,
                    secondIntermediate,
                    compoundMask,
                    in transformEdges,
                    in paletteEdges,
                    in lumaCoefficientEdges,
                    in blueCoefficientEdges,
                    in redCoefficientEdges,
                    modeInfoGrid,
                    modeInfoAllocation,
                    displacementVectors,
                    referenceContexts,
                    encoderSegmentMap,
                    previousSegmentMap,
                    superblockCoefficients,
                    MemoryMarshal.Cast<byte, byte>(sourceLuma),
                    MemoryMarshal.Cast<byte, byte>(sourceBlue),
                    MemoryMarshal.Cast<byte, byte>(sourceRed),
                    MemoryMarshal.Cast<byte, byte>(reconstructionLuma),
                    MemoryMarshal.Cast<byte, byte>(reconstructionBlue),
                    MemoryMarshal.Cast<byte, byte>(reconstructionRed),
                    macroBlock,
                    blockOrigin,
                    context,
                    publishContexts,
                    modeSearchReused,
                    encodeFollows);
            }

            return this.uint16Worker[0].ReconstructPartitionLeaf(
                writer,
                in tables,
                modeWorkspace.Cast<ushort>(),
                transformCoefficients,
                dequantizedCoefficients,
                transformWorkspace,
                interWorkspace.Cast<ushort>(),
                MemoryMarshal.Cast<byte, ushort>(transformPrediction),
                MemoryMarshal.Cast<byte, ushort>(interIntraAbove),
                MemoryMarshal.Cast<byte, ushort>(interIntraLeft),
                firstIntermediate,
                secondIntermediate,
                compoundMask,
                in transformEdges,
                in paletteEdges,
                in lumaCoefficientEdges,
                in blueCoefficientEdges,
                in redCoefficientEdges,
                modeInfoGrid,
                modeInfoAllocation,
                displacementVectors,
                referenceContexts,
                encoderSegmentMap,
                previousSegmentMap,
                superblockCoefficients,
                MemoryMarshal.Cast<byte, ushort>(sourceLuma),
                MemoryMarshal.Cast<byte, ushort>(sourceBlue),
                MemoryMarshal.Cast<byte, ushort>(sourceRed),
                MemoryMarshal.Cast<byte, ushort>(reconstructionLuma),
                MemoryMarshal.Cast<byte, ushort>(reconstructionBlue),
                MemoryMarshal.Cast<byte, ushort>(reconstructionRed),
                macroBlock,
                blockOrigin,
                context,
                publishContexts,
                modeSearchReused,
                encodeFollows);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.IsPartitionSiblingInsideFrame"/>
        private bool IsPartitionSiblingInsideFrame(
            Point blockOrigin,
            Av1BlockSize blockSize,
            Av1PartitionType partitionType,
            int leafIndex)
        {
            if (!this.byteWorker.IsEmpty)
            {
                return this.byteWorker[0].IsPartitionSiblingInsideFrame(blockOrigin, blockSize, partitionType, leafIndex);
            }

            return this.uint16Worker[0].IsPartitionSiblingInsideFrame(blockOrigin, blockSize, partitionType, leafIndex);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.GetSkipReferenceFrameMask"/>
        private int GetSkipReferenceFrameMask(Point blockOrigin, Av1BlockSize blockSize, Av1PartitionType partitionType)
        {
            if (!this.byteWorker.IsEmpty)
            {
                return this.byteWorker[0].GetSkipReferenceFrameMask(blockOrigin, blockSize, partitionType);
            }

            return this.uint16Worker[0].GetSkipReferenceFrameMask(blockOrigin, blockSize, partitionType);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.SetInterModeSkipMasks"/>
        private void SetInterModeSkipMasks(
            ReadOnlySpan<byte> sourceLuma,
            ReadOnlySpan<byte> sourceBlue,
            ReadOnlySpan<byte> sourceRed,
            Point blockOrigin,
            Av1BlockSize blockSize,
            ReadOnlySpan<Av1ReferenceMotionVectors> singleReferenceVectors)
        {
            if (!this.byteWorker.IsEmpty)
            {
                this.byteWorker[0].SetInterModeSkipMasks(
                    MemoryMarshal.Cast<byte, byte>(sourceLuma),
                    MemoryMarshal.Cast<byte, byte>(sourceBlue),
                    MemoryMarshal.Cast<byte, byte>(sourceRed),
                    blockOrigin,
                    blockSize,
                    singleReferenceVectors);

                return;
            }

            this.uint16Worker[0].SetInterModeSkipMasks(
                MemoryMarshal.Cast<byte, ushort>(sourceLuma),
                MemoryMarshal.Cast<byte, ushort>(sourceBlue),
                MemoryMarshal.Cast<byte, ushort>(sourceRed),
                blockOrigin,
                blockSize,
                singleReferenceVectors);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.PrepareTplInterModePruning"/>
        private void PrepareTplInterModePruning(Span<int> workspaceStorage, Point blockOrigin, Av1BlockSize blockSize)
        {
            if (!this.byteWorker.IsEmpty)
            {
                this.byteWorker[0].PrepareTplInterModePruning(workspaceStorage, blockOrigin, blockSize);
                return;
            }

            this.uint16Worker[0].PrepareTplInterModePruning(workspaceStorage, blockOrigin, blockSize);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.IsSingleReferenceSkipped"/>
        private bool IsSingleReferenceSkipped(int reference)
        {
            if (!this.byteWorker.IsEmpty)
            {
                return this.byteWorker[0].IsSingleReferenceSkipped(reference);
            }

            return this.uint16Worker[0].IsSingleReferenceSkipped(reference);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.ShouldPruneNearMode"/>
        private bool ShouldPruneNearMode(
            ReadOnlySpan<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            Av1MacroBlockD macroBlock,
            Av1ReferenceFrameType primary,
            Av1ReferenceFrameType secondary,
            long bestCost)
        {
            if (!this.byteWorker.IsEmpty)
            {
                return this.byteWorker[0].ShouldPruneNearMode(modeInfoGrid, modeInfoAllocation, macroBlock, primary, secondary, bestCost);
            }

            return this.uint16Worker[0].ShouldPruneNearMode(modeInfoGrid, modeInfoAllocation, macroBlock, primary, secondary, bestCost);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.PrunesZeroVectorWithSse"/>
        private bool PrunesZeroVectorWithSse(
            ReadOnlySpan<byte> sourceLuma,
            ReadOnlySpan<byte> sourceBlue,
            ReadOnlySpan<byte> sourceRed,
            Point blockOrigin,
            Av1BlockSize blockSize,
            Av1ReferenceFrameType primaryReference,
            Av1ReferenceFrameType secondaryReference)
        {
            if (!this.byteWorker.IsEmpty)
            {
                return this.byteWorker[0].PrunesZeroVectorWithSse(
                    MemoryMarshal.Cast<byte, byte>(sourceLuma),
                    MemoryMarshal.Cast<byte, byte>(sourceBlue),
                    MemoryMarshal.Cast<byte, byte>(sourceRed),
                    blockOrigin,
                    blockSize,
                    primaryReference,
                    secondaryReference);
            }

            return this.uint16Worker[0].PrunesZeroVectorWithSse(
                MemoryMarshal.Cast<byte, ushort>(sourceLuma),
                MemoryMarshal.Cast<byte, ushort>(sourceBlue),
                MemoryMarshal.Cast<byte, ushort>(sourceRed),
                blockOrigin,
                blockSize,
                primaryReference,
                secondaryReference);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.RecordMotionModeWinner"/>
        private void RecordMotionModeWinner(
            long cost,
            bool isCompound,
            Av1MacroBlockModeInfo modeInfo,
            Av1EncoderBlockStruct block,
            Av1MotionVector vector)
        {
            if (!this.byteWorker.IsEmpty)
            {
                this.byteWorker[0].RecordMotionModeWinner(cost, isCompound, modeInfo, block, vector);
                return;
            }

            this.uint16Worker[0].RecordMotionModeWinner(cost, isCompound, modeInfo, block, vector);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.IsCachedCompoundPair"/>
        private bool IsCachedCompoundPair(Av1ReferenceFrameType first, Av1ReferenceFrameType second)
        {
            if (!this.byteWorker.IsEmpty)
            {
                return this.byteWorker[0].IsCachedCompoundPair(first, second);
            }

            return this.uint16Worker[0].IsCachedCompoundPair(first, second);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.PrunesCompoundReferencePair"/>
        private bool PrunesCompoundReferencePair(Av1ReferenceFrameType first, Av1ReferenceFrameType second)
        {
            if (!this.byteWorker.IsEmpty)
            {
                return this.byteWorker[0].PrunesCompoundReferencePair(first, second);
            }

            return this.uint16Worker[0].PrunesCompoundReferencePair(first, second);
        }

        /// <summary>Gets the existing padded reference plane bounds in sample coordinates.</summary>
        /// <param name="referenceFrame">The selected prediction reference.</param>
        /// <returns>The reference plane bounds without changing its storage.</returns>
        private Rectangle GetReferenceBounds(Av1ReferenceFrameType referenceFrame)
            => !this.byteWorker.IsEmpty
                ? this.byteWorker[0].GetReferenceBounds(referenceFrame)
                : this.uint16Worker[0].GetReferenceBounds(referenceFrame);

        /// <summary>Reads the typed worker's cached 4x4 variance range.</summary>
        private (double Minimum, double Maximum) GetLogSubBlockVariance(
            Span<int> workspaceStorage, ReadOnlySpan<byte> sourceLuma, ReadOnlySpan<byte> sourceBlue, ReadOnlySpan<byte> sourceRed, Rectangle bounds)
        {
            if (!this.byteWorker.IsEmpty)
            {
                return this.byteWorker[0].GetLogSubBlockVariance(workspaceStorage, sourceLuma, sourceBlue, sourceRed, bounds);
            }

            return this.uint16Worker[0].GetLogSubBlockVariance(
                workspaceStorage,
                MemoryMarshal.Cast<byte, ushort>(sourceLuma),
                MemoryMarshal.Cast<byte, ushort>(sourceBlue),
                MemoryMarshal.Cast<byte, ushort>(sourceRed),
                bounds);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.PrepareVariancePartitions" />
        private void PrepareVariancePartitions(
            Span<int> transformCoefficients,
            in Av1EncoderInterPredictionWorkspace<byte> interWorkspace,
            Span<byte> midpoint,
            Span<ushort> firstIntermediate,
            Span<ushort> secondIntermediate,
            Span<byte> compoundMask,
            ReadOnlySpan<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            ReadOnlySpan<Av1EncoderDisplacementVector> displacementVectors,
            ReadOnlySpan<byte> encoderSegmentMap,
            ReadOnlySpan<byte> previousSegmentMap,
            Span<int> workspaceStorage,
            Span<byte> sourceLuma,
            Span<byte> sourceBlue,
            Span<byte> sourceRed,
            Av1MacroBlockD macroBlock,
            Point superblockOrigin)
        {
            if (!this.byteWorker.IsEmpty)
            {
                this.byteWorker[0].PrepareVariancePartitions(
                transformCoefficients,
                interWorkspace.Cast<byte>(),
                MemoryMarshal.Cast<byte, byte>(midpoint),
                firstIntermediate,
                secondIntermediate,
                compoundMask,
                modeInfoGrid,
                modeInfoAllocation,
                displacementVectors,
                encoderSegmentMap,
                previousSegmentMap,
                workspaceStorage,
                MemoryMarshal.Cast<byte, byte>(sourceLuma),
                MemoryMarshal.Cast<byte, byte>(sourceBlue),
                MemoryMarshal.Cast<byte, byte>(sourceRed),
                macroBlock,
                superblockOrigin);
                return;
            }

            this.uint16Worker[0].PrepareVariancePartitions(
                transformCoefficients,
                interWorkspace.Cast<ushort>(),
                MemoryMarshal.Cast<byte, ushort>(midpoint),
                firstIntermediate,
                secondIntermediate,
                compoundMask,
                modeInfoGrid,
                modeInfoAllocation,
                displacementVectors,
                encoderSegmentMap,
                previousSegmentMap,
                workspaceStorage,
                MemoryMarshal.Cast<byte, ushort>(sourceLuma),
                MemoryMarshal.Cast<byte, ushort>(sourceBlue),
                MemoryMarshal.Cast<byte, ushort>(sourceRed),
                macroBlock,
                superblockOrigin);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.PreparePartitionGeometry" />
        private void PreparePartitionGeometry(
            Span<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            Point blockOrigin,
            Av1BlockSize blockSize,
            Av1PartitionType partitionType)
        {
            if (!this.byteWorker.IsEmpty)
            {
                this.byteWorker[0].PreparePartitionGeometry(modeInfoGrid, modeInfoAllocation, blockOrigin, blockSize, partitionType);
                return;
            }

            this.uint16Worker[0].PreparePartitionGeometry(modeInfoGrid, modeInfoAllocation, blockOrigin, blockSize, partitionType);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.CalculateDoSplit" />
        private bool CalculateDoSplit(
            ReadOnlySpan<byte> sourceLuma,
            ReadOnlySpan<byte> sourceBlue,
            ReadOnlySpan<byte> sourceRed,
            in Av1EncoderInterPredictionWorkspace<byte> interWorkspace,
            int mergeLevel,
            bool noneSkip,
            bool noneBoosted,
            Av1PredictionMode noneMode,
            Av1BlockSize blockSize,
            Point blockOrigin)
        {
            if (!this.byteWorker.IsEmpty)
            {
                return this.byteWorker[0].CalculateDoSplit(
                MemoryMarshal.Cast<byte, byte>(sourceLuma),
                MemoryMarshal.Cast<byte, byte>(sourceBlue),
                MemoryMarshal.Cast<byte, byte>(sourceRed),
                interWorkspace.Cast<byte>(),
                mergeLevel,
                noneSkip,
                noneBoosted,
                noneMode,
                blockSize,
                blockOrigin);
            }

            return this.uint16Worker[0].CalculateDoSplit(
                MemoryMarshal.Cast<byte, ushort>(sourceLuma),
                MemoryMarshal.Cast<byte, ushort>(sourceBlue),
                MemoryMarshal.Cast<byte, ushort>(sourceRed),
                interWorkspace.Cast<ushort>(),
                mergeLevel,
                noneSkip,
                noneBoosted,
                noneMode,
                blockSize,
                blockOrigin);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.ScaleSsimRateMultiplier" />
        private int ScaleSsimRateMultiplier(int rateMultiplier, Point blockOrigin, Av1BlockSize blockSize)
        {
            if (!this.byteWorker.IsEmpty)
            {
                return this.byteWorker[0].ScaleSsimRateMultiplier(rateMultiplier, blockOrigin, blockSize);
            }

            return this.uint16Worker[0].ScaleSsimRateMultiplier(rateMultiplier, blockOrigin, blockSize);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.GetSourceLogVariance" />
        private double GetSourceLogVariance(
            Span<int> workspaceStorage,
            ReadOnlySpan<byte> sourceLuma,
            ReadOnlySpan<byte> sourceBlue,
            ReadOnlySpan<byte> sourceRed,
            Point origin)
        {
            if (!this.byteWorker.IsEmpty)
            {
                return this.byteWorker[0].GetSourceLogVariance(
                workspaceStorage,
                MemoryMarshal.Cast<byte, byte>(sourceLuma),
                MemoryMarshal.Cast<byte, byte>(sourceBlue),
                MemoryMarshal.Cast<byte, byte>(sourceRed),
                origin);
            }

            return this.uint16Worker[0].GetSourceLogVariance(
                workspaceStorage,
                MemoryMarshal.Cast<byte, ushort>(sourceLuma),
                MemoryMarshal.Cast<byte, ushort>(sourceBlue),
                MemoryMarshal.Cast<byte, ushort>(sourceRed),
                origin);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.ShouldReconstructSelectedTree" />
        private bool ShouldReconstructSelectedTree(Av1BlockSize blockSize, int nodeIndex)
        {
            if (!this.byteWorker.IsEmpty)
            {
                return this.byteWorker[0].ShouldReconstructSelectedTree(blockSize, nodeIndex);
            }

            return this.uint16Worker[0].ShouldReconstructSelectedTree(blockSize, nodeIndex);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.UpdateCdefSkip" />
        private void UpdateCdefSkip(Span<Av1MacroBlockModeInfo> modeInfoAllocation, Point blockOrigin, Av1PredictionMode mode)
        {
            if (!this.byteWorker.IsEmpty)
            {
                this.byteWorker[0].UpdateCdefSkip(modeInfoAllocation, blockOrigin, mode);
                return;
            }

            this.uint16Worker[0].UpdateCdefSkip(modeInfoAllocation, blockOrigin, mode);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.SetBlockGeometry" />
        private void SetBlockGeometry(
            Span<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            Point blockOrigin,
            Av1BlockSize blockSize,
            Av1PartitionType partitionType)
        {
            if (!this.byteWorker.IsEmpty)
            {
                this.byteWorker[0].SetBlockGeometry(modeInfoGrid, modeInfoAllocation, blockOrigin, blockSize, partitionType);
                return;
            }

            this.uint16Worker[0].SetBlockGeometry(modeInfoGrid, modeInfoAllocation, blockOrigin, blockSize, partitionType);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.RefineWarpedVector" />
        private Av1MotionVector RefineWarpedVector(
            ReadOnlySpan<byte> sourceLuma,
            ReadOnlySpan<byte> sourceBlue,
            ReadOnlySpan<byte> sourceRed,
            in Av1EncoderInterPredictionWorkspace<byte> interWorkspace,
            in Av1MotionVectorCosts motionVectorCosts,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1EncoderBlockModeInfo mode,
            Av1MotionVector vector,
            Av1MotionVector referenceVector)
        {
            if (!this.byteWorker.IsEmpty)
            {
                return this.byteWorker[0].RefineWarpedVector(
                MemoryMarshal.Cast<byte, byte>(sourceLuma),
                MemoryMarshal.Cast<byte, byte>(sourceBlue),
                MemoryMarshal.Cast<byte, byte>(sourceRed),
                interWorkspace.Cast<byte>(),
                in motionVectorCosts,
                macroBlock,
                blockOrigin,
                mode,
                vector,
                referenceVector);
            }

            return this.uint16Worker[0].RefineWarpedVector(
                MemoryMarshal.Cast<byte, ushort>(sourceLuma),
                MemoryMarshal.Cast<byte, ushort>(sourceBlue),
                MemoryMarshal.Cast<byte, ushort>(sourceRed),
                interWorkspace.Cast<ushort>(),
                in motionVectorCosts,
                macroBlock,
                blockOrigin,
                mode,
                vector,
                referenceVector);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.PrepareSelectedInterLumaPrediction" />
        private void PrepareSelectedInterLumaPrediction(
            Span<int> transformWorkspace,
            in Av1EncoderInterPredictionWorkspace<byte> interWorkspace,
            Span<byte> transformPrediction,
            Span<byte> interIntraAbove,
            Span<byte> interIntraLeft,
            Span<ushort> firstIntermediate,
            Span<ushort> secondIntermediate,
            Span<byte> compoundMask,
            ReadOnlySpan<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            ReadOnlySpan<Av1EncoderDisplacementVector> displacementVectors,
            ReadOnlySpan<byte> sourceLuma,
            ReadOnlySpan<byte> sourceBlue,
            ReadOnlySpan<byte> sourceRed,
            Span<byte> reconstructionLuma,
            Span<byte> reconstructionBlue,
            Span<byte> reconstructionRed,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1MacroBlockModeInfo modeInfo,
            Av1EncoderBlockStruct block,
            Av1MotionVector vector,
            Av1MotionVector secondaryVector)
        {
            if (!this.byteWorker.IsEmpty)
            {
                this.byteWorker[0].PrepareSelectedInterLumaPrediction(
                transformWorkspace,
                interWorkspace.Cast<byte>(),
                MemoryMarshal.Cast<byte, byte>(transformPrediction),
                MemoryMarshal.Cast<byte, byte>(interIntraAbove),
                MemoryMarshal.Cast<byte, byte>(interIntraLeft),
                firstIntermediate,
                secondIntermediate,
                compoundMask,
                modeInfoGrid,
                modeInfoAllocation,
                displacementVectors,
                MemoryMarshal.Cast<byte, byte>(sourceLuma),
                MemoryMarshal.Cast<byte, byte>(sourceBlue),
                MemoryMarshal.Cast<byte, byte>(sourceRed),
                MemoryMarshal.Cast<byte, byte>(reconstructionLuma),
                MemoryMarshal.Cast<byte, byte>(reconstructionBlue),
                MemoryMarshal.Cast<byte, byte>(reconstructionRed),
                macroBlock,
                blockOrigin,
                modeInfo,
                block,
                vector,
                secondaryVector);
                return;
            }

            this.uint16Worker[0].PrepareSelectedInterLumaPrediction(
                transformWorkspace,
                interWorkspace.Cast<ushort>(),
                MemoryMarshal.Cast<byte, ushort>(transformPrediction),
                MemoryMarshal.Cast<byte, ushort>(interIntraAbove),
                MemoryMarshal.Cast<byte, ushort>(interIntraLeft),
                firstIntermediate,
                secondIntermediate,
                compoundMask,
                modeInfoGrid,
                modeInfoAllocation,
                displacementVectors,
                MemoryMarshal.Cast<byte, ushort>(sourceLuma),
                MemoryMarshal.Cast<byte, ushort>(sourceBlue),
                MemoryMarshal.Cast<byte, ushort>(sourceRed),
                MemoryMarshal.Cast<byte, ushort>(reconstructionLuma),
                MemoryMarshal.Cast<byte, ushort>(reconstructionBlue),
                MemoryMarshal.Cast<byte, ushort>(reconstructionRed),
                macroBlock,
                blockOrigin,
                modeInfo,
                block,
                vector,
                secondaryVector);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.PenalizeTransformSkip" />
        private void PenalizeTransformSkip(
            ReadOnlySpan<byte> sourceLuma,
            ReadOnlySpan<byte> sourceBlue,
            ReadOnlySpan<byte> sourceRed,
            Point blockOrigin,
            Av1BlockSize blockSize,
            ReadOnlySpan<byte> prediction,
            ref long codedDistortion,
            ref long skipDistortion)
        {
            if (!this.byteWorker.IsEmpty)
            {
                this.byteWorker[0].PenalizeTransformSkip(
                MemoryMarshal.Cast<byte, byte>(sourceLuma),
                MemoryMarshal.Cast<byte, byte>(sourceBlue),
                MemoryMarshal.Cast<byte, byte>(sourceRed),
                blockOrigin,
                blockSize,
                MemoryMarshal.Cast<byte, byte>(prediction),
                ref codedDistortion,
                ref skipDistortion);
                return;
            }

            this.uint16Worker[0].PenalizeTransformSkip(
                MemoryMarshal.Cast<byte, ushort>(sourceLuma),
                MemoryMarshal.Cast<byte, ushort>(sourceBlue),
                MemoryMarshal.Cast<byte, ushort>(sourceRed),
                blockOrigin,
                blockSize,
                MemoryMarshal.Cast<byte, ushort>(prediction),
                ref codedDistortion,
                ref skipDistortion);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.ChargeRefinedInterTextureLoss" />
        private void ChargeRefinedInterTextureLoss(
            ReadOnlySpan<byte> sourceLuma,
            ReadOnlySpan<byte> sourceBlue,
            ReadOnlySpan<byte> sourceRed,
            ref Av1RateDistortionStatistics statistics,
            Point blockOrigin,
            Av1EncoderBlockModeInfo block,
            Av1MotionVector vector,
            ReadOnlySpan<byte> prediction,
            bool skip)
        {
            if (!this.byteWorker.IsEmpty)
            {
                this.byteWorker[0].ChargeRefinedInterTextureLoss(
                MemoryMarshal.Cast<byte, byte>(sourceLuma),
                MemoryMarshal.Cast<byte, byte>(sourceBlue),
                MemoryMarshal.Cast<byte, byte>(sourceRed),
                ref statistics,
                blockOrigin,
                block,
                vector,
                MemoryMarshal.Cast<byte, byte>(prediction),
                skip);
                return;
            }

            this.uint16Worker[0].ChargeRefinedInterTextureLoss(
                MemoryMarshal.Cast<byte, ushort>(sourceLuma),
                MemoryMarshal.Cast<byte, ushort>(sourceBlue),
                MemoryMarshal.Cast<byte, ushort>(sourceRed),
                ref statistics,
                blockOrigin,
                block,
                vector,
                MemoryMarshal.Cast<byte, ushort>(prediction),
                skip);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.PrunesOlderReference" />
        private bool PrunesOlderReference(
            Span<int> workspaceStorage,
            Av1ReferenceFrameType first,
            Av1ReferenceFrameType second,
            Av1ReferenceFrameType candidate,
            Av1ReferenceFrameType anchor)
        {
            if (!this.byteWorker.IsEmpty)
            {
                return this.byteWorker[0].PrunesOlderReference(workspaceStorage, first, second, candidate, anchor);
            }

            return this.uint16Worker[0].PrunesOlderReference(workspaceStorage, first, second, candidate, anchor);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.GetPredictionModelSize" />
        private Size GetPredictionModelSize(Point blockOrigin, Av1BlockSize blockSize, int subsamplingX, int subsamplingY)
        {
            if (!this.byteWorker.IsEmpty)
            {
                return this.byteWorker[0].GetPredictionModelSize(blockOrigin, blockSize, subsamplingX, subsamplingY);
            }

            return this.uint16Worker[0].GetPredictionModelSize(blockOrigin, blockSize, subsamplingX, subsamplingY);
        }

        /// <summary>Measures one native reconstruction cell without converting its samples.</summary>
        /// <param name="cell">The cell storage, measured in bytes.</param>
        /// <param name="stride">The row stride in samples.</param>
        /// <param name="bitDepth">The coded sample precision.</param>
        /// <returns>The logarithm of the sample variance.</returns>
        private double GetLogVariance(ReadOnlySpan<byte> cell, int stride, Av1BitDepth bitDepth)
            => !this.byteWorker.IsEmpty
                ? ModeDecision<byte, ByteOperator>.GetLogVariance(cell, stride, bitDepth)
                : ModeDecision<ushort, UInt16Operator>.GetLogVariance(MemoryMarshal.Cast<byte, ushort>(cell), stride, bitDepth);

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.PrepareReferencePlanePrediction" />
        private void PrepareReferencePlanePrediction(
            bool useSearchReferences,
            Av1MotionVector vector,
            Av1MotionVector secondaryVector,
            Av1Plane plane,
            Av1PredictionMode predictionMode,
            Av1ReferenceFrameType primaryReferenceFrame,
            Av1ReferenceFrameType secondaryReferenceFrame,
            bool usePreparedPrediction,
            Av1CompoundType compoundType,
            int compoundWedgeIndex,
            bool compoundWedgeSign,
            Av1DifferenceWeightedMaskType differenceWeightedMaskType,
            Av1InterpolationFilter horizontalFilter,
            Av1InterpolationFilter verticalFilter,
            Point lumaOrigin,
            int subsamplingX,
            int subsamplingY,
            Av1BlockSize blockSize,
            Span<byte> prediction,
            Span<short> residual,
            Span<short> filterRows,
            Span<ushort> firstIntermediate,
            Span<ushort> secondIntermediate,
            Span<byte> compoundMask,
            ReadOnlySpan<int> modeInfoGrid,
            ReadOnlySpan<Av1MacroBlockModeInfo> modeInfoAllocation,
            ReadOnlySpan<Av1EncoderDisplacementVector> displacementVectors,
            ReadOnlySpan<byte> sourceLuma,
            ReadOnlySpan<byte> sourceBlue,
            ReadOnlySpan<byte> sourceRed)
        {
            if (!this.byteWorker.IsEmpty)
            {
                this.byteWorker[0].PrepareReferencePlanePrediction(
                useSearchReferences,
                vector,
                secondaryVector,
                plane,
                predictionMode,
                primaryReferenceFrame,
                secondaryReferenceFrame,
                usePreparedPrediction,
                compoundType,
                compoundWedgeIndex,
                compoundWedgeSign,
                differenceWeightedMaskType,
                horizontalFilter,
                verticalFilter,
                lumaOrigin,
                subsamplingX,
                subsamplingY,
                blockSize,
                MemoryMarshal.Cast<byte, byte>(prediction),
                residual,
                filterRows,
                firstIntermediate,
                secondIntermediate,
                compoundMask,
                modeInfoGrid,
                modeInfoAllocation,
                displacementVectors,
                MemoryMarshal.Cast<byte, byte>(sourceLuma),
                MemoryMarshal.Cast<byte, byte>(sourceBlue),
                MemoryMarshal.Cast<byte, byte>(sourceRed));
                return;
            }

            this.uint16Worker[0].PrepareReferencePlanePrediction(
                useSearchReferences,
                vector,
                secondaryVector,
                plane,
                predictionMode,
                primaryReferenceFrame,
                secondaryReferenceFrame,
                usePreparedPrediction,
                compoundType,
                compoundWedgeIndex,
                compoundWedgeSign,
                differenceWeightedMaskType,
                horizontalFilter,
                verticalFilter,
                lumaOrigin,
                subsamplingX,
                subsamplingY,
                blockSize,
                MemoryMarshal.Cast<byte, ushort>(prediction),
                residual,
                filterRows,
                firstIntermediate,
                secondIntermediate,
                compoundMask,
                modeInfoGrid,
                modeInfoAllocation,
                displacementVectors,
                MemoryMarshal.Cast<byte, ushort>(sourceLuma),
                MemoryMarshal.Cast<byte, ushort>(sourceBlue),
                MemoryMarshal.Cast<byte, ushort>(sourceRed));
        }

        /// <summary>Converts a sample count to the length of its borrowed byte view.</summary>
        /// <param name="count">The number of native samples.</param>
        /// <returns>The byte count. The sample values and their precision do not change.</returns>
        private int GetSampleByteCount(int count) => this.byteWorker.IsEmpty ? count * sizeof(ushort) : count;

        /// <summary>Measures a complete native prediction block with the existing sample operator.</summary>
        /// <param name="source">The source storage, measured in bytes.</param>
        /// <param name="sourceStride">The source row stride in samples.</param>
        /// <param name="prediction">The prediction storage, measured in bytes.</param>
        /// <param name="predictionStride">The prediction row stride in samples.</param>
        /// <param name="width">The block width in samples.</param>
        /// <param name="height">The block height in samples.</param>
        /// <param name="sum">The sum of signed sample errors.</param>
        /// <param name="squares">The sum of squared sample errors.</param>
        private void GetMoments(
            ReadOnlySpan<byte> source,
            int sourceStride,
            ReadOnlySpan<byte> prediction,
            int predictionStride,
            int width,
            int height,
            out int sum,
            out long squares)
        {
            if (!this.byteWorker.IsEmpty)
            {
                ByteOperator.GetMoments(source, sourceStride, prediction, predictionStride, width, height, out sum, out squares);
                return;
            }

            UInt16Operator.GetMoments(
                MemoryMarshal.Cast<byte, ushort>(source),
                sourceStride,
                MemoryMarshal.Cast<byte, ushort>(prediction),
                predictionStride,
                width,
                height,
                out sum,
                out squares);
        }

        /// <summary>Blends a complete prediction block with the existing native SIMD operator.</summary>
        /// <param name="destination">The first prediction and blend destination, measured in bytes.</param>
        /// <param name="destinationStride">The destination row stride in samples.</param>
        /// <param name="second">The second prediction, measured in bytes.</param>
        /// <param name="secondStride">The second prediction row stride in samples.</param>
        /// <param name="mask">The weights of the first prediction.</param>
        /// <param name="maskStride">The mask row stride, or zero to repeat one row.</param>
        /// <param name="width">The block width in samples.</param>
        /// <param name="height">The block height in samples.</param>
        private void BlendMask(
            Span<byte> destination,
            int destinationStride,
            ReadOnlySpan<byte> second,
            int secondStride,
            ReadOnlySpan<byte> mask,
            int maskStride,
            int width,
            int height)
        {
            if (!this.byteWorker.IsEmpty)
            {
                ByteOperator.BlendMask(destination, destinationStride, second, secondStride, mask, maskStride, width, height);
                return;
            }

            UInt16Operator.BlendMask(
                MemoryMarshal.Cast<byte, ushort>(destination),
                destinationStride,
                MemoryMarshal.Cast<byte, ushort>(second),
                secondStride,
                mask,
                maskStride,
                width,
                height);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.SelectCompoundMask" />
        private long SelectCompoundMask(
            ReadOnlySpan<byte> sourceLuma,
            ReadOnlySpan<byte> sourceBlue,
            ReadOnlySpan<byte> sourceRed,
            in Av1CoefficientTables tables,
            in Av1EncoderInterPredictionWorkspace<byte> interWorkspace,
            Span<byte> compoundMask,
            Point blockOrigin,
            Av1BlockSize blockSize,
            Av1CompoundType compoundType,
            ReadOnlySpan<byte> firstPrediction,
            ReadOnlySpan<byte> secondPrediction,
            out int wedgeIndex,
            out bool wedgeSign,
            out Av1DifferenceWeightedMaskType maskType,
            out long selectedError)
        {
            if (!this.byteWorker.IsEmpty)
            {
                return this.byteWorker[0].SelectCompoundMask(
                MemoryMarshal.Cast<byte, byte>(sourceLuma),
                MemoryMarshal.Cast<byte, byte>(sourceBlue),
                MemoryMarshal.Cast<byte, byte>(sourceRed),
                in tables,
                interWorkspace.Cast<byte>(),
                compoundMask,
                blockOrigin,
                blockSize,
                compoundType,
                MemoryMarshal.Cast<byte, byte>(firstPrediction),
                MemoryMarshal.Cast<byte, byte>(secondPrediction),
                out wedgeIndex,
                out wedgeSign,
                out maskType,
                out selectedError);
            }

            return this.uint16Worker[0].SelectCompoundMask(
                MemoryMarshal.Cast<byte, ushort>(sourceLuma),
                MemoryMarshal.Cast<byte, ushort>(sourceBlue),
                MemoryMarshal.Cast<byte, ushort>(sourceRed),
                in tables,
                interWorkspace.Cast<ushort>(),
                compoundMask,
                blockOrigin,
                blockSize,
                compoundType,
                MemoryMarshal.Cast<byte, ushort>(firstPrediction),
                MemoryMarshal.Cast<byte, ushort>(secondPrediction),
                out wedgeIndex,
                out wedgeSign,
                out maskType,
                out selectedError);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.RefineCompoundVectors" />
        private void RefineCompoundVectors(
            Span<int> workspaceStorage,
            ReadOnlySpan<byte> sourceLuma,
            ReadOnlySpan<byte> sourceBlue,
            ReadOnlySpan<byte> sourceRed,
            in Av1EncoderInterPredictionWorkspace<byte> interWorkspace,
            in Av1MotionVectorCosts motionVectorCosts,
            Point blockOrigin,
            Av1BlockSize blockSize,
            Av1ReferenceFrameType primaryReference,
            Av1ReferenceFrameType secondaryReference,
            Av1PredictionMode mode,
            int referenceIndex,
            Av1ReferenceMotionVectors referenceMotionVectors,
            Span<byte> mask,
            bool wedge,
            ref Av1MotionVector primary,
            ref Av1MotionVector secondary)
        {
            if (!this.byteWorker.IsEmpty)
            {
                this.byteWorker[0].RefineCompoundVectors(
                workspaceStorage,
                MemoryMarshal.Cast<byte, byte>(sourceLuma),
                MemoryMarshal.Cast<byte, byte>(sourceBlue),
                MemoryMarshal.Cast<byte, byte>(sourceRed),
                interWorkspace.Cast<byte>(),
                in motionVectorCosts,
                blockOrigin,
                blockSize,
                primaryReference,
                secondaryReference,
                mode,
                referenceIndex,
                referenceMotionVectors,
                mask,
                wedge,
                ref primary,
                ref secondary);
                return;
            }

            this.uint16Worker[0].RefineCompoundVectors(
                workspaceStorage,
                MemoryMarshal.Cast<byte, ushort>(sourceLuma),
                MemoryMarshal.Cast<byte, ushort>(sourceBlue),
                MemoryMarshal.Cast<byte, ushort>(sourceRed),
                interWorkspace.Cast<ushort>(),
                in motionVectorCosts,
                blockOrigin,
                blockSize,
                primaryReference,
                secondaryReference,
                mode,
                referenceIndex,
                referenceMotionVectors,
                mask,
                wedge,
                ref primary,
                ref secondary);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.GetCompoundPredictionModelCost" />
        private long GetCompoundPredictionModelCost(
            ReadOnlySpan<byte> sourceLuma,
            ReadOnlySpan<byte> sourceBlue,
            ReadOnlySpan<byte> sourceRed,
            in Av1EncoderInterPredictionWorkspace<byte> interWorkspace,
            Point blockOrigin,
            Av1BlockSize blockSize,
            int syntaxRate,
            out long error,
            out int rate,
            out long distortion)
        {
            if (!this.byteWorker.IsEmpty)
            {
                return this.byteWorker[0].GetCompoundPredictionModelCost(
                MemoryMarshal.Cast<byte, byte>(sourceLuma),
                MemoryMarshal.Cast<byte, byte>(sourceBlue),
                MemoryMarshal.Cast<byte, byte>(sourceRed),
                interWorkspace.Cast<byte>(),
                blockOrigin,
                blockSize,
                syntaxRate,
                out error,
                out rate,
                out distortion);
            }

            return this.uint16Worker[0].GetCompoundPredictionModelCost(
                MemoryMarshal.Cast<byte, ushort>(sourceLuma),
                MemoryMarshal.Cast<byte, ushort>(sourceBlue),
                MemoryMarshal.Cast<byte, ushort>(sourceRed),
                interWorkspace.Cast<ushort>(),
                blockOrigin,
                blockSize,
                syntaxRate,
                out error,
                out rate,
                out distortion);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.EstimateInterPredictionResidual" />
        private long EstimateInterPredictionResidual(
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> transformWorkspace,
            in Av1EncoderInterPredictionWorkspace<byte> interWorkspace,
            in Av1NeighborEdges<byte> transformEdges,
            in Av1NeighborEdges<byte> lumaCoefficientEdges,
            ReadOnlySpan<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            ReadOnlySpan<byte> sourceLuma,
            ReadOnlySpan<byte> sourceBlue,
            ReadOnlySpan<byte> sourceRed,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            int modeRate,
            long residualBound,
            out Av1RateDistortionStatistics residualStatistics)
        {
            if (!this.byteWorker.IsEmpty)
            {
                return this.byteWorker[0].EstimateInterPredictionResidual(
                writer,
                in tables,
                transformCoefficients,
                dequantizedCoefficients,
                transformWorkspace,
                interWorkspace.Cast<byte>(),
                in transformEdges,
                in lumaCoefficientEdges,
                modeInfoGrid,
                modeInfoAllocation,
                MemoryMarshal.Cast<byte, byte>(sourceLuma),
                MemoryMarshal.Cast<byte, byte>(sourceBlue),
                MemoryMarshal.Cast<byte, byte>(sourceRed),
                macroBlock,
                blockOrigin,
                blockSize,
                modeRate,
                residualBound,
                out residualStatistics);
            }

            return this.uint16Worker[0].EstimateInterPredictionResidual(
                writer,
                in tables,
                transformCoefficients,
                dequantizedCoefficients,
                transformWorkspace,
                interWorkspace.Cast<ushort>(),
                in transformEdges,
                in lumaCoefficientEdges,
                modeInfoGrid,
                modeInfoAllocation,
                MemoryMarshal.Cast<byte, ushort>(sourceLuma),
                MemoryMarshal.Cast<byte, ushort>(sourceBlue),
                MemoryMarshal.Cast<byte, ushort>(sourceRed),
                macroBlock,
                blockOrigin,
                blockSize,
                modeRate,
                residualBound,
                out residualStatistics);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.ShouldSearchCompoundTransforms" />
        private bool ShouldSearchCompoundTransforms(int syntaxRate, long predictionError)
        {
            if (!this.byteWorker.IsEmpty)
            {
                return this.byteWorker[0].ShouldSearchCompoundTransforms(syntaxRate, predictionError);
            }

            return this.uint16Worker[0].ShouldSearchCompoundTransforms(syntaxRate, predictionError);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.GetNeighborMotionVector" />
        private Av1MotionVector? GetNeighborMotionVector(
            ReadOnlySpan<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            ReadOnlySpan<Av1EncoderDisplacementVector> displacementVectors,
            Av1MacroBlockD macroBlock,
            Point origin,
            bool above)
        {
            if (!this.byteWorker.IsEmpty)
            {
                return this.byteWorker[0].GetNeighborMotionVector(modeInfoGrid, modeInfoAllocation, displacementVectors, macroBlock, origin, above);
            }

            return this.uint16Worker[0].GetNeighborMotionVector(modeInfoGrid, modeInfoAllocation, displacementVectors, macroBlock, origin, above);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.PrepareEstimatedReference" />
        private void PrepareEstimatedReference(
            in Av1CoefficientTables tables,
            ReadOnlySpan<int> modeInfoGrid,
            ReadOnlySpan<Av1MacroBlockModeInfo> modeInfoAllocation,
            ReadOnlySpan<Av1EncoderDisplacementVector> displacementVectors,
            ReadOnlySpan<Av1EncoderReferenceContext> referenceContexts,
            ReadOnlySpan<byte> sourceLuma,
            ReadOnlySpan<byte> sourceBlue,
            ReadOnlySpan<byte> sourceRed,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            Av1PartitionType partition,
            Av1ReferenceFrameType reference,
            bool measureSad,
            ref Av1ReferenceMotionVectors referenceVectors)
        {
            if (!this.byteWorker.IsEmpty)
            {
                this.byteWorker[0].PrepareEstimatedReference(
                in tables,
                modeInfoGrid,
                modeInfoAllocation,
                displacementVectors,
                referenceContexts,
                MemoryMarshal.Cast<byte, byte>(sourceLuma),
                MemoryMarshal.Cast<byte, byte>(sourceBlue),
                MemoryMarshal.Cast<byte, byte>(sourceRed),
                macroBlock,
                blockOrigin,
                blockSize,
                partition,
                reference,
                measureSad,
                ref referenceVectors);
                return;
            }

            this.uint16Worker[0].PrepareEstimatedReference(
                in tables,
                modeInfoGrid,
                modeInfoAllocation,
                displacementVectors,
                referenceContexts,
                MemoryMarshal.Cast<byte, ushort>(sourceLuma),
                MemoryMarshal.Cast<byte, ushort>(sourceBlue),
                MemoryMarshal.Cast<byte, ushort>(sourceRed),
                macroBlock,
                blockOrigin,
                blockSize,
                partition,
                reference,
                measureSad,
                ref referenceVectors);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.SetEstimatedColorSensitivity" />
        private void SetEstimatedColorSensitivity(
            ReadOnlySpan<byte> sourceLuma,
            ReadOnlySpan<byte> sourceBlue,
            ReadOnlySpan<byte> sourceRed,
            Point origin,
            Av1BlockSize blockSize,
            int lumaSad,
            bool forceCheck,
            Span<byte> colorSensitivity)
        {
            if (!this.byteWorker.IsEmpty)
            {
                this.byteWorker[0].SetEstimatedColorSensitivity(
                MemoryMarshal.Cast<byte, byte>(sourceLuma),
                MemoryMarshal.Cast<byte, byte>(sourceBlue),
                MemoryMarshal.Cast<byte, byte>(sourceRed),
                origin,
                blockSize,
                lumaSad,
                forceCheck,
                colorSensitivity);
                return;
            }

            this.uint16Worker[0].SetEstimatedColorSensitivity(
                MemoryMarshal.Cast<byte, ushort>(sourceLuma),
                MemoryMarshal.Cast<byte, ushort>(sourceBlue),
                MemoryMarshal.Cast<byte, ushort>(sourceRed),
                origin,
                blockSize,
                lumaSad,
                forceCheck,
                colorSensitivity);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.GetEstimatedFilterSearchPolicy" />
        private int GetEstimatedFilterSearchPolicy(
            ReadOnlySpan<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            bool boostedSegment,
            out Av1InterpolationFilter filter)
        {
            if (!this.byteWorker.IsEmpty)
            {
                return this.byteWorker[0].GetEstimatedFilterSearchPolicy(
                modeInfoGrid,
                modeInfoAllocation,
                macroBlock,
                blockOrigin,
                blockSize,
                boostedSegment,
                out filter);
            }

            return this.uint16Worker[0].GetEstimatedFilterSearchPolicy(
                modeInfoGrid,
                modeInfoAllocation,
                macroBlock,
                blockOrigin,
                blockSize,
                boostedSegment,
                out filter);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.EncodeEstimatedInterWinner" />
        private void EncodeEstimatedInterWinner(
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> transformWorkspace,
            in Av1EncoderInterPredictionWorkspace<byte> interWorkspace,
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
            ReadOnlySpan<byte> sourceLuma,
            ReadOnlySpan<byte> sourceBlue,
            ReadOnlySpan<byte> sourceRed,
            Span<byte> reconstructionLuma,
            Span<byte> reconstructionBlue,
            Span<byte> reconstructionRed,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ref Av1EncoderBlockModeInfo modeInfo,
            Av1EncoderBlockStruct block,
            Av1MotionVector vector,
            Av1MotionVector secondaryVector,
            ReadOnlySpan<byte> lumaPrediction)
        {
            if (!this.byteWorker.IsEmpty)
            {
                this.byteWorker[0].EncodeEstimatedInterWinner(
                writer,
                in tables,
                transformCoefficients,
                dequantizedCoefficients,
                transformWorkspace,
                interWorkspace.Cast<byte>(),
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
                MemoryMarshal.Cast<byte, byte>(sourceLuma),
                MemoryMarshal.Cast<byte, byte>(sourceBlue),
                MemoryMarshal.Cast<byte, byte>(sourceRed),
                MemoryMarshal.Cast<byte, byte>(reconstructionLuma),
                MemoryMarshal.Cast<byte, byte>(reconstructionBlue),
                MemoryMarshal.Cast<byte, byte>(reconstructionRed),
                macroBlock,
                blockOrigin,
                ref modeInfo,
                block,
                vector,
                secondaryVector,
                MemoryMarshal.Cast<byte, byte>(lumaPrediction));
                return;
            }

            this.uint16Worker[0].EncodeEstimatedInterWinner(
                writer,
                in tables,
                transformCoefficients,
                dequantizedCoefficients,
                transformWorkspace,
                interWorkspace.Cast<ushort>(),
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
                MemoryMarshal.Cast<byte, ushort>(sourceLuma),
                MemoryMarshal.Cast<byte, ushort>(sourceBlue),
                MemoryMarshal.Cast<byte, ushort>(sourceRed),
                MemoryMarshal.Cast<byte, ushort>(reconstructionLuma),
                MemoryMarshal.Cast<byte, ushort>(reconstructionBlue),
                MemoryMarshal.Cast<byte, ushort>(reconstructionRed),
                macroBlock,
                blockOrigin,
                ref modeInfo,
                block,
                vector,
                secondaryVector,
                MemoryMarshal.Cast<byte, ushort>(lumaPrediction));
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.EncodeSelectedIntraPlane" />
        private void EncodeSelectedIntraPlane(
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            in Av1EncoderModeDecisionWorkspace<byte> modeWorkspace,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> transformWorkspace,
            in Av1NeighborEdges<byte> coefficientEdges,
            ReadOnlySpan<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            Span<int> superblockCoefficients,
            ReadOnlySpan<byte> sourceLuma,
            ReadOnlySpan<byte> sourceBlue,
            ReadOnlySpan<byte> sourceRed,
            Span<byte> reconstructionLuma,
            Span<byte> reconstructionBlue,
            Span<byte> reconstructionRed,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            Av1Plane plane,
            Av1PredictionMode mode,
            Av1TransformSize transformSize,
            int coefficientOffset,
            bool skipResidual,
            ReadOnlySpan<ushort> paletteColors)
        {
            if (!this.byteWorker.IsEmpty)
            {
                this.byteWorker[0].EncodeSelectedIntraPlane(
                writer,
                in tables,
                modeWorkspace.Cast<byte>(),
                transformCoefficients,
                dequantizedCoefficients,
                transformWorkspace,
                in coefficientEdges,
                modeInfoGrid,
                modeInfoAllocation,
                superblockCoefficients,
                MemoryMarshal.Cast<byte, byte>(sourceLuma),
                MemoryMarshal.Cast<byte, byte>(sourceBlue),
                MemoryMarshal.Cast<byte, byte>(sourceRed),
                MemoryMarshal.Cast<byte, byte>(reconstructionLuma),
                MemoryMarshal.Cast<byte, byte>(reconstructionBlue),
                MemoryMarshal.Cast<byte, byte>(reconstructionRed),
                macroBlock,
                blockOrigin,
                blockSize,
                plane,
                mode,
                transformSize,
                coefficientOffset,
                skipResidual,
                paletteColors);
                return;
            }

            this.uint16Worker[0].EncodeSelectedIntraPlane(
                writer,
                in tables,
                modeWorkspace.Cast<ushort>(),
                transformCoefficients,
                dequantizedCoefficients,
                transformWorkspace,
                in coefficientEdges,
                modeInfoGrid,
                modeInfoAllocation,
                superblockCoefficients,
                MemoryMarshal.Cast<byte, ushort>(sourceLuma),
                MemoryMarshal.Cast<byte, ushort>(sourceBlue),
                MemoryMarshal.Cast<byte, ushort>(sourceRed),
                MemoryMarshal.Cast<byte, ushort>(reconstructionLuma),
                MemoryMarshal.Cast<byte, ushort>(reconstructionBlue),
                MemoryMarshal.Cast<byte, ushort>(reconstructionRed),
                macroBlock,
                blockOrigin,
                blockSize,
                plane,
                mode,
                transformSize,
                coefficientOffset,
                skipResidual,
                paletteColors);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.EstimateInterFrameIntraCandidate" />
        private Av1RateDistortionStatistics EstimateInterFrameIntraCandidate(
            Span<int> searchDequantizedCoefficients,
            Span<int> transformWorkspace,
            Span<int> estimationRowCoefficients,
            in Av1EncoderInterPredictionWorkspace<byte> interWorkspace,
            ReadOnlySpan<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            ReadOnlySpan<byte> sourceLuma,
            ReadOnlySpan<byte> sourceBlue,
            ReadOnlySpan<byte> sourceRed,
            Span<byte> reconstructionLuma,
            Span<byte> reconstructionBlue,
            Span<byte> reconstructionRed,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            Av1PredictionMode mode,
            Av1PredictionMode chromaMode,
            Av1TransformSize transformSize,
            bool evaluateBlue,
            bool evaluateRed,
            Span<byte> lumaPrediction)
        {
            if (!this.byteWorker.IsEmpty)
            {
                return this.byteWorker[0].EstimateInterFrameIntraCandidate(
                searchDequantizedCoefficients,
                transformWorkspace,
                estimationRowCoefficients,
                interWorkspace.Cast<byte>(),
                modeInfoGrid,
                modeInfoAllocation,
                MemoryMarshal.Cast<byte, byte>(sourceLuma),
                MemoryMarshal.Cast<byte, byte>(sourceBlue),
                MemoryMarshal.Cast<byte, byte>(sourceRed),
                MemoryMarshal.Cast<byte, byte>(reconstructionLuma),
                MemoryMarshal.Cast<byte, byte>(reconstructionBlue),
                MemoryMarshal.Cast<byte, byte>(reconstructionRed),
                macroBlock,
                blockOrigin,
                blockSize,
                mode,
                chromaMode,
                transformSize,
                evaluateBlue,
                evaluateRed,
                MemoryMarshal.Cast<byte, byte>(lumaPrediction));
            }

            return this.uint16Worker[0].EstimateInterFrameIntraCandidate(
                searchDequantizedCoefficients,
                transformWorkspace,
                estimationRowCoefficients,
                interWorkspace.Cast<ushort>(),
                modeInfoGrid,
                modeInfoAllocation,
                MemoryMarshal.Cast<byte, ushort>(sourceLuma),
                MemoryMarshal.Cast<byte, ushort>(sourceBlue),
                MemoryMarshal.Cast<byte, ushort>(sourceRed),
                MemoryMarshal.Cast<byte, ushort>(reconstructionLuma),
                MemoryMarshal.Cast<byte, ushort>(reconstructionBlue),
                MemoryMarshal.Cast<byte, ushort>(reconstructionRed),
                macroBlock,
                blockOrigin,
                blockSize,
                mode,
                chromaMode,
                transformSize,
                evaluateBlue,
                evaluateRed,
                MemoryMarshal.Cast<byte, ushort>(lumaPrediction));
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.RecordLumaPrediction" />
        private void RecordLumaPrediction(in Av1EncoderInterPredictionWorkspace<byte> interWorkspace, ReadOnlySpan<byte> prediction)
        {
            if (!this.byteWorker.IsEmpty)
            {
                this.byteWorker[0].RecordLumaPrediction(interWorkspace.Cast<byte>(), MemoryMarshal.Cast<byte, byte>(prediction));
                return;
            }

            this.uint16Worker[0].RecordLumaPrediction(interWorkspace.Cast<ushort>(), MemoryMarshal.Cast<byte, ushort>(prediction));
        }

        /// <summary>Gets the reference edge storage in the active sample width.</summary>
        /// <param name="workspace">The existing mode workspace.</param>
        /// <param name="index">The native reference storage slot.</param>
        /// <returns>The same storage, measured in bytes.</returns>
        private Span<byte> GetReferenceSamples(in Av1EncoderModeDecisionWorkspace<byte> workspace, int index)
            => !this.byteWorker.IsEmpty
                ? workspace.GetReferenceSamples(index)
                : MemoryMarshal.AsBytes(workspace.Cast<ushort>().GetReferenceSamples(index));

        /// <summary>Gets a reconstruction block view without converting or copying its samples.</summary>
        /// <param name="samples">The reconstruction plane storage, measured in bytes.</param>
        /// <param name="plane">The component plane.</param>
        /// <param name="origin">The block origin in plane samples.</param>
        /// <returns>The same storage from the requested block origin, measured in bytes.</returns>
        private Span<byte> GetReconstructionBlock(Span<byte> samples, Av1Plane plane, Point origin)
            => !this.byteWorker.IsEmpty
                ? this.byteWorker[0].GetReconstructionBlock(samples, plane, origin)
                : MemoryMarshal.AsBytes(this.uint16Worker[0].GetReconstructionBlock(MemoryMarshal.Cast<byte, ushort>(samples), plane, origin));

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.GetReconstructionStride" />
        private int GetReconstructionStride(Av1Plane plane)
        {
            if (!this.byteWorker.IsEmpty)
            {
                return this.byteWorker[0].GetReconstructionStride(plane);
            }

            return this.uint16Worker[0].GetReconstructionStride(plane);
        }

        /// <summary>
        /// Builds the directional-mode skip mask that the gradient histogram pruning level of the speed settings selects.
        /// </summary>
        /// <param name="modeWorkspace">The mode decision buffers, which hold the gradient cache of the superblock.</param>
        /// <param name="sourceLuma">The samples of the complete source luma plane, read once per frame pass.</param>
        /// <param name="sourceBlue">The samples of the complete source blue-difference plane, read once per frame pass.</param>
        /// <param name="sourceRed">The samples of the complete source red-difference plane, read once per frame pass.</param>
        /// <param name="blockOrigin">The block origin in luma samples.</param>
        /// <param name="visibleHeight">The number of source rows inside the coded image.</param>
        /// <param name="visibleWidth">The number of source columns inside the coded image.</param>
        /// <returns>The bit mask for the eight directional modes, or zero when HOG pruning is disabled.</returns>
        private byte GetDirectionalModeSkipMask(
            in Av1EncoderModeDecisionWorkspace<byte> modeWorkspace,
            ReadOnlySpan<byte> sourceLuma,
            ReadOnlySpan<byte> sourceBlue,
            ReadOnlySpan<byte> sourceRed,
            Point blockOrigin,
            int visibleHeight,
            int visibleWidth)
        {
            if (!this.byteWorker.IsEmpty)
            {
                return this.byteWorker[0].GetDirectionalModeSkipMask(
                modeWorkspace.Cast<byte>(),
                MemoryMarshal.Cast<byte, byte>(sourceLuma),
                MemoryMarshal.Cast<byte, byte>(sourceBlue),
                MemoryMarshal.Cast<byte, byte>(sourceRed),
                blockOrigin,
                visibleHeight,
                visibleWidth);
            }

            return this.uint16Worker[0].GetDirectionalModeSkipMask(
                modeWorkspace.Cast<ushort>(),
                MemoryMarshal.Cast<byte, ushort>(sourceLuma),
                MemoryMarshal.Cast<byte, ushort>(sourceBlue),
                MemoryMarshal.Cast<byte, ushort>(sourceRed),
                blockOrigin,
                visibleHeight,
                visibleWidth);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.GetLumaModelCost" />
        private long GetLumaModelCost(
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            in LumaModelInputs inputs,
            Av1PredictionMode mode,
            int angleDelta,
            Av1FilterIntraMode filterMode)
        {
            if (!this.byteWorker.IsEmpty)
            {
                return this.byteWorker[0].GetLumaModelCost(macroBlock, blockOrigin, blockSize, in inputs, mode, angleDelta, filterMode);
            }

            return this.uint16Worker[0].GetLumaModelCost(macroBlock, blockOrigin, blockSize, in inputs, mode, angleDelta, filterMode);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.GetUniformLumaCandidateCost" />
        private Av1RateDistortionStatistics GetUniformLumaCandidateCost(
            ReadOnlySpan<byte> sourceLuma,
            ReadOnlySpan<byte> sourceBlue,
            ReadOnlySpan<byte> sourceRed,
            Span<byte> reconstructionLuma,
            Span<byte> reconstructionBlue,
            Span<byte> reconstructionRed,
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            in Av1EncoderModeDecisionWorkspace<byte> modeWorkspace,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> transformWorkspace,
            ReadOnlySpan<int> transformTypeProbabilities,
            in Av1NeighborEdges<byte> lumaCoefficientEdges,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            in LumaBlockState blockState,
            Av1TransformSize transformSize,
            int sourceVariance,
            Av1PredictionMode mode,
            int angleDelta,
            Av1FilterIntraMode filterIntraMode,
            int paletteSize,
            scoped ReadOnlySpan<ushort> paletteColors,
            int paletteHeaderRate,
            int paletteDisabledCost,
            int transformSizeContext,
            long costLimit,
            Span<Av1EncoderTransformBlockState> candidateTransformBlocks,
            out bool skipSmallerTransforms)
        {
            if (!this.byteWorker.IsEmpty)
            {
                return this.byteWorker[0].GetUniformLumaCandidateCost(
                MemoryMarshal.Cast<byte, byte>(sourceLuma),
                MemoryMarshal.Cast<byte, byte>(sourceBlue),
                MemoryMarshal.Cast<byte, byte>(sourceRed),
                MemoryMarshal.Cast<byte, byte>(reconstructionLuma),
                MemoryMarshal.Cast<byte, byte>(reconstructionBlue),
                MemoryMarshal.Cast<byte, byte>(reconstructionRed),
                writer,
                in tables,
                modeWorkspace.Cast<byte>(),
                transformCoefficients,
                dequantizedCoefficients,
                transformWorkspace,
                transformTypeProbabilities,
                in lumaCoefficientEdges,
                macroBlock,
                blockOrigin,
                blockSize,
                in blockState,
                transformSize,
                sourceVariance,
                mode,
                angleDelta,
                filterIntraMode,
                paletteSize,
                paletteColors,
                paletteHeaderRate,
                paletteDisabledCost,
                transformSizeContext,
                costLimit,
                candidateTransformBlocks,
                out skipSmallerTransforms);
            }

            return this.uint16Worker[0].GetUniformLumaCandidateCost(
                MemoryMarshal.Cast<byte, ushort>(sourceLuma),
                MemoryMarshal.Cast<byte, ushort>(sourceBlue),
                MemoryMarshal.Cast<byte, ushort>(sourceRed),
                MemoryMarshal.Cast<byte, ushort>(reconstructionLuma),
                MemoryMarshal.Cast<byte, ushort>(reconstructionBlue),
                MemoryMarshal.Cast<byte, ushort>(reconstructionRed),
                writer,
                in tables,
                modeWorkspace.Cast<ushort>(),
                transformCoefficients,
                dequantizedCoefficients,
                transformWorkspace,
                transformTypeProbabilities,
                in lumaCoefficientEdges,
                macroBlock,
                blockOrigin,
                blockSize,
                in blockState,
                transformSize,
                sourceVariance,
                mode,
                angleDelta,
                filterIntraMode,
                paletteSize,
                paletteColors,
                paletteHeaderRate,
                paletteDisabledCost,
                transformSizeContext,
                costLimit,
                candidateTransformBlocks,
                out skipSmallerTransforms);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.GetLumaBlockState" />
        private LumaBlockState GetLumaBlockState(
            in Av1CoefficientTables tables,
            ReadOnlySpan<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize)
        {
            if (!this.byteWorker.IsEmpty)
            {
                return this.byteWorker[0].GetLumaBlockState(in tables, modeInfoGrid, modeInfoAllocation, macroBlock, blockOrigin, blockSize);
            }

            return this.uint16Worker[0].GetLumaBlockState(in tables, modeInfoGrid, modeInfoAllocation, macroBlock, blockOrigin, blockSize);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.GetVarianceStatistics" />
        private void GetVarianceStatistics(
            ReadOnlySpan<byte> sourceLuma,
            ReadOnlySpan<byte> sourceBlue,
            ReadOnlySpan<byte> sourceRed,
            Point blockOrigin,
            Av1BlockSize blockSize,
            ReadOnlySpan<byte> samples,
            int stride,
            out long sourceVariance,
            out long sampleVariance)
        {
            if (!this.byteWorker.IsEmpty)
            {
                this.byteWorker[0].GetVarianceStatistics(
                MemoryMarshal.Cast<byte, byte>(sourceLuma),
                MemoryMarshal.Cast<byte, byte>(sourceBlue),
                MemoryMarshal.Cast<byte, byte>(sourceRed),
                blockOrigin,
                blockSize,
                MemoryMarshal.Cast<byte, byte>(samples),
                stride,
                out sourceVariance,
                out sampleVariance);
                return;
            }

            this.uint16Worker[0].GetVarianceStatistics(
                MemoryMarshal.Cast<byte, ushort>(sourceLuma),
                MemoryMarshal.Cast<byte, ushort>(sourceBlue),
                MemoryMarshal.Cast<byte, ushort>(sourceRed),
                blockOrigin,
                blockSize,
                MemoryMarshal.Cast<byte, ushort>(samples),
                stride,
                out sourceVariance,
                out sampleVariance);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.GetIntraSmoothingOffset" />
        private long GetIntraSmoothingOffset(
            ReadOnlySpan<byte> sourceLuma,
            ReadOnlySpan<byte> sourceBlue,
            ReadOnlySpan<byte> sourceRed,
            Point blockOrigin,
            Av1BlockSize blockSize,
            ReadOnlySpan<byte> samples,
            int stride)
        {
            if (!this.byteWorker.IsEmpty)
            {
                return this.byteWorker[0].GetIntraSmoothingOffset(
                MemoryMarshal.Cast<byte, byte>(sourceLuma),
                MemoryMarshal.Cast<byte, byte>(sourceBlue),
                MemoryMarshal.Cast<byte, byte>(sourceRed),
                blockOrigin,
                blockSize,
                MemoryMarshal.Cast<byte, byte>(samples),
                stride);
            }

            return this.uint16Worker[0].GetIntraSmoothingOffset(
                MemoryMarshal.Cast<byte, ushort>(sourceLuma),
                MemoryMarshal.Cast<byte, ushort>(sourceBlue),
                MemoryMarshal.Cast<byte, ushort>(sourceRed),
                blockOrigin,
                blockSize,
                MemoryMarshal.Cast<byte, ushort>(samples),
                stride);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.ChargeTextureLossCost" />
        private long ChargeTextureLossCost(
            ReadOnlySpan<byte> sourceLuma,
            ReadOnlySpan<byte> sourceBlue,
            ReadOnlySpan<byte> sourceRed,
            long cost,
            long offset,
            Point blockOrigin,
            Av1BlockSize blockSize,
            bool isInter,
            bool isCompound,
            bool smoothIntra)
        {
            if (!this.byteWorker.IsEmpty)
            {
                return this.byteWorker[0].ChargeTextureLossCost(
                MemoryMarshal.Cast<byte, byte>(sourceLuma),
                MemoryMarshal.Cast<byte, byte>(sourceBlue),
                MemoryMarshal.Cast<byte, byte>(sourceRed),
                cost,
                offset,
                blockOrigin,
                blockSize,
                isInter,
                isCompound,
                smoothIntra);
            }

            return this.uint16Worker[0].ChargeTextureLossCost(
                MemoryMarshal.Cast<byte, ushort>(sourceLuma),
                MemoryMarshal.Cast<byte, ushort>(sourceBlue),
                MemoryMarshal.Cast<byte, ushort>(sourceRed),
                cost,
                offset,
                blockOrigin,
                blockSize,
                isInter,
                isCompound,
                smoothIntra);
        }

        /// <summary>Encodes one complete intra trial with the existing native sample and SIMD operators.</summary>
        /// <param name="candidate">The shared plane settings and borrowed native storage.</param>
        /// <param name="mode">The intra prediction mode.</param>
        /// <param name="angleDelta">The directional angle adjustment.</param>
        /// <param name="transformType">The transform type.</param>
        /// <param name="state">The encoded transform state.</param>
        /// <param name="sse">The uncoded residual energy.</param>
        /// <returns>The candidate distortion.</returns>
        private long EncodeIntraLossyCandidate(
            in Av1IntraCandidatePlane candidate,
            Av1PredictionMode mode,
            int angleDelta,
            Av1TransformType transformType,
            ref Av1EncoderTransformBlockState state,
            out long sse)

            => !this.byteWorker.IsEmpty
                ? Av1TransformBlockEncoder.EncodeIntraLossyCandidate<byte, ByteOperator>(in candidate, mode, angleDelta, transformType, ref state, out sse)
                : Av1TransformBlockEncoder.EncodeIntraLossyCandidate<ushort, UInt16Operator>(in candidate, mode, angleDelta, transformType, ref state, out sse);

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.PrepareChromaCandidatePlanes" />
        private void PrepareChromaCandidatePlanes(
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            Av1EncoderModeDecisionWorkspace<byte> modeWorkspace,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> transformWorkspace,
            Span<short> blockResidual,
            in Av1NeighborEdges<byte> blueCoefficientEdges,
            in Av1NeighborEdges<byte> redCoefficientEdges,
            ReadOnlySpan<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            ReadOnlySpan<byte> sourceBlue,
            ReadOnlySpan<byte> sourceRed,
            Span<byte> reconstructionBlue,
            Span<byte> reconstructionRed,
            Av1MacroBlockD macroBlock,
            Point lumaOrigin,
            Point chromaOrigin,
            Av1BlockSize blockSize,
            Av1TransformSize transformSize,
            bool hasLeft,
            bool hasAbove,
            bool hasTopRight,
            bool hasBottomLeft,
            out Av1IntraCandidatePlane bluePlane,
            out Av1IntraCandidatePlane redPlane)
        {
            if (!this.byteWorker.IsEmpty)
            {
                this.byteWorker[0].PrepareChromaCandidatePlanes(
                writer,
                in tables,
                modeWorkspace.Cast<byte>(),
                transformCoefficients,
                dequantizedCoefficients,
                transformWorkspace,
                blockResidual,
                in blueCoefficientEdges,
                in redCoefficientEdges,
                modeInfoGrid,
                modeInfoAllocation,
                MemoryMarshal.Cast<byte, byte>(sourceBlue),
                MemoryMarshal.Cast<byte, byte>(sourceRed),
                MemoryMarshal.Cast<byte, byte>(reconstructionBlue),
                MemoryMarshal.Cast<byte, byte>(reconstructionRed),
                macroBlock,
                lumaOrigin,
                chromaOrigin,
                blockSize,
                transformSize,
                hasLeft,
                hasAbove,
                hasTopRight,
                hasBottomLeft,
                out bluePlane,
                out redPlane);
                return;
            }

            this.uint16Worker[0].PrepareChromaCandidatePlanes(
                writer,
                in tables,
                modeWorkspace.Cast<ushort>(),
                transformCoefficients,
                dequantizedCoefficients,
                transformWorkspace,
                blockResidual,
                in blueCoefficientEdges,
                in redCoefficientEdges,
                modeInfoGrid,
                modeInfoAllocation,
                MemoryMarshal.Cast<byte, ushort>(sourceBlue),
                MemoryMarshal.Cast<byte, ushort>(sourceRed),
                MemoryMarshal.Cast<byte, ushort>(reconstructionBlue),
                MemoryMarshal.Cast<byte, ushort>(reconstructionRed),
                macroBlock,
                lumaOrigin,
                chromaOrigin,
                blockSize,
                transformSize,
                hasLeft,
                hasAbove,
                hasTopRight,
                hasBottomLeft,
                out bluePlane,
                out redPlane);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.PrepareChromaFromLumaCandidates" />
        private void PrepareChromaFromLumaCandidates(
            ReadOnlySpan<byte> reconstructionLuma,
            Span<short> lumaQ3,
            Point origin,
            Av1TransformSize transformSize,
            Size extent,
            bool subsamplingX,
            bool subsamplingY,
            in Av1IntraCandidatePlane blue,
            in Av1IntraCandidatePlane red,
            out int blueDc,
            out int redDc)
        {
            if (!this.byteWorker.IsEmpty)
            {
                this.byteWorker[0].PrepareChromaFromLumaCandidates(
                MemoryMarshal.Cast<byte, byte>(reconstructionLuma),
                lumaQ3,
                origin,
                transformSize,
                extent,
                subsamplingX,
                subsamplingY,
                in blue,
                in red,
                out blueDc,
                out redDc);
                return;
            }

            this.uint16Worker[0].PrepareChromaFromLumaCandidates(
                MemoryMarshal.Cast<byte, ushort>(reconstructionLuma),
                lumaQ3,
                origin,
                transformSize,
                extent,
                subsamplingX,
                subsamplingY,
                in blue,
                in red,
                out blueDc,
                out redDc);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.GetPlaneSourceVariance" />
        private int GetPlaneSourceVariance(Av1Plane plane, ReadOnlySpan<byte> samples, Point origin, int width, int height)
        {
            if (!this.byteWorker.IsEmpty)
            {
                return this.byteWorker[0].GetPlaneSourceVariance(plane, MemoryMarshal.Cast<byte, byte>(samples), origin, width, height);
            }

            return this.uint16Worker[0].GetPlaneSourceVariance(plane, MemoryMarshal.Cast<byte, ushort>(samples), origin, width, height);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.GetChromaFromLumaPlaneCost" />
        private long GetChromaFromLumaPlaneCost(
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> searchDequantizedCoefficients,
            Span<int> transformWorkspace,
            ReadOnlySpan<int> transformTypeProbabilities,
            Span<short> residual,
            Span<int> searchCoefficients,
            Span<int> searchReconstructions,
            Av1PredictionMode lumaMode,
            Av1Plane plane,
            Point chromaOrigin,
            Av1TransformSize transformSize,
            ReadOnlySpan<byte> sourceSamples,
            int dc,
            Av1TransformBlockContext context,
            ReadOnlySpan<short> lumaQ3,
            int alphaQ3,
            Span<byte> prediction,
            int predictionStride,
            Span<int> coefficients,
            ref Av1EncoderTransformBlockState state,
            out int rate)
        {
            if (!this.byteWorker.IsEmpty)
            {
                return this.byteWorker[0].GetChromaFromLumaPlaneCost(
                writer,
                in tables,
                transformCoefficients,
                dequantizedCoefficients,
                searchDequantizedCoefficients,
                transformWorkspace,
                transformTypeProbabilities,
                residual,
                searchCoefficients,
                searchReconstructions,
                lumaMode,
                plane,
                chromaOrigin,
                transformSize,
                MemoryMarshal.Cast<byte, byte>(sourceSamples),
                dc,
                context,
                lumaQ3,
                alphaQ3,
                MemoryMarshal.Cast<byte, byte>(prediction),
                predictionStride,
                coefficients,
                ref state,
                out rate);
            }

            return this.uint16Worker[0].GetChromaFromLumaPlaneCost(
                writer,
                in tables,
                transformCoefficients,
                dequantizedCoefficients,
                searchDequantizedCoefficients,
                transformWorkspace,
                transformTypeProbabilities,
                residual,
                searchCoefficients,
                searchReconstructions,
                lumaMode,
                plane,
                chromaOrigin,
                transformSize,
                MemoryMarshal.Cast<byte, ushort>(sourceSamples),
                dc,
                context,
                lumaQ3,
                alphaQ3,
                MemoryMarshal.Cast<byte, ushort>(prediction),
                predictionStride,
                coefficients,
                ref state,
                out rate);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.FindBestChromaFromLumaEstimate" />
        private int FindBestChromaFromLumaEstimate(
            Av1EncoderBlockWorkspace blockWorkspace,
            Av1Plane plane,
            ReadOnlySpan<byte> sourceSamples,
            Point chromaOrigin,
            int dc,
            ReadOnlySpan<short> lumaQ3,
            Av1TransformSize transformSize,
            Av1BitDepth bitDepth,
            Span<byte> frameBlock,
            int frameStride,
            Span<short> residual,
            Span<int> transformCoefficients,
            Span<int> transformWorkspace)
        {
            if (!this.byteWorker.IsEmpty)
            {
                return this.byteWorker[0].FindBestChromaFromLumaEstimate(
                blockWorkspace,
                plane,
                MemoryMarshal.Cast<byte, byte>(sourceSamples),
                chromaOrigin,
                dc,
                lumaQ3,
                transformSize,
                bitDepth,
                MemoryMarshal.Cast<byte, byte>(frameBlock),
                frameStride,
                residual,
                transformCoefficients,
                transformWorkspace);
            }

            return this.uint16Worker[0].FindBestChromaFromLumaEstimate(
                blockWorkspace,
                plane,
                MemoryMarshal.Cast<byte, ushort>(sourceSamples),
                chromaOrigin,
                dc,
                lumaQ3,
                transformSize,
                bitDepth,
                MemoryMarshal.Cast<byte, ushort>(frameBlock),
                frameStride,
                residual,
                transformCoefficients,
                transformWorkspace);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.GetBlockDirectionalModeSkipMask" />
        private byte GetBlockDirectionalModeSkipMask(
            in Av1EncoderModeDecisionWorkspace<byte> modeWorkspace,
            Av1PlaneType planeType,
            ReadOnlySpan<byte> sourceSamples,
            Point origin,
            int rows,
            int columns,
            int histogramScale,
            float threshold)
        {
            if (!this.byteWorker.IsEmpty)
            {
                return this.byteWorker[0].GetBlockDirectionalModeSkipMask(
                modeWorkspace.Cast<byte>(),
                planeType,
                MemoryMarshal.Cast<byte, byte>(sourceSamples),
                origin,
                rows,
                columns,
                histogramScale,
                threshold);
            }

            return this.uint16Worker[0].GetBlockDirectionalModeSkipMask(
                modeWorkspace.Cast<ushort>(),
                planeType,
                MemoryMarshal.Cast<byte, ushort>(sourceSamples),
                origin,
                rows,
                columns,
                histogramScale,
                threshold);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.GetChromaFromLumaExtent" />
        private Size GetChromaFromLumaExtent(
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            Av1TransformSize lumaTransformSize,
            int subsamplingX,
            int subsamplingY)
        {
            if (!this.byteWorker.IsEmpty)
            {
                return this.byteWorker[0].GetChromaFromLumaExtent(
                modeInfoAllocation,
                macroBlock,
                blockOrigin,
                blockSize,
                lumaTransformSize,
                subsamplingX,
                subsamplingY);
            }

            return this.uint16Worker[0].GetChromaFromLumaExtent(
                modeInfoAllocation,
                macroBlock,
                blockOrigin,
                blockSize,
                lumaTransformSize,
                subsamplingX,
                subsamplingY);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.GetTiledPlaneCost" />
        private long GetTiledPlaneCost(
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            in Av1EncoderModeDecisionWorkspace<byte> modeWorkspace,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> searchDequantizedCoefficients,
            Span<int> transformWorkspace,
            ReadOnlySpan<int> transformTypeProbabilities,
            Span<int> searchCoefficients,
            Span<int> searchReconstructions,
            ReadOnlySpan<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
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
            ReadOnlySpan<byte> sourceSamples,
            Span<byte> reconstructionSamples,
            ReadOnlySpan<ushort> paletteColors,
            Av1PlaneRegion<byte> colorIndexMap,
            Span<int> candidateCoefficients,
            Span<Av1EncoderTransformBlockState> candidateStates,
            Span<byte> topContexts,
            Span<byte> leftContexts,
            long costLimit,
            out long predictionDistortion,
            out int rate)
        {
            if (!this.byteWorker.IsEmpty)
            {
                return this.byteWorker[0].GetTiledPlaneCost(
                writer,
                in tables,
                modeWorkspace.Cast<byte>(),
                transformCoefficients,
                dequantizedCoefficients,
                searchDequantizedCoefficients,
                transformWorkspace,
                transformTypeProbabilities,
                searchCoefficients,
                searchReconstructions,
                modeInfoGrid,
                modeInfoAllocation,
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
                plane,
                MemoryMarshal.Cast<byte, byte>(sourceSamples),
                MemoryMarshal.Cast<byte, byte>(reconstructionSamples),
                paletteColors,
                colorIndexMap,
                candidateCoefficients,
                candidateStates,
                topContexts,
                leftContexts,
                costLimit,
                out predictionDistortion,
                out rate);
            }

            return this.uint16Worker[0].GetTiledPlaneCost(
                writer,
                in tables,
                modeWorkspace.Cast<ushort>(),
                transformCoefficients,
                dequantizedCoefficients,
                searchDequantizedCoefficients,
                transformWorkspace,
                transformTypeProbabilities,
                searchCoefficients,
                searchReconstructions,
                modeInfoGrid,
                modeInfoAllocation,
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
                plane,
                MemoryMarshal.Cast<byte, ushort>(sourceSamples),
                MemoryMarshal.Cast<byte, ushort>(reconstructionSamples),
                paletteColors,
                colorIndexMap,
                candidateCoefficients,
                candidateStates,
                topContexts,
                leftContexts,
                costLimit,
                out predictionDistortion,
                out rate);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.CountPlanePaletteColors" />
        private int CountPlanePaletteColors(
            Av1Plane plane,
            ReadOnlySpan<byte> samples,
            Point origin,
            int rows,
            int columns,
            Span<int> counts,
            out int bins,
            out short minimum,
            out short maximum)
        {
            if (!this.byteWorker.IsEmpty)
            {
                return this.byteWorker[0].CountPlanePaletteColors(
                plane,
                MemoryMarshal.Cast<byte, byte>(samples),
                origin,
                rows,
                columns,
                counts,
                out bins,
                out minimum,
                out maximum);
            }

            return this.uint16Worker[0].CountPlanePaletteColors(
                plane,
                MemoryMarshal.Cast<byte, ushort>(samples),
                origin,
                rows,
                columns,
                counts,
                out bins,
                out minimum,
                out maximum);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.CopyPlanePaletteSamples" />
        private void CopyPlanePaletteSamples(Av1Plane plane, ReadOnlySpan<byte> samples, Point origin, int rows, int columns, Span<short> destination)
        {
            if (!this.byteWorker.IsEmpty)
            {
                this.byteWorker[0].CopyPlanePaletteSamples(plane, MemoryMarshal.Cast<byte, byte>(samples), origin, rows, columns, destination);
                return;
            }

            this.uint16Worker[0].CopyPlanePaletteSamples(plane, MemoryMarshal.Cast<byte, ushort>(samples), origin, rows, columns, destination);
        }

        /// <summary>
        /// Reconstructs the candidate most recently quantized into the workspace and measures its distortion.
        /// </summary>
        /// <remarks>
        /// Quantization and reconstruction are separate so that a transform search can compare the coefficient rate
        /// with its current winner first. A candidate whose rate alone already costs more cannot win, and then needs
        /// no inverse transform or pixel comparison.
        /// </remarks>
        /// <param name="workspace">The workspace supplying the visible extent of the plane.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the inverse transform.</param>
        /// <param name="dequantized">The dequantized coefficients of the candidate.</param>
        /// <param name="source">The source transform block, from its top-left sample.</param>
        /// <param name="sourceStride">The number of samples between rows of <paramref name="source"/>.</param>
        /// <param name="blockOrigin">The transform origin in plane samples, which gives the visible extent.</param>
        /// <param name="prediction">The prepared prediction surface.</param>
        /// <param name="inputStride">The number of prediction samples between rows.</param>
        /// <param name="reconstruction">
        /// The candidate reconstruction. When it starts at the first prediction sample, the candidate is reconstructed in place over its prediction.
        /// </param>
        /// <param name="reconstructionStride">The number of reconstruction samples between rows.</param>
        /// <param name="transformSize">The candidate transform dimensions.</param>
        /// <param name="plane">The component plane containing the block.</param>
        /// <param name="lossless">Whether the segment of the block codes losslessly, which selects the reversible inverse transform.</param>
        /// <param name="bitDepth">The coded sample precision.</param>
        /// <param name="state">The candidate transform type and end-of-block syntax.</param>
        /// <returns>The normalized pixel-domain distortion in AV1 transform units.</returns>
        private long ReconstructPredictionLossyCandidate(
            Av1EncoderBlockWorkspace workspace,
            Span<int> transformWorkspace,
            ReadOnlySpan<int> dequantized,
            ReadOnlySpan<byte> source,
            int sourceStride,
            Point blockOrigin,
            ReadOnlySpan<byte> prediction,
            int inputStride,
            Span<byte> reconstruction,
            int reconstructionStride,
            Av1TransformSize transformSize,
            Av1Plane plane,
            bool lossless,
            Av1BitDepth bitDepth,
            Av1EncoderTransformBlockState state)
        {
            if (!this.byteWorker.IsEmpty)
            {
                return Av1TransformBlockEncoder.ReconstructPredictionLossyCandidate<byte, ByteOperator>(
                workspace,
                transformWorkspace,
                dequantized,
                MemoryMarshal.Cast<byte, byte>(source),
                sourceStride,
                blockOrigin,
                MemoryMarshal.Cast<byte, byte>(prediction),
                inputStride,
                MemoryMarshal.Cast<byte, byte>(reconstruction),
                reconstructionStride,
                transformSize,
                plane,
                lossless,
                bitDepth,
                state);
            }

            return Av1TransformBlockEncoder.ReconstructPredictionLossyCandidate<ushort, UInt16Operator>(
                workspace,
                transformWorkspace,
                dequantized,
                MemoryMarshal.Cast<byte, ushort>(source),
                sourceStride,
                blockOrigin,
                MemoryMarshal.Cast<byte, ushort>(prediction),
                inputStride,
                MemoryMarshal.Cast<byte, ushort>(reconstruction),
                reconstructionStride,
                transformSize,
                plane,
                lossless,
                bitDepth,
                state);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.EstimateIntraMode" />
        private bool EstimateIntraMode(
            in LumaModelInputs inputs,
            Span<int> estimationRowCoefficients,
            Span<int> searchDequantizedCoefficients,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            Size codedExtent,
            Av1TransformSize transformSize,
            Av1TransformSize estimationSize,
            Av1PredictionMode mode,
            bool pruneSad,
            ref uint bestSad,
            out int rate,
            out long distortion,
            out bool skip)
        {
            if (!this.byteWorker.IsEmpty)
            {
                return this.byteWorker[0].EstimateIntraMode(
                in inputs,
                estimationRowCoefficients,
                searchDequantizedCoefficients,
                macroBlock,
                blockOrigin,
                blockSize,
                codedExtent,
                transformSize,
                estimationSize,
                mode,
                pruneSad,
                ref bestSad,
                out rate,
                out distortion,
                out skip);
            }

            return this.uint16Worker[0].EstimateIntraMode(
                in inputs,
                estimationRowCoefficients,
                searchDequantizedCoefficients,
                macroBlock,
                blockOrigin,
                blockSize,
                codedExtent,
                transformSize,
                estimationSize,
                mode,
                pruneSad,
                ref bestSad,
                out rate,
                out distortion,
                out skip);
        }

        /// <summary>Gets a source block view without converting or copying samples.</summary>
        /// <param name="samples">The complete source plane storage, measured in bytes.</param>
        /// <param name="plane">The component plane.</param>
        /// <param name="origin">The block origin in plane samples.</param>
        /// <returns>The same storage from the requested origin, measured in bytes.</returns>
        private ReadOnlySpan<byte> GetSourceBlock(ReadOnlySpan<byte> samples, Av1Plane plane, Point origin)
            => !this.byteWorker.IsEmpty
                ? this.byteWorker[0].GetSourceBlock(samples, plane, origin)
                : MemoryMarshal.AsBytes(this.uint16Worker[0].GetSourceBlock(MemoryMarshal.Cast<byte, ushort>(samples), plane, origin));

        /// <summary>Gets the existing transform prediction workspace in the active sample width.</summary>
        /// <param name="workspace">The existing inter workspace.</param>
        /// <returns>The borrowed transform workspace, measured in bytes.</returns>
        private Span<byte> GetInterTransformSamples(in Av1EncoderInterPredictionWorkspace<byte> workspace)
            => !this.byteWorker.IsEmpty
                ? workspace.TransformReconstruction
                : MemoryMarshal.AsBytes(workspace.Cast<ushort>().TransformReconstruction);

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.GetSourceStride" />
        private int GetSourceStride(Av1Plane plane)
        {
            if (!this.byteWorker.IsEmpty)
            {
                return this.byteWorker[0].GetSourceStride(plane);
            }

            return this.uint16Worker[0].GetSourceStride(plane);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.SubtractSourcePrediction" />
        private void SubtractSourcePrediction(
            Av1Plane plane,
            ReadOnlySpan<byte> source,
            Point origin,
            ReadOnlySpan<byte> prediction,
            int predictionStride,
            Span<short> residual,
            int width,
            int height)
        {
            if (!this.byteWorker.IsEmpty)
            {
                this.byteWorker[0].SubtractSourcePrediction(
                plane,
                MemoryMarshal.Cast<byte, byte>(source),
                origin,
                MemoryMarshal.Cast<byte, byte>(prediction),
                predictionStride,
                residual,
                width,
                height);
                return;
            }

            this.uint16Worker[0].SubtractSourcePrediction(
                plane,
                MemoryMarshal.Cast<byte, ushort>(source),
                origin,
                MemoryMarshal.Cast<byte, ushort>(prediction),
                predictionStride,
                residual,
                width,
                height);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.PrepareInterTransformResidual" />
        private void PrepareInterTransformResidual(
            ReadOnlySpan<byte> source,
            ReadOnlySpan<byte> prediction,
            Span<byte> transformPrediction,
            Span<short> residual,
            Point origin,
            Av1BlockSize blockSize,
            Av1TransformSize transformSize,
            int row,
            int column)
        {
            if (!this.byteWorker.IsEmpty)
            {
                this.byteWorker[0].PrepareInterTransformResidual(
                MemoryMarshal.Cast<byte, byte>(source),
                MemoryMarshal.Cast<byte, byte>(prediction),
                MemoryMarshal.Cast<byte, byte>(transformPrediction),
                residual,
                origin,
                blockSize,
                transformSize,
                row,
                column);
                return;
            }

            this.uint16Worker[0].PrepareInterTransformResidual(
                MemoryMarshal.Cast<byte, ushort>(source),
                MemoryMarshal.Cast<byte, ushort>(prediction),
                MemoryMarshal.Cast<byte, ushort>(transformPrediction),
                residual,
                origin,
                blockSize,
                transformSize,
                row,
                column);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.GetSkipPredictionLevel" />
        private int GetSkipPredictionLevel()
        {
            if (!this.byteWorker.IsEmpty)
            {
                return this.byteWorker[0].GetSkipPredictionLevel();
            }

            return this.uint16Worker[0].GetSkipPredictionLevel();
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.PredictSkipTransform" />
        private bool PredictSkipTransform(
            ReadOnlySpan<byte> sourceLuma,
            ReadOnlySpan<byte> sourceBlue,
            ReadOnlySpan<byte> sourceRed,
            Span<int> transformCoefficients,
            Span<int> transformWorkspace,
            in Av1EncoderInterPredictionWorkspace<byte> interWorkspace,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            int level,
            out long distortion)
        {
            if (!this.byteWorker.IsEmpty)
            {
                return this.byteWorker[0].PredictSkipTransform(
                MemoryMarshal.Cast<byte, byte>(sourceLuma),
                MemoryMarshal.Cast<byte, byte>(sourceBlue),
                MemoryMarshal.Cast<byte, byte>(sourceRed),
                transformCoefficients,
                transformWorkspace,
                interWorkspace.Cast<byte>(),
                macroBlock,
                blockOrigin,
                blockSize,
                level,
                out distortion);
            }

            return this.uint16Worker[0].PredictSkipTransform(
                MemoryMarshal.Cast<byte, ushort>(sourceLuma),
                MemoryMarshal.Cast<byte, ushort>(sourceBlue),
                MemoryMarshal.Cast<byte, ushort>(sourceRed),
                transformCoefficients,
                transformWorkspace,
                interWorkspace.Cast<ushort>(),
                macroBlock,
                blockOrigin,
                blockSize,
                level,
                out distortion);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.RefineInterIntraMotion" />
        private Av1MotionVector RefineInterIntraMotion(
            in Av1EncoderInterPredictionWorkspace<byte> interWorkspace,
            in Av1MotionVectorCosts motionVectorCosts,
            Span<int> workspaceStorage,
            ReadOnlySpan<byte> sourceLuma,
            ReadOnlySpan<byte> sourceBlue,
            ReadOnlySpan<byte> sourceRed,
            Point blockOrigin,
            Av1BlockSize blockSize,
            Av1ReferenceFrameType referenceFrame,
            Av1MotionVector vector,
            Av1MotionVector referenceVector,
            Av1InterpolationFilter horizontalFilter,
            Av1InterpolationFilter verticalFilter,
            ReadOnlySpan<byte> selectedIntra,
            ReadOnlySpan<byte> mask,
            Span<byte> blendedPrediction,
            Span<short> intraResidual)
        {
            if (!this.byteWorker.IsEmpty)
            {
                return this.byteWorker[0].RefineInterIntraMotion(
                interWorkspace.Cast<byte>(),
                in motionVectorCosts,
                workspaceStorage,
                MemoryMarshal.Cast<byte, byte>(sourceLuma),
                MemoryMarshal.Cast<byte, byte>(sourceBlue),
                MemoryMarshal.Cast<byte, byte>(sourceRed),
                blockOrigin,
                blockSize,
                referenceFrame,
                vector,
                referenceVector,
                horizontalFilter,
                verticalFilter,
                MemoryMarshal.Cast<byte, byte>(selectedIntra),
                mask,
                MemoryMarshal.Cast<byte, byte>(blendedPrediction),
                intraResidual);
            }

            return this.uint16Worker[0].RefineInterIntraMotion(
                interWorkspace.Cast<ushort>(),
                in motionVectorCosts,
                workspaceStorage,
                MemoryMarshal.Cast<byte, ushort>(sourceLuma),
                MemoryMarshal.Cast<byte, ushort>(sourceBlue),
                MemoryMarshal.Cast<byte, ushort>(sourceRed),
                blockOrigin,
                blockSize,
                referenceFrame,
                vector,
                referenceVector,
                horizontalFilter,
                verticalFilter,
                MemoryMarshal.Cast<byte, ushort>(selectedIntra),
                mask,
                MemoryMarshal.Cast<byte, ushort>(blendedPrediction),
                intraResidual);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.PrepareInterIntraPlane" />
        private Span<byte> PrepareInterIntraPlane(
            Span<int> transformWorkspace,
            in Av1EncoderInterPredictionWorkspace<byte> interWorkspace,
            Span<byte> transformPrediction,
            Span<byte> interIntraAbove,
            Span<byte> interIntraLeft,
            ReadOnlySpan<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            ReadOnlySpan<byte> sourceLuma,
            ReadOnlySpan<byte> sourceBlue,
            ReadOnlySpan<byte> sourceRed,
            Span<byte> reconstructionLuma,
            Span<byte> reconstructionBlue,
            Span<byte> reconstructionRed,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            Av1Plane plane,
            Av1InterIntraMode interIntraMode)
        {
            if (!this.byteWorker.IsEmpty)
            {
                return this.byteWorker[0].PrepareInterIntraPlane(
                transformWorkspace,
                interWorkspace.Cast<byte>(),
                MemoryMarshal.Cast<byte,
                byte>(transformPrediction),
                MemoryMarshal.Cast<byte,
                byte>(interIntraAbove),
                MemoryMarshal.Cast<byte,
                byte>(interIntraLeft),
                modeInfoGrid,
                modeInfoAllocation,
                MemoryMarshal.Cast<byte,
                byte>(sourceLuma),
                MemoryMarshal.Cast<byte,
                byte>(sourceBlue),
                MemoryMarshal.Cast<byte,
                byte>(sourceRed),
                MemoryMarshal.Cast<byte,
                byte>(reconstructionLuma),
                MemoryMarshal.Cast<byte,
                byte>(reconstructionBlue),
                MemoryMarshal.Cast<byte,
                byte>(reconstructionRed),
                macroBlock,
                blockOrigin,
                blockSize,
                plane,
                interIntraMode);
            }

            return MemoryMarshal.AsBytes(this.uint16Worker[0].PrepareInterIntraPlane(
                transformWorkspace,
                interWorkspace.Cast<ushort>(),
                MemoryMarshal.Cast<byte,
                ushort>(transformPrediction),
                MemoryMarshal.Cast<byte,
                ushort>(interIntraAbove),
                MemoryMarshal.Cast<byte,
                ushort>(interIntraLeft),
                modeInfoGrid,
                modeInfoAllocation,
                MemoryMarshal.Cast<byte,
                ushort>(sourceLuma),
                MemoryMarshal.Cast<byte,
                ushort>(sourceBlue),
                MemoryMarshal.Cast<byte,
                ushort>(sourceRed),
                MemoryMarshal.Cast<byte,
                ushort>(reconstructionLuma),
                MemoryMarshal.Cast<byte,
                ushort>(reconstructionBlue),
                MemoryMarshal.Cast<byte,
                ushort>(reconstructionRed),
                macroBlock,
                blockOrigin,
                blockSize,
                plane,
                interIntraMode));
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.PrepareSingleInterPrediction" />
        private void PrepareSingleInterPrediction(
            ReadOnlySpan<byte> sourceLuma,
            ReadOnlySpan<byte> sourceBlue,
            ReadOnlySpan<byte> sourceRed,
            Av1PredictionMode mode,
            Av1ReferenceFrameType referenceFrame,
            Av1BlockSize blockSize,
            Av1Plane plane,
            Point planeOrigin,
            int subsamplingX,
            int subsamplingY,
            Av1MotionVector vector,
            Av1InterpolationFilter horizontalFilter,
            Av1InterpolationFilter verticalFilter,
            Av1BlockSize predictionSize,
            Span<byte> prediction,
            Span<short> residual,
            Span<short> filterRows)
        {
            if (!this.byteWorker.IsEmpty)
            {
                this.byteWorker[0].PrepareSingleInterPrediction(
                MemoryMarshal.Cast<byte, byte>(sourceLuma),
                MemoryMarshal.Cast<byte, byte>(sourceBlue),
                MemoryMarshal.Cast<byte, byte>(sourceRed),
                mode,
                referenceFrame,
                blockSize,
                plane,
                planeOrigin,
                subsamplingX,
                subsamplingY,
                vector,
                horizontalFilter,
                verticalFilter,
                predictionSize,
                MemoryMarshal.Cast<byte, byte>(prediction),
                residual,
                filterRows);
                return;
            }

            this.uint16Worker[0].PrepareSingleInterPrediction(
                MemoryMarshal.Cast<byte, ushort>(sourceLuma),
                MemoryMarshal.Cast<byte, ushort>(sourceBlue),
                MemoryMarshal.Cast<byte, ushort>(sourceRed),
                mode,
                referenceFrame,
                blockSize,
                plane,
                planeOrigin,
                subsamplingX,
                subsamplingY,
                vector,
                horizontalFilter,
                verticalFilter,
                predictionSize,
                MemoryMarshal.Cast<byte, ushort>(prediction),
                residual,
                filterRows);
        }

        /// <summary>Subtracts one complete native prediction block with the existing SIMD operators.</summary>
        /// <param name="source">The first block, measured in bytes.</param>
        /// <param name="sourceStride">The source row stride in native samples.</param>
        /// <param name="prediction">The second block, measured in bytes.</param>
        /// <param name="predictionStride">The prediction row stride in native samples.</param>
        /// <param name="residual">The signed residual destination.</param>
        /// <param name="width">The block width.</param>
        /// <param name="height">The block height.</param>
        private void SubtractPrediction(
            ReadOnlySpan<byte> source,
            int sourceStride,
            ReadOnlySpan<byte> prediction,
            int predictionStride,
            Span<short> residual,
            int width,
            int height)
        {
            if (!this.byteWorker.IsEmpty)
            {
                ByteOperator.SubtractPrediction(source, sourceStride, prediction, predictionStride, residual, width, height);
                return;
            }

            UInt16Operator.SubtractPrediction(
                MemoryMarshal.Cast<byte, ushort>(source),
                sourceStride,
                MemoryMarshal.Cast<byte, ushort>(prediction),
                predictionStride,
                residual,
                width,
                height);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.SearchEstimatedCandidateMotion" />
        private bool SearchEstimatedCandidateMotion(
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            Span<short> blockResidual,
            in Av1EncoderInterPredictionWorkspace<byte> interWorkspace,
            Span<byte> motionSearchPrediction,
            in Av1MotionVectorCosts motionVectorCosts,
            ReadOnlySpan<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            ReadOnlySpan<byte> sourceLuma,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1EncoderBlockModeInfo modeInfo,
            ref Av1MotionVector vector,
            Av1ReferenceMotionVectors referenceVectors,
            out int motionRate)
        {
            if (!this.byteWorker.IsEmpty)
            {
                return this.byteWorker[0].SearchEstimatedCandidateMotion(
                writer,
                in tables,
                blockResidual,
                interWorkspace.Cast<byte>(),
                MemoryMarshal.Cast<byte, byte>(motionSearchPrediction),
                in motionVectorCosts,
                modeInfoGrid,
                modeInfoAllocation,
                MemoryMarshal.Cast<byte, byte>(sourceLuma),
                macroBlock,
                blockOrigin,
                modeInfo,
                ref vector,
                referenceVectors,
                out motionRate);
            }

            return this.uint16Worker[0].SearchEstimatedCandidateMotion(
                writer,
                in tables,
                blockResidual,
                interWorkspace.Cast<ushort>(),
                MemoryMarshal.Cast<byte, ushort>(motionSearchPrediction),
                in motionVectorCosts,
                modeInfoGrid,
                modeInfoAllocation,
                MemoryMarshal.Cast<byte, ushort>(sourceLuma),
                macroBlock,
                blockOrigin,
                modeInfo,
                ref vector,
                referenceVectors,
                out motionRate);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.EstimateInterResidual" />
        private Av1RateDistortionStatistics EstimateInterResidual(
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            Span<int> searchDequantizedCoefficients,
            Span<int> transformWorkspace,
            Span<int> estimationRowCoefficients,
            in Av1EncoderInterPredictionWorkspace<byte> interWorkspace,
            ReadOnlySpan<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            ReadOnlySpan<byte> sourceLuma,
            ReadOnlySpan<byte> sourceBlue,
            ReadOnlySpan<byte> sourceRed,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            ReadOnlySpan<byte> lumaPrediction,
            ReadOnlySpan<byte> bluePrediction,
            ReadOnlySpan<byte> redPrediction,
            bool evaluateBlue,
            bool evaluateRed,
            Av1TransformSize transformSize,
            bool earlyTermination,
            long lumaSquaredError,
            out long chromaDistortion,
            out bool initialSkip)
        {
            if (!this.byteWorker.IsEmpty)
            {
                return this.byteWorker[0].EstimateInterResidual(
                writer,
                in tables,
                searchDequantizedCoefficients,
                transformWorkspace,
                estimationRowCoefficients,
                interWorkspace.Cast<byte>(),
                modeInfoGrid,
                modeInfoAllocation,
                MemoryMarshal.Cast<byte, byte>(sourceLuma),
                MemoryMarshal.Cast<byte, byte>(sourceBlue),
                MemoryMarshal.Cast<byte, byte>(sourceRed),
                macroBlock,
                blockOrigin,
                blockSize,
                MemoryMarshal.Cast<byte, byte>(lumaPrediction),
                MemoryMarshal.Cast<byte, byte>(bluePrediction),
                MemoryMarshal.Cast<byte, byte>(redPrediction),
                evaluateBlue,
                evaluateRed,
                transformSize,
                earlyTermination,
                lumaSquaredError,
                out chromaDistortion,
                out initialSkip);
            }

            return this.uint16Worker[0].EstimateInterResidual(
                writer,
                in tables,
                searchDequantizedCoefficients,
                transformWorkspace,
                estimationRowCoefficients,
                interWorkspace.Cast<ushort>(),
                modeInfoGrid,
                modeInfoAllocation,
                MemoryMarshal.Cast<byte, ushort>(sourceLuma),
                MemoryMarshal.Cast<byte, ushort>(sourceBlue),
                MemoryMarshal.Cast<byte, ushort>(sourceRed),
                macroBlock,
                blockOrigin,
                blockSize,
                MemoryMarshal.Cast<byte, ushort>(lumaPrediction),
                MemoryMarshal.Cast<byte, ushort>(bluePrediction),
                MemoryMarshal.Cast<byte, ushort>(redPrediction),
                evaluateBlue,
                evaluateRed,
                transformSize,
                earlyTermination,
                lumaSquaredError,
                out chromaDistortion,
                out initialSkip);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.UsesLargeBlockModel" />
        private bool UsesLargeBlockModel(Av1BlockSize blockSize)
        {
            if (!this.byteWorker.IsEmpty)
            {
                return this.byteWorker[0].UsesLargeBlockModel(blockSize);
            }

            return this.uint16Worker[0].UsesLargeBlockModel(blockSize);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.PrepareSelectedChromaDc" />
        private int PrepareSelectedChromaDc(
            in Av1EncoderModeDecisionWorkspace<byte> modeWorkspace,
            ReadOnlySpan<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            Span<byte> reconstructionLuma,
            Span<byte> reconstructionBlue,
            Span<byte> reconstructionRed,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1Plane plane,
            Point origin,
            Av1BlockSize blockSize,
            Av1TransformSize transformSize,
            int subX,
            int subY,
            int width,
            Span<byte> samples)
        {
            if (!this.byteWorker.IsEmpty)
            {
                return this.byteWorker[0].PrepareSelectedChromaDc(
                modeWorkspace.Cast<byte>(),
                modeInfoGrid,
                modeInfoAllocation,
                MemoryMarshal.Cast<byte, byte>(reconstructionLuma),
                MemoryMarshal.Cast<byte, byte>(reconstructionBlue),
                MemoryMarshal.Cast<byte, byte>(reconstructionRed),
                macroBlock,
                blockOrigin,
                plane,
                origin,
                blockSize,
                transformSize,
                subX,
                subY,
                width,
                MemoryMarshal.Cast<byte, byte>(samples));
            }

            return this.uint16Worker[0].PrepareSelectedChromaDc(
                modeWorkspace.Cast<ushort>(),
                modeInfoGrid,
                modeInfoAllocation,
                MemoryMarshal.Cast<byte, ushort>(reconstructionLuma),
                MemoryMarshal.Cast<byte, ushort>(reconstructionBlue),
                MemoryMarshal.Cast<byte, ushort>(reconstructionRed),
                macroBlock,
                blockOrigin,
                plane,
                origin,
                blockSize,
                transformSize,
                subX,
                subY,
                width,
                MemoryMarshal.Cast<byte, ushort>(samples));
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.GetObmcReference" />
        private ReadOnlySpan<byte> GetObmcReference(Av1ReferenceFrameType referenceFrame, out int stride, out Rectangle bounds)
        {
            if (!this.byteWorker.IsEmpty)
            {
                return this.byteWorker[0].GetObmcReference(referenceFrame, out stride, out bounds);
            }

            return MemoryMarshal.AsBytes(this.uint16Worker[0].GetObmcReference(
                referenceFrame,
                out stride,
                out bounds));
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.CalculateObmcTarget" />
        private void CalculateObmcTarget(
            Av1BlockSize blockSize,
            Span<int> weightedSource,
            Span<int> mask,
            Span<short> filterRows,
            ReadOnlySpan<int> modeInfoGrid,
            ReadOnlySpan<Av1MacroBlockModeInfo> modeInfoAllocation,
            ReadOnlySpan<Av1EncoderDisplacementVector> displacementVectors,
            ReadOnlySpan<byte> sourceLuma,
            ReadOnlySpan<byte> sourceBlue,
            ReadOnlySpan<byte> sourceRed)
        {
            if (!this.byteWorker.IsEmpty)
            {
                this.byteWorker[0].CalculateObmcTarget(
                blockSize,
                weightedSource,
                mask,
                filterRows,
                modeInfoGrid,
                modeInfoAllocation,
                displacementVectors,
                MemoryMarshal.Cast<byte, byte>(sourceLuma),
                MemoryMarshal.Cast<byte, byte>(sourceBlue),
                MemoryMarshal.Cast<byte, byte>(sourceRed));
                return;
            }

            this.uint16Worker[0].CalculateObmcTarget(
                blockSize,
                weightedSource,
                mask,
                filterRows,
                modeInfoGrid,
                modeInfoAllocation,
                displacementVectors,
                MemoryMarshal.Cast<byte, ushort>(sourceLuma),
                MemoryMarshal.Cast<byte, ushort>(sourceBlue),
                MemoryMarshal.Cast<byte, ushort>(sourceRed));
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.GetObmcSubpixelCost" />
        private int GetObmcSubpixelCost(
            ReadOnlySpan<byte> reference,
            int referenceStride,
            int referenceOrigin,
            Av1MotionVector vector,
            Av1MotionVector referenceVector,
            ReadOnlySpan<int> weightedSource,
            ReadOnlySpan<int> mask,
            Span<byte> motionSearchPrediction,
            Span<short> filterRows,
            in Av1MotionVectorCosts motionVectorCosts,
            int width,
            int height,
            int taps)
        {
            if (!this.byteWorker.IsEmpty)
            {
                return this.byteWorker[0].GetObmcSubpixelCost(
                MemoryMarshal.Cast<byte, byte>(reference),
                referenceStride,
                referenceOrigin,
                vector,
                referenceVector,
                weightedSource,
                mask,
                MemoryMarshal.Cast<byte, byte>(motionSearchPrediction),
                filterRows,
                in motionVectorCosts,
                width,
                height,
                taps);
            }

            return this.uint16Worker[0].GetObmcSubpixelCost(
                MemoryMarshal.Cast<byte, ushort>(reference),
                referenceStride,
                referenceOrigin,
                vector,
                referenceVector,
                weightedSource,
                mask,
                MemoryMarshal.Cast<byte, ushort>(motionSearchPrediction),
                filterRows,
                in motionVectorCosts,
                width,
                height,
                taps);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.ChargeInterTextureLoss" />
        private void ChargeInterTextureLoss(
            ReadOnlySpan<byte> sourceLuma,
            ReadOnlySpan<byte> sourceBlue,
            ReadOnlySpan<byte> sourceRed,
            ref Av1RateDistortionStatistics statistics,
            long currentCost,
            long sourceVariance,
            long sampleVariance,
            Point blockOrigin,
            Av1BlockSize blockSize,
            bool skip,
            bool isCompound,
            bool isInterIntra,
            Av1MotionVector vector)
        {
            if (!this.byteWorker.IsEmpty)
            {
                this.byteWorker[0].ChargeInterTextureLoss(
                MemoryMarshal.Cast<byte, byte>(sourceLuma),
                MemoryMarshal.Cast<byte, byte>(sourceBlue),
                MemoryMarshal.Cast<byte, byte>(sourceRed),
                ref statistics,
                currentCost,
                sourceVariance,
                sampleVariance,
                blockOrigin,
                blockSize,
                skip,
                isCompound,
                isInterIntra,
                vector);
                return;
            }

            this.uint16Worker[0].ChargeInterTextureLoss(
                MemoryMarshal.Cast<byte, ushort>(sourceLuma),
                MemoryMarshal.Cast<byte, ushort>(sourceBlue),
                MemoryMarshal.Cast<byte, ushort>(sourceRed),
                ref statistics,
                currentCost,
                sourceVariance,
                sampleVariance,
                blockOrigin,
                blockSize,
                skip,
                isCompound,
                isInterIntra,
                vector);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.GetSmoothingOffset" />
        private long GetSmoothingOffset(
            ReadOnlySpan<byte> sourceLuma,
            ReadOnlySpan<byte> sourceBlue,
            ReadOnlySpan<byte> sourceRed,
            Point blockOrigin,
            Av1BlockSize blockSize,
            ReadOnlySpan<byte> samples,
            int stride)
        {
            if (!this.byteWorker.IsEmpty)
            {
                return this.byteWorker[0].GetSmoothingOffset(
                MemoryMarshal.Cast<byte, byte>(sourceLuma),
                MemoryMarshal.Cast<byte, byte>(sourceBlue),
                MemoryMarshal.Cast<byte, byte>(sourceRed),
                blockOrigin,
                blockSize,
                MemoryMarshal.Cast<byte, byte>(samples),
                stride);
            }

            return this.uint16Worker[0].GetSmoothingOffset(
                MemoryMarshal.Cast<byte, ushort>(sourceLuma),
                MemoryMarshal.Cast<byte, ushort>(sourceBlue),
                MemoryMarshal.Cast<byte, ushort>(sourceRed),
                blockOrigin,
                blockSize,
                MemoryMarshal.Cast<byte, ushort>(samples),
                stride);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.FindIntraBlockCopyCandidates" />
        private int FindIntraBlockCopyCandidates(
            Span<int> workspaceStorage,
            Point blockOrigin,
            Av1BlockSize blockSize,
            Av1MacroBlockD macroBlock,
            in Av1MotionVectorCosts costs,
            Av1MotionVector reference,
            Span<Av1MotionVector> candidates)
        {
            if (!this.byteWorker.IsEmpty)
            {
                return this.byteWorker[0].FindIntraBlockCopyCandidates(workspaceStorage, blockOrigin, blockSize, macroBlock, in costs, reference, candidates);
            }

            return this.uint16Worker[0].FindIntraBlockCopyCandidates(workspaceStorage, blockOrigin, blockSize, macroBlock, in costs, reference, candidates);
        }

        /// <inheritdoc cref="ModeDecision{TSample, TOperator}.GetSourceSize" />
        private Size GetSourceSize(Av1Plane plane)
        {
            if (!this.byteWorker.IsEmpty)
            {
                return this.byteWorker[0].GetSourceSize(plane);
            }

            return this.uint16Worker[0].GetSourceSize(plane);
        }
    }

    /// <summary>Retains the active typed motion-search view for the shared candidate controller.</summary>
    /// <remarks>
    /// Each view contains spans over existing storage. Only the selected view is initialized.
    /// The constructor runs once per reference, before candidate evaluation can change search state.
    /// This preserves the original search parameters and avoids rebuilding them for each candidate.
    /// </remarks>
    internal readonly ref struct SingleReferenceMotionSearch
    {
        private readonly Av1MotionSearchBase.SingleReferenceSearch<byte, ByteOperator> byteSearch;
        private readonly Av1MotionSearchBase.SingleReferenceSearch<ushort, UInt16Operator> uint16Search;
        private readonly bool isByte;

        /// <summary>Initializes a new instance of the <see cref="SingleReferenceMotionSearch"/> struct for eight-bit samples.</summary>
        /// <param name="search">The existing typed motion-search view.</param>
        public SingleReferenceMotionSearch(Av1MotionSearchBase.SingleReferenceSearch<byte, ByteOperator> search)
        {
            this.byteSearch = search;
            this.uint16Search = default;
            this.isByte = true;
        }

        /// <summary>Initializes a new instance of the <see cref="SingleReferenceMotionSearch"/> struct for high-bit-depth samples.</summary>
        /// <param name="search">The existing typed motion-search view.</param>
        public SingleReferenceMotionSearch(Av1MotionSearchBase.SingleReferenceSearch<ushort, UInt16Operator> search)
        {
            this.byteSearch = default;
            this.uint16Search = search;
            this.isByte = false;
        }

        /// <inheritdoc cref="Av1MotionSearchBase.SingleReferenceSearch{TSample, TOperator}.Search"/>
        public bool Search(
            in Av1CoefficientTables tables,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> transformWorkspace,
            Av1MotionSearchSettings settings,
            int frameStepParameter,
            int spatialMagnitude,
            bool showFrame,
            int searchRange,
            bool forceInteger,
            bool allowHighPrecision,
            bool fineMeshInterval,
            int referenceIndex,
            Av1MotionVector referenceVector,
            int drlRate,
            Av1InterpolationFilter horizontalFilter,
            Av1InterpolationFilter verticalFilter,
            ReadOnlySpan<Av1MotionSearchBase.StartingCandidate> starts,
            int totalWeight,
            ref Av1MotionSearchBase.SingleReferenceState state,
            out Av1MotionSearchBase.FractionalResult result)
        {
            if (this.isByte)
            {
                return this.byteSearch.Search(
                    in tables,
                    transformCoefficients,
                    dequantizedCoefficients,
                    transformWorkspace,
                    settings,
                    frameStepParameter,
                    spatialMagnitude,
                    showFrame,
                    searchRange,
                    forceInteger,
                    allowHighPrecision,
                    fineMeshInterval,
                    referenceIndex,
                    referenceVector,
                    drlRate,
                    horizontalFilter,
                    verticalFilter,
                    starts,
                    totalWeight,
                    ref state,
                    out result);
            }

            return this.uint16Search.Search(
                in tables,
                transformCoefficients,
                dequantizedCoefficients,
                transformWorkspace,
                settings,
                frameStepParameter,
                spatialMagnitude,
                showFrame,
                searchRange,
                forceInteger,
                allowHighPrecision,
                fineMeshInterval,
                referenceIndex,
                referenceVector,
                drlRate,
                horizontalFilter,
                verticalFilter,
                starts,
                totalWeight,
                ref state,
                out result);
        }
    }
}

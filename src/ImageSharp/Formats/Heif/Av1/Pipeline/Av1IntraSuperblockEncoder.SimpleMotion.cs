// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <content>
/// Collects cached luma motion-search features for partition decisions.
/// </content>
internal static partial class Av1IntraSuperblockEncoder
{
    internal partial struct ModeDecision<TSample, TOperator>
        where TSample : unmanaged
        where TOperator : struct, IBlockEncodingOperator<TSample>
    {
        /// <summary>
        /// Searches the ordinary retained reference and measures its final rounded luma predictor.
        /// </summary>
        /// <param name="workspaceStorage">The storage of the block workspace, which holds the search buffers of every block.</param>
        /// <param name="sourceLuma">The samples of the complete source luma plane, read once per frame pass.</param>
        /// <param name="sourceBlue">The samples of the complete source blue-difference plane, read once per frame pass.</param>
        /// <param name="sourceRed">The samples of the complete source red-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionLuma">The samples of the complete reconstructed luma plane, read once per frame pass.</param>
        /// <param name="reconstructionBlue">The samples of the complete reconstructed blue-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionRed">The samples of the complete reconstructed red-difference plane, read once per frame pass.</param>
        /// <param name="motionSearchPrediction">The prediction buffer of the motion search.</param>
        /// <param name="filterRows">The intermediate rows of the prediction filters.</param>
        /// <param name="motionVectorCosts">The motion vector rates of the frame precision.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="start">The vector that starts the search.</param>
        /// <param name="useSubpixel">Whether the search refines to fractional precision.</param>
        /// <param name="squaredError">The squared error of the selected prediction.</param>
        /// <param name="variance">The variance of the selected prediction error.</param>
        /// <returns>The selected vector.</returns>
        private Av1MotionVector SearchSimpleMotion(
            Span<int> workspaceStorage,
            ReadOnlySpan<TSample> sourceLuma,
            ReadOnlySpan<TSample> sourceBlue,
            ReadOnlySpan<TSample> sourceRed,
            Span<TSample> reconstructionLuma,
            Span<TSample> reconstructionBlue,
            Span<TSample> reconstructionRed,
            Span<TSample> motionSearchPrediction,
            Span<short> filterRows,
            in Av1MotionVectorCosts motionVectorCosts,
            Point blockOrigin,
            Av1BlockSize blockSize,
            Av1MotionVector start,
            bool useSubpixel,
            out int squaredError,
            out int variance)
        {
            if (!this.IsBlockOriginInsideFrame(blockOrigin))
            {
                squaredError = 0;
                variance = 0;
                return default;
            }

            Av1PlaneRegion<TSample> sourcePlane = this.source.GetPlane(Av1Plane.Y);
            Av1PlaneRegion<TSample> referencePlane = this.reference.GetPlane(Av1Plane.Y);
            ReadOnlySpan<TSample> source = Av1TransformBlockEncoder.GetPlaneSpan(sourceLuma, sourcePlane, blockOrigin);
            ReadOnlySpan<TSample> reference = referencePlane.Samples;
            int referenceOrigin = ((referencePlane.Bounds.Y + blockOrigin.Y) * referencePlane.Stride) + referencePlane.Bounds.X + blockOrigin.X;
            Size size = new(blockSize.GetWidth(), blockSize.GetHeight());
            Size frameSize = new(
                this.picture.Parent.Common.ModeInfoColumnCount << Av1Constants.ModeInfoSizeLog2,
                this.picture.Parent.Common.ModeInfoRowCount << Av1Constants.ModeInfoSizeLog2);

            Rectangle frameBounds = Av1MotionVector.GetFrameSearchBounds(
                new Rectangle(blockOrigin, size), frameSize, Math.Min(referencePlane.Bounds.X, referencePlane.Bounds.Y));

            Av1MotionVector zero = default;
            ObuFrameHeader frameHeader = this.picture.Parent.FrameHeader;
            Av1MotionSearchSettings settings = this.picture.Parent.MotionSearchSettings;
            Av1MotionSearchSettings.FullPixelSearchMethod method = settings.GetFullPixelMethod(blockSize);
            Av1MotionSearchSites sites = this.blockWorkspace.GetMotionSearchSites(workspaceStorage, method, referencePlane.Stride);
            int step = Math.Min(this.picture.Parent.MotionSearchStepParameter + this.picture.Parent.SpeedSettings.SimpleMotionStepReduction, 9);

            // The search prices vectors with the error per bit that the last block set. The partition search does not set its own value.
            int errorPerBitRateMultiplier = this.blockWorkspace.ErrorPerBitRateMultiplier;
            Av1MotionSearchBase.FullPixelSearch<TSample, TOperator> fullSearch = new(
                source,
                sourcePlane.Stride,
                reference,
                referencePlane.Stride,
                referenceOrigin,
                size,
                this.ApplySharpnessMargins(zero.GetFullPixelSearchBounds(frameBounds), blockOrigin, size, 1),
                zero,
                motionVectorCosts,
                this.bitDepth,
                Av1RateDistortion.GetMotionSearchSadPerBit(this.blockQIndex, this.bitDepth),
                errorPerBitRateMultiplier,
                [],
                []);

            Av1MotionSearchBase.FullPixelResult full = fullSearch.Search(
                new Point(start.Column / 8, start.Row / 8),
                step,
                method,
                sites,
                settings,
                keyFrame: false,
                fineMeshInterval: this.UsesFineSearchInterval,
                intraBlockCopy: false,
                [],
                out _);

            Av1MotionVector vector = new(full.Vector.Y * 8, full.Vector.X * 8);
            squaredError = full.SquaredError;
            variance = full.Variance;
            if (useSubpixel && full.Cost < int.MaxValue && !frameHeader.ForceIntegerMotionVector &&
                settings.SimpleMotionPrecision != Av1MotionSearchSettings.SearchPrecision.Integer)
            {
                // For a reference of another size, the fractional search and the final prediction read the original reference with its
                // scale factors, not the resized copy.
                Av1MotionSearchBase.ScaledReference<TSample> scaledReference =
                    this.GetScaledSearchReference(this.simpleMotionReference, blockOrigin, filterRows);

                Av1MotionSearchBase.FractionalSearch<TSample, TOperator> fractionalSearch = new(
                    source,
                    sourcePlane.Stride,
                    reference,
                    referencePlane.Stride,
                    referenceOrigin,
                    motionSearchPrediction,
                    size,
                    this.ApplySharpnessMargins(zero.GetSubpixelSearchBounds(frameBounds), blockOrigin, size, Av1MotionVector.SubpixelScale),
                    zero,
                    motionVectorCosts,
                    this.bitDepth,
                    errorPerBitRateMultiplier,
                    [],
                    [],
                    scaledReference);

                fractionalSearch.Search(
                    vector,
                    full,
                    settings.FractionalMethod,
                    settings.SimpleMotionPrecision,
                    frameHeader.AllowHighPrecisionMotionVector,
                    settings.FractionalIterationsPerStep,
                    settings.FractionalInterpolationTaps,
                    [],
                    [],
                    out Av1MotionSearchBase.FractionalResult fractional);

                vector = fractional.Vector;

                // The fractional search can use short filters and another intermediate rounding. As a result, the code predicts the winner again
                // with the regular filter before it measures the model features.
                if (scaledReference.IsScaled)
                {
                    scaledReference.Predict<TOperator>(vector, motionSearchPrediction, size, this.bitDepth.GetBitCount());
                }
                else
                {
                    TOperator.BuildPrediction(
                        reference,
                        referencePlane.Stride,
                        referenceOrigin + ((vector.Row >> 3) * referencePlane.Stride) + (vector.Column >> 3),
                        motionSearchPrediction,
                        size.Width,
                        filterRows,
                        size.Width,
                        size.Height,
                        Av1InterpolationFilter.Regular,
                        Av1InterpolationFilter.Regular,
                        (vector.Column & 7) << 1,
                        (vector.Row & 7) << 1,
                        this.bitDepth.GetBitCount());
                }

                // The prediction also goes into the reconstructed frame at this block. Later reads of the frame at this position see it.
                Av1PlaneRegion<TSample> luma = this.reconstruction.GetPlane(Av1Plane.Y);
                Av1TransformBlockEncoder.WriteFrameSamples(
                    luma, reconstructionLuma, blockOrigin, motionSearchPrediction, size.Width, size.Width, size.Height);

                TOperator.GetMoments(source, sourcePlane.Stride, motionSearchPrediction, size.Width, size.Width, size.Height, out int sum, out long squares);

                int shift = this.bitDepth.GetBitCount() - 8;
                sum = (sum + ((1 << shift) >> 1)) >> shift;
                squares = (squares + ((1L << (2 * shift)) >> 1)) >> (2 * shift);
                squaredError = (int)squares;
                variance = (int)Math.Max(0, squares - (((long)sum * sum) / (size.Width * size.Height)));
            }

            return vector;
        }

        /// <summary>
        /// Populates whole, quarter, and half-block features without repeating completed searches.
        /// </summary>
        /// <param name="workspaceStorage">The storage of the block workspace, which holds the search buffers of every block.</param>
        /// <param name="sourceLuma">The samples of the complete source luma plane, read once per frame pass.</param>
        /// <param name="sourceBlue">The samples of the complete source blue-difference plane, read once per frame pass.</param>
        /// <param name="sourceRed">The samples of the complete source red-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionLuma">The samples of the complete reconstructed luma plane, read once per frame pass.</param>
        /// <param name="reconstructionBlue">The samples of the complete reconstructed blue-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionRed">The samples of the complete reconstructed red-difference plane, read once per frame pass.</param>
        /// <param name="motionSearchPrediction">The prediction buffer of the motion search.</param>
        /// <param name="filterRows">The intermediate rows of the prediction filters.</param>
        /// <param name="motionVectorCosts">The motion vector rates of the frame precision.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="nodeIndex">The simple motion node of the block.</param>
        /// <param name="includeRectangles">Whether the half-block searches are measured too.</param>
        private void CollectSimpleMotionFeatures(
            Span<int> workspaceStorage,
            ReadOnlySpan<TSample> sourceLuma,
            ReadOnlySpan<TSample> sourceBlue,
            ReadOnlySpan<TSample> sourceRed,
            Span<TSample> reconstructionLuma,
            Span<TSample> reconstructionBlue,
            Span<TSample> reconstructionRed,
            Span<TSample> motionSearchPrediction,
            Span<short> filterRows,
            in Av1MotionVectorCosts motionVectorCosts,
            Point blockOrigin,
            Av1BlockSize blockSize,
            int nodeIndex,
            bool includeRectangles)
        {
            Span<Av1SimpleMotionData> nodes = this.blockWorkspace.GetSimpleMotionData(workspaceStorage);
            ref Av1SimpleMotionData node = ref nodes[nodeIndex];
            if (!node.WholeBlockValid)
            {
                this.MeasureSimpleMotionNode(
                    workspaceStorage,
                    sourceLuma,
                    sourceBlue,
                    sourceRed,
                    reconstructionLuma,
                    reconstructionBlue,
                    reconstructionRed,
                    motionSearchPrediction,
                    filterRows,
                    in motionVectorCosts,
                    blockOrigin,
                    blockSize,
                    nodeIndex);
            }

            Av1BlockSize childSize = Av1PartitionType.Split.GetBlockSubSize(blockSize);
            int half = blockSize.GetWidth() >> 1;
            for (int child = 0; child < 4; child++)
            {
                int childIndex = (nodeIndex * 4) + child + 1;
                if (!nodes[childIndex].WholeBlockValid)
                {
                    Point origin = new(blockOrigin.X + ((child & 1) * half), blockOrigin.Y + ((child >> 1) * half));
                    this.MeasureSimpleMotionNode(
                        workspaceStorage,
                        sourceLuma,
                        sourceBlue,
                        sourceRed,
                        reconstructionLuma,
                        reconstructionBlue,
                        reconstructionRed,
                        motionSearchPrediction,
                        filterRows,
                        in motionVectorCosts,
                        origin,
                        childSize,
                        childIndex);
                }
            }

            if (includeRectangles && !node.RectanglesValid)
            {
                for (int rectangle = 0; rectangle < 4; rectangle++)
                {
                    bool horizontal = rectangle < 2;
                    Point origin = new(
                        blockOrigin.X + (horizontal ? 0 : (rectangle & 1) * half),
                        blockOrigin.Y + (horizontal ? (rectangle & 1) * half : 0));

                    Av1BlockSize rectangleSize = (horizontal ? Av1PartitionType.Horizontal : Av1PartitionType.Vertical).GetBlockSubSize(blockSize);
                    this.SearchSimpleMotion(
                        workspaceStorage,
                        sourceLuma,
                        sourceBlue,
                        sourceRed,
                        reconstructionLuma,
                        reconstructionBlue,
                        reconstructionRed,
                        motionSearchPrediction,
                        filterRows,
                        in motionVectorCosts,
                        origin,
                        rectangleSize,
                        node.Starts[(int)Av1ReferenceFrameType.Last],
                        true,
                        out node.RectangleSquaredErrors[rectangle],
                        out node.RectangleVariances[rectangle]);
                }

                node.RectanglesValid = true;
            }
        }

        /// <summary>
        /// Packs log-scaled motion errors, quantization, and spatial-neighbor geometry for the partition models.
        /// </summary>
        /// <param name="motionSearchPrediction">The prediction buffer of the motion search.</param>
        /// <param name="filterRows">The intermediate rows of the prediction filters.</param>
        /// <param name="motionVectorCosts">The motion vector rates of the frame precision.</param>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="workspaceStorage">The storage of the block workspace, which holds the search buffers of every block.</param>
        /// <param name="sourceLuma">The samples of the complete source luma plane, read once per frame pass.</param>
        /// <param name="sourceBlue">The samples of the complete source blue-difference plane, read once per frame pass.</param>
        /// <param name="sourceRed">The samples of the complete source red-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionLuma">The samples of the complete reconstructed luma plane, read once per frame pass.</param>
        /// <param name="reconstructionBlue">The samples of the complete reconstructed blue-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionRed">The samples of the complete reconstructed red-difference plane, read once per frame pass.</param>
        /// <param name="macroBlock">The neighbor availability of the block.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="nodeIndex">The simple motion node of the block.</param>
        /// <param name="includeRectangles">Whether the half-block features are packed too.</param>
        /// <param name="features">The feature destination.</param>
        private void GetSimpleMotionFeatures(
            Span<TSample> motionSearchPrediction,
            Span<short> filterRows,
            in Av1MotionVectorCosts motionVectorCosts,
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
            Av1BlockSize blockSize,
            int nodeIndex,
            bool includeRectangles,
            Span<float> features)
        {
            this.CollectSimpleMotionFeatures(
                workspaceStorage,
                sourceLuma,
                sourceBlue,
                sourceRed,
                reconstructionLuma,
                reconstructionBlue,
                reconstructionRed,
                motionSearchPrediction,
                filterRows,
                in motionVectorCosts,
                blockOrigin,
                blockSize,
                nodeIndex,
                includeRectangles);

            Span<Av1SimpleMotionData> nodes = this.blockWorkspace.GetSimpleMotionData(workspaceStorage);
            features[0] = float.LogP1(nodes[nodeIndex].SquaredError);
            features[1] = float.LogP1(nodes[nodeIndex].Variance);
            int index = 2;
            for (int child = 0; child < 4; child++)
            {
                ref Av1SimpleMotionData childNode = ref nodes[(nodeIndex * 4) + child + 1];
                features[index++] = float.LogP1(childNode.SquaredError);
                features[index++] = float.LogP1(childNode.Variance);
            }

            if (includeRectangles)
            {
                for (int rectangle = 0; rectangle < 4; rectangle++)
                {
                    features[index++] = float.LogP1(nodes[nodeIndex].RectangleSquaredErrors[rectangle]);
                    features[index++] = float.LogP1(nodes[nodeIndex].RectangleVariances[rectangle]);
                }
            }

            int dcStep = Av1QuantizationLookup.GetDcQuant(this.blockQIndex, 0, this.bitDepth) >> (this.bitDepth.GetBitCount() - 8);
            features[index++] = float.LogP1((dcStep * dcStep) / 256F);
            Point position = new(blockOrigin.X >> Av1Constants.ModeInfoSizeLog2, blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2);
            Av1TileWriter.SetModeInfoRowAndColumn(
                macroBlock,
                macroBlock.Tile,
                position,
                blockSize,
                this.picture.Parent.Common.ModeInfoStride,
                this.picture.Parent.Common.ModeInfoRowCount,
                this.picture.Parent.Common.ModeInfoColumnCount);

            Av1BlockSize above = macroBlock.IsUpAvailable
                ? macroBlock.GetRelativeModeInfo(modeInfoGrid, modeInfoAllocation, -macroBlock.ModeInfoStride).Block.BlockSize
                : blockSize;

            Av1BlockSize left = macroBlock.IsLeftAvailable
                ? macroBlock.GetRelativeModeInfo(modeInfoGrid, modeInfoAllocation, -1).Block.BlockSize
                : blockSize;

            features[index++] = macroBlock.IsUpAvailable ? 1F : 0F;
            features[index++] = BitOperations.Log2((uint)above.GetWidth()) - Av1Constants.ModeInfoSizeLog2;
            features[index++] = BitOperations.Log2((uint)above.GetHeight()) - Av1Constants.ModeInfoSizeLog2;
            features[index++] = macroBlock.IsLeftAvailable ? 1F : 0F;
            features[index++] = BitOperations.Log2((uint)left.GetWidth()) - Av1Constants.ModeInfoSizeLog2;
            features[index] = BitOperations.Log2((uint)left.GetHeight()) - Av1Constants.ModeInfoSizeLog2;
        }

        /// <summary>
        /// Publishes a square's measured features and propagates its full-sample starting vector to its children.
        /// </summary>
        /// <param name="workspaceStorage">The storage of the block workspace, which holds the search buffers of every block.</param>
        /// <param name="sourceLuma">The samples of the complete source luma plane, read once per frame pass.</param>
        /// <param name="sourceBlue">The samples of the complete source blue-difference plane, read once per frame pass.</param>
        /// <param name="sourceRed">The samples of the complete source red-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionLuma">The samples of the complete reconstructed luma plane, read once per frame pass.</param>
        /// <param name="reconstructionBlue">The samples of the complete reconstructed blue-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionRed">The samples of the complete reconstructed red-difference plane, read once per frame pass.</param>
        /// <param name="motionSearchPrediction">The prediction buffer of the motion search.</param>
        /// <param name="filterRows">The intermediate rows of the prediction filters.</param>
        /// <param name="motionVectorCosts">The motion vector rates of the frame precision.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="nodeIndex">The simple motion node of the block.</param>
        private void MeasureSimpleMotionNode(
            Span<int> workspaceStorage,
            ReadOnlySpan<TSample> sourceLuma,
            ReadOnlySpan<TSample> sourceBlue,
            ReadOnlySpan<TSample> sourceRed,
            Span<TSample> reconstructionLuma,
            Span<TSample> reconstructionBlue,
            Span<TSample> reconstructionRed,
            Span<TSample> motionSearchPrediction,
            Span<short> filterRows,
            in Av1MotionVectorCosts motionVectorCosts,
            Point blockOrigin,
            Av1BlockSize blockSize,
            int nodeIndex)
        {
            Span<Av1SimpleMotionData> nodes = this.blockWorkspace.GetSimpleMotionData(workspaceStorage);
            ref Av1SimpleMotionData node = ref nodes[nodeIndex];
            Av1MotionVector vector = this.SearchSimpleMotion(
                workspaceStorage,
                sourceLuma,
                sourceBlue,
                sourceRed,
                reconstructionLuma,
                reconstructionBlue,
                reconstructionRed,
                motionSearchPrediction,
                filterRows,
                in motionVectorCosts,
                blockOrigin,
                blockSize,
                node.Starts[(int)Av1ReferenceFrameType.Last],
                true,
                out node.SquaredError,
                out node.Variance);

            node.WholeBlockValid = true;
            if (this.IsBlockOriginInsideFrame(blockOrigin))
            {
                // Child starts truncate toward zero after refinement, unlike the nearest rounding
                // used to seed the superblock from its spatial reference stack.
                Av1MotionVector fullVector = new((vector.Row / 8) * 8, (vector.Column / 8) * 8);
                node.Starts[(int)Av1ReferenceFrameType.Last] = fullVector;
                if (blockSize >= Av1BlockSize.Block8x8)
                {
                    for (int child = 0; child < 4; child++)
                    {
                        nodes[(nodeIndex * 4) + child + 1].Starts[(int)Av1ReferenceFrameType.Last] = fullVector;
                    }
                }
            }
        }
    }
}

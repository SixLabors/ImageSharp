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
        private Av1MotionVector SearchSimpleMotion(
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
            ReadOnlySpan<TSample> source = Av1TransformBlockEncoder.GetPlaneSpan(sourcePlane, blockOrigin);
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
            Av1MotionVectorCosts costs = this.blockWorkspace.GetMotionVectorCosts(frameHeader.MotionVectorPrecision);
            Av1MotionSearchSettings.FullPixelSearchMethod method = settings.GetFullPixelMethod(blockSize);
            Av1MotionSearchSites sites = this.blockWorkspace.GetMotionSearchSites(method, referencePlane.Stride);
            int step = Math.Min(this.picture.Parent.MotionSearchStepParameter + this.picture.Parent.SpeedSettings.SimpleMotionStepReduction, 9);

            // The search prices vectors with the error per bit set last, which the partition search does not set
            // for itself. Reference: x->errorperbit in av1_make_default_fullpel_ms_params() and
            // av1_make_default_subpel_ms_params().
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
                costs,
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
                Span<TSample> prediction = this.blockWorkspace.GetMotionSearchPrediction<TSample>();
                Av1MotionSearchBase.FractionalSearch<TSample, TOperator> fractionalSearch = new(
                    source,
                    sourcePlane.Stride,
                    reference,
                    referencePlane.Stride,
                    referenceOrigin,
                    prediction,
                    size,
                    this.ApplySharpnessMargins(zero.GetSubpixelSearchBounds(frameBounds), blockOrigin, size, Av1MotionVector.SubpixelScale),
                    zero,
                    costs,
                    this.bitDepth,
                    errorPerBitRateMultiplier,
                    [],
                    []);

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

                // Fractional search may use short filters and different intermediate rounding.
                // Rebuild its winner with the final regular predictor before collecting model features.
                TOperator.BuildPrediction(
                    reference,
                    referencePlane.Stride,
                    referenceOrigin + ((vector.Row >> 3) * referencePlane.Stride) + (vector.Column >> 3),
                    prediction,
                    this.blockWorkspace.GetInterPredictionWorkspace<TSample>().PredictionScratch,
                    size.Width,
                    size.Height,
                    Av1InterpolationFilter.Regular,
                    Av1InterpolationFilter.Regular,
                    (vector.Column & 7) << 1,
                    (vector.Row & 7) << 1,
                    this.bitDepth.GetBitCount());

                TOperator.GetMoments(
                    source, sourcePlane.Stride, prediction, size.Width, size.Width, size.Height, out int sum, out long squares);

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
        private void CollectSimpleMotionFeatures(Point blockOrigin, Av1BlockSize blockSize, int nodeIndex, bool includeRectangles)
        {
            Span<Av1SimpleMotionData> nodes = this.blockWorkspace.SimpleMotionData;
            ref Av1SimpleMotionData node = ref nodes[nodeIndex];
            if (!node.WholeBlockValid)
            {
                this.MeasureSimpleMotionNode(blockOrigin, blockSize, nodeIndex);
            }

            Av1BlockSize childSize = Av1PartitionType.Split.GetBlockSubSize(blockSize);
            int half = blockSize.GetWidth() >> 1;
            for (int child = 0; child < 4; child++)
            {
                int childIndex = (nodeIndex * 4) + child + 1;
                if (!nodes[childIndex].WholeBlockValid)
                {
                    Point origin = new(blockOrigin.X + ((child & 1) * half), blockOrigin.Y + ((child >> 1) * half));
                    this.MeasureSimpleMotionNode(origin, childSize, childIndex);
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
        private void GetSimpleMotionFeatures(
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            int nodeIndex,
            bool includeRectangles,
            Span<float> features)
        {
            this.CollectSimpleMotionFeatures(blockOrigin, blockSize, nodeIndex, includeRectangles);
            Span<Av1SimpleMotionData> nodes = this.blockWorkspace.SimpleMotionData;
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
                this.picture,
                macroBlock,
                macroBlock.Tile,
                position,
                blockSize,
                this.picture.Parent.Common.ModeInfoStride,
                this.picture.Parent.Common.ModeInfoRowCount,
                this.picture.Parent.Common.ModeInfoColumnCount);

            Av1BlockSize above = macroBlock.IsUpAvailable
                ? macroBlock.GetRelativeModeInfo(-macroBlock.ModeInfoStride).Block.BlockSize : blockSize;

            Av1BlockSize left = macroBlock.IsLeftAvailable ? macroBlock.GetRelativeModeInfo(-1).Block.BlockSize : blockSize;
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
        private void MeasureSimpleMotionNode(Point blockOrigin, Av1BlockSize blockSize, int nodeIndex)
        {
            Span<Av1SimpleMotionData> nodes = this.blockWorkspace.SimpleMotionData;
            ref Av1SimpleMotionData node = ref nodes[nodeIndex];
            Av1MotionVector vector = this.SearchSimpleMotion(
                blockOrigin, blockSize, node.Starts[(int)Av1ReferenceFrameType.Last], true, out node.SquaredError, out node.Variance);

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

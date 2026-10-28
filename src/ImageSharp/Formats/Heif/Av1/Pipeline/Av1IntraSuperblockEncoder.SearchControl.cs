// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
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
/// Shares partition and mode search control between native sample workers.
/// </content>
internal static partial class Av1IntraSuperblockEncoder
{
    /// <summary>
    /// The leaf bound that stands for a bound that is already negative at the multiplier of the caller. With this bound, the leaf
    /// search returns before it sets up the block.
    /// </summary>
    private const long NegativeLeafBound = long.MinValue;

    /// <summary>
    /// Returns a unidirectional compound pair, in the AV1 order of unidirectional pairs.
    /// </summary>
    /// <param name="index">The pair index, from zero to eight.</param>
    /// <returns>The two references.</returns>
    private static (Av1ReferenceFrameType First, Av1ReferenceFrameType Second) GetUnidirectionalPair(int index)
        => index switch
        {
            0 => (Av1ReferenceFrameType.Last, Av1ReferenceFrameType.Last2),
            1 => (Av1ReferenceFrameType.Last, Av1ReferenceFrameType.Last3),
            2 => (Av1ReferenceFrameType.Last, Av1ReferenceFrameType.Golden),
            3 => (Av1ReferenceFrameType.Backward, Av1ReferenceFrameType.Alternate),
            4 => (Av1ReferenceFrameType.Last2, Av1ReferenceFrameType.Last3),
            5 => (Av1ReferenceFrameType.Last2, Av1ReferenceFrameType.Golden),
            6 => (Av1ReferenceFrameType.Last3, Av1ReferenceFrameType.Golden),
            7 => (Av1ReferenceFrameType.Backward, Av1ReferenceFrameType.Alternate2),
            _ => (Av1ReferenceFrameType.Alternate2, Av1ReferenceFrameType.Alternate)
        };

    /// <summary>
    /// Returns the largest full-sample magnitude of the first two reference predictors, which sets the spatial search range of the
    /// block. A clamp at the last interpolation tap that can be visible limits the padded reads without a change to their prediction.
    /// </summary>
    /// <param name="referenceMotionVectors">The reference vector list of the block.</param>
    /// <param name="blockOrigin">The luma block origin.</param>
    /// <param name="blockSize">The block size.</param>
    /// <param name="frameSize">The frame size in luma samples.</param>
    /// <returns>The largest full-sample magnitude.</returns>
    private static int GetSpatialMotionMagnitude(
        Av1ReferenceMotionVectors referenceMotionVectors,
        Point blockOrigin,
        Av1BlockSize blockSize,
        Size frameSize)
    {
        int spatialMagnitude = 0;
        for (int index = 0; index < 2; index++)
        {
            Av1MotionVector spatial = referenceMotionVectors.GetNewReference(index);
            int column = Math.Clamp(spatial.Column, -(blockOrigin.X + blockSize.GetWidth() + 4) * 8, (frameSize.Width - blockOrigin.X + 4) * 8);
            int row = Math.Clamp(spatial.Row, -(blockOrigin.Y + blockSize.GetHeight() + 4) * 8, (frameSize.Height - blockOrigin.Y + 4) * 8);
            spatialMagnitude = Math.Max(spatialMagnitude, Math.Max(Math.Abs(row), Math.Abs(column)) >> 3);
        }

        return spatialMagnitude;
    }

    /// <summary>
    /// Measures the complete mode, dynamic-reference-list, and differential-vector syntax for one candidate.
    /// </summary>
    /// <param name="tables">The rate tables of the tile.</param>
    /// <param name="motionVectorCosts">The motion vector rates of the frame precision.</param>
    /// <param name="mode">The candidate single-reference inter mode.</param>
    /// <param name="vector">The candidate motion vector.</param>
    /// <param name="referenceMotionVectorIndex">The selected dynamic-reference-list entry.</param>
    /// <param name="referenceMotionVectors">The current spatial candidate stack.</param>
    /// <returns>The syntax rate in 1/512-bit units.</returns>
    private static int GetInterModeRate(
        in Av1CoefficientTables tables,
        in Av1MotionVectorCosts motionVectorCosts,
        Av1PredictionMode mode,
        Av1MotionVector vector,
        int referenceMotionVectorIndex,
        Av1ReferenceMotionVectors referenceMotionVectors)
    {
        int rate = Av1SymbolEncoder.GetInterModeCost(tables.ModeCosts, mode, referenceMotionVectors.ModeContext);
        if (mode == Av1PredictionMode.NearMotionVector)
        {
            for (int index = 1; index < 3 && referenceMotionVectors.Count > index + 1; index++)
            {
                bool advance = referenceMotionVectorIndex >= index;
                int context = Av1SymbolContextHelper.GetDrlContext(referenceMotionVectors.Weights, index);
                rate += Av1SymbolEncoder.GetDynamicReferenceListCost(tables.ModeCosts, advance, context);
                if (!advance)
                {
                    break;
                }
            }

            return rate;
        }

        if (mode != Av1PredictionMode.NewMotionVector)
        {
            return rate;
        }

        for (int index = 0; index < 2 && referenceMotionVectors.Count > index + 1; index++)
        {
            bool advance = referenceMotionVectorIndex > index;
            int context = Av1SymbolContextHelper.GetDrlContext(referenceMotionVectors.Weights, index);
            rate += Av1SymbolEncoder.GetDynamicReferenceListCost(tables.ModeCosts, advance, context);
            if (!advance)
            {
                break;
            }
        }

        Av1MotionVector reference = referenceMotionVectors.GetNewReference(referenceMotionVectorIndex);

        // Mode selection discounts motion syntax to 108/128 of its estimated rate. The rounded weight applies to the vector alone.
        // The mode and reference-list symbols keep their full rate.
        return rate + (((motionVectorCosts.GetCost(vector, reference) * 108) + 64) >> 7);
    }

    /// <summary>
    /// Returns whether the partition search of a block ends because neither the unsplit candidate nor the split candidate produced
    /// a cost. The superblock itself never ends this way.
    /// </summary>
    /// <param name="enabled">Whether the speed settings enable this termination.</param>
    /// <param name="blockSize">The block size.</param>
    /// <param name="superblockSize">The superblock size.</param>
    /// <param name="noneInvalid">Whether the unsplit candidate produced no cost.</param>
    /// <param name="splitInvalid">Whether the split candidate produced no cost.</param>
    /// <returns><see langword="true"/> when the partition search ends.</returns>
    internal static bool ShouldTerminatePartitionSearchAfterNoneAndSplit(
        bool enabled,
        Av1BlockSize blockSize,
        Av1BlockSize superblockSize,
        bool noneInvalid,
        bool splitInvalid)
        => enabled &&
            blockSize != superblockSize &&
            noneInvalid &&
            splitInvalid;

    /// <summary>
    /// Selects the source candidate of the cached luma decision of every asymmetric sub-block. Each sub-block takes the square or
    /// rectangular leaf that covers the same samples.
    /// </summary>
    /// <param name="cache">Receives the cached decision of each of the three sub-blocks.</param>
    /// <param name="partitionType">The asymmetric partition type.</param>
    /// <param name="split">The decisions of the four split children.</param>
    /// <param name="horizontal">The decisions of the two horizontal halves.</param>
    /// <param name="vertical">The decisions of the two vertical halves.</param>
    private static void SetAsymmetricModeCache(
        ref InlineArray3<Av1AsymmetricModeCacheEntry> cache,
        Av1PartitionType partitionType,
        ReadOnlySpan<Av1AsymmetricModeCacheEntry> split,
        ReadOnlySpan<Av1AsymmetricModeCacheEntry> horizontal,
        ReadOnlySpan<Av1AsymmetricModeCacheEntry> vertical)
    {
        switch (partitionType)
        {
            case Av1PartitionType.HorizontalA:
                cache[0] = split[0];
                cache[1] = split[1];
                cache[2] = horizontal[1];
                return;
            case Av1PartitionType.HorizontalB:
                cache[0] = horizontal[0];
                cache[1] = split[2];
                cache[2] = split[3];
                return;
            case Av1PartitionType.VerticalA:
                cache[0] = split[0];
                cache[1] = split[2];
                cache[2] = vertical[1];
                return;
            default:
                cache[0] = vertical[0];
                cache[1] = split[1];
                cache[2] = split[3];
                return;
        }
    }

    /// <summary>
    /// Gets whether a selected mode is skippable for the partition search, from the empty-transform flag of its rate statistics. An
    /// inter mode is skippable when its transform search left every block empty. A regular intra mode of an inter frame never is,
    /// because its statistics always code the non-skip flag. A palette winner is skippable when both its luma and its chroma quantized
    /// to nothing. This is not the coded skip flag.
    /// </summary>
    /// <param name="snapshot">The mode snapshot of the selected mode.</param>
    /// <returns><see langword="true"/> when the mode is skippable.</returns>
    private static bool IsSkippable(Av1EncoderPartitionTree.ModeSnapshot snapshot)
        => snapshot.Statistics.AllTransformsEmpty;

    /// <summary>
    /// Returns the part of a plane block that the coded transforms cover. The extent is the visible part of the block, rounded down
    /// to whole 4x4 units and then up to whole transforms.
    /// </summary>
    /// <param name="macroBlock">The frame edges of the block.</param>
    /// <param name="planeBlockSize">The block size in the plane.</param>
    /// <param name="transformSize">The transform size.</param>
    /// <param name="subsamplingX">The horizontal subsampling shift of the plane.</param>
    /// <param name="subsamplingY">The vertical subsampling shift of the plane.</param>
    /// <returns>The coded extent in plane samples.</returns>
    private static Size GetCodedTransformExtent(
        Av1MacroBlockD macroBlock,
        Av1BlockSize planeBlockSize,
        Av1TransformSize transformSize,
        int subsamplingX,
        int subsamplingY)
    {
        int width = planeBlockSize.GetWidth();
        int height = planeBlockSize.GetHeight();
        int transformWidth = transformSize.GetWidth();
        int transformHeight = transformSize.GetHeight();

        // A transform that intersects the coded frame is encoded in full. Only transforms wholly in the padded border are omitted. The
        // coefficient stream packs the remaining transforms.
        width += Math.Min(0, macroBlock.ToRightEdge >> (3 + subsamplingX));
        height += Math.Min(0, macroBlock.ToBottomEdge >> (3 + subsamplingY));
        width &= ~((1 << Av1Constants.ModeInfoSizeLog2) - 1);
        height &= ~((1 << Av1Constants.ModeInfoSizeLog2) - 1);
        return new Size(
            (width + transformWidth - 1) & -transformWidth,
            (height + transformHeight - 1) & -transformHeight);
    }

    /// <summary>
    /// Gets a value indicating whether the texture-loss charge treats a mode as smooth. The smooth modes are DC, the three smooth modes and Paeth.
    /// </summary>
    /// <param name="mode">The prediction mode.</param>
    /// <returns>True when the mode is smooth.</returns>
    private static bool IsSmoothTextureMode(Av1PredictionMode mode)
        => mode is Av1PredictionMode.DC or Av1PredictionMode.Smooth or Av1PredictionMode.SmoothVertical or Av1PredictionMode.SmoothHorizontal or
            Av1PredictionMode.Paeth;

    /// <summary>
    /// Copies the transform grid a luma search produced into a retained decision, covering every entry
    /// the later pass over that decision can read.
    /// </summary>
    private static void CopyWinnerTransformStates(
        ReadOnlySpan<Av1EncoderTransformBlockState> source, Span<Av1EncoderTransformBlockState> destination)
        => source[..Math.Min(source.Length, destination.Length)].CopyTo(destination);

    /// <summary>
    /// Returns the number of luma coefficients that an inter block codes. The method walks the transform tree in its smallest
    /// traversal units and counts each transform once, at its origin, when that origin lies inside the coded frame.
    /// </summary>
    /// <param name="macroBlock">The neighbor availability and frame edges of the block.</param>
    /// <param name="modeInfo">The syntax of the block, with its transform sizes.</param>
    /// <returns>The coded luma area, in coefficients.</returns>
    private static int GetInterLumaCodedArea(Av1MacroBlockD macroBlock, ref Av1EncoderBlockModeInfo modeInfo)
    {
        Av1TransformSize rootSize = modeInfo.TransformSize == Av1TransformSize.Size4x4
            ? Av1TransformSize.Size4x4
            : modeInfo.BlockSize.GetMaximumTransformSize();

        Av1TransformSize traversalSize = rootSize.GetSubSize().GetSubSize();
        Size extent = GetCodedTransformExtent(macroBlock, modeInfo.BlockSize, Av1TransformSize.Size4x4, 0, 0);
        int leafCount = modeInfo.BlockSize.GetWidth() * modeInfo.BlockSize.GetHeight() / traversalSize.GetSize2d();
        int area = 0;
        for (int leaf = 0; leaf < leafCount; leaf++)
        {
            Point offset = rootSize.GetBlockPartitionOrigin(modeInfo.BlockSize, traversalSize, leaf, 0, 0);
            Av1TransformSize size = modeInfo.InterTransformSizes[modeInfo.GetInterTransformSizeIndex(offset.Y >> 2, offset.X >> 2)];
            if (offset.X < extent.Width && offset.Y < extent.Height &&
                (offset.X % size.GetWidth()) == 0 && (offset.Y % size.GetHeight()) == 0)
            {
                area += size.GetSize2d();
            }
        }

        return area;
    }

    /// <summary>
    /// Leaves DCT_DCT as the kept type of every luma transform block that an encode of an inter decision quantized to nothing. Thus a
    /// later encode of the same decision transforms it with the default type.
    /// </summary>
    /// <param name="retained">The retained luma transform states, one per coefficient unit.</param>
    /// <param name="encoded">The luma transform states the encode left, one per coefficient unit.</param>
    private static void RetainEncodedZeroBlockTypes(
        Span<Av1EncoderTransformBlockState> retained,
        ReadOnlySpan<Av1EncoderTransformBlockState> encoded)
    {
        for (int index = 0; index < retained.Length; index++)
        {
            if (encoded[index].EndOfBlock == 0)
            {
                // Replay reads the same retained entry after this update.
                ref Av1EncoderTransformBlockState retainedState = ref retained[index];
                retainedState.TransformType = Av1TransformType.DctDct;
            }
        }
    }

    /// <summary>
    /// Returns the combined type of a reference or reference pair. A pair of a forward and a backward reference indexes the
    /// forward-by-backward table. A pair on one side indexes the unidirectional pairs after that table.
    /// </summary>
    /// <param name="first">The first reference.</param>
    /// <param name="second">The second reference, or none for a single reference.</param>
    /// <returns>The combined reference type.</returns>
    private static int GetReferenceFrameType(Av1ReferenceFrameType first, Av1ReferenceFrameType second)
    {
        if (second <= Av1ReferenceFrameType.Intra)
        {
            return (int)first;
        }

        for (int index = 0; index < 9; index++)
        {
            (Av1ReferenceFrameType pairFirst, Av1ReferenceFrameType pairSecond) = GetUnidirectionalPair(index);
            if (first == pairFirst && second == pairSecond)
            {
                return Av1Constants.ReferenceFrameCount + (4 * 3) + index;
            }
        }

        return Av1Constants.ReferenceFrameCount + ((int)first - (int)Av1ReferenceFrameType.Last) +
            (((int)second - (int)Av1ReferenceFrameType.Backward) * 4);
    }

    /// <summary>
    /// Clamps a single-reference new vector that a compound mode reuses into the fractional search range around the compound
    /// reference vector.
    /// </summary>
    /// <param name="vector">The reused single-reference vector.</param>
    /// <param name="reference">The compound reference vector of the component.</param>
    /// <param name="frameBounds">The full-pixel motion range of the block.</param>
    /// <returns>The clamped vector.</returns>
    private static Av1MotionVector ClampToSubpixelRange(Av1MotionVector vector, Av1MotionVector reference, Rectangle frameBounds)
    {
        Rectangle limits = reference.GetSubpixelSearchBounds(frameBounds);
        return new(
            Math.Clamp(vector.Row, limits.Top, limits.Bottom - 1),
            Math.Clamp(vector.Column, limits.Left, limits.Right - 1));
    }

    /// <summary>
    /// Returns whether a compound entry can be searched. Only the second component decides. A second component that searches a new
    /// vector leaves the entry valid. Any other second component must lie in the frame search bounds. The first component is not
    /// tested. This quirk is kept on purpose, because the entry set changes the coded decisions.
    /// </summary>
    /// <param name="mode">The compound prediction mode.</param>
    /// <param name="secondary">The vector of the second reference.</param>
    /// <param name="secondaryModes">The single mode of the second component of each compound mode.</param>
    /// <param name="frameBounds">The full-pixel frame displacement region of the block.</param>
    /// <returns><see langword="true"/> when the entry can be searched.</returns>
    private static bool CompoundVectorsInFrameSearchBounds(
        Av1PredictionMode mode,
        Av1MotionVector secondary,
        ReadOnlySpan<Av1PredictionMode> secondaryModes,
        Rectangle frameBounds)
    {
        // The test of the first component is overwritten by the test of the second, so it has no effect.
        int modeIndex = (int)mode - (int)Av1PredictionMode.CompoundInterModeStart;
        return secondaryModes[modeIndex] == Av1PredictionMode.NewMotionVector || secondary.IsInFrameSearchBounds(frameBounds);
    }

    /// <summary>
    /// Measures compound mode and dynamic-reference-list syntax for one candidate.
    /// </summary>
    /// <param name="tables">The rate tables of the tile.</param>
    /// <param name="motionVectorCosts">The motion vector rates of the frame precision.</param>
    /// <param name="mode">The compound mode.</param>
    /// <param name="vector">The first vector.</param>
    /// <param name="secondaryVector">The second vector.</param>
    /// <param name="referenceMotionVectorIndex">The dynamic reference list index.</param>
    /// <param name="referenceMotionVectors">The reference vector list of the pair.</param>
    /// <returns>The syntax rate in 1/512-bit units.</returns>
    private static int GetCompoundInterModeRate(
        in Av1CoefficientTables tables,
        in Av1MotionVectorCosts motionVectorCosts,
        Av1PredictionMode mode,
        Av1MotionVector vector,
        Av1MotionVector secondaryVector,
        int referenceMotionVectorIndex,
        Av1ReferenceMotionVectors referenceMotionVectors)
    {
        int rate = Av1SymbolEncoder.GetInterCompoundModeCost(tables.ModeCosts, mode, referenceMotionVectors.ModeContext);
        bool usesNear = mode is Av1PredictionMode.NearNearMotionVector or
            Av1PredictionMode.NearNewMotionVector or
            Av1PredictionMode.NewNearMotionVector;

        bool usesNewNew = mode == Av1PredictionMode.NewNewMotionVector;
        if (usesNear)
        {
            // Compound modes containing NEARMV use the same one-based DRL walk as single-reference NEARMV.
            // The stored index remains zero-based from the first near pair, matching decoder reconstruction.
            for (int index = 1; index < 3 && referenceMotionVectors.Count > index + 1; index++)
            {
                bool advance = referenceMotionVectorIndex >= index;
                int context = Av1SymbolContextHelper.GetDrlContext(referenceMotionVectors.Weights, index);
                rate += Av1SymbolEncoder.GetDynamicReferenceListCost(tables.ModeCosts, advance, context);
                if (!advance)
                {
                    break;
                }
            }
        }
        else if (usesNewNew)
        {
            for (int index = 0; index < 2 && referenceMotionVectors.Count > index + 1; index++)
            {
                bool advance = referenceMotionVectorIndex > index;
                int context = Av1SymbolContextHelper.GetDrlContext(referenceMotionVectors.Weights, index);
                rate += Av1SymbolEncoder.GetDynamicReferenceListCost(tables.ModeCosts, advance, context);
                if (!advance)
                {
                    break;
                }
            }
        }

        return rate + GetCompoundMotionRate(in motionVectorCosts, mode, vector, secondaryVector, referenceMotionVectorIndex, referenceMotionVectors);
    }

    /// <summary>
    /// Measures differential motion syntax for the searched components of a compound mode.
    /// </summary>
    /// <param name="motionVectorCosts">The motion vector rates of the frame precision.</param>
    /// <param name="mode">The compound mode.</param>
    /// <param name="vector">The first vector.</param>
    /// <param name="secondaryVector">The second vector.</param>
    /// <param name="referenceMotionVectorIndex">The dynamic reference list index.</param>
    /// <param name="referenceMotionVectors">The reference vector list of the pair.</param>
    /// <returns>The differential vector rate in 1/512-bit units.</returns>
    private static int GetCompoundMotionRate(
        in Av1MotionVectorCosts motionVectorCosts,
        Av1PredictionMode mode,
        Av1MotionVector vector,
        Av1MotionVector secondaryVector,
        int referenceMotionVectorIndex,
        Av1ReferenceMotionVectors referenceMotionVectors)
    {
        int rate = 0;
        bool usesNear = mode is Av1PredictionMode.NearNearMotionVector or
            Av1PredictionMode.NearNewMotionVector or Av1PredictionMode.NewNearMotionVector;

        int newReferenceIndex = usesNear ? referenceMotionVectorIndex + 1 : referenceMotionVectorIndex;
        if (mode is Av1PredictionMode.NewNearestMotionVector or
            Av1PredictionMode.NewNearMotionVector or
            Av1PredictionMode.NewNewMotionVector)
        {
            Av1MotionVector reference = referenceMotionVectors.GetCompoundNewReference(newReferenceIndex, 0);
            rate += ((motionVectorCosts.GetCost(vector, reference) * 108) + 64) >> 7;
        }

        if (mode is Av1PredictionMode.NearestNewMotionVector or
            Av1PredictionMode.NearNewMotionVector or
            Av1PredictionMode.NewNewMotionVector)
        {
            Av1MotionVector reference = referenceMotionVectors.GetCompoundNewReference(newReferenceIndex, 1);
            rate += ((motionVectorCosts.GetCost(secondaryVector, reference) * 108) + 64) >> 7;
        }

        return rate;
    }

    /// <summary>
    /// Returns whether every new vector of a compound mode differs from its reference vector.
    /// </summary>
    /// <param name="mode">The compound mode.</param>
    /// <param name="vector">The first vector.</param>
    /// <param name="secondaryVector">The second vector.</param>
    /// <param name="referenceMotionVectorIndex">The dynamic reference list index.</param>
    /// <param name="referenceMotionVectors">The reference vector list of the pair.</param>
    /// <returns><see langword="false"/> when a new vector equals its reference vector.</returns>
    private static bool HasCodedCompoundNewVectors(
        Av1PredictionMode mode,
        Av1MotionVector vector,
        Av1MotionVector secondaryVector,
        int referenceMotionVectorIndex,
        Av1ReferenceMotionVectors referenceMotionVectors)
    {
        int newReferenceIndex = mode is Av1PredictionMode.NearNewMotionVector or Av1PredictionMode.NewNearMotionVector
            ? referenceMotionVectorIndex + 1
            : referenceMotionVectorIndex;

        if (mode is Av1PredictionMode.NewNewMotionVector or Av1PredictionMode.NewNearestMotionVector or Av1PredictionMode.NewNearMotionVector &&
            vector == referenceMotionVectors.GetCompoundNewReference(newReferenceIndex, 0))
        {
            return false;
        }

        return !(mode is Av1PredictionMode.NewNewMotionVector or Av1PredictionMode.NearestNewMotionVector or Av1PredictionMode.NearNewMotionVector &&
            secondaryVector == referenceMotionVectors.GetCompoundNewReference(newReferenceIndex, 1));
    }

    /// <summary>
    /// Selects asymmetric trials from preceding costs, the partition model and child decisions.
    /// </summary>
    /// <param name="partitionSettings">The existing speed rules.</param>
    /// <param name="blockSize">The current square block size.</param>
    /// <param name="selectedPartition">The winner before the asymmetric stage.</param>
    /// <param name="bestCost">The cost of that winner.</param>
    /// <param name="parentSourceVariance">The variance of the complete parent block.</param>
    /// <param name="interSourceVariance">The variance left by the last coded block.</param>
    /// <param name="blockQIndex">The quantizer left by the preceding trial.</param>
    /// <param name="nodeIndex">The node in the retained partition tree.</param>
    /// <param name="rectangleWins">The winning rectangle directions of this node.</param>
    /// <param name="partitionTypes">The retained partition types, in tree order.</param>
    /// <param name="splitNoneCosts">The unsplit costs of the four square children.</param>
    /// <param name="horizontalCosts">The costs of the two horizontal leaves.</param>
    /// <param name="verticalCosts">The costs of the two vertical leaves.</param>
    /// <param name="rectanglesAllowed">Whether both rectangle directions are legal at this point.</param>
    /// <returns>One bit for each permitted asymmetric partition.</returns>
    private static int GetAsymmetricPartitionMask(
        Av1EncoderSpeedSettings partitionSettings,
        Av1BlockSize blockSize,
        Av1PartitionType selectedPartition,
        long bestCost,
        int parentSourceVariance,
        int interSourceVariance,
        int blockQIndex,
        int nodeIndex,
        byte rectangleWins,
        ReadOnlySpan<byte> partitionTypes,
        Span<long> splitNoneCosts,
        Span<long> horizontalCosts,
        Span<long> verticalCosts,
        bool rectanglesAllowed)
    {
        // Compare all four asymmetric candidates against the same incoming winner and costs.
        // Invalid or unvisited component searches contribute zero to this lower-cost estimate.
        int pruningLevel = partitionSettings.ExtendedPartitionPruningLevel;
        bool flatNone = pruningLevel == 1 && selectedPartition == Av1PartitionType.None &&
            parentSourceVariance < 32;

        bool allowHorizontal = selectedPartition is Av1PartitionType.Horizontal or Av1PartitionType.Split || flatNone;
        bool allowVertical = selectedPartition is Av1PartitionType.Vertical or Av1PartitionType.Split || flatNone;

        // Level one discounts the component costs by 14/16. Other levels use 15/16.
        // Divide before multiplying to retain the integer rounding used by the partition search.
        int weight = pruningLevel == 1 ? 14 : 15;
        for (int child = 0; child < 4; child++)
        {
            if (splitNoneCosts[child] == long.MaxValue)
            {
                splitNoneCosts[child] = 0;
            }
        }

        for (int child = 0; child < 2; child++)
        {
            if (horizontalCosts[child] == long.MaxValue)
            {
                horizontalCosts[child] = 0;
            }

            if (verticalCosts[child] == long.MaxValue)
            {
                verticalCosts[child] = 0;
            }
        }

        int asymmetricMask = 0;
        if (allowHorizontal)
        {
            if (((horizontalCosts[1] + splitNoneCosts[0] + splitNoneCosts[1]) / 16 * weight) < bestCost)
            {
                asymmetricMask |= 1;
            }

            if (((horizontalCosts[0] + splitNoneCosts[2] + splitNoneCosts[3]) / 16 * weight) < bestCost)
            {
                asymmetricMask |= 2;
            }
        }

        if (allowVertical)
        {
            if (((verticalCosts[1] + splitNoneCosts[0] + splitNoneCosts[2]) / 16 * weight) < bestCost)
            {
                asymmetricMask |= 4;
            }

            if (((verticalCosts[0] + splitNoneCosts[1] + splitNoneCosts[3]) / 16 * weight) < bestCost)
            {
                asymmetricMask |= 8;
            }
        }

        if (rectanglesAllowed)
        {
            // The classifier selects its own mask from all sixteen classes, replacing the
            // preliminary cost mask rather than intersecting away classes it permits.
            asymmetricMask = ClassifyAsymmetricPartitions(
                interSourceVariance,
                blockSize,
                selectedPartition,
                bestCost,
                horizontalCosts,
                verticalCosts,
                splitNoneCosts,
                asymmetricMask);
        }

        if (partitionSettings.ChildPartitionPruningLevel >= 2)
        {
            int requiredWins = Math.Min(3 * (2 * (255 - blockQIndex) / 255), 3);
            for (int shape = 0; shape < 4; shape++)
            {
                int firstChild = shape < 2 ? shape * 2 : shape - 2;
                int secondChild = firstChild + (shape < 2 ? 1 : 2);
                int directionBit = shape < 2 ? 1 : 2;

                // The superblock root has no rectangle win record. At the root, a win means that the current best partition is
                // that rectangle.
                int wins = nodeIndex == 0
                    ? (selectedPartition == (shape < 2 ? Av1PartitionType.Horizontal : Av1PartitionType.Vertical) ? 1 : 0)
                    : (rectangleWins & directionBit) != 0 ? 1 : 0;

                Av1PartitionType firstPartition =
                    (Av1PartitionType)partitionTypes[(nodeIndex * 4) + firstChild + 1];

                Av1PartitionType secondPartition =
                    (Av1PartitionType)partitionTypes[(nodeIndex * 4) + secondChild + 1];

                // Unvisited children retain their initial unsplit choice for this decision.
                wins += firstPartition is Av1PartitionType.None or Av1PartitionType.Invalid ? 1 : 0;
                wins += secondPartition is Av1PartitionType.None or Av1PartitionType.Invalid ? 1 : 0;
                if (wins < requiredWins)
                {
                    asymmetricMask &= ~(1 << shape);
                }
            }
        }

        return asymmetricMask;
    }

    /// <summary>
    /// Reuses leading leaves whose geometry and reconstructed neighbors match an earlier trial.
    /// </summary>
    /// <param name="partitionTree">The existing retained candidate storage.</param>
    /// <param name="partitionTypes">The partition types retained by earlier trials.</param>
    /// <param name="nodeIndex">The current tree node.</param>
    /// <param name="partitionType">The asymmetric candidate about to run.</param>
    private static void ReuseAsymmetricPartitionContexts(
        Av1EncoderPartitionTree partitionTree,
        ReadOnlySpan<byte> partitionTypes,
        int nodeIndex,
        Av1PartitionType partitionType)
    {
        bool squareFirst = partitionType is Av1PartitionType.HorizontalA or Av1PartitionType.VerticalA;
        int sourceNode = squareFirst ? (nodeIndex * 4) + 1 : nodeIndex;
        Av1PartitionType sourcePartition = squareFirst
            ? Av1PartitionType.None
            : partitionType == Av1PartitionType.HorizontalB ? Av1PartitionType.Horizontal : Av1PartitionType.Vertical;

        bool firstAvailable = !squareFirst ||
            (Av1PartitionType)partitionTypes[sourceNode] == Av1PartitionType.None;

        if (firstAvailable)
        {
            Av1EncoderPartitionTree.ModeContext previous =
                partitionTree.GetContext(sourceNode, sourcePartition, 0);

            if (previous.Snapshot.Ready)
            {
                // These leading leaves have the same geometry and already reconstructed neighbors. The reuse excludes the palette
                // and CfL choices, because their inputs depend on the reconstruction and palette contexts of the partition around
                // them.
                partitionTree.GetContext(nodeIndex, partitionType, 0).CopyFrom(previous, partitionType);
                int secondNode = (nodeIndex * 4) + 2;
                if (partitionType == Av1PartitionType.HorizontalA &&
                    (Av1PartitionType)partitionTypes[secondNode] == Av1PartitionType.None)
                {
                    Av1EncoderPartitionTree.ModeContext second =
                        partitionTree.GetContext(secondNode, Av1PartitionType.None, 0);

                    if (second.Snapshot.Ready)
                    {
                        partitionTree.GetContext(nodeIndex, partitionType, 1).CopyFrom(second, partitionType);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Removes strip directions that too few square children selected.
    /// </summary>
    /// <param name="blockQIndex">The quantizer left by the preceding trial.</param>
    /// <param name="childRectangleWins">The rectangle direction bits of the four square children.</param>
    /// <param name="fourStripMask">The directions retained by the preceding classifier.</param>
    /// <returns>The remaining direction bits: horizontal at bit zero, vertical at bit one.</returns>
    private static int PruneFourStripPartitions(int blockQIndex, ReadOnlySpan<byte> childRectangleWins, int fourStripMask)
    {
        // Lower quantizers require more children to favor the direction. Bits zero and one record horizontal and vertical wins.
        int requiredWins = Math.Min((3 * (255 - blockQIndex) / 255) + 1, 3);
        int horizontalWins = 0;
        int verticalWins = 0;
        for (int child = 0; child < 4; child++)
        {
            horizontalWins += childRectangleWins[child] & 1;
            verticalWins += (childRectangleWins[child] >> 1) & 1;
        }

        if (horizontalWins < requiredWins)
        {
            fourStripMask &= 2;
        }

        if (verticalWins < requiredWins)
        {
            fourStripMask &= 1;
        }

        return fourStripMask;
    }

    /// <summary>
    /// Selects rectangle exclusions from the unsplit intra mode and neighboring block sizes.
    /// </summary>
    /// <param name="noneMode">The mode retained by the unsplit trial.</param>
    /// <param name="blockSize">The current square block size.</param>
    /// <param name="macroBlock">The current neighbor availability and grid position.</param>
    /// <param name="modeInfoGrid">The existing allocation-index grid.</param>
    /// <param name="modeInfoAllocation">The existing neighbor mode information.</param>
    /// <returns>Whether to exclude horizontal and vertical rectangles.</returns>
    private static (bool Horizontal, bool Vertical) GetRectangularPartitionPruning(
        Av1PredictionMode noneMode,
        Av1BlockSize blockSize,
        Av1MacroBlockD macroBlock,
        ReadOnlySpan<int> modeInfoGrid,
        Span<Av1MacroBlockModeInfo> modeInfoAllocation)
    {
        if (noneMode is Av1PredictionMode.DC or Av1PredictionMode.Smooth)
        {
            bool largerLeft = macroBlock.IsLeftAvailable &&
                GetBlockArea(macroBlock.GetRelativeModeInfo(modeInfoGrid, modeInfoAllocation, -1).Block.BlockSize) > GetBlockArea(blockSize);

            bool largerAbove = macroBlock.IsUpAvailable &&
                GetBlockArea(macroBlock.GetRelativeModeInfo(modeInfoGrid, modeInfoAllocation, -macroBlock.ModeInfoStride).Block.BlockSize) >
                GetBlockArea(blockSize);

            bool pruneHorizontalRectangle = largerLeft || largerAbove;

            return (pruneHorizontalRectangle, pruneHorizontalRectangle);
        }
        else
        {
            bool pruneHorizontalRectangle = noneMode is
                Av1PredictionMode.Directional67Degrees or
                Av1PredictionMode.Vertical or
                Av1PredictionMode.Directional113Degrees;

            bool pruneVerticalRectangle = noneMode is
                Av1PredictionMode.Directional157Degrees or
                Av1PredictionMode.Horizontal or
                Av1PredictionMode.Directional203Degrees;

            return (pruneHorizontalRectangle, pruneVerticalRectangle);
        }
    }

    /// <summary>
    /// Returns the number of luma samples in a block size.
    /// </summary>
    /// <param name="blockSize">The block size.</param>
    /// <returns>The width times the height.</returns>
    private static int GetBlockArea(Av1BlockSize blockSize)
        => blockSize.GetWidth() * blockSize.GetHeight();

    /// <summary>
    /// Resets single-reference syntax and fills the dynamic-reference-list candidates in coding order.
    /// </summary>
    /// <param name="frameHeader">The existing frame header.</param>
    /// <param name="blockSize">The block size.</param>
    /// <param name="modeInfoPosition">The block origin in four-sample units.</param>
    /// <param name="lumaTransformSize">The largest permitted luma transform.</param>
    /// <param name="referenceFrame">The reference selected by this trial.</param>
    /// <param name="requestedMode">The motion-vector syntax mode to search.</param>
    /// <param name="referenceMotionVectors">The reference-vector list, passed by reference to avoid copying its inline storage.</param>
    /// <param name="candidateVectors">Caller-owned storage for at most three candidate vectors.</param>
    /// <param name="candidateReferenceIndices">Caller-owned storage for their coded list indices.</param>
    /// <param name="modeInfo">The reusable mode decisions to reset.</param>
    /// <param name="block">The reusable block syntax state to reset.</param>
    /// <returns>The number of populated candidates.</returns>
    private static int PrepareSingleReferenceCandidates(
        ObuFrameHeader frameHeader,
        Av1BlockSize blockSize,
        Point modeInfoPosition,
        Av1TransformSize lumaTransformSize,
        Av1ReferenceFrameType referenceFrame,
        Av1PredictionMode requestedMode,
        ref Av1ReferenceMotionVectors referenceMotionVectors,
        Span<Av1MotionVector> candidateVectors,
        Span<byte> candidateReferenceIndices,
        ref Av1MacroBlockModeInfo modeInfo,
        ref Av1EncoderBlockStruct block)
    {
        // A single-reference candidate carries none of the compound syntax, and the block is
        // reused across candidates, so every compound field is returned to its default here
        // rather than where a compound candidate happens to have set it. The same applies to
        // the intra fields: an inter block signals no angle delta, no filter-intra mode and no
        // chroma-from-luma parameters.
        modeInfo.Block.ReferenceFrame = referenceFrame;
        modeInfo.Block.SecondaryReferenceFrame = Av1ReferenceFrameType.None;
        modeInfo.Block.CompoundGroupIndex = false;
        modeInfo.Block.CompoundIndex = true;
        modeInfo.Block.CompoundType = Av1CompoundType.Average;
        modeInfo.Block.CompoundWedgeIndex = 0;
        modeInfo.Block.CompoundWedgeSign = false;
        modeInfo.Block.DifferenceWeightedMaskType = Av1DifferenceWeightedMaskType.Type38;
        modeInfo.Block.UvMode = Av1ChromaPredictionMode.DC;
        modeInfo.Block.TransformSize = lumaTransformSize;
        modeInfo.Block.UseIntraBlockCopy = false;
        modeInfo.Block.MotionMode = Av1MotionMode.SimpleTranslation;
        block.FilterIntraMode = Av1FilterIntraMode.AllFilterIntraModes;
        block.PredictionUnit.AngleDelta[(int)Av1PlaneType.Y] = 0;
        block.PredictionUnit.AngleDelta[(int)Av1PlaneType.Uv] = 0;
        block.PredictionUnit.ChromaFromLumaIndex = 0;
        block.PredictionUnit.ChromaFromLumaSigns = 0;

        // The global motion of a reference is a warp model, so the vector that it contributes depends on the position of the block.
        Av1MotionVector globalMotion = frameHeader
            .GetGlobalMotionParameters()[(int)referenceFrame - (int)Av1ReferenceFrameType.Last]
            .GetMotionVector(
                frameHeader.AllowHighPrecisionMotionVector,
                blockSize,
                modeInfoPosition,
                frameHeader.ForceIntegerMotionVector);

        int candidateCount = requestedMode switch
        {
            Av1PredictionMode.NewMotionVector => Math.Min(3, Math.Max(1, referenceMotionVectors.Count)),
            Av1PredictionMode.NearMotionVector => Math.Min(3, Math.Max(1, referenceMotionVectors.Count - 1)),
            _ => 1
        };

        for (int referenceIndex = 0; referenceIndex < candidateCount; referenceIndex++)
        {
            candidateReferenceIndices[referenceIndex] = (byte)referenceIndex;
            candidateVectors[referenceIndex] = requestedMode switch
            {
                Av1PredictionMode.NearestMotionVector => referenceMotionVectors.Nearest,
                Av1PredictionMode.NewMotionVector => referenceMotionVectors.GetNewReference(referenceIndex),
                Av1PredictionMode.NearMotionVector => referenceMotionVectors.GetNearReference(referenceIndex),
                _ => globalMotion
            };
        }

        return candidateCount;
    }

    /// <summary>
    /// Removes invalid list vectors and applies the existing reference-index pruning rules.
    /// </summary>
    /// <param name="parent">The existing frame coding state and speed rules.</param>
    /// <param name="blockQIndex">The current block quantizer.</param>
    /// <param name="requestedMode">The motion-vector syntax mode.</param>
    /// <param name="referenceFrame">The selected reference.</param>
    /// <param name="referenceMotionVectors">The coded vector-list weights, passed by reference to avoid copying their inline storage.</param>
    /// <param name="frameBounds">The permitted motion-search rectangle.</param>
    /// <param name="candidateVectors">The populated vectors in list order.</param>
    /// <param name="candidateReferenceIndices">The corresponding coded list indices.</param>
    /// <returns>One retained bit for each populated candidate.</returns>
    private static int PruneSingleReferenceCandidates(
        Av1PictureParentControlSet parent,
        int blockQIndex,
        Av1PredictionMode requestedMode,
        Av1ReferenceFrameType referenceFrame,
        ref Av1ReferenceMotionVectors referenceMotionVectors,
        Rectangle frameBounds,
        ReadOnlySpan<Av1MotionVector> candidateVectors,
        ReadOnlySpan<byte> candidateReferenceIndices)
    {
        int candidateCount = candidateVectors.Length;
        int candidateMask = (1 << candidateCount) - 1;

        // A list or global vector that points beyond the frame displacement region is not searched, and it is not modeled among the
        // near entries either. A new-motion entry searches from its reference instead.
        if (requestedMode != Av1PredictionMode.NewMotionVector)
        {
            for (int index = 0; index < candidateCount; index++)
            {
                if (!candidateVectors[index].IsInFrameSearchBounds(frameBounds))
                {
                    candidateMask &= ~(1 << index);
                }
            }
        }

        int reductionLevel = parent.SpeedSettings.ReduceInterReferenceIndices;
        if (reductionLevel != 0 && candidateCount > 1)
        {
            bool secondaryPastReference = referenceFrame is Av1ReferenceFrameType.Last2 or Av1ReferenceFrameType.Last3;
            bool distantNewReference = reductionLevel >= 2 && requestedMode == Av1PredictionMode.NewMotionVector &&
                referenceFrame != parent.NearestPastReference && referenceFrame != parent.NearestFutureReference;

            for (int index = 1; index < candidateCount; index++)
            {
                int weightIndex = candidateReferenceIndices[index] + (requestedMode == Av1PredictionMode.NearMotionVector ? 1 : 0);
                bool pruneByQuantizer = reductionLevel >= 3 ||
                    candidateReferenceIndices[index] >= (blockQIndex * 3 / 256) + 1;

                if ((secondaryPastReference || (distantNewReference && pruneByQuantizer)) &&
                    referenceMotionVectors.Weights[weightIndex] < Av1ReferenceMotionVectors.NearestCandidateWeight)
                {
                    candidateMask &= ~(1 << index);
                }
            }
        }

        return candidateMask;
    }

    /// <content>
    /// Runs candidate ordering, pruning, recursion and winner selection without native sample arithmetic.
    /// </content>
    internal readonly ref partial struct SearchControl
    {
        /// <summary>
        /// Runs <see cref="SelectBestPartitionCore"/> at the rate multiplier of the block.
        /// </summary>
        /// <param name="writer">The symbol encoder that prices the syntax.</param>
        /// <param name="tables">The rate tables of the tile.</param>
        /// <param name="modeWorkspace">The mode decision buffers of the block.</param>
        /// <param name="transformCoefficients">The forward transform output buffer.</param>
        /// <param name="dequantizedCoefficients">The dequantized coefficient buffer of the candidate.</param>
        /// <param name="searchDequantizedCoefficients">The dequantized coefficient buffer of the winner.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
        /// <param name="transformTypeProbabilities">The transform type probabilities of every update type and size.</param>
        /// <param name="blockResidual">The residual buffer of the motion search.</param>
        /// <param name="searchCoefficients">The quantized coefficient buffer of the winner.</param>
        /// <param name="searchReconstructions">The storage of the candidate and winner reconstructions of the type search.</param>
        /// <param name="estimationRowCoefficients">The coefficients of one row of estimation transforms.</param>
        /// <param name="interWorkspace">The inter prediction buffers of the block.</param>
        /// <param name="motionSearchPrediction">The prediction buffer of the motion search.</param>
        /// <param name="motionVectorCosts">The motion vector rates of the frame precision.</param>
        /// <param name="transformPrediction">The prediction storage of one transform block.</param>
        /// <param name="interIntraAbove">The extended above edge of an inter-intra prediction.</param>
        /// <param name="interIntraLeft">The extended left edge of an inter-intra prediction.</param>
        /// <param name="firstIntermediate">The compound intermediate of the first reference.</param>
        /// <param name="secondIntermediate">The compound intermediate of the second reference.</param>
        /// <param name="compoundMask">The blend mask of a masked compound prediction.</param>
        /// <param name="partitionEdges">The partition context edges of the tile.</param>
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
        /// <param name="macroBlock">The neighbor availability of the block.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="nodeIndex">The partition tree node of the block.</param>
        /// <param name="costLimit">The cost above which the search stops.</param>
        /// <param name="selectedStatistics">The rate and distortion of the selected partition.</param>
        /// <param name="noneCost">The cost of the unsplit block.</param>
        /// <param name="rectangleWins">Bit zero when the horizontal halves win, and bit one when the vertical halves win.</param>
        /// <returns>The selected partition type.</returns>
        public Av1PartitionType SelectBestPartition(
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
            Span<int> estimationRowCoefficients,
            in Av1EncoderInterPredictionWorkspace<byte> interWorkspace,
            Span<byte> motionSearchPrediction,
            in Av1MotionVectorCosts motionVectorCosts,
            Span<byte> transformPrediction,
            Span<byte> interIntraAbove,
            Span<byte> interIntraLeft,
            Span<ushort> firstIntermediate,
            Span<ushort> secondIntermediate,
            Span<byte> compoundMask,
            in Av1NeighborEdges<Av1PartitionContext> partitionEdges,
            in Av1NeighborEdges<byte> transformEdges,
            in Av1NeighborEdges<Av1EncoderPaletteInfo> paletteEdges,
            in Av1NeighborEdges<byte> lumaCoefficientEdges,
            in Av1NeighborEdges<byte> blueCoefficientEdges,
            in Av1NeighborEdges<byte> redCoefficientEdges,
            Span<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            Span<Av1EncoderDisplacementVector> displacementVectors,
            Span<Av1EncoderReferenceContext> referenceContexts,
            Span<byte> encoderSegmentMap,
            Span<byte> searchSegmentMap,
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
            int nodeIndex,
            Av1RateDistortionStatistics costLimit,
            out Av1RateDistortionStatistics selectedStatistics,
            out long noneCost,
            out byte rectangleWins)
        {
            // A negative bound skips the block setup.
            int savedRateMultiplier = this.state.RateMultiplier;
            if (costLimit.Cost >= 0)
            {
                this.SetBlockSegment(encoderSegmentMap, previousSegmentMap, blockOrigin, blockSize);
            }

            this.state.RateMultiplier = costLimit.Cost < 0
                ? this.GetBlockRateMultiplier(blockOrigin, blockSize, segmentId: -1)
                : this.SetupBlockRateMultiplier(blockOrigin, blockSize, segmentId: -1);

            // The bound arrives at the multiplier of the parent, so its cost is measured again at the multiplier of this block.
            costLimit.UpdateCost(this.state.RateMultiplier);

            // A 16x16 node of a frame that refreshes its variance segments measures the segment that its smaller blocks take.
            if (costLimit.Cost >= 0 && blockSize == Av1BlockSize.Block16x16 && this.state.Picture.Parent.VarianceSegmentRefresh)
            {
                this.state.BlockWorkspace.MacroblockEnergy = (int)this.GetLogBlockVariance(
                    workspaceStorage, sourceLuma, sourceBlue, sourceRed, blockOrigin, blockSize);
            }

            Av1PartitionType result = this.SelectBestPartitionCore(
                writer,
                in tables,
                in modeWorkspace,
                transformCoefficients,
                dequantizedCoefficients,
                searchDequantizedCoefficients,
                transformWorkspace,
                transformTypeProbabilities,
                blockResidual,
                searchCoefficients,
                searchReconstructions,
                estimationRowCoefficients,
                in interWorkspace,
                motionSearchPrediction,
                in motionVectorCosts,
                transformPrediction,
                interIntraAbove,
                interIntraLeft,
                firstIntermediate,
                secondIntermediate,
                compoundMask,
                in partitionEdges,
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
                searchSegmentMap,
                previousSegmentMap,
                superblockCoefficients,
                workspaceStorage,
                sourceLuma,
                sourceBlue,
                sourceRed,
                reconstructionLuma,
                reconstructionBlue,
                reconstructionRed,
                macroBlock,
                blockOrigin,
                blockSize,
                nodeIndex,
                costLimit,
                out selectedStatistics,
                out noneCost,
                out rectangleWins);

            this.state.RateMultiplier = savedRateMultiplier;
            return result;
        }

        /// <summary>
        /// Runs <see cref="EvaluatePartitionLeafCore"/> at the rate multiplier of the block.
        /// </summary>
        /// <param name="writer">The symbol encoder that prices the syntax.</param>
        /// <param name="tables">The rate tables of the tile.</param>
        /// <param name="modeWorkspace">The mode decision buffers of the block.</param>
        /// <param name="transformCoefficients">The forward transform output buffer.</param>
        /// <param name="dequantizedCoefficients">The dequantized coefficient buffer of the candidate.</param>
        /// <param name="searchDequantizedCoefficients">The dequantized coefficient buffer of the winner.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
        /// <param name="transformTypeProbabilities">The transform type probabilities of every update type and size.</param>
        /// <param name="blockResidual">The residual buffer of the motion search.</param>
        /// <param name="searchCoefficients">The quantized coefficient buffer of the winner.</param>
        /// <param name="searchReconstructions">The storage of the candidate and winner reconstructions of the type search.</param>
        /// <param name="estimationRowCoefficients">The coefficients of one row of estimation transforms.</param>
        /// <param name="interWorkspace">The inter prediction buffers of the block.</param>
        /// <param name="motionSearchPrediction">The prediction buffer of the motion search.</param>
        /// <param name="motionVectorCosts">The motion vector rates of the frame precision.</param>
        /// <param name="transformPrediction">The prediction storage of one transform block.</param>
        /// <param name="interIntraAbove">The extended above edge of an inter-intra prediction.</param>
        /// <param name="interIntraLeft">The extended left edge of an inter-intra prediction.</param>
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
        /// <param name="macroBlock">The neighbor availability of the block.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="partitionType">The partition type that contains the leaf.</param>
        /// <param name="context">The context that keeps the winner of the leaf.</param>
        /// <param name="costLimit">The cost above which the search stops.</param>
        /// <param name="publishContexts">Whether the winner writes the neighbor contexts.</param>
        /// <param name="publishCoefficientContexts">Whether the winner writes the coefficient contexts.</param>
        /// <param name="intraEncodeFollows">Whether an encode of an intra winner follows, which writes its own coefficient contexts.</param>
        /// <returns>The rate and distortion of the leaf.</returns>
        public Av1RateDistortionStatistics EvaluatePartitionLeaf(
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
            Span<int> estimationRowCoefficients,
            in Av1EncoderInterPredictionWorkspace<byte> interWorkspace,
            Span<byte> motionSearchPrediction,
            in Av1MotionVectorCosts motionVectorCosts,
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
            Span<byte> encoderSegmentMap,
            Span<byte> searchSegmentMap,
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
            Av1PartitionType partitionType,
            Av1EncoderPartitionTree.ModeContext context,
            long costLimit,
            bool publishContexts,
            bool publishCoefficientContexts,
            bool intraEncodeFollows)
        {
            // A bound that is already negative returns before the block setup, so neither the block variance nor the error per bit changes.
            if (costLimit == NegativeLeafBound)
            {
                return Av1RateDistortionStatistics.Invalid;
            }

            // The block takes its segment, and the variance and complexity modes price it at the segment quantizer. A frame that refreshes
            // its variance segments places the block by its source variance, but still searches it with the quantizer of segment 0.
            this.SetBlockSegment(encoderSegmentMap, previousSegmentMap, blockOrigin, blockSize);
            int rateSegmentId = -1;
            Av1AdaptiveQuantizationMode adaptiveQuantization = this.state.Picture.Parent.EncoderOptions.AdaptiveQuantizationMode;
            if (adaptiveQuantization == Av1AdaptiveQuantizationMode.Variance)
            {
                if (this.state.Picture.Parent.VarianceSegmentRefresh)
                {
                    this.state.BlockSegmentId = this.GetVarianceSegmentId(workspaceStorage, sourceLuma, sourceBlue, sourceRed, blockOrigin, blockSize);
                }

                rateSegmentId = this.state.BlockSegmentId;
            }
            else if (adaptiveQuantization is Av1AdaptiveQuantizationMode.Complexity or Av1AdaptiveQuantizationMode.CyclicRefresh)
            {
                rateSegmentId = this.state.BlockSegmentId;
            }

            // The search sets the motion vector error per bit from the block multiplier.
            int savedRateMultiplier = this.state.RateMultiplier;
            this.state.RateMultiplier = this.GetBlockRateMultiplier(blockOrigin, blockSize, rateSegmentId);
            this.state.BlockWorkspace.ErrorPerBitRateMultiplier = this.state.RateMultiplier;
            Av1RateDistortionStatistics result = this.EvaluatePartitionLeafCore(
                writer,
                in tables,
                in modeWorkspace,
                transformCoefficients,
                dequantizedCoefficients,
                searchDequantizedCoefficients,
                transformWorkspace,
                transformTypeProbabilities,
                blockResidual,
                searchCoefficients,
                searchReconstructions,
                estimationRowCoefficients,
                in interWorkspace,
                motionSearchPrediction,
                in motionVectorCosts,
                transformPrediction,
                interIntraAbove,
                interIntraLeft,
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
                searchSegmentMap,
                previousSegmentMap,
                superblockCoefficients,
                workspaceStorage,
                sourceLuma,
                sourceBlue,
                sourceRed,
                reconstructionLuma,
                reconstructionBlue,
                reconstructionRed,
                macroBlock,
                blockOrigin,
                blockSize,
                partitionType,
                context,
                costLimit,
                publishContexts,
                publishCoefficientContexts,
                intraEncodeFollows);

            this.state.RateMultiplier = savedRateMultiplier;

            // Complexity adaptive quantization places each searched block of 16x16 or larger by its rate. The block size order puts the 4:1
            // sizes after 128x128, so this test also includes them.
            if (result.Rate != int.MaxValue && blockSize >= Av1BlockSize.Block16x16 &&
                adaptiveQuantization == Av1AdaptiveQuantizationMode.Complexity && this.state.Picture.Parent.ComplexitySegmentRefresh)
            {
                this.SelectComplexitySegment(encoderSegmentMap, workspaceStorage, sourceLuma, sourceBlue, sourceRed, blockOrigin, blockSize, result.Rate);
            }

            return result;
        }

        /// <summary>
        /// Evaluates one partition type of a block leaf by leaf and adds the partition syntax. The evaluation stops at
        /// the first leaf whose accumulated cost passes the limit.
        /// </summary>
        /// <param name="writer">The symbol encoder that prices the syntax.</param>
        /// <param name="tables">The rate tables of the tile.</param>
        /// <param name="modeWorkspace">The mode decision buffers of the block.</param>
        /// <param name="transformCoefficients">The forward transform output buffer.</param>
        /// <param name="dequantizedCoefficients">The dequantized coefficient buffer of the candidate.</param>
        /// <param name="searchDequantizedCoefficients">The dequantized coefficient buffer of the winner.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
        /// <param name="transformTypeProbabilities">The transform type probabilities of every update type and size.</param>
        /// <param name="blockResidual">The residual buffer of the motion search.</param>
        /// <param name="searchCoefficients">The quantized coefficient buffer of the winner.</param>
        /// <param name="searchReconstructions">The storage of the candidate and winner reconstructions of the type search.</param>
        /// <param name="estimationRowCoefficients">The coefficients of one row of estimation transforms.</param>
        /// <param name="interWorkspace">The inter prediction buffers of the block.</param>
        /// <param name="motionSearchPrediction">The prediction buffer of the motion search.</param>
        /// <param name="motionVectorCosts">The motion vector rates of the frame precision.</param>
        /// <param name="transformPrediction">The prediction storage of one transform block.</param>
        /// <param name="interIntraAbove">The extended above edge of an inter-intra prediction.</param>
        /// <param name="interIntraLeft">The extended left edge of an inter-intra prediction.</param>
        /// <param name="firstIntermediate">The compound intermediate of the first reference.</param>
        /// <param name="secondIntermediate">The compound intermediate of the second reference.</param>
        /// <param name="compoundMask">The blend mask of a masked compound prediction.</param>
        /// <param name="partitionEdges">The partition context edges of the tile.</param>
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
        /// <param name="macroBlock">The neighbor availability of the block.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="partitionType">The partition type to evaluate.</param>
        /// <param name="nodeIndex">The partition tree node of the block.</param>
        /// <param name="costLimit">The cost above which the evaluation stops.</param>
        /// <param name="searchChildren">Whether each square leaf searches its own partition.</param>
        /// <param name="publishFinalContexts">Whether the last leaf writes its partition contexts.</param>
        /// <param name="childCosts">The cost of each leaf, or empty.</param>
        /// <param name="childRectangleWins">The rectangle wins of each split leaf, or empty.</param>
        /// <param name="stoppedAtLeaf">The leaf at which the evaluation stopped, or the leaf count when it finished.</param>
        /// <param name="accumulatedCost">The cost of the evaluated leaves, or the maximum when a leaf failed.</param>
        /// <returns>The rate and distortion of the partition.</returns>
        public Av1RateDistortionStatistics EvaluatePartitionCandidate(
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
            Span<int> estimationRowCoefficients,
            in Av1EncoderInterPredictionWorkspace<byte> interWorkspace,
            Span<byte> motionSearchPrediction,
            in Av1MotionVectorCosts motionVectorCosts,
            Span<byte> transformPrediction,
            Span<byte> interIntraAbove,
            Span<byte> interIntraLeft,
            Span<ushort> firstIntermediate,
            Span<ushort> secondIntermediate,
            Span<byte> compoundMask,
            in Av1NeighborEdges<Av1PartitionContext> partitionEdges,
            in Av1NeighborEdges<byte> transformEdges,
            in Av1NeighborEdges<Av1EncoderPaletteInfo> paletteEdges,
            in Av1NeighborEdges<byte> lumaCoefficientEdges,
            in Av1NeighborEdges<byte> blueCoefficientEdges,
            in Av1NeighborEdges<byte> redCoefficientEdges,
            Span<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            Span<Av1EncoderDisplacementVector> displacementVectors,
            Span<Av1EncoderReferenceContext> referenceContexts,
            Span<byte> encoderSegmentMap,
            Span<byte> searchSegmentMap,
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
            Av1PartitionType partitionType,
            int nodeIndex,
            Av1RateDistortionStatistics costLimit,
            bool searchChildren,
            bool publishFinalContexts,
            Span<long> childCosts,
            Span<byte> childRectangleWins,
            out int stoppedAtLeaf,
            out long accumulatedCost)
        {
            int rate = Av1TileWriter.GetPartitionCost(this.state.Picture, writer, tables.ModeCosts, blockSize, partitionType, blockOrigin, in partitionEdges);

            Av1RateDistortionStatistics statistics = new(this.state.RateMultiplier, rate, 0);
            int leafCount = partitionType.GetChildCount();

            // The sub-blocks of the asymmetric and four-way partitions are costed at their own rate multipliers. At the end, the sum
            // returns to the multiplier of the node.
            bool asymmetric = partitionType is >= Av1PartitionType.HorizontalA and <= Av1PartitionType.Vertical4;
            int nodeRateMultiplier = this.state.RateMultiplier;
            stoppedAtLeaf = 0;
            accumulatedCost = statistics.Cost;

            // Each child uses part of the bound of the parent. Later rates and distortions are not negative, so a losing prefix cannot
            // recover. The loop must stop before another child changes the contexts.
            for (int leafIndex = 0; leafIndex < leafCount; leafIndex++)
            {
                stoppedAtLeaf = leafIndex;

                // Only a square split compares the partition symbol alone against the bound. Every other shape measures its first
                // sub-block first and leaves those samples.
                if (!asymmetric && (leafIndex > 0 || partitionType == Av1PartitionType.Split) &&
                    statistics.Cost >= costLimit.Cost)
                {
                    return Av1RateDistortionStatistics.Invalid;
                }

                Point leafOrigin = partitionType.GetChildOrigin(blockOrigin, blockSize, leafIndex);
                Av1BlockSize leafSize = partitionType.GetChildBlockSize(blockSize, leafIndex);
                if (!this.IsBlockOriginInsideFrame(leafOrigin))
                {
                    continue;
                }

                Av1RateDistortionStatistics leafLimit = costLimit;
                if (asymmetric)
                {
                    // Each asymmetric sub-block sets up its own multiplier and measures the bound at it.
                    this.state.RateMultiplier = this.SetupBlockRateMultiplier(leafOrigin, leafSize, segmentId: -1);
                    leafLimit.UpdateCost(this.state.RateMultiplier);
                }

                Av1RateDistortionStatistics remainingCost = leafLimit.Subtract(this.state.RateMultiplier, statistics);
                bool publishContexts = leafIndex < leafCount - 1 || publishFinalContexts;
                this.state.ActiveModeCache = searchChildren && partitionType is >= Av1PartitionType.HorizontalA and <= Av1PartitionType.VerticalB
                    ? this.state.AsymmetricModeCache[leafIndex]
                    : default;

                long childNoneCost = 0;
                byte childWins = 3;

                // When an encode follows the search of a leaf, the leaf leaves its coefficient contexts to that encode. The mode search
                // updates no entropy context, so the encode reads the contexts of the neighbors, not the contexts of the leaf.
                bool siblingEncodeFollows = searchChildren &&
                    leafIndex + 1 < leafCount && (partitionType != Av1PartitionType.Split || blockSize <= Av1BlockSize.Block8x8);

                bool encodeFollows = siblingEncodeFollows && this.state.Picture.Parent.FrameHeader.IsIntra;

                // A replay of a finished subtree encodes every leaf at the multiplier of that leaf.
                if (!searchChildren && !(partitionType == Av1PartitionType.Split && blockSize > Av1BlockSize.Block8x8))
                {
                    this.state.RateMultiplier = this.SetupBlockRateMultiplier(leafOrigin, leafSize, segmentId: -1);
                }

                Av1RateDistortionStatistics childStatistics = partitionType == Av1PartitionType.Split && blockSize > Av1BlockSize.Block8x8
                    ? this.EvaluateSelectedPartitionTree(
                        writer,
                        in tables,
                        in modeWorkspace,
                        transformCoefficients,
                        dequantizedCoefficients,
                        searchDequantizedCoefficients,
                        transformWorkspace,
                        transformTypeProbabilities,
                        blockResidual,
                        searchCoefficients,
                        searchReconstructions,
                        estimationRowCoefficients,
                        in interWorkspace,
                        motionSearchPrediction,
                        in motionVectorCosts,
                        transformPrediction,
                        interIntraAbove,
                        interIntraLeft,
                        firstIntermediate,
                        secondIntermediate,
                        compoundMask,
                        in partitionEdges,
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
                        searchSegmentMap,
                        previousSegmentMap,
                        superblockCoefficients,
                        workspaceStorage,
                        sourceLuma,
                        sourceBlue,
                        sourceRed,
                        reconstructionLuma,
                        reconstructionBlue,
                        reconstructionRed,
                        macroBlock,
                        leafOrigin,
                        leafSize,
                        (nodeIndex * 4) + leafIndex + 1,
                        remainingCost,
                        searchChildren,
                        publishContexts,
                        out childNoneCost,
                        out childWins)
                    : searchChildren &&
                        !this.state.BlockWorkspace.PartitionTree.GetContext(nodeIndex, partitionType, leafIndex).Snapshot.Ready
                        ? this.EvaluatePartitionLeaf(
                            writer,
                            in tables,
                            in modeWorkspace,
                            transformCoefficients,
                            dequantizedCoefficients,
                            searchDequantizedCoefficients,
                            transformWorkspace,
                            transformTypeProbabilities,
                            blockResidual,
                            searchCoefficients,
                            searchReconstructions,
                            estimationRowCoefficients,
                            in interWorkspace,
                            motionSearchPrediction,
                            in motionVectorCosts,
                            transformPrediction,
                            interIntraAbove,
                            interIntraLeft,
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
                            searchSegmentMap,
                            previousSegmentMap,
                            superblockCoefficients,
                            workspaceStorage,
                            sourceLuma,
                            sourceBlue,
                            sourceRed,
                            reconstructionLuma,
                            reconstructionBlue,
                            reconstructionRed,
                            macroBlock,
                            leafOrigin,
                            leafSize,
                            partitionType == Av1PartitionType.Split ? Av1PartitionType.None : partitionType,
                            this.state.BlockWorkspace.PartitionTree.GetContext(nodeIndex, partitionType, leafIndex),
                            this.GetLeafCostLimit(remainingCost, leafOrigin, leafSize, partitionType == Av1PartitionType.Split),
                            publishContexts,
                            !encodeFollows,
                            siblingEncodeFollows)
                        : this.ReconstructPartitionLeaf(
                            writer,
                            in tables,
                            in modeWorkspace,
                            transformCoefficients,
                            dequantizedCoefficients,
                            transformWorkspace,
                            in interWorkspace,
                            transformPrediction,
                            interIntraAbove,
                            interIntraLeft,
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
                            sourceLuma,
                            sourceBlue,
                            sourceRed,
                            reconstructionLuma,
                            reconstructionBlue,
                            reconstructionRed,
                            macroBlock,
                            leafOrigin,
                            this.state.BlockWorkspace.PartitionTree.GetContext(nodeIndex, partitionType, leafIndex),
                            publishContexts,
                            searchChildren,
                            encodeFollows);

                this.state.ActiveModeCache = default;
                if (!searchChildren)
                {
                    this.state.RateMultiplier = nodeRateMultiplier;
                }

                if (!childRectangleWins.IsEmpty)
                {
                    childRectangleWins[leafIndex] = childWins;
                }

                // A 4x4 split child is an unsplit search of its own. When its result stays within its budget, it records the reference
                // that it picked for the rectangles of its parent.
                if (searchChildren && partitionType == Av1PartitionType.Split && blockSize <= Av1BlockSize.Block8x8 &&
                    childStatistics.Cost < remainingCost.Cost && !this.state.Picture.Parent.FrameHeader.IsIntra &&
                    this.state.Picture.Parent.SpeedSettings.GetRectangularPartitionReferencePruning(this.state.Picture.Parent.FrameUpdateType) != 0)
                {
                    this.UpdatePickedReferenceFrames(
                        leafOrigin,
                        leafSize,
                        this.state.BlockWorkspace.PartitionTree.GetContext(nodeIndex, partitionType, leafIndex).Snapshot.ModeInfo.Block);
                }

                if (!childCosts.IsEmpty)
                {
                    // The split classification uses the unsplit cost of each child, whatever its winner is. Rectangular leaves have no
                    // child partition search. They keep their complete mode cost, measured again at the multiplier of the node.
                    Av1RateDistortionStatistics nodeLeafCost = childStatistics;
                    if (partitionType != Av1PartitionType.Split)
                    {
                        nodeLeafCost.UpdateCost(nodeRateMultiplier);
                    }

                    childCosts[leafIndex] = partitionType == Av1PartitionType.Split && blockSize > Av1BlockSize.Block8x8
                        ? childNoneCost
                        : nodeLeafCost.Cost;
                }

                // Every split child is searched as a partition of its own. It reports a result only when that result stays below the
                // budget that it received. Otherwise the split is invalid. A 4x4 child is evaluated here directly, so the same test applies
                // to it.
                if (childStatistics.Cost == long.MaxValue ||
                    (partitionType == Av1PartitionType.Split && blockSize <= Av1BlockSize.Block8x8 &&
                    childStatistics.Cost >= remainingCost.Cost))
                {
                    this.state.RateMultiplier = nodeRateMultiplier;
                    accumulatedCost = long.MaxValue;
                    return Av1RateDistortionStatistics.Invalid;
                }

                statistics.Add(this.state.RateMultiplier, childStatistics);
                accumulatedCost = statistics.Cost;
                if (asymmetric && statistics.Cost >= leafLimit.Cost)
                {
                    this.state.RateMultiplier = nodeRateMultiplier;
                    return Av1RateDistortionStatistics.Invalid;
                }

                // A sibling predicts from the reconstruction that this leaf leaves, so the leaf is encoded across every plane before the
                // loop moves on. The mode search alone leaves the last chroma candidate in the plane, not the winner. A split child encodes
                // itself when its own partition search ends. A leaf that already spent the bound encodes nothing, because the search stops
                // and does not measure the sibling. A rectangle whose second half falls outside the frame encodes nothing either. A
                // rectangle tests its cost before the encode. An asymmetric sub-block encodes every sub-block but the last.
                // A split child normally reconstructs its own winning subtree when its partition search ends. A 4x4 child has no
                // partition search of its own, so the parent leaves those samples instead, for every child but the last.
                bool reconstructSplitLeaf = partitionType == Av1PartitionType.Split &&
                    blockSize <= Av1BlockSize.Block8x8 && leafIndex + 1 < leafCount;

                bool reconstructSibling = partitionType != Av1PartitionType.Split && leafIndex + 1 < leafCount &&
                    (asymmetric || statistics.Cost < costLimit.Cost) &&
                    this.IsPartitionSiblingInsideFrame(blockOrigin, blockSize, partitionType, leafIndex);

                // An inter leaf keeps the samples of its own search, but the encode that it represents still writes DCT_DCT for each luma
                // block that quantized to nothing. An intra leaf, in an inter frame and in an intra frame, is quantized again by the intra
                // block encode. A leaf whose search was reused also needs that encode for its samples.
                bool siblingIsIntra = (reconstructSplitLeaf || reconstructSibling) &&
                    (this.state.Picture.Parent.FrameHeader.IsIntra ||
                     this.state.BlockWorkspace.PartitionTree.GetContext(nodeIndex, partitionType, leafIndex).Snapshot.ModeInfo.Block
                         .ReferenceFrame <= Av1ReferenceFrameType.Intra);

                if (searchChildren && !siblingIsIntra && (reconstructSplitLeaf || reconstructSibling))
                {
                    Av1EncoderPartitionTree.ModeContext sibling =
                        this.state.BlockWorkspace.PartitionTree.GetContext(nodeIndex, partitionType, leafIndex);

                    if (!this.state.Picture.Parent.FrameHeader.LosslessArray[sibling.Snapshot.ModeInfo.Block.SegmentId] &&
                        !sibling.Snapshot.ModeInfo.Block.Skip)
                    {
                        Span<Av1EncoderTransformBlockState> siblingStates = sibling.GetTransformStates(Av1Plane.Y);
                        RetainEncodedZeroBlockTypes(siblingStates, siblingStates);
                    }
                }

                if (searchChildren && siblingIsIntra)
                {
                    Av1EncoderPartitionTree.ModeContext sibling =
                        this.state.BlockWorkspace.PartitionTree.GetContext(nodeIndex, partitionType, leafIndex);

                    int siblingLumaArea = this.state.CodedAreaLuma;
                    int siblingChromaArea = this.state.CodedAreaChroma;
                    if (sibling.Snapshot.ModeInfo.Block.UseIntraBlockCopy)
                    {
                        // A copied block is encoded from its displacement, not from an intra predictor, as an inter block is.
                        this.ReconstructPartitionLeaf(
                            writer,
                            in tables,
                            in modeWorkspace,
                            transformCoefficients,
                            dequantizedCoefficients,
                            transformWorkspace,
                            in interWorkspace,
                            transformPrediction,
                            interIntraAbove,
                            interIntraLeft,
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
                            sourceLuma,
                            sourceBlue,
                            sourceRed,
                            reconstructionLuma,
                            reconstructionBlue,
                            reconstructionRed,
                            macroBlock,
                            leafOrigin,
                            sibling,
                            true,
                            false,
                            false);
                    }
                    else
                    {
                        this.ReconstructSelectedIntraBlock(
                            writer,
                            in tables,
                            in modeWorkspace,
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
                            sourceLuma,
                            sourceBlue,
                            sourceRed,
                            reconstructionLuma,
                            reconstructionBlue,
                            reconstructionRed,
                            macroBlock,
                            leafOrigin,
                            sibling);

                        // The encode leaves the entropy contexts of what it coded for the next leaf, from the coefficients that it just wrote.
                        this.PublishPartitionLeafContexts(
                            in transformEdges,
                            in paletteEdges,
                            in lumaCoefficientEdges,
                            in blueCoefficientEdges,
                            in redCoefficientEdges,
                            superblockCoefficients,
                            macroBlock,
                            leafOrigin,
                            siblingLumaArea,
                            siblingChromaArea,
                            sibling.Snapshot.ModeInfo,
                            sibling.Snapshot.Block,
                            sibling.Snapshot.Palette,
                            true);
                    }

                    this.state.CodedAreaLuma = siblingLumaArea;
                    this.state.CodedAreaChroma = siblingChromaArea;
                }

                // The sub-block of an asymmetric partition encodes its sibling state before its multiplier returns to the node.
                this.state.RateMultiplier = nodeRateMultiplier;

                bool reusableSplit = partitionType == Av1PartitionType.Split && blockSize > Av1BlockSize.Block8x8 && leafIndex < 2;
                bool reusableRectangle = partitionType is Av1PartitionType.Horizontal or Av1PartitionType.Vertical &&
                    leafIndex == 0 && statistics.Cost < costLimit.Cost;

                int reusableNode = reusableSplit ? (nodeIndex * 4) + leafIndex + 1 : nodeIndex;
                if (searchChildren &&
                    (reusableRectangle || (reusableSplit &&
                        (Av1PartitionType)this.state.Superblock.Workspace.PartitionSearchTypes[reusableNode] == Av1PartitionType.None)))
                {
                    Av1EncoderPartitionTree.ModeContext reusable = this.state.BlockWorkspace.PartitionTree.GetContext(
                        reusableNode, reusableSplit ? Av1PartitionType.None : partitionType, 0);

                    reusable.Snapshot.Ready = reusable.Snapshot.Palette.PaletteSizes[0] == 0 &&
                        reusable.Snapshot.Palette.PaletteSizes[1] == 0 &&
                        reusable.Snapshot.ModeInfo.Block.UvMode != Av1ChromaPredictionMode.ChromaFromLuma;
                }
            }

            stoppedAtLeaf = leafCount;
            if (asymmetric)
            {
                statistics.UpdateCost(this.state.RateMultiplier);
                accumulatedCost = statistics.Cost;
            }

            return statistics.Cost < costLimit.Cost ? statistics : Av1RateDistortionStatistics.Invalid;
        }

        /// <summary>
        /// Evaluates reference-frame modes and retains the winning syntax and transform choices.
        /// </summary>
        /// <param name="writer">The symbol encoder that prices the syntax.</param>
        /// <param name="tables">The rate tables of the tile.</param>
        /// <param name="transformCoefficients">The forward transform output buffer.</param>
        /// <param name="dequantizedCoefficients">The dequantized coefficient buffer of the candidate.</param>
        /// <param name="searchDequantizedCoefficients">The dequantized coefficient buffer of the winner.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
        /// <param name="transformTypeProbabilities">The transform type probabilities of every update type and size.</param>
        /// <param name="blockResidual">The residual buffer of the motion search.</param>
        /// <param name="interWorkspace">The inter prediction buffers of the block.</param>
        /// <param name="motionSearchPrediction">The prediction buffer of the motion search.</param>
        /// <param name="motionVectorCosts">The motion vector rates of the frame precision.</param>
        /// <param name="transformPrediction">The prediction storage of one transform block.</param>
        /// <param name="interIntraAbove">The extended above edge of an inter-intra prediction.</param>
        /// <param name="interIntraLeft">The extended left edge of an inter-intra prediction.</param>
        /// <param name="firstIntermediate">The compound intermediate of the first reference.</param>
        /// <param name="secondIntermediate">The compound intermediate of the second reference.</param>
        /// <param name="compoundMask">The blend mask of a masked compound prediction.</param>
        /// <param name="transformEdges">The transform size context edges of the tile.</param>
        /// <param name="lumaCoefficientEdges">The luma coefficient context edges of the tile.</param>
        /// <param name="blueCoefficientEdges">The blue-difference coefficient context edges of the tile.</param>
        /// <param name="redCoefficientEdges">The red-difference coefficient context edges of the tile.</param>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="displacementVectors">The displacement vectors of the picture, one per allocation entry.</param>
        /// <param name="referenceContexts">The motion vector reference contexts of the picture, one per allocation entry.</param>
        /// <param name="workspaceStorage">The storage of the block workspace, which holds the search buffers of every block.</param>
        /// <param name="sourceLuma">The samples of the complete source luma plane, read once per frame pass.</param>
        /// <param name="sourceBlue">The samples of the complete source blue-difference plane, read once per frame pass.</param>
        /// <param name="sourceRed">The samples of the complete source red-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionLuma">The samples of the complete reconstructed luma plane, read once per frame pass.</param>
        /// <param name="reconstructionBlue">The samples of the complete reconstructed blue-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionRed">The samples of the complete reconstructed red-difference plane, read once per frame pass.</param>
        /// <param name="macroBlock">The neighbor availability of the block.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="modeInfo">The block decisions.</param>
        /// <param name="block">The block state.</param>
        /// <param name="selectedVector">The motion vector of the winner.</param>
        /// <param name="selectedSecondaryVector">The second motion vector of a compound winner.</param>
        /// <param name="selectedStates">The transform states of the winner.</param>
        /// <returns>The rate and distortion of the winner.</returns>
        public Av1RateDistortionStatistics SelectInterBlock(
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> searchDequantizedCoefficients,
            Span<int> transformWorkspace,
            ReadOnlySpan<int> transformTypeProbabilities,
            Span<short> blockResidual,
            in Av1EncoderInterPredictionWorkspace<byte> interWorkspace,
            Span<byte> motionSearchPrediction,
            in Av1MotionVectorCosts motionVectorCosts,
            Span<byte> transformPrediction,
            Span<byte> interIntraAbove,
            Span<byte> interIntraLeft,
            Span<ushort> firstIntermediate,
            Span<ushort> secondIntermediate,
            Span<byte> compoundMask,
            in Av1NeighborEdges<byte> transformEdges,
            in Av1NeighborEdges<byte> lumaCoefficientEdges,
            in Av1NeighborEdges<byte> blueCoefficientEdges,
            in Av1NeighborEdges<byte> redCoefficientEdges,
            ReadOnlySpan<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            ReadOnlySpan<Av1EncoderDisplacementVector> displacementVectors,
            ReadOnlySpan<Av1EncoderReferenceContext> referenceContexts,
            Span<int> workspaceStorage,
            ReadOnlySpan<byte> sourceLuma,
            ReadOnlySpan<byte> sourceBlue,
            ReadOnlySpan<byte> sourceRed,
            Span<byte> reconstructionLuma,
            Span<byte> reconstructionBlue,
            Span<byte> reconstructionRed,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ref Av1MacroBlockModeInfo modeInfo,
            ref Av1EncoderBlockStruct block,
            out Av1MotionVector selectedVector,
            out Av1MotionVector selectedSecondaryVector,
            out InlineArray128<Av1EncoderTransformBlockState> selectedStates)
        {
            this.state.BlockWorkspace.EvaluationStage = Av1EncoderEvaluationStage.Candidate;
            int estimation = this.state.Picture.Parent.SpeedSettings.InterModeEstimation;
            Av1BlockSize blockSize = modeInfo.Block.BlockSize;
            this.state.EstimateInterCandidates =
                (estimation == 1 && this.state.BlockWorkspace.GetInterModeModels(workspaceStorage)[(int)blockSize].IsReady) ||
                (estimation == 2 && blockSize.GetWidth() * blockSize.GetHeight() > 256);

            this.state.InterCandidateCount = 0;
            this.state.CompoundSearchRecordCount = 0;
            this.state.InterpolationSearchRecordCount = 0;
            this.state.BlockWorkspace.GetSingleReferenceFilterCosts(workspaceStorage).Fill(long.MaxValue);
            this.state.BlockWorkspace.GetSingleReferenceSimpleCosts(workspaceStorage).Fill(long.MaxValue);
            this.state.BestInterEstimate = long.MaxValue;
            this.state.BestInterPredictionCost = long.MaxValue;
            this.state.BestInterLumaPredictionCost = long.MaxValue;
            this.state.UseWarpedPrediction = false;
            this.state.UseObmcPrediction = false;
            this.state.MotionModeWinnerCount = 0;
            this.state.MotionModeWinnerLimit = this.state.Picture.Parent.SpeedSettings.GetMotionModeWinnerCount(this.state.Picture.Parent.FrameUpdateType);
            this.state.InterSourceVarianceCost = (long)this.state.InterSourceVariance * blockSize.GetWidth() * blockSize.GetHeight() * 128;
            Span<long> topAverageCosts = stackalloc long[5];
            topAverageCosts.Fill(long.MaxValue);

            // Every compound reference pair of the block shares the saved wedge index, wedge sign and difference-weighted mask type. The last
            // compound type is kept for each pair.
            Span<int> compoundMaskHistory = stackalloc int[64];
            compoundMaskHistory.Fill(-1);
            modeInfo.Block.MotionMode = Av1MotionMode.SimpleTranslation;
            Av1MacroBlockModeInfo initialModeInfo = modeInfo;
            Av1EncoderBlockStruct initialBlock = block;
            InlineArray8<Av1ReferenceMotionVectors> singleReferenceVectorsStorage = default;
            InlineArray8<int> interIntraModesStorage = default;
            InlineArray8<long> bestSingleCostsStorage = default;
            InlineArray8<Av1PredictionMode> bestSingleModesStorage = default;
            Span<Av1ReferenceMotionVectors> singleReferenceVectors = singleReferenceVectorsStorage;
            Span<int> interIntraModes = interIntraModesStorage;
            Span<long> bestSingleCosts = bestSingleCostsStorage;
            Span<Av1PredictionMode> bestSingleModes = bestSingleModesStorage;
            bestSingleCosts[..].Fill(long.MaxValue);
            bestSingleModes[..].Fill(Av1PredictionMode.PredictionModeCount);
            interIntraModes[..].Fill(-1);
            InlineArray8<InlineArray3<Av1MotionVector>> newMotionVectors = default;
            InlineArray8<byte> newMotionVectorMasksStorage = default;
            Span<byte> newMotionVectorMasks = newMotionVectorMasksStorage;
            uint searchedSingleModes = 0;

            // Each block search starts without a best single-reference error.
            this.state.BestSingleReferenceSses[..].Fill(int.MaxValue);

            // Each block search starts without a prediction error for any first reference.
            this.state.PredictionSses[..].Fill(int.MaxValue);
            this.state.LeftoverInterEstimate = Av1RateDistortionStatistics.Invalid;
            selectedVector = default;
            selectedSecondaryVector = default;
            selectedStates = default;
            Av1RateDistortionStatistics selectedStatistics = Av1RateDistortionStatistics.Invalid;
            ReadOnlySpan<Av1ReferenceFrameType> referenceOrder =
            [
                Av1ReferenceFrameType.Last, Av1ReferenceFrameType.Last2, Av1ReferenceFrameType.Last3,
                Av1ReferenceFrameType.Backward, Av1ReferenceFrameType.Alternate2, Av1ReferenceFrameType.Alternate,
                Av1ReferenceFrameType.Golden
            ];

            byte availableReferences = this.state.Picture.Parent.AvailableReferenceMask;
            Point modeInfoPosition = new(blockOrigin.X >> Av1Constants.ModeInfoSizeLog2, blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2);
            foreach (Av1ReferenceFrameType reference in referenceOrder)
            {
                if ((availableReferences & (1 << (int)reference)) == 0)
                {
                    continue;
                }

                singleReferenceVectors[(int)reference].Build(
                    this.state.Picture,
                    modeInfoGrid,
                    modeInfoAllocation,
                    displacementVectors,
                    referenceContexts,
                    macroBlock,
                    modeInfoPosition,
                    blockSize,
                    initialModeInfo.Block.PartitionType,
                    this.state.Picture.Sequence.SequenceHeader,
                    this.state.Picture.Parent.FrameHeader,
                    reference,
                    Av1ReferenceFrameType.None);
            }

            this.state.SkipReferenceFrameMask = this.GetSkipReferenceFrameMask(blockOrigin, blockSize, initialModeInfo.Block.PartitionType);
            this.SetInterModeSkipMasks(sourceLuma, sourceBlue, sourceRed, blockOrigin, blockSize, singleReferenceVectors[..]);
            this.PrepareTplInterModePruning(workspaceStorage, blockOrigin, blockSize);

            // The search tries the same syntax mode across the available references before it moves to the next mode. The complete
            // reference list search of NEWMV is kept for the later compound candidates.
            ReadOnlySpan<Av1PredictionMode> modeOrder =
            [
                Av1PredictionMode.NearestMotionVector, Av1PredictionMode.NewMotionVector,
                Av1PredictionMode.NearMotionVector, Av1PredictionMode.GlobalMotionVector
            ];

            Span<int> modeThresholdFactors = this.state.BlockWorkspace.GetModeThresholdFactors(workspaceStorage);
            Span<long> singleReferenceFilterCosts = this.state.BlockWorkspace.GetSingleReferenceFilterCosts(workspaceStorage);
            foreach (Av1PredictionMode mode in modeOrder)
            {
                foreach (Av1ReferenceFrameType reference in referenceOrder)
                {
                    // Each iteration of the mode loop clears the leftover skip flag.
                    this.state.TransformSearchSkip = false;
                    int index = (int)reference;

                    // The reference mask of the partition does not remove a reference that the cached decision of an asymmetric sub-block
                    // needs. A mode that the cache leaves searches motion modes only when the cache holds that single-reference mode itself.
                    int cacheDecision = this.GetInterModeCacheDecision(mode, reference, Av1ReferenceFrameType.None);
                    bool skipMotionModes = cacheDecision == 2 ||
                        (cacheDecision == 0 && (this.state.SkipReferenceFrameMask & (1 << index)) != 0);

                    if ((availableReferences & (1 << index)) == 0 ||
                        (this.state.InterModeSkipMasks[index] & (1u << (int)mode)) != 0 ||
                        (cacheDecision == 0 && this.IsSingleReferenceSkipped(index)) ||
                        this.PrunesReferenceBySelectiveReferenceFrame(workspaceStorage, reference, Av1ReferenceFrameType.None))
                    {
                        continue;
                    }

                    // The last argument is the all-empty result of the search winner, not the skip choice of the block.
                    if (Av1ModeThresholds.ShouldSkip(
                        modeThresholdFactors,
                        this.ModeThresholdQuantizerFactor,
                        this.ModeThresholdSkipMultiplier,
                        blockSize,
                        mode,
                        reference,
                        Av1ReferenceFrameType.None,
                        Math.Min(this.state.BlockCostLimit, selectedStatistics.Cost),
                        selectedStatistics.AllTransformsEmpty))
                    {
                        continue;
                    }

                    int vectorCount = singleReferenceVectors[index].Count;
                    Av1GlobalMotionType globalType = this.state.Picture.Parent.FrameHeader
                        .GetGlobalMotionParameters()[(int)reference - (int)Av1ReferenceFrameType.Last].Type;

                    Av1PredictionMode compareMode = Av1PredictionMode.PredictionModeCount;
                    if (mode == Av1PredictionMode.NearMotionVector)
                    {
                        if (vectorCount == 0)
                        {
                            compareMode = Av1PredictionMode.NearestMotionVector;
                        }
                        else if (vectorCount == 1 && globalType <= Av1GlobalMotionType.Translation)
                        {
                            compareMode = Av1PredictionMode.GlobalMotionVector;
                        }
                    }
                    else if (mode == Av1PredictionMode.GlobalMotionVector)
                    {
                        if (vectorCount == 0 && globalType <= Av1GlobalMotionType.Translation)
                        {
                            compareMode = Av1PredictionMode.NearestMotionVector;
                        }
                        else if (vectorCount == 1)
                        {
                            compareMode = Av1PredictionMode.NearMotionVector;
                        }
                    }

                    if (compareMode != Av1PredictionMode.PredictionModeCount)
                    {
                        int previousIndex = (((int)compareMode - (int)Av1PredictionMode.InterModeStart) * 3 *
                            Av1Constants.ReferenceFrameCount) + (int)reference;

                        long previousCost = singleReferenceFilterCosts[previousIndex];
                        int context = singleReferenceVectors[index].ModeContext;
                        if (previousCost != long.MaxValue &&
                            Av1SymbolEncoder.GetInterModeCost(tables.ModeCosts, mode, context) >
                            Av1SymbolEncoder.GetInterModeCost(tables.ModeCosts, compareMode, context))
                        {
                            // Identical vectors have identical prediction error. The modeled cost is kept for the compound comparisons, but
                            // the more expensive single syntax is not searched.
                            int currentIndex = (((int)mode - (int)Av1PredictionMode.InterModeStart) * 3 *
                                Av1Constants.ReferenceFrameCount) + (int)reference;

                            singleReferenceFilterCosts[currentIndex] = previousCost;
                            continue;
                        }
                    }

                    // The cache test follows the repeated-vector test. A single mode kept for a cached compound always passes the neighbor
                    // test.
                    if (cacheDecision == 1)
                    {
                        continue;
                    }

                    if (mode == Av1PredictionMode.NearMotionVector && cacheDecision != 2 &&
                        this.ShouldPruneNearMode(
                            modeInfoGrid,
                            modeInfoAllocation,
                            macroBlock,
                            reference,
                            Av1ReferenceFrameType.None,
                            Math.Min(this.state.BlockCostLimit, selectedStatistics.Cost)))
                    {
                        continue;
                    }

                    if (this.state.Picture.Parent.SpeedSettings.PruneSpatialMotionByWeight &&
                        !this.state.Picture.Parent.FrameHeader.AllowScreenContentTools &&
                        mode is Av1PredictionMode.NearestMotionVector or Av1PredictionMode.NearMotionVector &&
                        selectedStatistics.Cost != long.MaxValue && macroBlock.IsLeftAvailable && macroBlock.IsUpAvailable)
                    {
                        int count = Math.Min(3, vectorCount);
                        ReadOnlySpan<ushort> weights = singleReferenceVectors[index].Weights;
                        if (count != 0 &&
                            !(mode == Av1PredictionMode.NearestMotionVector && weights[0] >= Av1ReferenceMotionVectors.NearestCandidateWeight))
                        {
                            int nearestCount = 0;
                            for (int candidate = 0; candidate < count; candidate++)
                            {
                                nearestCount += weights[candidate] >= Av1ReferenceMotionVectors.NearestCandidateWeight ? 1 : 0;
                            }

                            if (nearestCount < (count >= 2 ? 2 : 1))
                            {
                                continue;
                            }
                        }
                    }

                    if (mode == Av1PredictionMode.GlobalMotionVector &&
                        this.PrunesZeroVectorWithSse(sourceLuma, sourceBlue, sourceRed, blockOrigin, blockSize, reference, Av1ReferenceFrameType.None))
                    {
                        continue;
                    }

                    Av1MacroBlockModeInfo candidateModeInfo = initialModeInfo;
                    Av1EncoderBlockStruct candidateBlock = initialBlock;
                    ref InlineArray3<Av1MotionVector> newVectors = ref newMotionVectors[index];
                    ref byte newVectorMask = ref newMotionVectorMasks[index];
                    Av1EncoderSpeedSettings settings = this.state.Picture.Parent.SpeedSettings;
                    if (!settings.SkipSingleInterpolationSearch &&
                        !(settings.UseWinnerInterpolation && this.state.Picture.Parent.FrameHeader.ReferenceMode == ObuReferenceMode.SingleReference))
                    {
                        searchedSingleModes |= 1U << ((((int)mode - (int)Av1PredictionMode.InterModeStart) *
                            Av1Constants.ReferenceFrameCount) + (int)reference);
                    }

                    this.state.TransformSearchReset = false;
                    Av1RateDistortionStatistics candidateStatistics = this.SelectSingleReferenceMode(
                        writer,
                        in tables,
                        transformCoefficients,
                        dequantizedCoefficients,
                        searchDequantizedCoefficients,
                        transformWorkspace,
                        transformTypeProbabilities,
                        blockResidual,
                        in interWorkspace,
                        motionSearchPrediction,
                        in motionVectorCosts,
                        transformPrediction,
                        interIntraAbove,
                        interIntraLeft,
                        firstIntermediate,
                        secondIntermediate,
                        compoundMask,
                        in transformEdges,
                        in lumaCoefficientEdges,
                        in blueCoefficientEdges,
                        in redCoefficientEdges,
                        modeInfoGrid,
                        modeInfoAllocation,
                        displacementVectors,
                        workspaceStorage,
                        sourceLuma,
                        sourceBlue,
                        sourceRed,
                        reconstructionLuma,
                        reconstructionBlue,
                        reconstructionRed,
                        macroBlock,
                        blockOrigin,
                        Math.Min(this.state.BlockCostLimit, selectedStatistics.Cost),
                        reference,
                        mode,
                        ref singleReferenceVectors[index],
                        ref interIntraModes[index],
                        this.state.MotionModeWinnerLimit == 0 && !skipMotionModes,
                        ref candidateModeInfo,
                        ref candidateBlock,
                        out Av1MotionVector candidateVector,
                        out InlineArray128<Av1EncoderTransformBlockState> candidateStates,
                        ref newVectors,
                        ref newVectorMask);

                    // A completed search leaves the skip flag of its best motion mode.
                    if (candidateStatistics.Cost != long.MaxValue)
                    {
                        this.state.TransformSearchSkip = candidateModeInfo.Block.Skip;
                    }
                    else if (this.state.TransformSearchReset)
                    {
                        // A search whose every entry failed leaves the flag its trials cleared.
                        this.state.TransformSearchSkip = false;
                    }

                    if (candidateStatistics.Cost < bestSingleCosts[(int)reference])
                    {
                        bestSingleCosts[(int)reference] = candidateStatistics.Cost;
                        bestSingleModes[(int)reference] = mode;
                    }

                    // A reference kept only because a compound pair uses it searches no motion mode.
                    if (!skipMotionModes)
                    {
                        this.RecordMotionModeWinner(candidateStatistics.Cost, false, candidateModeInfo, candidateBlock, candidateVector);
                    }

                    if (candidateStatistics.Cost < Math.Min(this.state.BlockCostLimit, selectedStatistics.Cost))
                    {
                        modeInfo = candidateModeInfo;
                        block = candidateBlock;
                        selectedVector = candidateVector;
                        selectedStates = candidateStates;
                        selectedStatistics = candidateStatistics;
                    }
                }
            }

            ReadOnlySpan<Av1ReferenceFrameType> compoundReferences =
            [
                Av1ReferenceFrameType.Last, Av1ReferenceFrameType.Alternate,
                Av1ReferenceFrameType.Last2, Av1ReferenceFrameType.Alternate,
                Av1ReferenceFrameType.Last3, Av1ReferenceFrameType.Alternate,
                Av1ReferenceFrameType.Golden, Av1ReferenceFrameType.Alternate,
                Av1ReferenceFrameType.Last, Av1ReferenceFrameType.Backward,
                Av1ReferenceFrameType.Last2, Av1ReferenceFrameType.Backward,
                Av1ReferenceFrameType.Last3, Av1ReferenceFrameType.Backward,
                Av1ReferenceFrameType.Golden, Av1ReferenceFrameType.Backward,
                Av1ReferenceFrameType.Last, Av1ReferenceFrameType.Alternate2,
                Av1ReferenceFrameType.Last2, Av1ReferenceFrameType.Alternate2,
                Av1ReferenceFrameType.Last3, Av1ReferenceFrameType.Alternate2,
                Av1ReferenceFrameType.Golden, Av1ReferenceFrameType.Alternate2,
                Av1ReferenceFrameType.Last, Av1ReferenceFrameType.Last2,
                Av1ReferenceFrameType.Last, Av1ReferenceFrameType.Last3,
                Av1ReferenceFrameType.Last, Av1ReferenceFrameType.Golden,
                Av1ReferenceFrameType.Backward, Av1ReferenceFrameType.Alternate
            ];

            ReadOnlySpan<byte> compoundSearchOrder = [4, 0, 1, 2, 3, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15];

            // The compound iterations of the mode loop follow the single ones, and each clears the skip flag.
            if (this.state.Picture.Parent.FrameHeader.ReferenceMode == ObuReferenceMode.ReferenceModeSelect)
            {
                this.state.TransformSearchSkip = false;
            }

            // After every single-reference mode, a pair is searched only when one of its references is within ten percent of the best single
            // reference.
            long singleReferenceCutoff = long.MaxValue;
            for (int reference = (int)Av1ReferenceFrameType.Last; reference <= (int)Av1ReferenceFrameType.Alternate; reference++)
            {
                singleReferenceCutoff = Math.Min(singleReferenceCutoff, bestSingleCosts[reference]);
            }

            if (singleReferenceCutoff != long.MaxValue)
            {
                singleReferenceCutoff = 110 * singleReferenceCutoff / 100;
            }

            // An alternate reference frame of the base layer searches no compound mode at the speeds that skip them.
            bool skipsCompound = this.state.Picture.Parent.PrunesAllCompoundReferences ||
                (this.state.Picture.Parent.SpeedSettings.SkipAlternateReferenceCompound &&
                 this.state.Picture.Parent.FrameUpdateType == Av1FrameUpdateType.Alternate);

            // The nearest pairs set a bound across all admitted references first. Then the remaining motion families complete each pair in
            // order, and keep the mask history of that pair.
            for (int phase = 0; !skipsCompound && phase < 2; phase++)
            {
                for (int index = 0; index < compoundSearchOrder.Length; index++)
                {
                    int pairIndex = phase == 0 ? index : compoundSearchOrder[index];
                    bool outsideSingleReferenceCutoff = this.state.Picture.Parent.SpeedSettings.PruneCompoundUsingSingleReference &&
                        bestSingleCosts[(int)compoundReferences[pairIndex * 2]] > singleReferenceCutoff &&
                        bestSingleCosts[(int)compoundReferences[(pairIndex * 2) + 1]] > singleReferenceCutoff;

                    this.SelectCompoundBlock(
                        writer,
                        in tables,
                        transformCoefficients,
                        dequantizedCoefficients,
                        searchDequantizedCoefficients,
                        transformWorkspace,
                        transformTypeProbabilities,
                        in interWorkspace,
                        in motionVectorCosts,
                        transformPrediction,
                        firstIntermediate,
                        secondIntermediate,
                        compoundMask,
                        in transformEdges,
                        in lumaCoefficientEdges,
                        in blueCoefficientEdges,
                        in redCoefficientEdges,
                        modeInfoGrid,
                        modeInfoAllocation,
                        displacementVectors,
                        referenceContexts,
                        workspaceStorage,
                        sourceLuma,
                        sourceBlue,
                        sourceRed,
                        reconstructionLuma,
                        reconstructionBlue,
                        reconstructionRed,
                        macroBlock,
                        blockOrigin,
                        compoundReferences[pairIndex * 2],
                        compoundReferences[(pairIndex * 2) + 1],
                        phase == 0,
                        topAverageCosts,
                        compoundMaskHistory[..3],
                        compoundMaskHistory.Slice(3 + pairIndex, 1),
                        outsideSingleReferenceCutoff,
                        bestSingleModes,
                        searchedSingleModes,
                        singleReferenceVectors,
                        newMotionVectors,
                        newMotionVectorMasks,
                        ref modeInfo,
                        ref block,
                        ref selectedStatistics,
                        ref selectedVector,
                        ref selectedSecondaryVector,
                        ref selectedStates);
                }
            }

            this.EvaluateMotionModeWinners(
                writer,
                in tables,
                transformCoefficients,
                dequantizedCoefficients,
                searchDequantizedCoefficients,
                transformWorkspace,
                transformTypeProbabilities,
                in interWorkspace,
                motionSearchPrediction,
                in motionVectorCosts,
                transformPrediction,
                firstIntermediate,
                secondIntermediate,
                compoundMask,
                in transformEdges,
                in lumaCoefficientEdges,
                in blueCoefficientEdges,
                in redCoefficientEdges,
                modeInfoGrid,
                modeInfoAllocation,
                displacementVectors,
                workspaceStorage,
                sourceLuma,
                sourceBlue,
                sourceRed,
                reconstructionLuma,
                reconstructionBlue,
                reconstructionRed,
                macroBlock,
                blockOrigin,
                singleReferenceVectors[..],
                ref modeInfo,
                ref block,
                ref selectedStatistics,
                ref selectedVector,
                ref selectedSecondaryVector,
                ref selectedStates);

            // The winner's luma cost gates the intra modes, unless the retained candidates are searched below.
            this.state.InterLumaThreshold = selectedStatistics.LumaCost;
            if (this.state.EstimateInterCandidates)
            {
                Av1RateDistortionStatistics bestEstimate = selectedStatistics;
                selectedStatistics = this.SearchRetainedInterCandidates(
                    writer,
                    in tables,
                    transformCoefficients,
                    dequantizedCoefficients,
                    searchDequantizedCoefficients,
                    transformWorkspace,
                    transformTypeProbabilities,
                    in interWorkspace,
                    transformPrediction,
                    interIntraAbove,
                    interIntraLeft,
                    firstIntermediate,
                    secondIntermediate,
                    compoundMask,
                    in transformEdges,
                    in lumaCoefficientEdges,
                    in blueCoefficientEdges,
                    in redCoefficientEdges,
                    modeInfoGrid,
                    modeInfoAllocation,
                    displacementVectors,
                    workspaceStorage,
                    sourceLuma,
                    sourceBlue,
                    sourceRed,
                    reconstructionLuma,
                    reconstructionBlue,
                    reconstructionRed,
                    macroBlock,
                    blockOrigin,
                    ref modeInfo,
                    ref block,
                    out selectedVector,
                    out selectedSecondaryVector,
                    out selectedStates,
                    out this.state.InterLumaThreshold);

                if (selectedStatistics.Cost == long.MaxValue)
                {
                    this.state.LeftoverInterEstimate = bestEstimate;
                }
            }

            return selectedStatistics;
        }

        /// <summary>
        /// Searches ordered compound modes using retained single-reference decisions.
        /// </summary>
        /// <param name="writer">The symbol encoder that prices the syntax.</param>
        /// <param name="tables">The rate tables of the tile.</param>
        /// <param name="transformCoefficients">The forward transform output buffer.</param>
        /// <param name="dequantizedCoefficients">The dequantized coefficient buffer of the candidate.</param>
        /// <param name="searchDequantizedCoefficients">The dequantized coefficient buffer of the winner.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
        /// <param name="transformTypeProbabilities">The transform type probabilities of every update type and size.</param>
        /// <param name="interWorkspace">The inter prediction buffers of the block.</param>
        /// <param name="motionVectorCosts">The motion vector rates of the frame precision.</param>
        /// <param name="transformPrediction">The prediction storage of one transform block.</param>
        /// <param name="firstIntermediate">The compound intermediate of the first reference.</param>
        /// <param name="secondIntermediate">The compound intermediate of the second reference.</param>
        /// <param name="compoundMask">The blend mask of a masked compound prediction.</param>
        /// <param name="transformEdges">The transform size context edges of the tile.</param>
        /// <param name="lumaCoefficientEdges">The luma coefficient context edges of the tile.</param>
        /// <param name="blueCoefficientEdges">The blue-difference coefficient context edges of the tile.</param>
        /// <param name="redCoefficientEdges">The red-difference coefficient context edges of the tile.</param>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="displacementVectors">The displacement vectors of the picture, one per allocation entry.</param>
        /// <param name="referenceContexts">The motion vector reference contexts of the picture, one per allocation entry.</param>
        /// <param name="workspaceStorage">The storage of the block workspace, which holds the search buffers of every block.</param>
        /// <param name="sourceLuma">The samples of the complete source luma plane, read once per frame pass.</param>
        /// <param name="sourceBlue">The samples of the complete source blue-difference plane, read once per frame pass.</param>
        /// <param name="sourceRed">The samples of the complete source red-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionLuma">The samples of the complete reconstructed luma plane, read once per frame pass.</param>
        /// <param name="reconstructionBlue">The samples of the complete reconstructed blue-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionRed">The samples of the complete reconstructed red-difference plane, read once per frame pass.</param>
        /// <param name="macroBlock">The neighbor availability of the block.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="primaryReference">The first reference of the pair.</param>
        /// <param name="secondaryReference">The second reference of the pair.</param>
        /// <param name="nearestOnly">Whether only the nearest-nearest mode is searched.</param>
        /// <param name="topAverageCosts">The lowest average-blend costs of the block so far.</param>
        /// <param name="compoundMaskHistory">The wedge index, wedge sign and difference-weighted mask type that every pair of the block shares.</param>
        /// <param name="compoundTypeHistory">The last compound type of the pair.</param>
        /// <param name="outsideSingleReferenceCutoff">Whether both references cost more than the single-reference cutoff.</param>
        /// <param name="bestSingleModes">The best single-reference mode of each reference.</param>
        /// <param name="searchedSingleModes">The bit set of the single-reference modes and references whose filters were searched.</param>
        /// <param name="singleReferenceVectors">The reference vector lists of the single references.</param>
        /// <param name="newMotionVectors">The new vectors of each single reference, by list index.</param>
        /// <param name="newMotionVectorMasks">The list indices whose new vector is set, for each reference.</param>
        /// <param name="modeInfo">The block decisions.</param>
        /// <param name="block">The block state.</param>
        /// <param name="selectedStatistics">The rate and distortion of the winner.</param>
        /// <param name="selectedVector">The motion vector of the winner.</param>
        /// <param name="selectedSecondaryVector">The second motion vector of a compound winner.</param>
        /// <param name="selectedStates">The transform states of the winner.</param>
        public void SelectCompoundBlock(
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> searchDequantizedCoefficients,
            Span<int> transformWorkspace,
            ReadOnlySpan<int> transformTypeProbabilities,
            in Av1EncoderInterPredictionWorkspace<byte> interWorkspace,
            in Av1MotionVectorCosts motionVectorCosts,
            Span<byte> transformPrediction,
            Span<ushort> firstIntermediate,
            Span<ushort> secondIntermediate,
            Span<byte> compoundMask,
            in Av1NeighborEdges<byte> transformEdges,
            in Av1NeighborEdges<byte> lumaCoefficientEdges,
            in Av1NeighborEdges<byte> blueCoefficientEdges,
            in Av1NeighborEdges<byte> redCoefficientEdges,
            ReadOnlySpan<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            ReadOnlySpan<Av1EncoderDisplacementVector> displacementVectors,
            ReadOnlySpan<Av1EncoderReferenceContext> referenceContexts,
            Span<int> workspaceStorage,
            ReadOnlySpan<byte> sourceLuma,
            ReadOnlySpan<byte> sourceBlue,
            ReadOnlySpan<byte> sourceRed,
            Span<byte> reconstructionLuma,
            Span<byte> reconstructionBlue,
            Span<byte> reconstructionRed,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1ReferenceFrameType primaryReference,
            Av1ReferenceFrameType secondaryReference,
            bool nearestOnly,
            Span<long> topAverageCosts,
            Span<int> compoundMaskHistory,
            Span<int> compoundTypeHistory,
            bool outsideSingleReferenceCutoff,
            ReadOnlySpan<Av1PredictionMode> bestSingleModes,
            uint searchedSingleModes,
            ReadOnlySpan<Av1ReferenceMotionVectors> singleReferenceVectors,
            ReadOnlySpan<InlineArray3<Av1MotionVector>> newMotionVectors,
            ReadOnlySpan<byte> newMotionVectorMasks,
            ref Av1MacroBlockModeInfo modeInfo,
            ref Av1EncoderBlockStruct block,
            ref Av1RateDistortionStatistics selectedStatistics,
            ref Av1MotionVector selectedVector,
            ref Av1MotionVector selectedSecondaryVector,
            ref InlineArray128<Av1EncoderTransformBlockState> selectedStates)
        {
            Span<byte> lumaFrame = reconstructionLuma;
            ObuFrameHeader frameHeader = this.state.Picture.Parent.FrameHeader;
            Av1BlockSize blockSize = modeInfo.Block.BlockSize;
            if ((this.state.Picture.Parent.AvailableReferenceMask & (1 << (int)primaryReference)) == 0 ||
                (this.state.Picture.Parent.AvailableReferenceMask & (1 << (int)secondaryReference)) == 0 ||
                frameHeader.ReferenceMode == ObuReferenceMode.SingleReference ||
                Math.Min(blockSize.GetWidth(), blockSize.GetHeight()) < 8 ||
                ((this.state.SkipReferenceFrameMask & (1 << GetReferenceFrameType(primaryReference, secondaryReference))) != 0 &&
                    !this.IsCachedCompoundPair(primaryReference, secondaryReference)) ||
                this.PrunesCompoundReferencePair(primaryReference, secondaryReference) ||
                this.PrunesReferenceBySelectiveReferenceFrame(workspaceStorage, primaryReference, secondaryReference) ||
                outsideSingleReferenceCutoff)
            {
                return;
            }

            Point modeInfoPosition = new(
                blockOrigin.X >> Av1Constants.ModeInfoSizeLog2,
                blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2);

            ref Av1ReferenceMotionVectors referenceMotionVectors = ref this.state.BlockWorkspace.ReferenceMotionVectors;
            referenceMotionVectors.Build(
                this.state.Picture,
                modeInfoGrid,
                modeInfoAllocation,
                displacementVectors,
                referenceContexts,
                macroBlock,
                modeInfoPosition,
                blockSize,
                modeInfo.Block.PartitionType,
                this.state.Picture.Sequence.SequenceHeader,
                frameHeader,
                primaryReference,
                secondaryReference);

            // The model prunes no entry of the pair when a neighbor shares one of its references.
            bool tplPruning = this.state.TplInterModePruning &&
                !this.HasNeighborReferenceMatch(modeInfoGrid, modeInfoAllocation, macroBlock, blockOrigin, blockSize, primaryReference, secondaryReference);

            Span<byte> referenceCounts = stackalloc byte[Av1Constants.ReferenceFrameCount];
            Av1TileWriter.CollectNeighborReferenceCounts(modeInfoGrid, modeInfoAllocation, macroBlock, referenceCounts);
            int intraInterContext = Av1TileWriter.GetIntraInterContext(modeInfoGrid, modeInfoAllocation, macroBlock);
            int commonPredictionRate = Av1SymbolEncoder.GetIsInterCost(tables.ModeCosts, isInter: true, intraInterContext) +
                Av1SymbolEncoder.GetCompoundReferenceCost(
                    tables.ModeCosts,
                    primaryReference,
                    secondaryReference,
                    Av1SymbolContextHelper.GetReferenceModeContext(modeInfoGrid, modeInfoAllocation, macroBlock),
                    Av1SymbolContextHelper.GetCompoundReferenceTypeContext(modeInfoGrid, modeInfoAllocation, macroBlock),
                    referenceCounts);

            InlineArray16<Av1MotionVector> primaryVectorsStorage = default;
            InlineArray16<Av1MotionVector> secondaryVectorsStorage = default;
            InlineArray16<Av1PredictionMode> modesStorage = default;
            InlineArray16<byte> referenceIndicesStorage = default;
            Span<Av1MotionVector> primaryVectors = primaryVectorsStorage;
            Span<Av1MotionVector> secondaryVectors = secondaryVectorsStorage;
            Span<Av1PredictionMode> modes = modesStorage;
            Span<byte> referenceIndices = referenceIndicesStorage;
            int candidateCount = 0;
            primaryVectors[candidateCount] = referenceMotionVectors.GetCompoundNearestReference(0);
            secondaryVectors[candidateCount] = referenceMotionVectors.GetCompoundNearestReference(1);
            modes[candidateCount++] = Av1PredictionMode.NearestNearestMotionVector;

            int maximumNearIndex = Math.Min(2, Math.Max(0, referenceMotionVectors.Count - 2));
            for (int referenceIndex = 0; referenceIndex <= maximumNearIndex; referenceIndex++)
            {
                primaryVectors[candidateCount] = referenceMotionVectors.GetCompoundNearReference(referenceIndex, 0);
                secondaryVectors[candidateCount] = referenceMotionVectors.GetCompoundNearReference(referenceIndex, 1);
                modes[candidateCount] = Av1PredictionMode.NearNearMotionVector;
                referenceIndices[candidateCount++] = (byte)referenceIndex;
            }

            // A list or global component that points beyond the frame displacement region removes its entry.
            Rectangle referenceBounds = this.GetReferenceBounds(primaryReference);
            Rectangle frameBounds = Av1MotionVector.GetFrameSearchBounds(
                new Rectangle(blockOrigin, new Size(blockSize.GetWidth(), blockSize.GetHeight())),
                new Size(
                    this.state.Picture.Parent.Common.ModeInfoColumnCount << Av1Constants.ModeInfoSizeLog2,
                    this.state.Picture.Parent.Common.ModeInfoRowCount << Av1Constants.ModeInfoSizeLog2),
                Math.Min(referenceBounds.X, referenceBounds.Y));

            ReadOnlySpan<Av1MotionVector> primaryNewVectors = newMotionVectors[(int)primaryReference];
            ReadOnlySpan<Av1MotionVector> secondaryNewVectors = newMotionVectors[(int)secondaryReference];
            byte primaryNewVectorMask = newMotionVectorMasks[(int)primaryReference];
            byte secondaryNewVectorMask = newMotionVectorMasks[(int)secondaryReference];
            int sharedNewMask = primaryNewVectorMask & secondaryNewVectorMask;
            int maximumNewIndex = Math.Min(2, Math.Max(0, referenceMotionVectors.Count - 1));
            for (int referenceIndex = 0; referenceIndex <= maximumNewIndex; referenceIndex++)
            {
                if ((sharedNewMask & (1 << referenceIndex)) != 0)
                {
                    primaryVectors[candidateCount] = ClampToSubpixelRange(
                        primaryNewVectors[referenceIndex], referenceMotionVectors.GetCompoundNewReference(referenceIndex, 0), frameBounds);

                    secondaryVectors[candidateCount] = ClampToSubpixelRange(
                        secondaryNewVectors[referenceIndex], referenceMotionVectors.GetCompoundNewReference(referenceIndex, 1), frameBounds);

                    modes[candidateCount] = Av1PredictionMode.NewNewMotionVector;
                    referenceIndices[candidateCount++] = (byte)referenceIndex;
                }
            }

            if ((primaryNewVectorMask & 1) != 0)
            {
                primaryVectors[candidateCount] = ClampToSubpixelRange(
                    primaryNewVectors[0], referenceMotionVectors.GetCompoundNewReference(0, 0), frameBounds);

                secondaryVectors[candidateCount] = referenceMotionVectors.GetCompoundNearestReference(1);
                modes[candidateCount++] = Av1PredictionMode.NewNearestMotionVector;
            }

            if ((secondaryNewVectorMask & 1) != 0)
            {
                primaryVectors[candidateCount] = referenceMotionVectors.GetCompoundNearestReference(0);
                secondaryVectors[candidateCount] = ClampToSubpixelRange(
                    secondaryNewVectors[0], referenceMotionVectors.GetCompoundNewReference(0, 1), frameBounds);

                modes[candidateCount++] = Av1PredictionMode.NearestNewMotionVector;
            }

            // All list entries of one syntax mode complete before the next mode starts. The searched vector cache uses the signaled index.
            // Only a NEAR predictor moves forward in the reference stack.
            for (int referenceIndex = 0; referenceIndex <= maximumNearIndex; referenceIndex++)
            {
                if ((primaryNewVectorMask & (1 << referenceIndex)) != 0)
                {
                    primaryVectors[candidateCount] = ClampToSubpixelRange(
                        primaryNewVectors[referenceIndex], referenceMotionVectors.GetCompoundNewReference(referenceIndex + 1, 0), frameBounds);

                    secondaryVectors[candidateCount] = referenceMotionVectors.GetCompoundNearReference(referenceIndex, 1);
                    modes[candidateCount] = Av1PredictionMode.NewNearMotionVector;
                    referenceIndices[candidateCount++] = (byte)referenceIndex;
                }
            }

            for (int referenceIndex = 0; referenceIndex <= maximumNearIndex; referenceIndex++)
            {
                if ((secondaryNewVectorMask & (1 << referenceIndex)) != 0)
                {
                    primaryVectors[candidateCount] = referenceMotionVectors.GetCompoundNearReference(referenceIndex, 0);
                    secondaryVectors[candidateCount] = ClampToSubpixelRange(
                        secondaryNewVectors[referenceIndex], referenceMotionVectors.GetCompoundNewReference(referenceIndex + 1, 1), frameBounds);

                    modes[candidateCount] = Av1PredictionMode.NearNewMotionVector;
                    referenceIndices[candidateCount++] = (byte)referenceIndex;
                }
            }

            primaryVectors[candidateCount] = frameHeader
                .GetGlobalMotionParameters()[(int)primaryReference - 1]
                .GetMotionVector(frameHeader.AllowHighPrecisionMotionVector, blockSize, modeInfoPosition, frameHeader.ForceIntegerMotionVector);

            secondaryVectors[candidateCount] = frameHeader
                .GetGlobalMotionParameters()[(int)secondaryReference - 1]
                .GetMotionVector(frameHeader.AllowHighPrecisionMotionVector, blockSize, modeInfoPosition, frameHeader.ForceIntegerMotionVector);

            modes[candidateCount++] = Av1PredictionMode.GlobalGlobalMotionVector;

            Av1InterpolationFilter filter = frameHeader.InterpolationFilter == Av1InterpolationFilter.Switchable
                ? Av1InterpolationFilter.Regular : frameHeader.InterpolationFilter;

            bool maskedCompoundEnabled = this.state.Picture.Sequence.SequenceHeader.EnableMaskedCompound &&
                Math.Min(blockSize.GetWidth(), blockSize.GetHeight()) >= 8;

            int compoundGroupContext = Av1TileWriter.GetCompoundGroupIndexContext(modeInfoGrid, modeInfoAllocation, macroBlock);
            int compoundIndexContext = Av1SymbolContextHelper.GetCompoundIndexContext(
                this.state.Picture.Sequence.SequenceHeader.OrderHintInfo,
                frameHeader,
                primaryReference,
                secondaryReference,
                modeInfoGrid,
                modeInfoAllocation,
                macroBlock);

            ReadOnlySpan<Av1PredictionMode> primaryModes =
            [
                Av1PredictionMode.NearestMotionVector, Av1PredictionMode.NearMotionVector,
                Av1PredictionMode.NearestMotionVector, Av1PredictionMode.NewMotionVector,
                Av1PredictionMode.NearMotionVector, Av1PredictionMode.NewMotionVector,
                Av1PredictionMode.GlobalMotionVector, Av1PredictionMode.NewMotionVector
            ];

            ReadOnlySpan<Av1PredictionMode> secondaryModes =
            [
                Av1PredictionMode.NearestMotionVector, Av1PredictionMode.NearMotionVector,
                Av1PredictionMode.NewMotionVector, Av1PredictionMode.NearestMotionVector,
                Av1PredictionMode.NewMotionVector, Av1PredictionMode.NearMotionVector,
                Av1PredictionMode.GlobalMotionVector, Av1PredictionMode.NewMotionVector
            ];

            Av1EncoderSpeedSettings settings = this.state.Picture.Parent.SpeedSettings;
            InlineArray4<byte> referenceMasksStorage = this.GetCompoundReferenceMasks(workspaceStorage, searchedSingleModes);
            Span<byte> referenceMasks = referenceMasksStorage;
            bool primaryNeighborMatch = false;
            bool secondaryNeighborMatch = false;
            for (int neighborIndex = 0; neighborIndex < 2; neighborIndex++)
            {
                if (neighborIndex == 0 ? !macroBlock.IsLeftAvailable : !macroBlock.IsUpAvailable)
                {
                    continue;
                }

                Av1EncoderBlockModeInfo neighbor = macroBlock.GetRelativeModeInfo(
                    modeInfoGrid,
                    modeInfoAllocation,
                    neighborIndex == 0 ? -1 : -macroBlock.ModeInfoStride).Block;

                if (neighbor.ReferenceFrame > Av1ReferenceFrameType.Intra)
                {
                    primaryNeighborMatch |= neighbor.ReferenceFrame == primaryReference ||
                        neighbor.SecondaryReferenceFrame == primaryReference;

                    secondaryNeighborMatch |= neighbor.ReferenceFrame == secondaryReference ||
                        neighbor.SecondaryReferenceFrame == secondaryReference;
                }
            }

            InlineArray2<Av1MotionVector> previousPrimaryStorage = default;
            InlineArray2<Av1MotionVector> previousSecondaryStorage = default;
            Span<Av1MotionVector> previousPrimary = previousPrimaryStorage;
            Span<Av1MotionVector> previousSecondary = previousSecondaryStorage;
            Av1PredictionMode previousMode = Av1PredictionMode.PredictionModeCount;
            byte previousVectorMask = 0;
            bool modeImproved = false;
            bool rejectMode = false;
            long modeBestCost = long.MaxValue;
            int candidateMask = 0;
            InlineArray3<long> translationCostsStorage = default;
            InlineArray4<Av1RateDistortionStatistics> planeStatisticsStorage = default;
            Span<long> translationCosts = translationCostsStorage;
            Span<Av1RateDistortionStatistics> planeStatistics = planeStatisticsStorage;
            Span<int> modeThresholdFactors = this.state.BlockWorkspace.GetModeThresholdFactors(workspaceStorage);
            Span<long> singleReferenceFilterCosts = this.state.BlockWorkspace.GetSingleReferenceFilterCosts(workspaceStorage);
            for (int candidateIndex = 0; candidateIndex < candidateCount; candidateIndex++)
            {
                Av1MotionVector candidatePrimary = primaryVectors[candidateIndex];
                Av1MotionVector candidateSecondary = secondaryVectors[candidateIndex];
                Av1PredictionMode mode = modes[candidateIndex];
                if ((mode == Av1PredictionMode.NearestNearestMotionVector) != nearestOnly)
                {
                    continue;
                }

                bool startsMode = mode != previousMode;
                if (startsMode)
                {
                    this.RecordMotionModeWinner(modeBestCost, true, default, default, default);
                    modeBestCost = long.MaxValue;
                    previousMode = mode;
                    previousVectorMask = 0;
                    modeImproved = false;
                    rejectMode = Av1ModeThresholds.ShouldSkip(
                        modeThresholdFactors,
                        this.ModeThresholdQuantizerFactor,
                        this.ModeThresholdSkipMultiplier,
                        blockSize,
                        mode,
                        primaryReference,
                        secondaryReference,
                        Math.Min(this.state.BlockCostLimit, selectedStatistics.Cost),
                        selectedStatistics.AllTransformsEmpty);

                    rejectMode |= (this.state.InterModeSkipMasks[(int)primaryReference] & (1u << (int)mode)) != 0;
                    rejectMode |= this.GetInterModeCacheDecision(mode, primaryReference, secondaryReference) == 1;
                    rejectMode |= mode == Av1PredictionMode.NearNearMotionVector &&
                        this.ShouldPruneNearMode(
                            modeInfoGrid,
                            modeInfoAllocation,
                            macroBlock,
                            primaryReference,
                            secondaryReference,
                            Math.Min(this.state.BlockCostLimit, selectedStatistics.Cost));

                    int modeIndex = (int)mode - (int)Av1PredictionMode.CompoundInterModeStart;
                    for (int component = 0; component < 2; component++)
                    {
                        Av1PredictionMode singleMode = component == 0 ? primaryModes[modeIndex] : secondaryModes[modeIndex];
                        int singleOffset = (int)singleMode - (int)Av1PredictionMode.InterModeStart;
                        Av1ReferenceFrameType reference = component == 0 ? primaryReference : secondaryReference;
                        uint searchedBit = 1U << ((singleOffset * Av1Constants.ReferenceFrameCount) + (int)reference);
                        if ((searchedSingleModes & searchedBit) == 0 || (referenceMasks[singleOffset] & (1 << (int)reference)) != 0)
                        {
                            continue;
                        }

                        // Neighbor-derived stacks differ between single and compound syntax. A single
                        // result can reject that component only when every predictor being compared agrees.
                        bool samePredictors = true;
                        if (singleMode == Av1PredictionMode.NearestMotionVector)
                        {
                            samePredictors = singleReferenceVectors[(int)reference].Nearest ==
                                referenceMotionVectors.GetCompoundNearestReference(component);
                        }
                        else if (singleMode == Av1PredictionMode.NearMotionVector)
                        {
                            for (int index = 0; index <= maximumNearIndex; index++)
                            {
                                samePredictors &= singleReferenceVectors[(int)reference].GetNearReference(index) ==
                                    referenceMotionVectors.GetCompoundNearReference(index, component);
                            }
                        }

                        rejectMode |= samePredictors;
                    }
                }

                // The entries of one mode share a budget. Their own results, before the image tune bias, lower that budget.
                long modeCostLimit = Math.Min(Math.Min(this.state.BlockCostLimit, selectedStatistics.Cost), modeBestCost);
                if (startsMode && !rejectMode)
                {
                    bool nearMode = mode is Av1PredictionMode.NearNearMotionVector or
                        Av1PredictionMode.NearNewMotionVector or Av1PredictionMode.NewNearMotionVector;

                    int referenceCount = nearMode ? maximumNearIndex + 1 :
                        mode == Av1PredictionMode.NewNewMotionVector ? maximumNewIndex + 1 : 1;

                    candidateMask = (1 << referenceCount) - 1;
                    if (referenceCount > 1)
                    {
                        // A scaled reference is not pruned.
                        bool modelTranslation = nearMode && settings.PruneNearMotionByTranslation &&
                            blockSize.GetWidth() * blockSize.GetHeight() > 64 &&
                            !this.IsScaledReference(primaryReference) && !this.IsScaledReference(secondaryReference);

                        translationCosts[..].Fill(long.MaxValue);
                        long bestTranslation = long.MaxValue;
                        Av1EncoderBlockModeInfo prediction = modeInfo.Block;
                        prediction.Mode = mode;
                        prediction.ReferenceFrame = primaryReference;
                        prediction.SecondaryReferenceFrame = secondaryReference;
                        prediction.CompoundType = Av1CompoundType.Average;
                        prediction.CompoundGroupIndex = false;
                        prediction.CompoundIndex = true;
                        bool secondaryPastPair = primaryReference is Av1ReferenceFrameType.Last2 or Av1ReferenceFrameType.Last3 ||
                            secondaryReference is Av1ReferenceFrameType.Last2 or Av1ReferenceFrameType.Last3;

                        for (int index = candidateIndex; index < candidateCount && modes[index] == mode; index++)
                        {
                            // A later list entry of a pair that uses LAST2 or LAST3 is searched only when its stack weight marks a nearest
                            // neighbor.
                            if (settings.ReduceInterReferenceIndices != 0 && referenceIndices[index] > 0 && secondaryPastPair &&
                                referenceMotionVectors.Weights[referenceIndices[index] + (nearMode ? 1 : 0)] <
                                Av1ReferenceMotionVectors.NearestCandidateWeight)
                            {
                                candidateMask &= ~(1 << referenceIndices[index]);
                                continue;
                            }

                            if (!CompoundVectorsInFrameSearchBounds(mode, secondaryVectors[index], secondaryModes, frameBounds))
                            {
                                candidateMask &= ~(1 << referenceIndices[index]);
                                continue;
                            }

                            // The translation model prices the mode without the differential vectors.
                            Av1MotionVector entryPrimary = primaryVectors[index];
                            Av1MotionVector entrySecondary = secondaryVectors[index];
                            int entryIndex = referenceIndices[index];
                            int translationModeRate = GetCompoundInterModeRate(
                                in tables, in motionVectorCosts, mode, entryPrimary, entrySecondary, entryIndex, referenceMotionVectors) -
                                GetCompoundMotionRate(in motionVectorCosts, mode, entryPrimary, entrySecondary, entryIndex, referenceMotionVectors);

                            int drlRate = translationModeRate -
                                Av1SymbolEncoder.GetInterCompoundModeCost(tables.ModeCosts, mode, referenceMotionVectors.ModeContext);

                            if (Av1RateDistortion.GetCost(this.state.RateMultiplier, commonPredictionRate + drlRate, 0) > modeCostLimit)
                            {
                                candidateMask &= ~(1 << referenceIndices[index]);
                                continue;
                            }

                            if (modelTranslation &&
                                Av1RateDistortion.GetCost(this.state.RateMultiplier, commonPredictionRate + translationModeRate, 0) <= modeCostLimit)
                            {
                                // The model predicts a new-vector component from the raw stack entry at the signaled index, not from a
                                // searched vector.
                                int componentModeIndex = (int)mode - (int)Av1PredictionMode.CompoundInterModeStart;
                                Av1MotionVector translationPrimary = primaryModes[componentModeIndex] == Av1PredictionMode.NewMotionVector
                                    ? referenceMotionVectors.GetCompoundNewReference(referenceIndices[index], 0)
                                    : primaryVectors[index];

                                Av1MotionVector translationSecondary = secondaryModes[componentModeIndex] == Av1PredictionMode.NewMotionVector
                                    ? referenceMotionVectors.GetCompoundNewReference(referenceIndices[index], 1)
                                    : secondaryVectors[index];

                                long translationLumaSquaredError = 0;
                                Av1RateDistortionStatistics estimate = this.GetInterFilterModelCost(
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
                                    translationPrimary,
                                    translationSecondary,
                                    prediction,
                                    blockOrigin,
                                    blockSize,
                                    false,
                                    filter,
                                    filter,
                                    commonPredictionRate + translationModeRate,
                                    long.MaxValue,
                                    100,
                                    0,
                                    this.GetInterSamples(in interWorkspace, 3),
                                    this.GetInterSamples(in interWorkspace, 4),
                                    this.GetInterSamples(in interWorkspace, 5),
                                    planeStatistics,
                                    ref translationLumaSquaredError);

                                // The estimate also predicts luma into the frame.
                                this.WriteInterLumaDestination(lumaFrame, blockOrigin, blockSize, this.GetInterSamples(in interWorkspace, 3));

                                // The model stores its luma error as the prediction error of the first reference.
                                this.SetPredictionSse(prediction.ReferenceFrame, translationLumaSquaredError);
                                translationCosts[referenceIndices[index]] = estimate.Cost;
                                bestTranslation = Math.Min(bestTranslation, estimate.Cost);
                            }
                        }

                        if (modelTranslation)
                        {
                            for (int index = 0; index < referenceCount; index++)
                            {
                                if (!((double)translationCosts[index] / bestTranslation < 1.05 &&
                                    (double)translationCosts[index] / modeCostLimit < 5))
                                {
                                    candidateMask &= ~(1 << index);
                                }
                            }
                        }
                    }
                }

                if (rejectMode || (candidateMask & (1 << referenceIndices[candidateIndex])) == 0 ||
                    !CompoundVectorsInFrameSearchBounds(mode, candidateSecondary, secondaryModes, frameBounds))
                {
                    continue;
                }

                if (tplPruning && this.PrunesInterModeByTpl(
                    modeCostLimit,
                    primaryReference,
                    secondaryReference,
                    referenceIndices[candidateIndex],
                    mode))
                {
                    continue;
                }

                bool mixedMode = mode is Av1PredictionMode.NearestNewMotionVector or Av1PredictionMode.NewNearestMotionVector or
                    Av1PredictionMode.NearNewMotionVector or Av1PredictionMode.NewNearMotionVector;

                if (mixedMode)
                {
                    if (settings.SkipMixedNearCompound &&
                        mode is Av1PredictionMode.NearNewMotionVector or Av1PredictionMode.NewNearMotionVector)
                    {
                        continue;
                    }

                    int matchingNeighbors = (primaryNeighborMatch ? 1 : 0) + (secondaryNeighborMatch ? 1 : 0);
                    if (matchingNeighbors < settings.CompoundNeighborPruningLevel)
                    {
                        continue;
                    }

                    Av1ReferenceFrameType searchedReference = mode is Av1PredictionMode.NewNearestMotionVector or Av1PredictionMode.NewNearMotionVector
                        ? primaryReference : secondaryReference;

                    if (settings.PruneMixedCompoundBySingleWinner &&
                        bestSingleModes[(int)searchedReference] != Av1PredictionMode.NewMotionVector)
                    {
                        continue;
                    }
                }

                int modeRate = commonPredictionRate + GetCompoundInterModeRate(
                    in tables, in motionVectorCosts, mode, candidatePrimary, candidateSecondary, referenceIndices[candidateIndex], referenceMotionVectors);

                if (mode != Av1PredictionMode.NearestNearestMotionVector &&
                    Av1RateDistortion.GetCost(this.state.RateMultiplier, modeRate, 0) > modeCostLimit)
                {
                    continue;
                }

                int referenceIndex = referenceIndices[candidateIndex];
                if (settings.CompoundReferenceIndexPruningLevel != 0)
                {
                    bool similar = false;
                    int threshold = 2 << (settings.CompoundReferenceIndexPruningLevel + 1);
                    for (int index = 0; index < referenceIndex && !modeImproved; index++)
                    {
                        if ((previousVectorMask & (1 << index)) != 0)
                        {
                            int distance = Math.Abs(previousPrimary[index].Row - candidatePrimary.Row) +
                                Math.Abs(previousPrimary[index].Column - candidatePrimary.Column) +
                                Math.Abs(previousSecondary[index].Row - candidateSecondary.Row) +
                                Math.Abs(previousSecondary[index].Column - candidateSecondary.Column);

                            similar |= distance <= threshold;
                        }
                    }

                    if (similar)
                    {
                        continue;
                    }

                    // Retain the vectors before blend refinement. A later DRL entry is compared with
                    // the same starting predictions, and an improving mode keeps all remaining entries.
                    if (referenceIndex < 2)
                    {
                        previousPrimary[referenceIndex] = candidatePrimary;
                        previousSecondary[referenceIndex] = candidateSecondary;
                        previousVectorMask |= (byte)(1 << referenceIndex);
                    }
                }

                if (mode == Av1PredictionMode.GlobalGlobalMotionVector &&
                    this.PrunesZeroVectorWithSse(sourceLuma, sourceBlue, sourceRed, blockOrigin, blockSize, primaryReference, secondaryReference))
                {
                    continue;
                }

                if (!this.SelectCompoundBlend(
                    writer,
                    in tables,
                    transformCoefficients,
                    dequantizedCoefficients,
                    transformWorkspace,
                    in interWorkspace,
                    in motionVectorCosts,
                    firstIntermediate,
                    secondIntermediate,
                    compoundMask,
                    in transformEdges,
                    in lumaCoefficientEdges,
                    modeInfoGrid,
                    modeInfoAllocation,
                    displacementVectors,
                    workspaceStorage,
                    sourceLuma,
                    sourceBlue,
                    sourceRed,
                    reconstructionLuma,
                    reconstructionBlue,
                    reconstructionRed,
                    macroBlock,
                    blockOrigin,
                    blockSize,
                    modeCostLimit,
                    commonPredictionRate,
                    modes[candidateIndex],
                    primaryReference,
                    secondaryReference,
                    filter,
                    referenceIndices[candidateIndex],
                    referenceMotionVectors,
                    topAverageCosts,
                    compoundMaskHistory,
                    compoundTypeHistory,
                    ref candidatePrimary,
                    ref candidateSecondary,
                    out Av1CompoundType compoundType,
                    out int wedgeIndex,
                    out bool wedgeSign,
                    out Av1DifferenceWeightedMaskType maskType,
                    out int blendRate))
                {
                    continue;
                }

                // The blend search above chose how the two predictions combine. The two flags below carry that choice to the bitstream. The
                // group flag separates the masked blends from the rest. The index then separates a plain average from a distance-weighted
                // average. Each mode starts from simple translation, whatever an earlier trial left in the block.
                Av1EncoderBlockModeInfo candidateMode = modeInfo.Block;
                candidateMode.ReferenceFrame = primaryReference;
                candidateMode.SecondaryReferenceFrame = secondaryReference;
                candidateMode.Mode = modes[candidateIndex];
                candidateMode.MotionMode = Av1MotionMode.SimpleTranslation;
                candidateMode.SkipMode = false;
                candidateMode.CompoundType = compoundType;
                candidateMode.CompoundGroupIndex = compoundType is Av1CompoundType.Wedge or Av1CompoundType.DifferenceWeighted;
                candidateMode.CompoundIndex = compoundType != Av1CompoundType.DistanceWeighted;
                candidateMode.CompoundWedgeIndex = (byte)wedgeIndex;
                candidateMode.CompoundWedgeSign = wedgeSign;
                candidateMode.DifferenceWeightedMaskType = maskType;
                candidateMode.HorizontalInterpolationFilter = filter;
                candidateMode.VerticalInterpolationFilter = filter;

                // A compound mode names one single-reference mode for each of its two references. The costs of those single-reference
                // searches are already known, so the cheaper of the two gives this candidate a bound. A compound prediction that cannot come
                // near the better of its own halves does not get a filter search.
                //
                // The two indices address one table laid out as mode, then list index, then reference frame. Each step multiplies by the
                // size of the level below it.
                int compoundModeIndex = (int)modes[candidateIndex] - (int)Av1PredictionMode.CompoundInterModeStart;
                int primaryCostIndex = (((((int)primaryModes[compoundModeIndex] - (int)Av1PredictionMode.InterModeStart) * 3) +
                    referenceIndices[candidateIndex]) * Av1Constants.ReferenceFrameCount) + (int)primaryReference;

                int secondaryCostIndex = (((((int)secondaryModes[compoundModeIndex] - (int)Av1PredictionMode.InterModeStart) * 3) +
                    referenceIndices[candidateIndex]) * Av1Constants.ReferenceFrameCount) + (int)secondaryReference;

                long singleReferenceCost = Math.Min(
                    singleReferenceFilterCosts[primaryCostIndex],
                    singleReferenceFilterCosts[secondaryCostIndex]);

                bool preparedPrediction = this.SelectInterFilters(
                    in tables,
                    in interWorkspace,
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
                    reconstructionLuma,
                    reconstructionBlue,
                    reconstructionRed,
                    macroBlock,
                    blockOrigin,
                    blockSize,
                    block.HasChroma,
                    candidatePrimary,
                    candidateSecondary,
                    ref candidateMode,
                    modeCostLimit,
                    singleReferenceCost,
                    out int filterRate,
                    out long filterModelCost);

                // The filter search prices the prediction with a model, not with a transform, so the thresholds below are loose on purpose.
                // When the speed setting allows it, the candidate drops when three eighths of the model cost exceed the block bound. The
                // candidate always drops when six eighths of the model cost exceed the cost of the better half of this compound pair.
                if (filterModelCost == long.MaxValue ||
                    (modeCostLimit != long.MaxValue &&
                        ((this.state.Picture.Parent.SpeedSettings.ModelBasedInterpolationBreakout &&
                            (filterModelCost >> 3) * 3 > modeCostLimit) ||
                        (filterModelCost >> 3) * 6 > singleReferenceCost)))
                {
                    continue;
                }

                // A new vector equal to its reference codes no difference, so the mode is invalid.
                if (!HasCodedCompoundNewVectors(
                    modes[candidateIndex], candidatePrimary, candidateSecondary, referenceIndices[candidateIndex], referenceMotionVectors))
                {
                    continue;
                }

                Av1RateDistortionStatistics candidateStatistics = this.EvaluateInterCandidate(
                    writer,
                    in tables,
                    transformCoefficients,
                    dequantizedCoefficients,
                    searchDequantizedCoefficients,
                    transformWorkspace,
                    transformTypeProbabilities,
                    in interWorkspace,
                    in motionVectorCosts,
                    transformPrediction,
                    firstIntermediate,
                    secondIntermediate,
                    compoundMask,
                    in transformEdges,
                    in lumaCoefficientEdges,
                    in blueCoefficientEdges,
                    in redCoefficientEdges,
                    modeInfoGrid,
                    modeInfoAllocation,
                    displacementVectors,
                    workspaceStorage,
                    sourceLuma,
                    sourceBlue,
                    sourceRed,
                    reconstructionLuma,
                    reconstructionBlue,
                    reconstructionRed,
                    macroBlock,
                    blockOrigin,
                    blockSize,
                    modeCostLimit,
                    block.HasChroma,
                    commonPredictionRate + filterRate,
                    primaryReference,
                    candidatePrimary,
                    candidateSecondary,
                    modes[candidateIndex],
                    secondaryReference,
                    usePreparedPrediction: preparedPrediction,
                    compoundType,
                    wedgeIndex,
                    wedgeSign,
                    maskType,
                    blendRate,
                    candidateMode.HorizontalInterpolationFilter,
                    candidateMode.VerticalInterpolationFilter,
                    referenceIndices[candidateIndex],
                    false,
                    default,
                    false,
                    0,
                    referenceMotionVectors,
                    this.GetInterSamples(in interWorkspace, 6),
                    interWorkspace.LumaCandidateCoefficients,
                    this.GetInterSamples(in interWorkspace, 7),
                    interWorkspace.BlueCandidateCoefficients,
                    this.GetInterSamples(in interWorkspace, 8),
                    interWorkspace.RedCandidateCoefficients,
                    out bool candidateSkip,
                    out InlineArray64<Av1EncoderTransformBlockState> candidateLumaStates,
                    out InlineArray16<Av1TransformSize> candidateLumaSizes,
                    out InlineArray16<Av1EncoderTransformBlockState> candidateBlueState,
                    out InlineArray16<Av1EncoderTransformBlockState> candidateRedState);

                modeBestCost = Math.Min(modeBestCost, candidateStatistics.Cost);
                if (candidateStatistics.Cost >= Math.Min(this.state.BlockCostLimit, selectedStatistics.Cost))
                {
                    continue;
                }

                modeImproved = true;
                selectedStatistics = candidateStatistics;
                selectedVector = candidatePrimary;
                selectedSecondaryVector = candidateSecondary;
                candidateLumaStates[..].CopyTo(selectedStates);

                // Setting the block transform size resets every transform size, so the searched tree is copied after it.
                modeInfo.Block.TransformSize = this.BlockLossless ? Av1TransformSize.Size4x4 : blockSize.GetMaximumTransformSize();
                candidateLumaSizes[..].CopyTo(modeInfo.Block.InterTransformSizes);
                candidateBlueState[..].CopyTo(selectedStates[64..80]);
                candidateRedState[..].CopyTo(selectedStates[80..96]);
                modeInfo.Block.ReferenceFrame = primaryReference;
                modeInfo.Block.SecondaryReferenceFrame = secondaryReference;
                modeInfo.Block.Mode = modes[candidateIndex];

                // A compound winner codes simple translation outside skip mode, whatever an earlier single-reference winner left in the block.
                modeInfo.Block.MotionMode = candidateMode.MotionMode;
                modeInfo.Block.SkipMode = candidateMode.SkipMode;
                modeInfo.Block.Skip = candidateSkip;
                modeInfo.Block.CompoundGroupIndex = compoundType is Av1CompoundType.Wedge or Av1CompoundType.DifferenceWeighted;
                modeInfo.Block.CompoundIndex = compoundType != Av1CompoundType.DistanceWeighted;
                modeInfo.Block.CompoundType = compoundType;
                modeInfo.Block.CompoundWedgeIndex = (byte)wedgeIndex;
                modeInfo.Block.CompoundWedgeSign = wedgeSign;
                modeInfo.Block.DifferenceWeightedMaskType = maskType;
                modeInfo.Block.HorizontalInterpolationFilter = candidateMode.HorizontalInterpolationFilter;
                modeInfo.Block.VerticalInterpolationFilter = candidateMode.VerticalInterpolationFilter;
                block.ReferenceMotionVectorIndex = referenceIndices[candidateIndex];
            }

            this.RecordMotionModeWinner(modeBestCost, true, default, default, default);
        }

        /// <summary>
        /// Searches the partition types of one block, from the unsplit block through the split, the rectangles and the extended shapes,
        /// and keeps the cheapest.
        /// </summary>
        /// <param name="writer">The symbol encoder that prices the syntax.</param>
        /// <param name="tables">The rate tables of the tile.</param>
        /// <param name="modeWorkspace">The mode decision buffers of the block.</param>
        /// <param name="transformCoefficients">The forward transform output buffer.</param>
        /// <param name="dequantizedCoefficients">The dequantized coefficient buffer of the candidate.</param>
        /// <param name="searchDequantizedCoefficients">The dequantized coefficient buffer of the winner.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
        /// <param name="transformTypeProbabilities">The transform type probabilities of every update type and size.</param>
        /// <param name="blockResidual">The residual buffer of the motion search.</param>
        /// <param name="searchCoefficients">The quantized coefficient buffer of the winner.</param>
        /// <param name="searchReconstructions">The storage of the candidate and winner reconstructions of the type search.</param>
        /// <param name="estimationRowCoefficients">The coefficients of one row of estimation transforms.</param>
        /// <param name="interWorkspace">The inter prediction buffers of the block.</param>
        /// <param name="motionSearchPrediction">The prediction buffer of the motion search.</param>
        /// <param name="motionVectorCosts">The motion vector rates of the frame precision.</param>
        /// <param name="transformPrediction">The prediction storage of one transform block.</param>
        /// <param name="interIntraAbove">The extended above edge of an inter-intra prediction.</param>
        /// <param name="interIntraLeft">The extended left edge of an inter-intra prediction.</param>
        /// <param name="firstIntermediate">The compound intermediate of the first reference.</param>
        /// <param name="secondIntermediate">The compound intermediate of the second reference.</param>
        /// <param name="compoundMask">The blend mask of a masked compound prediction.</param>
        /// <param name="partitionEdges">The partition context edges of the tile.</param>
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
        /// <param name="macroBlock">The neighbor availability of the block.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="nodeIndex">The partition tree node of the block.</param>
        /// <param name="costLimit">The cost above which the search stops.</param>
        /// <param name="selectedStatistics">The rate and distortion of the selected partition.</param>
        /// <param name="noneCost">The cost of the unsplit block.</param>
        /// <param name="rectangleWins">Bit zero when the horizontal halves win, and bit one when the vertical halves win.</param>
        /// <returns>The selected partition type.</returns>
        public Av1PartitionType SelectBestPartitionCore(
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
            Span<int> estimationRowCoefficients,
            in Av1EncoderInterPredictionWorkspace<byte> interWorkspace,
            Span<byte> motionSearchPrediction,
            in Av1MotionVectorCosts motionVectorCosts,
            Span<byte> transformPrediction,
            Span<byte> interIntraAbove,
            Span<byte> interIntraLeft,
            Span<ushort> firstIntermediate,
            Span<ushort> secondIntermediate,
            Span<byte> compoundMask,
            in Av1NeighborEdges<Av1PartitionContext> partitionEdges,
            in Av1NeighborEdges<byte> transformEdges,
            in Av1NeighborEdges<Av1EncoderPaletteInfo> paletteEdges,
            in Av1NeighborEdges<byte> lumaCoefficientEdges,
            in Av1NeighborEdges<byte> blueCoefficientEdges,
            in Av1NeighborEdges<byte> redCoefficientEdges,
            Span<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            Span<Av1EncoderDisplacementVector> displacementVectors,
            Span<Av1EncoderReferenceContext> referenceContexts,
            Span<byte> encoderSegmentMap,
            Span<byte> searchSegmentMap,
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
            int nodeIndex,
            Av1RateDistortionStatistics costLimit,
            out Av1RateDistortionStatistics selectedStatistics,
            out long noneCost,
            out byte rectangleWins)
        {
            // The superblock retry keeps the rectangle decisions of the first search. The retry starts after the pruning that runs before
            // the search, and the reset of the partition limits does not change the rectangle flags.
            bool retrying = false;
            bool retainedRectangularSplit = true;
            bool retainedPruneHorizontal = false;
            bool retainedPruneVertical = false;

        SearchPartitions:
            if (blockSize == Av1BlockSize.Block64x64 && this.state.Picture.Parent.FrameHeader.IsIntra)
            {
                this.state.IntraPartitionFeaturesValid = false;
            }

            rectangleWins = 3;
            InlineArray4<byte> childRectangleWinStorage = default;
            Span<byte> childRectangleWins = childRectangleWinStorage;
            childRectangleWins[..].Fill(3);
            Point modeInfoPosition = new(blockOrigin.X >> Av1Constants.ModeInfoSizeLog2, blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2);
            Av1TileWriter.SetModeInfoRowAndColumn(
                macroBlock,
                macroBlock.Tile,
                modeInfoPosition,
                blockSize,
                this.state.Picture.Parent.Common.ModeInfoStride,
                this.state.Picture.Parent.Common.ModeInfoRowCount,
                this.state.Picture.Parent.Common.ModeInfoColumnCount);

            if (nodeIndex == 0 && !this.state.MustFindValidPartition && !this.state.Picture.Parent.FrameHeader.IsIntra)
            {
                Av1ReferenceMotionVectors starts = default;
                starts.Build(
                    this.state.Picture,
                    modeInfoGrid,
                    modeInfoAllocation,
                    displacementVectors,
                    referenceContexts,
                    macroBlock,
                    modeInfoPosition,
                    blockSize,
                    Av1PartitionType.None,
                    this.state.Picture.Sequence.SequenceHeader,
                    this.state.Picture.Parent.FrameHeader,
                    this.state.SimpleMotionReference,
                    Av1ReferenceFrameType.None);

                Av1MotionVector nearest = starts.Nearest;

                // Round the initial spatial predictor to whole samples, with half samples away from zero.
                Av1MotionVector fullStart = new(
                    ((nearest.Row + 3 + (nearest.Row >= 0 ? 1 : 0)) >> 3) * 8,
                    ((nearest.Column + 3 + (nearest.Column >= 0 ? 1 : 0)) >> 3) * 8);

                Span<Av1SimpleMotionData> nodes = this.state.BlockWorkspace.GetSimpleMotionData(workspaceStorage);
                nodes.Clear();
                foreach (ref Av1SimpleMotionData node in nodes)
                {
                    node.Starts[(int)Av1ReferenceFrameType.Last] = fullStart;
                }

                Av1EncoderSpeedSettings settings = this.state.Picture.Parent.SpeedSettings;

                // An intra picture has no motion to predict a partition size from, so it keeps the speed cap.
                if (settings.MaximumPartitionPredictionMode != Av1EncoderSpeedSettings.MaximumPartitionPrediction.Disabled &&
                    !this.state.Picture.Parent.FrameHeader.IsIntra &&
                    blockSize == Av1BlockSize.Block128x128 && !this.state.Picture.Parent.FrameHeader.AllowScreenContentTools &&
                    this.state.Picture.Parent.FrameUpdateType is not (Av1FrameUpdateType.Overlay or Av1FrameUpdateType.IntermediateOverlay) &&
                    blockOrigin.X + 128 <= (this.state.Picture.Parent.Common.ModeInfoColumnCount << Av1Constants.ModeInfoSizeLog2) &&
                    blockOrigin.Y + 128 <= (this.state.Picture.Parent.Common.ModeInfoRowCount << Av1Constants.ModeInfoSizeLog2))
                {
                    this.state.MaximumPartitionSize = (Av1BlockSize)Math.Clamp(
                        (int)this.PredictMaximumPartition(
                            workspaceStorage,
                            sourceLuma,
                            sourceBlue,
                            sourceRed,
                            reconstructionLuma,
                            reconstructionBlue,
                            reconstructionRed,
                            motionSearchPrediction,
                            interWorkspace.FilterRows,
                            in motionVectorCosts,
                            this.GetCandidateReconstruction(in modeWorkspace, 0),
                            blockOrigin),
                        (int)settings.MinimumPartitionSize,
                        Math.Min((int)settings.MaximumPartitionSize, (int)this.state.Picture.Sequence.SequenceHeader.SuperblockSize));
                }
            }

            int savedLumaArea = this.state.CodedAreaLuma;
            int savedChromaArea = this.state.CodedAreaChroma;
            this.SavePartitionTrialContexts(
                workspaceStorage,
                in partitionEdges,
                in transformEdges,
                in paletteEdges,
                in lumaCoefficientEdges,
                in blueCoefficientEdges,
                in redCoefficientEdges,
                blockOrigin,
                blockSize);

            Av1RateDistortionStatistics bestStatistics = costLimit;
            selectedStatistics = Av1RateDistortionStatistics.Invalid;
            Av1PartitionType selectedPartition = Av1PartitionType.None;
            ReadOnlySpan<Av1PartitionType> searchOrder = PartitionSearchOrder;
            int candidateCount = blockSize == Av1BlockSize.Block8x8 ? 4 : searchOrder.Length;
            bool noneInvalid = true;
            bool splitInvalid = true;
            noneCost = 0;
            long nonePartitionCost = long.MaxValue;
            InlineArray4<long> splitNoneCostStorage = default;
            InlineArray2<long> horizontalCostStorage = default;
            InlineArray2<long> verticalCostStorage = default;
            InlineArray4<Av1AsymmetricModeCacheEntry> splitModeCacheStorage = default;
            InlineArray2<Av1AsymmetricModeCacheEntry> horizontalModeCacheStorage = default;
            InlineArray2<Av1AsymmetricModeCacheEntry> verticalModeCacheStorage = default;
            Span<long> splitNoneCosts = splitNoneCostStorage;
            Span<long> horizontalCosts = horizontalCostStorage;
            Span<long> verticalCosts = verticalCostStorage;
            Span<Av1AsymmetricModeCacheEntry> splitModeCache = splitModeCacheStorage;
            Span<Av1AsymmetricModeCacheEntry> horizontalModeCache = horizontalModeCacheStorage;
            Span<Av1AsymmetricModeCacheEntry> verticalModeCache = verticalModeCacheStorage;
            int asymmetricMask = 15;
            int fourStripMask = 3;
            int parentSourceVariance = -1;
            ObuFrameHeader frameHeader = this.state.Picture.Parent.FrameHeader;
            Av1EncoderSpeedSettings partitionSettings = this.state.Picture.Parent.SpeedSettings;

            // Asymmetric candidates exist only above 8x8, so no smaller block keeps a cached decision.
            bool cacheAsymmetricModes = blockSize > Av1BlockSize.Block8x8 &&
                partitionSettings.ReuseBestPredictionForAsymmetricPartitions(frameHeader.IsIntra);

            Av1BlockSize extendedThreshold = partitionSettings.GetExtendedPartitionThreshold(
                frameHeader.AllowScreenContentTools, frameHeader.IsIntra, this.state.Picture.Parent.FrameUpdateType);

            bool restrictExtendedToWinner = !this.state.MustFindValidPartition && partitionSettings.RestrictExtendedPartitionsToWinner(
                frameHeader.AllowScreenContentTools, frameHeader.IsIntra);

            // A losing split suppresses non-square searches, except a simple rectangle that
            // crosses an active frame edge. Directional mode pruning remains a separate decision.
            int halfWidth = blockSize.GetWidth() >> 1;
            int halfHeight = blockSize.GetHeight() >> 1;
            bool activeHorizontalEdge = blockOrigin.Y == 0 ||
                (this.state.Picture.Parent.Common.ModeInfoRowCount << Av1Constants.ModeInfoSizeLog2) < blockOrigin.Y + halfHeight;

            bool activeVerticalEdge = blockOrigin.X == 0 ||
                (this.state.Picture.Parent.Common.ModeInfoColumnCount << Av1Constants.ModeInfoSizeLog2) < blockOrigin.X + halfWidth;

            // Asymmetric partitions exist only where both block midpoints are inside the frame.
            bool midpointsInsideFrame =
                blockOrigin.Y + halfHeight < (this.state.Picture.Parent.Common.ModeInfoRowCount << Av1Constants.ModeInfoSizeLog2) &&
                blockOrigin.X + halfWidth < (this.state.Picture.Parent.Common.ModeInfoColumnCount << Av1Constants.ModeInfoSizeLog2);

            int sub8PruningLevel = partitionSettings.GetSub8PartitionPruningLevel(frameHeader.AllowScreenContentTools);
            bool pruneSmallSplits = !this.state.MustFindValidPartition && blockSize == Av1BlockSize.Block8x8 &&
                (sub8PruningLevel == 2 ||
                 (sub8PruningLevel == 1 && macroBlock.IsLeftAvailable && macroBlock.IsUpAvailable &&
                  (macroBlock.GetRelativeModeInfo(modeInfoGrid, modeInfoAllocation, -1).Block.BlockSize > Av1BlockSize.Block8x8 ||
                   macroBlock.GetRelativeModeInfo(modeInfoGrid, modeInfoAllocation, -macroBlock.ModeInfoStride).Block.BlockSize > Av1BlockSize.Block8x8)));

            bool allowRectangularSplit = true;
            bool pruneExtendedPartitions = false;
            bool allowExtendedStage = true;
            bool pruneHorizontalRectangle = false;
            bool pruneVerticalRectangle = false;
            (int motionSplitLevel, int motionAggressiveness, bool motionTerminateNone) = partitionSettings.GetSimpleMotionPartitionSettings(
                frameHeader.AllowScreenContentTools, this.state.Picture.Parent.FrameUpdateType);

            bool allowMotionNone = true;
            bool allowMotionSplit = !pruneSmallSplits && this.IsPartitionCandidateAllowed(blockOrigin, blockSize, Av1PartitionType.Split);
            bool squarePartitionsOnly = false;
            int intraPruningLevel = partitionSettings.GetIntraPartitionPruningLevel();
            if (!this.state.MustFindValidPartition && frameHeader.IsIntra && intraPruningLevel != 0 && blockSize <= Av1BlockSize.Block64x64 &&
                blockOrigin.X + blockSize.GetWidth() <= (this.state.Picture.Parent.Common.ModeInfoColumnCount << Av1Constants.ModeInfoSizeLog2) &&
                blockOrigin.Y + blockSize.GetHeight() <= (this.state.Picture.Parent.Common.ModeInfoRowCount << Av1Constants.ModeInfoSizeLog2))
            {
                squarePartitionsOnly = this.PruneIntraPartitions(
                    workspaceStorage,
                    sourceLuma,
                    sourceBlue,
                    sourceRed,
                    blockOrigin,
                    blockSize,
                    intraPruningLevel,
                    ref allowMotionNone,
                    ref allowMotionSplit,
                    ref allowRectangularSplit);
            }

            if (!this.state.MustFindValidPartition && !frameHeader.IsIntra && motionAggressiveness >= 0 &&
                frameHeader.FrameSize.SuperResolutionDenominator == Av1Constants.ScaleNumerator)
            {
                this.PrunePartitionsBySimpleMotion(
                    motionSearchPrediction,
                    interWorkspace.FilterRows,
                    in motionVectorCosts,
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
                    blockOrigin,
                    blockSize,
                    nodeIndex,
                    motionSplitLevel,
                    motionAggressiveness,
                    ref allowMotionNone,
                    ref allowMotionSplit,
                    ref allowRectangularSplit,
                    ref pruneHorizontalRectangle,
                    ref pruneVerticalRectangle);

                squarePartitionsOnly |= !allowMotionNone;
            }

            if (retrying)
            {
                allowRectangularSplit = retainedRectangularSplit;
                pruneHorizontalRectangle = retainedPruneHorizontal;
                pruneVerticalRectangle = retainedPruneVertical;
            }

            // A block above the largest partition can only split, whatever the models above decided. This rule applies after the pruning
            // that runs before the search.
            if (blockSize > this.state.MaximumPartitionSize)
            {
                allowMotionNone = false;
                allowMotionSplit = true;
                allowRectangularSplit = false;
            }

            if (this.state.Picture.Parent.EncoderOptions.IsAllIntra &&
                (blockSize >= Av1BlockSize.Block16x16 || (!this.state.MustFindValidPartition && partitionSettings.Speed >= HeifEncodingSpeed.Level6)))
            {
                int right = Math.Min(
                    blockOrigin.X + blockSize.GetWidth(),
                    this.state.Picture.Parent.Common.ModeInfoColumnCount << Av1Constants.ModeInfoSizeLog2);

                int bottom = Math.Min(
                    blockOrigin.Y + blockSize.GetHeight(),
                    this.state.Picture.Parent.Common.ModeInfoRowCount << Av1Constants.ModeInfoSizeLog2);

                (double minimum, double maximum) = this.GetLogSubBlockVariance(
                    workspaceStorage,
                    sourceLuma,
                    sourceBlue,
                    sourceRed,
                    new Rectangle(blockOrigin.X, blockOrigin.Y, right - blockOrigin.X, bottom - blockOrigin.Y));

                // A split separates sharp detail from an almost-flat quarter before ringing spreads across the larger block. This test can
                // enable the square split again after the learned model turned it off.
                if (blockSize >= Av1BlockSize.Block16x16 && minimum < 0.272 && maximum - minimum > 3D)
                {
                    allowMotionNone = false;
                    allowMotionSplit = true;
                }
                else if (!this.state.MustFindValidPartition && partitionSettings.Speed >= HeifEncodingSpeed.Level6 && maximum - minimum < 3D)
                {
                    allowRectangularSplit = false;
                }
            }

            // The simple motion data exists only for inter frames.
            Span<Av1SimpleMotionData> simpleMotionData = frameHeader.IsIntra ? default : this.state.BlockWorkspace.GetSimpleMotionData(workspaceStorage);
            for (int candidateIndex = 0; candidateIndex < candidateCount; candidateIndex++)
            {
                Av1PartitionType partitionType = searchOrder[candidateIndex];

                // Neither the unsplit candidate nor the split candidate produced a cost, so nothing remains that this block can code. A
                // candidate that the search skipped counts as one that produced no cost. This test runs after the split search.
                if (candidateIndex > SplitSearchOrderIndex && noneInvalid && splitInvalid &&
                    !this.state.MustFindValidPartition &&
                    partitionSettings.TerminatePartitionSearchAfterInvalidNoneAndSplit &&
                    blockSize != this.state.Picture.Sequence.SequenceHeader.SuperblockSize)
                {
                    break;
                }

                // A split stage that is not searched still runs the pruning that follows it, with no split cost.
                if (partitionType == Av1PartitionType.Split &&
                    (!allowMotionSplit || pruneSmallSplits || !this.IsPartitionCandidateAllowed(blockOrigin, blockSize, partitionType)))
                {
                    InlineArray4<long> unsearchedCostStorage = default;
                    Span<long> unsearchedCosts = unsearchedCostStorage;
                    bool noneAndSplitInvalid = !this.state.MustFindValidPartition && ShouldTerminatePartitionSearchAfterNoneAndSplit(
                        partitionSettings.TerminatePartitionSearchAfterInvalidNoneAndSplit,
                        blockSize,
                        this.state.Picture.Sequence.SequenceHeader.SuperblockSize,
                        noneInvalid,
                        true);

                    if (this.PrunePartitionsAfterSplit(
                        workspaceStorage,
                        sourceLuma,
                        sourceBlue,
                        sourceRed,
                        reconstructionLuma,
                        reconstructionBlue,
                        reconstructionRed,
                        in modeWorkspace,
                        motionSearchPrediction,
                        interWorkspace.FilterRows,
                        in motionVectorCosts,
                        blockOrigin,
                        blockSize,
                        nodeIndex,
                        noneInvalid,
                        bestStatistics.Cost,
                        nonePartitionCost,
                        noneCost,
                        long.MaxValue,
                        unsearchedCosts,
                        noneAndSplitInvalid,
                        ref allowRectangularSplit,
                        ref pruneExtendedPartitions,
                        ref pruneHorizontalRectangle,
                        ref pruneVerticalRectangle,
                        ref parentSourceVariance))
                    {
                        break;
                    }

                    continue;
                }

                if ((!allowMotionNone && partitionType == Av1PartitionType.None) ||
                    (squarePartitionsOnly && partitionType is not (Av1PartitionType.None or Av1PartitionType.Split)))
                {
                    continue;
                }

                if (pruneSmallSplits && partitionType != Av1PartitionType.None)
                {
                    continue;
                }

                if (!allowRectangularSplit &&
                    ((partitionType == Av1PartitionType.Horizontal && !activeHorizontalEdge) ||
                     (partitionType == Av1PartitionType.Vertical && !activeVerticalEdge) ||
                     (partitionType >= Av1PartitionType.HorizontalA && bestStatistics.Cost != long.MaxValue)))
                {
                    continue;
                }

                if (partitionType is Av1PartitionType.HorizontalA or Av1PartitionType.Horizontal4)
                {
                    // Resolve the stage from its incoming winner once. A winning asymmetric candidate
                    // must not retroactively exclude the remaining candidates in the same family.
                    bool incompatibleWinner = restrictExtendedToWinner &&
                        (partitionType == Av1PartitionType.HorizontalA
                            ? selectedPartition is not (Av1PartitionType.Horizontal or Av1PartitionType.Vertical)
                            : selectedPartition == Av1PartitionType.None);

                    bool widthAllowed = partitionType != Av1PartitionType.Horizontal4 ||
                        blockSize.GetWidth() >= (partitionSettings.MinimumPartitionSize.GetWidth() << partitionSettings.FourStripPartitionMinimumScale);

                    allowExtendedStage = bestStatistics.Cost == long.MaxValue ||
                        (!pruneExtendedPartitions && blockSize > extendedThreshold && !incompatibleWinner && widthAllowed);

                    if (partitionType == Av1PartitionType.Horizontal4 && allowExtendedStage &&
                        blockSize is >= Av1BlockSize.Block16x16 and <= Av1BlockSize.Block64x64)
                    {
                        fourStripMask = 3;
                        if (partitionSettings.ExtendedPartitionPruningLevel == 2)
                        {
                            bool horizontalWinner = selectedPartition is Av1PartitionType.Horizontal or
                                Av1PartitionType.HorizontalA or Av1PartitionType.HorizontalB or Av1PartitionType.Split or Av1PartitionType.None;

                            bool verticalWinner = selectedPartition is Av1PartitionType.Vertical or
                                Av1PartitionType.VerticalA or Av1PartitionType.VerticalB or Av1PartitionType.Split or Av1PartitionType.None;

                            fourStripMask = (horizontalWinner ? 1 : 0) | (verticalWinner ? 2 : 0);
                        }

                        if (this.IsPartitionCandidateAllowed(blockOrigin, blockSize, Av1PartitionType.Horizontal) &&
                            this.IsPartitionCandidateAllowed(blockOrigin, blockSize, Av1PartitionType.Vertical))
                        {
                            if (parentSourceVariance < 0)
                            {
                                parentSourceVariance = this.GetSourceVariance(
                                    sourceLuma, sourceBlue, sourceRed, this.GetCandidateReconstruction(in modeWorkspace, 0), blockOrigin, blockSize);
                            }

                            fourStripMask = this.ClassifyFourStripPartitions(
                                sourceLuma,
                                sourceBlue,
                                sourceRed,
                                this.GetCandidateReconstruction(in modeWorkspace, 0),
                                blockOrigin,
                                blockSize,
                                selectedPartition,
                                bestStatistics.Cost,
                                parentSourceVariance,
                                horizontalCosts,
                                verticalCosts,
                                splitNoneCosts,
                                fourStripMask);
                        }

                        if (partitionSettings.ChildPartitionPruningLevel != 0)
                        {
                            fourStripMask = PruneFourStripPartitions(this.state.BlockQIndex, childRectangleWins, fourStripMask);
                        }

                        bool boosted = this.state.Picture.Parent.FrameUpdateType is Av1FrameUpdateType.Key or
                            Av1FrameUpdateType.Golden or Av1FrameUpdateType.Alternate;

                        // The strip model runs only when the frame keeps the simple motion reference.
                        if (fourStripMask == 3 && !frameHeader.IsIntra && bestStatistics.Cost != long.MaxValue &&
                            (this.state.Picture.Parent.AvailableReferenceMask & (1 << (int)this.state.SimpleMotionReference)) != 0 &&
                            partitionSettings.Speed >= HeifEncodingSpeed.Level1 && partitionSettings.Speed <= HeifEncodingSpeed.Level6 &&
                            (!boosted || partitionSettings.Speed >= HeifEncodingSpeed.Level3) &&
                            blockOrigin.X + blockSize.GetWidth() <=
                                (this.state.Picture.Parent.Common.ModeInfoColumnCount << Av1Constants.ModeInfoSizeLog2) &&
                            blockOrigin.Y + blockSize.GetHeight() <=
                                (this.state.Picture.Parent.Common.ModeInfoRowCount << Av1Constants.ModeInfoSizeLog2))
                        {
                            Av1MotionVector start = simpleMotionData[nodeIndex].Starts[(int)Av1ReferenceFrameType.Last];
                            InlineArray2<long> directionCostStorage = default;
                            Span<long> directionCosts = directionCostStorage;
                            for (int direction = 0; direction < 2; direction++)
                            {
                                Av1PartitionType stripPartition = direction == 0 ? Av1PartitionType.Horizontal4 : Av1PartitionType.Vertical4;
                                long squaredError = 0;
                                for (int strip = 0; strip < 4; strip++)
                                {
                                    Point origin = stripPartition.GetChildOrigin(blockOrigin, blockSize, strip);
                                    Av1BlockSize size = stripPartition.GetChildBlockSize(blockSize, strip);
                                    this.SearchSimpleMotion(
                                        workspaceStorage,
                                        sourceLuma,
                                        sourceBlue,
                                        sourceRed,
                                        reconstructionLuma,
                                        reconstructionBlue,
                                        reconstructionRed,
                                        motionSearchPrediction,
                                        interWorkspace.FilterRows,
                                        in motionVectorCosts,
                                        origin,
                                        size,
                                        start,
                                        true,
                                        out int error,
                                        out _);

                                    squaredError += error;
                                }

                                int rate = Av1TileWriter.GetPartitionCost(
                                    this.state.Picture, writer, tables.ModeCosts, blockSize, stripPartition, blockOrigin, in partitionEdges);

                                directionCosts[direction] = new Av1RateDistortionStatistics(this.state.RateMultiplier, rate, squaredError).Cost;
                            }

                            // Compare complete strips with the same starting vector and partition-symbol cost.
                            // Equal costs retain both directions.
                            if (directionCosts[0] > directionCosts[1])
                            {
                                fourStripMask &= 2;
                            }
                            else if (directionCosts[1] > directionCosts[0])
                            {
                                fourStripMask &= 1;
                            }
                        }
                    }
                }

                if ((partitionType == Av1PartitionType.Horizontal4 && (fourStripMask & 1) == 0) ||
                    (partitionType == Av1PartitionType.Vertical4 && (fourStripMask & 2) == 0))
                {
                    continue;
                }

                if (partitionType >= Av1PartitionType.HorizontalA && !allowExtendedStage)
                {
                    continue;
                }

                // The asymmetric stage measures the variance of the complete block. The source border
                // covers a block only while its midpoints are inside the frame, which is also the only
                // geometry where an asymmetric candidate is legal.
                if (partitionType == Av1PartitionType.HorizontalA && midpointsInsideFrame)
                {
                    if (parentSourceVariance < 0)
                    {
                        parentSourceVariance = this.GetSourceVariance(
                            sourceLuma, sourceBlue, sourceRed, this.GetCandidateReconstruction(in modeWorkspace, 0), blockOrigin, blockSize);
                    }

                    bool rectanglesAllowed =
                        this.IsPartitionCandidateAllowed(blockOrigin, blockSize, Av1PartitionType.Horizontal) &&
                        this.IsPartitionCandidateAllowed(blockOrigin, blockSize, Av1PartitionType.Vertical);

                    asymmetricMask = GetAsymmetricPartitionMask(
                        partitionSettings,
                        blockSize,
                        selectedPartition,
                        bestStatistics.Cost,
                        parentSourceVariance,
                        this.state.InterSourceVariance,
                        this.state.BlockQIndex,
                        nodeIndex,
                        rectangleWins,
                        this.state.Superblock.Workspace.PartitionSearchTypes,
                        splitNoneCosts,
                        horizontalCosts,
                        verticalCosts,
                        rectanglesAllowed);
}

                if (partitionType is >= Av1PartitionType.HorizontalA and <= Av1PartitionType.VerticalB &&
                    (asymmetricMask & (1 << ((int)partitionType - (int)Av1PartitionType.HorizontalA))) == 0)
                {
                    continue;
                }

                if ((partitionType == Av1PartitionType.Horizontal && pruneHorizontalRectangle) ||
                    (partitionType == Av1PartitionType.Vertical && pruneVerticalRectangle))
                {
                    continue;
                }

                if (!this.IsPartitionCandidateAllowed(blockOrigin, blockSize, partitionType))
                {
                    continue;
                }

                if (partitionType is >= Av1PartitionType.HorizontalA and <= Av1PartitionType.VerticalB)
                {
                    ReuseAsymmetricPartitionContexts(
                        this.state.BlockWorkspace.PartitionTree,
                        this.state.Superblock.Workspace.PartitionSearchTypes,
                        nodeIndex,
                        partitionType);
                }

                Span<long> childCosts = partitionType switch
                {
                    Av1PartitionType.Split => splitNoneCosts,
                    Av1PartitionType.Horizontal => horizontalCosts,
                    Av1PartitionType.Vertical => verticalCosts,
                    _ => []
                };

                // An asymmetric sub-block keeps the luma mode of the candidate that already covered the same samples.
                this.state.AsymmetricModeCache = default;
                if (cacheAsymmetricModes && partitionType is >= Av1PartitionType.HorizontalA and <= Av1PartitionType.VerticalB)
                {
                    SetAsymmetricModeCache(
                        ref this.state.AsymmetricModeCache, partitionType, splitModeCache, horizontalModeCache, verticalModeCache);
                }

                // A block above the maximum partition size keeps what its split children leave. The contexts are not restored after the
                // split, so the dry run of the last child leaves its contexts, as the dry run of every earlier child does.
                bool keepsSplitContexts = blockSize > this.state.MaximumPartitionSize &&
                    blockSize != this.state.Picture.Sequence.SequenceHeader.SuperblockSize;

                Av1RateDistortionStatistics candidateStatistics = this.EvaluatePartitionCandidate(
                    writer,
                    in tables,
                    in modeWorkspace,
                    transformCoefficients,
                    dequantizedCoefficients,
                    searchDequantizedCoefficients,
                    transformWorkspace,
                    transformTypeProbabilities,
                    blockResidual,
                    searchCoefficients,
                    searchReconstructions,
                    estimationRowCoefficients,
                    in interWorkspace,
                    motionSearchPrediction,
                    in motionVectorCosts,
                    transformPrediction,
                    interIntraAbove,
                    interIntraLeft,
                    firstIntermediate,
                    secondIntermediate,
                    compoundMask,
                    in partitionEdges,
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
                    searchSegmentMap,
                    previousSegmentMap,
                    superblockCoefficients,
                    workspaceStorage,
                    sourceLuma,
                    sourceBlue,
                    sourceRed,
                    reconstructionLuma,
                    reconstructionBlue,
                    reconstructionRed,
                    macroBlock,
                    blockOrigin,
                    blockSize,
                    partitionType,
                    nodeIndex,
                    bestStatistics,
                    searchChildren: true,
                    publishFinalContexts: keepsSplitContexts && partitionType == Av1PartitionType.Split,
                    childCosts,
                    partitionType == Av1PartitionType.Split ? childRectangleWins : [],
                    out int stoppedAtLeaf,
                    out long accumulatedCost);

                this.state.AsymmetricModeCache = default;
                if (cacheAsymmetricModes)
                {
                    this.CaptureAsymmetricModeCacheSources(
                        nodeIndex,
                        partitionType,
                        stoppedAtLeaf,
                        childCosts,
                        ref splitModeCacheStorage,
                        ref horizontalModeCacheStorage,
                        ref verticalModeCacheStorage);
                }

                if (candidateStatistics.Cost >= bestStatistics.Cost)
                {
                    if (partitionType == Av1PartitionType.Horizontal)
                    {
                        rectangleWins &= 2;
                    }
                    else if (partitionType == Av1PartitionType.Vertical)
                    {
                        rectangleWins &= 1;
                    }
                }

                bool terminateAfterNone = false;
                if (partitionType == Av1PartitionType.None)
                {
                    noneInvalid = stoppedAtLeaf == 0 || accumulatedCost == long.MaxValue;

                    // A failed unsplit search leaves no cost.
                    nonePartitionCost = noneInvalid ? long.MaxValue : accumulatedCost;

                    // The unsplit cost is priced again from the rate and distortion of the block at the multiplier of the node. This drops
                    // the cost bias of the image tune.
                    Av1RateDistortionStatistics noneStatistics =
                        this.state.BlockWorkspace.PartitionTree.GetContext(nodeIndex, Av1PartitionType.None, 0).Snapshot.Statistics;

                    noneStatistics.UpdateCost(this.state.RateMultiplier);
                    noneCost = noneInvalid ? long.MaxValue : noneStatistics.Cost;

                    if (!noneInvalid && !this.state.Picture.Parent.FrameHeader.IsIntra &&
                        this.state.Picture.Parent.SpeedSettings.GetRectangularPartitionReferencePruning(this.state.Picture.Parent.FrameUpdateType) != 0)
                    {
                        this.UpdatePickedReferenceFrames(
                            blockOrigin,
                            blockSize,
                            this.state.BlockWorkspace.PartitionTree.GetContext(nodeIndex, Av1PartitionType.None, 0).Snapshot.ModeInfo.Block);
                    }

                    // The unsplit search leaves the block variance that it measured, whether or not it found a mode. A search that returned
                    // on a negative bound leaves the variance of the block searched before it. Before the first search of the superblock, it
                    // leaves no variance.
                    parentSourceVariance = this.state.InterSourceVariance;

                    if (candidateStatistics.Cost < bestStatistics.Cost && !this.state.Picture.Parent.FrameHeader.IsIntra &&
                        !this.BlockLossless && (allowMotionSplit || allowRectangularSplit) &&
                        IsSkippable(this.state.BlockWorkspace.PartitionTree.GetContext(nodeIndex, Av1PartitionType.None, 0).Snapshot))
                    {
                        // The distortion threshold scales by the block area relative to a maximum superblock. The rate threshold uses the
                        // logarithmic sample count. Both tests must pass before smaller partitions stop.
                        int sampleCountLog2 = BitOperations.Log2((uint)GetBlockArea(blockSize));
                        Av1EncoderSpeedSettings settings = this.state.Picture.Parent.SpeedSettings;
                        long distortionThreshold = settings.PartitionBreakoutDistortionThreshold >>
                            ((2 * Av1Constants.MaxSuperBlockSizeLog2) - sampleCountLog2);

                        int rateThreshold = settings.PartitionBreakoutRateThreshold * sampleCountLog2;

                        // The breakout clears the square and rectangular searches, but it does not end the search. A rectangle on an active
                        // image edge and the pruning after the split stage still run.
                        if (this.ShouldStopPartitionSearch(blockSize, candidateStatistics) ||
                            (candidateStatistics.Distortion < distortionThreshold && candidateStatistics.Rate < rateThreshold))
                        {
                            allowMotionSplit = false;
                            allowRectangularSplit = false;
                        }
                    }

                    if (!noneInvalid && this.state.Picture.Parent.SpeedSettings.PruneRectangularPartitionsUsingIntraMode)
                    {
                        Av1PredictionMode noneMode = this.state.BlockWorkspace.PartitionTree
                            .GetContext(nodeIndex, Av1PartitionType.None, 0)
                            .Snapshot.ModeInfo.Block.Mode;

                        (pruneHorizontalRectangle, pruneVerticalRectangle) = GetRectangularPartitionPruning(
                            noneMode,
                            blockSize,
                            macroBlock,
                            modeInfoGrid,
                            modeInfoAllocation);
                    }
                }

                // The model also runs in the retry that must find a partition.
                if (partitionType == Av1PartitionType.None && candidateStatistics.Cost < bestStatistics.Cost &&
                    !terminateAfterNone &&
                    motionTerminateNone && frameHeader.ShowFrame && !frameHeader.IsIntra && blockSize >= Av1BlockSize.Block16x16 &&
                    blockOrigin.X + halfWidth < (this.state.Picture.Parent.Common.ModeInfoColumnCount << Av1Constants.ModeInfoSizeLog2) &&
                    blockOrigin.Y + halfHeight < (this.state.Picture.Parent.Common.ModeInfoRowCount << Av1Constants.ModeInfoSizeLog2) &&
                    candidateStatistics.Cost is >= 0 and < long.MaxValue && candidateStatistics.Rate is >= 0 and < int.MaxValue &&
                    (allowMotionSplit || allowRectangularSplit))
                {
                    terminateAfterNone = this.ShouldTerminateAfterMotionNone(
                        motionSearchPrediction,
                        interWorkspace.FilterRows,
                        in motionVectorCosts,
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
                        blockOrigin,
                        blockSize,
                        nodeIndex,
                        candidateStatistics);
                }

                if (partitionType == Av1PartitionType.Split &&
                    candidateStatistics.Cost >= bestStatistics.Cost &&
                    (this.state.Picture.Parent.SpeedSettings.RectangularPartitionPruningLevel == 2 || stoppedAtLeaf <= 2) &&
                    noneCost > 0 &&
                    noneCost < accumulatedCost)
                {
                    allowRectangularSplit = false;
                }

                if (partitionType == Av1PartitionType.Split)
                {
                    splitInvalid = accumulatedCost == long.MaxValue;
                }

                bool terminateAfterSplit = !this.state.MustFindValidPartition && partitionType == Av1PartitionType.Split &&
                    ShouldTerminatePartitionSearchAfterNoneAndSplit(
                        this.state.Picture.Parent.SpeedSettings.TerminatePartitionSearchAfterInvalidNoneAndSplit,
                        blockSize,
                        this.state.Picture.Sequence.SequenceHeader.SuperblockSize,
                        noneInvalid,
                        accumulatedCost == long.MaxValue);

                if (candidateStatistics.Cost < bestStatistics.Cost)
                {
                    bestStatistics = candidateStatistics;
                    selectedStatistics = candidateStatistics;
                    selectedPartition = partitionType;
                }

                if (partitionType == Av1PartitionType.Split)
                {
                    terminateAfterSplit = this.PrunePartitionsAfterSplit(
                        workspaceStorage,
                        sourceLuma,
                        sourceBlue,
                        sourceRed,
                        reconstructionLuma,
                        reconstructionBlue,
                        reconstructionRed,
                        in modeWorkspace,
                        motionSearchPrediction,
                        interWorkspace.FilterRows,
                        in motionVectorCosts,
                        blockOrigin,
                        blockSize,
                        nodeIndex,
                        noneInvalid,
                        bestStatistics.Cost,
                        nonePartitionCost,
                        noneCost,
                        accumulatedCost,
                        splitNoneCosts,
                        terminateAfterSplit,
                        ref allowRectangularSplit,
                        ref pruneExtendedPartitions,
                        ref pruneHorizontalRectangle,
                        ref pruneVerticalRectangle,
                        ref parentSourceVariance);
                }

                // A block above the maximum partition size never reconstructs its winning subtree again, so the split children keep the
                // contexts and coefficients that they produced.
                if (partitionType != Av1PartitionType.Split || !keepsSplitContexts)
                {
                    this.ResetPartitionTrial(
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

                if (terminateAfterNone || terminateAfterSplit)
                {
                    break;
                }
            }

            if (blockSize == this.state.Picture.Sequence.SequenceHeader.SuperblockSize && selectedStatistics.Cost == long.MaxValue)
            {
                // A superblock must produce a legal partition. Retry with optional shape restrictions
                // removed, while retaining frame-edge syntax and the configured minimum/maximum sizes.
                this.state.MustFindValidPartition = true;
                retrying = true;
                retainedRectangularSplit = allowRectangularSplit;
                retainedPruneHorizontal = pruneHorizontalRectangle;
                retainedPruneVertical = pruneVerticalRectangle;
                goto SearchPartitions;
            }

            this.state.Superblock.Workspace.PartitionSearchTypes[nodeIndex] = selectedStatistics.Cost == long.MaxValue
                ? (byte)Av1PartitionType.Invalid
                : (byte)selectedPartition;

            if (!this.state.Picture.Parent.FrameHeader.IsIntra)
            {
                this.state.BlockWorkspace.GetSimpleMotionData(workspaceStorage)[nodeIndex].Partition = selectedPartition;
            }

            return selectedPartition;
        }

        /// <summary>
        /// Selects the inter, intra and intra block copy modes of one block, and encodes the winner when asked.
        /// </summary>
        /// <param name="writer">The symbol encoder that prices the syntax.</param>
        /// <param name="tables">The rate tables of the tile.</param>
        /// <param name="modeWorkspace">The mode decision buffers of the block.</param>
        /// <param name="transformCoefficients">The forward transform output buffer.</param>
        /// <param name="dequantizedCoefficients">The dequantized coefficient buffer of the candidate.</param>
        /// <param name="searchDequantizedCoefficients">The dequantized coefficient buffer of the winner.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
        /// <param name="transformTypeProbabilities">The transform type probabilities of every update type and size.</param>
        /// <param name="blockResidual">The residual buffer of the motion search.</param>
        /// <param name="searchCoefficients">The quantized coefficient buffer of the winner.</param>
        /// <param name="searchReconstructions">The storage of the candidate and winner reconstructions of the type search.</param>
        /// <param name="estimationRowCoefficients">The coefficients of one row of estimation transforms.</param>
        /// <param name="interWorkspace">The inter prediction buffers of the block.</param>
        /// <param name="motionSearchPrediction">The prediction buffer of the motion search.</param>
        /// <param name="motionVectorCosts">The motion vector rates of the frame precision.</param>
        /// <param name="transformPrediction">The prediction storage of one transform block.</param>
        /// <param name="interIntraAbove">The extended above edge of an inter-intra prediction.</param>
        /// <param name="interIntraLeft">The extended left edge of an inter-intra prediction.</param>
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
        /// <param name="macroBlock">The neighbor availability of the block.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="modeInfo">The block decisions.</param>
        /// <param name="block">The block state.</param>
        /// <param name="paletteInfo">The palette of the block.</param>
        /// <param name="encodeSelected">Whether the winner is encoded after the search.</param>
        public void EncodeBlockCore(
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
            Span<int> estimationRowCoefficients,
            in Av1EncoderInterPredictionWorkspace<byte> interWorkspace,
            Span<byte> motionSearchPrediction,
            in Av1MotionVectorCosts motionVectorCosts,
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
            ReadOnlySpan<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            Span<Av1EncoderDisplacementVector> displacementVectors,
            Span<Av1EncoderReferenceContext> referenceContexts,
            Span<byte> encoderSegmentMap,
            Span<byte> searchSegmentMap,
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
            ref Av1MacroBlockModeInfo modeInfo,
            ref Av1EncoderBlockStruct block,
            ref Av1EncoderPaletteInfo paletteInfo,
            bool encodeSelected)
        {
            this.state.BlockWorkspace.EvaluationStage = Av1EncoderEvaluationStage.Candidate;
            if (this.state.ReplayNodeIndex >= 0)
            {
                int halfSize = this.state.ReplayParentSize.GetWidth() >> 1;
                int localX = blockOrigin.X - this.state.ReplayPartitionOrigin.X;
                int localY = blockOrigin.Y - this.state.ReplayPartitionOrigin.Y;
                int leafIndex = this.state.ReplayPartition switch
                {
                    Av1PartitionType.Horizontal => localY / halfSize,
                    Av1PartitionType.Vertical => localX / halfSize,
                    Av1PartitionType.Split => (localX / halfSize) + (2 * (localY / halfSize)),
                    Av1PartitionType.HorizontalA => localY < halfSize ? localX / halfSize : 2,
                    Av1PartitionType.HorizontalB => localY < halfSize ? 0 : 1 + (localX / halfSize),
                    Av1PartitionType.VerticalA => localX < halfSize ? localY / halfSize : 2,
                    Av1PartitionType.VerticalB => localX < halfSize ? 0 : 1 + (localY / halfSize),
                    Av1PartitionType.Horizontal4 => localY / (halfSize >> 1),
                    Av1PartitionType.Vertical4 => localX / (halfSize >> 1),
                    _ => 0
                };

                // The writer reaches each terminal leaf in partition order. Restore its selected syntax
                // and rebuild the residual against the preceding leaves, without running mode search again.
                Av1EncoderPartitionTree.ModeContext context =
                    this.state.BlockWorkspace.PartitionTree.GetContext(this.state.ReplayNodeIndex, this.state.ReplayPartition, leafIndex);

                modeInfo = context.Snapshot.ModeInfo;
                block = context.Snapshot.Block;
                paletteInfo = context.Snapshot.Palette;

                // Complexity adaptive quantization takes the coded segment from the segment map, before the segment costs are added.
                int codedSegmentId = this.GetCodedBlockSegment(
                    encoderSegmentMap, previousSegmentMap, blockOrigin, modeInfo.Block.BlockSize, modeInfo.Block.SegmentId);

                modeInfo.Block.SegmentId = codedSegmentId;
                block.SegmentId = codedSegmentId;

                // A block that a merge trial of the estimated inter search kept updates its cyclic refresh segment when it is coded for
                // output. It always prices its segment, because the estimated search never sets the skip flag that the state update tests.
                bool countSegments = false;
                bool cyclicRefreshEncode = encodeSelected && this.UsesEstimatedInterSearch &&
                    this.UpdatesCyclicRefreshSegment(context.Snapshot.Statistics, out countSegments);

                if (cyclicRefreshEncode)
                {
                    Av1MotionVector firstVector = modeInfo.Block.ReferenceFrame > Av1ReferenceFrameType.Intra
                        ? context.Snapshot.Displacement
                        : default;

                    this.UpdateCyclicRefreshSegment(
                        encoderSegmentMap,
                        searchSegmentMap,
                        blockOrigin,
                        ref modeInfo.Block,
                        ref block,
                        firstVector,
                        context.Snapshot.Statistics,
                        countSegments);

                    // The intra reconstruction reads its segment from the retained decision.
                    context.Snapshot.ModeInfo.Block.SegmentId = modeInfo.Block.SegmentId;
                    context.Snapshot.Block.SegmentId = block.SegmentId;
                }

                bool searchSkip = !cyclicRefreshEncode && modeInfo.Block.Skip;
                if (encodeSelected && !searchSkip)
                {
                    this.AddSegmentPredictionCosts(
                        in tables, searchSegmentMap, previousSegmentMap, macroBlock, blockOrigin, modeInfo.Block.BlockSize, modeInfo.Block.SegmentId);
                }

                if (modeInfo.Block.ReferenceFrame > Av1ReferenceFrameType.Intra || modeInfo.Block.UseIntraBlockCopy)
                {
                    InlineArray128<Av1EncoderTransformBlockState> stateStorage = default;
                    Span<Av1EncoderTransformBlockState> states = stateStorage;

                    // A lossless segment codes 4x4 transforms with one retained state per transform.
                    bool replayLossless = this.state.Picture.Parent.FrameHeader.LosslessArray[modeInfo.Block.SegmentId];
                    Av1TransformSize replayRootSize = replayLossless
                        ? Av1TransformSize.Size4x4
                        : modeInfo.Block.BlockSize.GetMaximumTransformSize();

                    Av1TransformSize replayTraversalSize = replayRootSize.GetSubSize().GetSubSize();
                    Size replayExtent = GetCodedTransformExtent(
                        macroBlock, modeInfo.Block.BlockSize, Av1TransformSize.Size4x4, 0, 0);

                    ReadOnlySpan<Av1EncoderTransformBlockState> replayLumaStates = context.GetTransformStates(Av1Plane.Y);
                    int replayArea = 0;
                    int replayStateCount = 0;
                    int replayLeafCount = modeInfo.Block.BlockSize.GetWidth() * modeInfo.Block.BlockSize.GetHeight() / replayTraversalSize.GetSize2d();
                    for (int leaf = 0; leaf < replayLeafCount; leaf++)
                    {
                        Point offset = replayRootSize.GetBlockPartitionOrigin(modeInfo.Block.BlockSize, replayTraversalSize, leaf, 0, 0);
                        Av1TransformSize size = modeInfo.Block.InterTransformSizes[
                            modeInfo.Block.GetInterTransformSizeIndex(offset.Y >> 2, offset.X >> 2)];

                        if (offset.X >= replayExtent.Width || offset.Y >= replayExtent.Height ||
                            (offset.X % size.GetWidth()) != 0 || (offset.Y % size.GetHeight()) != 0)
                        {
                            continue;
                        }

                        if (!replayLossless)
                        {
                            states[replayStateCount++] = replayLumaStates[
                                replayArea / Av1EncoderCoefficientBuffer.TransformBlockUnitCoefficientCount];
                        }

                        replayArea += size.GetSize2d();
                    }

                    if (block.HasChroma)
                    {
                        int subX = this.state.ChromaSubsamplingX;
                        int subY = this.state.ChromaSubsamplingY;
                        Av1TransformSize chromaTransformSize = replayLossless
                            ? Av1TransformSize.Size4x4
                            : modeInfo.Block.BlockSize.GetMaxUvTransformSize(subX != 0, subY != 0);

                        Av1BlockSize chromaBlockSize = modeInfo.Block.BlockSize.GetSubsampled(subX != 0, subY != 0);
                        Size chromaExtent = GetCodedTransformExtent(macroBlock, chromaBlockSize, chromaTransformSize, subX, subY);
                        int chromaTransformCount = chromaExtent.Width * chromaExtent.Height / chromaTransformSize.GetSize2d();
                        int chromaStateStride = chromaTransformSize.GetSize2d() / Av1EncoderCoefficientBuffer.TransformBlockUnitCoefficientCount;
                        ReadOnlySpan<Av1EncoderTransformBlockState> blueStates = context.GetTransformStates(Av1Plane.U);
                        ReadOnlySpan<Av1EncoderTransformBlockState> redStates = context.GetTransformStates(Av1Plane.V);
                        for (int index = 0; !replayLossless && index < chromaTransformCount; index++)
                        {
                            states[64 + index] = blueStates[index * chromaStateStride];
                            states[80 + index] = redStates[index * chromaStateStride];
                        }
                    }

                    this.state.EncodedWithoutCoefficients = !this.ReconstructSelectedInterBlock(
                        writer,
                        in tables,
                        transformCoefficients,
                        dequantizedCoefficients,
                        transformWorkspace,
                        in interWorkspace,
                        transformPrediction,
                        interIntraAbove,
                        interIntraLeft,
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
                        sourceLuma,
                        sourceBlue,
                        sourceRed,
                        reconstructionLuma,
                        reconstructionBlue,
                        reconstructionRed,
                        macroBlock,
                        blockOrigin,
                        modeInfo,
                        block,
                        context.Snapshot.Displacement,
                        context.Snapshot.SecondaryDisplacement,
                        states);

                    Point replayModeInfoPosition = new(
                        blockOrigin.X >> Av1Constants.ModeInfoSizeLog2,
                        blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2);

                    this.state.Picture.SetDisplacementVector(displacementVectors, replayModeInfoPosition, context.Snapshot.Displacement);
                    if (modeInfo.Block.SecondaryReferenceFrame > Av1ReferenceFrameType.Intra)
                    {
                        this.state.Picture.SetSecondaryDisplacementVector(referenceContexts, replayModeInfoPosition, context.Snapshot.SecondaryDisplacement);
                    }

                    this.state.CodedAreaLuma += replayArea;
                    if (block.HasChroma)
                    {
                        int subX = this.state.ChromaSubsamplingX;
                        int subY = this.state.ChromaSubsamplingY;
                        Av1BlockSize chromaBlockSize = modeInfo.Block.BlockSize.GetSubsampled(subX != 0, subY != 0);
                        Av1TransformSize chromaTransformSize = replayLossless
                            ? Av1TransformSize.Size4x4
                            : modeInfo.Block.BlockSize.GetMaxUvTransformSize(subX != 0, subY != 0);

                        Size chromaExtent = GetCodedTransformExtent(
                            macroBlock,
                            chromaBlockSize,
                            chromaTransformSize,
                            subX,
                            subY);

                        this.state.CodedAreaChroma += chromaExtent.Width * chromaExtent.Height;
                    }
                }
                else
                {
                    this.ReconstructSelectedIntraBlock(
                        writer,
                        in tables,
                        in modeWorkspace,
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
                        sourceLuma,
                        sourceBlue,
                        sourceRed,
                        reconstructionLuma,
                        reconstructionBlue,
                        reconstructionRed,
                        macroBlock,
                        blockOrigin,
                        context);
                }

                if (encodeSelected && this.UsesEstimatedInterSearch)
                {
                    this.CountNoiseStillBlock(
                        blockOrigin,
                        modeInfo.Block,
                        modeInfo.Block.ReferenceFrame > Av1ReferenceFrameType.Intra ? context.Snapshot.Displacement : default);
                }

                if (cyclicRefreshEncode)
                {
                    this.ResetCyclicRefreshSkip(
                        encoderSegmentMap,
                        searchSegmentMap,
                        macroBlock,
                        blockOrigin,
                        ref modeInfo.Block,
                        ref block,
                        modeInfo.Block.Skip || this.state.EncodedWithoutCoefficients,
                        countSegments);
                }

                this.state.SelectedBlockStatistics = context.Snapshot.Statistics;
                return;
            }

            this.state.InterTransformNoSplitCosts[..].Fill(long.MaxValue);
            Av1BlockSize blockSize = modeInfo.Block.BlockSize;
            this.state.InterSourceVariance = this.GetSourceVariance(
                sourceLuma, sourceBlue, sourceRed, this.GetCandidateReconstruction(in modeWorkspace, 0), blockOrigin, blockSize);

            Av1PartitionType partitionType = modeInfo.Block.PartitionType;
            Av1TransformSize maximumLumaTransformSize = this.BlockLossless
                ? Av1TransformSize.Size4x4
                : blockSize.GetMaximumTransformSize();

            int qIndex = this.state.BlockQIndex;
            modeInfo.Block = new Av1EncoderBlockModeInfo
            {
                BlockSize = blockSize,
                PartitionType = partitionType,
                SegmentId = this.state.BlockSegmentId,
                TransformSize = maximumLumaTransformSize,
                Mode = Av1PredictionMode.DC,
                UvMode = Av1ChromaPredictionMode.DC
            };

            modeInfo.CdefStrength = 0;
            Point modeInfoPosition = new(blockOrigin.X >> Av1Constants.ModeInfoSizeLog2, blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2);
            block.HasChroma = !this.state.IsMonochrome &&
                Av1TileReader.HasChroma(this.state.Picture.Sequence.SequenceHeader, modeInfoPosition, blockSize);

            // The delta quantizer syntax codes the superblock quantizer, not the quantizer of the segment.
            block.QuantizationIndex = this.state.SuperblockQIndex;
            block.SegmentId = this.state.BlockSegmentId;

            if (this.state.Picture.Parent.FrameHeader.IsIntra && this.state.Picture.Parent.SpeedSettings.UseEstimatedModeDecision)
            {
                // A nonzero hybrid level sends a block below 16x16 to the full intra search when its source variance
                // reaches the threshold of the level. Levels 1, 2 and 3 use the thresholds 0, 101 and 201.
                int sourceVariance = this.state.InterSourceVariance;
                int hybridLevel = this.state.Picture.Parent.SpeedSettings.HybridIntraSearchLevel;
                bool useFullSearch = blockSize < Av1BlockSize.Block16x16 &&
                    hybridLevel != 0 &&
                    sourceVariance >= hybridLevel switch { 1 => 0, 2 => 101, _ => 201 };

                if (!useFullSearch)
                {
                    this.EncodeEstimatedIntraBlock(
                        writer,
                        in tables,
                        in modeWorkspace,
                        transformCoefficients,
                        dequantizedCoefficients,
                        searchDequantizedCoefficients,
                        transformWorkspace,
                        transformTypeProbabilities,
                        estimationRowCoefficients,
                        in transformEdges,
                        in paletteEdges,
                        in lumaCoefficientEdges,
                        in blueCoefficientEdges,
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
                        blockOrigin,
                        blockSize,
                        sourceVariance,
                        ref modeInfo,
                        ref block,
                        ref paletteInfo);

                    return;
                }
            }

            bool isInterFrame = !this.state.Picture.Parent.FrameHeader.IsIntra;
            if (isInterFrame && this.state.Picture.Parent.SpeedSettings.UseEstimatedInterModeDecision)
            {
                this.EncodeEstimatedInterBlock(
                    writer,
                    in tables,
                    in modeWorkspace,
                    transformCoefficients,
                    dequantizedCoefficients,
                    searchDequantizedCoefficients,
                    transformWorkspace,
                    transformTypeProbabilities,
                    blockResidual,
                    estimationRowCoefficients,
                    in interWorkspace,
                    motionSearchPrediction,
                    in motionVectorCosts,
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
                    searchSegmentMap,
                    previousSegmentMap,
                    superblockCoefficients,
                    workspaceStorage,
                    sourceLuma,
                    sourceBlue,
                    sourceRed,
                    reconstructionLuma,
                    reconstructionBlue,
                    reconstructionRed,
                    macroBlock,
                    blockOrigin,
                    ref modeInfo,
                    ref block,
                    ref paletteInfo);

                return;
            }

            Av1RateDistortionStatistics interStatistics = Av1RateDistortionStatistics.Invalid;
            Av1MacroBlockModeInfo interModeInfo = default;
            Av1EncoderBlockStruct interBlock = default;
            InlineArray128<Av1EncoderTransformBlockState> interStates = default;
            Av1MotionVector interVector = default;
            Av1MotionVector interSecondaryVector = default;
            if (isInterFrame)
            {
                Av1MacroBlockModeInfo initialModeInfo = modeInfo;
                Av1EncoderBlockStruct initialBlock = block;
                interStatistics = this.SelectInterBlock(
                    writer,
                    in tables,
                    transformCoefficients,
                    dequantizedCoefficients,
                    searchDequantizedCoefficients,
                    transformWorkspace,
                    transformTypeProbabilities,
                    blockResidual,
                    in interWorkspace,
                    motionSearchPrediction,
                    in motionVectorCosts,
                    transformPrediction,
                    interIntraAbove,
                    interIntraLeft,
                    firstIntermediate,
                    secondIntermediate,
                    compoundMask,
                    in transformEdges,
                    in lumaCoefficientEdges,
                    in blueCoefficientEdges,
                    in redCoefficientEdges,
                    modeInfoGrid,
                    modeInfoAllocation,
                    displacementVectors,
                    referenceContexts,
                    workspaceStorage,
                    sourceLuma,
                    sourceBlue,
                    sourceRed,
                    reconstructionLuma,
                    reconstructionBlue,
                    reconstructionRed,
                    macroBlock,
                    blockOrigin,
                    ref modeInfo,
                    ref block,
                    out interVector,
                    out interSecondaryVector,
                    out interStates);

                interModeInfo = modeInfo;
                interBlock = block;

                // Only the winning syntax and transform choices survive across mode families. The intra trials use the prediction and
                // coefficient storage again. The selected inter block is reconstructed afterwards.
                modeInfo = initialModeInfo;
                block = initialBlock;
                paletteInfo = default;
            }

            Span<Av1EncoderTransformBlockState> lumaTransformBlocks = this.state.CoefficientBuffer.GetTransformBlockSpan(superblockCoefficients, Av1Plane.Y);

            int lumaTransformIndex = this.state.CodedAreaLuma /
                Av1EncoderCoefficientBuffer.TransformBlockUnitCoefficientCount;

            Span<Av1EncoderTransformBlockState> retainedLumaStates = lumaTransformBlocks[lumaTransformIndex..];
            bool skipIntra = false;
            if (isInterFrame)
            {
                Av1EncoderSpeedSettings settings = this.state.Picture.Parent.SpeedSettings;

                // A block above the intra size limit skips the intra search when its best single-reference motion is small and its source is
                // not flat. Without a valid inter mode, the best mode is zero.
                bool validInter = interStatistics.Cost != long.MaxValue;
                if (settings.IntraModeMotionRangePruneLevel != 0 && blockSize > settings.MaximumIntraBlockSize &&
                    !(validInter && interModeInfo.Block.SecondaryReferenceFrame > Av1ReferenceFrameType.Intra))
                {
                    Av1MotionVector bestVector = validInter ? interVector : default;
                    int threshold = 16 << settings.IntraModeMotionRangePruneLevel;
                    skipIntra = Math.Abs(bestVector.Row) < threshold && Math.Abs(bestVector.Column) < threshold &&
                        this.state.InterSourceVariance > 128;
                }

                // The mean model intra and inter costs of the block stay -1 without the statistics of a whole superblock. They also stay -1
                // at speeds 0 and 1 for frames whose shorter side is more than 480 lines.
                long tplInterCost = -1;
                long tplIntraCost = -1;
                ObuFrameSize frameSize = this.state.Picture.Parent.FrameHeader.FrameSize;
                int minimumFrameDimension = Math.Min(frameSize.FrameWidth, frameSize.FrameHeight);
                bool tplPruning = !(minimumFrameDimension > 480 && this.state.Picture.Parent.EncodingSpeed <= HeifEncodingSpeed.Level1);
                if (tplPruning && settings.IntraInInterPruningLevel != 0)
                {
                    Av1TplModePruning.GetCostFromTplData(
                        this.state.BlockWorkspace.TplSuperblockInterCosts,
                        this.state.BlockWorkspace.TplSuperblockIntraCosts,
                        this.state.TplSuperblockBlockCount,
                        this.state.TplSuperblockStride,
                        this.state.Picture.Sequence.SequenceHeader.SuperblockSize.Get4x4WideCount(),
                        blockSize,
                        blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2,
                        blockOrigin.X >> Av1Constants.ModeInfoSizeLog2,
                        out tplInterCost,
                        out tplIntraCost);
                }

                // Without a valid inter mode, the best mode is zero, so it neither skips its transform nor codes a new vector.
                bool bestSkip = validInter && interModeInfo.Block.Skip;
                if (!skipIntra && settings.IntraInInterPruningLevel != 0 && this.state.InterSourceVariance > 1)
                {
                    if (settings.IntraInInterPruningLevel >= 2 && bestSkip)
                    {
                        bool newMotion = interModeInfo.Block.Mode is Av1PredictionMode.NewMotionVector or
                            Av1PredictionMode.NearestNewMotionVector or Av1PredictionMode.NewNearestMotionVector or
                            Av1PredictionMode.NearNewMotionVector or Av1PredictionMode.NewNearMotionVector or Av1PredictionMode.NewNewMotionVector;

                        // Nearly flat sources keep the intra search. Otherwise a skipped residual from inherited motion is sufficient
                        // evidence, but the weaker policy applies only at Q 200 or less. Without model costs, the strongest policies also accept
                        // searched motion.
                        skipIntra = (!newMotion && (settings.IntraInInterPruningLevel >= 3 || qIndex <= 200)) ||
                            (settings.IntraInInterPruningLevel >= 4 && (tplInterCost < 0 || tplIntraCost < 0));
                    }

                    // With both model costs, a small network decides from them, the transform skip of the best mode, the block shape and the
                    // quantizer.
                    if (!skipIntra)
                    {
                        skipIntra = Av1TplModePruning.SkipIntraByNetwork(
                            bestSkip, blockSize, tplIntraCost, tplInterCost, qIndex, this.state.BitDepth, minimumFrameDimension);
                    }
                }

                // A nonzero sharpness skips the intra search of a block wider or taller than 16 samples. High bit depth sharpness 3 is an
                // exception, because it searches all sizes.
                bool largeBlock = blockSize.GetWidth() > 16 || blockSize.GetHeight() > 16;
                if (this.state.BlockWorkspace.EncoderOptions.Sharpness != 0 && !this.UsesHighBitDepthSharpness && largeBlock)
                {
                    skipIntra = true;
                }
            }

            int lumaAngleDelta = 0;
            Av1FilterIntraMode filterIntraMode = Av1FilterIntraMode.AllFilterIntraModes;
            Av1TransformSize lumaTransformSize = maximumLumaTransformSize;
            Av1RateDistortionStatistics lumaStatistics = Av1RateDistortionStatistics.Invalid;
            if (!skipIntra)
            {
                // The intra search clears the leftover skip flag of the inter transform search.
                this.state.TransformSearchSkip = false;
                modeInfo.Block.Mode = this.SelectLumaMode(
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
                    in blueCoefficientEdges,
                    in redCoefficientEdges,
                    modeInfoGrid,
                    modeInfoAllocation,
                    encoderSegmentMap,
                    previousSegmentMap,
                    superblockCoefficients,
                    workspaceStorage,
                    sourceLuma,
                    sourceBlue,
                    sourceRed,
                    reconstructionLuma,
                    reconstructionBlue,
                    reconstructionRed,
                    macroBlock,
                    blockOrigin,
                    blockSize,
                    retainedLumaStates,
                    Math.Min(this.state.BlockCostLimit, interStatistics.Cost),
                    ref paletteInfo,
                    out lumaAngleDelta,
                    out filterIntraMode,
                    out lumaTransformSize,
                    out lumaStatistics);
            }

            // The chroma search is useful only after luma beats the inter luma cost. That cost is the luma cost of the winner, or, after
            // the search of the kept candidates, of the cheapest completed candidate, even above the budget. Empty luma residuals use the
            // skip-symbol estimate for this gate. The final intra syntax stays coded.
            if (isInterFrame && lumaStatistics.LumaCost >= this.state.InterLumaThreshold)
            {
                lumaStatistics = Av1RateDistortionStatistics.Invalid;
            }

            block.PredictionUnit.AngleDelta[(int)Av1PlaneType.Y] = (sbyte)lumaAngleDelta;
            block.FilterIntraMode = filterIntraMode;
            modeInfo.Block.TransformSize = lumaTransformSize;

            int chromaArea = 0;
            Av1RateDistortionStatistics chromaStatistics = new(this.state.RateMultiplier, 0, 0);
            if (block.HasChroma)
            {
                ObuColorConfig colorConfig = this.state.Picture.Sequence.SequenceHeader.ColorConfig;
                int subsamplingX = colorConfig.SubSamplingX ? 1 : 0;
                int subsamplingY = colorConfig.SubSamplingY ? 1 : 0;
                Point chromaOrigin = Av1TileWriter.GetChromaBlockOrigin(
                    blockOrigin,
                    subsamplingX,
                    subsamplingY);

                Av1TransformSize chromaTransformSize = this.BlockLossless
                    ? Av1TransformSize.Size4x4
                    : blockSize.GetMaxUvTransformSize(
                        colorConfig.SubSamplingX,
                        colorConfig.SubSamplingY);

                Av1BlockSize chromaBlockSize = blockSize.GetSubsampled(
                    colorConfig.SubSamplingX,
                    colorConfig.SubSamplingY);

                Size chromaExtent = GetCodedTransformExtent(
                    macroBlock, chromaBlockSize, chromaTransformSize, subsamplingX, subsamplingY);

                chromaArea = chromaExtent.Width * chromaExtent.Height;
                if (lumaStatistics.Cost != long.MaxValue)
                {
                    Span<Av1EncoderTransformBlockState> blueTransformBlocks = this.state.CoefficientBuffer.GetTransformBlockSpan(
                        superblockCoefficients,
                        Av1Plane.U);

                    Span<Av1EncoderTransformBlockState> redTransformBlocks = this.state.CoefficientBuffer.GetTransformBlockSpan(
                        superblockCoefficients,
                        Av1Plane.V);

                    int chromaTransformIndex = this.state.CodedAreaChroma /
                        Av1EncoderCoefficientBuffer.TransformBlockUnitCoefficientCount;

                    Span<Av1EncoderTransformBlockState> retainedBlueStates = blueTransformBlocks[chromaTransformIndex..];
                    Span<Av1EncoderTransformBlockState> retainedRedStates = redTransformBlocks[chromaTransformIndex..];
                    modeInfo.Block.UvMode = this.SelectChromaMode(
                        writer,
                        in tables,
                        in modeWorkspace,
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
                        sourceLuma,
                        sourceBlue,
                        sourceRed,
                        reconstructionLuma,
                        reconstructionBlue,
                        reconstructionRed,
                        macroBlock,
                        modeInfo,
                        blockOrigin,
                        chromaOrigin,
                        blockSize,
                        modeInfo.Block.Mode,
                        chromaTransformSize,
                        retainedBlueStates,
                        retainedRedStates,
                        ref paletteInfo,
                        out int chromaAngleDelta,
                        out byte chromaFromLumaIndex,
                        out sbyte chromaFromLumaSigns,
                        out chromaStatistics);

                    block.PredictionUnit.AngleDelta[(int)Av1PlaneType.Uv] = (sbyte)chromaAngleDelta;
                    block.PredictionUnit.ChromaFromLumaIndex = chromaFromLumaIndex;
                    block.PredictionUnit.ChromaFromLumaSigns = chromaFromLumaSigns;
                    if (chromaStatistics.Cost == long.MaxValue ||
                        (isInterFrame && Av1RateDistortion.GetCost(
                            this.state.RateMultiplier, chromaStatistics.ResidualRate, chromaStatistics.Distortion) > interStatistics.Cost))
                    {
                        lumaStatistics = Av1RateDistortionStatistics.Invalid;
                    }
                    else
                    {
                        lumaStatistics.Add(this.state.RateMultiplier, chromaStatistics);
                    }
                }
            }

            // The estimated mode search never searches a copy, even in the blocks that take the full search, although the header can still
            // allow one. Only a screen-content tune keeps the copy search in real-time usage. This encoder has no such tune, and detected
            // screen content does not select it.
            bool allowIntraBlockCopy = this.state.Picture.Parent.FrameHeader.AllowIntraBlockCopy;
            bool nonRdWithoutCopy = this.state.Picture.Parent.SpeedSettings.UseEstimatedModeDecision;

            bool searchIntraBlockCopy = allowIntraBlockCopy && !nonRdWithoutCopy &&
                this.state.Picture.Parent.MotionSearchSettings.AllowIntraBlockCopy &&
                (!this.state.Picture.Parent.MotionSearchSettings.UseFastIntraBlockCopySearch ||
                 blockSize is Av1BlockSize.Block4x4 or Av1BlockSize.Block8x8 or Av1BlockSize.Block16x16);

            Av1RateDistortionStatistics regularStatistics = lumaStatistics.Cost == long.MaxValue
                ? Av1RateDistortionStatistics.Invalid
                : this.GetRegularBlockCost(in tables, modeInfoGrid, modeInfoAllocation, macroBlock, lumaStatistics);

            // Sharpness 3 charges the merged intra result of an inter frame by the luma samples that its luma search left, before the
            // budget comparison.
            if (regularStatistics.Cost != long.MaxValue && this.ChargesSmoothing)
            {
                regularStatistics.AddSmoothingOffset(this.state.RateMultiplier, this.state.IntraSmoothingOffset);
            }
            else if (regularStatistics.Cost != long.MaxValue && isInterFrame && this.ChargesHighBitDepthTextureLoss)
            {
                // At a high bit depth, the charge also uses the mode of the luma winner, and its extra cost scales the merged cost.
                bool smoothMode = IsSmoothTextureMode(modeInfo.Block.Mode);
                long currentCost = regularStatistics.Cost;
                this.ChargeTextureLoss(
                    sourceLuma,
                    sourceBlue,
                    sourceRed,
                    ref regularStatistics,
                    currentCost,
                    this.state.IntraSmoothingOffset,
                    blockOrigin,
                    blockSize,
                    false,
                    false,
                    smoothMode);
            }

            // An inter frame keeps the intra result only when it beats the budget of the block. Thus a block without a mode below the budget
            // has no winner to refine.
            if (isInterFrame && regularStatistics.Cost >= this.state.BlockCostLimit)
            {
                regularStatistics = Av1RateDistortionStatistics.Invalid;
            }

            if (isInterFrame && interStatistics.Cost != long.MaxValue && interStatistics.Cost <= regularStatistics.Cost)
            {
                // Inter candidates precede intra candidates, so an equal cost retains the inter winner.
                modeInfo = interModeInfo;
                block = interBlock;
                paletteInfo = default;
                Av1EncoderSpeedSettings winnerSettings = this.state.Picture.Parent.SpeedSettings;
                bool hasNewMotion = modeInfo.Block.Mode is Av1PredictionMode.NewMotionVector or
                    Av1PredictionMode.NewNewMotionVector or Av1PredictionMode.NearestNewMotionVector or
                    Av1PredictionMode.NewNearestMotionVector or Av1PredictionMode.NearNewMotionVector or Av1PredictionMode.NewNearMotionVector;

                // The pruning level decides whether the winner skips its refinement, from its skip flag, its all-empty result, its motion
                // and the quantizer.
                bool bypassWinner = winnerSettings.GetInterWinnerPruningLevel(this.state.Picture.Parent.FrameUpdateType) switch
                {
                    2 => !hasNewMotion && interStatistics.AllTransformsEmpty,
                    3 => !hasNewMotion && (interStatistics.AllTransformsEmpty || (this.state.BlockQIndex <= 127 && modeInfo.Block.Skip)),
                    4 => !(winnerSettings.CoefficientOptimizationLevel >= 5 && this.state.BlockQIndex <= 70) &&
                        (modeInfo.Block.Skip || interStatistics.AllTransformsEmpty),
                    _ => false
                };

                // The partition search reads whether the mode-evaluation winner kept any coefficient. The winner refinement does not change
                // this value.
                bool skippable = interStatistics.AllTransformsEmpty;
                if (!bypassWinner && (winnerSettings.EnableWinnerCoefficientOptimization ||
                    winnerSettings.DeferTransformSizeSearch || winnerSettings.UseWinnerInterpolation ||
                    winnerSettings.InterTransformTypeProbabilityThreshold != int.MaxValue))
                {
                    this.state.BlockWorkspace.EvaluationStage = Av1EncoderEvaluationStage.Winner;
                    this.SetWarpedPrediction(macroBlock, blockOrigin, modeInfo.Block.PartitionType, modeInfo.Block, interVector);
                    this.RefineInterTransformSize(
                        writer,
                        in tables,
                        transformCoefficients,
                        dequantizedCoefficients,
                        searchDequantizedCoefficients,
                        transformWorkspace,
                        transformTypeProbabilities,
                        in interWorkspace,
                        transformPrediction,
                        interIntraAbove,
                        interIntraLeft,
                        firstIntermediate,
                        secondIntermediate,
                        compoundMask,
                        in transformEdges,
                        in lumaCoefficientEdges,
                        in blueCoefficientEdges,
                        in redCoefficientEdges,
                        modeInfoGrid,
                        modeInfoAllocation,
                        displacementVectors,
                        workspaceStorage,
                        sourceLuma,
                        sourceBlue,
                        sourceRed,
                        reconstructionLuma,
                        reconstructionBlue,
                        reconstructionRed,
                        macroBlock,
                        blockOrigin,
                        ref modeInfo,
                        block,
                        interVector,
                        interSecondaryVector,
                        ref interStatistics,
                        ref interStates);

                    this.state.UseWarpedPrediction = false;
                    this.state.UseObmcPrediction = false;
                }

                interStatistics.AllTransformsEmpty = skippable;
                this.state.SelectedBlockStatistics = interStatistics;
            }
            else
            {
                this.state.SelectedBlockStatistics = searchIntraBlockCopy
                    ? this.SelectIntraBlockCopy(
                        writer,
                        in tables,
                        transformCoefficients,
                        dequantizedCoefficients,
                        searchDequantizedCoefficients,
                        transformWorkspace,
                        transformTypeProbabilities,
                        in interWorkspace,
                        in motionVectorCosts,
                        transformPrediction,
                        interIntraAbove,
                        interIntraLeft,
                        firstIntermediate,
                        secondIntermediate,
                        compoundMask,
                        in transformEdges,
                        in lumaCoefficientEdges,
                        in blueCoefficientEdges,
                        in redCoefficientEdges,
                        modeInfoGrid,
                        modeInfoAllocation,
                        displacementVectors,
                        encoderSegmentMap,
                        previousSegmentMap,
                        superblockCoefficients,
                        workspaceStorage,
                        sourceLuma,
                        sourceBlue,
                        sourceRed,
                        reconstructionLuma,
                        reconstructionBlue,
                        reconstructionRed,
                        macroBlock,
                        blockOrigin,
                        regularStatistics,
                        ref modeInfo,
                        ref block,
                        ref paletteInfo)
                    : regularStatistics;
            }

            this.state.BlockWorkspace.EvaluationStage = Av1EncoderEvaluationStage.Default;
            if (isInterFrame && modeInfo.Block.ReferenceFrame == Av1ReferenceFrameType.Intra &&
                this.state.SelectedBlockStatistics.Cost != long.MaxValue)
            {
                // Winner refinement and palette search follow the family comparison. Retain syntax and
                // transform states in idle inter storage while these trials reuse the intra prediction storage.
                this.state.BlockWorkspace.EvaluationStage = Av1EncoderEvaluationStage.Default;
                Av1EncoderPartitionTree.ModeContext winner = Av1EncoderBlockWorkspace.GetIntraWinnerContext(workspaceStorage, block.HasChroma ? 3 : 1);
                winner.Snapshot = new Av1EncoderPartitionTree.ModeSnapshot
                {
                    ModeInfo = modeInfo,
                    Block = block,
                    Palette = paletteInfo,
                    Statistics = this.state.SelectedBlockStatistics
                };

                Size retainedExtent = GetCodedTransformExtent(macroBlock, blockSize, modeInfo.Block.TransformSize, 0, 0);
                this.RetainModeContext(
                    superblockCoefficients,
                    winner,
                    this.state.CodedAreaLuma,
                    this.state.CodedAreaChroma,
                    retainedExtent.Width * retainedExtent.Height,
                    chromaArea);

                // The winner keeps the transform grid that its own luma search chose. The shared coefficient buffer belongs to whichever
                // candidate wrote it last. A grid taken from there can give this block a transform type that its size does not allow.
                CopyWinnerTransformStates(retainedLumaStates, winner.GetTransformStates(Av1Plane.Y));

                // The palette search reads the mode search result and its chroma from before the winner refinement. The refinement keeps its
                // own best cost, so the palette bound and comparison use the result of the mode search.
                Av1RateDistortionStatistics modeSearchStatistics = this.state.SelectedBlockStatistics;
                Av1RateDistortionStatistics modeSearchChroma = chromaStatistics;
                Av1EncoderSpeedSettings speedSettings = this.state.Picture.Parent.SpeedSettings;
                if (!this.BlockLossless &&
                    (speedSettings.IntraTransformTypeSearchLevel != 0 ||
                     speedSettings.EnableWinnerCoefficientOptimization || speedSettings.DeferTransformSizeSearch))
                {
                    // The luma refinement writes each improving trial into the luma winner context, which shares storage with this winner.
                    // A copy of the grid of the winner lets a rejected refinement leave the grid as the mode search chose it.
                    InlineArray256<Av1EncoderTransformBlockState> keptLumaStates = default;
                    Span<Av1EncoderTransformBlockState> winnerLuma = winner.GetTransformStates(Av1Plane.Y);
                    Span<Av1EncoderTransformBlockState> keptLuma = keptLumaStates[..winnerLuma.Length];
                    winnerLuma.CopyTo(keptLuma);
                    bool refinementAccepted = false;

                    this.state.BlockWorkspace.EvaluationStage = Av1EncoderEvaluationStage.Winner;
                    Av1RateDistortionStatistics refinedLuma = Av1RateDistortionStatistics.Invalid;
                    Av1EncoderPaletteInfo refinedPalette = paletteInfo;
                    int refinedAngle = block.PredictionUnit.AngleDelta[(int)Av1PlaneType.Y];
                    Av1FilterIntraMode refinedFilter = block.FilterIntraMode;
                    Av1TransformSize refinedSize = modeInfo.Block.TransformSize;
                    Av1PredictionMode refinedMode = this.RefineLumaMode(
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
                        workspaceStorage,
                        sourceLuma,
                        sourceBlue,
                        sourceRed,
                        reconstructionLuma,
                        reconstructionBlue,
                        reconstructionRed,
                        macroBlock,
                        blockOrigin,
                        blockSize,
                        retainedLumaStates,
                        modeInfo.Block.Mode,
                        ref refinedPalette,
                        ref refinedAngle,
                        ref refinedFilter,
                        ref refinedSize,
                        ref refinedLuma);

                    // The selected UV mode is refined against the newly reconstructed luma. Its mode, angle, palette and CfL alpha stay
                    // fixed. Only the transform coefficients change.
                    if (refinedLuma.Cost != long.MaxValue)
                    {
                        this.state.BlockWorkspace.EvaluationStage = Av1EncoderEvaluationStage.Winner;
                        Av1MacroBlockModeInfo refinedModeInfo = modeInfo;
                        refinedModeInfo.Block.Mode = refinedMode;
                        refinedModeInfo.Block.TransformSize = refinedSize;
                        Av1EncoderBlockStruct refinedBlock = block;
                        refinedBlock.PredictionUnit.AngleDelta[(int)Av1PlaneType.Y] = (sbyte)refinedAngle;
                        refinedBlock.FilterIntraMode = refinedFilter;
                        Av1RateDistortionStatistics refinedChroma = this.RefineSelectedChroma(
                            writer,
                            in tables,
                            in modeWorkspace,
                            transformCoefficients,
                            dequantizedCoefficients,
                            searchDequantizedCoefficients,
                            transformWorkspace,
                            transformTypeProbabilities,
                            blockResidual,
                            searchCoefficients,
                            searchReconstructions,
                            in blueCoefficientEdges,
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
                            blockOrigin,
                            refinedModeInfo,
                            refinedBlock,
                            refinedPalette,
                            chromaStatistics);

                        refinedLuma.Add(this.state.RateMultiplier, refinedChroma);
                        Av1RateDistortionStatistics refinedStatistics =
                            this.GetRegularBlockCost(in tables, modeInfoGrid, modeInfoAllocation, macroBlock, refinedLuma);

                        if (this.UsesHighBitDepthSharpness)
                        {
                            this.ChargeRefinedIntraTextureLoss(
                                sourceLuma,
                                sourceBlue,
                                sourceRed,
                                reconstructionLuma,
                                reconstructionBlue,
                                reconstructionRed,
                                ref refinedStatistics,
                                blockOrigin,
                                blockSize,
                                refinedMode);
                        }

                        if (refinedStatistics.Cost < this.state.SelectedBlockStatistics.Cost)
                        {
                            modeInfo = refinedModeInfo;
                            block = refinedBlock;
                            paletteInfo = refinedPalette;
                            chromaStatistics = refinedChroma;
                            this.state.SelectedBlockStatistics = refinedStatistics;
                            winner.Snapshot.ModeInfo = modeInfo;
                            winner.Snapshot.Block = block;
                            winner.Snapshot.Palette = paletteInfo;
                            winner.Snapshot.Statistics = refinedStatistics;
                            Size refinedExtent = GetCodedTransformExtent(macroBlock, blockSize, refinedSize, 0, 0);
                            this.RetainModeContext(
                                superblockCoefficients,
                                winner,
                                this.state.CodedAreaLuma,
                                this.state.CodedAreaChroma,
                                refinedExtent.Width * refinedExtent.Height,
                                chromaArea);

                            CopyWinnerTransformStates(retainedLumaStates, winner.GetTransformStates(Av1Plane.Y));
                            refinementAccepted = true;
                        }
                    }

                    if (!refinementAccepted)
                    {
                        keptLuma.CopyTo(winnerLuma);
                        CopyWinnerTransformStates(keptLuma, retainedLumaStates);
                    }

                    this.state.BlockWorkspace.EvaluationStage = Av1EncoderEvaluationStage.Default;
                }

                // The search leaves the last transform block of each plane as its prediction. Thus the winner is encoded again, whether or
                // not a refinement ran. The next block predicts from it and stores its luma for chroma-from-luma. A leaf of its own partition
                // search has no such encode, so the winner keeps its searched types here. The partition search writes DCT_DCT for a sibling
                // that quantized to nothing.
                int reconstructedLumaArea = this.state.CodedAreaLuma;
                int reconstructedChromaArea = this.state.CodedAreaChroma;
                this.state.KeepSearchedZeroBlockTypes = true;
                this.ReconstructSelectedIntraBlock(
                    writer,
                    in tables,
                    in modeWorkspace,
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
                    sourceLuma,
                    sourceBlue,
                    sourceRed,
                    reconstructionLuma,
                    reconstructionBlue,
                    reconstructionRed,
                    macroBlock,
                    blockOrigin,
                    winner);

                this.state.KeepSearchedZeroBlockTypes = false;
                this.state.CodedAreaLuma = reconstructedLumaArea;
                this.state.CodedAreaChroma = reconstructedChromaArea;

                Av1RateDistortionStatistics paletteStatistics = modeSearchStatistics;
                Av1EncoderPaletteInfo candidatePalette = paletteInfo;
                Av1TransformSize paletteTransformSize = modeInfo.Block.TransformSize;
                Av1ModeCosts modeCosts = tables.ModeCosts;
                if (Av1TileWriter.IsPaletteAllowed(this.state.Picture.Parent.FrameHeader.AllowScreenContentTools, blockSize) &&
                    this.SelectLumaPalette(
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
                    blockOrigin,
                    blockSize,
                    retainedLumaStates,
                    64,
                    Av1SymbolEncoder.GetInterFrameLumaModeCost(modeCosts, Av1PredictionMode.DC, blockSize),
                    ref paletteStatistics,
                    ref candidatePalette,
                    ref paletteTransformSize))
                {
                    // An intra transform block always signals coefficients in the rate-distortion search. Thus a palette is never skippable,
                    // even when it quantized to nothing, and it pays for its residual syntax and the cleared skip flag.
                    int intraInterContext = Av1TileWriter.GetIntraInterContext(modeInfoGrid, modeInfoAllocation, macroBlock);
                    int skipContext = Av1TileWriter.GetSkipContext(modeInfoGrid, modeInfoAllocation, macroBlock);
                    int rate = Av1SymbolEncoder.GetIsInterCost(tables.ModeCosts, false, intraInterContext) +
                        paletteStatistics.Rate + modeSearchChroma.Rate +
                        Av1SymbolEncoder.GetSkipCost(modeCosts, false, skipContext);

                    Av1RateDistortionStatistics combinedStatistics = new(
                        this.state.RateMultiplier, rate, paletteStatistics.Distortion + modeSearchChroma.Distortion);

                    if (combinedStatistics.Cost < modeSearchStatistics.Cost)
                    {
                        // The coded skip flag stays clear, so the final encode quantizes every transform block again.
                        modeInfo.Block.Mode = Av1PredictionMode.DC;
                        modeInfo.Block.TransformSize = paletteTransformSize;
                        modeInfo.Block.Skip = false;
                        block.PredictionUnit.AngleDelta[(int)Av1PlaneType.Y] = 0;
                        block.FilterIntraMode = Av1FilterIntraMode.AllFilterIntraModes;
                        paletteInfo = candidatePalette;
                        this.state.SelectedBlockStatistics = combinedStatistics;
                        winner.Snapshot.ModeInfo = modeInfo;
                        winner.Snapshot.Block = block;
                        winner.Snapshot.Palette = paletteInfo;
                        winner.Snapshot.Statistics = combinedStatistics;
                        Size paletteExtent = GetCodedTransformExtent(macroBlock, blockSize, paletteTransformSize, 0, 0);
                        this.RetainModeContext(
                            superblockCoefficients,
                            winner,
                            this.state.CodedAreaLuma,
                            this.state.CodedAreaChroma,
                            paletteExtent.Width * paletteExtent.Height,
                            chromaArea);

                        CopyWinnerTransformStates(retainedLumaStates, winner.GetTransformStates(Av1Plane.Y));
                    }

                    // The kept winner is built again after the palette trials. This also builds the CfL chroma again from the reconstructed
                    // luma of a winning palette, and keeps its selected UV mode.
                    int savedLumaArea = this.state.CodedAreaLuma;
                    int savedChromaArea = this.state.CodedAreaChroma;
                    this.state.KeepSearchedZeroBlockTypes = true;
                    this.ReconstructSelectedIntraBlock(
                        writer,
                        in tables,
                        in modeWorkspace,
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
                        sourceLuma,
                        sourceBlue,
                        sourceRed,
                        reconstructionLuma,
                        reconstructionBlue,
                        reconstructionRed,
                        macroBlock,
                        blockOrigin,
                        winner);

                    this.state.KeepSearchedZeroBlockTypes = false;
                    this.state.CodedAreaLuma = savedLumaArea;
                    this.state.CodedAreaChroma = savedChromaArea;
                }
            }
            else if (encodeSelected && modeInfo.Block.ReferenceFrame <= Av1ReferenceFrameType.Intra &&
                !modeInfo.Block.UseIntraBlockCopy && this.state.SelectedBlockStatistics.Cost != long.MaxValue)
            {
                // The search leaves the last transform block of each plane as its prediction. Thus the winner is encoded again over every
                // transform block before the syntax is written, and the next block predicts from it.
                Av1EncoderPartitionTree.ModeContext winner = Av1EncoderBlockWorkspace.GetIntraWinnerContext(workspaceStorage, block.HasChroma ? 3 : 1);
                winner.Snapshot = new Av1EncoderPartitionTree.ModeSnapshot
                {
                    ModeInfo = modeInfo,
                    Block = block,
                    Palette = paletteInfo,
                    Statistics = this.state.SelectedBlockStatistics
                };

                Size selectedExtent = GetCodedTransformExtent(macroBlock, blockSize, modeInfo.Block.TransformSize, 0, 0);
                this.RetainModeContext(
                    superblockCoefficients,
                    winner,
                    this.state.CodedAreaLuma,
                    this.state.CodedAreaChroma,
                    selectedExtent.Width * selectedExtent.Height,
                    chromaArea);

                CopyWinnerTransformStates(retainedLumaStates, winner.GetTransformStates(Av1Plane.Y));
                int savedLumaArea = this.state.CodedAreaLuma;
                int savedChromaArea = this.state.CodedAreaChroma;
                this.ReconstructSelectedIntraBlock(
                    writer,
                    in tables,
                    in modeWorkspace,
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
                    sourceLuma,
                    sourceBlue,
                    sourceRed,
                    reconstructionLuma,
                    reconstructionBlue,
                    reconstructionRed,
                    macroBlock,
                    blockOrigin,
                    winner);

                this.state.CodedAreaLuma = savedLumaArea;
                this.state.CodedAreaChroma = savedChromaArea;
            }

            if (isInterFrame)
            {
                // Skip mode is a one-sided compound when every reference is in the past. Thus a frame that drops those pairs neither
                // searches it nor prices the non-skip-mode symbol. Sharpness 3 never searches it.
                ObuSkipModeParameters skipModeParameters = this.state.Picture.Parent.FrameHeader.SkipModeParameters;
                if (skipModeParameters.SkipModeFlag && Math.Min(blockSize.GetWidth(), blockSize.GetHeight()) >= 8 &&
                    this.state.BlockWorkspace.EncoderOptions.Sharpness != 3 &&
                    !this.state.Picture.Parent.PrunesAllCompoundReferences)
                {
                    int skipModeContext = Av1TileWriter.GetSkipModeContext(modeInfoGrid, modeInfoAllocation, macroBlock);
                    Av1RateDistortionStatistics selectedStatistics = this.state.SelectedBlockStatistics;

                    // A block whose kept candidates all failed still compares skip mode with the best estimate of the mode loop. It keeps no
                    // mode when skip mode loses.
                    bool comparesLeftoverEstimate = selectedStatistics.Cost == long.MaxValue &&
                        this.state.LeftoverInterEstimate.Cost != long.MaxValue;

                    if (comparesLeftoverEstimate)
                    {
                        selectedStatistics = this.state.LeftoverInterEstimate;
                    }

                    Av1RateDistortionStatistics syntaxStatistics = new(
                        this.state.RateMultiplier, Av1SymbolEncoder.GetSkipModeCost(tables.ModeCosts, false, skipModeContext), 0);

                    // The non-skip-mode symbol is priced only when the reference lists of the pair exist.
                    bool skipModeListsReady = this.HasSkipModeReferenceLists(
                        workspaceStorage, skipModeParameters.FirstReferenceFrame, skipModeParameters.SecondReferenceFrame);

                    byte availableReferences = this.state.Picture.Parent.AvailableReferenceMask;
                    bool searchesSkipMode = (availableReferences & (1 << (int)skipModeParameters.FirstReferenceFrame)) != 0 &&
                        (availableReferences & (1 << (int)skipModeParameters.SecondReferenceFrame)) != 0 &&
                        skipModeListsReady;

                    // A searched skip mode prices the symbol itself, after its vectors pass the vector validity test.
                    if (selectedStatistics.Cost != long.MaxValue && skipModeListsReady && !searchesSkipMode)
                    {
                        selectedStatistics.Add(this.state.RateMultiplier, syntaxStatistics);
                    }

                    if (searchesSkipMode)
                    {
                        this.SelectSkipModeBlock(
                            in tables,
                            in interWorkspace,
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
                            reconstructionLuma,
                            reconstructionBlue,
                            reconstructionRed,
                            macroBlock,
                            blockOrigin,
                            skipModeContext,
                            syntaxStatistics,
                            ref modeInfo,
                            ref block,
                            ref selectedStatistics,
                            ref interVector,
                            ref interSecondaryVector,
                            ref interStates);
                    }

                    // Skip mode replaces a result that nothing else produced. But a skip mode at or above the block budget still leaves the
                    // block without a result.
                    bool noMode = modeInfo.Block.SkipMode
                        ? selectedStatistics.Cost >= this.state.BlockCostLimit
                        : comparesLeftoverEstimate;

                    this.state.SelectedBlockStatistics = noMode ? Av1RateDistortionStatistics.Invalid : selectedStatistics;
                }

                if (modeInfo.Block.ReferenceFrame > Av1ReferenceFrameType.Intra)
                {
                    // The block codes no residual when the last transform search left its skip flag set, or when the mode search winner was
                    // skippable. This also applies after a winner refinement that kept coefficients. The rate-distortion result keeps the
                    // refined cost.
                    if (!modeInfo.Block.Skip && (this.state.TransformSearchSkip || this.state.SelectedBlockStatistics.AllTransformsEmpty))
                    {
                        Av1TransformSize maximumSize = this.BlockLossless
                            ? Av1TransformSize.Size4x4
                            : blockSize.GetMaximumTransformSize();

                        modeInfo.Block.Skip = true;
                        modeInfo.Block.TransformSize = maximumSize;
                        modeInfo.Block.InterTransformSizes.Fill(maximumSize);
                        interStates = default;
                    }

                    paletteInfo = default;
                    this.state.KeepSearchedZeroBlockTypes = true;
                    this.state.EncodedWithoutCoefficients = !this.ReconstructSelectedInterBlock(
                        writer,
                        in tables,
                        transformCoefficients,
                        dequantizedCoefficients,
                        transformWorkspace,
                        in interWorkspace,
                        transformPrediction,
                        interIntraAbove,
                        interIntraLeft,
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
                        sourceLuma,
                        sourceBlue,
                        sourceRed,
                        reconstructionLuma,
                        reconstructionBlue,
                        reconstructionRed,
                        macroBlock,
                        blockOrigin,
                        modeInfo,
                        block,
                        interVector,
                        interSecondaryVector,
                        interStates);

                    this.state.KeepSearchedZeroBlockTypes = false;
                    this.state.Picture.SetDisplacementVector(displacementVectors, modeInfoPosition, interVector);
                    if (modeInfo.Block.SecondaryReferenceFrame > Av1ReferenceFrameType.Intra)
                    {
                        // The spatial motion stack of the encoder must keep both vectors of the selected pair. Otherwise later compound blocks
                        // derive another nearest pair than the decoder does.
                        this.state.Picture.SetSecondaryDisplacementVector(referenceContexts, modeInfoPosition, interSecondaryVector);
                    }
                }
            }

            if (this.state.SelectedBlockStatistics.Cost == long.MaxValue)
            {
                return;
            }

            // A frame coded from the source of an alternate reference leaves the thresholds as they were.
            if (isInterFrame && this.state.Picture.Parent.SpeedSettings.AdaptiveModeThresholdLevel != 0 &&
                !this.state.Picture.Parent.IsSourceAlternateReference)
            {
                // The update runs only after the residual refinement, the palette and the skip-mode selection are complete. The partition
                // replay returns earlier and must not count the kept winner a second time.
                Av1ModeThresholds.Update(
                    this.state.BlockWorkspace.GetModeThresholdFactors(workspaceStorage),
                    blockSize,
                    this.state.Picture.Sequence.SequenceHeader.SuperblockSizeLog2 == 7 ? Av1BlockSize.Block128x128 : Av1BlockSize.Block64x64,
                    Av1ModeThresholds.GetIndex(modeInfo.Block.Mode, modeInfo.Block.ReferenceFrame, modeInfo.Block.SecondaryReferenceFrame),
                    this.state.Picture.Parent.FrameHeader.ReferenceMode == ObuReferenceMode.SingleReference,
                    this.state.Picture.Parent.SpeedSettings.AdaptiveModeThresholdLevel);
            }

            Size lumaExtent = GetCodedTransformExtent(macroBlock, blockSize, modeInfo.Block.TransformSize, 0, 0);
            this.state.CodedAreaLuma += modeInfo.Block.ReferenceFrame > Av1ReferenceFrameType.Intra || modeInfo.Block.UseIntraBlockCopy
                ? GetInterLumaCodedArea(macroBlock, ref modeInfo.Block)
                : lumaExtent.Width * lumaExtent.Height;

            this.state.CodedAreaChroma += chromaArea;
        }

        /// <summary>
        /// Searches one single-reference syntax mode and retains its dynamic-reference-list winner.
        /// </summary>
        /// <param name="writer">The symbol encoder that prices the syntax.</param>
        /// <param name="tables">The rate tables of the tile.</param>
        /// <param name="transformCoefficients">The forward transform output buffer.</param>
        /// <param name="dequantizedCoefficients">The dequantized coefficient buffer of the candidate.</param>
        /// <param name="searchDequantizedCoefficients">The dequantized coefficient buffer of the winner.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
        /// <param name="transformTypeProbabilities">The transform type probabilities of every update type and size.</param>
        /// <param name="blockResidual">The residual buffer of the motion search.</param>
        /// <param name="interWorkspace">The inter prediction buffers of the block.</param>
        /// <param name="motionSearchPrediction">The prediction buffer of the motion search.</param>
        /// <param name="motionVectorCosts">The motion vector rates of the frame precision.</param>
        /// <param name="transformPrediction">The prediction storage of one transform block.</param>
        /// <param name="interIntraAbove">The extended above edge of an inter-intra prediction.</param>
        /// <param name="interIntraLeft">The extended left edge of an inter-intra prediction.</param>
        /// <param name="firstIntermediate">The compound intermediate of the first reference.</param>
        /// <param name="secondIntermediate">The compound intermediate of the second reference.</param>
        /// <param name="compoundMask">The blend mask of a masked compound prediction.</param>
        /// <param name="transformEdges">The transform size context edges of the tile.</param>
        /// <param name="lumaCoefficientEdges">The luma coefficient context edges of the tile.</param>
        /// <param name="blueCoefficientEdges">The blue-difference coefficient context edges of the tile.</param>
        /// <param name="redCoefficientEdges">The red-difference coefficient context edges of the tile.</param>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="displacementVectors">The displacement vectors of the picture, one per allocation entry.</param>
        /// <param name="workspaceStorage">The storage of the block workspace, which holds the search buffers of every block.</param>
        /// <param name="sourceLuma">The samples of the complete source luma plane, read once per frame pass.</param>
        /// <param name="sourceBlue">The samples of the complete source blue-difference plane, read once per frame pass.</param>
        /// <param name="sourceRed">The samples of the complete source red-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionLuma">The samples of the complete reconstructed luma plane, read once per frame pass.</param>
        /// <param name="reconstructionBlue">The samples of the complete reconstructed blue-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionRed">The samples of the complete reconstructed red-difference plane, read once per frame pass.</param>
        /// <param name="macroBlock">The neighbor availability of the block.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="bestCost">The cost of the best block result so far.</param>
        /// <param name="referenceFrame">The reference frame of the mode.</param>
        /// <param name="requestedMode">The single-reference mode to search.</param>
        /// <param name="referenceMotionVectors">The reference vector list of the reference frame.</param>
        /// <param name="cachedInterIntraMode">The inter-intra mode that an earlier search of the reference chose.</param>
        /// <param name="searchMotionModes">Whether each candidate searches OBMC and warped motion after simple translation.</param>
        /// <param name="modeInfo">The block decisions.</param>
        /// <param name="block">The block state.</param>
        /// <param name="selectedVector">The motion vector of the winner.</param>
        /// <param name="selectedStates">The transform states of the winner.</param>
        /// <param name="searchedNewVectors">The new vectors that earlier searches of the reference found, by list index.</param>
        /// <param name="searchedNewVectorMask">The list indices whose new vector is in <paramref name="searchedNewVectors"/>.</param>
        /// <returns>The rate and distortion of the winner.</returns>
        public Av1RateDistortionStatistics SelectSingleReferenceMode(
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> searchDequantizedCoefficients,
            Span<int> transformWorkspace,
            ReadOnlySpan<int> transformTypeProbabilities,
            Span<short> blockResidual,
            in Av1EncoderInterPredictionWorkspace<byte> interWorkspace,
            Span<byte> motionSearchPrediction,
            in Av1MotionVectorCosts motionVectorCosts,
            Span<byte> transformPrediction,
            Span<byte> interIntraAbove,
            Span<byte> interIntraLeft,
            Span<ushort> firstIntermediate,
            Span<ushort> secondIntermediate,
            Span<byte> compoundMask,
            in Av1NeighborEdges<byte> transformEdges,
            in Av1NeighborEdges<byte> lumaCoefficientEdges,
            in Av1NeighborEdges<byte> blueCoefficientEdges,
            in Av1NeighborEdges<byte> redCoefficientEdges,
            ReadOnlySpan<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            ReadOnlySpan<Av1EncoderDisplacementVector> displacementVectors,
            Span<int> workspaceStorage,
            ReadOnlySpan<byte> sourceLuma,
            ReadOnlySpan<byte> sourceBlue,
            ReadOnlySpan<byte> sourceRed,
            Span<byte> reconstructionLuma,
            Span<byte> reconstructionBlue,
            Span<byte> reconstructionRed,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            long bestCost,
            Av1ReferenceFrameType referenceFrame,
            Av1PredictionMode requestedMode,
            ref Av1ReferenceMotionVectors referenceMotionVectors,
            ref int cachedInterIntraMode,
            bool searchMotionModes,
            ref Av1MacroBlockModeInfo modeInfo,
            ref Av1EncoderBlockStruct block,
            out Av1MotionVector selectedVector,
            out InlineArray128<Av1EncoderTransformBlockState> selectedStates,
            ref InlineArray3<Av1MotionVector> searchedNewVectors,
            ref byte searchedNewVectorMask)
        {
            Span<byte> lumaFrame = reconstructionLuma;
            Av1BlockSize blockSize = modeInfo.Block.BlockSize;
            Av1TransformSize lumaTransformSize = this.BlockLossless
                ? Av1TransformSize.Size4x4
                : blockSize.GetMaximumTransformSize();

            ObuFrameHeader frameHeader = this.state.Picture.Parent.FrameHeader;
            Point modeInfoPosition = new(blockOrigin.X >> Av1Constants.ModeInfoSizeLog2, blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2);

            // A mode that codes a dynamic reference list index searches more than one entry of that list, at most three. The near mode
            // starts at the second entry, because the nearest mode already covers the first, so it has one fewer candidate. Every other mode
            // has a single vector and no list index to code.
            InlineArray3<Av1MotionVector> candidateVectorsStorage = default;
            InlineArray3<byte> candidateReferenceIndicesStorage = default;
            Span<Av1MotionVector> candidateVectors = candidateVectorsStorage;
            Span<byte> candidateReferenceIndices = candidateReferenceIndicesStorage;
            int candidateCount = PrepareSingleReferenceCandidates(
                frameHeader,
                blockSize,
                modeInfoPosition,
                lumaTransformSize,
                referenceFrame,
                requestedMode,
                ref referenceMotionVectors,
                candidateVectors,
                candidateReferenceIndices,
                ref modeInfo,
                ref block);

            // Each plane keeps two buffers: one for the best candidate so far, and one for the candidate under test. The winner moves by a
            // swap of the spans, not by a copy of a block of samples for every improvement.
            Span<byte> selectedLumaReconstruction = this.GetInterSamples(in interWorkspace, 0);
            Span<byte> candidateLumaReconstruction = this.GetInterSamples(in interWorkspace, 6);
            Span<byte> selectedBlueReconstruction = this.GetInterSamples(in interWorkspace, 1);
            Span<byte> candidateBlueReconstruction = this.GetInterSamples(in interWorkspace, 7);
            Span<byte> selectedRedReconstruction = this.GetInterSamples(in interWorkspace, 2);
            Span<byte> candidateRedReconstruction = this.GetInterSamples(in interWorkspace, 8);
            Span<int> selectedLumaCoefficients = interWorkspace.SelectedLumaCoefficients;
            Span<int> candidateLumaCoefficients = interWorkspace.LumaCandidateCoefficients;
            Span<int> selectedBlueCoefficients = interWorkspace.SelectedBlueCoefficients;
            Span<int> candidateBlueCoefficients = interWorkspace.BlueCandidateCoefficients;
            Span<int> selectedRedCoefficients = interWorkspace.SelectedRedCoefficients;
            Span<int> candidateRedCoefficients = interWorkspace.RedCandidateCoefficients;
            int skipContext = Av1TileWriter.GetSkipContext(modeInfoGrid, modeInfoAllocation, macroBlock);
            Span<byte> referenceCounts = stackalloc byte[Av1Constants.ReferenceFrameCount];
            Av1TileWriter.CollectNeighborReferenceCounts(modeInfoGrid, modeInfoAllocation, macroBlock, referenceCounts);

            // Every candidate prices its reference and skip syntax with the same rates.
            Av1ModeCosts modeCosts = tables.ModeCosts;

            // Every candidate of this reference pays the same syntax: the inter flag and the reference selection itself. This rate is priced
            // once, outside the candidate loop.
            int intraInterContext = Av1TileWriter.GetIntraInterContext(modeInfoGrid, modeInfoAllocation, macroBlock);
            int commonPredictionRate = Av1SymbolEncoder.GetIsInterCost(tables.ModeCosts, isInter: true, intraInterContext) +
                Av1SymbolEncoder.GetSingleReferenceCost(tables.ModeCosts, referenceFrame, referenceCounts);

            // A frame that lets each block choose its reference mode codes one flag that tells whether the block is compound. A block with a
            // side below eight samples cannot be compound, so it codes no flag and pays nothing.
            if (frameHeader.ReferenceMode == ObuReferenceMode.ReferenceModeSelect &&
                Math.Min(blockSize.GetWidth(), blockSize.GetHeight()) >= 8)
            {
                commonPredictionRate += modeCosts.GetCompInter(Av1SymbolContextHelper.GetReferenceModeContext(modeInfoGrid, modeInfoAllocation, macroBlock), 0);
            }

            // A frame that selects its transform size codes, for each block, whether the largest transform is split. This search never
            // splits, so every candidate pays the same unsplit flag. The smallest transform cannot split and codes nothing. A mode evaluation
            // that defers the transform size search codes the largest transform, which signals no split flag.
            int transformPartitionRate = 0;
            if (frameHeader.TransformMode == Av1TransformMode.Select && lumaTransformSize != Av1TransformSize.Size4x4 &&
                (!this.state.Picture.Parent.SpeedSettings.DeferTransformSizeSearch ||
                    this.state.BlockWorkspace.EvaluationStage != Av1EncoderEvaluationStage.Candidate))
            {
                int topIndex = transformEdges.GetTopIndex(blockOrigin);
                int leftIndex = transformEdges.GetLeftIndex(blockOrigin);
                int transformPartitionContext = Av1SymbolContextHelper.GetTransformPartitionContext(
                    transformEdges.Top[topIndex], transformEdges.Left[leftIndex], blockSize, lumaTransformSize);

                transformPartitionRate = Av1SymbolEncoder.GetTransformPartitionCost(tables.ModeCosts, false, transformPartitionContext);
            }

            selectedStates = default;

            ObuSequenceHeader sequenceHeader = this.state.Picture.Sequence.SequenceHeader;
            bool interIntraEligible = sequenceHeader.EnableInterIntraCompound &&
                blockSize is >= Av1BlockSize.Block8x8 and <= Av1BlockSize.Block32x32;

            bool isSwitchable = frameHeader.InterpolationFilter == Av1InterpolationFilter.Switchable;
            Av1InterpolationFilter defaultFilter = isSwitchable ? Av1InterpolationFilter.Regular : frameHeader.InterpolationFilter;
            SingleReferenceSearchState search = new(defaultFilter);
            SingleReferenceMotionSearch motionSearch = this.CreateSingleReferenceSearch(
                writer,
                in tables,
                sourceLuma,
                motionSearchPrediction,
                blockResidual,
                interWorkspace,
                in lumaCoefficientEdges,
                in motionVectorCosts,
                blockOrigin,
                blockSize,
                referenceFrame,
                transformPartitionRate,
                skipContext,
                out Size frameSize,
                out Rectangle frameBounds);

            int spatialMagnitude = GetSpatialMotionMagnitude(referenceMotionVectors, blockOrigin, blockSize, frameSize);

            Av1MotionSearchSettings motionSettings = this.state.Picture.Parent.MotionSearchSettings;
            Av1MotionSearchBase.SingleReferenceState motionState = default;

            // The starts are the spatial start and one candidate for each 16x16 model block of the largest block.
            Span<Av1MotionSearchBase.StartingCandidate> motionStarts =
                stackalloc Av1MotionSearchBase.StartingCandidate[Av1EncoderBlockWorkspace.TplSuperblockBlockCount + 1];

            // The model prunes no entry of the mode when a neighbor shares its reference.
            bool tplPruning = this.state.TplInterModePruning &&
                !this.HasNeighborReferenceMatch(
                    modeInfoGrid,
                    modeInfoAllocation,
                    macroBlock,
                    blockOrigin,
                    blockSize,
                    referenceFrame,
                    Av1ReferenceFrameType.None);

            int candidateMask = PruneSingleReferenceCandidates(
                this.state.Picture.Parent,
                this.state.BlockQIndex,
                requestedMode,
                referenceFrame,
                ref referenceMotionVectors,
                frameBounds,
                candidateVectors[..candidateCount],
                candidateReferenceIndices[..candidateCount]);

            if (requestedMode == Av1PredictionMode.NearMotionVector && candidateCount > 1)
            {
                InlineArray3<long> translationCostsStorage = default;
                Span<long> translationCosts = translationCostsStorage;
                translationCosts[..].Fill(long.MaxValue);
                InlineArray4<Av1RateDistortionStatistics> planeStatisticsStorage = default;
                Span<Av1RateDistortionStatistics> planeStatistics = planeStatisticsStorage;

                // A scaled reference is not pruned.
                bool modelTranslation = this.state.Picture.Parent.SpeedSettings.PruneNearMotionByTranslation &&
                    blockSize.GetWidth() * blockSize.GetHeight() > 64 &&
                    !this.IsScaledReference(modeInfo.Block.ReferenceFrame);

                modeInfo.Block.Mode = requestedMode;
                long bestTranslation = long.MaxValue;
                for (int index = 0; index < candidateCount; index++)
                {
                    if ((candidateMask & (1 << index)) == 0)
                    {
                        continue;
                    }

                    int modeRate = GetInterModeRate(
                        in tables, in motionVectorCosts, requestedMode, candidateVectors[index], candidateReferenceIndices[index], referenceMotionVectors);

                    // Every candidate of this mode pays the mode itself. Without that rate, the rest separates one list entry from another. A
                    // candidate whose syntax alone already costs more than the block winner cannot win at any distortion, so it drops before
                    // any prediction is formed.
                    int drlRate = modeRate - Av1SymbolEncoder.GetInterModeCost(tables.ModeCosts, requestedMode, referenceMotionVectors.ModeContext);

                    if (Av1RateDistortion.GetCost(this.state.RateMultiplier, commonPredictionRate + drlRate, 0) > bestCost)
                    {
                        candidateMask &= ~(1 << index);
                        continue;
                    }

                    // A surviving candidate is priced with a model of the prediction error, not with a transform search. The estimate only
                    // ranks the list entries against each other, so its absolute value does not have to be exact.
                    if (modelTranslation && Av1RateDistortion.GetCost(this.state.RateMultiplier, commonPredictionRate + modeRate, 0) <= bestCost)
                    {
                        long translationLumaSquaredError = 0;
                        Av1RateDistortionStatistics estimate = this.GetInterFilterModelCost(
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
                            candidateVectors[index],
                            default,
                            modeInfo.Block,
                            blockOrigin,
                            blockSize,
                            false,
                            defaultFilter,
                            defaultFilter,
                            commonPredictionRate + modeRate,
                            long.MaxValue,
                            100,
                            0,
                            this.GetInterSamples(in interWorkspace, 3),
                            this.GetInterSamples(in interWorkspace, 4),
                            this.GetInterSamples(in interWorkspace, 5),
                            planeStatistics,
                            ref translationLumaSquaredError);

                        // The estimate also predicts luma into the frame.
                        this.WriteInterLumaDestination(lumaFrame, blockOrigin, blockSize, this.GetInterSamples(in interWorkspace, 3));

                        // The model stores its luma error as the prediction error of the reference.
                        this.SetPredictionSse(modeInfo.Block.ReferenceFrame, translationLumaSquaredError);
                        translationCosts[index] = estimate.Cost;
                        bestTranslation = Math.Min(bestTranslation, estimate.Cost);
                    }
                }

                if (modelTranslation)
                {
                    for (int index = 0; index < candidateCount; index++)
                    {
                        // The two relative bounds keep the best list estimate. They reject the entries that cannot come near it or the current
                        // block winner.
                        if (!((double)translationCosts[index] / bestTranslation < 1.001 &&
                            (double)translationCosts[index] / bestCost < 5))
                        {
                            candidateMask &= ~(1 << index);
                        }
                    }
                }
            }

            // The block keeps the interpolation families of the previous list entry until the next entry searches its filters, so a
            // new-motion search predicts with them. The first entry starts from the default filters.
            Av1InterpolationFilter heldHorizontalFilter = defaultFilter;
            Av1InterpolationFilter heldVerticalFilter = defaultFilter;

            // A new-motion entry keeps its motion modes only when the vector that its last trial leaves in the block differs from the
            // reference. Otherwise the entry gives nothing, and the block keeps the filters of that trial. The spare buffers hold the winner
            // from before the entry until the test. The test runs when the next entry starts and after the last entry.
            Span<byte> spareLumaReconstruction = this.GetInterSamples(in interWorkspace, 9);
            Span<byte> spareBlueReconstruction = this.GetInterSamples(in interWorkspace, 10);
            Span<byte> spareRedReconstruction = this.GetInterSamples(in interWorkspace, 11);
            Span<int> spareLumaCoefficients = interWorkspace.SpareLumaCoefficients;
            Span<int> spareBlueCoefficients = interWorkspace.SpareBlueCoefficients;
            Span<int> spareRedCoefficients = interWorkspace.SpareRedCoefficients;
            bool entryPending = false;
            bool winnerHeld = false;
            Av1MotionVector entryReference = default;
            Av1MotionVector lastTrialVector = default;
            Av1InterpolationFilter lastTrialHorizontalFilter = defaultFilter;
            Av1InterpolationFilter lastTrialVerticalFilter = defaultFilter;

            // Rank interpolation families with prediction-error modeling before running a full transform search.
            // The selected inter reconstruction remains untouched while two existing prediction views alternate.
            Span<long> singleReferenceFilterCosts = this.state.BlockWorkspace.GetSingleReferenceFilterCosts(workspaceStorage);
            Span<long> singleReferenceSimpleCosts = this.state.BlockWorkspace.GetSingleReferenceSimpleCosts(workspaceStorage);
            for (int candidateIndex = 0; candidateIndex <= candidateCount; candidateIndex++)
            {
                if (entryPending)
                {
                    entryPending = false;
                    if (lastTrialVector == entryReference)
                    {
                        heldHorizontalFilter = lastTrialHorizontalFilter;
                        heldVerticalFilter = lastTrialVerticalFilter;
                        if (winnerHeld)
                        {
                            Span<byte> discardedLumaReconstruction = selectedLumaReconstruction;
                            selectedLumaReconstruction = spareLumaReconstruction;
                            spareLumaReconstruction = discardedLumaReconstruction;
                            Span<byte> discardedBlueReconstruction = selectedBlueReconstruction;
                            selectedBlueReconstruction = spareBlueReconstruction;
                            spareBlueReconstruction = discardedBlueReconstruction;
                            Span<byte> discardedRedReconstruction = selectedRedReconstruction;
                            selectedRedReconstruction = spareRedReconstruction;
                            spareRedReconstruction = discardedRedReconstruction;
                            Span<int> discardedLumaCoefficients = selectedLumaCoefficients;
                            selectedLumaCoefficients = spareLumaCoefficients;
                            spareLumaCoefficients = discardedLumaCoefficients;
                            Span<int> discardedBlueCoefficients = selectedBlueCoefficients;
                            selectedBlueCoefficients = spareBlueCoefficients;
                            spareBlueCoefficients = discardedBlueCoefficients;
                            Span<int> discardedRedCoefficients = selectedRedCoefficients;
                            selectedRedCoefficients = spareRedCoefficients;
                            spareRedCoefficients = discardedRedCoefficients;
                            search.RestoreWinner();
                        }
                    }

                    winnerHeld = false;
                }

                if (candidateIndex == candidateCount)
                {
                    break;
                }

                if ((candidateMask & (1 << candidateIndex)) == 0)
                {
                    continue;
                }

                if (tplPruning && this.PrunesInterModeByTpl(
                    Math.Min(bestCost, search.Winner.Statistics.Cost),
                    referenceFrame,
                    Av1ReferenceFrameType.None,
                    candidateReferenceIndices[candidateIndex],
                    requestedMode))
                {
                    continue;
                }

                if (requestedMode == Av1PredictionMode.NewMotionVector)
                {
                    int referenceIndex = candidateReferenceIndices[candidateIndex];
                    Av1MotionVector referenceVector = candidateVectors[candidateIndex];

                    // The list index is coded as a run of flags, each saying whether to move past
                    // the current entry. Reaching entry n therefore costs n flags that advance and
                    // one that stops, and the run ends at the first flag that does not advance.
                    int drlRate = 0;
                    for (int index = 0; index < 2 && referenceMotionVectors.Count > index + 1; index++)
                    {
                        bool advance = referenceIndex > index;
                        int context = Av1SymbolContextHelper.GetDrlContext(referenceMotionVectors.Weights, index);
                        drlRate += Av1SymbolEncoder.GetDynamicReferenceListCost(tables.ModeCosts, advance, context);
                        if (!advance)
                        {
                            break;
                        }
                    }

                    if (candidateCount > 1 &&
                        Av1RateDistortion.GetCost(this.state.RateMultiplier, commonPredictionRate + drlRate, 0) > bestCost)
                    {
                        continue;
                    }

                    // A later list entry usually starts near an entry already searched. With the nearest searched entry, this search covers
                    // only the distance between the two starting points plus the distance that the earlier search moved. It does not cover
                    // the full range again.
                    int searchRange = int.MaxValue;
                    if (motionSettings.ReduceSearchRange && referenceIndex > 0)
                    {
                        int minimumDifference = int.MaxValue;
                        int bestMatch = 0;
                        for (int index = 0; index < referenceIndex; index++)
                        {
                            // Every earlier list entry is compared, searched or not, by the vector of the list itself. The candidate array
                            // holds the search result of an entry already searched, so the comparison does not read it.
                            Av1MotionVector previousReference = referenceMotionVectors.GetNewReference(index);
                            int difference = Math.Max(
                                Math.Abs(referenceVector.Row - previousReference.Row),
                                Math.Abs(referenceVector.Column - previousReference.Column));

                            if (difference < minimumDifference)
                            {
                                minimumDifference = difference;
                                bestMatch = index;
                            }
                        }

                        // The bound holds only when the two starting points are close. Beyond sixteen samples, in the eighth-sample precision of
                        // the vectors, the earlier search gives no useful bound.
                        ref Av1MotionSearchBase.ReferenceSearchResult previous = ref motionState.References[bestMatch];
                        if (minimumDifference < 16 * 8 && previous.IsValid)
                        {
                            Av1MotionVector matchReference = referenceMotionVectors.GetNewReference(bestMatch);
                            int displacement = Math.Max(
                                Math.Abs(previous.Vector.Row - matchReference.Row),
                                Math.Abs(previous.Vector.Column - matchReference.Column));

                            searchRange = (minimumDifference + displacement + 4) >> 3;
                        }
                    }

                    Point startVector = new(
                        (referenceVector.Column + 3 + (referenceVector.Column >= 0 ? 1 : 0)) >> 3,
                        (referenceVector.Row + 3 + (referenceVector.Row >= 0 ? 1 : 0)) >> 3);

                    // The temporal dependency vectors of the covered 16x16 blocks join the spatial start, unless the speed limits the
                    // full-pixel search to the spatial start.
                    motionStarts[0] = new Av1MotionSearchBase.StartingCandidate(startVector, 0);
                    int startCount = 1;
                    int startWeight = 0;
                    if (!motionSettings.LimitFullPixelStartingCandidates)
                    {
                        startCount = Av1MotionSearchBase.CollectStartingCandidates(
                            this.state.BlockWorkspace.TplSuperblockVectors,
                            this.state.TplSuperblockBlockCount,
                            this.state.TplSuperblockStride,
                            this.state.Picture.Sequence.SequenceHeader.SuperblockSize.Get4x4WideCount(),
                            modeInfoPosition,
                            blockSize,
                            (int)referenceFrame - (int)Av1ReferenceFrameType.Last,
                            motionStarts,
                            out startWeight);
                    }

                    if (!motionSearch.Search(
                        in tables,
                        transformCoefficients,
                        dequantizedCoefficients,
                        transformWorkspace,
                        motionSettings,
                        this.state.Picture.Parent.MotionSearchStepParameter,
                        spatialMagnitude,
                        frameHeader.ShowFrame,
                        searchRange,
                        frameHeader.ForceIntegerMotionVector,
                        frameHeader.AllowHighPrecisionMotionVector,
                        fineMeshInterval: this.UsesFineSearchInterval,
                        referenceIndex,
                        referenceVector,
                        drlRate,
                        heldHorizontalFilter,
                        heldVerticalFilter,
                        motionStarts[..startCount],
                        startWeight,
                        ref motionState,
                        out Av1MotionSearchBase.FractionalResult searchResult))
                    {
                        continue;
                    }

                    // A search result is kept for the compound modes, even when the entry is then skipped as a repeat of an earlier one.
                    searchedNewVectors[referenceIndex] = searchResult.Vector;
                    searchedNewVectorMask |= (byte)(1 << referenceIndex);
                    if (motionState.References[referenceIndex].Skip)
                    {
                        continue;
                    }

                    candidateVectors[candidateIndex] = searchResult.Vector;

                    // The prediction error of the search is kept for the zero-vector pruning. The value is the error that the fractional
                    // search leaves. A forced integer vector skips that search, so the value that an earlier candidate of the reference left is
                    // read.
                    if (!frameHeader.ForceIntegerMotionVector)
                    {
                        this.SetPredictionSse(referenceFrame, searchResult.SquaredError);
                    }

                    uint searchSse = this.state.PredictionSses[(int)referenceFrame];
                    if (searchSse < this.state.BestSingleReferenceSses[(int)referenceFrame])
                    {
                        this.state.BestSingleReferenceSses[(int)referenceFrame] = searchSse;
                    }
                }

                modeInfo.Block.Mode = requestedMode;

                // The estimated mode evaluation keeps the budget of the entry for every motion mode trial. A full search lowers the budget to
                // each trial that it completes.
                long candidateLimit = Math.Min(bestCost, search.Winner.Statistics.Cost);

                // A completed trial bounds the later trials of the entry by its cost before the image tune bias.
                long entryTrialCost = long.MaxValue;
                modeInfo.Block.HorizontalInterpolationFilter = defaultFilter;
                modeInfo.Block.VerticalInterpolationFilter = defaultFilter;
                int filterCostIndex = (((((int)requestedMode - (int)Av1PredictionMode.InterModeStart) * 3) +
                    candidateReferenceIndices[candidateIndex]) * Av1Constants.ReferenceFrameCount) + (int)referenceFrame;

                Av1EncoderSpeedSettings settings = this.state.Picture.Parent.SpeedSettings;
                bool skipInterpolation = settings.SkipSingleInterpolationSearch ||
                    (settings.UseWinnerInterpolation && frameHeader.ReferenceMode == ObuReferenceMode.SingleReference);

                bool preparedPrediction = false;
                int filterRate = 0;
                if (!skipInterpolation)
                {
                    preparedPrediction = this.SelectInterFilters(
                        in tables,
                        in interWorkspace,
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
                        reconstructionLuma,
                        reconstructionBlue,
                        reconstructionRed,
                        macroBlock,
                        blockOrigin,
                        blockSize,
                        block.HasChroma,
                        candidateVectors[candidateIndex],
                        default,
                        ref modeInfo.Block,
                        candidateLimit,
                        long.MaxValue,
                        out filterRate,
                        out long filterModelCost);

                    singleReferenceFilterCosts[filterCostIndex] = filterModelCost;
                    heldHorizontalFilter = modeInfo.Block.HorizontalInterpolationFilter;
                    heldVerticalFilter = modeInfo.Block.VerticalInterpolationFilter;
                    if (settings.ModelBasedInterpolationBreakout &&
                        candidateLimit != long.MaxValue && (filterModelCost >> 3) * 3 > candidateLimit)
                    {
                        continue;
                    }
                }
                else if (Av1TileWriter.UsesSwitchableInterpolation(frameHeader, modeInfo.Block))
                {
                    int verticalContext = Av1SymbolContextHelper.GetSwitchableInterpolationContext(
                        modeInfo.Block,
                        modeInfoGrid,
                        modeInfoAllocation,
                        macroBlock,
                        0);

                    filterRate = Av1SymbolEncoder.GetSwitchableInterpolationFilterCost(tables.ModeCosts, defaultFilter, verticalContext);

                    if (sequenceHeader.EnableDualFilter)
                    {
                        int horizontalContext = Av1SymbolContextHelper.GetSwitchableInterpolationContext(
                            modeInfo.Block,
                            modeInfoGrid,
                            modeInfoAllocation,
                            macroBlock,
                            1);

                        filterRate += Av1SymbolEncoder.GetSwitchableInterpolationFilterCost(tables.ModeCosts, defaultFilter, horizontalContext);
                    }
                }

                // A new vector equal to its reference codes no difference, so simple translation skips it. The other motion modes search their
                // own vectors.
                bool newEntry = requestedMode == Av1PredictionMode.NewMotionVector;
                Av1MotionVector newReference = newEntry
                    ? referenceMotionVectors.GetNewReference(candidateReferenceIndices[candidateIndex])
                    : default;

                bool simpleSkipped = newEntry && candidateVectors[candidateIndex] == newReference;
                if (simpleSkipped && !searchMotionModes)
                {
                    continue;
                }

                Av1InterpolationFilter horizontalFilter = modeInfo.Block.HorizontalInterpolationFilter;
                Av1InterpolationFilter verticalFilter = modeInfo.Block.VerticalInterpolationFilter;
                entryPending = newEntry;
                entryReference = newReference;
                lastTrialVector = candidateVectors[candidateIndex];
                lastTrialHorizontalFilter = horizontalFilter;
                lastTrialVerticalFilter = verticalFilter;
                if (entryPending)
                {
                    search.SaveWinner();
                }

                int simplePredictionRate = commonPredictionRate + filterRate +
                    (interIntraEligible ? Av1SymbolEncoder.GetInterIntraCost(tables.ModeCosts, blockSize, false, default, false, 0) : 0) +
                    this.GetSimpleTranslationRate(in tables, macroBlock, blockOrigin, blockSize, referenceFrame, requestedMode);

                Av1RateDistortionStatistics candidateStatistics = Av1RateDistortionStatistics.Invalid;
                bool candidateSkip = false;
                InlineArray64<Av1EncoderTransformBlockState> candidateLumaStatesStorage = default;
                InlineArray16<Av1TransformSize> candidateLumaSizesStorage = default;
                InlineArray16<Av1EncoderTransformBlockState> candidateBlueStateStorage = default;
                InlineArray16<Av1EncoderTransformBlockState> candidateRedStateStorage = default;
                Span<Av1EncoderTransformBlockState> candidateLumaStates = candidateLumaStatesStorage;
                Span<Av1TransformSize> candidateLumaSizes = candidateLumaSizesStorage;
                Span<Av1EncoderTransformBlockState> candidateBlueState = candidateBlueStateStorage;
                Span<Av1EncoderTransformBlockState> candidateRedState = candidateRedStateStorage;
                if (!simpleSkipped)
                {
                    this.state.TransformSearchReset = true;
                    candidateStatistics = this.EvaluateInterCandidate(
                        writer,
                        in tables,
                        transformCoefficients,
                        dequantizedCoefficients,
                        searchDequantizedCoefficients,
                        transformWorkspace,
                        transformTypeProbabilities,
                        in interWorkspace,
                        in motionVectorCosts,
                        transformPrediction,
                        firstIntermediate,
                        secondIntermediate,
                        compoundMask,
                        in transformEdges,
                        in lumaCoefficientEdges,
                        in blueCoefficientEdges,
                        in redCoefficientEdges,
                        modeInfoGrid,
                        modeInfoAllocation,
                        displacementVectors,
                        workspaceStorage,
                        sourceLuma,
                        sourceBlue,
                        sourceRed,
                        reconstructionLuma,
                        reconstructionBlue,
                        reconstructionRed,
                        macroBlock,
                        blockOrigin,
                        blockSize,
                        Math.Min(bestCost, search.Winner.Statistics.Cost),
                        block.HasChroma,
                        simplePredictionRate,
                        referenceFrame,
                        candidateVectors[candidateIndex],
                        default,
                        requestedMode,
                        Av1ReferenceFrameType.None,
                        usePreparedPrediction: preparedPrediction,
                        Av1CompoundType.Average,
                        0,
                        false,
                        Av1DifferenceWeightedMaskType.Type38,
                        0,
                        horizontalFilter,
                        verticalFilter,
                        candidateReferenceIndices[candidateIndex],
                        false,
                        default,
                        false,
                        0,
                        referenceMotionVectors,
                        candidateLumaReconstruction,
                        candidateLumaCoefficients,
                        candidateBlueReconstruction,
                        candidateBlueCoefficients,
                        candidateRedReconstruction,
                        candidateRedCoefficients,
                        out candidateSkip,
                        out candidateLumaStatesStorage,
                        out candidateLumaSizesStorage,
                        out candidateBlueStateStorage,
                        out candidateRedStateStorage);

                    entryTrialCost = this.state.UnbiasedInterTrialCost;
                }

                singleReferenceSimpleCosts[filterCostIndex] = candidateStatistics.Cost;

                // The block leaves this entry with the filters of its best motion mode. When no motion mode succeeds, it keeps the filters of
                // its last motion mode trial. A warped trial holds the regular filter.
                long motionModeCost = candidateStatistics.Cost;

                // A strict replacement keeps the nearest, new, near, then global mode order on equal RD cost.
                if (candidateStatistics.Cost < search.Winner.Statistics.Cost)
                {
                    // The winner moves by a swap of the two buffers of each plane, not by a copy of samples. The loser becomes the buffer that
                    // the next candidate writes into, so no block of reconstruction or coefficients is ever moved.
                    Span<byte> displacedLumaReconstruction = selectedLumaReconstruction;
                    selectedLumaReconstruction = candidateLumaReconstruction;
                    candidateLumaReconstruction = displacedLumaReconstruction;

                    Span<byte> displacedBlueReconstruction = selectedBlueReconstruction;
                    selectedBlueReconstruction = candidateBlueReconstruction;
                    candidateBlueReconstruction = displacedBlueReconstruction;

                    Span<byte> displacedRedReconstruction = selectedRedReconstruction;
                    selectedRedReconstruction = candidateRedReconstruction;
                    candidateRedReconstruction = displacedRedReconstruction;

                    Span<int> displacedLumaCoefficients = selectedLumaCoefficients;
                    selectedLumaCoefficients = candidateLumaCoefficients;
                    candidateLumaCoefficients = displacedLumaCoefficients;

                    Span<int> displacedBlueCoefficients = selectedBlueCoefficients;
                    selectedBlueCoefficients = candidateBlueCoefficients;
                    candidateBlueCoefficients = displacedBlueCoefficients;

                    Span<int> displacedRedCoefficients = selectedRedCoefficients;
                    selectedRedCoefficients = candidateRedCoefficients;
                    candidateRedCoefficients = displacedRedCoefficients;

                    search.Winner.SelectInter(
                        candidateStatistics,
                        candidateVectors[candidateIndex],
                        requestedMode,
                        horizontalFilter,
                        verticalFilter,
                        candidateReferenceIndices[candidateIndex],
                        candidateSkip,
                        Av1MotionMode.SimpleTranslation,
                        in candidateLumaStatesStorage,
                        in candidateLumaSizesStorage,
                        in candidateBlueStateStorage,
                        in candidateRedStateStorage);

                    // The first winner of a new-motion entry keeps the earlier winner in the spare buffers until the final vector test of the
                    // entry.
                    if (entryPending && !winnerHeld)
                    {
                        Span<byte> spareLumaReconstructionBuffer = spareLumaReconstruction;
                        spareLumaReconstruction = candidateLumaReconstruction;
                        candidateLumaReconstruction = spareLumaReconstructionBuffer;
                        Span<byte> spareBlueReconstructionBuffer = spareBlueReconstruction;
                        spareBlueReconstruction = candidateBlueReconstruction;
                        candidateBlueReconstruction = spareBlueReconstructionBuffer;
                        Span<byte> spareRedReconstructionBuffer = spareRedReconstruction;
                        spareRedReconstruction = candidateRedReconstruction;
                        candidateRedReconstruction = spareRedReconstructionBuffer;
                        Span<int> spareLumaCoefficientsBuffer = spareLumaCoefficients;
                        spareLumaCoefficients = candidateLumaCoefficients;
                        candidateLumaCoefficients = spareLumaCoefficientsBuffer;
                        Span<int> spareBlueCoefficientsBuffer = spareBlueCoefficients;
                        spareBlueCoefficients = candidateBlueCoefficients;
                        candidateBlueCoefficients = spareBlueCoefficientsBuffer;
                        Span<int> spareRedCoefficientsBuffer = spareRedCoefficients;
                        spareRedCoefficients = candidateRedCoefficients;
                        candidateRedCoefficients = spareRedCoefficientsBuffer;
                        winnerHeld = true;
                    }
                }

                // When the motion modes are not deferred to the winners, each candidate searches OBMC and warped motion after simple
                // translation. A reference kept only because a compound pair uses it searches no other motion mode. A simple translation whose
                // luma search failed ends the candidate.
                if (searchMotionModes && !simpleSkipped && this.state.LumaSearchFailed)
                {
                    continue;
                }

                if (searchMotionModes)
                {
                    Av1MotionMode lastAllowed = Av1EncoderMotionVariation.GetLastAllowedMotionMode(
                        this.state.Picture, macroBlock, modeInfoPosition, modeInfo.Block);

                    int baseRate = commonPredictionRate +
                        (interIntraEligible ? Av1SymbolEncoder.GetInterIntraCost(tables.ModeCosts, blockSize, false, default, false, 0) : 0);

                    for (Av1MotionMode motionMode = Av1MotionMode.Obmc; motionMode <= lastAllowed; motionMode++)
                    {
                        Av1EncoderBlockModeInfo motionModeInfo = modeInfo.Block;
                        Av1MotionVector motionVector = candidateVectors[candidateIndex];
                        Av1RateDistortionStatistics motionStatistics = this.EvaluateMotionMode(
                            writer,
                            in tables,
                            transformCoefficients,
                            dequantizedCoefficients,
                            searchDequantizedCoefficients,
                            transformWorkspace,
                            transformTypeProbabilities,
                            in interWorkspace,
                            motionSearchPrediction,
                            in motionVectorCosts,
                            transformPrediction,
                            firstIntermediate,
                            secondIntermediate,
                            compoundMask,
                            in transformEdges,
                            in lumaCoefficientEdges,
                            in blueCoefficientEdges,
                            in redCoefficientEdges,
                            modeInfoGrid,
                            modeInfoAllocation,
                            displacementVectors,
                            workspaceStorage,
                            sourceLuma,
                            sourceBlue,
                            sourceRed,
                            reconstructionLuma,
                            reconstructionBlue,
                            reconstructionRed,
                            macroBlock,
                            blockOrigin,
                            motionMode,
                            lastAllowed,
                            baseRate,
                            this.state.EstimateInterCandidates ? candidateLimit : Math.Min(Math.Min(bestCost, search.Winner.Statistics.Cost), entryTrialCost),
                            block.HasChroma,
                            candidateReferenceIndices[candidateIndex],
                            referenceMotionVectors,
                            ref motionModeInfo,
                            ref motionVector,
                            candidateLumaReconstruction,
                            candidateLumaCoefficients,
                            candidateBlueReconstruction,
                            candidateBlueCoefficients,
                            candidateRedReconstruction,
                            candidateRedCoefficients,
                            out bool motionSkip,
                            out InlineArray64<Av1EncoderTransformBlockState> motionLumaStates,
                            out InlineArray16<Av1TransformSize> motionLumaSizes,
                            out InlineArray16<Av1EncoderTransformBlockState> motionBlueState,
                            out InlineArray16<Av1EncoderTransformBlockState> motionRedState);

                        entryTrialCost = Math.Min(entryTrialCost, this.state.UnbiasedInterTrialCost);
                        lastTrialVector = motionVector;
                        lastTrialHorizontalFilter = motionModeInfo.HorizontalInterpolationFilter;
                        lastTrialVerticalFilter = motionModeInfo.VerticalInterpolationFilter;
                        if (motionModeCost == long.MaxValue || motionStatistics.Cost < motionModeCost)
                        {
                            motionModeCost = motionStatistics.Cost;
                            heldHorizontalFilter = motionModeInfo.HorizontalInterpolationFilter;
                            heldVerticalFilter = motionModeInfo.VerticalInterpolationFilter;
                        }

                        if (motionStatistics.Cost >= search.Winner.Statistics.Cost)
                        {
                            continue;
                        }

                        Span<byte> displacedLumaReconstruction = selectedLumaReconstruction;
                        selectedLumaReconstruction = candidateLumaReconstruction;
                        candidateLumaReconstruction = displacedLumaReconstruction;
                        Span<byte> displacedBlueReconstruction = selectedBlueReconstruction;
                        selectedBlueReconstruction = candidateBlueReconstruction;
                        candidateBlueReconstruction = displacedBlueReconstruction;
                        Span<byte> displacedRedReconstruction = selectedRedReconstruction;
                        selectedRedReconstruction = candidateRedReconstruction;
                        candidateRedReconstruction = displacedRedReconstruction;
                        Span<int> displacedLumaCoefficients = selectedLumaCoefficients;
                        selectedLumaCoefficients = candidateLumaCoefficients;
                        candidateLumaCoefficients = displacedLumaCoefficients;
                        Span<int> displacedBlueCoefficients = selectedBlueCoefficients;
                        selectedBlueCoefficients = candidateBlueCoefficients;
                        candidateBlueCoefficients = displacedBlueCoefficients;
                        Span<int> displacedRedCoefficients = selectedRedCoefficients;
                        selectedRedCoefficients = candidateRedCoefficients;
                        candidateRedCoefficients = displacedRedCoefficients;
                        search.Winner.SelectInter(
                            motionStatistics,
                            motionVector,
                            requestedMode,
                            motionModeInfo.HorizontalInterpolationFilter,
                            motionModeInfo.VerticalInterpolationFilter,
                            candidateReferenceIndices[candidateIndex],
                            motionSkip,
                            motionMode,
                            in motionLumaStates,
                            in motionLumaSizes,
                            in motionBlueState,
                            in motionRedState);

                        // The first winner of a new-motion entry keeps the earlier winner in the spare buffers until the entry's
                        // final vector test.
                        if (entryPending && !winnerHeld)
                        {
                            Span<byte> spareLumaReconstructionBuffer = spareLumaReconstruction;
                            spareLumaReconstruction = candidateLumaReconstruction;
                            candidateLumaReconstruction = spareLumaReconstructionBuffer;
                            Span<byte> spareBlueReconstructionBuffer = spareBlueReconstruction;
                            spareBlueReconstruction = candidateBlueReconstruction;
                            candidateBlueReconstruction = spareBlueReconstructionBuffer;
                            Span<byte> spareRedReconstructionBuffer = spareRedReconstruction;
                            spareRedReconstruction = candidateRedReconstruction;
                            candidateRedReconstruction = spareRedReconstructionBuffer;
                            Span<int> spareLumaCoefficientsBuffer = spareLumaCoefficients;
                            spareLumaCoefficients = candidateLumaCoefficients;
                            candidateLumaCoefficients = spareLumaCoefficientsBuffer;
                            Span<int> spareBlueCoefficientsBuffer = spareBlueCoefficients;
                            spareBlueCoefficients = candidateBlueCoefficients;
                            candidateBlueCoefficients = spareBlueCoefficientsBuffer;
                            Span<int> spareRedCoefficientsBuffer = spareRedCoefficients;
                            spareRedCoefficients = candidateRedCoefficients;
                            candidateRedCoefficients = spareRedCoefficientsBuffer;
                            winnerHeld = true;
                        }
                    }
                }

                if (!interIntraEligible || !searchMotionModes)
                {
                    continue;
                }

                int interIntraMotionRate = 0;
                if (requestedMode == Av1PredictionMode.NewMotionVector)
                {
                    Av1MotionVector referenceVector = referenceMotionVectors.GetNewReference(candidateReferenceIndices[candidateIndex]);
                    interIntraMotionRate = ((motionVectorCosts.GetCost(candidateVectors[candidateIndex], referenceVector) * 108) + 64) >> 7;
                }

                // The inter-intra trial keeps the interpolation filters.
                if (motionModeCost == long.MaxValue)
                {
                    heldHorizontalFilter = horizontalFilter;
                    heldVerticalFilter = verticalFilter;
                }

                lastTrialVector = candidateVectors[candidateIndex];
                lastTrialHorizontalFilter = horizontalFilter;
                lastTrialVerticalFilter = verticalFilter;

                Av1MotionVector interIntraVector = candidateVectors[candidateIndex];
                if (!this.SelectInterIntraBlend(
                    writer,
                    in tables,
                    transformCoefficients,
                    dequantizedCoefficients,
                    transformWorkspace,
                    in interWorkspace,
                    in motionVectorCosts,
                    transformPrediction,
                    interIntraAbove,
                    interIntraLeft,
                    compoundMask,
                    in transformEdges,
                    in lumaCoefficientEdges,
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
                    blockOrigin,
                    blockSize,
                    requestedMode,
                    referenceFrame,
                    ref interIntraVector,
                    referenceMotionVectors.GetNewReference(candidateReferenceIndices[candidateIndex]),
                    requestedMode == Av1PredictionMode.NewMotionVector,
                    horizontalFilter,
                    verticalFilter,
                    interIntraMotionRate,
                    this.state.EstimateInterCandidates ? candidateLimit : Math.Min(Math.Min(bestCost, search.Winner.Statistics.Cost), entryTrialCost),
                    ref cachedInterIntraMode,
                    out Av1InterIntraMode interIntraMode,
                    out bool useWedge,
                    out int wedgeIndex))
                {
                    continue;
                }

                // A new vector that the blend search leaves at its reference codes no difference, so the trial is skipped.
                lastTrialVector = interIntraVector;
                if (newEntry && interIntraVector == newReference)
                {
                    continue;
                }

                this.PrepareInterIntraPrediction(
                    transformWorkspace,
                    in interWorkspace,
                    transformPrediction,
                    interIntraAbove,
                    interIntraLeft,
                    compoundMask,
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
                    block.HasChroma,
                    requestedMode,
                    referenceFrame,
                    interIntraVector,
                    horizontalFilter,
                    verticalFilter,
                    interIntraMode,
                    useWedge,
                    wedgeIndex);

                int interIntraRate = Av1SymbolEncoder.GetInterIntraCost(tables.ModeCosts, blockSize, true, interIntraMode, useWedge, wedgeIndex);

                this.state.TransformSearchReset = true;
                Av1RateDistortionStatistics interIntraStatistics = this.EvaluateInterCandidate(
                    writer,
                    in tables,
                    transformCoefficients,
                    dequantizedCoefficients,
                    searchDequantizedCoefficients,
                    transformWorkspace,
                    transformTypeProbabilities,
                    in interWorkspace,
                    in motionVectorCosts,
                    transformPrediction,
                    firstIntermediate,
                    secondIntermediate,
                    compoundMask,
                    in transformEdges,
                    in lumaCoefficientEdges,
                    in blueCoefficientEdges,
                    in redCoefficientEdges,
                    modeInfoGrid,
                    modeInfoAllocation,
                    displacementVectors,
                    workspaceStorage,
                    sourceLuma,
                    sourceBlue,
                    sourceRed,
                    reconstructionLuma,
                    reconstructionBlue,
                    reconstructionRed,
                    macroBlock,
                    blockOrigin,
                    blockSize,
                    this.state.EstimateInterCandidates ? candidateLimit : Math.Min(Math.Min(bestCost, search.Winner.Statistics.Cost), entryTrialCost),
                    block.HasChroma,
                    commonPredictionRate + filterRate + interIntraRate,
                    referenceFrame,
                    interIntraVector,
                    default,
                    requestedMode,
                    Av1ReferenceFrameType.None,
                    usePreparedPrediction: true,
                    Av1CompoundType.Average,
                    0,
                    false,
                    Av1DifferenceWeightedMaskType.Type38,
                    0,
                    horizontalFilter,
                    verticalFilter,
                    candidateReferenceIndices[candidateIndex],
                    true,
                    interIntraMode,
                    useWedge,
                    wedgeIndex,
                    referenceMotionVectors,
                    candidateLumaReconstruction,
                    candidateLumaCoefficients,
                    candidateBlueReconstruction,
                    candidateBlueCoefficients,
                    candidateRedReconstruction,
                    candidateRedCoefficients,
                    out bool interIntraSkip,
                    out InlineArray64<Av1EncoderTransformBlockState> interIntraLumaStates,
                    out InlineArray16<Av1TransformSize> interIntraLumaSizes,
                    out InlineArray16<Av1EncoderTransformBlockState> interIntraBlueState,
                    out InlineArray16<Av1EncoderTransformBlockState> interIntraRedState);

                if (interIntraStatistics.Cost < motionModeCost)
                {
                    heldHorizontalFilter = horizontalFilter;
                    heldVerticalFilter = verticalFilter;
                }

                if (interIntraStatistics.Cost >= search.Winner.Statistics.Cost)
                {
                    continue;
                }

                Span<byte> previousLumaReconstruction = selectedLumaReconstruction;
                selectedLumaReconstruction = candidateLumaReconstruction;
                candidateLumaReconstruction = previousLumaReconstruction;
                Span<byte> previousBlueReconstruction = selectedBlueReconstruction;
                selectedBlueReconstruction = candidateBlueReconstruction;
                candidateBlueReconstruction = previousBlueReconstruction;
                Span<byte> previousRedReconstruction = selectedRedReconstruction;
                selectedRedReconstruction = candidateRedReconstruction;
                candidateRedReconstruction = previousRedReconstruction;
                Span<int> previousLumaCoefficients = selectedLumaCoefficients;
                selectedLumaCoefficients = candidateLumaCoefficients;
                candidateLumaCoefficients = previousLumaCoefficients;
                Span<int> previousBlueCoefficients = selectedBlueCoefficients;
                selectedBlueCoefficients = candidateBlueCoefficients;
                candidateBlueCoefficients = previousBlueCoefficients;
                Span<int> previousRedCoefficients = selectedRedCoefficients;
                selectedRedCoefficients = candidateRedCoefficients;
                candidateRedCoefficients = previousRedCoefficients;
                search.Winner.SelectInterIntra(
                    interIntraStatistics,
                    interIntraVector,
                    requestedMode,
                    horizontalFilter,
                    verticalFilter,
                    candidateReferenceIndices[candidateIndex],
                    interIntraSkip,
                    interIntraMode,
                    useWedge,
                    wedgeIndex,
                    in interIntraLumaStates,
                    in interIntraLumaSizes,
                    in interIntraBlueState,
                    in interIntraRedState);

                // The first winner of a new-motion entry keeps the earlier winner in the spare buffers until the entry's
                // final vector test.
                if (entryPending && !winnerHeld)
                {
                    Span<byte> spareLumaReconstructionBuffer = spareLumaReconstruction;
                    spareLumaReconstruction = candidateLumaReconstruction;
                    candidateLumaReconstruction = spareLumaReconstructionBuffer;
                    Span<byte> spareBlueReconstructionBuffer = spareBlueReconstruction;
                    spareBlueReconstruction = candidateBlueReconstruction;
                    candidateBlueReconstruction = spareBlueReconstructionBuffer;
                    Span<byte> spareRedReconstructionBuffer = spareRedReconstruction;
                    spareRedReconstruction = candidateRedReconstruction;
                    candidateRedReconstruction = spareRedReconstructionBuffer;
                    Span<int> spareLumaCoefficientsBuffer = spareLumaCoefficients;
                    spareLumaCoefficients = candidateLumaCoefficients;
                    candidateLumaCoefficients = spareLumaCoefficientsBuffer;
                    Span<int> spareBlueCoefficientsBuffer = spareBlueCoefficients;
                    spareBlueCoefficients = candidateBlueCoefficients;
                    candidateBlueCoefficients = spareBlueCoefficientsBuffer;
                    Span<int> spareRedCoefficientsBuffer = spareRedCoefficients;
                    spareRedCoefficients = candidateRedCoefficients;
                    candidateRedCoefficients = spareRedCoefficientsBuffer;
                    winnerHeld = true;
                }
            }

            search.Winner.WriteDecisions(ref modeInfo, ref block, selectedStates);
            selectedVector = search.Winner.Vector;
            return search.Winner.Statistics;
        }
    }
}

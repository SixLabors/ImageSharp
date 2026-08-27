// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;

/// <summary>
/// Derives AV1 entropy contexts and syntax mappings shared by symbol readers and writers.
/// </summary>
internal static class Av1SymbolContextHelper
{
    /// <summary>
    /// The number of transform types represented by each flattened transform-set row.
    /// </summary>
    private const int TransformTypeCount = 16;

    /// <summary>
    /// The number of AV1 transform sets.
    /// </summary>
    private const int TransformSetCount = 6;

    /// <summary>
    /// Gets the mapping from each transform set and transform type to its coded symbol index.
    /// </summary>
    private static ReadOnlySpan<byte> ExtendedTransformIndices =>
    [
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // DCT only
        1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // Inter set 3
        1, 3, 4, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // Intra set 2
        1, 5, 6, 4, 0, 0, 0, 0, 0, 0, 2, 3, 0, 0, 0, 0, // Intra set 1
        3, 4, 5, 8, 6, 7, 9, 10, 11, 0, 1, 2, 0, 0, 0, 0, // Inter set 2
        7, 8, 9, 12, 10, 11, 13, 14, 15, 0, 1, 2, 3, 4, 5, 6 // All 16, inter set 1
    ];

    /// <summary>
    /// Gets the mapping from transform-set types to their intra and inter transform-type distribution indices.
    /// </summary>
    private static ReadOnlySpan<sbyte> ExtendedTransformSetToIndex =>
    [
        0, -1, 2, 1, -1, -1,
        0, 3, -1, -1, 2, 1
    ];

    /// <summary>
    /// Gets the mapping from coded transform-type symbols to transform types for each transform set.
    /// </summary>
    private static ReadOnlySpan<Av1TransformType> ExtendedTransformTypes =>
    [

        // DCT only. Unused positions retain DCT-DCT so each set occupies one fixed 16-entry row.
        Av1TransformType.DctDct, Av1TransformType.DctDct, Av1TransformType.DctDct, Av1TransformType.DctDct,
        Av1TransformType.DctDct, Av1TransformType.DctDct, Av1TransformType.DctDct, Av1TransformType.DctDct,
        Av1TransformType.DctDct, Av1TransformType.DctDct, Av1TransformType.DctDct, Av1TransformType.DctDct,
        Av1TransformType.DctDct, Av1TransformType.DctDct, Av1TransformType.DctDct, Av1TransformType.DctDct,

        // Inter set 3.
        Av1TransformType.Identity, Av1TransformType.DctDct, Av1TransformType.DctDct, Av1TransformType.DctDct,
        Av1TransformType.DctDct, Av1TransformType.DctDct, Av1TransformType.DctDct, Av1TransformType.DctDct,
        Av1TransformType.DctDct, Av1TransformType.DctDct, Av1TransformType.DctDct, Av1TransformType.DctDct,
        Av1TransformType.DctDct, Av1TransformType.DctDct, Av1TransformType.DctDct, Av1TransformType.DctDct,

        // Intra set 2.
        Av1TransformType.Identity, Av1TransformType.DctDct, Av1TransformType.AdstAdst, Av1TransformType.AdstDct,
        Av1TransformType.DctAdst, Av1TransformType.DctDct, Av1TransformType.DctDct, Av1TransformType.DctDct,
        Av1TransformType.DctDct, Av1TransformType.DctDct, Av1TransformType.DctDct, Av1TransformType.DctDct,
        Av1TransformType.DctDct, Av1TransformType.DctDct, Av1TransformType.DctDct, Av1TransformType.DctDct,

        // Intra set 1.
        Av1TransformType.Identity, Av1TransformType.DctDct, Av1TransformType.VerticalDct, Av1TransformType.HorizontalDct,
        Av1TransformType.AdstAdst, Av1TransformType.AdstDct, Av1TransformType.DctAdst, Av1TransformType.DctDct,
        Av1TransformType.DctDct, Av1TransformType.DctDct, Av1TransformType.DctDct, Av1TransformType.DctDct,
        Av1TransformType.DctDct, Av1TransformType.DctDct, Av1TransformType.DctDct, Av1TransformType.DctDct,

        // Inter set 2.
        Av1TransformType.Identity, Av1TransformType.VerticalDct, Av1TransformType.HorizontalDct, Av1TransformType.DctDct,
        Av1TransformType.AdstDct, Av1TransformType.DctAdst, Av1TransformType.FlipAdstDct, Av1TransformType.DctFlipAdst,
        Av1TransformType.AdstAdst, Av1TransformType.FlipAdstFlipAdst, Av1TransformType.AdstFlipAdst, Av1TransformType.FlipAdstAdst,
        Av1TransformType.DctDct, Av1TransformType.DctDct, Av1TransformType.DctDct, Av1TransformType.DctDct,

        // All 16, inter set 1.
        Av1TransformType.Identity, Av1TransformType.VerticalDct, Av1TransformType.HorizontalDct, Av1TransformType.VerticalAdst,
        Av1TransformType.HorizontalAdst, Av1TransformType.VerticalFlipAdst, Av1TransformType.HorizontalFlipAdst, Av1TransformType.DctDct,
        Av1TransformType.AdstDct, Av1TransformType.DctAdst, Av1TransformType.FlipAdstDct, Av1TransformType.DctFlipAdst,
        Av1TransformType.AdstAdst, Av1TransformType.FlipAdstFlipAdst, Av1TransformType.AdstFlipAdst, Av1TransformType.FlipAdstAdst
    ];

    /// <summary>
    /// Gets the number of coded symbols in each transform set.
    /// </summary>
    private static ReadOnlySpan<byte> ExtendedTransformTypeCounts =>
    [
        1, 2, 5, 7, 12, 16
    ];

    /// <summary>
    /// Gets the number of extra offset bits associated with each end-of-block token.
    /// </summary>
    public static ReadOnlySpan<int> EndOfBlockOffsetBits => [0, 0, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9];

    /// <summary>
    /// Gets the first coefficient position represented by each end-of-block token.
    /// </summary>
    public static ReadOnlySpan<int> EndOfBlockGroupStart => [0, 1, 2, 3, 5, 9, 17, 33, 65, 129, 257, 513];

    /// <summary>
    /// Gets the mapping from end-of-block positions below 33 directly to their token.
    /// </summary>
    private static ReadOnlySpan<byte> EndOfBlockToPositionSmall =>
    [
        0, 1, 2, // 0-2
        3, 3, // 3-4
        4, 4, 4, 4, // 5-8
        5, 5, 5, 5, 5, 5, 5, 5, // 9-16
        6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6 // 17-32
    ];

    /// <summary>
    /// Gets the mapping from groups of 32 larger end-of-block positions to their token.
    /// </summary>
    private static ReadOnlySpan<byte> EndOfBlockToPositionLarge =>
    [
        6, // place holder
        7, // 33-64
        8,
        8, // 65-128
        9,
        9,
        9,
        9, // 129-256
        10,
        10,
        10,
        10,
        10,
        10,
        10,
        10, // 257-512
        11 // 513-
    ];

    /// <summary>
    /// Gets the mapping from clipped top and left coefficient-level classes to the transform-block skip context.
    /// </summary>
    private static ReadOnlySpan<byte> TransformBlockSkipContexts =>
    [
        1, 2, 2, 2, 3,
        2, 4, 4, 4, 5,
        2, 4, 4, 4, 5,
        2, 4, 4, 4, 5,
        3, 5, 5, 5, 6
    ];

    /// <summary>
    /// Reduces a rectangular transform size to the square context used by transform-size distributions.
    /// </summary>
    /// <param name="originalSize">The coded transform size.</param>
    /// <returns>The square transform-size context.</returns>
    internal static Av1TransformSize GetTransformSizeContext(Av1TransformSize originalSize)
        => (Av1TransformSize)(((int)originalSize.GetSquareSize() + (int)originalSize.GetSquareUpSize() + 1) >> 1);

    /// <summary>
    /// Derives the luma transform-block skip context from the neighboring coefficient levels.
    /// </summary>
    /// <param name="top">The union of the packed coefficient contexts above the transform.</param>
    /// <param name="left">The union of the packed coefficient contexts to the left of the transform.</param>
    /// <returns>The transform-block skip context.</returns>
    public static int GetTransformBlockSkipContext(int top, int left)
    {
        int topClass = Math.Min(top, 4);
        int leftClass = Math.Min(left, 4);

        // AV1 groups each edge into zero, low, or high coefficient-level classes. Retaining libaom's complete table
        // lets the reader and writer share one compile-time mapping without an encoder-side jagged-array allocation.
        return TransformBlockSkipContexts[(topClass * 5) + leftClass];
    }

    /// <summary>
    /// Reconstructs an end-of-block coefficient position from its token and extra offset.
    /// </summary>
    /// <param name="endOfBlockPoint">The decoded end-of-block token.</param>
    /// <param name="endOfBlockExtra">The decoded offset within the token group.</param>
    /// <returns>The one-based end-of-block coefficient position.</returns>
    internal static int RecordEndOfBlockPosition(int endOfBlockPoint, int endOfBlockExtra)
    {
        int endOfBlock = EndOfBlockGroupStart[endOfBlockPoint];
        if (endOfBlock > 2)
        {
            endOfBlock += endOfBlockExtra;
        }

        return endOfBlock;
    }

    /// <summary>
    /// Derives the lower-level context for the final nonzero coefficient from an index expressed as a two-dimensional coordinate.
    /// </summary>
    /// <param name="levels">The padded coefficient-level buffer.</param>
    /// <param name="position">The coordinate whose row-major index identifies the coefficient's scan position.</param>
    /// <returns>The end-of-block lower-level context.</returns>
    internal static int GetLowerLevelContextEndOfBlock(Av1LevelBuffer levels, Point position)
        => GetLowerLevelContextEndOfBlock(levels, position.X + (position.Y * levels.Size.Width));

    /// <summary>
    /// Derives the lower-level context for the final nonzero coefficient from its scan-order index.
    /// </summary>
    /// <param name="levels">The padded coefficient-level buffer.</param>
    /// <param name="scanIndex">The zero-based coefficient index in scan order.</param>
    /// <returns>The end-of-block lower-level context.</returns>
    internal static int GetLowerLevelContextEndOfBlock(Av1LevelBuffer levels, int scanIndex)
    {
        if (scanIndex == 0)
        {
            return 0;
        }

        int total = levels.Size.Height * levels.Size.Width;
        if (scanIndex <= total >> 3)
        {
            return 1;
        }

        if (scanIndex <= total >> 2)
        {
            return 2;
        }

        return 3;
    }

    /// <summary>
    /// Derives a two-dimensional lower-level context from five forward coefficient neighbors.
    /// </summary>
    /// <param name="levelBuffer">The padded coefficient-level buffer.</param>
    /// <param name="position">The coefficient position in raster order.</param>
    /// <param name="transformSize">The transform size selecting the positional context offset.</param>
    /// <returns>The lower-level context.</returns>
    internal static int GetLowerLevelsContext2d(Av1LevelBuffer levelBuffer, Point position, Av1TransformSize transformSize)
    {
        DebugGuard.MustBeGreaterThan(position.X + position.Y, 0, nameof(position));
        int mag;
        Span<byte> row0 = levelBuffer.GetRow(position.Y)[position.X..];
        Span<byte> row1 = levelBuffer.GetRow(position.Y + 1)[position.X..];
        Span<byte> row2 = levelBuffer.GetRow(position.Y + 2)[position.X..];
        mag = Math.Min((int)row0[1], 3); // { 0, 1 }
        mag += Math.Min((int)row1[0], 3); // { 1, 0 }
        mag += Math.Min((int)row1[1], 3); // { 1, 1 }
        mag += Math.Min((int)row0[2], 3); // { 0, 2 }
        mag += Math.Min((int)row2[0], 3); // { 2, 0 }

        int ctx = Math.Min((mag + 1) >> 1, 4);
        return ctx + Av1NzMap.GetNzMapContext(transformSize, position);
    }

    /// <summary>
    /// Section 8.3.2 in the spec, under coeff_br. Optimized for end of block based
    /// on the fact that {0, 1}, {1, 0}, {1, 1}, {0, 2} and {2, 0} will all be 0 in
    /// the end of block case.
    /// </summary>
    /// <param name="pos">The final nonzero coefficient position.</param>
    /// <param name="transformClass">The transform direction class.</param>
    /// <returns>The base-range context.</returns>
    internal static int GetBaseRangeContextEndOfBlock(Point pos, Av1TransformClass transformClass)
    {
        if (pos.X == 0 && pos.Y == 0)
        {
            return 0;
        }

        if ((transformClass == Av1TransformClass.Class2D && pos.Y < 2 && pos.X < 2) ||
            (transformClass == Av1TransformClass.ClassHorizontal && pos.X == 0) ||
            (transformClass == Av1TransformClass.ClassVertical && pos.Y == 0))
        {
            return 7;
        }

        return 14;
    }

    /// <summary>
    /// Derives a base-range context from the transform-class-specific forward neighbors.
    /// </summary>
    /// <remarks>Spec section 8.2.3, under 'coeff_br'.</remarks>
    /// <param name="levels">The padded coefficient-level buffer.</param>
    /// <param name="position">The coefficient position in raster order.</param>
    /// <param name="transformClass">The transform direction class.</param>
    /// <returns>The base-range context.</returns>
    internal static int GetBaseRangeContext(Av1LevelBuffer levels, Point position, Av1TransformClass transformClass)
    {
        Span<byte> row0 = levels.GetRow(position.Y);
        Span<byte> row1 = levels.GetRow(position.Y + 1);
        int mag = row0[position.X + 1];
        mag += row1[position.X];
        switch (transformClass)
        {
            case Av1TransformClass.Class2D:
                mag += row1[position.X + 1];
                mag = Math.Min((mag + 1) >> 1, 6);
                if ((position.X + position.Y) == 0)
                {
                    return mag;
                }

                if (position.Y < 2 && position.X < 2)
                {
                    return mag + 7;
                }

                break;
            case Av1TransformClass.ClassHorizontal:
                mag += row0[position.X + 2];
                mag = Math.Min((mag + 1) >> 1, 6);
                if ((position.X + position.Y) == 0)
                {
                    return mag;
                }

                if (position.X == 0)
                {
                    return mag + 7;
                }

                break;
            case Av1TransformClass.ClassVertical:
                mag += levels.GetRow(position.Y + 2)[position.X];
                mag = Math.Min((mag + 1) >> 1, 6);
                if ((position.X + position.Y) == 0)
                {
                    return mag;
                }

                if (position.Y == 0)
                {
                    return mag + 7;
                }

                break;
            default:
                break;
        }

        return mag + 14;
    }

    /// <summary>
    /// Derives the two-dimensional base-range context from right, below, and below-right levels.
    /// </summary>
    /// <param name="levels">The padded coefficient-level buffer.</param>
    /// <param name="position">The coefficient position in raster order.</param>
    /// <returns>The two-dimensional base-range context.</returns>
    internal static int GetBaseRangeContext2d(Av1LevelBuffer levels, Point position)
    {
        DebugGuard.MustBeGreaterThan(position.X + position.Y, 0, nameof(position));
        Span<byte> row0 = levels.GetRow(position.Y);
        Span<byte> row1 = levels.GetRow(position.Y + 1);

        // The final magnitude context is clipped to six, so clipping every source level to the AV1 base-range limit
        // first cannot change the result.
        int mag =
            row0[position.X + 1] + // {0, 1}
            row1[position.X] + //     {1, 0}
            row1[position.X + 1];  // {1, 1}
        mag = Math.Min((mag + 1) >> 1, 6);
        if ((position.Y | position.X) < 2)
        {
            return mag + 7;
        }

        return mag + 14;
    }

    /// <summary>
    /// Derives a lower-level context from the transform-class-specific nonzero-map magnitude.
    /// </summary>
    /// <param name="levels">The padded coefficient-level buffer.</param>
    /// <param name="position">The coefficient position in raster order.</param>
    /// <param name="transformSize">The coded transform size.</param>
    /// <param name="transformClass">The transform direction class.</param>
    /// <returns>The lower-level coefficient context.</returns>
    internal static int GetLowerLevelsContext(Av1LevelBuffer levels, Point position, Av1TransformSize transformSize, Av1TransformClass transformClass)
    {
        int stats = Av1NzMap.GetNzMagnitude(levels, position, transformClass);
        return Av1NzMap.GetNzMapContextFromStats(stats, position, transformSize, transformClass);
    }

    /// <summary>
    /// Selects the intra transform set permitted for a transform size and reduced-set flag.
    /// </summary>
    /// <param name="transformSize">The coded transform size.</param>
    /// <param name="useReducedSet">Indicates whether the frame restricts transform choices.</param>
    /// <returns>The permitted transform set.</returns>
    internal static Av1TransformSetType GetExtendedTransformSetType(Av1TransformSize transformSize, bool useReducedSet)
        => GetExtendedTransformSetType(transformSize, false, useReducedSet);

    /// <summary>
    /// Selects the transform set permitted for a transform size, prediction class, and reduced-set flag.
    /// </summary>
    /// <param name="transformSize">The coded transform size.</param>
    /// <param name="isInter">Indicates whether the block uses inter prediction.</param>
    /// <param name="useReducedSet">Indicates whether the frame restricts transform choices.</param>
    /// <returns>The permitted transform set.</returns>
    internal static Av1TransformSetType GetExtendedTransformSetType(Av1TransformSize transformSize, bool isInter, bool useReducedSet)
    {
        Av1TransformSize squareUpSize = transformSize.GetSquareUpSize();

        if (squareUpSize > Av1TransformSize.Size32x32)
        {
            return Av1TransformSetType.DctOnly;
        }

        if (squareUpSize == Av1TransformSize.Size32x32)
        {
            return isInter ? Av1TransformSetType.InterSet3 : Av1TransformSetType.DctOnly;
        }

        if (useReducedSet)
        {
            return isInter ? Av1TransformSetType.InterSet3 : Av1TransformSetType.IntraSet2;
        }

        Av1TransformSize squareSize = transformSize.GetSquareSize();
        if (isInter)
        {
            return squareSize == Av1TransformSize.Size16x16 ? Av1TransformSetType.InterSet2 : Av1TransformSetType.InterSet1;
        }

        return squareSize == Av1TransformSize.Size16x16
            ? Av1TransformSetType.IntraSet2
            : Av1TransformSetType.IntraSet1;
    }

    /// <summary>
    /// Maps an intra prediction mode to its default transform type.
    /// </summary>
    /// <param name="modeInfo">The block prediction modes.</param>
    /// <param name="planeType">The luma or chroma plane category.</param>
    /// <returns>The transform type associated with the selected prediction mode.</returns>
    internal static Av1TransformType ConvertIntraModeToTransformType(Av1BlockModeInfo modeInfo, Av1PlaneType planeType)
    {
        // libaom's get_uv_mode() is the explicit boundary between the distinct UV and luma prediction domains. CfL maps
        // to DC because the chroma AC contribution is applied to a DC predictor before coefficient reconstruction.
        Av1PredictionMode mode = planeType == Av1PlaneType.Y ? modeInfo.YMode : modeInfo.UvMode.ToLumaMode();

        return mode.ToTransformType();
    }

    /// <summary>
    /// Derives the nonzero-map context for one coefficient preceding the final nonzero coefficient.
    /// </summary>
    /// <param name="levels">The padded coefficient-level buffer.</param>
    /// <param name="position">The coefficient position in raster order.</param>
    /// <param name="transformSize">The coded transform size.</param>
    /// <param name="transformClass">The transform direction class.</param>
    /// <returns>The nonzero-map context.</returns>
    internal static sbyte GetNzMapContext(
        Av1LevelBuffer levels,
        Point position,
        Av1TransformSize transformSize,
        Av1TransformClass transformClass)
    {
        int stats = Av1NzMap.GetNzMagnitude(levels, position, transformClass);
        return (sbyte)Av1NzMap.GetNzMapContextFromStats(stats, position, transformSize, transformClass);
    }

    /// <summary>
    /// Populates nonzero-map contexts for every coefficient preceding the end-of-block position.
    /// </summary>
    /// <param name="levels">The padded coefficient-level buffer.</param>
    /// <param name="scan">The coefficient scan order.</param>
    /// <param name="eob">The one-based end-of-block position.</param>
    /// <param name="transformSize">The coded transform size.</param>
    /// <param name="transformClass">The transform direction class.</param>
    /// <param name="coefficientContexts">The raster-indexed destination contexts.</param>
    internal static void GetNzMapContexts(
        Av1LevelBuffer levels,
        ReadOnlySpan<short> scan,
        ushort eob,
        Av1TransformSize transformSize,
        Av1TransformClass transformClass,
        Span<sbyte> coefficientContexts)
    {
        for (int i = 0; i < eob; ++i)
        {
            int pos = scan[i];
            Point position = levels.GetPosition(pos);

            // The final coefficient context is based on its scan position, while all preceding contexts use the
            // coefficient's raster position and already-decoded forward neighbors.
            coefficientContexts[pos] = i == eob - 1
                ? (sbyte)GetLowerLevelContextEndOfBlock(levels, i)
                : GetNzMapContext(levels, position, transformSize, transformClass);
        }
    }

    /// <summary>
    /// Gets the coded symbol index for a transform type in a transform set.
    /// </summary>
    /// <param name="setType">The transform set.</param>
    /// <param name="transformType">The transform type.</param>
    /// <returns>The coded symbol index.</returns>
    public static int GetExtendedTransformIndex(Av1TransformSetType setType, Av1TransformType transformType)
        => ExtendedTransformIndices[((int)setType * TransformTypeCount) + (int)transformType];

    /// <summary>
    /// Gets the transform type represented by a coded symbol in a transform set.
    /// </summary>
    /// <param name="setType">The transform set.</param>
    /// <param name="symbol">The coded symbol index.</param>
    /// <returns>The represented transform type.</returns>
    public static Av1TransformType GetExtendedTransformType(Av1TransformSetType setType, int symbol)
        => ExtendedTransformTypes[((int)setType * TransformTypeCount) + symbol];

    /// <summary>
    /// Gets the number of transform types in a transform set.
    /// </summary>
    /// <param name="setType">The transform set.</param>
    /// <returns>The number of permitted transform types.</returns>
    internal static int GetExtendedTransformTypeCount(Av1TransformSetType setType) => ExtendedTransformTypeCounts[(int)setType];

    /// <summary>
    /// Gets the entropy-distribution index for an intra transform set.
    /// </summary>
    /// <param name="setType">The transform set.</param>
    /// <returns>The distribution index, or <c>-1</c> for an inter-only set.</returns>
    internal static int GetExtendedTransformSet(Av1TransformSetType setType)
        => GetExtendedTransformSet(setType, false);

    /// <summary>
    /// Gets the entropy-distribution index for a transform set and prediction class.
    /// </summary>
    /// <param name="setType">The transform set.</param>
    /// <param name="isInter">Indicates whether the block uses inter prediction.</param>
    /// <returns>The distribution index, or <c>-1</c> when the set is unavailable for the prediction class.</returns>
    internal static int GetExtendedTransformSet(Av1TransformSetType setType, bool isInter)
        => ExtendedTransformSetToIndex[((isInter ? 1 : 0) * TransformSetCount) + (int)setType];

    /// <summary>
    /// Packs the sign of the DC coefficient into a cumulative-level context value.
    /// </summary>
    /// <param name="culLevel">The cumulative-level context to update.</param>
    /// <param name="dcValue">The signed DC coefficient.</param>
    internal static void SetDcSign(ref int culLevel, int dcValue)
    {
        if (dcValue < 0)
        {
            culLevel |= 1 << Av1Constants.CoefficientContextBitCount;
        }
        else if (dcValue > 0)
        {
            culLevel += 2 << Av1Constants.CoefficientContextBitCount;
        }
    }

    /// <summary>
    /// Converts a one-based end-of-block position to its token and group offset.
    /// </summary>
    /// <param name="endOfBlock">The one-based end-of-block position.</param>
    /// <param name="extra">Receives the offset within the selected token group.</param>
    /// <returns>The end-of-block token.</returns>
    internal static short GetEndOfBlockPosition(ushort endOfBlock, out int extra)
    {
        short t;
        if (endOfBlock < 33)
        {
            t = EndOfBlockToPositionSmall[endOfBlock];
        }
        else
        {
            int e = Math.Min((endOfBlock - 1) >> 5, 16);
            t = EndOfBlockToPositionLarge[e];
        }

        extra = endOfBlock - EndOfBlockGroupStart[t];
        return t;
    }

    /// <summary>
    /// Gets the decoded segment identifier at one spatial-neighbor position.
    /// </summary>
    /// <param name="segmentIds">The row-major decoded segment map.</param>
    /// <param name="rowIndex">The mode-info row.</param>
    /// <param name="columnIndex">The mode-info column.</param>
    /// <returns>The segment identifier stored at the requested position.</returns>
    public static int GetSegmentId(int[][] segmentIds, int rowIndex, int columnIndex)
        => segmentIds[rowIndex][columnIndex];

    /// <summary>
    /// Gets the minimum encoded segment identifier across a block's clipped mode-info coverage.
    /// </summary>
    /// <param name="encoderCommon">The encoder frame geometry.</param>
    /// <param name="segmentIds">The row-major encoder segment map.</param>
    /// <param name="blockSize">The block size.</param>
    /// <param name="modeInfoPosition">The starting position in mode-info units.</param>
    /// <returns>The minimum segment identifier in the covered region.</returns>
    public static int GetSegmentId(Av1EncoderCommon encoderCommon, ReadOnlySpan<byte> segmentIds, Av1BlockSize blockSize, Point modeInfoPosition)
    {
        int modeInfoOffset = (modeInfoPosition.Y * encoderCommon.ModeInfoColumnCount) + modeInfoPosition.X;
        int blockWidth = blockSize.Get4x4WideCount();
        int blockHeight = blockSize.Get4x4HighCount();
        int columnCount = Math.Min(encoderCommon.ModeInfoColumnCount - modeInfoPosition.X, blockWidth);
        int rowCount = Math.Min(encoderCommon.ModeInfoRowCount - modeInfoPosition.Y, blockHeight);
        int segmentId = Av1Constants.MaxSegmentCount;

        for (int y = 0; y < rowCount; ++y)
        {
            int offset = modeInfoOffset + (y * encoderCommon.ModeInfoColumnCount);
            for (int x = 0; x < columnCount; ++x)
            {
                segmentId = Math.Min(segmentId, segmentIds[offset + x]);
            }
        }

        Guard.IsTrue(segmentId is >= 0 and < Av1Constants.MaxSegmentCount, nameof(segmentId), "Segment ID needs to be in proper range.");
        return segmentId;
    }

    /// <summary>
    /// Reconstructs a segment identifier coded as an alternating distance from its spatial predictor.
    /// </summary>
    /// <param name="diff">The coded nonnegative distance symbol.</param>
    /// <param name="reference">The predicted segment identifier.</param>
    /// <param name="max">The exclusive upper bound of the segment identifier range.</param>
    /// <returns>The reconstructed segment identifier.</returns>
    public static int NegativeDeinterleave(int diff, int reference, int max)
    {
        if (reference == 0)
        {
            return diff;
        }

        if (reference >= max - 1)
        {
            return max - diff - 1;
        }

        if (2 * reference < max)
        {
            if (diff <= 2 * reference)
            {
                if ((diff & 1) > 0)
                {
                    return reference + ((diff + 1) >> 1);
                }
                else
                {
                    return reference - (diff >> 1);
                }
            }

            return diff;
        }
        else
        {
            if (diff <= 2 * (max - reference - 1))
            {
                if ((diff & 1) > 0)
                {
                    return reference + ((diff + 1) >> 1);
                }
                else
                {
                    return reference - (diff >> 1);
                }
            }

            return max - (diff + 1);
        }
    }
}

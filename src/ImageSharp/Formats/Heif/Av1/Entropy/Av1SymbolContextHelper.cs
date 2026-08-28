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
    /// The bit offset of the global-motion decision context in a packed inter-mode context.
    /// </summary>
    private const int GlobalMvContextOffset = 3;

    /// <summary>
    /// The bit offset of the spatial reference-motion-vector context in a packed inter-mode context.
    /// </summary>
    private const int RefMvContextOffset = 4;

    /// <summary>
    /// The low-three-bit mask containing the new-motion-vector context.
    /// </summary>
    private const int NewMvContextMask = (1 << GlobalMvContextOffset) - 1;

    /// <summary>
    /// The mask selecting the two-value global-motion context from its single packed bit.
    /// </summary>
    private const int ZeroMvContextMask = (1 << (RefMvContextOffset - GlobalMvContextOffset)) - 1;

    /// <summary>
    /// The high-nibble mask containing the spatial reference-motion-vector context.
    /// </summary>
    private const int RefMvContextMask = (1 << (8 - RefMvContextOffset)) - 1;

    /// <summary>
    /// The weight at which AV1 classifies a reference-motion-vector candidate as a strong spatial match.
    /// </summary>
    private const int ReferenceCategoryLevel = 640;

    /// <summary>
    /// The number of interpolation filters selectable by per-block switchable syntax.
    /// </summary>
    private const int SwitchableInterpolationFilterCount = 3;

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
    /// Gets the intra/inter prediction context from the immediately above and left blocks.
    /// </summary>
    /// <param name="above">The above block, or <see langword="null"/> at a tile boundary.</param>
    /// <param name="left">The left block, or <see langword="null"/> at a tile boundary.</param>
    /// <returns>The context in the inclusive range zero through three.</returns>
    public static int GetIntraInterContext(Av1BlockModeInfo? above, Av1BlockModeInfo? left)
    {
        if (above is not null && left is not null)
        {
            bool aboveIsIntra = above.ReferenceFrames[0] <= Av1ReferenceFrameType.Intra;
            bool leftIsIntra = left.ReferenceFrames[0] <= Av1ReferenceFrameType.Intra;

            // AV1 reserves context three for two intra neighbors, context one for a mixed pair, and context zero for
            // two inter neighbors. These values directly index intra_inter_cdf and are not probability ranks.
            if (aboveIsIntra && leftIsIntra)
            {
                return 3;
            }

            return aboveIsIntra || leftIsIntra ? 1 : 0;
        }

        // A single intra neighbor uses context two. A single inter neighbor and a block with no neighbors both use
        // context zero, matching the unavailable-neighbor behavior in libaom's av1_get_intra_inter_context.
        if (above is not null)
        {
            return above.ReferenceFrames[0] <= Av1ReferenceFrameType.Intra ? 2 : 0;
        }

        if (left is not null)
        {
            return left.ReferenceFrames[0] <= Av1ReferenceFrameType.Intra ? 2 : 0;
        }

        return 0;
    }

    /// <summary>
    /// Gets the block reference-mode context from the immediately above and left blocks.
    /// </summary>
    /// <param name="above">The above block, or <see langword="null"/> at a tile boundary.</param>
    /// <param name="left">The left block, or <see langword="null"/> at a tile boundary.</param>
    /// <returns>The context in the inclusive range zero through four.</returns>
    public static int GetReferenceModeContext(Av1BlockModeInfo? above, Av1BlockModeInfo? left)
    {
        // Libaom first classifies whether each neighbor uses a second inter reference. Single neighbors then contribute
        // their forward/backward direction, while intra neighbors take the same branch as a non-forward reference.
        if (above is not null && left is not null)
        {
            bool aboveIsCompound = above.ReferenceFrames[1] > Av1ReferenceFrameType.Intra;
            bool leftIsCompound = left.ReferenceFrames[1] > Av1ReferenceFrameType.Intra;

            if (!aboveIsCompound && !leftIsCompound)
            {
                bool aboveIsBackward = above.ReferenceFrames[0] >= Av1ReferenceFrameType.Backward;
                bool leftIsBackward = left.ReferenceFrames[0] >= Av1ReferenceFrameType.Backward;

                return aboveIsBackward == leftIsBackward ? 0 : 1;
            }

            if (!aboveIsCompound)
            {
                bool aboveIsBackward = above.ReferenceFrames[0] >= Av1ReferenceFrameType.Backward;
                bool aboveIsIntra = above.ReferenceFrames[0] <= Av1ReferenceFrameType.Intra;

                return 2 + (aboveIsBackward || aboveIsIntra ? 1 : 0);
            }

            if (!leftIsCompound)
            {
                bool leftIsBackward = left.ReferenceFrames[0] >= Av1ReferenceFrameType.Backward;
                bool leftIsIntra = left.ReferenceFrames[0] <= Av1ReferenceFrameType.Intra;

                return 2 + (leftIsBackward || leftIsIntra ? 1 : 0);
            }

            return 4;
        }

        Av1BlockModeInfo? neighbor = above ?? left;

        if (neighbor is not null)
        {
            bool isCompound = neighbor.ReferenceFrames[1] > Av1ReferenceFrameType.Intra;

            if (isCompound)
            {
                return 3;
            }

            return neighbor.ReferenceFrames[0] >= Av1ReferenceFrameType.Backward ? 1 : 0;
        }

        // With no spatial votes, AV1 uses the neutral single-versus-compound context rather than context zero.
        return 1;
    }

    /// <summary>
    /// Gets the context that selects a unidirectional or bidirectional compound reference pair.
    /// </summary>
    /// <param name="above">The above block, or <see langword="null"/> at a tile boundary.</param>
    /// <param name="left">The left block, or <see langword="null"/> at a tile boundary.</param>
    /// <returns>The context in the inclusive range zero through four.</returns>
    public static int GetCompoundReferenceTypeContext(Av1BlockModeInfo? above, Av1BlockModeInfo? left)
    {
        if (above is not null && left is not null)
        {
            bool aboveIntra = !IsInterBlock(above);
            bool leftIntra = !IsInterBlock(left);
            if (aboveIntra && leftIntra)
            {
                return 2;
            }

            if (aboveIntra || leftIntra)
            {
                Av1BlockModeInfo inter = aboveIntra ? left : above;
                return HasCompoundReference(inter) ? 1 + (2 * (HasUnidirectionalCompoundReferences(inter) ? 1 : 0)) : 2;
            }

            bool aboveSingle = !HasCompoundReference(above);
            bool leftSingle = !HasCompoundReference(left);
            Av1ReferenceFrameType abovePrimary = above.ReferenceFrames[0];
            Av1ReferenceFrameType leftPrimary = left.ReferenceFrames[0];
            if (aboveSingle && leftSingle)
            {
                return 1 + (2 * (IsBackwardReference(abovePrimary) == IsBackwardReference(leftPrimary) ? 1 : 0));
            }

            if (aboveSingle || leftSingle)
            {
                Av1BlockModeInfo compound = aboveSingle ? left : above;
                if (!HasUnidirectionalCompoundReferences(compound))
                {
                    return 1;
                }

                return 3 + (IsBackwardReference(abovePrimary) == IsBackwardReference(leftPrimary) ? 1 : 0);
            }

            bool aboveUnidirectional = HasUnidirectionalCompoundReferences(above);
            bool leftUnidirectional = HasUnidirectionalCompoundReferences(left);
            if (!aboveUnidirectional && !leftUnidirectional)
            {
                return 0;
            }

            if (!aboveUnidirectional || !leftUnidirectional)
            {
                return 2;
            }

            return 3 + ((abovePrimary == Av1ReferenceFrameType.Backward) == (leftPrimary == Av1ReferenceFrameType.Backward) ? 1 : 0);
        }

        Av1BlockModeInfo? edge = above ?? left;
        if (edge is null || !IsInterBlock(edge) || !HasCompoundReference(edge))
        {
            return 2;
        }

        return HasUnidirectionalCompoundReferences(edge) ? 4 : 0;
    }

    /// <summary>
    /// Gets the switchable interpolation-filter context for one prediction direction.
    /// </summary>
    /// <param name="modeInfo">The current inter block.</param>
    /// <param name="above">The above block, or <see langword="null"/> at a tile boundary.</param>
    /// <param name="left">The left block, or <see langword="null"/> at a tile boundary.</param>
    /// <param name="direction">Zero for the vertical filter or one for the horizontal filter.</param>
    /// <returns>The context in the inclusive range zero through fifteen.</returns>
    public static int GetSwitchableInterpolationContext(
        Av1BlockModeInfo modeInfo,
        Av1BlockModeInfo? above,
        Av1BlockModeInfo? left,
        int direction)
    {
        const int filterContextCount = SwitchableInterpolationFilterCount + 1;
        const int horizontalContextOffset = filterContextCount * 2;
        ReadOnlySpan<Av1ReferenceFrameType> referenceFrames = modeInfo.ReferenceFrames;
        Av1ReferenceFrameType primaryReference = referenceFrames[0];
        bool isCompound = referenceFrames[1] > Av1ReferenceFrameType.Intra;

        // The sixteen rows are laid out as single vertical, compound vertical, single horizontal, then compound
        // horizontal, with four neighbor states in each group.
        int context = (isCompound ? filterContextCount : 0) + (direction * horizontalContextOffset);
        int leftFilter = GetReferenceInterpolationFilterContext(left, primaryReference, direction);
        int aboveFilter = GetReferenceInterpolationFilterContext(above, primaryReference, direction);

        if (leftFilter == aboveFilter)
        {
            return context + leftFilter;
        }

        // The fourth neighbor state is not a selectable Bilinear filter. It is the value libaom uses when a neighbor
        // does not share the current primary reference, and when two contributing neighbors selected different filters.
        if (leftFilter == SwitchableInterpolationFilterCount)
        {
            return context + aboveFilter;
        }

        if (aboveFilter == SwitchableInterpolationFilterCount)
        {
            return context + leftFilter;
        }

        return context + SwitchableInterpolationFilterCount;
    }

    /// <summary>
    /// Gets the new-motion-vector decision context from a packed single-reference inter-mode context.
    /// </summary>
    /// <param name="modeContext">The packed mode context produced by reference-motion-vector candidate analysis.</param>
    /// <returns>For a valid packed mode context, the context in the inclusive range zero through five.</returns>
    public static int GetNewMvContext(int modeContext) => modeContext & NewMvContextMask;

    /// <summary>
    /// Gets the global-motion decision context from a packed single-reference inter-mode context.
    /// </summary>
    /// <param name="modeContext">The packed mode context produced by reference-motion-vector candidate analysis.</param>
    /// <returns>The context in the inclusive range zero through one.</returns>
    public static int GetZeroMvContext(int modeContext) => (modeContext >> GlobalMvContextOffset) & ZeroMvContextMask;

    /// <summary>
    /// Gets the spatial reference-motion-vector decision context from a packed single-reference inter-mode context.
    /// </summary>
    /// <param name="modeContext">The packed mode context produced by reference-motion-vector candidate analysis.</param>
    /// <returns>For a valid packed mode context, the context in the inclusive range zero through five.</returns>
    public static int GetRefMvContext(int modeContext) => (modeContext >> RefMvContextOffset) & RefMvContextMask;

    /// <summary>
    /// Maps the packed paired-reference candidate context to one of the eight compound inter-mode distributions.
    /// </summary>
    /// <param name="modeContext">The packed mode context produced by paired reference-motion-vector analysis.</param>
    /// <returns>The compound inter-mode context in the inclusive range zero through seven.</returns>
    public static int GetCompoundModeContext(int modeContext)
    {
        ReadOnlySpan<byte> contextMap =
        [
            0, 1, 1, 1, 1,
            1, 2, 3, 4, 4,
            4, 4, 5, 6, 7,
        ];

        int newMvContext = Math.Min(GetNewMvContext(modeContext), 4);
        int referenceContextGroup = GetRefMvContext(modeContext) >> 1;
        return contextMap[(referenceContextGroup * 5) + newMvContext];
    }

    /// <summary>
    /// Gets the dynamic reference-list context for two adjacent motion-vector candidates.
    /// </summary>
    /// <param name="referenceWeights">The candidate weights in dynamic reference-list order.</param>
    /// <param name="referenceIndex">The zero-based index of the first candidate in the pair.</param>
    /// <returns>The context in the inclusive range zero through two.</returns>
    public static int GetDrlContext(ReadOnlySpan<ushort> referenceWeights, int referenceIndex)
    {
        int currentWeight = referenceWeights[referenceIndex];
        int nextWeight = referenceWeights[referenceIndex + 1];

        // Candidate weights at or above the reference-category threshold carry a strong spatial match. The four
        // normative pairings use context zero for strong/strong and weak/strong, one for strong/weak, and two for weak/weak.
        if (currentWeight >= ReferenceCategoryLevel && nextWeight >= ReferenceCategoryLevel)
        {
            return 0;
        }

        if (currentWeight >= ReferenceCategoryLevel && nextWeight < ReferenceCategoryLevel)
        {
            return 1;
        }

        return currentWeight < ReferenceCategoryLevel && nextWeight < ReferenceCategoryLevel ? 2 : 0;
    }

    /// <summary>
    /// Counts the reference-frame labels used by the immediately above and left inter blocks.
    /// </summary>
    /// <param name="above">The above block, or <see langword="null"/> at a tile boundary.</param>
    /// <param name="left">The left block, or <see langword="null"/> at a tile boundary.</param>
    /// <param name="referenceCounts">The eight-entry reference-count destination indexed by <see cref="Av1ReferenceFrameType"/>.</param>
    public static void CollectNeighborReferenceCounts(Av1BlockModeInfo? above, Av1BlockModeInfo? left, Span<byte> referenceCounts)
    {
        // The caller reuses fixed inline storage across blocks. Clearing all eight entries matches libaom's
        // av1_collect_neighbors_ref_counts and prevents an unavailable neighbor from retaining an earlier block's vote.
        referenceCounts.Clear();

        if (above is not null)
        {
            AddNeighborReferenceCounts(above, referenceCounts);
        }

        if (left is not null)
        {
            AddNeighborReferenceCounts(left, referenceCounts);
        }
    }

    /// <summary>
    /// Gets the context that selects a backward instead of forward single reference.
    /// </summary>
    /// <param name="referenceCounts">The neighboring reference counts indexed by <see cref="Av1ReferenceFrameType"/>.</param>
    /// <returns>The context in the inclusive range zero through two.</returns>
    public static int GetSingleReferenceBackwardContext(ReadOnlySpan<byte> referenceCounts)
    {
        int forwardCount = referenceCounts[(int)Av1ReferenceFrameType.Last] +
            referenceCounts[(int)Av1ReferenceFrameType.Last2] +
            referenceCounts[(int)Av1ReferenceFrameType.Last3] +
            referenceCounts[(int)Av1ReferenceFrameType.Golden];

        int backwardCount = referenceCounts[(int)Av1ReferenceFrameType.Backward] +
            referenceCounts[(int)Av1ReferenceFrameType.Alternate2] +
            referenceCounts[(int)Av1ReferenceFrameType.Alternate];

        return GetBinaryReferenceContext(forwardCount, backwardCount);
    }

    /// <summary>
    /// Gets the context that selects Alternate instead of Backward or Alternate2.
    /// </summary>
    /// <param name="referenceCounts">The neighboring reference counts indexed by <see cref="Av1ReferenceFrameType"/>.</param>
    /// <returns>The context in the inclusive range zero through two.</returns>
    public static int GetSingleReferenceAlternateContext(ReadOnlySpan<byte> referenceCounts)
    {
        int backwardOrAlternate2Count = referenceCounts[(int)Av1ReferenceFrameType.Backward] +
            referenceCounts[(int)Av1ReferenceFrameType.Alternate2];

        int alternateCount = referenceCounts[(int)Av1ReferenceFrameType.Alternate];

        return GetBinaryReferenceContext(backwardOrAlternate2Count, alternateCount);
    }

    /// <summary>
    /// Gets the context that selects Last3 or Golden instead of Last or Last2.
    /// </summary>
    /// <param name="referenceCounts">The neighboring reference counts indexed by <see cref="Av1ReferenceFrameType"/>.</param>
    /// <returns>The context in the inclusive range zero through two.</returns>
    public static int GetSingleReferenceLast3OrGoldenContext(ReadOnlySpan<byte> referenceCounts)
    {
        int lastOrLast2Count = referenceCounts[(int)Av1ReferenceFrameType.Last] +
            referenceCounts[(int)Av1ReferenceFrameType.Last2];

        int last3OrGoldenCount = referenceCounts[(int)Av1ReferenceFrameType.Last3] +
            referenceCounts[(int)Av1ReferenceFrameType.Golden];

        return GetBinaryReferenceContext(lastOrLast2Count, last3OrGoldenCount);
    }

    /// <summary>
    /// Gets the context that selects Last2 instead of Last.
    /// </summary>
    /// <param name="referenceCounts">The neighboring reference counts indexed by <see cref="Av1ReferenceFrameType"/>.</param>
    /// <returns>The context in the inclusive range zero through two.</returns>
    public static int GetSingleReferenceLast2Context(ReadOnlySpan<byte> referenceCounts)
    {
        int lastCount = referenceCounts[(int)Av1ReferenceFrameType.Last];
        int last2Count = referenceCounts[(int)Av1ReferenceFrameType.Last2];

        return GetBinaryReferenceContext(lastCount, last2Count);
    }

    /// <summary>
    /// Gets the context that selects Golden instead of Last3.
    /// </summary>
    /// <param name="referenceCounts">The neighboring reference counts indexed by <see cref="Av1ReferenceFrameType"/>.</param>
    /// <returns>The context in the inclusive range zero through two.</returns>
    public static int GetSingleReferenceGoldenContext(ReadOnlySpan<byte> referenceCounts)
    {
        int last3Count = referenceCounts[(int)Av1ReferenceFrameType.Last3];
        int goldenCount = referenceCounts[(int)Av1ReferenceFrameType.Golden];

        return GetBinaryReferenceContext(last3Count, goldenCount);
    }

    /// <summary>
    /// Gets the context that selects Alternate2 instead of Backward.
    /// </summary>
    /// <param name="referenceCounts">The neighboring reference counts indexed by <see cref="Av1ReferenceFrameType"/>.</param>
    /// <returns>The context in the inclusive range zero through two.</returns>
    public static int GetSingleReferenceAlternate2Context(ReadOnlySpan<byte> referenceCounts)
    {
        int backwardCount = referenceCounts[(int)Av1ReferenceFrameType.Backward];
        int alternate2Count = referenceCounts[(int)Av1ReferenceFrameType.Alternate2];

        return GetBinaryReferenceContext(backwardCount, alternate2Count);
    }

    /// <summary>
    /// Gets the first unidirectional compound-reference decision context.
    /// </summary>
    public static int GetUnidirectionalCompoundBackwardContext(ReadOnlySpan<byte> referenceCounts)
        => GetSingleReferenceBackwardContext(referenceCounts);

    /// <summary>
    /// Gets the context that selects Last3 or Golden instead of Last2 for a forward unidirectional pair.
    /// </summary>
    public static int GetUnidirectionalCompoundLast3OrGoldenContext(ReadOnlySpan<byte> referenceCounts)
    {
        int last2Count = referenceCounts[(int)Av1ReferenceFrameType.Last2];
        int last3OrGoldenCount = referenceCounts[(int)Av1ReferenceFrameType.Last3] +
            referenceCounts[(int)Av1ReferenceFrameType.Golden];

        return GetBinaryReferenceContext(last2Count, last3OrGoldenCount);
    }

    /// <summary>
    /// Gets the context that selects Golden instead of Last3 for a forward unidirectional pair.
    /// </summary>
    public static int GetUnidirectionalCompoundGoldenContext(ReadOnlySpan<byte> referenceCounts)
        => GetSingleReferenceGoldenContext(referenceCounts);

    /// <summary>
    /// Gets the context that selects Last3 or Golden instead of Last or Last2 for a bidirectional pair.
    /// </summary>
    public static int GetCompoundForwardLast3OrGoldenContext(ReadOnlySpan<byte> referenceCounts)
        => GetSingleReferenceLast3OrGoldenContext(referenceCounts);

    /// <summary>
    /// Gets the context that selects Last2 instead of Last for a bidirectional pair.
    /// </summary>
    public static int GetCompoundForwardLast2Context(ReadOnlySpan<byte> referenceCounts)
        => GetSingleReferenceLast2Context(referenceCounts);

    /// <summary>
    /// Gets the context that selects Golden instead of Last3 for a bidirectional pair.
    /// </summary>
    public static int GetCompoundForwardGoldenContext(ReadOnlySpan<byte> referenceCounts)
        => GetSingleReferenceGoldenContext(referenceCounts);

    /// <summary>
    /// Gets the context that selects Alternate instead of Backward or Alternate2 for a bidirectional pair.
    /// </summary>
    public static int GetCompoundBackwardAlternateContext(ReadOnlySpan<byte> referenceCounts)
        => GetSingleReferenceAlternateContext(referenceCounts);

    /// <summary>
    /// Gets the context that selects Alternate2 instead of Backward for a bidirectional pair.
    /// </summary>
    public static int GetCompoundBackwardAlternate2Context(ReadOnlySpan<byte> referenceCounts)
        => GetSingleReferenceAlternate2Context(referenceCounts);

    /// <summary>
    /// Gets the temporal segment-prediction context from the immediately above and left blocks.
    /// </summary>
    /// <param name="aboveModeInfo">The above block, or <see langword="null"/> at a tile boundary.</param>
    /// <param name="leftModeInfo">The left block, or <see langword="null"/> at a tile boundary.</param>
    /// <returns>The context in the inclusive range zero through two.</returns>
    public static int GetSegmentIdPredictedContext(Av1BlockModeInfo? aboveModeInfo, Av1BlockModeInfo? leftModeInfo)
    {
        int abovePredicted = aboveModeInfo is not null && aboveModeInfo.SegmentIdPredicted ? 1 : 0;
        int leftPredicted = leftModeInfo is not null && leftModeInfo.SegmentIdPredicted ? 1 : 0;
        return abovePredicted + leftPredicted;
    }

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

    /// <summary>
    /// Adds one decoded inter neighbor's primary and optional secondary reference votes.
    /// </summary>
    /// <param name="modeInfo">The decoded neighboring block.</param>
    /// <param name="referenceCounts">The reference counts updated in place.</param>
    private static void AddNeighborReferenceCounts(Av1BlockModeInfo modeInfo, Span<byte> referenceCounts)
    {
        ReadOnlySpan<Av1ReferenceFrameType> referenceFrames = modeInfo.ReferenceFrames;

        if (referenceFrames[0] <= Av1ReferenceFrameType.Intra)
        {
            return;
        }

        referenceCounts[(int)referenceFrames[0]]++;

        // A current block may use one reference, but the conditioning neighbors may be compound blocks. Libaom counts
        // both labels so later single-reference decisions remain bit-exact when compound support is enabled.
        if (referenceFrames[1] > Av1ReferenceFrameType.Intra)
        {
            referenceCounts[(int)referenceFrames[1]]++;
        }
    }

    /// <summary>
    /// Converts neighboring votes for a binary reference-tree decision to its three-state AV1 context.
    /// </summary>
    /// <param name="zeroSymbolCount">The votes for the branch represented by symbol zero.</param>
    /// <param name="oneSymbolCount">The votes for the branch represented by symbol one.</param>
    /// <returns>One for tied votes, zero when symbol one has more votes, or two when symbol zero has more votes.</returns>
    private static int GetBinaryReferenceContext(int zeroSymbolCount, int oneSymbolCount)
        => zeroSymbolCount == oneSymbolCount ? 1 : zeroSymbolCount < oneSymbolCount ? 0 : 2;

    /// <summary>
    /// Determines whether a decoded block uses an inter reference.
    /// </summary>
    private static bool IsInterBlock(Av1BlockModeInfo modeInfo)
        => modeInfo.ReferenceFrames[0] >= Av1ReferenceFrameType.Last;

    /// <summary>
    /// Determines whether a decoded block has a second inter reference.
    /// </summary>
    private static bool HasCompoundReference(Av1BlockModeInfo modeInfo)
        => modeInfo.ReferenceFrames[1] > Av1ReferenceFrameType.Intra;

    /// <summary>
    /// Determines whether both compound references point in the same display-order direction.
    /// </summary>
    private static bool HasUnidirectionalCompoundReferences(Av1BlockModeInfo modeInfo)
        => IsBackwardReference(modeInfo.ReferenceFrames[0]) == IsBackwardReference(modeInfo.ReferenceFrames[1]);

    /// <summary>
    /// Determines whether a retained reference belongs to the backward group.
    /// </summary>
    private static bool IsBackwardReference(Av1ReferenceFrameType referenceFrame)
        => referenceFrame >= Av1ReferenceFrameType.Backward;

    /// <summary>
    /// Gets one neighbor's interpolation-filter contribution for the requested reference and direction.
    /// </summary>
    /// <param name="modeInfo">The decoded neighboring block, or <see langword="null"/> when unavailable.</param>
    /// <param name="referenceFrame">The current block's primary reference.</param>
    /// <param name="direction">Zero for the vertical filter or one for the horizontal filter.</param>
    /// <returns>The selected filter index, or three when the neighbor does not contribute.</returns>
    private static int GetReferenceInterpolationFilterContext(
        Av1BlockModeInfo? modeInfo,
        Av1ReferenceFrameType referenceFrame,
        int direction)
    {
        if (modeInfo is null)
        {
            return SwitchableInterpolationFilterCount;
        }

        ReadOnlySpan<Av1ReferenceFrameType> referenceFrames = modeInfo.ReferenceFrames;

        // A compound neighbor contributes when either of its references matches the current primary reference.
        if (referenceFrames[0] != referenceFrame && referenceFrames[1] != referenceFrame)
        {
            return SwitchableInterpolationFilterCount;
        }

        return (int)modeInfo.InterpolationFilters[direction];
    }
}

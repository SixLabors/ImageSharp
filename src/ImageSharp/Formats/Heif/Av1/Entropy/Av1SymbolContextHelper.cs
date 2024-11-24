// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;

internal static class Av1SymbolContextHelper
{
    public static readonly int[] EndOfBlockOffsetBits = [0, 0, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9];
    public static readonly int[] EndOfBlockGroupStart = [0, 1, 2, 3, 5, 9, 17, 33, 65, 129, 257, 513];
    private static readonly int[] TransformCountInSet = [1, 2, 5, 7, 12, 16];
    private static readonly byte[] EndOfBlockToPositionSmall = [
        0, 1, 2, // 0-2
        3, 3, // 3-4
        4, 4, 4, 4, // 5-8
        5, 5, 5, 5, 5, 5, 5, 5, // 9-16
        6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6 // 17-32
    ];

    private static readonly byte[] EndOfBlockToPositionLarge = [
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

    // Maps tx set types to the indices. INTRA values only
    private static readonly int[] ExtendedTransformSetToIndex = [0, -1, 2, 1, -1, -1];

    internal static int RecordEndOfBlockPosition(int endOfBlockPoint, int endOfBlockExtra)
    {
        int endOfBlock = EndOfBlockGroupStart[endOfBlockPoint];
        if (endOfBlock > 2)
        {
            endOfBlock += endOfBlockExtra;
        }

        return endOfBlock;
    }

    internal static int GetBaseRangeContextEndOfBlock(int index, int blockWidthLog2, Av1TransformClass transformClass)
    {
        int row = index >> blockWidthLog2;
        int col = index - (row << blockWidthLog2);
        if (index == 0)
        {
            return 0;
        }

        if ((transformClass == Av1TransformClass.Class2D && row < 2 && col < 2) ||
            (transformClass == Av1TransformClass.ClassHorizontal && col == 0) ||
            (transformClass == Av1TransformClass.ClassVertical && row == 0))
        {
            return 7;
        }

        return 14;
    }

    /// <summary>
    /// SVT: get_lower_levels_ctx_eob
    /// </summary>
    internal static int GetLowerLevelContextEndOfBlock(int blockWidthLog2, int height, int scanIndex)
    {
        if (scanIndex == 0)
        {
            return 0;
        }

        if (scanIndex <= height << blockWidthLog2 >> 3)
        {
            return 1;
        }

        if (scanIndex <= height << blockWidthLog2 >> 2)
        {
            return 2;
        }

        return 3;
    }

    /// <summary>
    /// SVT: get_br_ctx_2d
    /// </summary>
    internal static int GetBaseRangeContext2d(Av1LevelBuffer levels, int index, int blockWidthLog2)
    {
        DebugGuard.MustBeGreaterThan(index, 0, nameof(index));
        int y = index >> blockWidthLog2;
        int x = index - (y << blockWidthLog2);
        int stride = (1 << blockWidthLog2) + Av1Constants.TransformPadHorizontal;
        int pos = (y * stride) + x;
        Span<byte> row0 = levels.GetRow(y);
        Span<byte> row1 = levels.GetRow(y + 1);
        int mag =
            Math.Min((int)row0[1], Av1Constants.MaxBaseRange) +
            Math.Min((int)row1[0], Av1Constants.MaxBaseRange) +
            Math.Min((int)row1[1], Av1Constants.MaxBaseRange);
        mag = Math.Min((mag + 1) >> 1, 6);
        if ((y | x) < 2)
        {
            return mag + 7;
        }

        return mag + 14;
    }

    /// <summary>
    /// SVT: get_lower_levels_ctx_2d
    /// </summary>
    internal static int GetLowerLevelsContext2d(Av1LevelBuffer levelBuffer, int index, int blockWidthLog2, Av1TransformSize transformSize)
    {
        DebugGuard.MustBeGreaterThan(index, 0, nameof(index));
        int y = index >> blockWidthLog2;
        int x = index - (y << blockWidthLog2);
        int mag;
        Span<byte> row0 = levelBuffer.GetRow(y);
        Span<byte> row1 = levelBuffer.GetRow(y + 1);
        Span<byte> row2 = levelBuffer.GetRow(y + 2);
        mag = Math.Min((int)row0[1], 3); // { 0, 1 }
        mag += Math.Min((int)row1[0], 3); // { 1, 0 }
        mag += Math.Min((int)row1[1], 3); // { 1, 1 }
        mag += Math.Min((int)row0[2], 3); // { 0, 2 }
        mag += Math.Min((int)row2[0], 3); // { 2, 0 }

        int ctx = Math.Min((mag + 1) >> 1, 4);
        return ctx + Av1NzMap.GetNzMapContext(transformSize, index);
    }

    /// <summary>
    /// SVT: get_br_ctx
    /// </summary>
    internal static int GetBaseRangeContext(Av1LevelBuffer levels, int index, int blockWidthLog2, Av1TransformClass transformClass)
    {
        int y = index >> blockWidthLog2;
        int x = index - (y << blockWidthLog2);
        Span<byte> row0 = levels.GetRow(y);
        Span<byte> row1 = levels.GetRow(y + 1);
        int mag = row0[x + 1];
        mag += row1[x];
        switch (transformClass)
        {
            case Av1TransformClass.Class2D:
                mag += row1[x + 1];
                mag = Math.Min((mag + 1) >> 1, 6);
                if (index == 0)
                {
                    return mag;
                }

                if (y < 2 && x < 2)
                {
                    return mag + 7;
                }

                break;
            case Av1TransformClass.ClassHorizontal:
                mag += row0[2];
                mag = Math.Min((mag + 1) >> 1, 6);
                if (index == 0)
                {
                    return mag;
                }

                if (x == 0)
                {
                    return mag + 7;
                }

                break;
            case Av1TransformClass.ClassVertical:
                mag += levels.GetRow(y + 2)[0];
                mag = Math.Min((mag + 1) >> 1, 6);
                if (index == 0)
                {
                    return mag;
                }

                if (y == 0)
                {
                    return mag + 7;
                }

                break;
            default:
                break;
        }

        return mag + 14;
    }

    internal static int GetLowerLevelsContext(Av1LevelBuffer levels, int pos, int bwl, Av1TransformSize transformSize, Av1TransformClass transformClass)
    {
        int stats = Av1NzMap.GetNzMagnitude(levels, pos >> bwl, transformClass);
        return Av1NzMap.GetNzMapContextFromStats(stats, pos, bwl, transformSize, transformClass);
    }

    internal static Av1TransformSetType GetExtendedTransformSetType(Av1TransformSize transformSize, bool useReducedSet)
    {
        Av1TransformSize squareUpSize = transformSize.GetSquareUpSize();

        if (squareUpSize >= Av1TransformSize.Size32x32)
        {
            return Av1TransformSetType.DctOnly;
        }

        if (useReducedSet)
        {
            return Av1TransformSetType.Dtt4Identity;
        }

        Av1TransformSize squareSize = transformSize.GetSquareSize();
        return squareSize == Av1TransformSize.Size16x16 ? Av1TransformSetType.Dtt4Identity : Av1TransformSetType.Dtt4Identity1dDct;
    }

    internal static Av1TransformType ConvertIntraModeToTransformType(Av1BlockModeInfo modeInfo, Av1PlaneType planeType)
    {
        Av1PredictionMode mode = (planeType == Av1PlaneType.Y) ? modeInfo.YMode : modeInfo.UvMode;
        if (mode == Av1PredictionMode.UvChromaFromLuma)
        {
            mode = Av1PredictionMode.DC;
        }

        return mode.ToTransformType();
    }

    /// <summary>
    /// SVT: get_nz_map_ctx
    /// </summary>
    internal static sbyte GetNzMapContext(
        Av1LevelBuffer levels,
        int index,
        int blockWidthLog2,
        int height,
        int scan_idx,
        bool is_eob,
        Av1TransformSize transformSize,
        Av1TransformClass transformClass)
    {
        if (is_eob)
        {
            if (scan_idx == 0)
            {
                return 0;
            }

            if (scan_idx <= (height << blockWidthLog2) / 8)
            {
                return 1;
            }

            if (scan_idx <= (height << blockWidthLog2) / 4)
            {
                return 2;
            }

            return 3;
        }

        int stats = Av1NzMap.GetNzMagnitude(levels, index, blockWidthLog2, transformClass);
        return (sbyte)Av1NzMap.GetNzMapContextFromStats(stats, index, blockWidthLog2, transformSize, transformClass);
    }

    /// <summary>
    /// SVT: svt_av1_get_nz_map_contexts_c
    /// </summary>
    internal static void GetNzMapContexts(
        Av1LevelBuffer levels,
        ReadOnlySpan<short> scan,
        ushort eob,
        Av1TransformSize transformSize,
        Av1TransformClass transformClass,
        Span<sbyte> coefficientContexts)
    {
        int blockWidthLog2 = transformSize.GetBlockWidthLog2();
        int height = transformSize.GetHeight();
        for (int i = 0; i < eob; ++i)
        {
            int pos = scan[i];
            coefficientContexts[pos] = GetNzMapContext(levels, pos, blockWidthLog2, height, i, i == eob - 1, transformSize, transformClass);
        }
    }

    /// <summary>
    /// SVT: get_ext_tx_set_type
    /// </summary>
    internal static Av1TransformSetType GetExtendedTransformSetType(Av1TransformSize transformSize, bool isInter, bool useReducedTransformSet)
    {
        Av1TransformSize transformSizeSquareUp = transformSize.GetSquareUpSize();

        if (transformSizeSquareUp > Av1TransformSize.Size32x32)
        {
            return Av1TransformSetType.DctOnly;
        }

        if (transformSizeSquareUp == Av1TransformSize.Size32x32)
        {
            return isInter ? Av1TransformSetType.DctIdentity : Av1TransformSetType.DctOnly;
        }

        if (useReducedTransformSet)
        {
            return isInter ? Av1TransformSetType.DctIdentity : Av1TransformSetType.Dtt4Identity;
        }

        Av1TransformSize transformSizeSquare = transformSize.GetSquareSize();
        if (isInter)
        {
            return transformSizeSquare == Av1TransformSize.Size16x16 ? Av1TransformSetType.Dtt9Identity1dDct : Av1TransformSetType.All16;
        }
        else
        {
            return transformSizeSquare == Av1TransformSize.Size16x16 ? Av1TransformSetType.Dtt4Identity : Av1TransformSetType.Dtt4Identity1dDct;
        }
    }

    internal static int GetExtendedTransformTypeCount(Av1TransformSize transformSize, bool useReducedTransformSet)
    {
        int setType = (int)GetExtendedTransformSetType(transformSize, useReducedTransformSet);
        return TransformCountInSet[setType];
    }

    /// <summary>
    /// SVT: get_ext_tx_set
    /// </summary>
    internal static int GetExtendedTransformSet(Av1TransformSize transformSize, bool useReducedTransformSet)
    {
        int setType = (int)GetExtendedTransformSetType(transformSize, useReducedTransformSet);
        return ExtendedTransformSetToIndex[setType];
    }

    /// <summary>
    /// SVT: set_dc_sign
    /// </summary>
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
    /// SVT: get_eob_pos_token
    /// </summary>
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
}

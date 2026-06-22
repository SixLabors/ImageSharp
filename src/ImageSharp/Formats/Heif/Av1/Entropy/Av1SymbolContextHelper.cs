// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;

internal static class Av1SymbolContextHelper
{
    public static readonly int[][] ExtendedTransformIndices = [
        [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0], // DCT only
        [1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0], // Inter set 3
        [1, 3, 4, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0], // Intra set 2
        [1, 5, 6, 4, 0, 0, 0, 0, 0, 0, 2, 3, 0, 0, 0, 0], // Intra set 1
        [3, 4, 5, 8, 6, 7, 9, 10, 11, 0, 1, 2, 0, 0, 0, 0], // Inter set 2
        [7, 8, 9, 12, 10, 11, 13, 14, 15, 0, 1, 2, 3, 4, 5, 6], // All 16, inter set 1
    ];

    // Maps tx set types to the distribution indices. INTRA values only
    private static readonly int[] ExtendedTransformSetToIndex = [0, -1, 2, 1, -1, -1];

    /// <summary>
    /// Section 5.11.48: Transform type syntax
    /// </summary>
    public static readonly Av1TransformType[][] ExtendedTransformInverse = [
        [Av1TransformType.DctDct], // DCT only
        [], // Inter set 3
        [Av1TransformType.Identity, Av1TransformType.DctDct, Av1TransformType.AdstAdst, Av1TransformType.AdstDct, Av1TransformType.DctAdst], // Intra set 2
        [Av1TransformType.Identity, Av1TransformType.DctDct, Av1TransformType.VerticalDct, Av1TransformType.HorizontalDct, Av1TransformType.AdstAdst, Av1TransformType.AdstDct, Av1TransformType.DctAdst], // Intra set 1
        [], // Inter set 2
        [], // All 16, inter set 1
    ];

    public static readonly int[] EndOfBlockOffsetBits = [0, 0, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9];
    public static readonly int[] EndOfBlockGroupStart = [0, 1, 2, 3, 5, 9, 17, 33, 65, 129, 257, 513];
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

    internal static Av1TransformSize GetTransformSizeContext(Av1TransformSize originalSize)
        => (Av1TransformSize)(((int)originalSize.GetSquareSize() + (int)originalSize.GetSquareUpSize() + 1) >> 1);

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
    /// SVT: get_lower_levels_ctx_eob
    /// </summary>
    internal static int GetLowerLevelContextEndOfBlock(Av1LevelBuffer levels, Point position)
    {
        if (position.X == 0 && position.Y == 0)
        {
            return 0;
        }

        int total = levels.Size.Height * levels.Size.Width;
        int index = position.X + (position.Y * levels.Size.Width);
        if (index <= total >> 3)
        {
            return 1;
        }

        if (index <= total >> 2)
        {
            return 2;
        }

        return 3;
    }

    /// <summary>
    /// SVT: get_lower_levels_ctx_2d
    /// </summary>
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
    /// SVT: get_br_ctx
    /// </summary>
    /// <remarks>Spec section 8.2.3, under 'coeff_br'.</remarks>
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
    /// SVT: get_br_ctx_2d
    /// </summary>
    internal static int GetBaseRangeContext2d(Av1LevelBuffer levels, Point position)
    {
        DebugGuard.MustBeGreaterThan(position.X + position.Y, 0, nameof(position));
        Span<byte> row0 = levels.GetRow(position.Y);
        Span<byte> row1 = levels.GetRow(position.Y + 1);

        // No need to clip quantized values to COEFF_BASE_RANGE + NUM_BASE_LEVELS
        // + 1, because we clip the overall output to 6 and the unclipped
        // quantized values will always result in an output of greater than 6.
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

    internal static int GetLowerLevelsContext(Av1LevelBuffer levels, Point position, Av1TransformSize transformSize, Av1TransformClass transformClass)
    {
        int stats = Av1NzMap.GetNzMagnitude(levels, position, transformClass);
        return Av1NzMap.GetNzMapContextFromStats(stats, position, transformSize, transformClass);
    }

    /// <summary>
    /// SVT: get_ext_tx_set_type
    /// </summary>
    internal static Av1TransformSetType GetExtendedTransformSetType(Av1TransformSize transformSize, bool useReducedSet)
    {
        Av1TransformSize squareUpSize = transformSize.GetSquareUpSize();

        if (squareUpSize >= Av1TransformSize.Size32x32)
        {
            return Av1TransformSetType.DctOnly;
        }

        if (useReducedSet)
        {
            return Av1TransformSetType.IntraSet2;
        }

        Av1TransformSize squareSize = transformSize.GetSquareSize();
        return squareSize == Av1TransformSize.Size16x16 ? Av1TransformSetType.IntraSet2 : Av1TransformSetType.IntraSet1;
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
        Point position,
        bool isEndOfBlock,
        Av1TransformSize transformSize,
        Av1TransformClass transformClass)
    {
        if (isEndOfBlock)
        {
            return (sbyte)GetLowerLevelContextEndOfBlock(levels, position);
        }

        int stats = Av1NzMap.GetNzMagnitude(levels, position, transformClass);
        return (sbyte)Av1NzMap.GetNzMapContextFromStats(stats, position, transformSize, transformClass);
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
        for (int i = 0; i < eob; ++i)
        {
            int pos = scan[i];
            Point position = levels.GetPosition(pos);
            coefficientContexts[pos] = GetNzMapContext(levels, position, i == eob - 1, transformSize, transformClass);
        }
    }

    /// <summary>
    /// SVT: get_ext_tx_types
    /// </summary>
    internal static int GetExtendedTransformTypeCount(Av1TransformSetType setType) => ExtendedTransformInverse[(int)setType].Length;

    /// <summary>
    /// SVT: get_ext_tx_set
    /// </summary>
    internal static int GetExtendedTransformSet(Av1TransformSetType setType) => ExtendedTransformSetToIndex[(int)setType];

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

    public static int GetSegmentId(Av1PartitionInfo partitionInfo, ObuFrameHeader frameHeader, int[][] segmentIds, int rowIndex, int columnIndex)
    {
        int modeInfoOffset = (rowIndex * frameHeader.ModeInfoColumnCount) + columnIndex;
        int bw4 = partitionInfo.ModeInfo.BlockSize.Get4x4WideCount();
        int bh4 = partitionInfo.ModeInfo.BlockSize.Get4x4HighCount();
        int xMin = Math.Min(frameHeader.ModeInfoColumnCount - columnIndex, bw4);
        int yMin = Math.Min(frameHeader.ModeInfoRowCount - rowIndex, bh4);
        int segmentId = Av1Constants.MaxSegmentCount - 1;
        for (int y = 0; y < yMin; y++)
        {
            for (int x = 0; x < xMin; x++)
            {
                segmentId = Math.Min(segmentId, segmentIds[y][x]);
            }
        }

        return segmentId;
    }

    /// <summary>
    /// SVT: svt_aom_get_segment_id
    /// </summary>
    public static int GetSegmentId(Av1EncoderCommon cm, ReadOnlySpan<byte> segment_ids, Av1BlockSize bsize, Point modeInfoPosition)
    {
        int mi_offset = (modeInfoPosition.Y * cm.ModeInfoColumnCount) + modeInfoPosition.X;
        int bw = bsize.GetWidth();
        int bh = bsize.GetHeight();
        int xmis = Math.Min(cm.ModeInfoColumnCount - modeInfoPosition.X, bw);
        int ymis = Math.Min(cm.ModeInfoRowCount - modeInfoPosition.Y, bh);
        int segment_id = Av1Constants.MaxSegmentCount;

        for (int y = 0; y < ymis; ++y)
        {
            int offset = mi_offset + (y * cm.ModeInfoColumnCount);
            for (int x = 0; x < xmis; ++x)
            {
                segment_id = Math.Min(segment_id, segment_ids[offset + x]);
            }
        }

        Guard.IsTrue(segment_id is >= 0 and < Av1Constants.MaxSegmentCount, nameof(segment_id), "Segment ID needs to be in proper range.");
        return segment_id;
    }

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

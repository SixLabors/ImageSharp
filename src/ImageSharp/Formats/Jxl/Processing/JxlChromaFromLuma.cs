// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Fields;
using static SixLabors.ImageSharp.Formats.Jxl.Processing.JxlFrameDimensions;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing;

/// <summary>
/// Shared constants for CFL (Chroma From Luma) functions.
/// </summary>
internal static class JxlChromaFromLuma
{
    /// <summary>
    /// Tiles are 64x64.
    /// </summary>
    public const int ColorTileDimension = 64;

    /// <summary>
    /// Division of the color tile dimension by the block dimension. Therefore,
    /// this is 8x8.
    /// </summary>
    public const int ColorTileDimensionInBlocks = ColorTileDimension / BlockDimensions;

    public const int DefaultColorFactor = 84;

    /// <summary>
    /// Chroma From Luma fixed point precision, which is
    /// 11 bits.
    /// </summary>
    public const int CflFixedPointPrecision = 11;

    /// <summary>
    /// 524287
    /// </summary>
    public const int CflFixedPointRatioMax = (256 << CflFixedPointPrecision) - 1;

    /// <summary>
    /// Shared variable U32 distributions for the color factor.
    /// </summary>
    public static readonly JxlU32Enc ColorFactorDistribution = new(
        JxlFieldExpressions.Value(DefaultColorFactor),
        JxlFieldExpressions.Value(256),
        JxlFieldExpressions.BitsOffset(8, 2),
        JxlFieldExpressions.BitsOffset(16, 258));
}

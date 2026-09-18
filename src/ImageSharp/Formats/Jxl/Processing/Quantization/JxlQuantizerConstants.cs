// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Quantization;

/// <summary>
/// Shared constants used by the quantizer.
/// </summary>
internal static class JxlQuantizerConstants
{
    public const int Log2MaxDistanceBands = 4;

    public const int MaxDistanceBands = 1 + (1 << Log2MaxDistanceBands);

    public const int CeilLog2NumPredefinedTables = 0;

    public const int Log2NumQuantModes = 3;

    /// <summary>
    /// Total number of quantization tables.
    /// </summary>
    public const byte NumberOfQuantizerTables = (byte)(JxlQuantTable.DCT128X256 + 1);

    /// <summary>
    /// Gets the inverse DC quantization table.
    /// </summary>
    public static ReadOnlySpan<float> InverseDcQuant => [4096f, 512f, 256f];

    /// <summary>
    /// Gets the forward DC quantization table.
    /// </summary>
    public static ReadOnlySpan<float> DcQuant => [
        1f / 4096f,
        1f / 512f,
        1f / 256f];

    /// <summary>
    /// Gets a translation table for converting AC strategies to quant tables.
    /// Simply pass the index of the AC strategy enum and you'll get back the
    /// matching quant table.
    /// </summary>
    public static ReadOnlySpan<JxlQuantTable> AcStrategyToQuantTableMap =>
    [
        JxlQuantTable.DCT,        JxlQuantTable.IDENTITY,   JxlQuantTable.DCT2X2,
        JxlQuantTable.DCT4X4,     JxlQuantTable.DCT16X16,   JxlQuantTable.DCT32X32,
        JxlQuantTable.DCT8X16,    JxlQuantTable.DCT8X16,    JxlQuantTable.DCT8X32,
        JxlQuantTable.DCT8X32,    JxlQuantTable.DCT16X32,   JxlQuantTable.DCT16X32,
        JxlQuantTable.DCT4X8,     JxlQuantTable.DCT4X8,     JxlQuantTable.AFV0,
        JxlQuantTable.AFV0,       JxlQuantTable.AFV0,       JxlQuantTable.AFV0,
        JxlQuantTable.DCT64X64,   JxlQuantTable.DCT32X64,   JxlQuantTable.DCT32X64,
        JxlQuantTable.DCT128X128, JxlQuantTable.DCT64X128,  JxlQuantTable.DCT64X128,
        JxlQuantTable.DCT256X256, JxlQuantTable.DCT128X256, JxlQuantTable.DCT128X256
    ];
}

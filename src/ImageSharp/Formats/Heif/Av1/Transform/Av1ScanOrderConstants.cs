// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Quantization;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

internal static class Av1ScanOrderConstants
{
    public const int QuantizationMatrixLevelBitCount = 4;
    public const int QuantizationMatrixLevelCount = 1 << QuantizationMatrixLevelBitCount;

    private static readonly Av1ScanOrder[][] ScanOrders =
    [

        // Transform size 4x4
        [
            new Av1ScanOrder(DefaultScan4x4),
            new Av1ScanOrder(DefaultScan4x4),
            new Av1ScanOrder(DefaultScan4x4),
            new Av1ScanOrder(DefaultScan4x4),
            new Av1ScanOrder(DefaultScan4x4),
            new Av1ScanOrder(DefaultScan4x4),
            new Av1ScanOrder(DefaultScan4x4),
            new Av1ScanOrder(DefaultScan4x4),
            new Av1ScanOrder(DefaultScan4x4),
            new Av1ScanOrder(DefaultScan4x4),
            new Av1ScanOrder(MatrixRowScan4x4),
            new Av1ScanOrder(MatrixColumnScan4x4),
            new Av1ScanOrder(MatrixRowScan4x4),
            new Av1ScanOrder(MatrixColumnScan4x4),
            new Av1ScanOrder(MatrixRowScan4x4),
            new Av1ScanOrder(MatrixColumnScan4x4),
        ],

        // Transform size 8x8
        [
            new Av1ScanOrder(DefaultScan8x8),
            new Av1ScanOrder(DefaultScan8x8),
            new Av1ScanOrder(DefaultScan8x8),
            new Av1ScanOrder(DefaultScan8x8),
            new Av1ScanOrder(DefaultScan8x8),
            new Av1ScanOrder(DefaultScan8x8),
            new Av1ScanOrder(DefaultScan8x8),
            new Av1ScanOrder(DefaultScan8x8),
            new Av1ScanOrder(DefaultScan8x8),
            new Av1ScanOrder(DefaultScan8x8),
            new Av1ScanOrder(MatrixRowScan8x8),
            new Av1ScanOrder(MatrixColumnScan8x8),
            new Av1ScanOrder(MatrixRowScan8x8),
            new Av1ScanOrder(MatrixColumnScan8x8),
            new Av1ScanOrder(MatrixRowScan8x8),
            new Av1ScanOrder(MatrixColumnScan8x8),
        ],
    ];

    private static readonly short[] DefaultScan4x4 = [0, 1, 4, 8, 5, 2, 3, 6, 9, 12, 13, 10, 7, 11, 14, 15];
    private static readonly short[] MatrixColumnScan4x4 = [0, 4, 8, 12, 1, 5, 9, 13, 2, 6, 10, 14, 3, 7, 11, 15];
    private static readonly short[] MatrixRowScan4x4 = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15];

    private static readonly short[] DefaultScan8x8 = [0,  1,  8,  16, 9,  2,  3,  10, 17, 24, 32, 25, 18, 11, 4,  5,  12, 19, 26, 33, 40, 48,
        41, 34, 27, 20, 13, 6,  7,  14, 21, 28, 35, 42, 49, 56, 57, 50, 43, 36, 29, 22, 15, 23,
        30, 37, 44, 51, 58, 59, 52, 45, 38, 31, 39, 46, 53, 60, 61, 54, 47, 55, 62, 63];

    private static readonly short[] MatrixColumnScan8x8 = [0,  8,  16, 24, 32, 40, 48, 56, 1,  9,  17, 25, 33, 41, 49, 57, 2,  10, 18, 26, 34, 42,
        50, 58, 3,  11, 19, 27, 35, 43, 51, 59, 4,  12, 20, 28, 36, 44, 52, 60, 5,  13, 21, 29,
        37, 45, 53, 61, 6,  14, 22, 30, 38, 46, 54, 62, 7,  15, 23, 31, 39, 47, 55, 63];

    private static readonly short[] MatrixRowScan8x8 = [0,  1,  2,  3,  4,  5,  6,  7,  8,  9,  10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21,
        22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43,
        44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61, 62, 63];

    public static Av1ScanOrder GetScanOrder(Av1TransformSize txSize, Av1TransformMode txMode)
        => ScanOrders[(int)txSize][(int)txMode];
}

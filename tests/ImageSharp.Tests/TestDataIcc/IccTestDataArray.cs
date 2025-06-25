// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Tests.TestDataIcc;

internal static class IccTestDataArray
{
    public static readonly byte[] UInt8 = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9];

    public static readonly object[][] UInt8TestData =
    [
        [UInt8, UInt8]
    ];

    public static readonly ushort[] UInt16Val = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9];

    public static readonly byte[] UInt16Arr = ArrayHelper.Concat(
        IccTestDataPrimitives.UInt160,
        IccTestDataPrimitives.UInt161,
        IccTestDataPrimitives.UInt162,
        IccTestDataPrimitives.UInt163,
        IccTestDataPrimitives.UInt164,
        IccTestDataPrimitives.UInt165,
        IccTestDataPrimitives.UInt166,
        IccTestDataPrimitives.UInt167,
        IccTestDataPrimitives.UInt168,
        IccTestDataPrimitives.UInt169);

    public static readonly object[][] UInt16TestData =
    [
        [UInt16Arr, UInt16Val]
    ];

    public static readonly short[] Int16Val = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9];

    public static readonly byte[] Int16Arr = ArrayHelper.Concat(
        IccTestDataPrimitives.Int160,
        IccTestDataPrimitives.Int161,
        IccTestDataPrimitives.Int162,
        IccTestDataPrimitives.Int163,
        IccTestDataPrimitives.Int164,
        IccTestDataPrimitives.Int165,
        IccTestDataPrimitives.Int166,
        IccTestDataPrimitives.Int167,
        IccTestDataPrimitives.Int168,
        IccTestDataPrimitives.Int169);

    public static readonly object[][] Int16TestData =
    [
        [Int16Arr, Int16Val]
    ];

    public static readonly uint[] UInt32Val = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9];

    public static readonly byte[] UInt32Arr = ArrayHelper.Concat(
        IccTestDataPrimitives.UInt320,
        IccTestDataPrimitives.UInt321,
        IccTestDataPrimitives.UInt322,
        IccTestDataPrimitives.UInt323,
        IccTestDataPrimitives.UInt324,
        IccTestDataPrimitives.UInt325,
        IccTestDataPrimitives.UInt326,
        IccTestDataPrimitives.UInt327,
        IccTestDataPrimitives.UInt328,
        IccTestDataPrimitives.UInt329);

    public static readonly object[][] UInt32TestData =
    [
        [UInt32Arr, UInt32Val]
    ];

    public static readonly int[] Int32Val = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9];

    public static readonly byte[] Int32Arr = ArrayHelper.Concat(
        IccTestDataPrimitives.Int320,
        IccTestDataPrimitives.Int321,
        IccTestDataPrimitives.Int322,
        IccTestDataPrimitives.Int323,
        IccTestDataPrimitives.Int324,
        IccTestDataPrimitives.Int325,
        IccTestDataPrimitives.Int326,
        IccTestDataPrimitives.Int327,
        IccTestDataPrimitives.Int328,
        IccTestDataPrimitives.Int329);

    public static readonly object[][] Int32TestData =
    [
        [Int32Arr, Int32Val]
    ];

    public static readonly ulong[] UInt64Val = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9];

    public static readonly byte[] UInt64Arr = ArrayHelper.Concat(
        IccTestDataPrimitives.UInt640,
        IccTestDataPrimitives.UInt641,
        IccTestDataPrimitives.UInt642,
        IccTestDataPrimitives.UInt643,
        IccTestDataPrimitives.UInt644,
        IccTestDataPrimitives.UInt645,
        IccTestDataPrimitives.UInt646,
        IccTestDataPrimitives.UInt647,
        IccTestDataPrimitives.UInt648,
        IccTestDataPrimitives.UInt649);

    public static readonly object[][] UInt64TestData =
    [
        [UInt64Arr, UInt64Val]
    ];
}

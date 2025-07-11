// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Tests.TestDataIcc.Conversion;

public class IccConversionDataLut
{
    private static readonly float[] LutEven = [0, 0.5f, 1];

    private static readonly float[] LutUneven = [0, 0.7f, 1];

    public static object[][] LutConversionTestData =
    [
        [LutEven, false,  0.5f, 0.5f],
        [LutEven, false,  0.25f, 0.25f],
        [LutEven, false,  0.75f, 0.75f],

        [LutEven, true,  0.5f, 0.5f],
        [LutEven, true,  0.25f, 0.25f],
        [LutEven, true,  0.75f, 0.75f],

        [LutUneven, false, 0.1, 0.14],
        [LutUneven, false, 0.5, 0.7],
        [LutUneven, false, 0.75, 0.85],

        [LutUneven, true, 0.14, 0.1],
        [LutUneven, true, 0.7, 0.5],
        [LutUneven, true, 0.85, 0.75]
    ];
}

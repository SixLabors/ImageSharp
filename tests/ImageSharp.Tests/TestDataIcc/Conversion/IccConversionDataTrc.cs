// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.Tests.TestDataIcc.Conversion;

public static class IccConversionDataTrc
{
    internal static IccCurveTagDataEntry IdentityCurve = new();
    internal static IccCurveTagDataEntry Gamma2Curve = new(2);
    internal static IccCurveTagDataEntry LutCurve = new([0, 0.7f, 1]);

    internal static IccParametricCurveTagDataEntry ParamCurve1 = new(new IccParametricCurve(2.2f));
    internal static IccParametricCurveTagDataEntry ParamCurve2 = new(new IccParametricCurve(2.2f, 1.5f, -0.5f));
    internal static IccParametricCurveTagDataEntry ParamCurve3 = new(new IccParametricCurve(2.2f, 1.5f, -0.5f, 0.3f));
    internal static IccParametricCurveTagDataEntry ParamCurve4 = new(new IccParametricCurve(2.4f, 1 / 1.055f, 0.055f / 1.055f, 1 / 12.92f, 0.04045f));
    internal static IccParametricCurveTagDataEntry ParamCurve5 = new(new IccParametricCurve(2.2f, 0.7f, 0.2f, 0.3f, 0.1f, 0.5f, 0.2f));

    public static object[][] TrcArrayConversionTestData { get; } =
    [
        [
            new IccTagDataEntry[] { IdentityCurve, Gamma2Curve, ParamCurve1 },
            false,
            new Vector4(2, 2, 0.5f, 0),
            new Vector4(2, 4, 0.217637628f, 0)
        ],
        [
            new IccTagDataEntry[] { IdentityCurve, Gamma2Curve, ParamCurve1 },
            true,
            new Vector4(1, 4, 0.217637628f, 0),
            new Vector4(1, 2, 0.5f, 0)
        ]
    ];

    public static object[][] CurveConversionTestData { get; } =
    [
        [IdentityCurve, false, 2, 2],
        [Gamma2Curve, false, 2, 4],
        [LutCurve, false, 0.1, 0.14],
        [LutCurve, false, 0.5, 0.7],
        [LutCurve, false, 0.75, 0.85],

        [IdentityCurve, true, 2, 2],
        [Gamma2Curve, true, 4, 2],
        [LutCurve, true, 0.14, 0.1],
        [LutCurve, true, 0.7, 0.5],
        [LutCurve, true, 0.85, 0.75]
    ];

    public static object[][] ParametricCurveConversionTestData { get; } =
    [
        [ParamCurve1, false, 0.5f, 0.217637628f],
        [ParamCurve2, false, 0.6f, 0.133208528f],
        [ParamCurve2, false, 0.21f, 0],
        [ParamCurve3, false, 0.61f, 0.444446117f],
        [ParamCurve3, false, 0.22f, 0.3f],
        [ParamCurve4, false, 0.3f, 0.0732389539f],
        [ParamCurve4, false, 0.03f, 0.00232198136f],
        [ParamCurve5, false, 0.2f, 0.593165159f],
        [ParamCurve5, false, 0.05f, 0.215f],

        [ParamCurve1, true, 0.217637628f, 0.5f],
        [ParamCurve2, true, 0.133208528f, 0.6f],
        [ParamCurve2, true, 0, 1 / 3f],
        [ParamCurve3, true, 0.444446117f, 0.61f],
        [ParamCurve3, true, 0.3f, 1 / 3f],
        [ParamCurve4, true, 0.0732389539f, 0.3f],
        [ParamCurve4, true, 0.00232198136f, 0.03f],
        [ParamCurve5, true, 0.593165159f, 0.2f],
        [ParamCurve5, true, 0.215f, 0.05f]
    ];
}

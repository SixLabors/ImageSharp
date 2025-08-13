// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.Tests.TestDataIcc.Conversion;

public class IccConversionDataLutAB
{
    private static readonly IccLutAToBTagDataEntry LutAtoBSingleCurve = new(
        [
            IccConversionDataTrc.IdentityCurve,
           IccConversionDataTrc.IdentityCurve,
           IccConversionDataTrc.IdentityCurve
        ],
       null,
       null,
       null,
       null,
       null);

    // also need:
    // # CurveM + matrix
    // # CurveA + CLUT + CurveB
    // # CurveA + CLUT + CurveM + Matrix + CurveB
    private static readonly IccLutBToATagDataEntry LutBtoASingleCurve = new(
        [
            IccConversionDataTrc.IdentityCurve,
           IccConversionDataTrc.IdentityCurve,
           IccConversionDataTrc.IdentityCurve
        ],
       null,
       null,
       null,
       null,
       null);

    public static object[][] LutAToBConversionTestData =
    [
        [LutAtoBSingleCurve, new Vector4(0.2f, 0.3f, 0.4f, 0), new Vector4(0.2f, 0.3f, 0.4f, 0)]
    ];

    public static object[][] LutBToAConversionTestData =
    [
        [LutBtoASingleCurve, new Vector4(0.2f, 0.3f, 0.4f, 0), new Vector4(0.2f, 0.3f, 0.4f, 0)]
    ];
}

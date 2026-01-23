// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.Tests.TestDataIcc.Conversion;

public class IccConversionDataMultiProcessElement
{
    private static readonly IccMatrixProcessElement Matrix = new(
        new float[,]
        {
            { 2, 4, 6 },
            { 3, 5, 7 },
        },
        [3, 4, 5]);

    private static readonly IccClut Clut = new(
        [
            0.2f, 0.3f,
            0.4f, 0.2f,

            0.21f, 0.31f,
            0.41f, 0.51f,

            0.22f, 0.32f,
            0.42f, 0.52f,

            0.23f, 0.33f,
            0.43f, 0.53f
        ],
        [2, 2, 2],
        IccClutDataType.Float,
        outputChannelCount: 2);

    private static readonly IccFormulaCurveElement FormulaCurveElement1 = new(IccFormulaCurveType.Type1, 2.2f, 0.7f, 0.2f, 0.3f, 0, 0);
    private static readonly IccFormulaCurveElement FormulaCurveElement2 = new(IccFormulaCurveType.Type2, 2.2f, 0.9f, 0.9f, 0.02f, 0.1f, 0);
    private static readonly IccFormulaCurveElement FormulaCurveElement3 = new(IccFormulaCurveType.Type3, 0, 0.9f, 0.9f, 1.02f, 0.1f, 0.02f);

    private static readonly IccCurveSetProcessElement CurveSet1DFormula1 = Create1DSingleCurveSet(FormulaCurveElement1);
    private static readonly IccCurveSetProcessElement CurveSet1DFormula2 = Create1DSingleCurveSet(FormulaCurveElement2);
    private static readonly IccCurveSetProcessElement CurveSet1DFormula3 = Create1DSingleCurveSet(FormulaCurveElement3);

    private static readonly IccCurveSetProcessElement CurveSet1DFormula1And2 = Create1DMultiCurveSet([0.5f], FormulaCurveElement1, FormulaCurveElement2);

    private static readonly IccClutProcessElement ClutElement = new(Clut);

    private static IccCurveSetProcessElement Create1DSingleCurveSet(IccCurveSegment segment)
    {
        IccOneDimensionalCurve curve = new([], [segment]);
        return new IccCurveSetProcessElement([curve]);
    }

    private static IccCurveSetProcessElement Create1DMultiCurveSet(float[] breakPoints, params IccCurveSegment[] segments)
    {
        IccOneDimensionalCurve curve = new(breakPoints, segments);
        return new IccCurveSetProcessElement([curve]);
    }

    public static object[][] MpeCurveConversionTestData =
    [
        [CurveSet1DFormula1, new[] { 0.51f }, new[] { 0.575982451f }],
        [CurveSet1DFormula2, new[] { 0.52f }, new[] { -0.4684991f }],
        [CurveSet1DFormula3, new[] { 0.53f }, new[] { 0.86126f }],

        [CurveSet1DFormula1And2, new[] { 0.31f }, new[] { 0.445982f }],
        [CurveSet1DFormula1And2, new[] { 0.61f }, new[] { -0.341274023f }]
    ];

    public static object[][] MpeMatrixConversionTestData =
    [
        [Matrix, new float[] { 2, 4 }, new float[] { 19, 32, 45 }]
    ];

    public static object[][] MpeClutConversionTestData =
    [
        [ClutElement, new[] { 0.5f, 0.5f, 0.5f }, new[] { 0.5f, 0.5f }]
    ];
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.Tests.TestDataIcc;

internal static class IccTestDataMultiProcessElements
{
    /// <summary>
    /// <para>Input Channel Count: 3</para>
    /// <para>Output Channel Count: 3</para>
    /// </summary>
    public static readonly IccCurveSetProcessElement CurvePeValGrad = new(new[]
    {
        IccTestDataCurves.OneDimensionalValFormula1,
        IccTestDataCurves.OneDimensionalValFormula2,
        IccTestDataCurves.OneDimensionalValFormula1
    });

    /// <summary>
    /// <para>Input Channel Count: 3</para>
    /// <para>Output Channel Count: 3</para>
    /// </summary>
    public static readonly byte[] CurvePeGrad = ArrayHelper.Concat(
        IccTestDataCurves.OneDimensionalFormula1,
        IccTestDataCurves.OneDimensionalFormula2,
        IccTestDataCurves.OneDimensionalFormula1);

    public static readonly object[][] CurveSetTestData =
    {
        new object[] { CurvePeGrad, CurvePeValGrad, 3, 3 },
    };

    /// <summary>
    /// <para>Input Channel Count: 3</para>
    /// <para>Output Channel Count: 3</para>
    /// </summary>
    public static readonly IccMatrixProcessElement MatrixPeValGrad = new(
        IccTestDataMatrix.Single2DArrayValGrad,
        IccTestDataMatrix.Single1DArrayValGrad);

    /// <summary>
    /// <para>Input Channel Count: 3</para>
    /// <para>Output Channel Count: 3</para>
    /// </summary>
    public static readonly byte[] MatrixPeGrad = ArrayHelper.Concat(
        IccTestDataMatrix.Single2DGrad,
        IccTestDataMatrix.Single1DGrad);

    public static readonly object[][] MatrixTestData =
    {
        new object[] { MatrixPeGrad, MatrixPeValGrad, 3, 3 },
    };

    /// <summary>
    /// <para>Input Channel Count: 2</para>
    /// <para>Output Channel Count: 3</para>
    /// </summary>
    public static readonly IccClutProcessElement ClutpeValGrad = new(IccTestDataLut.ClutValf32);

    /// <summary>
    /// <para>Input Channel Count: 2</para>
    /// <para>Output Channel Count: 3</para>
    /// </summary>
    public static readonly byte[] ClutpeGrad = IccTestDataLut.ClutF32;

    public static readonly object[][] ClutTestData =
    {
        new object[] { ClutpeGrad, ClutpeValGrad, 2, 3 },
    };

    public static readonly IccMultiProcessElement MpeValMatrix = MatrixPeValGrad;
    public static readonly IccMultiProcessElement MpeValClut = ClutpeValGrad;
    public static readonly IccMultiProcessElement MpeValCurve = CurvePeValGrad;
    public static readonly IccMultiProcessElement MpeValbAcs = new IccBAcsProcessElement(3, 3);
    public static readonly IccMultiProcessElement MpeValeAcs = new IccEAcsProcessElement(3, 3);

    public static readonly byte[] MpeMatrix = ArrayHelper.Concat(
        new byte[]
        {
            0x6D, 0x61, 0x74, 0x66,
            0x00, 0x03,
            0x00, 0x03,
        },
        MatrixPeGrad);

    public static readonly byte[] MpeClut = ArrayHelper.Concat(
        new byte[]
        {
            0x63, 0x6C, 0x75, 0x74,
            0x00, 0x02,
            0x00, 0x03,
        },
        ClutpeGrad);

    public static readonly byte[] MpeCurve = ArrayHelper.Concat(
        new byte[]
        {
            0x6D, 0x66, 0x6C, 0x74,
            0x00, 0x03,
            0x00, 0x03,
        },
        CurvePeGrad);

    public static readonly byte[] MpeBAcs =
    {
        0x62, 0x41, 0x43, 0x53,
        0x00, 0x03,
        0x00, 0x03,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
    };

    public static readonly byte[] MpeEAcs =
    {
        0x65, 0x41, 0x43, 0x53,
        0x00, 0x03,
        0x00, 0x03,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
    };

    public static readonly object[][] MultiProcessElementTestData =
    {
        new object[] { MpeMatrix, MpeValMatrix },
        new object[] { MpeClut, MpeValClut },
        new object[] { MpeCurve, MpeValCurve },
        new object[] { MpeBAcs, MpeValbAcs },
        new object[] { MpeEAcs, MpeValeAcs },
    };
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.Tests.TestDataIcc;

internal static class IccTestDataCurves
{
    /// <summary>
    /// Channels: 3
    /// </summary>
    public static readonly IccResponseCurve ResponseValGrad = new(
        IccCurveMeasurementEncodings.StatusA,
        new[]
        {
            IccTestDataNonPrimitives.XyzNumberValVar1,
            IccTestDataNonPrimitives.XyzNumberValVar2,
            IccTestDataNonPrimitives.XyzNumberValVar3
        },
        new[]
        {
            new[] { IccTestDataNonPrimitives.ResponseNumberVal1, IccTestDataNonPrimitives.ResponseNumberVal2 },
            new[] { IccTestDataNonPrimitives.ResponseNumberVal3, IccTestDataNonPrimitives.ResponseNumberVal4 },
            new[] { IccTestDataNonPrimitives.ResponseNumberVal5, IccTestDataNonPrimitives.ResponseNumberVal6 },
        });

    /// <summary>
    /// Channels: 3
    /// </summary>
    public static readonly byte[] ResponseGrad = ArrayHelper.Concat(
        new byte[] { 0x53, 0x74, 0x61, 0x41 },
        IccTestDataPrimitives.UInt322,
        IccTestDataPrimitives.UInt322,
        IccTestDataPrimitives.UInt322,
        IccTestDataNonPrimitives.XyzNumberVar1,
        IccTestDataNonPrimitives.XyzNumberVar2,
        IccTestDataNonPrimitives.XyzNumberVar3,
        IccTestDataNonPrimitives.ResponseNumber1,
        IccTestDataNonPrimitives.ResponseNumber2,
        IccTestDataNonPrimitives.ResponseNumber3,
        IccTestDataNonPrimitives.ResponseNumber4,
        IccTestDataNonPrimitives.ResponseNumber5,
        IccTestDataNonPrimitives.ResponseNumber6);

    public static readonly object[][] ResponseCurveTestData =
    {
        new object[] { ResponseGrad, ResponseValGrad, 3 },
    };

    public static readonly IccParametricCurve ParametricValVar1 = new(1);
    public static readonly IccParametricCurve ParametricValVar2 = new(1, 2, 3);
    public static readonly IccParametricCurve ParametricValVar3 = new(1, 2, 3, 4);
    public static readonly IccParametricCurve ParametricValVar4 = new(1, 2, 3, 4, 5);
    public static readonly IccParametricCurve ParametricValVar5 = new(1, 2, 3, 4, 5, 6, 7);

    public static readonly byte[] ParametricVar1 = ArrayHelper.Concat(
        new byte[]
        {
            0x00, 0x00,
            0x00, 0x00,
        },
        IccTestDataPrimitives.Fix161);

    public static readonly byte[] ParametricVar2 = ArrayHelper.Concat(
        new byte[]
        {
            0x00, 0x01,
            0x00, 0x00,
        },
        IccTestDataPrimitives.Fix161,
        IccTestDataPrimitives.Fix162,
        IccTestDataPrimitives.Fix163);

    public static readonly byte[] ParametricVar3 = ArrayHelper.Concat(
        new byte[]
        {
            0x00, 0x02,
            0x00, 0x00,
        },
        IccTestDataPrimitives.Fix161,
        IccTestDataPrimitives.Fix162,
        IccTestDataPrimitives.Fix163,
        IccTestDataPrimitives.Fix164);

    public static readonly byte[] ParametricVar4 = ArrayHelper.Concat(
        new byte[]
        {
            0x00, 0x03,
            0x00, 0x00,
        },
        IccTestDataPrimitives.Fix161,
        IccTestDataPrimitives.Fix162,
        IccTestDataPrimitives.Fix163,
        IccTestDataPrimitives.Fix164,
        IccTestDataPrimitives.Fix165);

    public static readonly byte[] ParametricVar5 = ArrayHelper.Concat(
        new byte[]
        {
            0x00, 0x04,
            0x00, 0x00,
        },
        IccTestDataPrimitives.Fix161,
        IccTestDataPrimitives.Fix162,
        IccTestDataPrimitives.Fix163,
        IccTestDataPrimitives.Fix164,
        IccTestDataPrimitives.Fix165,
        IccTestDataPrimitives.Fix166,
        IccTestDataPrimitives.Fix167);

    public static readonly object[][] ParametricCurveTestData =
    {
        new object[] { ParametricVar1, ParametricValVar1 },
        new object[] { ParametricVar2, ParametricValVar2 },
        new object[] { ParametricVar3, ParametricValVar3 },
        new object[] { ParametricVar4, ParametricValVar4 },
        new object[] { ParametricVar5, ParametricValVar5 },
    };

    // Formula Segment
    public static readonly IccFormulaCurveElement FormulaValVar1 = new(IccFormulaCurveType.Type1, 1, 2, 3, 4, 0, 0);
    public static readonly IccFormulaCurveElement FormulaValVar2 = new(IccFormulaCurveType.Type2, 1, 2, 3, 4, 5, 0);
    public static readonly IccFormulaCurveElement FormulaValVar3 = new(IccFormulaCurveType.Type3, 0, 2, 3, 4, 5, 6);

    public static readonly byte[] FormulaVar1 = ArrayHelper.Concat(
        new byte[]
        {
            0x00, 0x00,
            0x00, 0x00,
        },
        IccTestDataPrimitives.Single1,
        IccTestDataPrimitives.Single2,
        IccTestDataPrimitives.Single3,
        IccTestDataPrimitives.Single4);

    public static readonly byte[] FormulaVar2 = ArrayHelper.Concat(
        new byte[]
        {
            0x00, 0x01,
            0x00, 0x00,
        },
        IccTestDataPrimitives.Single1,
        IccTestDataPrimitives.Single2,
        IccTestDataPrimitives.Single3,
        IccTestDataPrimitives.Single4,
        IccTestDataPrimitives.Single5);

    public static readonly byte[] FormulaVar3 = ArrayHelper.Concat(
        new byte[]
        {
            0x00, 0x02,
            0x00, 0x00,
        },
        IccTestDataPrimitives.Single2,
        IccTestDataPrimitives.Single3,
        IccTestDataPrimitives.Single4,
        IccTestDataPrimitives.Single5,
        IccTestDataPrimitives.Single6);

    public static readonly object[][] FormulaCurveSegmentTestData =
    {
        new object[] { FormulaVar1, FormulaValVar1 },
        new object[] { FormulaVar2, FormulaValVar2 },
        new object[] { FormulaVar3, FormulaValVar3 },
    };

    // Sampled Segment
    public static readonly IccSampledCurveElement SampledValGrad1 = new(new float[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 });
    public static readonly IccSampledCurveElement SampledValGrad2 = new(new float[] { 9, 8, 7, 6, 5, 4, 3, 2, 1 });

    public static readonly byte[] SampledGrad1 = ArrayHelper.Concat(
        IccTestDataPrimitives.UInt329,
        IccTestDataPrimitives.Single1,
        IccTestDataPrimitives.Single2,
        IccTestDataPrimitives.Single3,
        IccTestDataPrimitives.Single4,
        IccTestDataPrimitives.Single5,
        IccTestDataPrimitives.Single6,
        IccTestDataPrimitives.Single7,
        IccTestDataPrimitives.Single8,
        IccTestDataPrimitives.Single9);

    public static readonly byte[] SampledGrad2 = ArrayHelper.Concat(
        IccTestDataPrimitives.UInt329,
        IccTestDataPrimitives.Single9,
        IccTestDataPrimitives.Single8,
        IccTestDataPrimitives.Single7,
        IccTestDataPrimitives.Single6,
        IccTestDataPrimitives.Single5,
        IccTestDataPrimitives.Single4,
        IccTestDataPrimitives.Single3,
        IccTestDataPrimitives.Single2,
        IccTestDataPrimitives.Single1);

    public static readonly object[][] SampledCurveSegmentTestData =
    {
        new object[] { SampledGrad1, SampledValGrad1 },
        new object[] { SampledGrad2, SampledValGrad2 },
    };

    public static readonly IccCurveSegment SegmentValFormula1 = FormulaValVar1;
    public static readonly IccCurveSegment SegmentValFormula2 = FormulaValVar2;
    public static readonly IccCurveSegment SegmentValFormula3 = FormulaValVar3;
    public static readonly IccCurveSegment SegmentValSampled1 = SampledValGrad1;
    public static readonly IccCurveSegment SegmentValSampled2 = SampledValGrad2;

    public static readonly byte[] SegmentFormula1 = ArrayHelper.Concat(
        new byte[]
        {
            0x70, 0x61, 0x72, 0x66,
            0x00, 0x00, 0x00, 0x00,
        },
        FormulaVar1);

    public static readonly byte[] SegmentFormula2 = ArrayHelper.Concat(
        new byte[]
        {
            0x70, 0x61, 0x72, 0x66,
            0x00, 0x00, 0x00, 0x00,
        },
        FormulaVar2);

    public static readonly byte[] SegmentFormula3 = ArrayHelper.Concat(
        new byte[]
        {
            0x70, 0x61, 0x72, 0x66,
            0x00, 0x00, 0x00, 0x00,
        },
        FormulaVar3);

    public static readonly byte[] SegmentSampled1 = ArrayHelper.Concat(
        new byte[]
        {
            0x73, 0x61, 0x6D, 0x66,
            0x00, 0x00, 0x00, 0x00,
        },
        SampledGrad1);

    public static readonly byte[] SegmentSampled2 = ArrayHelper.Concat(
        new byte[]
        {
            0x73, 0x61, 0x6D, 0x66,
            0x00, 0x00, 0x00, 0x00,
        },
        SampledGrad2);

    public static readonly object[][] CurveSegmentTestData =
    {
        new object[] { SegmentFormula1, SegmentValFormula1 },
        new object[] { SegmentFormula2, SegmentValFormula2 },
        new object[] { SegmentFormula3, SegmentValFormula3 },
        new object[] { SegmentSampled1, SegmentValSampled1 },
        new object[] { SegmentSampled2, SegmentValSampled2 },
    };

    public static readonly IccOneDimensionalCurve OneDimensionalValFormula1 = new(
        new float[] { 0, 1 },
        new[] { SegmentValFormula1, SegmentValFormula2, SegmentValFormula3 });

    public static readonly IccOneDimensionalCurve OneDimensionalValFormula2 = new(
        new float[] { 0, 1 },
        new[] { SegmentValFormula3, SegmentValFormula2, SegmentValFormula1 });

    public static readonly IccOneDimensionalCurve OneDimensionalValSampled = new(
        new float[] { 0, 1 },
        new[] { SegmentValSampled1, SegmentValSampled2, SegmentValSampled1 });

    public static readonly byte[] OneDimensionalFormula1 = ArrayHelper.Concat(
        new byte[]
        {
            0x00, 0x03,
            0x00, 0x00,
        },
        IccTestDataPrimitives.Single0,
        IccTestDataPrimitives.Single1,
        SegmentFormula1,
        SegmentFormula2,
        SegmentFormula3);

    public static readonly byte[] OneDimensionalFormula2 = ArrayHelper.Concat(
        new byte[]
        {
            0x00, 0x03,
            0x00, 0x00,
        },
        IccTestDataPrimitives.Single0,
        IccTestDataPrimitives.Single1,
        SegmentFormula3,
        SegmentFormula2,
        SegmentFormula1);

    public static readonly byte[] OneDimensionalSampled = ArrayHelper.Concat(
        new byte[]
        {
            0x00, 0x03,
            0x00, 0x00,
        },
        IccTestDataPrimitives.Single0,
        IccTestDataPrimitives.Single1,
        SegmentSampled1,
        SegmentSampled2,
        SegmentSampled1);

    public static readonly object[][] OneDimensionalCurveTestData =
    {
        new object[] { OneDimensionalFormula1, OneDimensionalValFormula1 },
        new object[] { OneDimensionalFormula2, OneDimensionalValFormula2 },
        new object[] { OneDimensionalSampled, OneDimensionalValSampled },
    };
}

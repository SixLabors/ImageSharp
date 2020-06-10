// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.Tests
{
    internal static class IccTestDataCurves
    {
#pragma warning disable SA1118 // Parameter should not span multiple lines
        /// <summary>
        /// Channels: 3
        /// </summary>
        public static readonly IccResponseCurve Response_ValGrad = new IccResponseCurve(
            IccCurveMeasurementEncodings.StatusA,
            new[]
            {
                IccTestDataNonPrimitives.XyzNumber_ValVar1,
                IccTestDataNonPrimitives.XyzNumber_ValVar2,
                IccTestDataNonPrimitives.XyzNumber_ValVar3
            },
            new IccResponseNumber[][]
            {
                new IccResponseNumber[] { IccTestDataNonPrimitives.ResponseNumber_Val1, IccTestDataNonPrimitives.ResponseNumber_Val2 },
                new IccResponseNumber[] { IccTestDataNonPrimitives.ResponseNumber_Val3, IccTestDataNonPrimitives.ResponseNumber_Val4 },
                new IccResponseNumber[] { IccTestDataNonPrimitives.ResponseNumber_Val5, IccTestDataNonPrimitives.ResponseNumber_Val6 },
            });
#pragma warning restore SA1118 // Parameter should not span multiple lines

        /// <summary>
        /// Channels: 3
        /// </summary>
        public static readonly byte[] Response_Grad = ArrayHelper.Concat(
            new byte[] { 0x53, 0x74, 0x61, 0x41 },
            IccTestDataPrimitives.UInt32_2,
            IccTestDataPrimitives.UInt32_2,
            IccTestDataPrimitives.UInt32_2,
            IccTestDataNonPrimitives.XyzNumber_Var1,
            IccTestDataNonPrimitives.XyzNumber_Var2,
            IccTestDataNonPrimitives.XyzNumber_Var3,
            IccTestDataNonPrimitives.ResponseNumber_1,
            IccTestDataNonPrimitives.ResponseNumber_2,
            IccTestDataNonPrimitives.ResponseNumber_3,
            IccTestDataNonPrimitives.ResponseNumber_4,
            IccTestDataNonPrimitives.ResponseNumber_5,
            IccTestDataNonPrimitives.ResponseNumber_6);

        public static readonly object[][] ResponseCurveTestData =
        {
            new object[] { Response_Grad, Response_ValGrad, 3 },
        };

        public static readonly IccParametricCurve Parametric_ValVar1 = new IccParametricCurve(1);
        public static readonly IccParametricCurve Parametric_ValVar2 = new IccParametricCurve(1, 2, 3);
        public static readonly IccParametricCurve Parametric_ValVar3 = new IccParametricCurve(1, 2, 3, 4);
        public static readonly IccParametricCurve Parametric_ValVar4 = new IccParametricCurve(1, 2, 3, 4, 5);
        public static readonly IccParametricCurve Parametric_ValVar5 = new IccParametricCurve(1, 2, 3, 4, 5, 6, 7);

        public static readonly byte[] Parametric_Var1 = ArrayHelper.Concat(
            new byte[]
            {
                0x00, 0x00,
                0x00, 0x00,
            },
            IccTestDataPrimitives.Fix16_1);

        public static readonly byte[] Parametric_Var2 = ArrayHelper.Concat(
            new byte[]
            {
                0x00, 0x01,
                0x00, 0x00,
            },
            IccTestDataPrimitives.Fix16_1,
            IccTestDataPrimitives.Fix16_2,
            IccTestDataPrimitives.Fix16_3);

        public static readonly byte[] Parametric_Var3 = ArrayHelper.Concat(
            new byte[]
            {
                0x00, 0x02,
                0x00, 0x00,
            },
            IccTestDataPrimitives.Fix16_1,
            IccTestDataPrimitives.Fix16_2,
            IccTestDataPrimitives.Fix16_3,
            IccTestDataPrimitives.Fix16_4);

        public static readonly byte[] Parametric_Var4 = ArrayHelper.Concat(
            new byte[]
            {
                0x00, 0x03,
                0x00, 0x00,
            },
            IccTestDataPrimitives.Fix16_1,
            IccTestDataPrimitives.Fix16_2,
            IccTestDataPrimitives.Fix16_3,
            IccTestDataPrimitives.Fix16_4,
            IccTestDataPrimitives.Fix16_5);

        public static readonly byte[] Parametric_Var5 = ArrayHelper.Concat(
            new byte[]
            {
                0x00, 0x04,
                0x00, 0x00,
            },
            IccTestDataPrimitives.Fix16_1,
            IccTestDataPrimitives.Fix16_2,
            IccTestDataPrimitives.Fix16_3,
            IccTestDataPrimitives.Fix16_4,
            IccTestDataPrimitives.Fix16_5,
            IccTestDataPrimitives.Fix16_6,
            IccTestDataPrimitives.Fix16_7);

        public static readonly object[][] ParametricCurveTestData =
        {
            new object[] { Parametric_Var1, Parametric_ValVar1 },
            new object[] { Parametric_Var2, Parametric_ValVar2 },
            new object[] { Parametric_Var3, Parametric_ValVar3 },
            new object[] { Parametric_Var4, Parametric_ValVar4 },
            new object[] { Parametric_Var5, Parametric_ValVar5 },
        };

        // Formula Segment
        public static readonly IccFormulaCurveElement Formula_ValVar1 = new IccFormulaCurveElement(IccFormulaCurveType.Type1, 1, 2, 3, 4, 0, 0);
        public static readonly IccFormulaCurveElement Formula_ValVar2 = new IccFormulaCurveElement(IccFormulaCurveType.Type2, 1, 2, 3, 4, 5, 0);
        public static readonly IccFormulaCurveElement Formula_ValVar3 = new IccFormulaCurveElement(IccFormulaCurveType.Type3, 0, 2, 3, 4, 5, 6);

        public static readonly byte[] Formula_Var1 = ArrayHelper.Concat(
            new byte[]
            {
                0x00, 0x00,
                0x00, 0x00,
            },
            IccTestDataPrimitives.Single_1,
            IccTestDataPrimitives.Single_2,
            IccTestDataPrimitives.Single_3,
            IccTestDataPrimitives.Single_4);

        public static readonly byte[] Formula_Var2 = ArrayHelper.Concat(
            new byte[]
            {
                0x00, 0x01,
                0x00, 0x00,
            },
            IccTestDataPrimitives.Single_1,
            IccTestDataPrimitives.Single_2,
            IccTestDataPrimitives.Single_3,
            IccTestDataPrimitives.Single_4,
            IccTestDataPrimitives.Single_5);

        public static readonly byte[] Formula_Var3 = ArrayHelper.Concat(
            new byte[]
            {
                0x00, 0x02,
                0x00, 0x00,
            },
            IccTestDataPrimitives.Single_2,
            IccTestDataPrimitives.Single_3,
            IccTestDataPrimitives.Single_4,
            IccTestDataPrimitives.Single_5,
            IccTestDataPrimitives.Single_6);

        public static readonly object[][] FormulaCurveSegmentTestData =
        {
            new object[] { Formula_Var1, Formula_ValVar1 },
            new object[] { Formula_Var2, Formula_ValVar2 },
            new object[] { Formula_Var3, Formula_ValVar3 },
        };

        // Sampled Segment
        public static readonly IccSampledCurveElement Sampled_ValGrad1 = new IccSampledCurveElement(new float[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 });
        public static readonly IccSampledCurveElement Sampled_ValGrad2 = new IccSampledCurveElement(new float[] { 9, 8, 7, 6, 5, 4, 3, 2, 1 });

        public static readonly byte[] Sampled_Grad1 = ArrayHelper.Concat(
            IccTestDataPrimitives.UInt32_9,
            IccTestDataPrimitives.Single_1,
            IccTestDataPrimitives.Single_2,
            IccTestDataPrimitives.Single_3,
            IccTestDataPrimitives.Single_4,
            IccTestDataPrimitives.Single_5,
            IccTestDataPrimitives.Single_6,
            IccTestDataPrimitives.Single_7,
            IccTestDataPrimitives.Single_8,
            IccTestDataPrimitives.Single_9);

        public static readonly byte[] Sampled_Grad2 = ArrayHelper.Concat(
            IccTestDataPrimitives.UInt32_9,
            IccTestDataPrimitives.Single_9,
            IccTestDataPrimitives.Single_8,
            IccTestDataPrimitives.Single_7,
            IccTestDataPrimitives.Single_6,
            IccTestDataPrimitives.Single_5,
            IccTestDataPrimitives.Single_4,
            IccTestDataPrimitives.Single_3,
            IccTestDataPrimitives.Single_2,
            IccTestDataPrimitives.Single_1);

        public static readonly object[][] SampledCurveSegmentTestData =
        {
            new object[] { Sampled_Grad1, Sampled_ValGrad1 },
            new object[] { Sampled_Grad2, Sampled_ValGrad2 },
        };

        public static readonly IccCurveSegment Segment_ValFormula1 = Formula_ValVar1;
        public static readonly IccCurveSegment Segment_ValFormula2 = Formula_ValVar2;
        public static readonly IccCurveSegment Segment_ValFormula3 = Formula_ValVar3;
        public static readonly IccCurveSegment Segment_ValSampled1 = Sampled_ValGrad1;
        public static readonly IccCurveSegment Segment_ValSampled2 = Sampled_ValGrad2;

        public static readonly byte[] Segment_Formula1 = ArrayHelper.Concat(
            new byte[]
            {
                0x70, 0x61, 0x72, 0x66,
                0x00, 0x00, 0x00, 0x00,
            },
            Formula_Var1);

        public static readonly byte[] Segment_Formula2 = ArrayHelper.Concat(
            new byte[]
            {
                0x70, 0x61, 0x72, 0x66,
                0x00, 0x00, 0x00, 0x00,
            },
            Formula_Var2);

        public static readonly byte[] Segment_Formula3 = ArrayHelper.Concat(
            new byte[]
            {
                0x70, 0x61, 0x72, 0x66,
                0x00, 0x00, 0x00, 0x00,
            },
            Formula_Var3);

        public static readonly byte[] Segment_Sampled1 = ArrayHelper.Concat(
            new byte[]
            {
                0x73, 0x61, 0x6D, 0x66,
                0x00, 0x00, 0x00, 0x00,
            },
            Sampled_Grad1);

        public static readonly byte[] Segment_Sampled2 = ArrayHelper.Concat(
            new byte[]
            {
                0x73, 0x61, 0x6D, 0x66,
                0x00, 0x00, 0x00, 0x00,
            },
            Sampled_Grad2);

        public static readonly object[][] CurveSegmentTestData =
        {
            new object[] { Segment_Formula1, Segment_ValFormula1 },
            new object[] { Segment_Formula2, Segment_ValFormula2 },
            new object[] { Segment_Formula3, Segment_ValFormula3 },
            new object[] { Segment_Sampled1, Segment_ValSampled1 },
            new object[] { Segment_Sampled2, Segment_ValSampled2 },
        };

        public static readonly IccOneDimensionalCurve OneDimensional_ValFormula1 = new IccOneDimensionalCurve(
            new float[] { 0, 1 },
            new IccCurveSegment[] { Segment_ValFormula1, Segment_ValFormula2, Segment_ValFormula3 });

        public static readonly IccOneDimensionalCurve OneDimensional_ValFormula2 = new IccOneDimensionalCurve(
            new float[] { 0, 1 },
            new IccCurveSegment[] { Segment_ValFormula3, Segment_ValFormula2, Segment_ValFormula1 });

        public static readonly IccOneDimensionalCurve OneDimensional_ValSampled = new IccOneDimensionalCurve(
            new float[] { 0, 1 },
            new IccCurveSegment[] { Segment_ValSampled1, Segment_ValSampled2, Segment_ValSampled1 });

        public static readonly byte[] OneDimensional_Formula1 = ArrayHelper.Concat(
            new byte[]
            {
                0x00, 0x03,
                0x00, 0x00,
            },
            IccTestDataPrimitives.Single_0,
            IccTestDataPrimitives.Single_1,
            Segment_Formula1,
            Segment_Formula2,
            Segment_Formula3);

        public static readonly byte[] OneDimensional_Formula2 = ArrayHelper.Concat(
            new byte[]
            {
                0x00, 0x03,
                0x00, 0x00,
            },
            IccTestDataPrimitives.Single_0,
            IccTestDataPrimitives.Single_1,
            Segment_Formula3,
            Segment_Formula2,
            Segment_Formula1);

        public static readonly byte[] OneDimensional_Sampled = ArrayHelper.Concat(
            new byte[]
            {
                0x00, 0x03,
                0x00, 0x00,
            },
            IccTestDataPrimitives.Single_0,
            IccTestDataPrimitives.Single_1,
            Segment_Sampled1,
            Segment_Sampled2,
            Segment_Sampled1);

        public static readonly object[][] OneDimensionalCurveTestData =
        {
            new object[] { OneDimensional_Formula1, OneDimensional_ValFormula1 },
            new object[] { OneDimensional_Formula2, OneDimensional_ValFormula2 },
            new object[] { OneDimensional_Sampled, OneDimensional_ValSampled },
        };
    }
}

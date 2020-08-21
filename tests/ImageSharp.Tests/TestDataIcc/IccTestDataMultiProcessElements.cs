// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.Tests
{
    internal static class IccTestDataMultiProcessElements
    {
        /// <summary>
        /// <para>Input Channel Count: 3</para>
        /// <para>Output Channel Count: 3</para>
        /// </summary>
        public static readonly IccCurveSetProcessElement CurvePE_ValGrad = new IccCurveSetProcessElement(new IccOneDimensionalCurve[]
        {
            IccTestDataCurves.OneDimensional_ValFormula1,
            IccTestDataCurves.OneDimensional_ValFormula2,
            IccTestDataCurves.OneDimensional_ValFormula1
        });

        /// <summary>
        /// <para>Input Channel Count: 3</para>
        /// <para>Output Channel Count: 3</para>
        /// </summary>
        public static readonly byte[] CurvePE_Grad = ArrayHelper.Concat(
            IccTestDataCurves.OneDimensional_Formula1,
            IccTestDataCurves.OneDimensional_Formula2,
            IccTestDataCurves.OneDimensional_Formula1);

        public static readonly object[][] CurveSetTestData =
        {
            new object[] { CurvePE_Grad, CurvePE_ValGrad, 3, 3 },
        };

        /// <summary>
        /// <para>Input Channel Count: 3</para>
        /// <para>Output Channel Count: 3</para>
        /// </summary>
        public static readonly IccMatrixProcessElement MatrixPE_ValGrad = new IccMatrixProcessElement(
            IccTestDataMatrix.Single_2DArray_ValGrad,
            IccTestDataMatrix.Single_1DArray_ValGrad);

        /// <summary>
        /// <para>Input Channel Count: 3</para>
        /// <para>Output Channel Count: 3</para>
        /// </summary>
        public static readonly byte[] MatrixPE_Grad = ArrayHelper.Concat(
            IccTestDataMatrix.Single_2D_Grad,
            IccTestDataMatrix.Single_1D_Grad);

        public static readonly object[][] MatrixTestData =
        {
            new object[] { MatrixPE_Grad, MatrixPE_ValGrad, 3, 3 },
        };

        /// <summary>
        /// <para>Input Channel Count: 2</para>
        /// <para>Output Channel Count: 3</para>
        /// </summary>
        public static readonly IccClutProcessElement CLUTPE_ValGrad = new IccClutProcessElement(IccTestDataLut.CLUT_Valf32);

        /// <summary>
        /// <para>Input Channel Count: 2</para>
        /// <para>Output Channel Count: 3</para>
        /// </summary>
        public static readonly byte[] CLUTPE_Grad = IccTestDataLut.CLUT_f32;

        public static readonly object[][] ClutTestData =
        {
            new object[] { CLUTPE_Grad, CLUTPE_ValGrad, 2, 3 },
        };

        public static readonly IccMultiProcessElement MPE_ValMatrix = MatrixPE_ValGrad;
        public static readonly IccMultiProcessElement MPE_ValCLUT = CLUTPE_ValGrad;
        public static readonly IccMultiProcessElement MPE_ValCurve = CurvePE_ValGrad;
        public static readonly IccMultiProcessElement MPE_ValbACS = new IccBAcsProcessElement(3, 3);
        public static readonly IccMultiProcessElement MPE_ValeACS = new IccEAcsProcessElement(3, 3);

        public static readonly byte[] MPE_Matrix = ArrayHelper.Concat(
            new byte[]
            {
                0x6D, 0x61, 0x74, 0x66,
                0x00, 0x03,
                0x00, 0x03,
            },
            MatrixPE_Grad);

        public static readonly byte[] MPE_CLUT = ArrayHelper.Concat(
            new byte[]
            {
                0x63, 0x6C, 0x75, 0x74,
                0x00, 0x02,
                0x00, 0x03,
            },
            CLUTPE_Grad);

        public static readonly byte[] MPE_Curve = ArrayHelper.Concat(
            new byte[]
            {
                0x6D, 0x66, 0x6C, 0x74,
                0x00, 0x03,
                0x00, 0x03,
            },
            CurvePE_Grad);

        public static readonly byte[] MPE_bACS =
        {
            0x62, 0x41, 0x43, 0x53,
            0x00, 0x03,
            0x00, 0x03,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        };

        public static readonly byte[] MPE_eACS =
        {
            0x65, 0x41, 0x43, 0x53,
            0x00, 0x03,
            0x00, 0x03,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        };

        public static readonly object[][] MultiProcessElementTestData =
        {
            new object[] { MPE_Matrix, MPE_ValMatrix },
            new object[] { MPE_CLUT, MPE_ValCLUT },
            new object[] { MPE_Curve, MPE_ValCurve },
            new object[] { MPE_bACS, MPE_ValbACS },
            new object[] { MPE_eACS, MPE_ValeACS },
        };
    }
}

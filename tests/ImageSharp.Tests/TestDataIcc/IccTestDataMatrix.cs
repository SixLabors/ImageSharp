// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.Memory;

namespace SixLabors.ImageSharp.Tests
{
    using SixLabors.ImageSharp.Primitives;

    internal static class IccTestDataMatrix
    {
        #region 2D

        /// <summary>
        /// 3x3 Matrix
        /// </summary>
        public static readonly float[,] Single_2DArray_ValGrad =
        {
            { 1, 2, 3 },
            { 4, 5, 6 },
            { 7, 8, 9 },
        };
        /// <summary>
        /// 3x3 Matrix
        /// </summary>
        public static readonly float[,] Single_2DArray_ValIdentity =
        {
            { 1, 0, 0 },
            { 0, 1, 0 },
            { 0, 0, 1 },
        };

        /// <summary>
        /// 3x3 Matrix
        /// </summary>
        public static readonly Matrix4x4 Single_Matrix4x4_ValGrad = new Matrix4x4(1, 2, 3, 0, 4, 5, 6, 0, 7, 8, 9, 0, 0, 0, 0, 1);

        /// <summary>
        /// 3x3 Matrix
        /// </summary>
        public static readonly Matrix4x4 Single_Matrix4x4_ValIdentity = Matrix4x4.Identity;

        /// <summary>
        /// 3x3 Matrix
        /// </summary>
        public static readonly DenseMatrix<float> Single_DenseMatrix_ValGrad = new DenseMatrix<float>(Single_2DArray_ValGrad);

        /// <summary>
        /// 3x3 Matrix
        /// </summary>
        public static readonly DenseMatrix<float> Single_DenseMatrix_ValIdentity = new DenseMatrix<float>(Single_2DArray_ValIdentity);

        /// <summary>
        /// 3x3 Matrix
        /// </summary>
        public static readonly byte[] Fix16_2D_Grad = ArrayHelper.Concat
        (
            IccTestDataPrimitives.Fix16_1,
            IccTestDataPrimitives.Fix16_4,
            IccTestDataPrimitives.Fix16_7,

            IccTestDataPrimitives.Fix16_2,
            IccTestDataPrimitives.Fix16_5,
            IccTestDataPrimitives.Fix16_8,

            IccTestDataPrimitives.Fix16_3,
            IccTestDataPrimitives.Fix16_6,
            IccTestDataPrimitives.Fix16_9
        );

        /// <summary>
        /// 3x3 Matrix
        /// </summary>
        public static readonly byte[] Fix16_2D_Identity = ArrayHelper.Concat
        (
            IccTestDataPrimitives.Fix16_1,
            IccTestDataPrimitives.Fix16_0,
            IccTestDataPrimitives.Fix16_0,

            IccTestDataPrimitives.Fix16_0,
            IccTestDataPrimitives.Fix16_1,
            IccTestDataPrimitives.Fix16_0,

            IccTestDataPrimitives.Fix16_0,
            IccTestDataPrimitives.Fix16_0,
            IccTestDataPrimitives.Fix16_1
        );

        /// <summary>
        /// 3x3 Matrix
        /// </summary>
        public static readonly byte[] Single_2D_Grad = ArrayHelper.Concat
        (
            IccTestDataPrimitives.Single_1,
            IccTestDataPrimitives.Single_4,
            IccTestDataPrimitives.Single_7,

            IccTestDataPrimitives.Single_2,
            IccTestDataPrimitives.Single_5,
            IccTestDataPrimitives.Single_8,

            IccTestDataPrimitives.Single_3,
            IccTestDataPrimitives.Single_6,
            IccTestDataPrimitives.Single_9
        );

        public static readonly object[][] Matrix2D_FloatArrayTestData =
        {
            new object[] { Fix16_2D_Grad, 3, 3, false, Single_2DArray_ValGrad },
            new object[] { Fix16_2D_Identity, 3, 3, false, Single_2DArray_ValIdentity },
            new object[] { Single_2D_Grad, 3, 3, true, Single_2DArray_ValGrad },
        };

        public static readonly object[][] Matrix2D_DenseMatrixTestData =
        {
            new object[] { Fix16_2D_Grad, 3, 3, false, Single_DenseMatrix_ValGrad },
            new object[] { Fix16_2D_Identity, 3, 3, false, Single_DenseMatrix_ValIdentity },
            new object[] { Single_2D_Grad, 3, 3, true, Single_DenseMatrix_ValGrad },
        };

        public static readonly object[][] Matrix2D_Matrix4x4TestData =
        {
            new object[] { Fix16_2D_Grad, 3, 3, false, Single_Matrix4x4_ValGrad },
            new object[] { Fix16_2D_Identity, 3, 3, false, Single_Matrix4x4_ValIdentity },
            new object[] { Single_2D_Grad, 3, 3, true, Single_Matrix4x4_ValGrad },
        };

        #endregion

        #region 1D

        /// <summary>
        /// 3x1 Matrix
        /// </summary>
        public static readonly float[] Single_1DArray_ValGrad = { 1, 4, 7 };
        /// <summary>
        /// 3x1 Matrix
        /// </summary>
        public static readonly Vector3 Single_Vector3_ValGrad = new Vector3(1, 4, 7);

        /// <summary>
        /// 3x1 Matrix
        /// </summary>
        public static readonly byte[] Fix16_1D_Grad = ArrayHelper.Concat
        (
            IccTestDataPrimitives.Fix16_1,
            IccTestDataPrimitives.Fix16_4,
            IccTestDataPrimitives.Fix16_7
        );

        /// <summary>
        /// 3x1 Matrix
        /// </summary>
        public static readonly byte[] Single_1D_Grad = ArrayHelper.Concat
        (
            IccTestDataPrimitives.Single_1,
            IccTestDataPrimitives.Single_4,
            IccTestDataPrimitives.Single_7
        );

        public static readonly object[][] Matrix1D_ArrayTestData =
        {
            new object[] { Fix16_1D_Grad, 3, false, Single_1DArray_ValGrad },
            new object[] { Single_1D_Grad, 3, true, Single_1DArray_ValGrad },
        };

        public static readonly object[][] Matrix1D_Vector3TestData =
        {
            new object[] { Fix16_1D_Grad, 3, false, Single_Vector3_ValGrad },
            new object[] { Single_1D_Grad, 3, true, Single_Vector3_ValGrad },
        };

        #endregion
    }
}

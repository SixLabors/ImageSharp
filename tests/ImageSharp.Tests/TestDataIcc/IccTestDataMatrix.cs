// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;

namespace SixLabors.ImageSharp.Tests.TestDataIcc;

internal static class IccTestDataMatrix
{
    /// <summary>
    /// 3x3 Matrix
    /// </summary>
    public static readonly float[,] Single2DArrayValGrad =
    {
        { 1, 2, 3 },
        { 4, 5, 6 },
        { 7, 8, 9 },
    };

    /// <summary>
    /// 3x3 Matrix
    /// </summary>
    public static readonly float[,] Single2DArrayValIdentity =
    {
        { 1, 0, 0 },
        { 0, 1, 0 },
        { 0, 0, 1 },
    };

    /// <summary>
    /// 3x3 Matrix
    /// </summary>
    public static readonly Matrix4x4 SingleMatrix4X4ValGrad = new(1, 2, 3, 0, 4, 5, 6, 0, 7, 8, 9, 0, 0, 0, 0, 1);

    /// <summary>
    /// 3x3 Matrix
    /// </summary>
    public static readonly Matrix4x4 SingleMatrix4X4ValIdentity = Matrix4x4.Identity;

    /// <summary>
    /// 3x3 Matrix
    /// </summary>
    public static readonly DenseMatrix<float> SingleDenseMatrixValGrad = new(Single2DArrayValGrad);

    /// <summary>
    /// 3x3 Matrix
    /// </summary>
    public static readonly DenseMatrix<float> SingleDenseMatrixValIdentity = new(Single2DArrayValIdentity);

    /// <summary>
    /// 3x3 Matrix
    /// </summary>
    public static readonly byte[] Fix162DGrad = ArrayHelper.Concat(
        IccTestDataPrimitives.Fix161,
        IccTestDataPrimitives.Fix164,
        IccTestDataPrimitives.Fix167,
        IccTestDataPrimitives.Fix162,
        IccTestDataPrimitives.Fix165,
        IccTestDataPrimitives.Fix168,
        IccTestDataPrimitives.Fix163,
        IccTestDataPrimitives.Fix166,
        IccTestDataPrimitives.Fix169);

    /// <summary>
    /// 3x3 Matrix
    /// </summary>
    public static readonly byte[] Fix162DIdentity = ArrayHelper.Concat(
        IccTestDataPrimitives.Fix161,
        IccTestDataPrimitives.Fix160,
        IccTestDataPrimitives.Fix160,
        IccTestDataPrimitives.Fix160,
        IccTestDataPrimitives.Fix161,
        IccTestDataPrimitives.Fix160,
        IccTestDataPrimitives.Fix160,
        IccTestDataPrimitives.Fix160,
        IccTestDataPrimitives.Fix161);

    /// <summary>
    /// 3x3 Matrix
    /// </summary>
    public static readonly byte[] Single2DGrad = ArrayHelper.Concat(
        IccTestDataPrimitives.Single1,
        IccTestDataPrimitives.Single4,
        IccTestDataPrimitives.Single7,
        IccTestDataPrimitives.Single2,
        IccTestDataPrimitives.Single5,
        IccTestDataPrimitives.Single8,
        IccTestDataPrimitives.Single3,
        IccTestDataPrimitives.Single6,
        IccTestDataPrimitives.Single9);

    public static readonly object[][] Matrix2DFloatArrayTestData =
    {
        new object[] { Fix162DGrad, 3, 3, false, Single2DArrayValGrad },
        new object[] { Fix162DIdentity, 3, 3, false, Single2DArrayValIdentity },
        new object[] { Single2DGrad, 3, 3, true, Single2DArrayValGrad },
    };

    public static readonly object[][] Matrix2DDenseMatrixTestData =
    {
        new object[] { Fix162DGrad, 3, 3, false, SingleDenseMatrixValGrad },
        new object[] { Fix162DIdentity, 3, 3, false, SingleDenseMatrixValIdentity },
        new object[] { Single2DGrad, 3, 3, true, SingleDenseMatrixValGrad },
    };

    public static readonly object[][] Matrix2DMatrix4X4TestData =
    {
        new object[] { Fix162DGrad, 3, 3, false, SingleMatrix4X4ValGrad },
        new object[] { Fix162DIdentity, 3, 3, false, SingleMatrix4X4ValIdentity },
        new object[] { Single2DGrad, 3, 3, true, SingleMatrix4X4ValGrad },
    };

    /// <summary>
    /// 3x1 Matrix
    /// </summary>
    public static readonly float[] Single1DArrayValGrad = { 1, 4, 7 };

    /// <summary>
    /// 3x1 Matrix
    /// </summary>
    public static readonly Vector3 SingleVector3ValGrad = new(1, 4, 7);

    /// <summary>
    /// 3x1 Matrix
    /// </summary>
    public static readonly byte[] Fix161DGrad = ArrayHelper.Concat(
        IccTestDataPrimitives.Fix161,
        IccTestDataPrimitives.Fix164,
        IccTestDataPrimitives.Fix167);

    /// <summary>
    /// 3x1 Matrix
    /// </summary>
    public static readonly byte[] Single1DGrad = ArrayHelper.Concat(
        IccTestDataPrimitives.Single1,
        IccTestDataPrimitives.Single4,
        IccTestDataPrimitives.Single7);

    public static readonly object[][] Matrix1DArrayTestData =
    {
        new object[] { Fix161DGrad, 3, false, Single1DArrayValGrad },
        new object[] { Single1DGrad, 3, true, Single1DArrayValGrad },
    };

    public static readonly object[][] Matrix1DVector3TestData =
    {
        new object[] { Fix161DGrad, 3, false, SingleVector3ValGrad },
        new object[] { Single1DGrad, 3, true, SingleVector3ValGrad },
    };
}

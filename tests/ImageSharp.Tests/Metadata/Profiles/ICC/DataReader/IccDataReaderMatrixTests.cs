// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.Tests.Metadata.Profiles.ICC.DataReader;

[Trait("Profile", "Icc")]
public class IccDataReaderMatrixTests
{
    [Theory]
    [MemberData(nameof(IccTestDataMatrix.Matrix2D_FloatArrayTestData), MemberType = typeof(IccTestDataMatrix))]
    public void ReadMatrix2D(byte[] data, int xCount, int yCount, bool isSingle, float[,] expected)
    {
        IccDataReader reader = CreateReader(data);

        float[,] output = reader.ReadMatrix(xCount, yCount, isSingle);

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataMatrix.Matrix1D_ArrayTestData), MemberType = typeof(IccTestDataMatrix))]
    public void ReadMatrix1D(byte[] data, int yCount, bool isSingle, float[] expected)
    {
        IccDataReader reader = CreateReader(data);

        float[] output = reader.ReadMatrix(yCount, isSingle);

        Assert.Equal(expected, output);
    }

    private static IccDataReader CreateReader(byte[] data)
    {
        return new(data);
    }
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

[Trait("Format", "Avif")]
public class Av1PredictorTests
{
    [Theory]
    [MemberData(nameof(GetTransformSizes))]
    public void VerifyDcFill(int width, int height)
    {
        // Assign
        byte[] destination = new byte[width * height];
        byte[] left = new byte[1];
        byte[] above = new byte[1];
        byte expected = 0x80;

        // Act
        Av1DcFillPredictor predictor = new(new Size(width, height));
        predictor.PredictScalar(destination, (nuint)width, above, left);

        // Assert
        Assert.All(destination, (b) => AssertValue(expected, b));
    }

    [Theory]
    [MemberData(nameof(GetTransformSizes))]
    public void VerifyDc(int width, int height)
    {
        // Assign
        byte[] destination = new byte[width * height];
        byte[] left = new byte[height];
        byte[] above = new byte[width];
        Array.Fill(left, (byte)5);
        Array.Fill(above, (byte)28);
        int count = width + height;
        int sum = Sum(left, height) + Sum(above, width);
        byte expected = (byte)((sum + (count >> 1)) / count);

        // Act
        Av1DcPredictor predictor = new(new Size(width, height));
        predictor.PredictScalar(destination, (nuint)width, above, left);

        // Assert
        Assert.Equal((5 * height) + (28 * width), sum);
        Assert.All(destination, (b) => AssertValue(expected, b));
    }

    [Theory]
    [MemberData(nameof(GetTransformSizes))]
    public void VerifyDcLeft(int width, int height)
    {
        // Assign
        byte[] destination = new byte[width * height];
        byte[] left = new byte[height];
        byte[] above = new byte[width];
        Array.Fill(left, (byte)5);
        Array.Fill(above, (byte)28);
        byte expected = left[0];

        // Act
        Av1DcLeftPredictor predictor = new(new Size(width, height));
        predictor.PredictScalar(destination, (nuint)width, above, left);

        // Assert
        Assert.All(destination, (b) => AssertValue(expected, b));
    }

    [Theory]
    [MemberData(nameof(GetTransformSizes))]
    public void VerifyDcTop(int width, int height)
    {
        // Assign
        byte[] destination = new byte[width * height];
        byte[] left = new byte[height];
        byte[] above = new byte[width];
        Array.Fill(left, (byte)5);
        Array.Fill(above, (byte)28);
        byte expected = above[0];

        // Act
        Av1DcTopPredictor predictor = new(new Size(width, height));
        predictor.PredictScalar(destination, (nuint)width, above, left);

        // Assert
        Assert.All(destination, (b) => AssertValue(expected, b));
    }

    private static void AssertValue(byte expected, byte actual)
    {
        Assert.NotEqual(0, actual);
        Assert.Equal(expected, actual);
    }

    private static int Sum(Span<byte> values, int length)
    {
        int sum = 0;
        for (int i = 0; i < length; i++)
        {
            sum += values[i];
        }

        return sum;
    }

    public static TheoryData<int, int> GetTransformSizes()
    {
        TheoryData<int, int> combinations = [];
        for (int s = 0; s < (int)Av1TransformSize.AllSizes; s++)
        {
            Av1TransformSize size = (Av1TransformSize)s;
            int width = size.GetWidth();
            int height = size.GetHeight();
            combinations.Add(width, height);
        }

        return combinations;
    }
}

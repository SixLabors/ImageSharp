// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Forward;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Inverse;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// SVT: test/InvTxfm1dTest.cc
/// SVT: test/InvTxfm2dAsmTest.cc
/// </summary>
[Trait("Format", "Avif")]
public class Av1InverseTransformTests
{
    [Fact]
    public void AccuracyOfDct1dTransformSize4Test()
        => AssertAccuracy1d(Av1TransformType.DctDct, Av1TransformSize.Size4x4, 1);

    // [Fact]
    public void AccuracyOfDct1dTransformSize8Test()
        => AssertAccuracy1d(Av1TransformType.DctDct, Av1TransformSize.Size8x8, 1, 2);

    // [Fact]
    public void AccuracyOfDct1dTransformSize16Test()
        => AssertAccuracy1d(Av1TransformType.DctDct, Av1TransformSize.Size16x16, 1, 3);

    // [Fact]
    public void AccuracyOfDct1dTransformSize32Test()
        => AssertAccuracy1d(Av1TransformType.DctDct, Av1TransformSize.Size32x32, 1, 4);

    // [Fact]
    public void AccuracyOfDct1dTransformSize64Test()
        => AssertAccuracy1d(Av1TransformType.DctDct, Av1TransformSize.Size64x64, 1, 5);

    [Fact]
    public void AccuracyOfAdst1dTransformSize4Test()
        => AssertAccuracy1d(Av1TransformType.AdstAdst, Av1TransformSize.Size4x4, 1);

    // [Fact]
    public void AccuracyOfAdst1dTransformSize8Test()
        => AssertAccuracy1d(Av1TransformType.AdstAdst, Av1TransformSize.Size8x8, 1, 2);

    // [Fact]
    public void AccuracyOfAdst1dTransformSize16Test()
        => AssertAccuracy1d(Av1TransformType.AdstAdst, Av1TransformSize.Size16x16, 1, 3);

    // [Fact]
    public void AccuracyOfAdst1dTransformSize32Test()
        => AssertAccuracy1d(Av1TransformType.AdstAdst, Av1TransformSize.Size32x32, 1, 3);

    [Fact]
    public void AccuracyOfIdentity1dTransformSize4Test()
        => AssertAccuracy1d(Av1TransformType.Identity, Av1TransformSize.Size4x4, 1);

    [Fact]
    public void AccuracyOfIdentity1dTransformSize8Test()
        => AssertAccuracy1d(Av1TransformType.Identity, Av1TransformSize.Size8x8, 2);

    [Fact]
    public void AccuracyOfIdentity1dTransformSize16Test()
        => AssertAccuracy1d(Av1TransformType.Identity, Av1TransformSize.Size16x16, 1);

    [Fact]
    public void AccuracyOfIdentity1dTransformSize32Test()
        => AssertAccuracy1d(Av1TransformType.Identity, Av1TransformSize.Size32x32, 4);

    [Fact]
    public void AccuracyOfIdentity1dTransformSize64Test()
        => AssertAccuracy1d(Av1TransformType.Identity, Av1TransformSize.Size64x64, 4);

    [Fact]
    public void AccuracyOfEchoTransformSize4Test()
        => AssertAccuracy1d(Av1TransformType.Identity, Av1TransformSize.Size4x4, 0, new EchoTestTransformer(), new EchoTestTransformer());

    private static void AssertAccuracy1d(
        Av1TransformType transformType,
        Av1TransformSize transformSize,
        int scaleLog2,
        int allowedError = 1)
    {
        Av1Transform2dFlipConfiguration config = new(transformType, transformSize);
        IAv1Forward1dTransformer forward = GetForwardTransformer(config.TransformFunctionTypeColumn);
        IAv1Forward1dTransformer inverse = GetInverseTransformer(config.TransformFunctionTypeColumn);
        AssertAccuracy1d(transformType, transformSize, scaleLog2, forward, inverse, allowedError);
    }

    private static void AssertAccuracy1d(
        Av1TransformType transformType,
        Av1TransformSize transformSize,
        int scaleLog2,
        IAv1Forward1dTransformer forwardTransformer,
        IAv1Forward1dTransformer inverseTransformer,
        int allowedError = 1)
    {
        const int bitDepth = 10;
        Random rnd = new(0);
        const int testBlockCount = 100; // Originally set to: 5000
        Av1Transform2dFlipConfiguration config = new(transformType, transformSize);
        config.GenerateStageRange(bitDepth);
        int width = config.TransformSize.GetWidth();

        int[] inputOfTest = new int[width];
        int[] outputOfTest = new int[width];
        int[] outputReference = new int[width];
        for (int ti = 0; ti < testBlockCount; ++ti)
        {
            // prepare random test data
            for (int ni = 0; ni < width; ++ni)
            {
                inputOfTest[ni] = (short)rnd.Next((1 << bitDepth) - 1);
                outputReference[ni] = 0;
                outputOfTest[ni] = 255;
            }

            // calculate in forward transform functions
            forwardTransformer.Transform(
                inputOfTest,
                outputReference,
                config.CosBitColumn,
                config.StageRangeColumn);

            // calculate in inverse transform functions
            inverseTransformer.Transform(
                outputReference,
                outputOfTest,
                config.CosBitColumn,
                config.StageRangeColumn);

            // Assert
            Assert.True(CompareWithError(inputOfTest, outputOfTest.Select(x => x >> scaleLog2).ToArray(), allowedError), $"Error: {GetMaximumError(inputOfTest, outputOfTest)}");
        }
    }

    private static IAv1Forward1dTransformer GetForwardTransformer(Av1TransformFunctionType func) =>
        func switch
        {
            Av1TransformFunctionType.Dct4 => new Av1Dct4Forward1dTransformer(),
            Av1TransformFunctionType.Dct8 => new Av1Dct8Forward1dTransformer(),
            Av1TransformFunctionType.Dct16 => new Av1Dct16Forward1dTransformer(),
            Av1TransformFunctionType.Dct32 => new Av1Dct32Forward1dTransformer(),
            Av1TransformFunctionType.Dct64 => new Av1Dct64Forward1dTransformer(),
            Av1TransformFunctionType.Adst4 => new Av1Adst4Forward1dTransformer(),
            Av1TransformFunctionType.Adst8 => new Av1Adst8Forward1dTransformer(),
            Av1TransformFunctionType.Adst16 => new Av1Adst16Forward1dTransformer(),
            Av1TransformFunctionType.Adst32 => new Av1Adst32Forward1dTransformer(),
            Av1TransformFunctionType.Identity4 => new Av1Identity4Forward1dTransformer(),
            Av1TransformFunctionType.Identity8 => new Av1Identity8Forward1dTransformer(),
            Av1TransformFunctionType.Identity16 => new Av1Identity16Forward1dTransformer(),
            Av1TransformFunctionType.Identity32 => new Av1Identity32Forward1dTransformer(),
            Av1TransformFunctionType.Identity64 => new Av1Identity64Forward1dTransformer(),
            Av1TransformFunctionType.Invalid => null,
            _ => null,
        };

    private static IAv1Forward1dTransformer GetInverseTransformer(Av1TransformFunctionType func) =>
        func switch
        {
            Av1TransformFunctionType.Dct4 => new Av1Dct4Inverse1dTransformer(),
            Av1TransformFunctionType.Dct8 => null, // new Av1Dct8Inverse1dTransformer(),
            Av1TransformFunctionType.Dct16 => null, // new Av1Dct16Inverse1dTransformer(),
            Av1TransformFunctionType.Dct32 => null, // new Av1Dct32Inverse1dTransformer(),
            Av1TransformFunctionType.Dct64 => null, // new Av1Dct64Inverse1dTransformer(),
            Av1TransformFunctionType.Adst4 => new Av1Adst4Inverse1dTransformer(),
            Av1TransformFunctionType.Adst8 => null, // new Av1Adst8Inverse1dTransformer(),
            Av1TransformFunctionType.Adst16 => null, // new Av1Adst16Inverse1dTransformer(),
            Av1TransformFunctionType.Adst32 => null, // new Av1Adst32Inverse1dTransformer(),
            Av1TransformFunctionType.Identity4 => new Av1Identity4Inverse1dTransformer(),
            Av1TransformFunctionType.Identity8 => new Av1Identity8Inverse1dTransformer(),
            Av1TransformFunctionType.Identity16 => new Av1Identity16Inverse1dTransformer(),
            Av1TransformFunctionType.Identity32 => new Av1Identity32Inverse1dTransformer(),
            Av1TransformFunctionType.Identity64 => new Av1Identity64Inverse1dTransformer(),
            Av1TransformFunctionType.Invalid => null,
            _ => null,
        };

    private static bool CompareWithError(Span<int> expected, Span<int> actual, int allowedError)
    {
        // compare for the result is within accuracy
        int maximumErrorInTest = GetMaximumError(expected, actual);
        return maximumErrorInTest <= allowedError;
    }

    private static int GetMaximumError(Span<int> expected, Span<int> actual)
    {
        int maximumErrorInTest = 0;
        int count = Math.Min(expected.Length, 32);
        for (int ni = 0; ni < count; ++ni)
        {
            maximumErrorInTest = Math.Max(maximumErrorInTest, Math.Abs(actual[ni] - expected[ni]));
        }

        return maximumErrorInTest;
    }
}

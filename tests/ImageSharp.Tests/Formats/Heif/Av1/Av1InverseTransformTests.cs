// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Globalization;
using SixLabors.ImageSharp.Formats.Heif.Av1;
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

    [Fact]
    public void AccuracyOfDct1dTransformSize8Test()
        => AssertAccuracy1d(Av1TransformType.DctDct, Av1TransformSize.Size8x8, 2, 2);

    [Fact]
    public void AccuracyOfDct1dTransformSize16Test()
        => AssertAccuracy1d(Av1TransformType.DctDct, Av1TransformSize.Size16x16, 3, 3);

    [Fact]
    public void AccuracyOfDct1dTransformSize32Test()
        => AssertAccuracy1d(Av1TransformType.DctDct, Av1TransformSize.Size32x32, 4, 4);

    [Fact]
    public void AccuracyOfDct1dTransformSize64Test()
        => AssertAccuracy1d(Av1TransformType.DctDct, Av1TransformSize.Size64x64, 5, 5);

    [Fact]
    public void AccuracyOfAdst1dTransformSize4Test()
        => AssertAccuracy1d(Av1TransformType.AdstAdst, Av1TransformSize.Size4x4, 1);

    [Fact]
    public void AccuracyOfAdst1dTransformSize8Test()
        => AssertAccuracy1d(Av1TransformType.AdstAdst, Av1TransformSize.Size8x8, 2, 2);

    [Fact]
    public void AccuracyOfAdst1dTransformSize16Test()
        => AssertAccuracy1d(Av1TransformType.AdstAdst, Av1TransformSize.Size16x16, 3, 3);

    // Not mentioned in the spec.
    public void AccuracyOfAdst1dTransformSize32Test()
        => AssertAccuracy1d(Av1TransformType.AdstAdst, Av1TransformSize.Size32x32, 4, 3);

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
        => AssertAccuracy1d(Av1TransformType.Identity, Av1TransformSize.Size64x64, 1);

    [Fact]
    public void AccuracyOfEchoTransformSize4Test()
        => AssertAccuracy1d(Av1TransformType.Identity, Av1TransformSize.Size4x4, 0, new Av1EchoTestTransformer(), new Av1EchoTestTransformer());

    [Fact]
    public void FlipNothingTest()
    {
        // Arrange
        int[] input = [
            1, 2, 3, 4,
            5, 6, 7, 8,
            9, 10, 11, 12,
            13, 14, 15, 16];
        short[] expected = [
            2, 4, 6, 8,
            10, 12, 14, 16,
            18, 20, 22, 24,
            26, 28, 30, 32];
        int[] temp = new int[16 + 8];
        short[] actual = new short[16];
        Av1Transform2dFlipConfiguration config = new(Av1TransformType.Identity, Av1TransformSize.Size4x4);
        config.GenerateStageRange(8);
        config.SetFlip(false, false);
        config.SetShift(0, 0, 0);
        IAv1Transformer1d transformer = new Av1EchoTestTransformer();

        // Act
        Av1Inverse2dTransformer.Transform2dAdd(
            input,
            actual,
            4,
            actual,
            4,
            config,
            temp,
            8);

        // Assert
        Assert.True(CompareWithError<short>(expected, actual, 1));
    }

    [Fact]
    public void FlipHorizontalTest()
    {
        // Arrange
        short[] expected = [
            8, 6, 4, 2,
            16, 14, 12, 10,
            24, 22, 20, 18,
            32, 30, 28, 26];
        int[] input = [
            1, 2, 3, 4,
            5, 6, 7, 8,
            9, 10, 11, 12,
            13, 14, 15, 16];
        int[] temp = new int[16 + 8];
        short[] actual = new short[16];
        Av1Transform2dFlipConfiguration config = new(Av1TransformType.Identity, Av1TransformSize.Size4x4);
        config.SetFlip(false, true);
        config.SetShift(0, 0, 0);
        IAv1Transformer1d transformer = new Av1EchoTestTransformer();

        // Act
        Av1Inverse2dTransformer.Transform2dAdd(
            input,
            actual,
            4,
            actual,
            4,
            config,
            temp,
            8);

        // Assert
        Assert.True(CompareWithError<short>(expected, actual, 1));
    }

    [Fact]
    public void FlipVerticalTest()
    {
        // Arrange
        short[] expected = [
            26, 28, 30, 32,
            18, 20, 22, 24,
            10, 12, 14, 16,
            2, 4, 6, 8];
        int[] input = [
            1, 2, 3, 4,
            5, 6, 7, 8,
            9, 10, 11, 12,
            13, 14, 15, 16];
        int[] temp = new int[16 + 8];
        short[] actual = new short[16];
        Av1Transform2dFlipConfiguration config = new(Av1TransformType.Identity, Av1TransformSize.Size4x4);
        config.SetFlip(true, false);
        config.SetShift(0, 0, 0);
        IAv1Transformer1d transformer = new Av1EchoTestTransformer();

        // Act
        Av1Inverse2dTransformer.Transform2dAdd(
            input,
            actual,
            4,
            actual,
            4,
            config,
            temp,
            8);

        // Assert
        Assert.True(CompareWithError<short>(expected, actual, 1));
    }

    [Fact]
    public void FlipHorizontalAndVerticalTest()
    {
        // Arrange
        short[] expected = [
            32, 30, 28, 26,
            24, 22, 20, 18,
            16, 14, 12, 10,
            8, 6, 4, 2];
        int[] input = [
            1, 2, 3, 4,
            5, 6, 7, 8,
            9, 10, 11, 12,
            13, 14, 15, 16];
        int[] temp = new int[16 + 8];
        short[] actual = new short[16];
        Av1Transform2dFlipConfiguration config = new(Av1TransformType.Identity, Av1TransformSize.Size4x4);
        config.SetFlip(true, true);
        config.SetShift(0, 0, 0);
        IAv1Transformer1d transformer = new Av1EchoTestTransformer();

        // Act
        Av1Inverse2dTransformer.Transform2dAdd(
            input,
            actual,
            4,
            actual,
            4,
            config,
            temp,
            8);

        // Assert
        Assert.True(CompareWithError<short>(expected, actual, 1));
    }

    private static void AssertAccuracy1d(
        Av1TransformType transformType,
        Av1TransformSize transformSize,
        int scaleLog2,
        int allowedError = 1)
    {
        Av1Transform2dFlipConfiguration config = new(transformType, transformSize);
        IAv1Transformer1d forward = GetForwardTransformer(config.TransformFunctionTypeColumn);
        IAv1Transformer1d inverse = GetInverseTransformer(config.TransformFunctionTypeColumn);
        AssertAccuracy1d(transformType, transformSize, scaleLog2, forward, inverse, allowedError);
    }

    private static void AssertAccuracy1d(
        Av1TransformType transformType,
        Av1TransformSize transformSize,
        int scaleLog2,
        IAv1Transformer1d forwardTransformer,
        IAv1Transformer1d inverseTransformer,
        int allowedError = 1)
    {
        const int bitDepth = 10;
        Random rnd = new(0);
        const int testBlockCount = 30; // Originally set to: 5000
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
            Assert.True(CompareWithError<int>(inputOfTest, outputOfTest.Select(x => x >> scaleLog2).ToArray(), allowedError), $"Error: {GetMaximumError<int>(inputOfTest, outputOfTest)}");
        }
    }

    // [Theory]
    // [MemberData(nameof(Generate2dCombinations))]
    public void Test2dTransformAdd(int txSize, int txType, bool isLossless)
    {
        const int bitDepth = 8;
        Av1TransformType transformType = (Av1TransformType)txType;
        Av1TransformSize transformSize = (Av1TransformSize)txSize;
        Av1TransformFunctionParameters transformFunctionParams = new()
        {
            BitDepth = bitDepth,
            IsLossless = isLossless,
            TransformSize = transformSize,
            EndOfBuffer = Av1InverseTransformMath.GetMaxEndOfBuffer(transformSize)
        };

        if (bitDepth > 8 && !isLossless)
        {
            // Not support 10 bit with not lossless
            return;
        }

        int width = transformSize.GetWidth();
        int height = transformSize.GetHeight();
        uint stride = (uint)width;
        short[] input = new short[width * height];
        int[] referenceOutput = new int[width * height];
        short[] outputOfTest = new short[width * height];
        int[] transformActual = new int[width * height];
        int[] tempBuffer = new int[(width * height) + 128];

        transformFunctionParams.TransformType = transformType;
        Av1Transform2dFlipConfiguration config = new(transformType, transformSize);
        config.GenerateStageRange(bitDepth);

        const int loops = 1; // Initially: 10;
        for (int k = 0; k < loops; k++)
        {
            PopulateWithRandomValues(input, bitDepth);

            Av1ForwardTransformer.Transform2d(
                input,
                referenceOutput,
                stride,
                transformType,
                transformSize,
                bitDepth);
            Av1Inverse2dTransformer.Transform2dAdd(
                referenceOutput.Select(x => x >> 3).ToArray(),
                outputOfTest,
                width,
                outputOfTest,
                width,
                config,
                tempBuffer,
                bitDepth);
            Av1ForwardTransformer.Transform2d(
                outputOfTest.Select(x => (short)(x >> 1)).ToArray(),
                transformActual,
                stride,
                transformType,
                transformSize,
                bitDepth);

            Assert.True(CompareWithError<int>(referenceOutput, transformActual, 1), $"Error: {GetMaximumError<int>(referenceOutput, transformActual)}");
        }
    }

    private static void DivideArray(Span<int> list, int factor)
    {
        for (int i = 0; i < list.Length; i++)
        {
            list[i] = list[i] / factor;
        }
    }

    private static void DivideArray(Span<short> list, int factor)
    {
        for (int i = 0; i < list.Length; i++)
        {
            list[i] = (short)(list[i] / factor);
        }
    }

    public static TheoryData<int, int, bool> Generate2dCombinations()
    {
        int[][] transformFunctionSupportMatrix = [

            // [Size][type]" // O - No; 1 - lossless; 2 - !lossless; 3 - any
            /*0  1  2  3  4  5  6  7  8  9 10 11 12 13 14 15*/
            [3, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2], // 0  TX_4X4,
            [3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3], // 1  TX_8X8,
            [3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3], // 2  TX_16X16,
            [3, 0, 0, 0, 0, 0, 0, 0, 0, 3, 0, 0, 0, 0, 0, 0], // 3  TX_32X32,
            [3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0], // 4  TX_64X64,
            [3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3], // 5  TX_4X8,
            [3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3], // 6  TX_8X4,
            [3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3], // 7  TX_8X16,
            [3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3], // 8  TX_16X8,
            [3, 1, 3, 1, 1, 3, 1, 1, 1, 3, 3, 3, 1, 3, 1, 3], // 9  TX_16X32,
            [3, 3, 1, 1, 3, 1, 1, 1, 1, 3, 3, 3, 3, 1, 3, 1], // 10 TX_32X16,
            [3, 0, 1, 0, 0, 1, 0, 0, 0, 3, 3, 3, 0, 1, 0, 1], // 11 TX_32X64,
            [3, 1, 0, 0, 1, 0, 0, 0, 0, 3, 3, 3, 1, 0, 1, 0], // 12 TX_64X32,
            [3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3], // 13 TX_4X16,
            [3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3], // 14 TX_16X4,
            [3, 1, 3, 1, 1, 3, 1, 1, 1, 3, 3, 3, 1, 3, 1, 3], // 15 TX_8X32,
            [3, 3, 1, 1, 3, 1, 1, 1, 1, 3, 3, 3, 3, 1, 3, 1], // 16 TX_32X8,
            [3, 0, 3, 0, 0, 3, 0, 0, 0, 3, 3, 3, 0, 3, 0, 3], // 17 TX_16X64,
            [3, 3, 0, 0, 3, 0, 0, 0, 0, 3, 3, 3, 3, 0, 3, 0], // 18 TX_64X16,
            /*0  1  2  3  4  5  6  7  8  9 10 11 12 13 14 15*/
        ];

        TheoryData<int, int, bool> data = [];
        for (int size = 0; size < (int)Av1TransformSize.AllSizes; size++)
        {
            for (int type = 0; type < (int)Av1TransformType.AllTransformTypes; type++)
            {
                for (int i = 0; i < 2; i++)
                {
                    bool isLossless = i == 1;
                    if ((isLossless && ((transformFunctionSupportMatrix[size][type] & 1) == 0)) ||
                        (!isLossless && ((transformFunctionSupportMatrix[size][type] & 2) == 0)))
                    {
                        continue;
                    }

                    if (IsTransformTypeImplemented((Av1TransformType)type, (Av1TransformSize)size))
                    {
                        data.Add(size, type, isLossless);
                    }
                }
            }
        }

        return data;
    }

    private static void PopulateWithRandomValues(Span<short> input, int bitDepth)
    {
        Random rnd = new(42);
        int maxValue = (1 << (bitDepth - 1)) - 1;
        int minValue = -maxValue;
        for (int i = 0; i < input.Length; i++)
        {
            input[i] = (short)rnd.Next(minValue, maxValue);
        }
    }

    private static bool IsTransformTypeImplemented(Av1TransformType transformType, Av1TransformSize transformSize)
        => transformSize == Av1TransformSize.Size4x4;

    private static IAv1Transformer1d GetForwardTransformer(Av1TransformFunctionType func) =>
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

    private static IAv1Transformer1d GetInverseTransformer(Av1TransformFunctionType func) =>
        func switch
        {
            Av1TransformFunctionType.Dct4 => new Av1Dct4Inverse1dTransformer(),
            Av1TransformFunctionType.Dct8 => new Av1Dct8Inverse1dTransformer(),
            Av1TransformFunctionType.Dct16 => new Av1Dct16Inverse1dTransformer(),
            Av1TransformFunctionType.Dct32 => new Av1Dct32Inverse1dTransformer(),
            Av1TransformFunctionType.Dct64 => new Av1Dct64Inverse1dTransformer(),
            Av1TransformFunctionType.Adst4 => new Av1Adst4Inverse1dTransformer(),
            Av1TransformFunctionType.Adst8 => new Av1Adst8Inverse1dTransformer(),
            Av1TransformFunctionType.Adst16 => new Av1Adst16Inverse1dTransformer(),
            Av1TransformFunctionType.Adst32 => new Av1Adst32Inverse1dTransformer(),
            Av1TransformFunctionType.Identity4 => new Av1Identity4Inverse1dTransformer(),
            Av1TransformFunctionType.Identity8 => new Av1Identity8Inverse1dTransformer(),
            Av1TransformFunctionType.Identity16 => new Av1Identity16Inverse1dTransformer(),
            Av1TransformFunctionType.Identity32 => new Av1Identity32Inverse1dTransformer(),
            Av1TransformFunctionType.Identity64 => new Av1Identity64Inverse1dTransformer(),
            Av1TransformFunctionType.Invalid => null,
            _ => null,
        };

    private static bool CompareWithError<T>(Span<T> expected, Span<T> actual, int allowedError)
        where T : unmanaged
    {
        // compare for the result is within accuracy
        int maximumErrorInTest = GetMaximumError(expected, actual);
        return maximumErrorInTest <= allowedError;
    }

    private static int GetMaximumError<T>(Span<T> expected, Span<T> actual)
    {
        int maximumErrorInTest = 0;
        for (int ni = 0; ni < expected.Length; ++ni)
        {
            maximumErrorInTest = Math.Max(maximumErrorInTest, Math.Abs(Convert.ToInt32(actual[ni], CultureInfo.InvariantCulture) - Convert.ToInt32(expected[ni], CultureInfo.InvariantCulture)));
        }

        return maximumErrorInTest;
    }
}

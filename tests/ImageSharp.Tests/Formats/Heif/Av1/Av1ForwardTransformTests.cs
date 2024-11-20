// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Forward;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// SVY: test/FwdTxfm1dTest.cc
/// SVY: test/FwdTxfm2dAsmTest.cc
/// </summary>
[Trait("Format", "Avif")]
public class Av1ForwardTransformTests
{
    private static readonly int[] MaximumAllowedError =
        [
            3,    // 4x4 transform
            5,    // 8x8 transform
            11,   // 16x16 transform
            70,   // 32x32 transform
            64,   // 64x64 transform
            4,    // 4x8 transform
            5,    // 8x4 transform
            12,   // 8x16 transform
            12,   // 16x8 transform
            32,   // 16x32 transform
            46,   // 32x16 transform
            136,  // 32x64 transform
            136,  // 64x32 transform
            5,    // 4x16 transform
            6,    // 16x4 transform
            21,   // 8x32 transform
            13,   // 32x8 transform
            30,   // 16x64 transform
            36,   // 64x16 transform
        ];

    [Theory]
    [MemberData(nameof(GetCombinations))]
    public void ConfigTest(int txSize, int txType, int _)
    {
        // Arrange
        Av1TransformType transformType = (Av1TransformType)txType;
        Av1TransformSize transformSize = (Av1TransformSize)txSize;
        int width = transformSize.GetWidth();
        int height = transformSize.GetHeight();

        // Act
        Av1Transform2dFlipConfiguration config = new(transformType, transformSize);

        // Assert
        Assert.Equal(transformSize, config.TransformSize);
        Assert.Equal(transformType, config.TransformType);
        string actual = $"{config.TransformTypeColumn}{config.TransformTypeRow}";
        if (actual == "IdentityIdentity")
        {
            actual = "Identity";
        }
        else
        {
            if (actual.StartsWith("Identity", StringComparison.InvariantCulture))
            {
                actual = actual.Replace("Identity", "Horizontal");
            }

            if (actual.EndsWith("Identity", StringComparison.InvariantCulture))
            {
                actual = "Vertical" + actual.Replace("Identity", string.Empty);
            }
        }

        Assert.Equal(transformType.ToString(), actual);
    }

    [Theory]
    [MemberData(nameof(GetCombinations))]
    public void ScaleFactorTest(int txSize, int txType, int _)
    {
        // Arrange
        Av1TransformType transformType = (Av1TransformType)txType;
        Av1TransformSize transformSize = (Av1TransformSize)txSize;
        short[] input = new short[64 * 64];
        Array.Fill<short>(input, 1);
        int[] actual = new int[64 * 64];
        Av1Transform2dFlipConfiguration config = new(transformType, transformSize);
        IAv1Forward1dTransformer transformer = new Av1EchoTestTransformer();
        int width = transformSize.GetWidth();
        int height = transformSize.GetHeight();
        int blockSize = width * height;
        double expected = Av1ReferenceTransform.GetScaleFactor(config);

        // Act
        Av1ForwardTransformer.Transform2d(
            transformer,
            transformer,
            input,
            actual,
            (uint)width,
            config,
            8);

        // Assert
        Assert.True(actual.Take(blockSize).All(x => Math.Abs(x - expected) < 1d));
    }

    [Fact]
    public void FlipNothingTest()
    {
        // Arrange
        short[] input = [
            1, 2, 3, 4,
            5, 6, 7, 8,
            9, 10, 11, 12,
            13, 14, 15, 16];
        int[] expected = [
            1, 2, 3, 4,
            5, 6, 7, 8,
            9, 10, 11, 12,
            13, 14, 15, 16];
        int[] actual = new int[16];
        Av1Transform2dFlipConfiguration config = new(Av1TransformType.Identity, Av1TransformSize.Size4x4);
        config.SetFlip(false, false);
        config.SetShift(0, 0, 0);
        IAv1Forward1dTransformer transformer = new Av1EchoTestTransformer();

        // Act
        Av1ForwardTransformer.Transform2d(
            transformer,
            transformer,
            input,
            actual,
            4,
            config,
            8);

        // Assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void FlipHorizontalTest()
    {
        // Arrange
        int[] expected = [
            4, 3, 2, 1,
            8, 7, 6, 5,
            12, 11, 10, 9,
            16, 15, 14, 13];
        short[] input = [
            1, 2, 3, 4,
            5, 6, 7, 8,
            9, 10, 11, 12,
            13, 14, 15, 16];
        int[] actual = new int[16];
        Av1Transform2dFlipConfiguration config = new(Av1TransformType.Identity, Av1TransformSize.Size4x4);
        config.SetFlip(false, true);
        config.SetShift(0, 0, 0);
        IAv1Forward1dTransformer transformer = new Av1EchoTestTransformer();

        // Act
        Av1ForwardTransformer.Transform2d(
            transformer,
            transformer,
            input,
            actual,
            4,
            config,
            8);

        // Assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void FlipVerticalTest()
    {
        // Arrange
        int[] expected = [
            13, 14, 15, 16,
            9, 10, 11, 12,
            5, 6, 7, 8,
            1, 2, 3, 4];
        short[] input = [
            1, 2, 3, 4,
            5, 6, 7, 8,
            9, 10, 11, 12,
            13, 14, 15, 16];
        int[] actual = new int[16];
        Av1Transform2dFlipConfiguration config = new(Av1TransformType.Identity, Av1TransformSize.Size4x4);
        config.SetFlip(true, false);
        config.SetShift(0, 0, 0);
        IAv1Forward1dTransformer transformer = new Av1EchoTestTransformer();

        // Act
        Av1ForwardTransformer.Transform2d(
            transformer,
            transformer,
            input,
            actual,
            4,
            config,
            8);

        // Assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void FlipHorizontalAndVerticalTest()
    {
        // Arrange
        int[] expected = [
            16, 15, 14, 13,
            12, 11, 10, 9,
            8, 7, 6, 5,
            4, 3, 2, 1];
        short[] input = [
            1, 2, 3, 4,
            5, 6, 7, 8,
            9, 10, 11, 12,
            13, 14, 15, 16];
        int[] actual = new int[16];
        Av1Transform2dFlipConfiguration config = new(Av1TransformType.Identity, Av1TransformSize.Size4x4);
        config.SetFlip(true, true);
        config.SetShift(0, 0, 0);
        IAv1Forward1dTransformer transformer = new Av1EchoTestTransformer();

        // Act
        Av1ForwardTransformer.Transform2d(
            transformer,
            transformer,
            input,
            actual,
            4,
            config,
            8);

        // Assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void NonSquareTransformSizeLandscapeTest()
    {
        // Arrange
        short[] input = [
            1, 2, 3, 4, 5, 6, 7, 8,
            9, 10, 11, 12, 13, 14, 15, 16,
            17, 18, 19, 20, 21, 22, 23, 24,
            25, 26, 27, 28, 29, 30, 31, 32];

        // Expected is divided by Sqrt(2).
        int[] expected = [
            18, 20, 21, 23, 24, 25, 27, 28,
            13, 14, 16, 17, 18, 20, 21, 23,
            7, 8, 10, 11, 13, 14, 16, 17,
            1, 3, 4, 6, 7, 8, 10, 11];
        int[] actual = new int[32];
        Av1Transform2dFlipConfiguration config = new(Av1TransformType.Identity, Av1TransformSize.Size8x4);
        config.SetFlip(true, false);
        config.SetShift(0, 0, 0);
        IAv1Forward1dTransformer transformer = new Av1EchoTestTransformer();

        // Act
        Av1ForwardTransformer.Transform2d(
            transformer,
            transformer,
            input,
            actual,
            4,
            config,
            8);

        // Assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void NonSquareTransformSizePortraitTest()
    {
        // Arrange
        short[] input = [
            1, 2, 3, 4,
            5, 6, 7, 8,
            9, 10, 11, 12,
            13, 14, 15, 16,
            17, 18, 19, 20,
            21, 22, 23, 24,
            25, 26, 27, 28,
            29, 30, 31, 32];

        // Expected is multiplied by Sqrt(2).
        int[] expected = [
            41, 42, 44, 45,
            35, 37, 38, 40,
            30, 31, 33, 34,
            24, 25, 27, 28,
            18, 20, 21, 23,
            13, 14, 16, 17,
            7, 8, 10, 11,
            1, 3, 4, 6];
        int[] actual = new int[32];
        Av1Transform2dFlipConfiguration config = new(Av1TransformType.Identity, Av1TransformSize.Size4x8);
        config.SetFlip(true, false);
        config.SetShift(0, 0, 0);
        IAv1Forward1dTransformer transformer = new Av1EchoTestTransformer();

        // Act
        Av1ForwardTransformer.Transform2d(
            transformer,
            transformer,
            input,
            actual,
            4,
            config,
            8);

        // Assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void AccuracyOfDct1dTransformSize4Test()
        => AssertAccuracy1d(Av1TransformSize.Size4x4, Av1TransformType.DctDct, new Av1Dct4Forward1dTransformer());

    [Fact]
    public void AccuracyOfDct1dTransformSize8Test()
        => AssertAccuracy1d(Av1TransformSize.Size8x8, Av1TransformType.DctDct, new Av1Dct8Forward1dTransformer(), 2);

    [Fact]
    public void AccuracyOfDct1dTransformSize16Test()
        => AssertAccuracy1d(Av1TransformSize.Size16x16, Av1TransformType.DctDct, new Av1Dct16Forward1dTransformer(), 3);

    [Fact]
    public void AccuracyOfDct1dTransformSize32Test()
        => AssertAccuracy1d(Av1TransformSize.Size32x32, Av1TransformType.DctDct, new Av1Dct32Forward1dTransformer(), 4);

    [Fact]
    public void AccuracyOfDct1dTransformSize64Test()
        => AssertAccuracy1d(Av1TransformSize.Size64x64, Av1TransformType.DctDct, new Av1Dct64Forward1dTransformer(), 5);

    [Fact]
    public void AccuracyOfAdst1dTransformSize4Test()
        => AssertAccuracy1d(Av1TransformSize.Size4x4, Av1TransformType.AdstAdst, new Av1Adst4Forward1dTransformer());

    [Fact]
    public void AccuracyOfAdst1dTransformSize8Test()
        => AssertAccuracy1d(Av1TransformSize.Size8x8, Av1TransformType.AdstAdst, new Av1Adst8Forward1dTransformer(), 2);

    [Fact]
    public void AccuracyOfAdst1dTransformSize16Test()
        => AssertAccuracy1d(Av1TransformSize.Size16x16, Av1TransformType.AdstAdst, new Av1Adst16Forward1dTransformer(), 3);

    [Fact]
    public void AccuracyOfAdst1dTransformSize32Test()
        => AssertAccuracy1d(Av1TransformSize.Size32x32, Av1TransformType.AdstAdst, new Av1Adst32Forward1dTransformer(), 5);

    [Fact]
    public void AccuracyOfIdentity1dTransformSize4Test()
        => AssertAccuracy1d(Av1TransformSize.Size4x4, Av1TransformType.Identity, new Av1Identity4Forward1dTransformer());

    [Fact]
    public void AccuracyOfIdentity1dTransformSize8Test()
        => AssertAccuracy1d(Av1TransformSize.Size8x8, Av1TransformType.Identity, new Av1Identity8Forward1dTransformer());

    [Fact]
    public void AccuracyOfIdentity1dTransformSize16Test()
        => AssertAccuracy1d(Av1TransformSize.Size16x16, Av1TransformType.Identity, new Av1Identity16Forward1dTransformer());

    [Fact]
    public void AccuracyOfIdentity1dTransformSize32Test()
        => AssertAccuracy1d(Av1TransformSize.Size32x32, Av1TransformType.Identity, new Av1Identity32Forward1dTransformer());

    [Fact]
    public void AccuracyOfIdentity1dTransformSize64Test()
        => AssertAccuracy1d(Av1TransformSize.Size64x64, Av1TransformType.Identity, new Av1Identity64Forward1dTransformer());

    [Theory]
    [MemberData(nameof(GetCombinations))]
    public void Accuracy2dTest(int txSize, int txType, int maxAllowedError = 0)
    {
        const int bitDepth = 8;
        Random rnd = new(0);
        const int testBlockCount = 1; // Originally set to: 1000
        Av1TransformSize transformSize = (Av1TransformSize)txSize;
        Av1TransformType transformType = (Av1TransformType)txType;
        Av1Transform2dFlipConfiguration config = new(transformType, transformSize);
        int width = config.TransformSize.GetWidth();
        int height = config.TransformSize.GetHeight();
        int blockSize = width * height;
        double scaleFactor = Av1ReferenceTransform.GetScaleFactor(config);

        // TODO: Still some limitations in either reference or the actual implementation.
        if (config.TransformTypeColumn == Av1TransformType1d.FlipAdst || config.TransformTypeRow == Av1TransformType1d.FlipAdst)
        {
            return;
        }

        if (width == 64 || height == 64 || width != height)
        {
            return;
        }

        short[] inputOfTest = new short[blockSize];
        double[] inputOfReference = new double[blockSize];
        int[] outputOfTest = new int[blockSize];
        double[] outputOfReference = new double[blockSize];
        for (int ti = 0; ti < testBlockCount; ++ti)
        {
            // prepare random test data
            for (int ni = 0; ni < blockSize; ++ni)
            {
                inputOfTest[ni] = (short)rnd.Next((1 << 10) - 1);
                inputOfReference[ni] = inputOfTest[ni];
                outputOfReference[ni] = 0;
                outputOfTest[ni] = 255;
            }

            // calculate in forward transform functions
            Av1ForwardTransformer.Transform2d(
                inputOfTest,
                outputOfTest,
                (uint)width,
                transformType,
                transformSize,
                bitDepth);

            // calculate in reference forward transform functions
            FlipInput(config, inputOfReference);
            Av1ReferenceTransform.ReferenceTransformFunction2d(inputOfReference, outputOfReference, transformType, transformSize, scaleFactor);

            // repack the coefficents for some tx_size
            RepackCoefficients(outputOfTest, outputOfReference, width, height);

            Assert.True(CompareWithError(outputOfReference, outputOfTest, maxAllowedError * scaleFactor), $"{transformType} of {transformSize}, error: {GetMaximumError(outputOfReference, outputOfTest)}.");
        }
    }

    private static void FlipInput(Av1Transform2dFlipConfiguration config, Span<double> input)
    {
        int width = config.TransformSize.GetWidth();
        int height = config.TransformSize.GetHeight();
        double tmp;
        if (config.FlipLeftToRight)
        {
            for (int r = 0; r < height; ++r)
            {
                for (int c = 0; c < width / 2; ++c)
                {
                    tmp = input[(r * width) + c];
                    input[(r * width) + c] = input[(r * width) + width - 1 - c];
                    input[(r * width) + width - 1 - c] = tmp;
                }
            }
        }

        if (config.FlipUpsideDown)
        {
            for (int c = 0; c < width; ++c)
            {
                for (int r = 0; r < height / 2; ++r)
                {
                    tmp = input[(r * width) + c];
                    input[(r * width) + c] = input[((height - 1 - r) * width) + c];
                    input[((height - 1 - r) * width) + c] = tmp;
                }
            }
        }
    }

    // The max txb_width or txb_height is 32, as specified in spec 7.12.3.
    // Clear the high frequency coefficents and repack it in linear layout.
    private static void RepackCoefficients(Span<int> outputOfTest, Span<double> outputReference, int tx_width, int tx_height)
    {
        for (int i = 0; i < 2; ++i)
        {
            uint elementSize = i == 0 ? (uint)sizeof(int) : sizeof(double);
            ref byte output = ref (i == 0) ? ref Unsafe.As<int, byte>(ref outputOfTest[0])
                                  : ref Unsafe.As<double, byte>(ref outputReference[0]);

            if (tx_width == 64 && tx_height == 64)
            {
                // tx_size == TX_64X64
                // zero out top-right 32x32 area.
                for (uint row = 0; row < 32; ++row)
                {
                    Unsafe.InitBlock(ref Unsafe.Add(ref output, ((row * 64) + 32) * elementSize), 0, 32 * elementSize);
                }

                // zero out the bottom 64x32 area.
                Unsafe.InitBlock(ref Unsafe.Add(ref output, 32 * 64 * elementSize), 0, 32 * 64 * elementSize);

                // Re-pack non-zero coeffs in the first 32x32 indices.
                for (uint row = 1; row < 32; ++row)
                {
                    Unsafe.CopyBlock(
                        ref Unsafe.Add(ref output, row * 32 * elementSize),
                        ref Unsafe.Add(ref output, row * 64 * elementSize),
                        32 * elementSize);
                }
            }
            else if (tx_width == 32 && tx_height == 64)
            {
                // tx_size == TX_32X64
                // zero out the bottom 32x32 area.
                Unsafe.InitBlock(ref Unsafe.Add(ref output, 32 * 32 * elementSize), 0, 32 * 32 * elementSize);

                // Note: no repacking needed here.
            }
            else if (tx_width == 64 && tx_height == 32)
            {
                // tx_size == TX_64X32
                // zero out right 32x32 area.
                for (uint row = 0; row < 32; ++row)
                {
                    Unsafe.InitBlock(ref Unsafe.Add(ref output, ((row * 64) + 32) * elementSize), 0, 32 * elementSize);
                }

                // Re-pack non-zero coeffs in the first 32x32 indices.
                for (uint row = 1; row < 32; ++row)
                {
                    Unsafe.CopyBlock(
                        ref Unsafe.Add(ref output, row * 32 * elementSize),
                        ref Unsafe.Add(ref output, row * 64 * elementSize),
                        32 * elementSize);
                }
            }
            else if (tx_width == 16 && tx_height == 64)
            {
                // tx_size == TX_16X64
                // zero out the bottom 16x32 area.
                Unsafe.InitBlock(ref Unsafe.Add(ref output, 16 * 32 * elementSize), 0, 16 * 32 * elementSize);

                // Note: no repacking needed here.
            }
            else if (tx_width == 64 &&
                       tx_height == 16)
            {
                // tx_size == TX_64X16
                // zero out right 32x16 area.
                for (uint row = 0; row < 16; ++row)
                {
                    Unsafe.InitBlock(ref Unsafe.Add(ref output, ((row * 64) + 32) * elementSize), 0, 32 * elementSize);
                }

                // Re-pack non-zero coeffs in the first 32x16 indices.
                for (uint row = 1; row < 16; ++row)
                {
                    Unsafe.CopyBlock(
                        ref Unsafe.Add(ref output, row * 32 * elementSize),
                        ref Unsafe.Add(ref output, row * 64 * elementSize),
                        32 * elementSize);
                }
            }
        }
    }

    private static void AssertAccuracy1d(
        Av1TransformSize transformSize,
        Av1TransformType transformType,
        IAv1Forward1dTransformer transformerUnderTest,
        int allowedError = 1)
    {
        Random rnd = new(0);
        const int testBlockCount = 100; // Originally set to: 1000
        Av1Transform2dFlipConfiguration config = new(transformType, transformSize);
        int width = config.TransformSize.GetWidth();

        int[] inputOfTest = new int[width];
        double[] inputReference = new double[width];
        int[] outputOfTest = new int[width];
        double[] outputReference = new double[width];
        for (int ti = 0; ti < testBlockCount; ++ti)
        {
            // prepare random test data
            for (int ni = 0; ni < width; ++ni)
            {
                inputOfTest[ni] = (short)rnd.Next((1 << 10) - 1);
                inputReference[ni] = inputOfTest[ni];
                outputReference[ni] = 0;
                outputOfTest[ni] = 255;
            }

            // calculate in forward transform functions
            transformerUnderTest.Transform(
                inputOfTest,
                outputOfTest,
                config.CosBitColumn,
                config.StageRangeColumn);

            // calculate in reference forward transform functions
            Av1ReferenceTransform.ReferenceTransform1d(config.TransformTypeColumn, inputReference, outputReference, width);

            // Assert
            Assert.True(CompareWithError(outputReference, outputOfTest, allowedError));
        }
    }

    private static bool CompareWithError(Span<double> expected, Span<int> actual, double allowedError)
    {
        // compare for the result is within accuracy
        double maximumErrorInTest = GetMaximumError(expected, actual);
        return maximumErrorInTest <= allowedError;
    }

    private static double GetMaximumError(Span<double> expected, Span<int> actual)
    {
        double maximumErrorInTest = 0d;
        for (int ni = 0; ni < expected.Length; ++ni)
        {
            maximumErrorInTest = Math.Max(maximumErrorInTest, Math.Abs(actual[ni] - Math.Round(expected[ni])));
        }

        return maximumErrorInTest;
    }

    public static TheoryData<int, int, int> GetCombinations()
    {
        TheoryData<int, int, int> combinations = [];
        for (int s = 0; s < (int)Av1TransformSize.AllSizes; s++)
        {
            Av1TransformSize transformSize = (Av1TransformSize)s;
            int maxError = MaximumAllowedError[s];
            for (int t = 0; t < (int)Av1TransformType.AllTransformTypes; t++)
            {
                Av1TransformType transformType = (Av1TransformType)t;
                Av1Transform2dFlipConfiguration config = new(transformType, transformSize);
                if (config.IsAllowed())
                {
                    combinations.Add(s, t, maxError);
                }
            }
        }

        return combinations;
    }
}

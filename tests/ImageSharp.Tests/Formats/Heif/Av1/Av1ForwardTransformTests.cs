// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// SVY: test/FwdTxfm2dTest.cc
/// </summary>
[Trait("Format", "Avif")]
public class Av1ForwardTransformTests
{
    private static readonly double[] MaximumAllowedError =
        [
            3,    // 4x4 transform
            5,    // 8x8 transform
            11,   // 16x16 transform
            70,   // 32x32 transform
            64,   // 64x64 transform
            3.9,  // 4x8 transform
            4.3,  // 8x4 transform
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

    private readonly short[] inputOfTest;
    private readonly int[] outputOfTest;
    private readonly double[] inputReference;
    private readonly double[] outputReference;

    public Av1ForwardTransformTests()
    {
        this.inputOfTest = new short[64 * 64];
        this.outputOfTest = new int[64 * 64];
        this.inputReference = new double[64 * 64];
        this.outputReference = new double[64 * 64];
    }

    [Theory]
    [MemberData(nameof(GetCombinations))]
    public void Accuracy2dTest(int txSize, int txType, int maxAllowedError)
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
        double scaleFactor = Av1ReferenceTransform.GetScaleFactor(config, width, height);

        for (int ti = 0; ti < testBlockCount; ++ti)
        {
            // prepare random test data
            for (int ni = 0; ni < blockSize; ++ni)
            {
                this.inputOfTest[ni] = (short)rnd.Next((1 << 10) - 1);
                this.inputReference[ni] = this.inputOfTest[ni];
                this.outputReference[ni] = 0;
                this.outputOfTest[ni] = 255;
            }

            // calculate in forward transform functions
            Av1ForwardTransformer.Transform2d(
                this.inputOfTest,
                this.outputOfTest,
                (uint)transformSize.GetWidth(),
                transformType,
                transformSize,
                bitDepth);

            // calculate in reference forward transform functions
            Av1ReferenceTransform.ReferenceTransformFunction2d(this.inputReference, this.outputReference, transformType, transformSize, scaleFactor);

            // repack the coefficents for some tx_size
            this.RepackCoefficients(width, height);

            // compare for the result is in accuracy
            double maximumErrorInTest = 0;
            for (int ni = 0; ni < blockSize; ++ni)
            {
                maximumErrorInTest = Math.Max(maximumErrorInTest, Math.Abs(this.outputOfTest[ni] - Math.Round(this.outputReference[ni])));
            }

            maximumErrorInTest /= scaleFactor;
            Assert.True(maxAllowedError >= maximumErrorInTest, $"Forward transform 2d test with transform type: {transformType}, transform size: {transformSize} and loop: {ti}");
        }
    }

    // The max txb_width or txb_height is 32, as specified in spec 7.12.3.
    // Clear the high frequency coefficents and repack it in linear layout.
    private void RepackCoefficients(int tx_width, int tx_height)
    {
        for (int i = 0; i < 2; ++i)
        {
            uint e_size = i == 0 ? (uint)sizeof(int) : sizeof(double);
            ref byte output = ref (i == 0) ? ref Unsafe.As<int, byte>(ref this.outputOfTest[0])
                                  : ref Unsafe.As<double, byte>(ref this.outputReference[0]);

            if (tx_width == 64 && tx_height == 64)
            {
                // tx_size == TX_64X64
                // zero out top-right 32x32 area.
                for (uint row = 0; row < 32; ++row)
                {
                    Unsafe.InitBlock(ref Unsafe.Add(ref output, ((row * 64) + 32) * e_size), 0, 32 * e_size);
                }

                // zero out the bottom 64x32 area.
                Unsafe.InitBlock(ref Unsafe.Add(ref output, 32 * 64 * e_size), 0, 32 * 64 * e_size);

                // Re-pack non-zero coeffs in the first 32x32 indices.
                for (uint row = 1; row < 32; ++row)
                {
                    Unsafe.CopyBlock(
                        ref Unsafe.Add(ref output, row * 32 * e_size),
                        ref Unsafe.Add(ref output, row * 64 * e_size),
                        32 * e_size);
                }
            }
            else if (tx_width == 32 && tx_height == 64)
            {
                // tx_size == TX_32X64
                // zero out the bottom 32x32 area.
                Unsafe.InitBlock(ref Unsafe.Add(ref output, 32 * 32 * e_size), 0, 32 * 32 * e_size);

                // Note: no repacking needed here.
            }
            else if (tx_width == 64 && tx_height == 32)
            {
                // tx_size == TX_64X32
                // zero out right 32x32 area.
                for (uint row = 0; row < 32; ++row)
                {
                    Unsafe.InitBlock(ref Unsafe.Add(ref output, ((row * 64) + 32) * e_size), 0, 32 * e_size);
                }

                // Re-pack non-zero coeffs in the first 32x32 indices.
                for (uint row = 1; row < 32; ++row)
                {
                    Unsafe.CopyBlock(
                        ref Unsafe.Add(ref output, row * 32 * e_size),
                        ref Unsafe.Add(ref output, row * 64 * e_size),
                        32 * e_size);
                }
            }
            else if (tx_width == 16 && tx_height == 64)
            {
                // tx_size == TX_16X64
                // zero out the bottom 16x32 area.
                Unsafe.InitBlock(ref Unsafe.Add(ref output, 16 * 32 * e_size), 0, 16 * 32 * e_size);

                // Note: no repacking needed here.
            }
            else if (tx_width == 64 &&
                       tx_height == 16)
            {
                // tx_size == TX_64X16
                // zero out right 32x16 area.
                for (uint row = 0; row < 16; ++row)
                {
                    Unsafe.InitBlock(ref Unsafe.Add(ref output, ((row * 64) + 32) * e_size), 0, 32 * e_size);
                }

                // Re-pack non-zero coeffs in the first 32x16 indices.
                for (uint row = 1; row < 16; ++row)
                {
                    Unsafe.CopyBlock(
                        ref Unsafe.Add(ref output, row * 32 * e_size),
                        ref Unsafe.Add(ref output, row * 64 * e_size),
                        32 * e_size);
                }
            }
        }
    }

    public static TheoryData<int, int, int> GetCombinations()
    {
        TheoryData<int, int, int> combinations = [];
        for (int s = 0; s < (int)Av1TransformSize.AllSizes; s++)
        {
            double maxError = MaximumAllowedError[s];
            for (int t = 0; t < (int)Av1TransformType.AllTransformTypes; ++t)
            {
                Av1TransformType transformType = (Av1TransformType)t;
                Av1TransformSize transformSize = (Av1TransformSize)s;
                Av1Transform2dFlipConfiguration config = new(transformType, transformSize);
                if (config.IsAllowed())
                {
                    combinations.Add(s, t, (int)maxError);
                }
            }
        }

        return combinations;
    }
}

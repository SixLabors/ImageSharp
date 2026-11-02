// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Formats.Jxl.Cms.TransferFunctions;
using SixLabors.ImageSharp.Formats.Jxl.Processing;

namespace SixLabors.ImageSharp.Tests.Formats.Jxl.Processing;

/// <summary>
/// Round-trip tests for high-performance, SIMD-accelerated
/// routines.
/// </summary>
public class FastSimdTests
{
    /// <summary>
    /// Ensures that <see cref="JxlSimdUtils.FastPow2f(Vector{float})"/>
    /// works correctly and is precise.
    /// </summary>
    [Fact]
    public void TestFastPow2()
    {
        const int numberOfTrials = 1 << 23;
        Rng rng = new(1);

        for (int i = 0; i < numberOfTrials; i++)
        {
            float f = rng.UniformF(-100, 100);

            // Result of FastPow2f (faster, lower precision)
            Vector<float> vectorized = JxlSimdUtils.FastPow2f(Vector.Create(f));

            // Result of MathF.Pow (precise)
            float scalar = MathF.Pow(2, f);

            // Precision difference
            float error = MathF.Abs(scalar - vectorized.ToScalar()) / scalar;

            // Precision should be normal.
            Assert.True(error < 3.1E-6);
        }
    }

    /// <summary>
    /// Ensures that <see cref="JxlSimdUtils.FastPowf(Vector{float}, Vector{float})"/>
    /// works correctly and is precise.
    /// </summary>
    [Fact]
    public void TestFastPow()
    {
        const int numberOfTrials = 1 << 23;
        Rng rng = new(1);

        for (int i = 0; i < numberOfTrials; i++)
        {
            // Generate a random base and exponent
            float @base = rng.UniformF(1e-3f, 1e3f);
            float exponent = rng.UniformF(-10, 10);

            // Result of FastPowf (faster, lower precision)
            Vector<float> vectorized = JxlSimdUtils.FastPowf(Vector.Create(@base), Vector.Create(exponent));

            // Result of MathF.Pow (precise)
            float scalar = MathF.Pow(@base, exponent);

            // Precision difference
            float error = MathF.Abs(scalar - vectorized.ToScalar()) / scalar;

            // Precision should be normal.
            Assert.True(error < 3e-5);
        }
    }

    /// <summary>
    /// Ensures that <see cref="JxlSimdUtils.FastErff(Vector{float})"/>
    /// works correctly and is precise.
    /// </summary>
    [Fact]
    public void TestFastErf()
    {
        const int numberOfTrials = 1 << 23;
        Rng rng = new(1);

        for (int i = 0; i < numberOfTrials; i++)
        {
            float f = rng.UniformF(-5f, 5f);

            // Result of FastPowf (faster, lower precision)
            Vector<float> vectorized = JxlSimdUtils.FastErff(Vector.Create(f));

            // Result of a more precise error function. (.NET doesn't
            // support an std::erf equivalent)
            float scalar = JxlSimdUtils.FastErff(f);

            // Precision difference
            float error = MathF.Abs(scalar - vectorized.ToScalar()) / scalar;

            // Precision should be normal.
            Assert.True(error < 7e-4);
        }
    }

    /// <summary>
    /// Tests the SIMD BT.709 transfer function against a
    /// scalar version.
    /// </summary>
    [Fact]
    public void TestFastBt709EncodedFromDisplay()
    {
        const int numberOfTrials = 1 << 23;
        Rng rng = new(1);

        for (int i = 0; i < numberOfTrials; i++)
        {
            float f = rng.UniformF(0, 1f);

            float simd = JxlBt709TransferFunction.EncodedFromDisplay(Vector.Create(f)).ToScalar();
            float scalar = JxlBt709TransferFunction.EncodedFromDisplay(f);

            float absoluteError = MathF.Abs(scalar - simd);
            Assert.True(absoluteError < 2e-6f);
        }
    }
}

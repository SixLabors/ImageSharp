// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

[Trait("Format", "Avif")]
public class Av1DenseFlowSolverTests
{
    /// <summary>
    /// The configuration set the other AV1 vector tests use.
    /// </summary>
    private const HwIntrinsics SolverConfigurations =
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic;

    [Fact]
    public void SolveMatchesTheReferencePatchSolver()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidateSolvePositions, SolverConfigurations);

    /// <summary>
    /// Compares the refined flow vectors with the reference at the frame origin and at an interior patch.
    /// </summary>
    private static void ValidateSolvePositions()
    {
        ValidateSolve(0, 0);
        ValidateSolve(40, 32);
    }

    /// <summary>
    /// Compares the refined flow vector with the reference for one patch position.
    /// </summary>
    /// <param name="x">The patch's first column.</param>
    /// <param name="y">The patch's first row.</param>
    private static void ValidateSolve(int x, int y)
    {
        const int Width = 64;
        const int Height = 48;
        const int Border = 16;
        int stride = Width + (2 * Border);
        int origin = (Border * stride) + Border;
        int length = stride * (Height + (2 * Border));

        byte[] source = new byte[length];
        byte[] reference = new byte[length];
        for (int row = -Border; row < Height + Border; row++)
        {
            for (int column = -Border; column < Width + Border; column++)
            {
                int index = origin + (row * stride) + column;

                // Content with real structure, and a reference that is the source shifted by a
                // fraction of a sample, so the solver has a vector to find.
                source[index] = (byte)(((column * 17) + (row * 11) + ((column * row) / 3)) & byte.MaxValue);
                reference[index] = (byte)(((((column + 1) * 17) + (row * 11) + (((column + 1) * row) / 3)) + 3) & byte.MaxValue);
            }
        }

        // Several starting vectors, including fractional and negative ones, so that the interpolation
        // kernel and the clamped fetch position are both exercised.
        double[][] starts =
        [
            [0.0, 0.0],
            [0.5, -0.25],
            [-1.75, 2.5],
            [3.0, -3.0]
        ];

        foreach (double[] start in starts)
        {
            double expectedU = start[0];
            double expectedV = start[1];
            Av1DenseFlowSolverOracle.Solve(
                source, reference, origin, x, y, Width, Height, stride, ref expectedU, ref expectedV);

            double actualU = start[0];
            double actualV = start[1];
            Av1DenseFlowSolver.Solve(
                source,
                reference,
                origin,
                x,
                y,
                Width,
                Height,
                stride,
                ref actualU,
                ref actualV);

            Assert.Equal(expectedU, actualU, 12);
            Assert.Equal(expectedV, actualV, 12);
        }
    }
}

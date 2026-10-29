// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Processing.Splines;

namespace SixLabors.ImageSharp.Tests.Formats.Jxl.Processing;

public class SplineTests
{
    /// <summary>
    /// Checks control-point double deltas and accumulated reconstruction work through quantization and reconstruction.
    /// </summary>
    [Fact]
    public void Reconstruct_PreservesControlPointsAndAccumulatesFrameWork()
    {
        PointF[] points = [new(0, 0), new(1, 0), new(2, 1)];
        using JxlSpline source = new();
        source.ReserveControlPoints(TestEnvironment.Configuration, points.Length);
        points.AsSpan().CopyTo(source.ControlPoints.Span);

        using JxlQuantizedSpline encoded = JxlQuantizedSpline.Create(TestEnvironment.Configuration, source, 0, 0F, 0F);

        // The first point is separate. Deltas (1,0), (1,1) become double deltas (1,0), (0,1).
        Assert.Equal(2, encoded.ControlPoints.Length);
        Assert.Equal(1, encoded.ControlPoints.Span[0].First);
        Assert.Equal(0, encoded.ControlPoints.Span[0].Second);
        Assert.Equal(0, encoded.ControlPoints.Span[1].First);
        Assert.Equal(1, encoded.ControlPoints.Span[1].Second);

        using JxlSpline reconstructed = new();
        ulong work = 0;
        Assert.True(encoded.Dequantize(TestEnvironment.Configuration, points[0], 0, 0F, 0F, 16, ref work, reconstructed));
        Assert.True(points.AsSpan().SequenceEqual(reconstructed.ControlPoints.Span));
        Assert.True(work > 0);
        ulong firstSplineWork = work;

        // Reconstructing a second identical spline adds its work to the same frame budget.
        Assert.True(encoded.Dequantize(TestEnvironment.Configuration, points[0], 0, 0F, 0F, 16, ref work, reconstructed));
        Assert.Equal(2 * firstSplineWork, work);
        Assert.True(points.AsSpan().SequenceEqual(reconstructed.ControlPoints.Span));
    }

    /// <summary>
    /// Checks the final path position and residual distance, including an exact endpoint with zero residual weight.
    /// </summary>
    /// <param name="length">The length of a horizontal path.</param>
    /// <param name="expectedCount">The number of unit-spaced and endpoint samples.</param>
    /// <param name="expectedResidual">The distance after the last complete unit step.</param>
    [Theory]
    [InlineData(0.5F, 2, 0.5F)]
    [InlineData(2.5F, 4, 0.5F)]
    [InlineData(2F, 4, 0F)]
    public void SamplePath_PreservesFinalPositionAndResidual(float length, int expectedCount, float expectedResidual)
    {
        PointF[] path = [new(0, 0), new(length, 0)];
        List<(PointF Point, float Multiplier)> samples = [];

        JxlSplineUtils.GetEquallySpacedPoints(path, samples);

        Assert.Equal(expectedCount, samples.Count);
        Assert.Equal(new PointF(length, 0), samples[^1].Point);
        Assert.Equal(expectedResidual, samples[^1].Multiplier);
    }

    /// <summary>
    /// Checks numeric image coordinates in packed spline drawing and its scalar fallback.
    /// </summary>
    [Fact]
    public void DrawSegment_UsesNumericImageCoordinates()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            VerifyNumericCoordinates,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Checks that the Gaussian sample at the spline center contributes to the selected color channel.
    /// </summary>
    private static void VerifyNumericCoordinates()
    {
        const int count = 33;
        float[] red = new float[count];
        float[] green = new float[count];
        float[] blue = new float[count];
        InlineArray3<Memory<float>> rows = default;
        rows[0] = red;
        rows[1] = green;
        rows[2] = blue;

        InlineArray3<float> color = default;
        color[0] = 1F;
        JxlSplineSegment segment = new()
        {
            Center = new PointF(16, 0),
            MaximumDistance = 32F,
            InverseSigma = 1F,
            SigmaOver4TimesIntensity = 0.25F,
            Color = color
        };

        // The center is inside every packed traversal width. Reinterpreting integer column bits would place it far from the center.
        JxlSplineUtils.DrawSegment(ref segment, true, 0, 0, count, rows);

        Assert.True(red[16] > 0F);
        Assert.All(green, value => Assert.Equal(0F, value));
        Assert.All(blue, value => Assert.Equal(0F, value));
    }
}

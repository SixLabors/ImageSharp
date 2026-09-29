// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies the pieces that estimate the warp model carrying one frame onto another.
/// </summary>
[Trait("Format", "Heif")]
public class Av1GlobalMotionTests
{
    /// <summary>
    /// Verifies that points which move on their own do not pull the fitted model away from the rest.
    /// </summary>
    [Fact]
    public void RansacIgnoresPointsThatDisagree()
    {
        const int Shift = 3;
        Av1Correspondence[] points = new Av1Correspondence[80];
        for (int i = 0; i < points.Length; i++)
        {
            double x = (i % 10) * 11;
            double y = (i / 10) * 19;

            // One point in five moves somewhere else entirely. The rest share one translation, so a
            // model fitted to the majority must still be that translation.
            double offset = i % 5 == 0 ? 40 : Shift;
            points[i] = new Av1Correspondence(x, y, x + offset, y);
        }

        Av1MotionModel[] models = [new Av1MotionModel()];
        Assert.True(Av1Ransac.Run<Av1Ransac.RotationZoomModel>(Configuration.Default.MemoryAllocator, points, models));

        Assert.Equal(Shift, models[0].Parameters[0], 6);
        Assert.Equal(64, models[0].InlierCount);
    }

    /// <summary>
    /// Verifies that the estimator recovers a translation from a pair of real frames.
    /// </summary>
    [Fact]
    public void EstimatorRecoversAHorizontalTranslation()
    {
        const int Width = 192;
        const int Height = 128;
        const int Stride = Width + (2 * Av1ImagePyramid.Padding);
        const int Shift = 4;
        MemoryAllocator allocator = Configuration.Default.MemoryAllocator;

        int origin = (Av1ImagePyramid.Padding * Stride) + Av1ImagePyramid.Padding;
        byte[] source = new byte[Stride * (Height + (2 * Av1ImagePyramid.Padding))];
        byte[] reference = new byte[source.Length];

        // A pattern with detail in both directions, so the corner detector has corners to find and
        // the flow solver has gradients to work with.
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                byte value = (byte)(((x * 37) + (y * 53) + (x * y * 11)) & byte.MaxValue);
                source[origin + (y * Stride) + x] = value;
                reference[origin + (y * Stride) + Math.Min(x + Shift, Width - 1)] = value;
            }
        }

        Av1MotionModel[] models = [new Av1MotionModel()];
        bool fitted = Av1GlobalMotionEstimator.Compute<byte, Av1GlobalMotionEstimator.ByteFillOperator, Av1Ransac.RotationZoomModel>(
            allocator, source, reference, Width, Height, Stride, origin, 8, 0, models);

        Assert.True(fitted);
        Assert.True(models[0].InlierCount > 0);

        // The model maps the source onto the reference, so a source point lands Shift samples to its
        // right. One sample of tolerance covers the interpolation the flow field goes through.
        Assert.Equal(Shift, models[0].Parameters[0], 1);
        Assert.Equal(0, models[0].Parameters[1], 1);
    }
}

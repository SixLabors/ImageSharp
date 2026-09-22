// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
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
    /// Verifies that a model fitted to points which all move the same way is that movement.
    /// </summary>
    [Fact]
    public void RansacFitsAPureTranslation()
    {
        const int Shift = 4;
        Av1Correspondence[] points = new Av1Correspondence[64];
        for (int i = 0; i < points.Length; i++)
        {
            double x = (i % 8) * 17;
            double y = (i / 8) * 13;
            points[i] = new Av1Correspondence(x, y, x + Shift, y);
        }

        Av1MotionModel[] models = [new Av1MotionModel()];
        Assert.True(Av1Ransac.Run<Av1Ransac.RotationZoomModel>(Configuration.Default.MemoryAllocator, points, models));

        Assert.Equal(points.Length, models[0].InlierCount);
        Assert.Equal(Shift, models[0].Parameters[0], 6);
        Assert.Equal(0, models[0].Parameters[1], 6);
        Assert.Equal(1, models[0].Parameters[2], 6);
        Assert.Equal(0, models[0].Parameters[3], 6);
        Assert.Equal(0, models[0].Parameters[4], 6);
        Assert.Equal(1, models[0].Parameters[5], 6);
    }

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
    /// Verifies that a fit is refused when there are too few points to trust one.
    /// </summary>
    [Fact]
    public void RansacRefusesTooFewPoints()
    {
        Av1Correspondence[] points = new Av1Correspondence[4];
        for (int i = 0; i < points.Length; i++)
        {
            points[i] = new Av1Correspondence(i, i, i + 1, i);
        }

        Av1MotionModel[] models = [new Av1MotionModel()];
        Assert.False(Av1Ransac.Run<Av1Ransac.RotationZoomModel>(Configuration.Default.MemoryAllocator, points, models));
        Assert.Equal(0, models[0].InlierCount);
    }

    /// <summary>
    /// Verifies that a model fitted to a rotation and a zoom keeps both derived parameters.
    /// </summary>
    [Fact]
    public void RansacFitsARotationZoom()
    {
        const double Scale = 1.05;
        Av1Correspondence[] points = new Av1Correspondence[64];
        for (int i = 0; i < points.Length; i++)
        {
            double x = (i % 8) * 17;
            double y = (i / 8) * 13;
            points[i] = new Av1Correspondence(x, y, Scale * x, Scale * y);
        }

        Av1MotionModel[] models = [new Av1MotionModel()];
        Assert.True(Av1Ransac.Run<Av1Ransac.RotationZoomModel>(Configuration.Default.MemoryAllocator, points, models));

        Assert.Equal(Scale, models[0].Parameters[2], 6);
        Assert.Equal(models[0].Parameters[2], models[0].Parameters[5], 6);
        Assert.Equal(-models[0].Parameters[3], models[0].Parameters[4], 6);
    }

    /// <summary>
    /// Verifies that rounding a model to the coded precision and reading its family back agree.
    /// </summary>
    [Fact]
    public void ConvertedModelKeepsItsFamily()
    {
        double[] identity = [0.0, 0.0, 1.0, 0.0, 0.0, 1.0];
        Av1GlobalMotionParameters converted = Av1GlobalMotionSearch.ConvertModelToParameters(identity);
        Assert.Equal(Av1GlobalMotionType.Identity, converted.Type);

        double[] translation = [4.0, -2.0, 1.0, 0.0, 0.0, 1.0];
        converted = Av1GlobalMotionSearch.ConvertModelToParameters(translation);
        Assert.Equal(Av1GlobalMotionType.Translation, converted.Type);
        Assert.Equal(4 * Av1GlobalMotionParameters.ModelScale, converted[0]);
        Assert.Equal(-2 * Av1GlobalMotionParameters.ModelScale, converted[1]);

        double[] rotationZoom = [1.0, 2.0, 1.25, 0.5, -0.5, 1.25];
        converted = Av1GlobalMotionSearch.ConvertModelToParameters(rotationZoom);
        Assert.Equal(Av1GlobalMotionType.RotationZoom, converted.Type);

        double[] affine = [1.0, 2.0, 1.25, 0.5, 0.25, 1.5];
        converted = Av1GlobalMotionSearch.ConvertModelToParameters(affine);
        Assert.Equal(Av1GlobalMotionType.Affine, converted.Type);
    }

    /// <summary>
    /// Verifies that a step taken at coded precision and then undone returns the original value.
    /// </summary>
    [Fact]
    public void ParameterOffsetIsReversible()
    {
        int translation = 3 * Av1GlobalMotionParameters.ModelScale;
        int moved = Av1GlobalMotionSearch.AddParameterOffset(0, translation, 5);
        Assert.Equal(translation, Av1GlobalMotionSearch.AddParameterOffset(0, moved, -5));

        int diagonal = Av1GlobalMotionParameters.ModelScale;
        moved = Av1GlobalMotionSearch.AddParameterOffset(2, diagonal, 7);
        Assert.NotEqual(diagonal, moved);
        Assert.Equal(diagonal, Av1GlobalMotionSearch.AddParameterOffset(2, moved, -7));
    }

    /// <summary>
    /// Verifies that the whole frame is measured when too few blocks hold agreeing points.
    /// </summary>
    [Fact]
    public void SegmentationMapFallsBackToTheWholeFrame()
    {
        const int Width = 8;
        const int Height = 8;
        byte[] map = new byte[Width * Height];

        // Three points in one block is enough to mark that block, but one marked block is far below
        // the count at which the measure trusts the marks.
        int[] inliers = [0, 0, 1, 1, 2, 2];
        Av1GlobalMotionSearch.ComputeFeatureSegmentationMap(map, Width, Height, inliers);
        Assert.All(map, value => Assert.Equal(1, value));
    }

    /// <summary>
    /// Verifies that a block holding too few agreeing points is left out of the measure.
    /// </summary>
    [Fact]
    public void SegmentationMapMarksOnlyPopulatedBlocks()
    {
        const int Width = 16;
        const int Height = 16;
        byte[] map = new byte[Width * Height];

        // Sixty blocks are populated, which passes the count at which the marks are trusted, and one
        // block is given only two points, which is below the count one block needs.
        List<int> inliers = [];
        for (int block = 0; block < 60; block++)
        {
            for (int point = 0; point < Av1GlobalMotionSearch.ErrorBlock / 8; point++)
            {
                inliers.Add(((block % Width) << Av1GlobalMotionSearch.ErrorBlockLog) + point);
                inliers.Add((block / Width) << Av1GlobalMotionSearch.ErrorBlockLog);
            }
        }

        inliers.Add(15 << Av1GlobalMotionSearch.ErrorBlockLog);
        inliers.Add(15 << Av1GlobalMotionSearch.ErrorBlockLog);
        inliers.Add((15 << Av1GlobalMotionSearch.ErrorBlockLog) + 1);
        inliers.Add(15 << Av1GlobalMotionSearch.ErrorBlockLog);

        Av1GlobalMotionSearch.ComputeFeatureSegmentationMap(map, Width, Height, CollectionsMarshal.AsSpan(inliers));
        Assert.Equal(1, map[0]);
        Assert.Equal(0, map[(15 * Width) + 15]);
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
            allocator, source, reference, Width, Height, Stride, origin, 8, models);

        Assert.True(fitted);
        Assert.True(models[0].InlierCount > 0);

        // The model maps the source onto the reference, so a source point lands Shift samples to its
        // right. One sample of tolerance covers the interpolation the flow field goes through.
        Assert.Equal(Shift, models[0].Parameters[0], 1);
        Assert.Equal(0, models[0].Parameters[1], 1);
    }
}

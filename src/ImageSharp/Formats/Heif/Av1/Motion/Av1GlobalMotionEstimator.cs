// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Motion;

/// <summary>
/// Estimates the warp model that carries one whole frame onto another.
/// </summary>
/// <remarks>
/// <para>
/// The estimate is built in four steps. A pyramid of each frame is built, so that a large movement
/// is found at a coarse level and then refined. A dense flow field is solved from the coarsest level
/// down to the second-finest. The corners of the source frame are then looked up in that field,
/// which gives a list of points and where each one moved to. A model is finally fitted to that list,
/// ignoring the points that disagree with the rest.
/// </para>
/// <para>Reference: aom_compute_global_motion() and av1_compute_global_motion_disflow().</para>
/// </remarks>
internal static class Av1GlobalMotionEstimator
{
    /// <summary>
    /// The levels of the pyramid that the flow search asks for.
    /// </summary>
    /// <remarks>
    /// The frame size decides how many are actually built. Reference: DISFLOW_PYRAMID_LEVELS.
    /// </remarks>
    public const int PyramidLevels = 12;

    /// <summary>
    /// Estimates the models that the most parts of a frame agree with.
    /// </summary>
    /// <typeparam name="TModel">The family of models to fit.</typeparam>
    /// <param name="allocator">The allocator of every buffer this search uses.</param>
    /// <param name="source">The whole luma storage of the frame the model maps from.</param>
    /// <param name="reference">The whole luma storage of the frame the model maps to.</param>
    /// <param name="width">The coded width of both frames, in samples.</param>
    /// <param name="height">The coded height of both frames, in samples.</param>
    /// <param name="stride">The row stride of both storages, in samples.</param>
    /// <param name="origin">The index of the first coded sample of both storages.</param>
    /// <param name="models">The models to fill, best first.</param>
    /// <returns>Whether any model was fitted.</returns>
    /// <remarks>Reference: av1_compute_global_motion_disflow().</remarks>
    public static bool Compute<TModel>(
        MemoryAllocator allocator,
        ReadOnlySpan<byte> source,
        ReadOnlySpan<byte> reference,
        int width,
        int height,
        int stride,
        int origin,
        ReadOnlySpan<Av1MotionModel> models)
        where TModel : struct, Av1Ransac.IAv1RansacModel
    {
        // A frame smaller than one field entry has no field to solve.
        if (width < Av1FlowField.DownsampleFactor || height < Av1FlowField.DownsampleFactor)
        {
            return false;
        }

        using Av1ImagePyramid sourcePyramid = new(allocator, width, height);
        using Av1ImagePyramid referencePyramid = new(allocator, width, height);
        int sourceLevels = sourcePyramid.Fill(source[origin..], stride, PyramidLevels);
        int referenceLevels = referencePyramid.Fill(reference[origin..], stride, PyramidLevels);
        if (sourceLevels < 1 || sourceLevels != referenceLevels)
        {
            return false;
        }

        using IMemoryOwner<int> cornerOwner = allocator.Allocate<int>(2 * Av1CornerDetector.MaximumCorners);
        Span<int> corners = cornerOwner.Memory.Span;
        Av1ImagePyramid.Level finest = sourcePyramid.GetLevel(0);
        int cornerCount = Av1CornerDetector.Detect(
            allocator,
            sourcePyramid.GetSamples(0),
            finest.Origin,
            finest.Width,
            finest.Height,
            finest.Stride,
            corners);

        if (cornerCount == 0)
        {
            return false;
        }

        using Av1FlowField flow = new(allocator, finest.Width, finest.Height);
        flow.Compute(allocator, sourcePyramid, referencePyramid, sourceLevels);

        using IMemoryOwner<Av1Correspondence> pointOwner = allocator.Allocate<Av1Correspondence>(cornerCount);
        Span<Av1Correspondence> points = pointOwner.Memory.Span;
        int pointCount = DetermineCorrespondences(
            sourcePyramid, referencePyramid, corners[..(2 * cornerCount)], flow, points);

        return Av1Ransac.Run<TModel>(allocator, points[..pointCount], models);
    }

    /// <summary>
    /// Reads the flow of every corner out of the field and refines it once more.
    /// </summary>
    /// <param name="source">The pyramid of the frame the model maps from.</param>
    /// <param name="reference">The pyramid of the frame the model maps to.</param>
    /// <param name="corners">The column and row of each corner, interleaved.</param>
    /// <param name="flow">The solved flow field.</param>
    /// <param name="points">Receives one correspondence per usable corner.</param>
    /// <returns>The correspondences written.</returns>
    /// <remarks>Reference: determine_disflow_correspondence().</remarks>
    private static int DetermineCorrespondences(
        Av1ImagePyramid source,
        Av1ImagePyramid reference,
        ReadOnlySpan<int> corners,
        Av1FlowField flow,
        Span<Av1Correspondence> points)
    {
        Av1ImagePyramid.Level level = source.GetLevel(0);
        ReadOnlySpan<byte> sourceSamples = source.GetSamples(0);
        ReadOnlySpan<byte> referenceSamples = reference.GetSamples(0);
        ReadOnlySpan<double> horizontal = flow.Horizontal;
        ReadOnlySpan<double> vertical = flow.Vertical;
        Span<double> horizontalKernel = stackalloc double[4];
        Span<double> verticalKernel = stackalloc double[4];

        int count = 0;
        for (int corner = 0; corner < corners.Length / 2; corner++)
        {
            int cornerX = corners[2 * corner];
            int cornerY = corners[(2 * corner) + 1];

            // A field entry stands for the sample at the center of its block, not for the first
            // sample of it. Removing that offset before the position is split makes the whole part
            // name the entry at or before the corner and the fractional part the distance from it.
            int x = cornerX - Av1FlowField.UpsampleCenterOffset;
            int y = cornerY - Av1FlowField.UpsampleCenterOffset;
            int entryX = x >> Av1FlowField.DownsampleShift;
            int entryY = y >> Av1FlowField.DownsampleShift;
            double fractionX = (x & (Av1FlowField.DownsampleFactor - 1)) / (double)Av1FlowField.DownsampleFactor;
            double fractionY = (y & (Av1FlowField.DownsampleFactor - 1)) / (double)Av1FlowField.DownsampleFactor;

            // A corner whose interpolation would reach the edge of the field is dropped. The edge
            // entries are copies rather than solved values, so they would weaken the fit.
            if (entryX < 1 || entryX + 2 >= flow.Width || entryY < 1 || entryY + 2 >= flow.Height)
            {
                continue;
            }

            Av1FlowField.GetCubicKernel(fractionX, horizontalKernel);
            Av1FlowField.GetCubicKernel(fractionY, verticalKernel);
            int entry = flow.Origin + (entryY * flow.Stride) + entryX;
            double flowX = Av1FlowField.Interpolate(horizontal, entry, flow.Stride, horizontalKernel, verticalKernel);
            double flowY = Av1FlowField.Interpolate(vertical, entry, flow.Stride, horizontalKernel, verticalKernel);

            // The interpolated vector is refined against the finest level, which is why that level
            // was never solved as a field: refining the few corners costs far less than refining
            // every entry, and it starts from a better guess.
            Av1DenseFlowSolver.Solve(
                sourceSamples,
                referenceSamples,
                level.Origin,
                cornerX - Av1DenseFlowSolver.PatchCenter,
                cornerY - Av1DenseFlowSolver.PatchCenter,
                level.Width,
                level.Height,
                level.Stride,
                ref flowX,
                ref flowY);

            points[count++] = new Av1Correspondence(cornerX, cornerY, cornerX + flowX, cornerY + flowY);
        }

        return count;
    }
}

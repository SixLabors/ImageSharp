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
/// The estimate has four steps. First, the estimator builds a pyramid of each frame, so it finds a large movement at a coarse level and then
/// refines it. Then it solves a dense flow field from the coarsest level down to the second-finest level. Then it looks up the corners of the
/// source frame in that field. This gives a list of points and where each one moved to. Last, it fits a model to that list and ignores the points
/// that disagree with the rest.
/// </para>
/// </remarks>
internal static class Av1GlobalMotionEstimator
{
    /// <summary>
    /// The number of pyramid levels that the flow search asks for.
    /// </summary>
    /// <remarks>
    /// The frame size sets how many levels the pyramid actually builds.
    /// </remarks>
    public const int PyramidLevels = 12;

    /// <summary>
    /// Defines how one frame of a given sample depth fills a pyramid.
    /// </summary>
    /// <typeparam name="TSample">The component sample type.</typeparam>
    /// <remarks>
    /// A pyramid is always eight bits deep. Thus a frame of greater depth loses its low bits when the fill copies it into the first level.
    /// </remarks>
    internal interface IAv1PyramidFillOperator<TSample>
        where TSample : unmanaged
    {
        /// <summary>
        /// Fills the levels of one pyramid from one frame.
        /// </summary>
        /// <param name="pyramid">The pyramid to fill.</param>
        /// <param name="source">The frame samples, beginning at the first coded sample.</param>
        /// <param name="stride">The frame row stride.</param>
        /// <param name="bitDepth">The coded sample depth.</param>
        /// <param name="levels">The number of levels to fill.</param>
        /// <returns>The number of levels filled.</returns>
        public static abstract int Fill(Av1ImagePyramid pyramid, ReadOnlySpan<TSample> source, int stride, int bitDepth, int levels);
    }

    /// <summary>
    /// Estimates the models that most parts of a frame agree with.
    /// </summary>
    /// <typeparam name="TSample">The component sample type.</typeparam>
    /// <typeparam name="TFill">The way a frame of that depth fills a pyramid.</typeparam>
    /// <typeparam name="TModel">The family of models to fit.</typeparam>
    /// <param name="allocator">The allocator of every buffer this search uses.</param>
    /// <param name="source">The whole luma storage of the frame the model maps from.</param>
    /// <param name="reference">The whole luma storage of the frame the model maps to.</param>
    /// <param name="width">The coded width of both frames, in samples.</param>
    /// <param name="height">The coded height of both frames, in samples.</param>
    /// <param name="stride">The row stride of both storages, in samples.</param>
    /// <param name="origin">The index of the first coded sample of both storages.</param>
    /// <param name="bitDepth">The coded sample depth of both frames.</param>
    /// <param name="downsampleLevel">The pyramid level on which the corners are found.</param>
    /// <param name="models">The models to fill, best first.</param>
    /// <returns>Whether any model was fitted.</returns>
    public static bool Compute<TSample, TFill, TModel>(
        MemoryAllocator allocator,
        ReadOnlySpan<TSample> source,
        ReadOnlySpan<TSample> reference,
        int width,
        int height,
        int stride,
        int origin,
        int bitDepth,
        int downsampleLevel,
        ReadOnlySpan<Av1MotionModel> models)
        where TSample : unmanaged
        where TFill : struct, IAv1PyramidFillOperator<TSample>
        where TModel : struct, Av1Ransac.IAv1RansacModel
    {
        // A frame smaller than one field entry has no field to solve.
        if (width < Av1FlowField.DownsampleFactor || height < Av1FlowField.DownsampleFactor)
        {
            return false;
        }

        using Av1ImagePyramid sourcePyramid = new(allocator, width, height);
        using Av1ImagePyramid referencePyramid = new(allocator, width, height);
        int sourceLevels = TFill.Fill(sourcePyramid, source[origin..], stride, bitDepth, PyramidLevels);
        int referenceLevels = TFill.Fill(referencePyramid, reference[origin..], stride, bitDepth, PyramidLevels);
        if (sourceLevels < 1 || sourceLevels != referenceLevels)
        {
            return false;
        }

        // The corners come from the downsampled level, clamped to the levels that the frame has. The loop below scales them back to full resolution.
        using IMemoryOwner<int> cornerOwner = allocator.Allocate<int>(2 * Av1CornerDetector.MaximumCorners);
        Span<int> corners = cornerOwner.Memory.Span;
        int cornerLevel = Math.Min(downsampleLevel, sourceLevels - 1);
        Av1ImagePyramid.Level cornerPlane = sourcePyramid.GetLevel(cornerLevel);
        int cornerCount = Av1CornerDetector.Detect(
            allocator,
            sourcePyramid.GetSamples(cornerLevel),
            cornerPlane.Origin,
            cornerPlane.Width,
            cornerPlane.Height,
            cornerPlane.Stride,
            corners);

        if (cornerCount == 0)
        {
            return false;
        }

        for (int i = 0; i < 2 * cornerCount; i++)
        {
            corners[i] <<= cornerLevel;
        }

        Av1ImagePyramid.Level finest = sourcePyramid.GetLevel(0);

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
    /// <returns>The number of correspondences written.</returns>
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

            // A field entry stands for the sample at the center of its block, not for its first sample. The code removes that offset before it
            // splits the position. Then the whole part names the entry at or before the corner, and the fractional part is the distance from that
            // entry.
            int x = cornerX - Av1FlowField.UpsampleCenterOffset;
            int y = cornerY - Av1FlowField.UpsampleCenterOffset;
            int entryX = x >> Av1FlowField.DownsampleShift;
            int entryY = y >> Av1FlowField.DownsampleShift;
            double fractionX = (x & (Av1FlowField.DownsampleFactor - 1)) / (double)Av1FlowField.DownsampleFactor;
            double fractionY = (y & (Av1FlowField.DownsampleFactor - 1)) / (double)Av1FlowField.DownsampleFactor;

            // The code drops a corner whose interpolation reaches the edge of the field. The edge entries are copies, not solved values, so they
            // make the fit worse.
            if (entryX < 1 || entryX + 2 >= flow.Width || entryY < 1 || entryY + 2 >= flow.Height)
            {
                continue;
            }

            Av1FlowField.GetCubicKernel(fractionX, horizontalKernel);
            Av1FlowField.GetCubicKernel(fractionY, verticalKernel);
            int entry = flow.Origin + (entryY * flow.Stride) + entryX;
            double flowX = Av1FlowField.Interpolate(horizontal, entry, flow.Stride, horizontalKernel, verticalKernel);
            double flowY = Av1FlowField.Interpolate(vertical, entry, flow.Stride, horizontalKernel, verticalKernel);

            // The solver refines the interpolated vector against the finest level. For this reason, the field does not solve the finest level.
            // A refinement of the few corners costs far less than a refinement of every entry, and it starts from a better estimate.
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

    /// <summary>
    /// Fills a pyramid from an eight-bit frame, which needs no conversion.
    /// </summary>
    internal readonly struct ByteFillOperator : IAv1PyramidFillOperator<byte>
    {
        /// <inheritdoc/>
        public static int Fill(Av1ImagePyramid pyramid, ReadOnlySpan<byte> source, int stride, int bitDepth, int levels)
            => pyramid.Fill(source, stride, levels);
    }

    /// <summary>
    /// Fills a pyramid from a high-bit-depth frame, dropping the low bits of every sample.
    /// </summary>
    internal readonly struct UInt16FillOperator : IAv1PyramidFillOperator<ushort>
    {
        /// <inheritdoc/>
        public static int Fill(Av1ImagePyramid pyramid, ReadOnlySpan<ushort> source, int stride, int bitDepth, int levels)
            => pyramid.Fill(source, stride, bitDepth, levels);
    }
}

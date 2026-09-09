// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.ReferenceFrames;

/// <summary>
/// Extends reconstructed AV1 edge samples through the padded reference-frame border.
/// </summary>
internal static class Av1ReferenceFrameBorder
{
    /// <summary>
    /// Replicates every visible plane edge through its complete decoder padding.
    /// </summary>
    /// <param name="frameBuffer">The post-restoration reference frame whose padding is extended.</param>
    public static void Extend(Av1FrameBuffer<byte> frameBuffer)
    {
        ObuColorConfig colorConfig = frameBuffer.ColorConfig;
        int subsamplingX = !colorConfig.IsMonochrome && colorConfig.SubSamplingX ? 1 : 0;
        int subsamplingY = !colorConfig.IsMonochrome && colorConfig.SubSamplingY ? 1 : 0;

        ExtendPlane(
            frameBuffer,
            frameBuffer.GetPlaneBuffer(Av1Plane.Y),
            frameBuffer.OriginX,
            frameBuffer.OriginY,
            frameBuffer.Width,
            frameBuffer.Height);

        if (!colorConfig.IsMonochrome)
        {
            int chromaWidth = Av1Math.DivideLog2Ceiling(frameBuffer.Width, subsamplingX);
            int chromaHeight = Av1Math.DivideLog2Ceiling(frameBuffer.Height, subsamplingY);
            int chromaOriginX = frameBuffer.OriginX >> subsamplingX;
            int chromaOriginY = frameBuffer.OriginY >> subsamplingY;

            ExtendPlane(frameBuffer, frameBuffer.GetPlaneBuffer(Av1Plane.U), chromaOriginX, chromaOriginY, chromaWidth, chromaHeight);
            ExtendPlane(frameBuffer, frameBuffer.GetPlaneBuffer(Av1Plane.V), chromaOriginX, chromaOriginY, chromaWidth, chromaHeight);
        }
    }

    /// <summary>
    /// Selects the native sample representation for one byte-backed plane.
    /// </summary>
    /// <param name="frameBuffer">The frame that defines the native sample size.</param>
    /// <param name="buffer">The padded plane allocation.</param>
    /// <param name="originX">The horizontal visible origin in plane samples.</param>
    /// <param name="originY">The vertical visible origin in rows.</param>
    /// <param name="width">The visible plane width.</param>
    /// <param name="height">The visible plane height.</param>
    private static void ExtendPlane(
        Av1FrameBuffer<byte> frameBuffer,
        Buffer2D<byte> buffer,
        int originX,
        int originY,
        int width,
        int height)
    {
        if (frameBuffer.BytesPerSample == 2)
        {
            ExtendPlane(MemoryMarshal.Cast<byte, ushort>(buffer.DangerousGetSingleSpan()), buffer.Width >> 1, originX, originY, width, height);
        }
        else
        {
            ExtendPlane(buffer.DangerousGetSingleSpan(), buffer.Width, originX, originY, width, height);
        }
    }

    /// <summary>
    /// Extends one native sample plane horizontally and then vertically.
    /// </summary>
    /// <typeparam name="TSample">The native eight-bit or high-bit-depth sample type.</typeparam>
    /// <param name="plane">The complete padded plane allocation.</param>
    /// <param name="stride">The number of native samples between adjacent rows.</param>
    /// <param name="originX">The horizontal visible origin in plane samples.</param>
    /// <param name="originY">The vertical visible origin in rows.</param>
    /// <param name="width">The visible plane width.</param>
    /// <param name="height">The visible plane height.</param>
    private static void ExtendPlane<TSample>(Span<TSample> plane, int stride, int originX, int originY, int width, int height)
        where TSample : unmanaged
    {
        int rightStart = originX + width;
        int rightLength = stride - rightStart;

        for (int row = 0; row < height; row++)
        {
            Span<TSample> destinationRow = plane.Slice((originY + row) * stride, stride);

            // Span.Fill maps these long constant runs to the runtime's vectorized fill implementation. Extending the
            // horizontal edges first also makes each later full-row copy include complete left and right padding.
            destinationRow[..originX].Fill(destinationRow[originX]);
            destinationRow.Slice(rightStart, rightLength).Fill(destinationRow[rightStart - 1]);
        }

        ReadOnlySpan<TSample> firstVisibleRow = plane.Slice(originY * stride, stride);
        for (int row = 0; row < originY; row++)
        {
            firstVisibleRow.CopyTo(plane.Slice(row * stride, stride));
        }

        int bottomStart = originY + height;
        ReadOnlySpan<TSample> lastVisibleRow = plane.Slice((bottomStart - 1) * stride, stride);
        for (int row = bottomStart; row < plane.Length / stride; row++)
        {
            lastVisibleRow.CopyTo(plane.Slice(row * stride, stride));
        }
    }
}

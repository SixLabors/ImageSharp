// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.IO.FrameHeader;

/// <summary>
/// Helper methods associated with JxlColorTransform.
/// </summary>
internal static class JxlColorTransformHelpers
{
    private static ReadOnlySpan<int> Grayscale => [0, 0, 0];

    private static ReadOnlySpan<int> YCbCr => [1, 0, 2];

    private static ReadOnlySpan<int> None => [0, 1, 2];

    public static ReadOnlySpan<int> GetJpegOrder(JxlColorTransform transform, bool isGraysacle)
    {
        if (isGraysacle)
        {
            return Grayscale;
        }

        if (transform == JxlColorTransform.YCbCr)
        {
            return YCbCr;
        }

        return None;
    }
}

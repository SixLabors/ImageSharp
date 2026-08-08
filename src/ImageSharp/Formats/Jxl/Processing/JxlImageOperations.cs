// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing;

internal static class JxlImageOperations
{
    /// <summary>
    /// Returns true if first image has same width and height as the second image.
    /// </summary>
    /// <param name="a">First image</param>
    /// <param name="b">Second image</param>
    /// <returns>True if width and height is equal.</returns>
    public static bool SameSize(JxlPlaneBase a, JxlPlaneBase b) => a.XSize == b.XSize && a.YSize == b.YSize;

    public static bool CopyImage<T>(JxlPlane<T> from, JxlPlane<T> to)
        where T : unmanaged
    {
        if (!SameSize(from, to))
        {
            return false;
        }

        if (from.XSize == 0 || from.YSize == 0)
        {
            return true;
        }

        for (int y = 0; y < from.YSize; y++)
        {
            Span<T> rowFrom = from.GetRow(y);
            Span<T> rowTo = to.GetRow(y);
            rowFrom.CopyTo(rowTo);
        }

        return true;
    }

    public static bool CopyImageTo<T>(Rectangle rectFrom, JxlPlane<T> from, Rectangle rectTo, JxlPlane<T> to)
        where T : unmanaged
    {
        if (rectFrom != rectTo)
        {
            return false;
        }

        
    }
}

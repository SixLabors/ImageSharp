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

        if (!from.IsRectangleInside(rectFrom))
        {
            return false;
        }

        if (!to.IsRectangleInside(rectTo))
        {
            return false;
        }

        if (rectFrom.Width == 0)
        {
            return true;
        }

        for (int y = 0; y < rectFrom.Height; y++)
        {
            Span<T> rowFrom = from.GetRow(rectFrom, y);
            Span<T> rowTo = to.GetRow(rectTo, y);
            rowFrom.CopyTo(rowTo);
        }

        return true;
    }

    public static bool CopyImageTo<T>(Rectangle rectFrom, JxlImage3<T> from, Rectangle rectTo, JxlImage3<T> to)
        where T : unmanaged
    {
        if (rectFrom != rectTo)
        {
            return false;
        }

        for (int plane = 0; plane < 3; plane++)
        {
            if (!CopyImageTo(rectFrom, from.Plane(plane), rectTo, to.Plane(plane)))
            {
                return false;
            }
        }

        return true;
    }
}

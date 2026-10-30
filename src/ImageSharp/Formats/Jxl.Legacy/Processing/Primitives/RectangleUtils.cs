// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;

internal static class RectangleUtils
{
    public static int X0(this in Rectangle rect) => rect.X;

    public static int Y0(this in Rectangle rect) => rect.Y;

    public static int X1(this in Rectangle rect) => rect.X + rect.Width;

    public static int Y1(this in Rectangle rect) => rect.Y + rect.Height;

    public static Rectangle Extend(Rectangle curr, int border, Rectangle parent)
    {
        int newX0 = X0(in curr) > X0(in parent) + border ? X0(in curr) - border : X0(in parent);
        int newY0 = Y0(in curr) > Y0(in parent) + border ? Y0(in curr) - border : Y0(in parent);
        int newX1 = X1(in curr) + border > X1(in parent) ? X1(in parent) : X1(in curr) + border;
        int newY1 = Y1(in curr) + border > Y1(in parent) ? Y1(in parent) : Y1(in curr) + border;

        return new(newX0, newY0, newX1 - newX0, newY1 - newY0);
    }

    public static bool IsInside(this Rectangle a, Rectangle b) =>
        a.X0() >= b.X0() &&
        a.X1() <= b.X1() &&
        a.Y0() >= b.Y0() &&
        a.Y1() <= b.Y1();

    public static Rectangle CreateRectangle(int xbegin, int ybegin, int xSizeMax, int ySizeMax, int xEnd, int yEnd)
        => new(xbegin, ybegin, ClampedSize(xbegin, xSizeMax, xEnd), ClampedSize(ybegin, ySizeMax, yEnd));

    private static int ClampedSize(int begin, int sizeMax, int end)
        => begin + sizeMax <= end
            ? sizeMax
            : (end > begin ? end - begin : 0);
}

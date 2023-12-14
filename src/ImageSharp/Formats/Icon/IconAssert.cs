// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Icon;

internal class IconAssert
{
    internal static void CanSeek(Stream stream)
    {
        if (!stream.CanSeek)
        {
            throw new NotSupportedException("This stream cannot support seek");
        }
    }

    internal static int EndOfStream(int v, int length)
    {
        if (v != length)
        {
            throw new EndOfStreamException();
        }

        return v;
    }

    internal static long EndOfStream(long v, long length)
    {
        if (v != length)
        {
            throw new EndOfStreamException();
        }

        return v;
    }

    internal static void NotSquare(in Size size)
    {
        if (size.Width != size.Height)
        {
            throw new FormatException("This image is not square.");
        }
    }
}

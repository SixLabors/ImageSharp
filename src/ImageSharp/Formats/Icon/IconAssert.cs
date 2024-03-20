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

    internal static byte Is1ByteSize(int i)
    {
        if (i is 256)
        {
            return 0;
        }
        else if (i > byte.MaxValue)
        {
            throw new FormatException("Image size Too Large.");
        }

        return (byte)i;
    }
}

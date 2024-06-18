// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Icon;

internal class IconAssert
{
    internal static int EndOfStream(int v, int length)
    {
        if (v != length)
        {
            throw new EndOfStreamException();
        }

        return v;
    }
}

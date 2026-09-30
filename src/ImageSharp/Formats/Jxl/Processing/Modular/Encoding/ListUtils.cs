// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Encoding;

internal static class ListUtils
{
    public static void Grow<T>(this List<T> list, T item, int desiredSize)
    {
        while (list.Count < desiredSize)
        {
            list.Add(item);
        }
    }
}

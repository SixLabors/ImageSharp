// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing;

internal static unsafe class JxlUnsafe
{
    public static bool IsSimdAligned<T>(T* ptr)
        where T : unmanaged
        => IsAligned(ptr, Vector<T>.Count * sizeof(T));

    public static bool IsAligned<T>(T* ptr, int alignment)
        where T : unmanaged
        => ((long)ptr % alignment) == 0;
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing;

internal static partial class JxlSimdUtils
{
    public static unsafe void StoreInterleaved<T>(Vector<T> v1, Vector<T> v2, ref T memory)
        where T : unmanaged
    {
        v1.StoreUnsafe(ref Unsafe.Add(ref memory, Vector<T>.Count * 0));
        v2.StoreUnsafe(ref Unsafe.Add(ref memory, Vector<T>.Count * 1));
    }

    public static unsafe void StoreInterleaved<T>(Vector<T> v1, Vector<T> v2, Vector<T> v3, ref T memory)
        where T : unmanaged
    {
        v1.StoreUnsafe(ref Unsafe.Add(ref memory, Vector<T>.Count * 0));
        v2.StoreUnsafe(ref Unsafe.Add(ref memory, Vector<T>.Count * 1));
        v3.StoreUnsafe(ref Unsafe.Add(ref memory, Vector<T>.Count * 2));
    }

    public static unsafe void StoreInterleaved<T>(Vector<T> v1, Vector<T> v2, Vector<T> v3, Vector<T> v4, ref T memory)
        where T : unmanaged
    {
        v1.StoreUnsafe(ref Unsafe.Add(ref memory, Vector<T>.Count * 0));
        v2.StoreUnsafe(ref Unsafe.Add(ref memory, Vector<T>.Count * 1));
        v3.StoreUnsafe(ref Unsafe.Add(ref memory, Vector<T>.Count * 2));
        v4.StoreUnsafe(ref Unsafe.Add(ref memory, Vector<T>.Count * 3));
    }

    public static unsafe void StoreInterleaved<T>(Vector<T> v1, Vector<T> v2, Vector<T> v3, Vector<T> v4, Vector<T> v5, ref T memory)
        where T : unmanaged
    {
        v1.StoreUnsafe(ref Unsafe.Add(ref memory, Vector<T>.Count * 0));
        v2.StoreUnsafe(ref Unsafe.Add(ref memory, Vector<T>.Count * 1));
        v3.StoreUnsafe(ref Unsafe.Add(ref memory, Vector<T>.Count * 2));
        v4.StoreUnsafe(ref Unsafe.Add(ref memory, Vector<T>.Count * 3));
        v5.StoreUnsafe(ref Unsafe.Add(ref memory, Vector<T>.Count * 4));
    }

    public static unsafe void StoreInterleaved<T>(Vector<T> v1, Vector<T> v2, Vector<T> v3, Vector<T> v4, Vector<T> v5, Vector<T> v6, ref T memory)
        where T : unmanaged
    {
        v1.StoreUnsafe(ref Unsafe.Add(ref memory, Vector<T>.Count * 0));
        v2.StoreUnsafe(ref Unsafe.Add(ref memory, Vector<T>.Count * 1));
        v3.StoreUnsafe(ref Unsafe.Add(ref memory, Vector<T>.Count * 2));
        v4.StoreUnsafe(ref Unsafe.Add(ref memory, Vector<T>.Count * 3));
        v5.StoreUnsafe(ref Unsafe.Add(ref memory, Vector<T>.Count * 4));
        v6.StoreUnsafe(ref Unsafe.Add(ref memory, Vector<T>.Count * 5));
    }

    public static unsafe void StoreInterleaved<T>(Vector128<T> v1, Vector128<T> v2, ref T memory)
        where T : unmanaged
    {
        v1.StoreUnsafe(ref Unsafe.Add(ref memory, Vector<T>.Count * 0));
        v2.StoreUnsafe(ref Unsafe.Add(ref memory, Vector<T>.Count * 1));
    }

    public static unsafe void StoreInterleaved<T>(Vector128<T> v1, Vector128<T> v2, Vector128<T> v3, ref T memory)
        where T : unmanaged
    {
        v1.StoreUnsafe(ref Unsafe.Add(ref memory, Vector<T>.Count * 0));
        v2.StoreUnsafe(ref Unsafe.Add(ref memory, Vector<T>.Count * 1));
        v3.StoreUnsafe(ref Unsafe.Add(ref memory, Vector<T>.Count * 2));
    }

    public static unsafe void StoreInterleaved<T>(Vector128<T> v1, Vector128<T> v2, Vector128<T> v3, Vector128<T> v4, ref T memory)
        where T : unmanaged
    {
        v1.StoreUnsafe(ref Unsafe.Add(ref memory, Vector<T>.Count * 0));
        v2.StoreUnsafe(ref Unsafe.Add(ref memory, Vector<T>.Count * 1));
        v3.StoreUnsafe(ref Unsafe.Add(ref memory, Vector<T>.Count * 2));
        v4.StoreUnsafe(ref Unsafe.Add(ref memory, Vector<T>.Count * 3));
    }

    public static unsafe void StoreInterleaved<T>(Vector128<T> v1, Vector128<T> v2, Vector128<T> v3, Vector128<T> v4, Vector128<T> v5, ref T memory)
        where T : unmanaged
    {
        v1.StoreUnsafe(ref Unsafe.Add(ref memory, Vector<T>.Count * 0));
        v2.StoreUnsafe(ref Unsafe.Add(ref memory, Vector<T>.Count * 1));
        v3.StoreUnsafe(ref Unsafe.Add(ref memory, Vector<T>.Count * 2));
        v4.StoreUnsafe(ref Unsafe.Add(ref memory, Vector<T>.Count * 3));
        v5.StoreUnsafe(ref Unsafe.Add(ref memory, Vector<T>.Count * 4));
    }

    public static unsafe void StoreInterleaved<T>(Vector128<T> v1, Vector128<T> v2, Vector128<T> v3, Vector128<T> v4, Vector128<T> v5, Vector128<T> v6, ref T memory)
        where T : unmanaged
    {
        v1.StoreUnsafe(ref Unsafe.Add(ref memory, Vector<T>.Count * 0));
        v2.StoreUnsafe(ref Unsafe.Add(ref memory, Vector<T>.Count * 1));
        v3.StoreUnsafe(ref Unsafe.Add(ref memory, Vector<T>.Count * 2));
        v4.StoreUnsafe(ref Unsafe.Add(ref memory, Vector<T>.Count * 3));
        v5.StoreUnsafe(ref Unsafe.Add(ref memory, Vector<T>.Count * 4));
        v6.StoreUnsafe(ref Unsafe.Add(ref memory, Vector<T>.Count * 5));
    }

    public static unsafe void StoreInterleaved<T>(Vector256<T> v1, Vector256<T> v2, ref T memory)
        where T : unmanaged
    {
        v1.StoreUnsafe(ref Unsafe.Add(ref memory, Vector<T>.Count * 0));
        v2.StoreUnsafe(ref Unsafe.Add(ref memory, Vector<T>.Count * 1));
    }

    public static unsafe void StoreInterleaved<T>(Vector256<T> v1, Vector256<T> v2, Vector256<T> v3, ref T memory)
        where T : unmanaged
    {
        v1.StoreUnsafe(ref Unsafe.Add(ref memory, Vector<T>.Count * 0));
        v2.StoreUnsafe(ref Unsafe.Add(ref memory, Vector<T>.Count * 1));
        v3.StoreUnsafe(ref Unsafe.Add(ref memory, Vector<T>.Count * 2));
    }

    public static unsafe void StoreInterleaved<T>(Vector256<T> v1, Vector256<T> v2, Vector256<T> v3, Vector256<T> v4, ref T memory)
        where T : unmanaged
    {
        v1.StoreUnsafe(ref Unsafe.Add(ref memory, Vector<T>.Count * 0));
        v2.StoreUnsafe(ref Unsafe.Add(ref memory, Vector<T>.Count * 1));
        v3.StoreUnsafe(ref Unsafe.Add(ref memory, Vector<T>.Count * 2));
        v4.StoreUnsafe(ref Unsafe.Add(ref memory, Vector<T>.Count * 3));
    }

    public static unsafe void StoreInterleaved<T>(Vector256<T> v1, Vector256<T> v2, Vector256<T> v3, Vector256<T> v4, Vector256<T> v5, ref T memory)
        where T : unmanaged
    {
        v1.StoreUnsafe(ref Unsafe.Add(ref memory, Vector<T>.Count * 0));
        v2.StoreUnsafe(ref Unsafe.Add(ref memory, Vector<T>.Count * 1));
        v3.StoreUnsafe(ref Unsafe.Add(ref memory, Vector<T>.Count * 2));
        v4.StoreUnsafe(ref Unsafe.Add(ref memory, Vector<T>.Count * 3));
        v5.StoreUnsafe(ref Unsafe.Add(ref memory, Vector<T>.Count * 4));
    }

    public static unsafe void StoreInterleaved<T>(Vector256<T> v1, Vector256<T> v2, Vector256<T> v3, Vector256<T> v4, Vector256<T> v5, Vector256<T> v6, ref T memory)
        where T : unmanaged
    {
        v1.StoreUnsafe(ref Unsafe.Add(ref memory, Vector<T>.Count * 0));
        v2.StoreUnsafe(ref Unsafe.Add(ref memory, Vector<T>.Count * 1));
        v3.StoreUnsafe(ref Unsafe.Add(ref memory, Vector<T>.Count * 2));
        v4.StoreUnsafe(ref Unsafe.Add(ref memory, Vector<T>.Count * 3));
        v5.StoreUnsafe(ref Unsafe.Add(ref memory, Vector<T>.Count * 4));
        v6.StoreUnsafe(ref Unsafe.Add(ref memory, Vector<T>.Count * 5));
    }

}

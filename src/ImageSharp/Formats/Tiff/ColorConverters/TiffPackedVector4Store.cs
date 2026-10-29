// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Tiff.ColorConverters;

/// <summary>
/// Writes packed RGBA float registers directly in destination pixel order.
/// </summary>
internal readonly struct TiffPackedVector4Store : ITiffColorStore<float, Vector4>
{
    /// <inheritdoc/>
    public static int WordsPerPixel => 1;

    /// <inheritdoc/>
    public void Store(ref Vector4 destination, float c0, float c1, float c2, float c3) => destination = new Vector4(c0, c1, c2, c3);

    /// <inheritdoc/>
    public void Store(ref Vector4 destination, Vector128<float> c0, Vector128<float> c1, Vector128<float> c2, Vector128<float> c3)
    {
        ref float first = ref Unsafe.As<Vector4, float>(ref destination);
        c0.StoreUnsafe(ref first);
        c1.StoreUnsafe(ref first, 4);
        c2.StoreUnsafe(ref first, 8);
        c3.StoreUnsafe(ref first, 12);
    }

    /// <inheritdoc/>
    public void Store(ref Vector4 destination, Vector256<float> c0, Vector256<float> c1, Vector256<float> c2, Vector256<float> c3)
    {
        ref float first = ref Unsafe.As<Vector4, float>(ref destination);
        c0.StoreUnsafe(ref first);
        c1.StoreUnsafe(ref first, 8);
        c2.StoreUnsafe(ref first, 16);
        c3.StoreUnsafe(ref first, 24);
    }

    /// <inheritdoc/>
    public void Store(ref Vector4 destination, Vector512<float> c0, Vector512<float> c1, Vector512<float> c2, Vector512<float> c3)
    {
        ref float first = ref Unsafe.As<Vector4, float>(ref destination);
        c0.StoreUnsafe(ref first);
        c1.StoreUnsafe(ref first, 16);
        c2.StoreUnsafe(ref first, 32);
        c3.StoreUnsafe(ref first, 48);
    }
}

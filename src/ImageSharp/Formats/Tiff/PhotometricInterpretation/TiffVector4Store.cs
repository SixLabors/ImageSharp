// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

/// <summary>
/// Interleaves four floating component lanes into scaled RGBA vectors.
/// </summary>
internal readonly struct TiffVector4Store : ITiffColorStore<float, Vector4>
{
    /// <inheritdoc/>
    public static int WordsPerPixel => 1;

    /// <inheritdoc/>
    public void Store(ref Vector4 destination, float c0, float c1, float c2, float c3) => destination = new Vector4(c0, c1, c2, c3);

    /// <inheritdoc/>
    public void Store(ref Vector4 destination, Vector128<float> c0, Vector128<float> c1, Vector128<float> c2, Vector128<float> c3)
    {
        SimdUtils.Transpose4(c0, c1, c2, c3, out Vector128<float> pixel0, out Vector128<float> pixel1, out Vector128<float> pixel2, out Vector128<float> pixel3);
        ref Vector128<float> first = ref Unsafe.As<Vector4, Vector128<float>>(ref destination);
        first = pixel0;
        Unsafe.Add(ref first, 1) = pixel1;
        Unsafe.Add(ref first, 2) = pixel2;
        Unsafe.Add(ref first, 3) = pixel3;
    }

    /// <inheritdoc/>
    public void Store(ref Vector4 destination, Vector256<float> c0, Vector256<float> c1, Vector256<float> c2, Vector256<float> c3)
    {
        SimdUtils.Interleave4(c0, c1, c2, c3, ref Unsafe.As<Vector4, float>(ref destination));
    }

    /// <inheritdoc/>
    public void Store(ref Vector4 destination, Vector512<float> c0, Vector512<float> c1, Vector512<float> c2, Vector512<float> c3)
    {
        SimdUtils.Interleave4(c0, c1, c2, c3, ref Unsafe.As<Vector4, float>(ref destination));
    }
}

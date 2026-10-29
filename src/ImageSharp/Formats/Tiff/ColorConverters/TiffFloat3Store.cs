// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Tiff.ColorConverters;

/// <summary>
/// Interleaves three floating-point components without changing their values.
/// </summary>
/// <remarks>
/// The input registers contain one component per lane. The output stores three consecutive floats per pixel.
/// The fourth component is ignored because the stored layout has no alpha sample.
/// </remarks>
internal readonly struct TiffFloat3Store : ITiffColorStore<float, float>
{
    /// <inheritdoc />
    public static int WordsPerPixel => 3;

    /// <inheritdoc />
    public void Store(ref float destination, float c0, float c1, float c2, float c3)
    {
        destination = c0;
        Unsafe.Add(ref destination, 1U) = c1;
        Unsafe.Add(ref destination, 2U) = c2;
    }

    /// <inheritdoc />
    public void Store(ref float destination, Vector128<float> c0, Vector128<float> c1, Vector128<float> c2, Vector128<float> c3)
        => SimdUtils.Interleave3(c0, c1, c2, ref destination);

    /// <inheritdoc />
    public void Store(ref float destination, Vector256<float> c0, Vector256<float> c1, Vector256<float> c2, Vector256<float> c3)
        => SimdUtils.Interleave3(c0, c1, c2, ref destination);

    /// <inheritdoc />
    public void Store(ref float destination, Vector512<float> c0, Vector512<float> c1, Vector512<float> c2, Vector512<float> c3)
        => SimdUtils.Interleave3(c0, c1, c2, ref destination);
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Tiff.ColorConverters;

/// <summary>
/// Stores the first converted component as one native-endian grayscale float per pixel.
/// </summary>
/// <remarks>
/// Each lane of c0 holds one pixel's grayscale value. Store that register directly; c1, c2 and c3 are not encoded.
/// The shared traversal supplies complete SIMD groups and sends the final pixels through the scalar store.
/// </remarks>
internal readonly struct TiffGrayFloat32Store : ITiffColorStore<float, float>
{
    /// <inheritdoc />
    public static int WordsPerPixel => 1;

    /// <inheritdoc />
    public void Store(ref float destination, float c0, float c1, float c2, float c3) => destination = c0;

    /// <inheritdoc />
    public void Store(ref float destination, Vector128<float> c0, Vector128<float> c1, Vector128<float> c2, Vector128<float> c3)
        => c0.StoreUnsafe(ref destination);

    /// <inheritdoc />
    public void Store(ref float destination, Vector256<float> c0, Vector256<float> c1, Vector256<float> c2, Vector256<float> c3)
        => c0.StoreUnsafe(ref destination);

    /// <inheritdoc />
    public void Store(ref float destination, Vector512<float> c0, Vector512<float> c1, Vector512<float> c2, Vector512<float> c3)
        => c0.StoreUnsafe(ref destination);
}

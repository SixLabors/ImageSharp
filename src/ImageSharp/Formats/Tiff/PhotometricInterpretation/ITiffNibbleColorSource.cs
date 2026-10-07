// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

/// <summary>
/// Reads a byte-padded row of regularly packed four-bit color components.
/// </summary>
internal interface ITiffNibbleColorSource
{
    /// <summary>
    /// Gets the number of stored bits per pixel.
    /// </summary>
    int BitsPerPixel { get; }

    /// <summary>
    /// Gets the number of stored color components per pixel.
    /// </summary>
    int ComponentCount { get; }

    /// <summary>
    /// Unpacks four complete pixels into component registers.
    /// </summary>
    /// <param name="row">The stored row bytes.</param>
    /// <param name="index">The first pixel index within the row.</param>
    /// <param name="c0">Four red codes.</param>
    /// <param name="c1">Four green codes.</param>
    /// <param name="c2">Four blue codes.</param>
    /// <param name="c3">Four alpha codes, or zero for RGB.</param>
    void ReadFour(ReadOnlySpan<byte> row, int index, out Vector128<float> c0, out Vector128<float> c1, out Vector128<float> c2, out Vector128<float> c3);
}

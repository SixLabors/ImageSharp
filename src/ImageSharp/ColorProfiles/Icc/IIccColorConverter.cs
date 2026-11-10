// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;

namespace SixLabors.ImageSharp.ColorProfiles.Icc;

/// <summary>
/// Provides scalar and bulk conversion through an ICC transform selected during profile setup.
/// </summary>
internal interface IIccColorConverter
{
    /// <summary>
    /// Gets a value indicating whether the transform uses tone response curves and a matrix.
    /// </summary>
    bool IsTrc { get; }

    /// <summary>
    /// Gets a value indicating whether the transform uses a sixteen-bit LUT's legacy PCS encoding.
    /// </summary>
    bool Is16BitLut { get; }

    /// <summary>
    /// Converts one color.
    /// </summary>
    /// <param name="value">The source components.</param>
    /// <returns>The converted components.</returns>
    Vector4 Convert(Vector4 value);

    /// <summary>
    /// Converts complete SIMD blocks and the scalar remainder, including when the spans are the same.
    /// </summary>
    /// <param name="source">The source components.</param>
    /// <param name="destination">Storage for at least as many colors as the source.</param>
    /// <param name="convertW">
    /// <see langword="true"/> when W is a color component of the source or target profile, as K is for CMYK.
    /// The operator then reads and writes W.
    /// <see langword="false"/> when W is not color data, for example the alpha of an RGB pixel.
    /// The operator then receives W = 1, the value that <see cref="Rgb.ToScaledVector4()"/> supplies,
    /// and the destination keeps the source W.
    /// </param>
    void Convert(ReadOnlySpan<Vector4> source, Span<Vector4> destination, bool convertW);
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1;

/// <summary>
/// Provides sample-precision conversions for AV1 bit-depth values.
/// </summary>
internal static class Av1BitDepthExtensions
{
    /// <summary>
    /// Gets the number of bits represented by an AV1 bit-depth value.
    /// </summary>
    /// <param name="bitDepth">The AV1 bit-depth value.</param>
    /// <returns>Eight, ten, or twelve.</returns>
    public static int GetBitCount(this Av1BitDepth bitDepth) => 8 + ((int)bitDepth << 1);
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <summary>
/// Describes the reusable storage required by an AV1 two-dimensional transform operation.
/// </summary>
/// <remarks>
/// The workspace reserves three maximum-length transform vectors for input, output, and stage exchange, followed by
/// full-block raster buffers and the fixed AVX-512 transpose area. All storage is expressed as integers so callers can
/// rent one buffer and reinterpret aligned prefixes for 16-bit or 32-bit SIMD lanes without per-block allocations.
/// </remarks>
internal static class Av1TransformWorkspace
{
    /// <summary>
    /// The number of integer elements occupied by the three 512-bit transform vectors.
    /// </summary>
    public const int Vector512StorageLength = 3 * Av1Constants.MaxTransformSize * 16;

    /// <summary>
    /// The number of integer elements occupied by the AVX-512 transpose scratch.
    /// </summary>
    public const int Vector512TransposeStorageLength = 16 * 8 * 2;

    /// <summary>
    /// The number of integer elements occupied by the three 256-bit transform vectors.
    /// </summary>
    public const int Vector256StorageLength = 3 * Av1Constants.MaxTransformSize * 8;

    /// <summary>
    /// The number of integer elements occupied by the three 128-bit transform vectors.
    /// </summary>
    public const int Vector128StorageLength = 3 * Av1Constants.MaxTransformSize * 4;

    /// <summary>
    /// The number of integers required for the largest supported transform block.
    /// </summary>
    public const int MaximumLength =
        (2 * Av1Constants.MaxTransformSize * Av1Constants.MaxTransformSize)
        + Vector512StorageLength
        + Vector512TransposeStorageLength;

    /// <summary>
    /// Gets the number of integers required for a transform size.
    /// </summary>
    /// <param name="transformSize">The transform-block dimensions.</param>
    /// <returns>The required workspace length.</returns>
    public static int GetRequiredLength(Av1TransformSize transformSize)
    {
        // The widest packed transform evaluates thirty-two independent axes together. Small AV1 blocks are padded
        // to that lane count, so their workspace requirement is determined by the vector tile rather than the
        // coded coefficient count.
        int width = Math.Max(transformSize.GetWidth(), Vector512<short>.Count);
        int height = Math.Max(transformSize.GetHeight(), Vector512<short>.Count);
        int blockLength = width * height;

        return (2 * blockLength) + Vector512StorageLength + Vector512TransposeStorageLength;
    }
}

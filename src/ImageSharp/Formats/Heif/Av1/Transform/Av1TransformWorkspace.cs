// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <summary>
/// Describes the reusable storage required by an AV1 two-dimensional transform operation.
/// </summary>
internal static class Av1TransformWorkspace
{
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
    public const int MaximumLength = (Av1Constants.MaxTransformSize * Av1Constants.MaxTransformSize) + Vector256StorageLength;

    /// <summary>
    /// Gets the number of integers required for a transform size.
    /// </summary>
    /// <param name="transformSize">The transform-block dimensions.</param>
    /// <returns>The required workspace length.</returns>
    public static int GetRequiredLength(Av1TransformSize transformSize)
    {
        return (transformSize.GetWidth() * transformSize.GetHeight()) + Vector256StorageLength;
    }
}

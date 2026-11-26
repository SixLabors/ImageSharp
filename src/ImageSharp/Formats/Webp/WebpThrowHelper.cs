// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;

namespace SixLabors.ImageSharp.Formats.Webp;

internal static class WebpThrowHelper
{
    [DoesNotReturn]
    public static void ThrowInvalidImageContentException(string errorMessage) => throw new InvalidImageContentException(errorMessage);

    [DoesNotReturn]
    public static void ThrowDimensionsTooLarge(int width, int height)
        => throw new ImageFormatException($"Image is too large to encode at {width}x{height} for WEBP format.");

    /// <summary>
    /// Throws an <see cref="ImageFormatException"/> for a RIFF chunk that does not fit in the 32-bit size field.
    /// </summary>
    /// <param name="size">The size of the chunk data in bytes.</param>
    [DoesNotReturn]
    public static void ThrowChunkTooLarge(long size)
        => throw new ImageFormatException($"The RIFF chunk size {size} is larger than the 32-bit size field allows.");

    /// <summary>
    /// Throws an <see cref="ImageFormatException"/> for a WebP file that is larger than the WebP container allows.
    /// </summary>
    /// <param name="size">The value of the File Size field in bytes.</param>
    [DoesNotReturn]
    public static void ThrowFileTooLarge(long size)
        => throw new ImageFormatException($"The WebP file size {size} is larger than the WebP container allows.");
}

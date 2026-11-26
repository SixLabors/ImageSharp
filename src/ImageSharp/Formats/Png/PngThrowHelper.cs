// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;

namespace SixLabors.ImageSharp.Formats.Png;

internal static class PngThrowHelper
{
    [DoesNotReturn]
    public static void ThrowInvalidImageContentException(string errorMessage) => throw new InvalidImageContentException(errorMessage);

    [DoesNotReturn]
    public static void ThrowInvalidHeader()
        => throw new InvalidImageContentException("PNG Image must contain a header chunk and it must be located before any other chunks.");

    [DoesNotReturn]
    public static void ThrowNoData() => throw new InvalidImageContentException("PNG Image does not contain a data chunk.");

    [DoesNotReturn]
    public static void ThrowMissingDefaultData() => throw new InvalidImageContentException("APNG Image does not contain a default data chunk.");

    [DoesNotReturn]
    public static void ThrowMissingPalette() => throw new InvalidImageContentException("PNG Image does not contain a palette chunk.");

    [DoesNotReturn]
    public static void ThrowInvalidChunkCrc(string chunkTypeName)
        => throw new InvalidImageContentException($"CRC Error. PNG {chunkTypeName} chunk is corrupt!");

    /// <summary>
    /// Throws a <see cref="NotSupportedException"/> for a critical chunk that the decoder does not know. The decoder cannot decode the image without the data
    /// of a critical chunk.
    /// </summary>
    /// <param name="chunkTypeName">The four-letter type of the chunk.</param>
    [DoesNotReturn]
    public static void ThrowUnknownCriticalChunk(string chunkTypeName)
        => throw new NotSupportedException($"The PNG {chunkTypeName} chunk is critical, and the decoder does not know it.");
}

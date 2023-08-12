// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;

namespace SixLabors.ImageSharp.Formats.Png;

internal static class PngThrowHelper
{
    [DoesNotReturn]
    public static void ThrowInvalidImageContentException(string errorMessage, Exception innerException)
        => throw new InvalidImageContentException(errorMessage, innerException);

    [DoesNotReturn]
    public static void ThrowInvalidHeader() => throw new InvalidImageContentException("PNG Image must contain a header chunk and it must be located before any other chunks.");

    [DoesNotReturn]
    public static void ThrowNoData() => throw new InvalidImageContentException("PNG Image does not contain a data chunk.");

    [DoesNotReturn]
    public static void ThrowMissingDefaultData() => throw new InvalidImageContentException("APNG Image does not contain a default data chunk.");

    [DoesNotReturn]
    public static void ThrowInvalidAnimationControl() => throw new InvalidImageContentException("APNG Image must contain a acTL chunk and it must be located before any IDAT and fdAT chunks.");

    [DoesNotReturn]
    public static void ThrowMissingFrameControl() => throw new InvalidImageContentException("One of APNG Image's frames do not have a frame control chunk.");

    [DoesNotReturn]
    public static void ThrowMissingPalette() => throw new InvalidImageContentException("PNG Image does not contain a palette chunk.");

    [DoesNotReturn]
    public static void ThrowInvalidChunkType() => throw new InvalidImageContentException("Invalid PNG data.");

    [DoesNotReturn]
    public static void ThrowInvalidChunkType(string message) => throw new InvalidImageContentException(message);

    [DoesNotReturn]
    public static void ThrowInvalidChunkCrc(string chunkTypeName) => throw new InvalidImageContentException($"CRC Error. PNG {chunkTypeName} chunk is corrupt!");

    [DoesNotReturn]
    public static void ThrowNotSupportedColor() => throw new NotSupportedException("Unsupported PNG color type.");

    [DoesNotReturn]
    public static void ThrowUnknownFilter() => throw new InvalidImageContentException("Unknown filter type.");
}

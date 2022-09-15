// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Png;

/// <summary>
/// Cold path optimizations for throwing png format based exceptions.
/// </summary>
internal static class PngThrowHelper
{
    [DoesNotReturn]
    [MethodImpl(InliningOptions.ColdPath)]
    public static void ThrowInvalidImageContentException(string errorMessage, Exception innerException)
        => throw new InvalidImageContentException(errorMessage, innerException);

    [DoesNotReturn]
    [MethodImpl(InliningOptions.ColdPath)]
    public static void ThrowNoHeader() => throw new InvalidImageContentException("PNG Image does not contain a header chunk");

    [DoesNotReturn]
    [MethodImpl(InliningOptions.ColdPath)]
    public static void ThrowNoData() => throw new InvalidImageContentException("PNG Image does not contain a data chunk");

    [DoesNotReturn]
    [MethodImpl(InliningOptions.ColdPath)]
    public static void ThrowMissingPalette() => throw new InvalidImageContentException("PNG Image does not contain a palette chunk");

    [DoesNotReturn]
    [MethodImpl(InliningOptions.ColdPath)]
    public static void ThrowInvalidChunkType() => throw new InvalidImageContentException("Invalid PNG data.");

    [DoesNotReturn]
    [MethodImpl(InliningOptions.ColdPath)]
    public static void ThrowInvalidChunkType(string message) => throw new InvalidImageContentException(message);

    [DoesNotReturn]
    [MethodImpl(InliningOptions.ColdPath)]
    public static void ThrowInvalidChunkCrc(string chunkTypeName) => throw new InvalidImageContentException($"CRC Error. PNG {chunkTypeName} chunk is corrupt!");

    [DoesNotReturn]
    [MethodImpl(InliningOptions.ColdPath)]
    public static void ThrowNotSupportedColor() => throw new NotSupportedException("Unsupported PNG color type");

    [DoesNotReturn]
    [MethodImpl(InliningOptions.ColdPath)]
    public static void ThrowUnknownFilter() => throw new InvalidImageContentException("Unknown filter type.");
}

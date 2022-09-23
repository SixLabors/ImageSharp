// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.OpenExr;

/// <summary>
/// Cold path optimizations for throwing exr format based exceptions.
/// </summary>
internal static class ExrThrowHelper
{
    public static Exception NotSupportedDecompressor(string compressionType) => throw new NotSupportedException($"Not supported decoder compression method: {compressionType}");

    public static void ThrowInvalidImageContentException(string errorMessage) => throw new InvalidImageContentException(errorMessage);

    public static void ThrowNotSupportedVersion() => throw new NotSupportedException("Unsupported EXR version");

    public static void ThrowNotSupported(string msg) => throw new NotSupportedException(msg);

    public static void ThrowInvalidImageHeader() => throw new InvalidImageContentException("Invalid EXR image header");

    public static void ThrowInvalidImageHeader(string msg) => throw new InvalidImageContentException(msg);
}

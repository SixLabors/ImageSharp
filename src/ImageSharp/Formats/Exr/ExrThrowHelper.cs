// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;

namespace SixLabors.ImageSharp.Formats.Exr;

/// <summary>
/// Cold path optimizations for throwing exr format based exceptions.
/// </summary>
internal static class ExrThrowHelper
{
    [DoesNotReturn]
    public static Exception NotSupportedDecompressor(string compressionType) => throw new NotSupportedException($"Not supported decoder compression method: {compressionType}");

    [DoesNotReturn]
    public static void ThrowInvalidImageContentException(string errorMessage) => throw new InvalidImageContentException(errorMessage);

    [DoesNotReturn]
    public static void ThrowNotSupportedVersion() => throw new NotSupportedException("Unsupported EXR version");

    [DoesNotReturn]
    public static void ThrowNotSupported(string msg) => throw new NotSupportedException(msg);

    [DoesNotReturn]
    public static void ThrowInvalidImageHeader() => throw new InvalidImageContentException("Invalid EXR image header");

    [DoesNotReturn]
    public static void ThrowInvalidImageHeader(string msg) => throw new InvalidImageContentException(msg);

    [DoesNotReturn]
    public static Exception NotSupportedCompressor(string compressionType) => throw new NotSupportedException($"Not supported encoder compression method: {compressionType}");
}

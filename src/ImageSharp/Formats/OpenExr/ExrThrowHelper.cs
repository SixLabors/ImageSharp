// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.OpenExr;

/// <summary>
/// Cold path optimizations for throwing exr format based exceptions.
/// </summary>
internal static class ExrThrowHelper
{
    [MethodImpl(InliningOptions.ColdPath)]
    public static Exception NotSupportedDecompressor(string compressionType) => throw new NotSupportedException($"Not supported decoder compression method: {compressionType}");

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowInvalidImageContentException(string errorMessage) => throw new InvalidImageContentException(errorMessage);

    [MethodImpl(InliningOptions.ColdPath)]
    public static void ThrowNotSupportedVersion() => throw new NotSupportedException("Unsupported EXR version");

    [MethodImpl(InliningOptions.ColdPath)]
    public static void ThrowNotSupported(string msg) => throw new NotSupportedException(msg);

    [MethodImpl(InliningOptions.ColdPath)]
    public static void ThrowInvalidImageHeader() => throw new InvalidImageContentException("Invalid EXR image header");

    [MethodImpl(InliningOptions.ColdPath)]
    public static void ThrowInvalidImageHeader(string msg) => throw new InvalidImageContentException(msg);
}

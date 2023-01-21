// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;

namespace SixLabors.ImageSharp.Formats.Tiff;

internal static class TiffThrowHelper
{
    [DoesNotReturn]
    public static Exception ThrowImageFormatException(string errorMessage) => throw new ImageFormatException(errorMessage);

    [DoesNotReturn]
    public static Exception NotSupportedDecompressor(string compressionType) => throw new NotSupportedException($"Not supported decoder compression method: {compressionType}");

    [DoesNotReturn]
    public static Exception NotSupportedCompressor(string compressionType) => throw new NotSupportedException($"Not supported encoder compression method: {compressionType}");

    [DoesNotReturn]
    public static Exception InvalidColorType(string colorType) => throw new NotSupportedException($"Invalid color type: {colorType}");

    [DoesNotReturn]
    public static Exception ThrowInvalidHeader() => throw new ImageFormatException("Invalid TIFF file header.");

    [DoesNotReturn]
    public static void ThrowNotSupported(string message) => throw new NotSupportedException(message);

    [DoesNotReturn]
    public static void ThrowArgumentException(string message) => throw new ArgumentException(message);
}

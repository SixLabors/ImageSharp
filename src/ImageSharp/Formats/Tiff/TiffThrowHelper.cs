// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Tiff;

internal static class TiffThrowHelper
{
    [DoesNotReturn]
    public static Exception ThrowImageFormatException(string errorMessage) => throw new ImageFormatException(errorMessage);

    public static Exception NotSupportedDecompressor(string compressionType) => throw new NotSupportedException($"Not supported decoder compression method: {compressionType}");

    public static Exception NotSupportedCompressor(string compressionType) => throw new NotSupportedException($"Not supported encoder compression method: {compressionType}");

    public static Exception InvalidColorType(string colorType) => throw new NotSupportedException($"Invalid color type: {colorType}");

    public static Exception ThrowInvalidHeader() => throw new ImageFormatException("Invalid TIFF file header.");

    public static void ThrowNotSupported(string message) => throw new NotSupportedException(message);

    public static void ThrowArgumentException(string message) => throw new ArgumentException(message);
}

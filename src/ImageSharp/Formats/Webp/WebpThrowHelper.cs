// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;

namespace SixLabors.ImageSharp.Formats.Webp;

internal static class WebpThrowHelper
{
    [DoesNotReturn]
    public static void ThrowInvalidImageContentException(string errorMessage) => throw new InvalidImageContentException(errorMessage);

    [DoesNotReturn]
    public static void ThrowImageFormatException(string errorMessage) => throw new ImageFormatException(errorMessage);

    [DoesNotReturn]
    public static void ThrowNotSupportedException(string errorMessage) => throw new NotSupportedException(errorMessage);

    [DoesNotReturn]
    public static void ThrowInvalidImageDimensions(string errorMessage) => throw new InvalidImageContentException(errorMessage);

    [DoesNotReturn]
    public static void ThrowDimensionsTooLarge(int width, int height) => throw new ImageFormatException($"Image is too large to encode at {width}x{height} for WEBP format.");
}

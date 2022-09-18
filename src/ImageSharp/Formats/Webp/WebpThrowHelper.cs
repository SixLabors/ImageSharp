// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Webp;

internal static class WebpThrowHelper
{
    public static void ThrowInvalidImageContentException(string errorMessage) => throw new InvalidImageContentException(errorMessage);

    public static void ThrowImageFormatException(string errorMessage) => throw new ImageFormatException(errorMessage);

    public static void ThrowNotSupportedException(string errorMessage) => throw new NotSupportedException(errorMessage);

    public static void ThrowInvalidImageDimensions(string errorMessage) => throw new InvalidImageContentException(errorMessage);
}

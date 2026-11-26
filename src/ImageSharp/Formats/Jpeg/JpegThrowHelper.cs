// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jpeg;

internal static class JpegThrowHelper
{
    public static void ThrowNotSupportedException(string errorMessage) => throw new NotSupportedException(errorMessage);

    public static void ThrowInvalidImageContentException(string errorMessage) => throw new InvalidImageContentException(errorMessage);

    public static void ThrowBadSampling() => throw new InvalidImageContentException("Bad sampling factor.");

    public static void ThrowInvalidImageDimensions(int width, int height)
        => throw new InvalidImageContentException($"Invalid image dimensions: {width}x{height}.");

    public static void ThrowDimensionsTooLarge(int width, int height)
        => throw new ImageFormatException($"Image is too large to encode at {width}x{height} for JPEG format.");

    public static void ThrowNotSupportedComponentCount(int componentCount)
        => throw new NotSupportedException($"Images with {componentCount} components are not supported.");

    public static void ThrowNotSupportedColorSpace() => throw new NotSupportedException("Image color space could not be deduced.");
}

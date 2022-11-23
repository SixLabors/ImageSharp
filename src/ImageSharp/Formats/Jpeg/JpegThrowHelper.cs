// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jpeg;

internal static class JpegThrowHelper
{
    public static void ThrowNotSupportedException(string errorMessage) => throw new NotSupportedException(errorMessage);

    public static void ThrowInvalidImageContentException(string errorMessage) => throw new InvalidImageContentException(errorMessage);

    public static void ThrowBadMarker(string marker, int length) => throw new InvalidImageContentException($"Marker {marker} has bad length {length}.");

    public static void ThrowNotEnoughBytesForMarker(byte marker) => throw new InvalidImageContentException($"Input stream does not have enough bytes to parse declared contents of the {marker:X2} marker.");

    public static void ThrowBadQuantizationTableIndex(int index) => throw new InvalidImageContentException($"Bad Quantization Table index {index}.");

    public static void ThrowBadQuantizationTablePrecision(int precision) => throw new InvalidImageContentException($"Unknown Quantization Table precision {precision}.");

    public static void ThrowBadSampling() => throw new InvalidImageContentException("Bad sampling factor.");

    public static void ThrowBadSampling(int factor) => throw new InvalidImageContentException($"Bad sampling factor: {factor}");

    public static void ThrowBadProgressiveScan(int ss, int se, int ah, int al) => throw new InvalidImageContentException($"Invalid progressive parameters Ss={ss} Se={se} Ah={ah} Al={al}.");

    public static void ThrowInvalidImageDimensions(int width, int height) => throw new InvalidImageContentException($"Invalid image dimensions: {width}x{height}.");

    public static void ThrowDimensionsTooLarge(int width, int height) => throw new ImageFormatException($"Image is too large to encode at {width}x{height} for JPEG format.");

    public static void ThrowNotSupportedComponentCount(int componentCount) => throw new NotSupportedException($"Images with {componentCount} components are not supported.");

    public static void ThrowNotSupportedColorSpace() => throw new NotSupportedException("Image color space could not be deduced.");
}

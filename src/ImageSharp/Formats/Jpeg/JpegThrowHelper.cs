// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Jpeg
{
    internal static class JpegThrowHelper
    {
        /// <summary>
        /// Cold path optimization for throwing <see cref="NotSupportedException"/>'s.
        /// </summary>
        /// <param name="errorMessage">The error message for the exception.</param>
        [MethodImpl(InliningOptions.ColdPath)]
        public static void ThrowNotSupportedException(string errorMessage) => throw new NotSupportedException(errorMessage);

        /// <summary>
        /// Cold path optimization for throwing <see cref="InvalidImageContentException"/>'s.
        /// </summary>
        /// <param name="errorMessage">The error message for the exception.</param>
        [MethodImpl(InliningOptions.ColdPath)]
        public static void ThrowInvalidImageContentException(string errorMessage) => throw new InvalidImageContentException(errorMessage);

        [MethodImpl(InliningOptions.ColdPath)]
        public static void ThrowBadMarker(string marker, int length) => throw new InvalidImageContentException($"Marker {marker} has bad length {length}.");

        [MethodImpl(InliningOptions.ColdPath)]
        public static void ThrowNotEnoughBytesForMarker(byte marker) => throw new InvalidImageContentException($"Input stream does not have enough bytes to parse declared contents of the {marker:X2} marker.");

        [MethodImpl(InliningOptions.ColdPath)]
        public static void ThrowBadQuantizationTableIndex(int index) => throw new InvalidImageContentException($"Bad Quantization Table index {index}.");

        [MethodImpl(InliningOptions.ColdPath)]
        public static void ThrowBadQuantizationTablePrecision(int precision) => throw new InvalidImageContentException($"Unknown Quantization Table precision {precision}.");

        [MethodImpl(InliningOptions.ColdPath)]
        public static void ThrowBadSampling() => throw new InvalidImageContentException("Bad sampling factor.");

        [MethodImpl(InliningOptions.ColdPath)]
        public static void ThrowBadSampling(int factor) => throw new InvalidImageContentException($"Bad sampling factor: {factor}");

        [MethodImpl(InliningOptions.ColdPath)]
        public static void ThrowBadProgressiveScan(int ss, int se, int ah, int al) => throw new InvalidImageContentException($"Invalid progressive parameters Ss={ss} Se={se} Ah={ah} Al={al}.");

        [MethodImpl(InliningOptions.ColdPath)]
        public static void ThrowInvalidImageDimensions(int width, int height) => throw new InvalidImageContentException($"Invalid image dimensions: {width}x{height}.");

        [MethodImpl(InliningOptions.ColdPath)]
        public static void ThrowDimensionsTooLarge(int width, int height) => throw new ImageFormatException($"Image is too large to encode at {width}x{height} for JPEG format.");

        [MethodImpl(InliningOptions.ColdPath)]
        public static void ThrowNotSupportedComponentCount(int componentCount) => throw new NotSupportedException($"Images with {componentCount} components are not supported.");

        [MethodImpl(InliningOptions.ColdPath)]
        public static void ThrowNotSupportedColorSpace() => throw new NotSupportedException("Image color space could not be deduced.");
    }
}

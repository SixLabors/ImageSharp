// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

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

        /// <summary>
        /// Cold path optimization for throwing <see cref="InvalidImageContentException"/>'s.
        /// </summary>
        /// <param name="errorMessage">The error message for the exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception, or a null reference
        /// if no inner exception is specified.</param>
        [MethodImpl(InliningOptions.ColdPath)]
        public static void ThrowInvalidImageContentException(string errorMessage, Exception innerException) => throw new InvalidImageContentException(errorMessage, innerException);

        /// <summary>
        /// Cold path optimization for throwing <see cref="NotImplementedException"/>'s
        /// </summary>
        /// <param name="errorMessage">The error message for the exception.</param>
        [MethodImpl(InliningOptions.ColdPath)]
        public static void ThrowNotImplementedException(string errorMessage)
            => throw new NotImplementedException(errorMessage);

        [MethodImpl(InliningOptions.ColdPath)]
        public static void ThrowBadMarker(string marker, int length) => throw new InvalidImageContentException($"Marker {marker} has bad length {length}.");

        [MethodImpl(InliningOptions.ColdPath)]
        public static void ThrowBadQuantizationTableIndex(int index) => throw new InvalidImageContentException($"Bad Quantization Table index {index}.");

        [MethodImpl(InliningOptions.ColdPath)]
        public static void ThrowBadQuantizationTablePrecision(int precision) => throw new InvalidImageContentException($"Unknown Quantization Table precision {precision}.");

        [MethodImpl(InliningOptions.ColdPath)]
        public static void ThrowBadSampling() => throw new InvalidImageContentException("Bad sampling factor.");

        [MethodImpl(InliningOptions.ColdPath)]
        public static void ThrowBadProgressiveScan(int ss, int se, int ah, int al) => throw new InvalidImageContentException($"Invalid progressive parameters Ss={ss} Se={se} Ah={ah} Al={al}.");

        [MethodImpl(InliningOptions.ColdPath)]
        public static void ThrowInvalidImageDimensions(int width, int height) => throw new InvalidImageContentException($"Invalid image dimensions: {width}x{height}.");

        [MethodImpl(InliningOptions.ColdPath)]
        public static void ThrowDimensionsTooLarge(int width, int height) => throw new ImageFormatException($"Image is too large to encode at {width}x{height} for JPEG format.");
    }
}

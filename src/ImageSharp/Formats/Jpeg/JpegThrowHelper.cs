// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Jpeg
{
    internal static class JpegThrowHelper
    {
        /// <summary>
        /// Cold path optimization for throwing <see cref="ImageFormatException"/>'s.
        /// </summary>
        /// <param name="errorMessage">The error message for the exception.</param>
        [MethodImpl(InliningOptions.ColdPath)]
        public static void ThrowImageFormatException(string errorMessage) => throw new ImageFormatException(errorMessage);

        [MethodImpl(InliningOptions.ColdPath)]
        public static void ThrowBadMarker(string marker, int length) => throw new ImageFormatException($"Marker {marker} has bad length {length}.");

        [MethodImpl(InliningOptions.ColdPath)]
        public static void ThrowBadQuantizationTable() => throw new ImageFormatException("Bad Quantization Table index.");

        [MethodImpl(InliningOptions.ColdPath)]
        public static void ThrowBadSampling() => throw new ImageFormatException("Bad sampling factor.");

        [MethodImpl(InliningOptions.ColdPath)]
        public static void ThrowBadProgressiveScan(int ss, int se, int ah, int al) => throw new ImageFormatException($"Invalid progressive parameters Ss={ss} Se={se} Ah={ah} Al={al}.");

        [MethodImpl(InliningOptions.ColdPath)]
        public static void ThrowInvalidImageDimensions(int width, int height) => throw new ImageFormatException($"Invalid image dimensions: {width}x{height}.");
    }
}
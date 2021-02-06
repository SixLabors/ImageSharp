// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Experimental.Tiff
{
    /// <summary>
    /// Cold path optimizations for throwing tiff format based exceptions.
    /// </summary>
    internal static class TiffThrowHelper
    {
        /// <summary>
        /// Cold path optimization for throwing <see cref="ImageFormatException"/>-s
        /// </summary>
        /// <param name="errorMessage">The error message for the exception.</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowImageFormatException(string errorMessage) => throw new ImageFormatException(errorMessage);

        [MethodImpl(InliningOptions.ColdPath)]
        public static Exception NotSupportedDecompressor(string compressionType) => throw new NotSupportedException($"Not supported decoder compression method: {compressionType}");

        [MethodImpl(InliningOptions.ColdPath)]
        public static Exception NotSupportedCompressor(string compressionType) => throw new NotSupportedException($"Not supported encoder compression method: {compressionType}");

        [MethodImpl(InliningOptions.ColdPath)]
        public static Exception InvalidColorType(string colorType) => throw new NotSupportedException($"Invalid color type: {colorType}");

        [MethodImpl(InliningOptions.ColdPath)]
        public static Exception ThrowInvalidHeader() => throw new ImageFormatException("Invalid TIFF file header.");

        [MethodImpl(InliningOptions.ColdPath)]
        public static void ThrowNotSupported(string message) => throw new NotSupportedException(message);
    }
}

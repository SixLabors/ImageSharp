// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Tiff
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
        public static void ThrowImageFormatException(string errorMessage)
        {
            throw new ImageFormatException(errorMessage);
        }

        [MethodImpl(InliningOptions.ColdPath)]
        public static Exception TagNotFound(string tagName)
            => new ArgumentException("Required tag is not found.", tagName);

        [MethodImpl(InliningOptions.ColdPath)]
        public static void ThrowTagNotFound(string tagName)
            => throw TagNotFound(tagName);

        [MethodImpl(InliningOptions.ColdPath)]
        public static void ThrowBadZlibHeader(int cmf) => throw new ImageFormatException($"Bad compression method for ZLIB header: cmf={cmf}");

        [MethodImpl(InliningOptions.ColdPath)]
        public static Exception NotSupportedCompression(string compressionType) => new NotSupportedException("Not supported compression: " + compressionType);

        [MethodImpl(InliningOptions.ColdPath)]
        public static void ThrowNotSupportedCompression(string compressionType) => throw NotSupportedCompression(compressionType);

        [MethodImpl(InliningOptions.ColdPath)]
        public static Exception InvalidColorType(string colorType) => new NotSupportedException("Invalid color type: " + colorType);

        [MethodImpl(InliningOptions.ColdPath)]
        public static Exception InvalidHeader() => new ImageFormatException("Invalid TIFF file header.");

        [MethodImpl(InliningOptions.ColdPath)]
        public static void ThrowInvalidHeader() => throw InvalidHeader();

        [MethodImpl(InliningOptions.ColdPath)]
        public static void ThrowOutOfRange(string structure) => throw new InvalidDataException($"Out of range of {structure} structure.");

        [MethodImpl(InliningOptions.ColdPath)]
        public static void ThrowBadStringEntry() => throw new ImageFormatException("The retrieved string is not null terminated.");

        [MethodImpl(InliningOptions.ColdPath)]
        public static void ThrowNotSupported(string message) => throw new NotSupportedException(message);
    }
}

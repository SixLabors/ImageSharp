// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Gif
{
    internal static class GifThrowHelper
    {
        /// <summary>
        /// Cold path optimization for throwing <see cref="InvalidImageContentException"/>'s
        /// </summary>
        /// <param name="errorMessage">The error message for the exception.</param>
        [MethodImpl(InliningOptions.ColdPath)]
        public static void ThrowInvalidImageContentException(string errorMessage)
            => throw new InvalidImageContentException(errorMessage);

        /// <summary>
        /// Cold path optimization for throwing <see cref="InvalidImageContentException"/>'s.
        /// </summary>
        /// <param name="errorMessage">The error message for the exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception, or a null reference
        /// if no inner exception is specified.</param>
        [MethodImpl(InliningOptions.ColdPath)]
        public static void ThrowInvalidImageContentException(string errorMessage, Exception innerException) => throw new InvalidImageContentException(errorMessage, innerException);

        [MethodImpl(InliningOptions.ColdPath)]
        public static void ThrowNoHeader() => throw new InvalidImageContentException("Gif image does not contain a Logical Screen Descriptor.");

        [MethodImpl(InliningOptions.ColdPath)]
        public static void ThrowNoData() => throw new InvalidImageContentException("Unable to read Gif image data");
    }
}

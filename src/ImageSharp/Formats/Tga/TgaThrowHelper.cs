// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Tga
{
    internal static class TgaThrowHelper
    {
        /// <summary>
        /// Cold path optimization for throwing <see cref="ImageFormatException"/>'s
        /// </summary>
        /// <param name="errorMessage">The error message for the exception.</param>
        [MethodImpl(InliningOptions.ColdPath)]
        public static void ThrowInvalidImageContentException(string errorMessage)
            => throw new InvalidImageContentException(errorMessage);

        /// <summary>
        /// Cold path optimization for throwing <see cref="ImageFormatException"/>'s
        /// </summary>
        /// <param name="errorMessage">The error message for the exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception, or a null reference
        /// if no inner exception is specified.</param>
        [MethodImpl(InliningOptions.ColdPath)]
        public static void ThrowInvalidImageContentException(string errorMessage, Exception innerException)
            => throw new InvalidImageContentException(errorMessage, innerException);

        /// <summary>
        /// Cold path optimization for throwing <see cref="NotSupportedException"/>'s
        /// </summary>
        /// <param name="errorMessage">The error message for the exception.</param>
        [MethodImpl(InliningOptions.ColdPath)]
        public static void ThrowNotSupportedException(string errorMessage)
            => throw new NotSupportedException(errorMessage);
    }
}

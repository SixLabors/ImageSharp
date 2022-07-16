// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Webp
{
    internal static class WebpThrowHelper
    {
        /// <summary>
        /// Cold path optimization for throwing <see cref="InvalidImageContentException"/>'s.
        /// </summary>
        /// <param name="errorMessage">The error message for the exception.</param>
        [MethodImpl(InliningOptions.ColdPath)]
        public static void ThrowInvalidImageContentException(string errorMessage) => throw new InvalidImageContentException(errorMessage);

        /// <summary>
        /// Cold path optimization for throwing <see cref="ImageFormatException"/>-s
        /// </summary>
        /// <param name="errorMessage">The error message for the exception.</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowImageFormatException(string errorMessage) => throw new ImageFormatException(errorMessage);

        /// <summary>
        /// Cold path optimization for throwing <see cref="NotSupportedException"/>-s
        /// </summary>
        /// <param name="errorMessage">The error message for the exception.</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowNotSupportedException(string errorMessage) => throw new NotSupportedException(errorMessage);

        /// <summary>
        /// Cold path optimization for throwing <see cref="InvalidImageContentException"/>-s
        /// </summary>
        /// <param name="errorMessage">The error message for the exception.</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowInvalidImageDimensions(string errorMessage) => throw new InvalidImageContentException(errorMessage);
    }
}

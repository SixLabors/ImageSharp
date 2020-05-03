// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

using System;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.WebP
{
    internal static class WebPThrowHelper
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

        /// <summary>
        /// Cold path optimization for throwing <see cref="NotSupportedException"/>-s
        /// </summary>
        /// <param name="errorMessage">The error message for the exception.</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowNotSupportedException(string errorMessage)
        {
            throw new NotSupportedException(errorMessage);
        }
    }
}

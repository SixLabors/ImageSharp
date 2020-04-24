// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Gif
{
    internal static class GifThrowHelper
    {
        /// <summary>
        /// Cold path optimization for throwing <see cref="InvalidImageContentException"/>'s
        /// </summary>
        /// <param name="errorMessage">The error message for the exception.</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowInvalidImageContentException(string errorMessage)
            => throw new InvalidImageContentException(errorMessage);
    }
}

// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Jpeg
{
    internal static class JpegThrowHelper
    {
        /// <summary>
        /// Cold path optimization for throwing <see cref="ImageFormatException"/>-s
        /// </summary>
        /// <param name="errorMessage">The error message for the exception</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowImageFormatException(string errorMessage)
        {
            throw new ImageFormatException(errorMessage);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowBadHuffmanCode()
        {
            throw new ImageFormatException("Bad Huffman code.");
        }
    }
}
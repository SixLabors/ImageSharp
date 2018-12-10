// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Tiff
{
    /// <summary>
    /// Class to handle cases where TIFF image data is not compressed.
    /// </summary>
    internal static class NoneTiffCompression
    {
        /// <summary>
        /// Decompresses image data into the supplied buffer.
        /// </summary>
        /// <param name="stream">The <see cref="Stream"/> to read image data from.</param>
        /// <param name="byteCount">The number of bytes to read from the input stream.</param>
        /// <param name="buffer">The output buffer for uncompressed data.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Decompress(Stream stream, int byteCount, byte[] buffer)
        {
            stream.ReadFull(buffer, byteCount);
        }
    }
}

// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.IO;

namespace SixLabors.ImageSharp.Formats.Tiff
{
    /// <summary>
    /// TIFF specific utilities and extension methods.
    /// </summary>
    internal static class TiffUtils
    {
        /// <summary>
        /// Reads a sequence of bytes from the input stream into a buffer.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="buffer">A buffer to store the retrieved data.</param>
        /// <param name="count">The number of bytes to read.</param>
        public static void ReadFull(this Stream stream, byte[] buffer, int count)
        {
            int offset = 0;

            while (count > 0)
            {
                int bytesRead = stream.Read(buffer, offset, count);

                if (bytesRead == 0)
                {
                    break;
                }

                offset += bytesRead;
                count -= bytesRead;
            }
        }

        /// <summary>
        /// Reads all bytes from the input stream into a buffer until the end of stream or the buffer is full.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="buffer">A buffer to store the retrieved data.</param>
        public static void ReadFull(this Stream stream, byte[] buffer)
        {
            ReadFull(stream, buffer, buffer.Length);
        }
    }
}
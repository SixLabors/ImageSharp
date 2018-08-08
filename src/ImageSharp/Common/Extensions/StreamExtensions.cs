// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;

using SixLabors.ImageSharp.Memory;
using SixLabors.Memory;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Extension methods for the <see cref="Stream"/> type.
    /// </summary>
    internal static class StreamExtensions
    {
#if NETCOREAPP2_1
        /// <summary>
        /// Writes data from a stream into the provided buffer.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset within the buffer to begin writing.</param>
        /// <param name="count">The number of bytes to write to the stream.</param>
        public static void Write(this Stream stream, Span<byte> buffer, int offset, int count)
        {
            stream.Write(buffer.Slice(offset, count));
        }

        /// <summary>
        /// Reads data from a stream into the provided buffer.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <param name="buffer">The buffer..</param>
        /// <param name="offset">The offset within the buffer where the bytes are read into.</param>
        /// <param name="count">The number of bytes, if available, to read.</param>
        /// <returns>The actual number of bytes read.</returns>
        public static int Read(this Stream stream, Span<byte> buffer, int offset, int count)
        {
            return stream.Read(buffer.Slice(offset, count));
        }
#endif

        /// <summary>
        /// Skips the number of bytes in the given stream.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <param name="count">The count.</param>
        public static void Skip(this Stream stream, int count)
        {
            if (count < 1)
            {
                return;
            }

            if (stream.CanSeek)
            {
                stream.Seek(count, SeekOrigin.Current); // Position += count;
            }
            else
            {
                byte[] foo = new byte[count];
                while (count > 0)
                {
                    int bytesRead = stream.Read(foo, 0, count);
                    if (bytesRead == 0)
                    {
                        break;
                    }

                    count -= bytesRead;
                }
            }
        }

        public static void Read(this Stream stream, IManagedByteBuffer buffer)
        {
            stream.Read(buffer.Array, 0, buffer.Length());
        }

        public static void Write(this Stream stream, IManagedByteBuffer buffer)
        {
            stream.Write(buffer.Array, 0, buffer.Length());
        }
    }
}

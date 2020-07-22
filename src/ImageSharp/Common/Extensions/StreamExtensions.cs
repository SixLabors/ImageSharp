// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.IO;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Extension methods for the <see cref="Stream"/> type.
    /// </summary>
    internal static class StreamExtensions
    {
        /// <summary>
        /// Writes data from a stream into the provided buffer.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset within the buffer to begin writing.</param>
        /// <param name="count">The number of bytes to write to the stream.</param>
        public static void Write(this Stream stream, Span<byte> buffer, int offset, int count)
            => stream.Write(buffer.Slice(offset, count));

        /// <summary>
        /// Reads data from a stream into the provided buffer.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset within the buffer where the bytes are read into.</param>
        /// <param name="count">The number of bytes, if available, to read.</param>
        /// <returns>The actual number of bytes read.</returns>
        public static int Read(this Stream stream, Span<byte> buffer, int offset, int count)
            => stream.Read(buffer.Slice(offset, count));

        /// <summary>
        /// Skips the number of bytes in the given stream.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <param name="count">A byte offset relative to the origin parameter.</param>
        public static void Skip(this Stream stream, int count)
        {
            if (count < 1)
            {
                return;
            }

            if (stream.CanSeek)
            {
                stream.Seek(count, SeekOrigin.Current);
                return;
            }

            byte[] buffer = ArrayPool<byte>.Shared.Rent(count);
            try
            {
                while (count > 0)
                {
                    int bytesRead = stream.Read(buffer, 0, count);
                    if (bytesRead == 0)
                    {
                        break;
                    }

                    count -= bytesRead;
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        public static void Read(this Stream stream, IManagedByteBuffer buffer)
            => stream.Read(buffer.Array, 0, buffer.Length());

        public static void Write(this Stream stream, IManagedByteBuffer buffer)
            => stream.Write(buffer.Array, 0, buffer.Length());

#if !SUPPORTS_SPAN_STREAM
        // This is a port of the CoreFX implementation and is MIT Licensed:
        // https://github.com/dotnet/corefx/blob/17300169760c61a90cab8d913636c1058a30a8c1/src/Common/src/CoreLib/System/IO/Stream.cs#L742
        public static int Read(this Stream stream, Span<byte> buffer)
        {
            // This uses ArrayPool<byte>.Shared, rather than taking a MemoryAllocator,
            // in order to match the signature of the framework method that exists in
            // .NET Core.
            byte[] sharedBuffer = ArrayPool<byte>.Shared.Rent(buffer.Length);
            try
            {
                int numRead = stream.Read(sharedBuffer, 0, buffer.Length);
                if ((uint)numRead > (uint)buffer.Length)
                {
                    throw new IOException("Stream was too long.");
                }

                new Span<byte>(sharedBuffer, 0, numRead).CopyTo(buffer);
                return numRead;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(sharedBuffer);
            }
        }

        // This is a port of the CoreFX implementation and is MIT Licensed:
        // https://github.com/dotnet/corefx/blob/17300169760c61a90cab8d913636c1058a30a8c1/src/Common/src/CoreLib/System/IO/Stream.cs#L775
        public static void Write(this Stream stream, ReadOnlySpan<byte> buffer)
        {
            // This uses ArrayPool<byte>.Shared, rather than taking a MemoryAllocator,
            // in order to match the signature of the framework method that exists in
            // .NET Core.
            byte[] sharedBuffer = ArrayPool<byte>.Shared.Rent(buffer.Length);
            try
            {
                buffer.CopyTo(sharedBuffer);
                stream.Write(sharedBuffer, 0, buffer.Length);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(sharedBuffer);
            }
        }
#endif
    }
}

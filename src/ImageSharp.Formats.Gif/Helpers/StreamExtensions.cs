// <copyright file="StreamExtensions.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System.Buffers;
    using System.IO;

    /// <summary>
    /// Extension methods for the <see cref="Stream"/> type.
    /// </summary>
    internal static class StreamExtensions
    {
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
                stream.Position += count;
            }
            else
            {
                byte[] foo = ArrayPool<byte>.Shared.Rent(count);
                try
                {
                    stream.Read(foo, 0, count);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(foo);
                }
            }
        }
    }
}

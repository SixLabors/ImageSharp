// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Buffers;
using System.IO;

namespace SixLabors.ImageSharp
{
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
                stream.Seek(count, SeekOrigin.Current); // Position += count;
            }
            else
            {
                byte[] foo = ArrayPool<byte>.Shared.Rent(count);
                try
                {
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
                finally
                {
                    ArrayPool<byte>.Shared.Return(foo);
                }
            }
        }
    }
}

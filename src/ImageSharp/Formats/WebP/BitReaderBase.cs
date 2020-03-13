// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;

using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.WebP
{
    /// <summary>
    /// Base class for VP8 and VP8L bitreader.
    /// </summary>
    internal abstract class BitReaderBase
    {
        /// <summary>
        /// Gets or sets the raw encoded image data.
        /// </summary>
        public byte[] Data { get; set; }

        /// <summary>
        /// Copies the raw encoded image data from the stream into a byte array.
        /// </summary>
        /// <param name="input">The input stream.</param>
        /// <param name="bytesToRead">Number of bytes to read as indicated from the chunk size.</param>
        /// <param name="memoryAllocator">Used for allocating memory during reading data from the stream.</param>
        protected void ReadImageDataFromStream(Stream input, int bytesToRead, MemoryAllocator memoryAllocator)
        {
            using (var ms = new MemoryStream())
            using (IManagedByteBuffer buffer = memoryAllocator.AllocateManagedByteBuffer(4096))
            {
                Span<byte> bufferSpan = buffer.GetSpan();
                int read;
                while (bytesToRead > 0 && (read = input.Read(buffer.Array, 0, Math.Min(bufferSpan.Length, bytesToRead))) > 0)
                {
                    ms.Write(buffer.Array, 0, read);
                    bytesToRead -= read;
                }

                if (bytesToRead > 0)
                {
                    WebPThrowHelper.ThrowImageFormatException("image file has insufficient data");
                }

                this.Data = ms.ToArray();
            }
        }
    }
}

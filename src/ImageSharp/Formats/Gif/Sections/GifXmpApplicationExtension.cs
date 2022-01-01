// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;

namespace SixLabors.ImageSharp.Formats.Gif
{
    internal readonly struct GifXmpApplicationExtension : IGifExtension
    {
        public GifXmpApplicationExtension(byte[] data) => this.Data = data;

        public byte Label => GifConstants.ApplicationExtensionLabel;

        public int ContentLength => this.Data.Length + 269; // 12 + Data Length + 1 + 256

        /// <summary>
        /// Gets the raw Data.
        /// </summary>
        public byte[] Data { get; }

        /// <summary>
        /// Reads the XMP metadata from the specified stream.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <returns>The XMP metadata</returns>
        /// <exception cref="ImageFormatException">Thrown if the XMP block is not properly terminated.</exception>
        public static GifXmpApplicationExtension Read(Stream stream)
        {
            // Read data in blocks, until an \0 character is encountered.
            // We overshoot, indicated by the terminatorIndex variable.
            const int bufferSize = 256;
            var list = new List<byte[]>();
            int terminationIndex = -1;
            while (terminationIndex < 0)
            {
                byte[] temp = new byte[bufferSize];
                int bytesRead = stream.Read(temp);
                list.Add(temp);
                terminationIndex = Array.IndexOf(temp, (byte)1);
            }

            // Pack all the blocks (except magic trailer) into one single array again.
            int dataSize = ((list.Count - 1) * bufferSize) + terminationIndex;
            byte[] buffer = new byte[dataSize];
            Span<byte> bufferSpan = buffer;
            int pos = 0;
            for (int j = 0; j < list.Count - 1; j++)
            {
                list[j].CopyTo(bufferSpan.Slice(pos));
                pos += bufferSize;
            }

            // Last one only needs the portion until terminationIndex copied over.
            Span<byte> lastBytes = list[list.Count - 1];
            lastBytes.Slice(0, terminationIndex).CopyTo(bufferSpan.Slice(pos));

            // Skip the remainder of the magic trailer.
            stream.Skip(258 - (bufferSize - terminationIndex));
            return new GifXmpApplicationExtension(buffer);
        }

        public int WriteTo(Span<byte> buffer)
        {
            int totalSize = this.ContentLength;
            if (buffer.Length < totalSize)
            {
                throw new InsufficientMemoryException("Unable to write XMP metadata to GIF image");
            }

            int bytesWritten = 0;
            buffer[bytesWritten++] = GifConstants.ApplicationBlockSize;

            // Write "XMP DataXMP"
            ReadOnlySpan<byte> idBytes = GifConstants.XmpApplicationIdentificationBytes;
            idBytes.CopyTo(buffer.Slice(bytesWritten));
            bytesWritten += idBytes.Length;

            // XMP Data itself
            this.Data.CopyTo(buffer.Slice(bytesWritten));
            bytesWritten += this.Data.Length;

            // Write the Magic Trailer
            buffer[bytesWritten++] = 0x01;
            for (byte i = 255; i > 0; i--)
            {
                buffer[bytesWritten++] = i;
            }

            buffer[bytesWritten++] = 0x00;

            return totalSize;
        }
    }
}

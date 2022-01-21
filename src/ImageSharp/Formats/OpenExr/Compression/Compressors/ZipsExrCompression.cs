// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.IO.Compression;
using SixLabors.ImageSharp.Compression.Zlib;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.OpenExr.Compression.Compressors
{
    internal class ZipsExrCompression : ExrBaseDecompressor
    {
        private readonly IMemoryOwner<byte> tmpBuffer;

        public ZipsExrCompression(MemoryAllocator allocator, uint uncompressedBytes)
            : base(allocator, uncompressedBytes) => this.tmpBuffer = allocator.Allocate<byte>((int)uncompressedBytes);

        public override void Decompress(BufferedReadStream stream, uint compressedBytes, Span<byte> buffer)
        {
            long pos = stream.Position;
            using var deframeStream = new ZlibInflateStream(
                       stream,
                       () =>
                       {
                           int left = (int)(compressedBytes - (stream.Position - pos));
                           return left > 0 ? left : 0;
                       });
            deframeStream.AllocateNewBytes((int)this.UncompressedBytes, true);
            DeflateStream dataStream = deframeStream.CompressedStream;

            Span<byte> tmp = this.tmpBuffer.GetSpan();
            int totalRead = 0;
            while (totalRead < buffer.Length)
            {
                int bytesRead = dataStream.Read(tmp, totalRead, buffer.Length - totalRead);
                if (bytesRead <= 0)
                {
                    break;
                }

                totalRead += bytesRead;
            }

            Reconstruct(tmp, this.UncompressedBytes);
            Interleave(tmp, this.UncompressedBytes, buffer);
        }

        private static void Reconstruct(Span<byte> buffer, uint unCompressedBytes)
        {
            int offset = 0;
            for (int i = 0; i < unCompressedBytes - 1; i++)
            {
                byte d = (byte)(buffer[offset] + (buffer[offset + 1] - 128));
                buffer[offset + 1] = d;
                offset++;
            }
        }

        private static void Interleave(Span<byte> source, uint unCompressedBytes, Span<byte> output)
        {
            int sourceOffset = 0;
            int offset0 = 0;
            int offset1 = (int)((unCompressedBytes + 1) / 2);
            while (sourceOffset < unCompressedBytes)
            {
                output[sourceOffset++] = source[offset0++];
                output[sourceOffset++] = source[offset1++];
            }
        }

        protected override void Dispose(bool disposing) => this.tmpBuffer.Dispose();
    }
}

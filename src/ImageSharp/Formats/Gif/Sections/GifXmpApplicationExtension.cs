// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Gif;

internal readonly struct GifXmpApplicationExtension : IGifExtension
{
    public GifXmpApplicationExtension(byte[] data) => this.Data = data;

    public byte Label => GifConstants.ApplicationExtensionLabel;

    // size          : 1
    // identifier    : 11
    // magic trailer : 257
    public int ContentLength => (this.Data.Length > 0) ? this.Data.Length + 269 : 0;

    /// <summary>
    /// Gets the raw Data.
    /// </summary>
    public byte[] Data { get; }

    /// <summary>
    /// Reads the XMP metadata from the specified stream.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="allocator">The memory allocator.</param>
    /// <returns>The XMP metadata</returns>
    public static GifXmpApplicationExtension Read(Stream stream, MemoryAllocator allocator)
    {
        byte[] xmpBytes = ReadXmpData(stream, allocator);

        // Exclude the "magic trailer", see XMP Specification Part 3, 1.1.2 GIF
        int xmpLength = xmpBytes.Length - 256; // 257 - unread 0x0
        byte[] buffer = Array.Empty<byte>();
        if (xmpLength > 0)
        {
            buffer = new byte[xmpLength];
            xmpBytes.AsSpan(0, xmpLength).CopyTo(buffer);
            stream.Skip(1); // Skip the terminator.
        }

        return new GifXmpApplicationExtension(buffer);
    }

    public int WriteTo(Span<byte> buffer)
    {
        int bytesWritten = 0;
        buffer[bytesWritten++] = GifConstants.ApplicationBlockSize;

        // Write "XMP DataXMP"
        ReadOnlySpan<byte> idBytes = GifConstants.XmpApplicationIdentificationBytes;
        idBytes.CopyTo(buffer[bytesWritten..]);
        bytesWritten += idBytes.Length;

        // XMP Data itself
        this.Data.CopyTo(buffer[bytesWritten..]);
        bytesWritten += this.Data.Length;

        // Write the Magic Trailer
        buffer[bytesWritten++] = 0x01;
        for (byte i = 255; i > 0; i--)
        {
            buffer[bytesWritten++] = i;
        }

        buffer[bytesWritten++] = 0x00;

        return this.ContentLength;
    }

    private static byte[] ReadXmpData(Stream stream, MemoryAllocator allocator)
    {
        using ChunkedMemoryStream bytes = new(allocator);

        // XMP data doesn't have a fixed length nor is there an indicator of the length.
        // So we simply read one byte at a time until we hit the 0x0 value at the end
        // of the magic trailer or the end of the stream.
        // Using ChunkedMemoryStream reduces the array resize allocation normally associated
        // with writing from a non fixed-size buffer.
        while (true)
        {
            int b = stream.ReadByte();
            if (b <= 0)
            {
                return bytes.ToArray();
            }

            bytes.WriteByte((byte)b);
        }
    }
}

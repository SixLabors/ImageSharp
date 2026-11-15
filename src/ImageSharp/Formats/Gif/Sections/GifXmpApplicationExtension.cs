// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata.Profiles.Xmp;

namespace SixLabors.ImageSharp.Formats.Gif;

internal readonly struct GifXmpApplicationExtension : IGifExtension
{
    private const int TrailerLength = 257;
    private const int ContentOverhead = 1 + GifConstants.ApplicationBlockSize + TrailerLength;

    /// <summary>
    /// Initializes a new instance of the <see cref="GifXmpApplicationExtension"/> struct.
    /// </summary>
    /// <param name="data">The XMP packet, or null for an empty extension.</param>
    public GifXmpApplicationExtension(byte[]? data)
    {
        // The GIF writer adds three bytes for the introducer, label, and terminator.
        if (data?.Length > int.MaxValue - ContentOverhead - 3)
        {
            throw new ImageFormatException("The GIF XMP extension exceeds the supported size.");
        }

        this.Data = data ?? Array.Empty<byte>();
    }

    public byte Label => GifConstants.ApplicationExtensionLabel;

    public int ContentLength => this.Data.Length > 0
        ? this.Data.Length + ContentOverhead
        : 0;

    /// <summary>
    /// Gets the raw Data.
    /// </summary>
    public byte[] Data { get; }

    /// <summary>
    /// Reads the XMP metadata from the specified stream.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="allocator">The memory allocator.</param>
    /// <param name="profile">The profile, or null when the packet is empty.</param>
    /// <returns>Whether the application extension was read in full.</returns>
    public static bool TryReadProfile(BufferedReadStream stream, MemoryAllocator allocator, out XmpProfile? profile)
    {
        profile = null;
        using ChunkedMemoryStream bytes = new(allocator);

        // A valid XMP packet has no zero bytes. The first zero ends the GIF trailer.
        int value;
        while ((value = stream.ReadByte()) > 0)
        {
            bytes.WriteByte((byte)value);
        }

        if (value < 0)
        {
            throw new InvalidImageContentException("Unexpected end of stream while reading gif XMP data");
        }

        long packetLength = bytes.Length - (TrailerLength - 1);
        if (packetLength <= 0 || value != 0)
        {
            return false;
        }

        bytes.Position = packetLength;
        for (int i = 0; i < TrailerLength - 1; i++)
        {
            if (bytes.ReadByte() != GetTrailerByte(i))
            {
                return false;
            }
        }

        // The next zero terminates the application extension. The generic GIF writer emits it.
        if (stream.ReadByte() != 0)
        {
            return false;
        }

        bytes.Position = 0;
        return XmpReader.TryReadProfile(bytes, packetLength, out profile);
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

        for (int i = 0; i < TrailerLength; i++)
        {
            buffer[bytesWritten++] = GetTrailerByte(i);
        }

        return this.ContentLength;
    }

    /// <summary>
    /// Gets a byte from the XMP trailer defined for GIF application extensions.
    /// </summary>
    /// <param name="index">The position within the trailer.</param>
    /// <returns>The trailer byte.</returns>
    private static byte GetTrailerByte(int index) => index == 0 ? (byte)1 : (byte)(256 - index);
}

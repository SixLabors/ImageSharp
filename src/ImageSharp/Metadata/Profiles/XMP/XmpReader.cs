// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Xml;
using System.Xml.Linq;

namespace SixLabors.ImageSharp.Metadata.Profiles.Xmp;

/// <summary>
/// Reads XMP packets and their XML documents.
/// </summary>
internal sealed class XmpReader
{
    private byte[]? packet;
    private int written;

    /// <summary>
    /// Appends one complete packet segment. A short segment does not become part of the packet.
    /// </summary>
    /// <param name="stream">The input positioned at the segment payload.</param>
    /// <param name="length">The segment payload length.</param>
    /// <returns>Whether the complete segment was read.</returns>
    public bool TryReadSegment(Stream stream, int length)
        => length == 0 || stream.TryAppendExactly(ref this.packet, ref this.written, length, Array.MaxLength);

    /// <summary>
    /// Returns the packet assembled from the segments read so far.
    /// </summary>
    /// <returns>The assembled profile, or null when the packet is empty.</returns>
    public XmpProfile? ToProfile()
        => this.packet is null || this.written == this.packet.Length
            ? ReadProfile(this.packet)
            : ReadProfile(this.packet.AsSpan(0, this.written));

    /// <summary>
    /// Creates a profile from a complete packet already owned by the decoder.
    /// </summary>
    /// <param name="data">The packet bytes.</param>
    /// <returns>The profile, or null when the packet is empty.</returns>
    public static XmpProfile? ReadProfile(byte[]? data)
    {
        if (data is not { Length: > 0 })
        {
            return null;
        }

        XmpProfile profile = new(data);
        return profile.Data is { Length: > 0 } ? profile : null;
    }

    /// <summary>
    /// Copies the packet so the caller can reuse the input buffer.
    /// </summary>
    /// <param name="data">The bounded packet bytes.</param>
    /// <returns>The profile, or null when the packet is empty.</returns>
    public static XmpProfile? ReadProfile(ReadOnlySpan<byte> data)
        => IsPacketLengthValid(data.Length, data.Length) ? ReadProfile(data.ToArray()) : null;

    /// <summary>
    /// Reads an entire packet after validating its declared length.
    /// </summary>
    /// <param name="stream">The input positioned at the packet.</param>
    /// <param name="length">The packet length declared by the container.</param>
    /// <param name="profile">The resulting profile, or null when the packet is empty.</param>
    /// <returns>Whether the declared packet was available and read in full.</returns>
    public static bool TryReadProfile(Stream stream, long length, out XmpProfile? profile)
    {
        profile = null;
        if (length == 0)
        {
            return true;
        }

        if (!stream.TryReadExactly(length, out byte[]? data))
        {
            return false;
        }

        profile = ReadProfile(data);
        return true;
    }

    /// <summary>
    /// Parses the XML document stored in an XMP packet.
    /// </summary>
    /// <param name="data">The packet bytes.</param>
    /// <returns>The document, or null when the packet is absent.</returns>
    public static XDocument? ReadDocument(byte[]? data)
    {
        if (data is null || data.Length == 0)
        {
            return null;
        }

        using MemoryStream stream = new(data, writable: false);

        XmlReaderSettings settings = new()
        {
            DtdProcessing = DtdProcessing.Ignore,
            XmlResolver = null,
            CloseInput = false
        };

        using XmlReader reader = XmlReader.Create(stream, settings);
        return XDocument.Load(reader, LoadOptions.PreserveWhitespace);
    }

    /// <summary>
    /// Removes a UTF-8 byte order mark and trailing packet padding when present.
    /// </summary>
    /// <param name="data">The packet bytes.</param>
    /// <returns>The normalized packet, or null when only padding remains.</returns>
    public static byte[]? ReadPacket(byte[]? data)
    {
        if (data is null || data.Length == 0)
        {
            return data;
        }

        // Allocation-free fast path for the normal case.

        // Check for UTF-8 BOM (0xEF,0xBB,0xBF)
        bool hasBom = data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF;

        // A TIFF writer can use tag 700 to store an XMP packet in a fixed-size block.
        // Producers often pad unused space so the packet can be updated in-place without
        // rewriting the file. In practice this padding is either NUL (0x00) from the container
        // or 0x0F used by Adobe XMP writers. Both are invalid XML and must be trimmed.
        bool hasTrailingPad = data[^1] is 0 or 0x0F;

        if (!hasBom && !hasTrailingPad)
        {
            return data;
        }

        int start = hasBom ? 3 : 0;
        int end = data.Length;

        if (hasTrailingPad)
        {
            while (end > start)
            {
                byte b = data[end - 1];
                if (b is not 0 and not 0x0F)
                {
                    break;
                }

                end--;
            }
        }

        int length = end - start;
        if (length <= 0)
        {
            return null;
        }

        byte[] normalized = new byte[length];
        Buffer.BlockCopy(data, start, normalized, 0, length);
        return normalized;
    }

    /// <summary>
    /// Checks that a nonempty packet fits in the available bytes and can be stored in an array.
    /// </summary>
    /// <param name="length">The declared packet length.</param>
    /// <param name="available">The number of bytes available to read.</param>
    /// <returns>Whether the packet can be read.</returns>
    private static bool IsPacketLengthValid(long length, long available)
        => length > 0 && length <= available && length <= Array.MaxLength;
}

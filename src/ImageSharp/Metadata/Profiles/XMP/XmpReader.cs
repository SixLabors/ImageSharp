// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Text;
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
        if (data is null)
        {
            return null;
        }

        // Exclude packet padding before parsing the XML.
        int count = data.Length;
        while (count > 0 && data[count - 1] is 0 or 0x0F)
        {
            count--;
        }

        using MemoryStream stream = new(data, 0, count);
        using StreamReader reader = new(stream, Encoding.UTF8);
        return XDocument.Load(reader);
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

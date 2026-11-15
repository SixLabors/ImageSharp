// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Globalization;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Metadata.Profiles.IPTC;

namespace SixLabors.ImageSharp.Metadata.Profiles.Iptc;

/// <summary>
/// Reads IPTC records and the Photoshop resources that contain them.
/// </summary>
internal sealed class IptcReader
{
    /// <summary>
    /// The marker at the start of an IPTC record.
    /// </summary>
    public const byte TagMarker = 0x1C;

    /// <summary>
    /// The largest payload that fits a standard IPTC record.
    /// </summary>
    public const int MaxStandardRecordLength = 0x7FFF;

    /// <summary>
    /// The IPTC resource identifier in a Photoshop resource block.
    /// </summary>
    public const ushort PhotoshopResourceId = 0x0404;

    private byte[]? data;

    /// <summary>
    /// Gets the optional header before the Photoshop resource blocks.
    /// </summary>
    public static ReadOnlySpan<byte> PhotoshopHeader => "Photoshop 3.0\0"u8;

    /// <summary>
    /// Gets the signature at the start of each Photoshop resource block.
    /// </summary>
    public static ReadOnlySpan<byte> PhotoshopResourceSignature => "8BIM"u8;

    /// <summary>
    /// Retains the IPTC packet from a complete Photoshop resource block.
    /// </summary>
    /// <param name="stream">The input positioned at the resource block.</param>
    /// <param name="length">The length declared by the JPEG segment.</param>
    /// <param name="skip">Whether to skip the metadata.</param>
    /// <returns>Whether the resource block was read without a framing error.</returns>
    public bool TryReadPhotoshopSegment(Stream stream, int length, bool skip)
    {
        ReadOnlySpan<byte> header = PhotoshopHeader;
        long available = stream.Length - stream.Position;
        if (length < header.Length || length > available)
        {
            stream.Position += Math.Min(Math.Max(0, length), Math.Max(0, available));
            return false;
        }

        long end = stream.Position + length;
        if (skip)
        {
            stream.Position = end;
            return true;
        }

        Span<byte> identifier = stackalloc byte[header.Length];
        if (!stream.TryReadExactly(identifier))
        {
            return false;
        }

        if (!identifier.SequenceEqual(header))
        {
            stream.Position = end;
            return true;
        }

        int size = length - header.Length;
        Span<byte> resources = size <= 128 ? stackalloc byte[size] : new byte[size];
        if (!stream.TryReadExactly(resources) || !TryReadPhotoshopResources(resources, out ReadOnlySpan<byte> iptc))
        {
            return false;
        }

        if (!iptc.IsEmpty)
        {
            this.data = iptc.ToArray();
        }

        return true;
    }

    /// <summary>
    /// Creates the profile from the last complete IPTC packet.
    /// </summary>
    /// <returns>The profile, or null if no IPTC packet was read.</returns>
    public IptcProfile? ToProfile() => this.data is not null ? new IptcProfile(this.data) : null;

    /// <summary>
    /// Reads application records from the supplied IPTC data.
    /// </summary>
    /// <param name="data">The IPTC record bytes.</param>
    /// <param name="values">The destination for decoded values.</param>
    public static void ReadRecords(ReadOnlySpan<byte> data, Collection<IptcValue> values)
    {
        if (data.IsEmpty || data[0] != TagMarker)
        {
            return;
        }

        const int RecordHeaderLength = 5;
        while (data.Length >= RecordHeaderLength)
        {
            byte marker = data[0];
            byte record = data[1];
            IptcTag tag = (IptcTag)data[2];
            int length = BinaryPrimitives.ReadUInt16BigEndian(data[3..]);
            data = data[RecordHeaderLength..];

            // Lengths with the top bit set describe extended records, which are unsupported.
            if (length > MaxStandardRecordLength || length > data.Length)
            {
                break;
            }

            if (marker == TagMarker && record == (byte)IptcRecordNumber.Application && length > 0)
            {
                values.Add(new IptcValue(tag, data[..length].ToArray(), false));
            }

            data = data[length..];
        }
    }

    /// <summary>
    /// Reads IPTC from the legacy PNG text format, unwrapping a Photoshop resource when present.
    /// </summary>
    /// <param name="text">The IPTC label, byte length, and hex data.</param>
    /// <returns>The decoded profile, or null if the text is invalid.</returns>
    public static IptcProfile? ReadLegacyText(ReadOnlySpan<char> text)
    {
        text = text.TrimStart();
        int labelEnd = text.IndexOf('\n');
        if (labelEnd < 0 || !text.StartsWith("iptc", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        text = text[(labelEnd + 1)..].TrimStart();
        int lengthEnd = text.IndexOf('\n');
        if (lengthEnd < 0 ||
            !int.TryParse(text[..lengthEnd], NumberStyles.Integer, CultureInfo.InvariantCulture, out int length) ||
            length <= 0 ||
            !HexConverter.TryDecodeLines(text[(lengthEnd + 1)..], length, out byte[]? data))
        {
            return null;
        }

        return new IptcProfile(TryReadPhotoshopResources(data, out ReadOnlySpan<byte> iptc) && !iptc.IsEmpty
            ? iptc.ToArray()
            : data);
    }

    /// <summary>
    /// Finds the IPTC payload in a sequence of Photoshop resource blocks.
    /// </summary>
    /// <param name="data">The resource blocks, with or without the Photoshop header.</param>
    /// <param name="iptc">The first IPTC payload, or an empty span if there is none.</param>
    /// <returns>Whether the resource blocks were valid up to the first IPTC payload.</returns>
    public static bool TryReadPhotoshopResources(ReadOnlySpan<byte> data, out ReadOnlySpan<byte> iptc)
    {
        iptc = default;
        if (data.StartsWith(PhotoshopHeader))
        {
            data = data[PhotoshopHeader.Length..];
        }

        while (!data.IsEmpty)
        {
            if (data.Length < 12 || !data.StartsWith(PhotoshopResourceSignature))
            {
                return false;
            }

            ushort resourceId = BinaryPrimitives.ReadUInt16BigEndian(data[4..]);

            // The Pascal string includes its length byte and is padded to an even size.
            int lengthOffset = 6 + ((data[6] + 2) & ~1);
            if (data.Length - lengthOffset < sizeof(uint))
            {
                return false;
            }

            uint size = BinaryPrimitives.ReadUInt32BigEndian(data[lengthOffset..]);
            data = data[(lengthOffset + sizeof(uint))..];
            if (size > (uint)data.Length)
            {
                return false;
            }

            if (resourceId == PhotoshopResourceId)
            {
                iptc = data[..(int)size];
                return !iptc.IsEmpty;
            }

            data = data[(int)size..];

            // Some writers omit the pad byte at the end of the final resource.
            if ((size & 1) != 0 && !data.IsEmpty)
            {
                data = data[1..];
            }
        }

        return true;
    }
}

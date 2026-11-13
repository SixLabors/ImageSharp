// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Text;
using SixLabors.ImageSharp.Metadata.Profiles.IPTC;

namespace SixLabors.ImageSharp.Metadata.Profiles.Iptc;

/// <summary>
/// Writes IPTC records and Photoshop resources.
/// </summary>
internal static class IptcWriter
{
    /// <summary>
    /// The size of an IPTC Photoshop resource header with an empty name.
    /// </summary>
    private const int PhotoshopResourceHeaderLength = 12;

    /// <summary>
    /// The dataset number of the coded character set record.
    /// </summary>
    private const byte CodedCharacterSet = 0x5A;

    /// <summary>
    /// Gets the IPTC escape sequence that declares UTF-8.
    /// </summary>
    private static ReadOnlySpan<byte> Utf8 => new byte[] { 0x1B, 0x25, 0x47 };

    /// <summary>
    /// Writes the IPTC application records and the UTF-8 declaration when needed.
    /// </summary>
    /// <param name="values">The profile values.</param>
    /// <returns>The serialized IPTC records.</returns>
    public static byte[] WriteRecords(Collection<IptcValue> values)
    {
        const int RecordHeaderLength = 5;
        long length = 0;
        bool usesUtf8 = false;
        foreach (IptcValue value in values)
        {
            if (value.Length > IptcReader.MaxStandardRecordLength)
            {
                throw new ImageFormatException("An IPTC value exceeds the standard record size.");
            }

            length += RecordHeaderLength + value.Length;
            usesUtf8 |= value.Encoding.CodePage == Encoding.UTF8.CodePage;
        }

        if (usesUtf8)
        {
            length += RecordHeaderLength + Utf8.Length;
        }

        if (length > Array.MaxLength)
        {
            throw new ImageFormatException("The IPTC profile exceeds the supported size.");
        }

        byte[] data = new byte[(int)length];
        int offset = 0;
        if (usesUtf8)
        {
            offset += WriteRecord(data, IptcRecordNumber.Envelope, CodedCharacterSet, Utf8);
        }

        foreach (IptcValue value in values)
        {
            offset += WriteRecord(data.AsSpan(offset), IptcRecordNumber.Application, (byte)value.Tag, value.ToByteArray());
        }

        return data;
    }

    /// <summary>
    /// Gets the resource length, including the header and even-byte padding.
    /// </summary>
    /// <param name="payloadLength">The number of IPTC data bytes.</param>
    /// <returns>The total number of bytes in the resource.</returns>
    public static long GetPhotoshopResourceLength(int payloadLength)
        => PhotoshopResourceHeaderLength + (long)payloadLength + GetPhotoshopResourcePadding(payloadLength);

    /// <summary>
    /// Writes a Photoshop resource directly to a stream.
    /// </summary>
    /// <param name="stream">The stream receiving the resource.</param>
    /// <param name="payload">The unpadded IPTC bytes.</param>
    public static void WritePhotoshopResource(Stream stream, byte[] payload)
    {
        Span<byte> header = stackalloc byte[PhotoshopResourceHeaderLength];
        WritePhotoshopResourceHeader(header, payload.Length);
        stream.Write(header);
        stream.Write(payload, 0, payload.Length);
        if (GetPhotoshopResourcePadding(payload.Length) != 0)
        {
            stream.WriteByte(0);
        }
    }

    /// <summary>
    /// Gets the zero padding needed to align a Photoshop resource payload to an even byte count.
    /// </summary>
    /// <param name="payloadLength">The number of IPTC bytes.</param>
    /// <returns>The number of padding bytes.</returns>
    private static int GetPhotoshopResourcePadding(int payloadLength) => payloadLength & 1;

    /// <summary>
    /// Writes the header of a Photoshop resource with an empty Pascal name.
    /// </summary>
    /// <param name="destination">The destination for the header.</param>
    /// <param name="payloadLength">The unpadded number of IPTC data bytes.</param>
    private static void WritePhotoshopResourceHeader(Span<byte> destination, int payloadLength)
    {
        IptcReader.PhotoshopResourceSignature.CopyTo(destination);
        BinaryPrimitives.WriteUInt16BigEndian(destination[4..], IptcReader.PhotoshopResourceId);
        destination.Slice(6, 2).Clear();
        BinaryPrimitives.WriteInt32BigEndian(destination[8..], payloadLength);
    }

    /// <summary>
    /// Writes a single IPTC record and returns the number of bytes written.
    /// </summary>
    /// <param name="destination">The destination for the record.</param>
    /// <param name="record">The record number.</param>
    /// <param name="tag">The dataset number.</param>
    /// <param name="value">The record payload.</param>
    /// <returns>The number of bytes written.</returns>
    private static int WriteRecord(Span<byte> destination, IptcRecordNumber record, byte tag, ReadOnlySpan<byte> value)
    {
        destination[0] = IptcReader.TagMarker;
        destination[1] = (byte)record;
        destination[2] = tag;
        BinaryPrimitives.WriteUInt16BigEndian(destination[3..], (ushort)value.Length);
        value.CopyTo(destination[5..]);
        return 5 + value.Length;
    }
}

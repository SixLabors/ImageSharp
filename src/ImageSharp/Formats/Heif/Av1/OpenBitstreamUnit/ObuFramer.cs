// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

/// <summary>
/// Finds the OBU boundaries in a bounded low-overhead AV1 payload. The OBU reader and the container scan use this type, so both apply the same framing rules.
/// </summary>
/// <remarks>
/// AV1 Bitstream &amp; Decoding Process Specification, section 5.3 "OBU syntax" and section 4.10.5 "leb128()". The specification is at
/// https://aomediacodec.github.io/av1-spec/av1-spec.pdf.
/// </remarks>
internal static class ObuFramer
{
    /// <summary>
    /// The maximum number of bytes in a LEB128 value. AV1 section 4.10.5 reads at most eight bytes.
    /// </summary>
    private const int MaxLeb128Size = 8;

    /// <summary>
    /// Reads one unsigned LEB128 value.
    /// </summary>
    /// <remarks>
    /// Each byte holds 7 value bits and a continuation flag in bit 7. The value ends at the first byte with bit 7 clear. AV1 section 4.10.5 requires a value of
    /// at most 2^32 - 1. The syntax reads at most eight bytes. Thus the reader rejects a value that does not end in eight bytes.
    /// </remarks>
    /// <param name="data">The bytes from the first byte of the value to the end of the data.</param>
    /// <param name="value">Receives the value.</param>
    /// <param name="length">Receives the number of bytes of the value.</param>
    /// <returns>
    /// <see cref="ObuFramingStatus.Complete"/>, <see cref="ObuFramingStatus.Truncated"/> when the data ends inside the value, or
    /// <see cref="ObuFramingStatus.InvalidSize"/>.
    /// </returns>
    public static ObuFramingStatus TryReadLeb128(ReadOnlySpan<byte> data, out uint value, out int length)
    {
        ulong result = 0;
        int count = Math.Min(data.Length, MaxLeb128Size);
        for (int i = 0; i < count; i++)
        {
            byte current = data[i];
            result |= (ulong)(current & 0x7F) << (i * 7);
            if ((current & 0x80) == 0)
            {
                length = i + 1;
                if (result > uint.MaxValue)
                {
                    value = 0;
                    return ObuFramingStatus.InvalidSize;
                }

                value = (uint)result;
                return ObuFramingStatus.Complete;
            }
        }

        value = 0;
        length = 0;
        return data.Length < MaxLeb128Size ? ObuFramingStatus.Truncated : ObuFramingStatus.InvalidSize;
    }

    /// <summary>
    /// Reads the header and the size field of the first OBU in the data.
    /// </summary>
    /// <remarks>
    /// In AV1 section 5.3.2, the first header byte holds obu_forbidden_bit in bit 7 and obu_type in bits 3 to 6. Bit 2 holds obu_extension_flag, bit 1 holds
    /// obu_has_size_field, and bit 0 holds obu_reserved_1bit. The reader ignores the reserved bit. AV1-ISOBMFF lets the last OBU of a sample omit its size
    /// field. The payload of that OBU is then the rest of the data.
    /// </remarks>
    /// <param name="data">The bytes from the first header byte to the end of the bounded payload. It is not empty.</param>
    /// <param name="obu">Receives the OBU boundaries. The payload stays inside the data.</param>
    /// <returns>
    /// <see cref="ObuFramingStatus.Complete"/> or <see cref="ObuFramingStatus.PayloadTruncated"/> when <paramref name="obu"/> is set. Otherwise the error that
    /// stops the framing.
    /// </returns>
    public static ObuFramingStatus ReadObu(ReadOnlySpan<byte> data, out ObuHeader obu)
    {
        DebugGuard.IsTrue(data.Length > 0, "The OBU data must not be empty.");

        obu = default;
        int header = data[0];
        if ((header & 0x80) != 0)
        {
            return ObuFramingStatus.ForbiddenBit;
        }

        bool hasExtension = (header & 0x04) != 0;
        bool hasSizeField = (header & 0x02) != 0;
        int payloadOffset = hasExtension ? 2 : 1;
        if (data.Length < payloadOffset)
        {
            return ObuFramingStatus.Truncated;
        }

        ObuFramingStatus status = ObuFramingStatus.Complete;
        int payloadLength;
        if (hasSizeField)
        {
            ObuFramingStatus sizeStatus = TryReadLeb128(data[payloadOffset..], out uint payloadSize, out int sizeLength);
            if (sizeStatus != ObuFramingStatus.Complete)
            {
                return sizeStatus;
            }

            payloadOffset += sizeLength;
            payloadLength = data.Length - payloadOffset;

            // The integrity mode of the caller decides the result for a payload that ends after the data. The payload holds the bytes that remain.
            if (payloadSize > (uint)payloadLength)
            {
                status = ObuFramingStatus.PayloadTruncated;
            }
            else
            {
                payloadLength = (int)payloadSize;
            }
        }
        else
        {
            payloadLength = data.Length - payloadOffset;
        }

        obu = new ObuHeader((ObuType)((header >> 3) & 0x0F), hasExtension ? data[1] : -1, hasSizeField, payloadOffset, payloadLength);
        return status;
    }

    /// <summary>
    /// Gets the error message for a framing status that is not <see cref="ObuFramingStatus.Complete"/>.
    /// </summary>
    /// <param name="status">The framing status.</param>
    /// <returns>The error message.</returns>
    public static string GetMessage(ObuFramingStatus status)
        => status switch
        {
            ObuFramingStatus.Truncated => "The AV1 data ends inside an OBU header or its size field.",
            ObuFramingStatus.PayloadTruncated => "The AV1 OBU payload exceeds its data boundary.",
            ObuFramingStatus.ForbiddenBit => "The AV1 OBU header has its forbidden bit set.",
            _ => "The AV1 OBU size field is not terminated within eight bytes or exceeds 2^32 - 1."
        };
}

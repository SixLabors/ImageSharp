// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Hevc;

/// <summary>
/// Contains one decoded HEVC network abstraction layer unit.
/// </summary>
internal sealed class HevcNalUnit
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HevcNalUnit"/> class.
    /// </summary>
    /// <param name="data">The complete NAL unit, including its two-byte header.</param>
    /// <exception cref="InvalidImageContentException">The NAL header or encoded payload is malformed.</exception>
    public HevcNalUnit(ReadOnlySpan<byte> data)
    {
        this.Header = HevcNalUnitHeader.Parse(data);

        // Container and configuration NAL units carry EBSP bytes. Decode them once at the boundary so every
        // parameter-set and slice parser observes the same validated RBSP representation.
        ReadOnlySpan<byte> encodedPayload = data[2..];
        byte[] rbspBuffer = new byte[encodedPayload.Length];
        int rbspLength = HevcRbspDecoder.Decode(encodedPayload, rbspBuffer);
        this.Rbsp = rbspBuffer.AsMemory(0, rbspLength);
    }

    /// <summary>
    /// Gets the decoded two-byte NAL-unit header.
    /// </summary>
    public HevcNalUnitHeader Header { get; }

    /// <summary>
    /// Gets the raw byte sequence payload after removal of emulation-prevention bytes.
    /// </summary>
    public ReadOnlyMemory<byte> Rbsp { get; }
}

/// <summary>
/// Removes HEVC emulation-prevention bytes from an encoded raw byte sequence payload.
/// </summary>
internal static class HevcRbspDecoder
{
    /// <summary>
    /// Decodes an encoded byte sequence payload into a raw byte sequence payload.
    /// </summary>
    /// <param name="encodedPayload">The NAL payload following the two-byte header.</param>
    /// <param name="destination">A buffer at least as long as <paramref name="encodedPayload"/>.</param>
    /// <returns>The number of decoded bytes written to <paramref name="destination"/>.</returns>
    /// <exception cref="InvalidImageContentException">
    /// The payload contains a forbidden start-code-like byte sequence or an invalid emulation-prevention byte.
    /// </exception>
    public static int Decode(ReadOnlySpan<byte> encodedPayload, Span<byte> destination)
    {
        DebugGuard.MustBeGreaterThanOrEqualTo(destination.Length, encodedPayload.Length, nameof(destination));

        int destinationOffset = 0;
        int consecutiveZeroBytes = 0;
        for (int sourceOffset = 0; sourceOffset < encodedPayload.Length; sourceOffset++)
        {
            byte value = encodedPayload[sourceOffset];

            // HEVC section 7.3.1.1 forbids 00 00 00 through 00 00 02 in EBSP form. A 03 after two zeros is an
            // emulation-prevention byte only when another byte in the range 00 through 03 follows it.
            if (consecutiveZeroBytes == 2)
            {
                if (value < 3)
                {
                    throw new InvalidImageContentException("The HEVC NAL unit contains a forbidden start-code-like byte sequence.");
                }

                if (value == 3)
                {
                    sourceOffset++;
                    if (sourceOffset == encodedPayload.Length || encodedPayload[sourceOffset] > 3)
                    {
                        throw new InvalidImageContentException("The HEVC NAL unit contains an invalid emulation-prevention byte.");
                    }

                    // Removal depends on the preceding two decoded bytes, so this deliberately remains a single
                    // scalar pass rather than introducing a second SIMD behavior model for a non-hot syntax path.
                    value = encodedPayload[sourceOffset];
                    consecutiveZeroBytes = 0;
                }
            }

            destination[destinationOffset++] = value;
            consecutiveZeroBytes = value == 0 ? consecutiveZeroBytes + 1 : 0;
        }

        return destinationOffset;
    }
}

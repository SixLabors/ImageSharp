// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Hevc;

/// <summary>
/// Contains the type, layer, and temporal identifier encoded by an HEVC NAL-unit header.
/// </summary>
internal readonly struct HevcNalUnitHeader
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HevcNalUnitHeader"/> struct.
    /// </summary>
    /// <param name="nalUnitType">The six-bit NAL-unit type.</param>
    /// <param name="layerId">The six-bit layer identifier.</param>
    /// <param name="temporalId">The zero-based temporal identifier.</param>
    private HevcNalUnitHeader(byte nalUnitType, byte layerId, byte temporalId)
    {
        this.NalUnitType = nalUnitType;
        this.LayerId = layerId;
        this.TemporalId = temporalId;
    }

    /// <summary>
    /// Gets the six-bit NAL-unit type.
    /// </summary>
    public byte NalUnitType { get; }

    /// <summary>
    /// Gets the six-bit layer identifier.
    /// </summary>
    public byte LayerId { get; }

    /// <summary>
    /// Gets the zero-based temporal identifier.
    /// </summary>
    public byte TemporalId { get; }

    /// <summary>
    /// Gets a value indicating whether the NAL unit contains coded slice-segment data.
    /// </summary>
    public bool IsVideoCodingLayer => this.NalUnitType <= 31;

    /// <summary>
    /// Gets a value indicating whether the NAL unit begins an instantaneous decoder refresh picture.
    /// </summary>
    public bool IsInstantaneousDecoderRefresh => this.NalUnitType is 19 or 20;

    /// <summary>
    /// Reads and validates an HEVC NAL-unit header.
    /// </summary>
    /// <param name="data">The complete NAL unit beginning with its two-byte header.</param>
    /// <returns>The decoded header.</returns>
    /// <exception cref="InvalidImageContentException">
    /// The header is truncated, its forbidden bit is set, or its temporal identifier is reserved.
    /// </exception>
    public static HevcNalUnitHeader Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < 2)
        {
            throw new InvalidImageContentException("The HEVC NAL-unit header is truncated.");
        }

        // Use the same bounded MSB-first reader as the RBSP parsers so header truncation and field ordering have
        // one behavior model instead of a second set of shifts and masks.
        HevcBitReader reader = new(data[..2]);
        bool forbiddenZeroBit = reader.ReadFlag();
        byte nalUnitType = (byte)reader.ReadBits(6);
        byte layerId = (byte)reader.ReadBits(6);
        byte temporalIdPlusOne = (byte)reader.ReadBits(3);
        if (forbiddenZeroBit || temporalIdPlusOne == 0)
        {
            throw new InvalidImageContentException("The HEVC NAL-unit header is invalid.");
        }

        return new HevcNalUnitHeader(nalUnitType, layerId, (byte)(temporalIdPlusOne - 1));
    }
}

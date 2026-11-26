// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

/// <summary>
/// Gives the header fields and the byte boundaries of one AV1 open bitstream unit in a bounded payload.
/// </summary>
internal readonly struct ObuHeader
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ObuHeader"/> struct.
    /// </summary>
    /// <param name="type">The OBU type.</param>
    /// <param name="extension">The extension byte, or <c>-1</c> when the header has no extension.</param>
    /// <param name="hasSizeField">A value indicating whether the header has an obu_size field.</param>
    /// <param name="payloadOffset">The offset of the payload from the first header byte.</param>
    /// <param name="payloadLength">The number of payload bytes in the data.</param>
    public ObuHeader(ObuType type, int extension, bool hasSizeField, int payloadOffset, int payloadLength)
    {
        this.Type = type;
        this.Extension = extension;
        this.HasSizeField = hasSizeField;
        this.PayloadOffset = payloadOffset;
        this.PayloadLength = payloadLength;
    }

    /// <summary>
    /// Gets the OBU type.
    /// </summary>
    public ObuType Type { get; }

    /// <summary>
    /// Gets the extension byte, or <c>-1</c> when the header has no extension.
    /// </summary>
    public int Extension { get; }

    /// <summary>
    /// Gets a value indicating whether the header has an extension byte.
    /// </summary>
    public bool HasExtension => this.Extension >= 0;

    /// <summary>
    /// Gets the temporal-layer identifier. AV1 section 5.3.3 stores it in bits 5 to 7 of the extension byte.
    /// </summary>
    public int TemporalId => this.HasExtension ? this.Extension >> 5 : 0;

    /// <summary>
    /// Gets the spatial-layer identifier. AV1 section 5.3.3 stores it in bits 3 and 4 of the extension byte.
    /// </summary>
    public int SpatialId => this.HasExtension ? (this.Extension >> 3) & 0x03 : 0;

    /// <summary>
    /// Gets a value indicating whether the header has an obu_size field.
    /// </summary>
    public bool HasSizeField { get; }

    /// <summary>
    /// Gets the offset of the payload from the first header byte.
    /// </summary>
    public int PayloadOffset { get; }

    /// <summary>
    /// Gets the number of payload bytes in the data.
    /// </summary>
    public int PayloadLength { get; }

    /// <summary>
    /// Gets the number of bytes from the first header byte to the end of the payload.
    /// </summary>
    public int Size => this.PayloadOffset + this.PayloadLength;
}

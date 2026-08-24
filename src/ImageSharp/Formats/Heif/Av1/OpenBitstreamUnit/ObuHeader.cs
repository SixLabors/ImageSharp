// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

/// <summary>
/// Contains the parsed header and payload size of an AV1 open bitstream unit.
/// </summary>
internal class ObuHeader
{
    /// <summary>
    /// Gets or sets the number of bytes occupied by the OBU header and its size field.
    /// </summary>
    public int Size { get; set; }

    /// <summary>
    /// Gets or sets the OBU payload type.
    /// </summary>
    public ObuType Type { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the OBU carries an explicit payload-size field.
    /// </summary>
    public bool HasSize { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the OBU carries temporal and spatial identifiers.
    /// </summary>
    public bool HasExtension { get; set; }

    /// <summary>
    /// Gets or sets the temporal-layer identifier.
    /// </summary>
    public int TemporalId { get; set; }

    /// <summary>
    /// Gets or sets the spatial-layer identifier.
    /// </summary>
    public int SpatialId { get; set; }

    /// <summary>
    /// Gets or sets the OBU payload size, in bytes.
    /// </summary>
    public int PayloadSize { get; set; }
}

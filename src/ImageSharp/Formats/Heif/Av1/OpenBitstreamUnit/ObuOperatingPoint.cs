// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

/// <summary>
/// Contains the sequence-level constraints for an AV1 operating point.
/// </summary>
internal class ObuOperatingPoint
{
    /// <summary>
    /// Gets or sets the operating-point index.
    /// </summary>
    internal int OperatorIndex { get; set; }

    /// <summary>
    /// Gets or sets the AV1 sequence-level index.
    /// </summary>
    internal int SequenceLevelIndex { get; set; }

    /// <summary>
    /// Gets or sets the sequence tier.
    /// </summary>
    internal int SequenceTier { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether decoder-model timing is present for this operating point.
    /// </summary>
    internal bool IsDecoderModelInfoPresent { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether an initial display delay is present for this operating point.
    /// </summary>
    internal bool IsInitialDisplayDelayPresent { get; set; }

    /// <summary>
    /// Gets or sets the initial display delay minus one, in decoded frames.
    /// </summary>
    internal uint InitialDisplayDelay { get; set; }

    /// <summary>
    /// Gets or sets the bitmask selecting temporal and spatial layers for the operating point.
    /// A value of zero selects the complete coded sequence.
    /// </summary>
    internal uint Idc { get; set; }
}

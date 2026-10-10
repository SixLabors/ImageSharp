// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

/// <summary>
/// Contains the sequence-level constraints for an AV1 operating point.
/// </summary>
internal sealed class ObuOperatingPoint
{
    /// <summary>
    /// Gets or sets the operating-point index.
    /// </summary>
    public int OperatorIndex { get; set; }

    /// <summary>
    /// Gets or sets the AV1 sequence-level index.
    /// </summary>
    public int SequenceLevelIndex { get; set; }

    /// <summary>
    /// Gets or sets the sequence tier.
    /// </summary>
    public int SequenceTier { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether decoder-model timing is present for this operating point.
    /// </summary>
    public bool IsDecoderModelInfoPresent { get; set; }

    /// <summary>
    /// Gets or sets the decoder-buffer delay measured in decoding ticks.
    /// </summary>
    public uint DecoderBufferDelay { get; set; }

    /// <summary>
    /// Gets or sets the encoder-buffer delay measured in decoding ticks.
    /// </summary>
    public uint EncoderBufferDelay { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the operating point uses the low-delay decoding model.
    /// </summary>
    public bool LowDelayMode { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether an initial display delay is present for this operating point.
    /// </summary>
    public bool IsInitialDisplayDelayPresent { get; set; }

    /// <summary>
    /// Gets or sets the initial display delay, in decoded frames.
    /// </summary>
    public uint InitialDisplayDelay { get; set; }

    /// <summary>
    /// Gets or sets the bitmask selecting temporal and spatial layers for the operating point.
    /// A value of zero selects the complete coded sequence.
    /// </summary>
    public uint Idc { get; set; }
}

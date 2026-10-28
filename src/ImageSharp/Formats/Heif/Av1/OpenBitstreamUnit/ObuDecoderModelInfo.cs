// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1;

/// <summary>
/// Contains the field widths and decoding-clock units used by the AV1 decoder model.
/// </summary>
internal sealed class ObuDecoderModelInfo
{
    /// <summary>
    /// Gets or sets the length, in bits, of the `decoder_buffer_delay` and `encoder_buffer_delay` syntax elements.
    /// </summary>
    public uint BufferDelayLength { get; set; }

    /// <summary>
    /// Gets or sets the number of time units of the decoding clock in one clock tick. The decoding clock runs at `time_scale` Hz.
    /// </summary>
    public uint NumUnitsInDecodingTick { get; set; }

    /// <summary>
    /// Gets or sets the length, in bits, of the `buffer_removal_time` syntax element.
    /// </summary>
    public uint BufferRemovalTimeLength { get; set; }

    /// <summary>
    /// Gets or sets the length, in bits, of the `frame_presentation_time` syntax element.
    /// </summary>
    public uint FramePresentationTimeLength { get; set; }
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1;

internal class ObuDecoderModelInfo
{
    /// <summary>
    /// Gets or sets BufferDelayLength. Specifies the length of the decoder_buffer_delay and the encoder_buffer_delay
    /// syntax elements, in bits.
    /// </summary>
    internal uint BufferDelayLength { get; set; }

    /// <summary>
    /// Gets or sets NumUnitsInDecodingTick. This is the number of time units of a decoding clock operating at the frequency time_scale Hz
    /// that corresponds to one increment of a clock tick counter.
    /// </summary>
    internal uint NumUnitsInDecodingTick { get; set; }

    /// <summary>
    /// Gets or sets BufferRemovalTimeLength. Specifies the length of the buffer_removal_time syntax element, in bits.
    /// </summary>
    internal uint BufferRemovalTimeLength { get; set; }

    /// <summary>
    /// Gets or sets the FramePresentationTimeLength. Specifies the length of the frame_presentation_time syntax element, in bits.
    /// </summary>
    internal uint FramePresentationTimeLength { get; set; }
}

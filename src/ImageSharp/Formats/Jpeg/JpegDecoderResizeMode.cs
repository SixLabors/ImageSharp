// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jpeg;

/// <summary>
/// Specifies how the JPEG decoder makes an image of <see cref="DecoderOptions.TargetSize"/>.
/// The decoder uses this value only when <see cref="DecoderOptions.TargetSize"/> has a value.
/// </summary>
public enum JpegDecoderResizeMode
{
    /// <summary>
    /// The decoder decodes at the smallest scale (1/8, 1/4, 1/2 or full size) that covers the target size.
    /// If that result is larger than the target size, the decoder resizes it with <see cref="DecoderOptions.Sampler"/>.
    /// This mode is the fastest mode that gives the target size.
    /// </summary>
    Combined,

    /// <summary>
    /// The decoder decodes at the smallest scale (1/8, 1/4, 1/2 or full size) that covers the target size.
    /// The decoder does not resize the result, so the result can be larger than the target size.
    /// </summary>
    IdctOnly,

    /// <summary>
    /// The decoder decodes at full size. Then it resizes the result to the target size with <see cref="DecoderOptions.Sampler"/>.
    /// This mode does the most work, and it gives the highest quality.
    /// </summary>
    ScaleOnly
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Processing;

namespace SixLabors.ImageSharp.Formats.Jpeg
{
    /// <summary>
    /// Provides enumeration for resize modes taken during decoding.
    /// Applicable only when <see cref="DecoderOptions.TargetSize"/> has a value.
    /// </summary>
    public enum JpegDecoderResizeMode
    {
        /// <summary>
        /// Both <see cref="IdctOnly"/> and <see cref="ScaleOnly"/>.
        /// </summary>
        Combined,

        /// <summary>
        /// IDCT-only to nearest block scale. Similar in output to <see cref="KnownResamplers.Box"/>.
        /// </summary>
        IdctOnly,

        /// <summary>
        /// Opt-out the IDCT part and only Resize. Can be useful in case of quality concerns.
        /// </summary>
        ScaleOnly
    }
}

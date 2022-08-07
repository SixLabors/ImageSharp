// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

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
        /// IDCT-only to nearest block scale.
        /// </summary>
        IdctOnly,

        /// <summary>
        /// Opt-out the IDCT part and only Resize. Can be useful in case of quality concerns.
        /// </summary>
        ScaleOnly
    }
}

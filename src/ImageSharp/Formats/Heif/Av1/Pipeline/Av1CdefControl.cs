// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// The CDEF control of the encoder.
/// </summary>
internal enum Av1CdefControl
{
    /// <summary>
    /// The encoder disables CDEF.
    /// </summary>
    None,

    /// <summary>
    /// The encoder enables CDEF for every frame.
    /// </summary>
    All,

    /// <summary>
    /// The CDEF strength adapts to the frame quantizer.
    /// </summary>
    Adaptive,
}

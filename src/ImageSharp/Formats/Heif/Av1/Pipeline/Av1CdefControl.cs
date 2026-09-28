// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// The CDEF control of the encoder. Reference: CDEF_CONTROL.
/// </summary>
internal enum Av1CdefControl
{
    /// <summary>
    /// CDEF is disabled. Reference: CDEF_NONE.
    /// </summary>
    None,

    /// <summary>
    /// CDEF is enabled for every frame. Reference: CDEF_ALL.
    /// </summary>
    All,

    /// <summary>
    /// CDEF is enabled for reference frames only. Reference: CDEF_REFERENCE.
    /// </summary>
    Reference,

    /// <summary>
    /// CDEF strength adapts to the frame quantizer. Reference: CDEF_ADAPTIVE.
    /// </summary>
    Adaptive,
}

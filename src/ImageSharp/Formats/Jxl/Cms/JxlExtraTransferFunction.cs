// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Cms.TransferFunctions;

namespace SixLabors.ImageSharp.Formats.Jxl.Cms;

/// <summary>
/// Specifies the kind of extra transfer function.
/// </summary>
internal enum JxlExtraTransferFunction : byte
{
    /// <summary>
    /// No transfer functions.
    /// </summary>
    None,

    /// <summary>
    /// Perceptual Quantization. <seealso cref="JxlPqTransferFunction"/>
    /// </summary>
    Pq,

    /// <summary>
    /// Hybrid Log Gamma. <seealso cref="JxlHlgTransferFunction"/>.
    /// </summary>
    Hlg,

    /// <summary>
    /// sRGB. <seealso cref="JxlSRgbTransferFunction"/>.
    /// </summary>
    SRgb,
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Quantization;

/// <summary>
/// Specifies which algorithm should be used to quantize coefficients.
/// </summary>
internal enum JxlQuantMode : byte
{
    /// <summary>
    /// The quantizer relies on predefined tables for quantization.
    /// The idea is similar to Huffman coding.
    /// </summary>
    Library,

    /// <summary>
    /// The quantizer uses an Identity transform.
    /// </summary>
    Id,

    /// <summary>
    /// The quantizer uses a 2x2 Discrete Cosine Transform.
    /// </summary>
    Dct2,

    /// <summary>
    /// The quantizer uses a 4x4 Discrete Cosine Transform.
    /// </summary>
    Dct4,

    /// <summary>
    /// The quantizer uses a 4x8 Discrete Cosine Transform.
    /// </summary>
    Dct4x8,

    /// <summary>
    /// The quantizer uses the AFV transform.
    /// </summary>
    Afv,

    /// <summary>
    /// The quantizer uses a Discrete Cosine Transform with custom block size.
    /// </summary>
    Dct,

    /// <summary>
    /// No quantization is performed. Input data becomes the output as-is.
    /// </summary>
    Raw
}

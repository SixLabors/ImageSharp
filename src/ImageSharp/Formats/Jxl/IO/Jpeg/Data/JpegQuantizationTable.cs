// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.IO.Jpeg.Data;

/// <summary>
/// Representation of quantization values for an 8x8 pixel block.
/// </summary>
// We may need to get a ref to fields, so don't
// make these properties.
internal struct JpegQuantizationTable()
{
    /// <summary>
    /// Quantization values
    /// </summary>
    public InlineArray64<int> Values;

    public int Precision;

    /// <summary>
    /// Gets or sets the index of the quantization table
    /// as it was parsed from the input JPEG.
    /// </summary>
    public int Index;

    /// <summary>
    /// Gets or sets a value indicating whether this table
    /// is the last one within its marker segment.
    /// </summary>
    public bool IsLast = true;
}

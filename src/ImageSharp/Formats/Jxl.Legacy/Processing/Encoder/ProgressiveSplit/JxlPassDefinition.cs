// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.ProgressiveSplit;

internal struct JxlPassDefinition
{
    public JxlPassDefinition(int numCoefficients, int shift, int suitableForDownsamplingOfAtLeast)
    {
        this.NumCoefficients = numCoefficients;
        this.Shift = shift;
        this.SuitableForDownsamplingOfAtLeast = suitableForDownsamplingOfAtLeast;
    }

    /// <summary>
    /// Gets or sets the side of the square of the coefficients that should be kept in
    /// each 8x8 block. Must be > 1, and at most 8. Should be in non-decreasing
    /// order.
    /// </summary>
    public int NumCoefficients { get; set; }

    /// <summary>
    /// Gets or sets how much to shift the encoded values by, with rounding.
    /// </summary>
    public int Shift { get; set; }

    /// <summary>
    /// Gets or sets a value where, if specified indicates that if the required downsampling factor
    /// is sufficiently high, then it is fine to stop decoding after this pass.
    /// By default, passes are not marked as being suitable for any downsampling.
    /// </summary>
    public int SuitableForDownsamplingOfAtLeast { get; set; }
}

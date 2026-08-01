// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;

internal sealed class JxlOpsinParameters
{
    // Use arrays instead of InlineArrays because, with inline arrays we can't do:
    //      JxlOpsinParameters parameters = ...;
    //      parameters.OpsinBiasesCbrt[0] /* <-- error */ = 1.25f;
    public float[] InverseOpsinMatrix { get; set; } = new float[36];

    public float[] OpsinBiases { get; set; } = new float[4];

    public float[] OpsinBiasesCbrt { get; set; } = new float[4];

    public float[] QuantBiases { get; set; } = new float[4];
}

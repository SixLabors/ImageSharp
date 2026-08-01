// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Fields;
using SixLabors.ImageSharp.Formats.Jxl.Processing;

namespace SixLabors.ImageSharp.Formats.Jxl.IO.Metadata;

internal sealed class JxlOpsinInverseMatrix : IJxlFields
{
    public bool AllDefault { get; set; }

    public JxlMatrix3x3F InverseMatrix { get; set; }

    // Prefer arrays so we can set values like this:
    //      JxlOpsinInverseMatrix m = ...;
    //      m.OpsinBiases[0] = 1f;
    // An InlineArray can't do that.
    public float[] OpsinBiases { get; set; } = new float[3];

    public float[] QuantBiases { get; set; } = new float[4];

    public bool Visit(JxlVisitor visitor) => throw new NotImplementedException();
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

#pragma warning disable SA1401 // Fields should be private

using SixLabors.ImageSharp.Formats.Jxl.Fields;
using SixLabors.ImageSharp.Formats.Jxl.Processing;

namespace SixLabors.ImageSharp.Formats.Jxl.IO.Metadata;

internal sealed class JxlOpsinInverseMatrix : IJxlFields
{
    public InlineArray3<float> OpsinBiases;

    public InlineArray3<float> QuantBiases;

    public bool AllDefault { get; set; }

    public JxlMatrix3x3F InverseMatrix { get; set; }

    public bool Visit(JxlVisitor visitor) => throw new NotImplementedException();
}

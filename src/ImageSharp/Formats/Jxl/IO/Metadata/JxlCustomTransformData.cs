// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Fields;

namespace SixLabors.ImageSharp.Formats.Jxl.IO.Metadata;

internal sealed class JxlCustomTransformData : IJxlFields
{
    public bool NonserializedXybEncoded { get; set; }

    public bool AllDefault { get; set; }

    public JxlOpsinInverseMatrix? OpsinInverseMatrix { get; set; }

    public int CustomWeightsMask { get; set; }

    public InlineArray15<float> Upsampling2Weights { get; set; }

    public InlineArray55<float> Upsampling4Weights { get; set; }

    public InlineArray210<float> Upsampling8Weights { get; set; }

    public bool Visit(JxlVisitor visitor) => throw new NotImplementedException();
}

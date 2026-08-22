// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Fields;

namespace SixLabors.ImageSharp.Formats.Jxl.IO.Metadata;

internal sealed class JxlExtraChannelInfo : IJxlFields
{
    public bool AllDefault { get; set; }

    public JxlExtraChannel Type { get; set; }

    public JxlBitDepth? BitDepth { get; set; }

    public int DimensionShift { get; set; }

    public string? Name { get; set; }

    public bool AlphaAssociated { get; set; }

    public InlineArray4<float> SpotColor { get; set; }

    public int CfaChannel { get; set; }

    public bool Visit(JxlVisitor visitor) => throw new NotImplementedException();
}

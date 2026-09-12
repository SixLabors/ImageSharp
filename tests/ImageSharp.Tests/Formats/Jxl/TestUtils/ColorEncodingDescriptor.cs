// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Cms;

namespace SixLabors.ImageSharp.Tests.Formats.Jxl.TestUtils;

internal struct ColorEncodingDescriptor
{
    public JxlColorSpace ColorSpace { get; set; }

    public JxlWhitePoint WhitePoint { get; set; }

    public JxlPrimaries Primaries { get; set; }

    public JxlTransferFunction TransferFunction { get; set; }

    public JxlRenderingIntent RenderingIntent { get; set; }
}

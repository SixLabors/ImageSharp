// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder.Ans;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Modular;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Encoding;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Transforms;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder.Modular;

internal sealed class JxlModularFrameDecoder
{
    private readonly Configuration configuration;
    private readonly JxlModularImage fullImage;
    private readonly List<JxlTransform> globalTransform;
    private JxlFrameDimensions frameDimensions;
    private bool doColor;
    private bool haveSomething;
    private bool useFullImage = true;
    private bool allSameShift;
    private JxlAnsCode code;
    private readonly List<byte> contextMap;
    private JxlGroupHeader groupHeader;
}

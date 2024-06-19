// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Icon;

namespace SixLabors.ImageSharp.Formats.Cur;

internal sealed class CurEncoderCore : IconEncoderCore
{
    public CurEncoderCore(QuantizingImageEncoder encoder)
        : base(encoder, IconFileType.CUR)
    {
    }
}

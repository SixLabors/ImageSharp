// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Metadata;

namespace SixLabors.ImageSharp.Formats.Icon.Cur;

internal sealed class CurDecoderCore : IconDecoderCore
{
    public CurDecoderCore(DecoderOptions options)
        : base(options)
    {
    }

    protected override IconFrameMetadata GetFrameMetadata(ImageFrameMetadata metadata) => metadata.GetCurMetadata();
}

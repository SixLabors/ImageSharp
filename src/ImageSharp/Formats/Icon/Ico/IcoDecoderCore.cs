// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Metadata;

namespace SixLabors.ImageSharp.Formats.Icon.Ico;

internal sealed class IcoDecoderCore : IconDecoderCore
{
    public IcoDecoderCore(DecoderOptions options)
        : base(options)
    {
    }

    protected override IconFrameMetadata GetFrameMetadata(ImageFrameMetadata metadata) => metadata.GetIcoMetadata();
}

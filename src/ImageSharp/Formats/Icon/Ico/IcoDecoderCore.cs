// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Metadata;

// TODO: flatten namespace.
// namespace SixLabors.ImageSharp.Formats.Ico;
namespace SixLabors.ImageSharp.Formats.Icon.Ico;

internal sealed class IcoDecoderCore : IconDecoderCore
{
    public IcoDecoderCore(DecoderOptions options)
        : base(options)
    {
    }

    protected override void SetFrameMetadata(ImageFrameMetadata metadata, in IconDirEntry entry)
        => metadata.GetIcoMetadata().FromIconDirEntry(entry);
}

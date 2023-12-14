// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Icon;

namespace SixLabors.ImageSharp.Formats.Ico;

internal sealed class IcoEncoderCore : IconEncoderCore
{
    protected override void GetHeader(in Image image)
    {
        this.FileHeader = new(IconFileType.ICO, (ushort)image.Frames.Count);
        this.Entries = image.Frames.Select(i =>
        {
            IcoFrameMetadata metadata = i.Metadata.GetIcoMetadata();
            return metadata.ToIconDirEntry();
        }).ToArray();
    }
}

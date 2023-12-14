// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Icon;

namespace SixLabors.ImageSharp.Formats.Cur;

internal sealed class CurEncoderCore : IconEncoderCore
{
    protected override void GetHeader(in Image image)
    {
        this.FileHeader = new(IconFileType.ICO, (ushort)image.Frames.Count);
        this.Entries = image.Frames.Select(i =>
        {
            CurFrameMetadata metadata = i.Metadata.GetCurMetadata();
            return metadata.ToIconDirEntry();
        }).ToArray();
    }
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Icon;

namespace SixLabors.ImageSharp.Formats.Ico;

internal sealed class IcoEncoderCore : IconEncoderCore
{
    public IcoEncoderCore()
        : base(IconFileType.ICO)
    {
    }
}

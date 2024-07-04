// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Symbol;

internal class Av1TransformInfo
{
    public Av1TransformInfo()
    {
    }

    public Av1TransformInfo(Av1TransformInfo originalInfo)
    {
        this.TransformSize = originalInfo.TransformSize;
        this.OffsetX = originalInfo.OffsetX;
        this.OffsetY = originalInfo.OffsetY;
    }

    public Av1TransformSize TransformSize { get; internal set; }

    public int OffsetX { get; internal set; }

    public int OffsetY { get; internal set; }

    public bool CodeBlockFlag { get; internal set; }

    public int TransformOffsetX { get; internal set; }

    public int TransformOffsetY { get; internal set; }
}

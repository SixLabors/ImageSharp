// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Memory.ImageTypes;

/// <summary>
/// Represents a single-plane, 2D raster image of type <see cref="sbyte"/>.
/// </summary>
internal sealed class JxlImageSB : JxlPlane<sbyte>
{
    public JxlImageSB()
    {
    }

    public JxlImageSB(int width, int height)
        : base(width, height)
    {
    }

    public JxlImageSB(Configuration configuration, int xSize, int ySize, int prePadding = 0)
        : base(xSize, ySize)
        => this.Allocate(configuration, prePadding);
}

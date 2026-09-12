// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Memory.ImageTypes;

/// <summary>
/// Represents a single-plane, 2D raster image of type <see cref="short"/>.
/// </summary>
internal sealed class JxlImageS : JxlPlane<short>
{
    public JxlImageS()
    {
    }

    public JxlImageS(int width, int height)
        : base(width, height)
    {
    }

    public JxlImageS(Configuration configuration, int xSize, int ySize, int prePadding = 0)
        : base(xSize, ySize)
        => this.Allocate(configuration, prePadding);
}

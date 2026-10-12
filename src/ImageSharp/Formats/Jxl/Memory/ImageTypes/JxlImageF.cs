// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Memory.ImageTypes;

/// <summary>
/// Represents a single-plane, 2D raster image of type <see cref="float"/>.
/// </summary>
internal sealed class JxlImageF : JxlPlane<float>
{
    public JxlImageF()
    {
    }

    public JxlImageF(int width, int height)
        : base(width, height)
    {
    }

    public JxlImageF(Configuration configuration, int xSize, int ySize, int prePadding = 0)
        : base(xSize, ySize)
        => this.Allocate(configuration, prePadding);
}

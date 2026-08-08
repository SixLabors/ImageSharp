// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Memory.ImageTypes;

/// <summary>
/// Represents a three-plane, 2D raster image of type <see cref="float"/>.
/// </summary>
internal sealed class JxlImage3F : JxlImage3<float>
{
    public JxlImage3F()
    {
    }

    public JxlImage3F(Configuration configuration, int xSize, int ySize)
        : base(configuration, xSize, ySize)
    {
    }
}

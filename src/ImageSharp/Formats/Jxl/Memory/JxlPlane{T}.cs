// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Processing;

namespace SixLabors.ImageSharp.Formats.Jxl.Memory;

// NOTE: Do not seal this class.
internal class JxlPlane<T> : JxlPlaneBase
    where T : unmanaged
{
    public JxlPlane()
    {
    }

    public unsafe JxlPlane(int width, int height)
        : base(width, height, sizeof(T))
    {
    }

    public unsafe int PixelsPerRow => this.BytesPerRow / sizeof(T);

    public static JxlPlane<T> Create(Configuration configuration, int xSize, int ySize, int prePadding = 0)
    {
        JxlPlane<T> plane = new(xSize, ySize);

        bool allocated = plane.Allocate(configuration, prePadding);

        if (!allocated)
        {
            throw new InvalidOperationException("Failed to allocate a JPEG XL plane");
        }

        return plane;
    }

    public Span<T> GetRow(int y) => this.GetRowBase<T>(y);

    public Span<T> GetRow(Rectangle rectangle, int y)
    {
        DebugGuard.MustBeGreaterThanOrEqualTo(y + rectangle.Top, 0, nameof(y));

        return this.GetRow(y + rectangle.Top)[rectangle.Left..];
    }

    public bool IsRectangleInside(Rectangle rectangle) => rectangle.Contains(this.GetRectangle());

    public Rectangle GetRectangle() => new(0, 0, this.XSize, this.YSize);

    /// <summary>
    /// Fills everything in this image with 0.
    /// </summary>
    public void Clear() => JxlImageOperations.ZeroFillImage(this);
}

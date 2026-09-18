// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Memory.ImageTypes;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Modular;

/// <summary>
/// A wrapper over <see cref="JxlImageI"/> for modular operations.
/// </summary>
internal sealed class JxlModularChannel : IDisposable
{
    public JxlModularChannel(Configuration configuration, int width, int height, int horizShift, int vertShift)
    {
        this.HorizontalShift = horizShift;
        this.VerticalShift = vertShift;
        this.Width = width;
        this.Height = height;
        this.Plane = new JxlImageI(configuration, width, height);
    }

    /// <summary>
    /// Gets or sets the image width.
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// Gets or sets the image height.
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// Gets or sets the image horizontal shift.
    /// </summary>
    /// <remarks>
    /// width ~= width &gt;&gt; HorizontalShift
    /// </remarks>
    public int HorizontalShift { get; set; }

    /// <summary>
    /// Gets or sets the image vertical shift.
    /// </summary>
    /// <remarks>
    /// height ~= height &gt;&gt; VerticalShift
    /// </remarks>
    public int VerticalShift { get; set; }

    /// <summary>
    /// Gets or sets the index of the component.
    /// </summary>
    public int Component { get; set; } = -1;

    /// <summary>
    /// Gets or sets the backing plane buffer.
    /// </summary>
    public JxlImageI Plane { get; set; }

    public void Shrink(Configuration configuration)
    {
        if (this.Plane.XSize == this.Width && this.Plane.YSize == this.Height)
        {
            return;
        }

        this.Plane.Dispose();
        this.Plane = new JxlImageI(configuration, this.Width, this.Height);
    }

    public void Shrink(Configuration configuration, int newWidth, int newHeight)
    {
        this.Width = newWidth;
        this.Height = newHeight;
        this.Shrink(configuration);
    }

    public Span<int> GetRow(int y) => this.Plane.GetRow(y);

    public Span<int> GetRowMinus(int y, int minus) => this.Plane.GetRowMinus(y, minus);

    public Span<int> GetRowPlus(int y, int plus) => this.Plane.GetRowPlus(y, plus);

    public void Dispose() => this.Plane.Dispose();
}

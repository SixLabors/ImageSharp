// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Modular;

/// <summary>
/// Owns one integer sample plane and its modular channel dimensions and shifts.
/// </summary>
internal sealed class JxlModularChannel : IDisposable
{
    /// <summary>
    /// Allocates a channel whose logical dimensions initially match its plane allocation.
    /// </summary>
    /// <param name="configuration">The configuration used for sample allocation.</param>
    /// <param name="width">The channel width in samples.</param>
    /// <param name="height">The channel height in rows.</param>
    /// <param name="horizShift">The horizontal subsampling shift, or -1 for a metachannel.</param>
    /// <param name="vertShift">The vertical subsampling shift, or -1 for a metachannel.</param>
    public JxlModularChannel(Configuration configuration, int width, int height, int horizShift, int vertShift)
    {
        this.HorizontalShift = horizShift;
        this.VerticalShift = vertShift;
        this.Width = width;
        this.Height = height;
        this.Plane = new JxlPlane<int>(configuration, width, height);
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
    /// Gets the owned plane. Row views and transform inputs borrow this storage.
    /// </summary>
    public JxlPlane<int> Plane { get; }

    /// <summary>
    /// Applies reduced channel dimensions before decoding without reallocating or changing the row stride.
    /// </summary>
    /// <remarks>The metadata transform must only reduce dimensions within this channel's existing plane capacity.</remarks>
    public void Shrink()
    {
        int previousWidth = this.Plane.XSize;
        _ = this.Plane.ShrinkTo(this.Width, this.Height);

        // The physical stride stays fixed. Columns removed from the logical width now form padding that SIMD tails can read.
        // Existing padding is already zero; clear only the newly exposed columns before decoding writes the remaining samples.
        if (this.Width != previousWidth)
        {
            Span<int> samples = this.Plane.GetRow(0);
            for (int y = 0; y < this.Height; y++)
            {
                samples.Slice((y * this.Plane.PixelsPerRow) + this.Width, previousWidth - this.Width).Clear();
            }
        }
    }

    /// <summary>
    /// Reduces the logical channel dimensions before decoding while retaining its sample allocation.
    /// </summary>
    /// <param name="newWidth">The reduced width within the current plane dimensions.</param>
    /// <param name="newHeight">The reduced height within the current plane dimensions.</param>
    public void Shrink(int newWidth, int newHeight)
    {
        this.Width = newWidth;
        this.Height = newHeight;
        this.Shrink();
    }

    /// <summary>
    /// Exchanges owned storage and channel metadata without copying samples.
    /// </summary>
    /// <param name="other">The other owner, which receives this channel's previous storage and metadata.</param>
    public void Swap(JxlModularChannel other)
    {
        this.Plane.Swap(other.Plane);
        (this.Width, other.Width) = (other.Width, this.Width);
        (this.Height, other.Height) = (other.Height, this.Height);
        (this.HorizontalShift, other.HorizontalShift) = (other.HorizontalShift, this.HorizontalShift);
        (this.VerticalShift, other.VerticalShift) = (other.VerticalShift, this.VerticalShift);
        (this.Component, other.Component) = (other.Component, this.Component);
    }

    /// <summary>
    /// Borrows storage from a row through the end of the plane allocation.
    /// </summary>
    /// <param name="y">The row index.</param>
    /// <returns>The row, its padding and following rows.</returns>
    public Span<int> GetRow(int y) => this.Plane.GetRow(y);

    /// <summary>
    /// Borrows row storage with a prefix before the row origin.
    /// </summary>
    /// <param name="y">The row index.</param>
    /// <param name="minus">The prefix length in samples.</param>
    /// <returns>The remaining allocated samples from the requested position.</returns>
    public Span<int> GetRowMinus(int y, int minus) => this.Plane.GetRowMinus(y, minus);

    /// <summary>
    /// Borrows row storage starting after the row origin.
    /// </summary>
    /// <param name="y">The row index.</param>
    /// <param name="plus">The offset in samples.</param>
    /// <returns>The remaining allocated samples from the requested position.</returns>
    public Span<int> GetRowPlus(int y, int plus) => this.Plane.GetRowPlus(y, plus);

    /// <inheritdoc />
    public void Dispose() => this.Plane.Dispose();
}

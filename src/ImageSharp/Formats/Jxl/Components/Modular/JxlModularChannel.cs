// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.Modular;

/// <summary>
/// Retains ready modular samples in an owned buffer or a borrowed region.
/// </summary>
internal struct JxlModularChannel : IDisposable
{
    private Buffer2D<int>? ownedBuffer;

    /// <summary>
    /// Initializes a new instance of the <see cref="JxlModularChannel"/> struct with its complete layout and sample region.
    /// </summary>
    /// <param name="layout">The validated logical layout.</param>
    /// <param name="region">The ready sample region, or an empty region only when the layout contains no samples.</param>
    /// <param name="ownedBuffer">The sample owner, or null for borrowed or empty channels.</param>
    private JxlModularChannel(JxlModularChannelLayout layout, Buffer2DRegion<int> region, Buffer2D<int>? ownedBuffer)
    {
        this.Layout = layout;
        this.Region = region;
        this.ownedBuffer = ownedBuffer;
    }

    /// <summary>
    /// Gets the logical dimensions and resolution of the ready samples.
    /// </summary>
    public JxlModularChannelLayout Layout { get; }

    /// <summary>
    /// Gets the logical dimensions.
    /// </summary>
    public Size Size => this.Layout.Size;

    /// <summary>
    /// Gets the logical width.
    /// </summary>
    public int Width => this.Layout.Width;

    /// <summary>
    /// Gets the logical height.
    /// </summary>
    public int Height => this.Layout.Height;

    /// <summary>
    /// Gets the horizontal base-two resolution shift, or minus one for metadata.
    /// </summary>
    public int HorizontalShift => this.Layout.HorizontalShift;

    /// <summary>
    /// Gets the vertical base-two resolution shift, or minus one for metadata.
    /// </summary>
    public int VerticalShift => this.Layout.VerticalShift;

    /// <summary>
    /// Gets a value indicating whether this channel contains palette metadata.
    /// </summary>
    public bool IsMetadata => this.Layout.IsMetadata;

    /// <summary>
    /// Gets a value indicating whether this channel contains no samples.
    /// </summary>
    public bool IsEmpty => this.Layout.IsEmpty;

    /// <summary>
    /// Gets the ready sample region. Empty channels have no backing samples; their logical shape remains in <see cref="Layout"/>.
    /// </summary>
    public Buffer2DRegion<int> Region { get; }

    /// <summary>
    /// Creates a ready channel that owns sample storage for its validated layout.
    /// </summary>
    /// <param name="allocator">The decoder's tracked sample allocator.</param>
    /// <param name="layout">The complete logical layout after transform parsing and validation.</param>
    /// <returns>A ready owned channel, or an empty channel that needs no sample allocation.</returns>
    public static JxlModularChannel Allocate(MemoryAllocator allocator, JxlModularChannelLayout layout)
    {
        // Empty palette and squeeze layouts are legal. Their ready sample set is empty, not an unallocated image.
        // Nonempty channels are constructed only after both their allocation and complete region exist.
        if (layout.IsEmpty)
        {
            return new JxlModularChannel(layout, default, null);
        }

        Buffer2D<int> buffer = allocator.Allocate2D<int>(layout.Size, AllocationOptions.Clean);
        return new JxlModularChannel(layout, buffer.GetRegion(), buffer);
    }

    /// <summary>
    /// Borrows an untransformed frame region without taking ownership of its underlying samples.
    /// </summary>
    /// <param name="bounds">The validated rectangle in channel coordinates.</param>
    /// <returns>A ready borrowed channel. The source retains ownership of the sample memory.</returns>
    public JxlModularChannel Borrow(Rectangle bounds)
        => this.Borrow(bounds, this.HorizontalShift, this.VerticalShift);

    /// <summary>
    /// Borrows ready sample storage with the logical resolution required by another modular substream.
    /// </summary>
    /// <param name="bounds">The validated rectangle in the backing channel.</param>
    /// <param name="horizontalShift">The borrowed channel's horizontal resolution shift.</param>
    /// <param name="verticalShift">The borrowed channel's vertical resolution shift.</param>
    /// <returns>A ready borrowed channel. The source retains its sample allocation.</returns>
    public JxlModularChannel Borrow(Rectangle bounds, int horizontalShift, int verticalShift)
    {
        // DC samples and AC correlation metadata can borrow the same scratch allocation with different encoded shifts.
        // The new layout changes only the logical view; it does not resize or take ownership of the backing samples.
        JxlModularChannelLayout layout = new(bounds.Size, horizontalShift, verticalShift);
        Buffer2DRegion<int> region = layout.IsEmpty ? default : this.Region.GetSubRegion(bounds);
        return new JxlModularChannel(layout, region, null);
    }

    /// <summary>
    /// Compares the dimensions and resolution used by transforms and cross-channel prediction.
    /// </summary>
    /// <param name="other">The ready channel to compare.</param>
    /// <returns>Whether both channels use the same logical sample layout.</returns>
    public bool HasSameShape(JxlModularChannel other) => this.Layout.HasSameShape(other.Layout);

    /// <inheritdoc/>
    public void Dispose()
    {
        // Borrowed and empty channels have no owner. Clear only this descriptor's ownership so failure cleanup
        // can revisit a consumed squeeze channel without returning its buffer twice.
        this.ownedBuffer?.Dispose();
        this.ownedBuffer = null;
    }
}

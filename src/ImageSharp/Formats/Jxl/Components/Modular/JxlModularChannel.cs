// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.Modular;

/// <summary>
/// Retains a modular channel's logical shape and either owns its sample buffer or borrows a frame region.
/// </summary>
internal struct JxlModularChannel : IDisposable
{
    private Buffer2D<int>? ownedBuffer;
    private Buffer2DRegion<int> region;

    /// <summary>
    /// Initializes a new instance of the <see cref="JxlModularChannel"/> struct without allocating samples.
    /// </summary>
    /// <param name="size">The logical dimensions. Either dimension can be zero after a transform.</param>
    /// <param name="horizontalShift">The horizontal resolution shift, or minus one for metadata.</param>
    /// <param name="verticalShift">The vertical resolution shift, or minus one for metadata.</param>
    public JxlModularChannel(Size size, int horizontalShift, int verticalShift)
    {
        this.Size = size;
        this.HorizontalShift = horizontalShift;
        this.VerticalShift = verticalShift;
        this.ownedBuffer = null;
        this.region = default;
    }

    /// <summary>
    /// Gets the logical dimensions independently of whether sample storage is empty or borrowed.
    /// </summary>
    public Size Size { get; }

    /// <summary>
    /// Gets the logical width.
    /// </summary>
    public int Width => this.Size.Width;

    /// <summary>
    /// Gets the logical height.
    /// </summary>
    public int Height => this.Size.Height;

    /// <summary>
    /// Gets the horizontal base-two resolution shift, or minus one for metadata.
    /// </summary>
    public int HorizontalShift { get; }

    /// <summary>
    /// Gets the vertical base-two resolution shift, or minus one for metadata.
    /// </summary>
    public int VerticalShift { get; }

    /// <summary>
    /// Gets a value indicating whether this is a palette metadata channel.
    /// </summary>
    public bool IsMetadata => this.HorizontalShift < 0;

    /// <summary>
    /// Gets a value indicating whether the logical shape contains no samples.
    /// </summary>
    public bool IsEmpty => this.Width == 0 || this.Height == 0;

    /// <summary>
    /// Gets the sample region, or a default region for an empty logical shape.
    /// </summary>
    public Buffer2DRegion<int> Region => this.region;

    /// <summary>
    /// Allocates the final encoded shape once its complete transform list has been applied.
    /// </summary>
    /// <param name="allocator">The decoder's tracked sample allocator.</param>
    public void Allocate(MemoryAllocator allocator)
    {
        // Empty palette and squeeze channels are legal. Preserve their logical shape without allocating a dummy sample.
        if (!this.IsEmpty)
        {
            this.ownedBuffer = allocator.Allocate2D<int>(this.Size, AllocationOptions.Clean);
            this.region = new Buffer2DRegion<int>(this.ownedBuffer);
        }
    }

    /// <summary>
    /// Borrows an untransformed frame region without taking ownership of its underlying samples.
    /// </summary>
    /// <param name="bounds">The validated rectangle in channel coordinates.</param>
    /// <returns>A descriptor disposed independently of the source channel.</returns>
    public JxlModularChannel Borrow(Rectangle bounds)
    {
        JxlModularChannel result = new(bounds.Size, this.HorizontalShift, this.VerticalShift);
        if (!result.IsEmpty)
        {
            result.region = this.region.GetSubRegion(bounds);
        }

        return result;
    }

    /// <summary>
    /// Checks the dimensions and resolution used to validate transforms and cross-channel references.
    /// </summary>
    /// <param name="other">The channel to compare.</param>
    /// <returns>Whether both channels have the same logical sample layout.</returns>
    public bool HasSameShape(JxlModularChannel other)
        => this.Size == other.Size && this.HorizontalShift == other.HorizontalShift && this.VerticalShift == other.VerticalShift;

    /// <inheritdoc/>
    public void Dispose()
    {
        // Borrowed group regions have no owner. Disposing their descriptors never returns the frame's sample memory.
        this.ownedBuffer?.Dispose();
        this.ownedBuffer = null;
        this.region = default;
    }
}

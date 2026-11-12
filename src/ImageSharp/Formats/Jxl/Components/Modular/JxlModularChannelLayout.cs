// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Components.Modular;

/// <summary>
/// Describes modular channel dimensions and resolution independently of sample storage.
/// </summary>
internal readonly struct JxlModularChannelLayout
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JxlModularChannelLayout"/> struct.
    /// </summary>
    /// <param name="size">The logical dimensions. Either dimension can be zero after a transform.</param>
    /// <param name="horizontalShift">The horizontal resolution shift, or minus one for metadata.</param>
    /// <param name="verticalShift">The vertical resolution shift, or minus one for metadata.</param>
    public JxlModularChannelLayout(Size size, int horizontalShift, int verticalShift)
    {
        this.Size = size;
        this.HorizontalShift = horizontalShift;
        this.VerticalShift = verticalShift;
    }

    /// <summary>
    /// Gets the logical dimensions, including empty palette or squeeze layouts.
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
    /// Gets a value indicating whether this is a palette metadata layout.
    /// </summary>
    public bool IsMetadata => this.HorizontalShift < 0;

    /// <summary>
    /// Gets a value indicating whether the logical shape contains no samples.
    /// </summary>
    public bool IsEmpty => this.Width == 0 || this.Height == 0;

    /// <summary>
    /// Compares the dimensions and resolution required by transforms and cross-channel prediction.
    /// </summary>
    /// <param name="other">The layout to compare.</param>
    /// <returns>Whether both channels use the same logical sample layout.</returns>
    public bool HasSameShape(JxlModularChannelLayout other)
        => this.Size == other.Size && this.HorizontalShift == other.HorizontalShift && this.VerticalShift == other.VerticalShift;
}

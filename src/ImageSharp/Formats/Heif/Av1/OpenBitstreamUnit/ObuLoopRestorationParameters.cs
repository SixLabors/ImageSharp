// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

/// <summary>
/// Contains the AV1 loop-restoration configuration for a frame.
/// </summary>
internal sealed class ObuLoopRestorationParameters
{
    /// <summary>
    /// Stores the fixed three plane configurations without an outer array or per-plane object allocation.
    /// </summary>
    private InlineArray4<ObuLoopRestorationItem> items;

    /// <summary>
    /// Gets or sets a value indicating whether any plane uses loop restoration.
    /// </summary>
    public bool UsesLoopRestoration { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether either chroma plane uses loop restoration.
    /// </summary>
    public bool UsesChromaLoopRestoration { get; set; }

    /// <summary>
    /// Gets the loop-restoration configuration for each plane.
    /// </summary>
    public Span<ObuLoopRestorationItem> Items => this.items[..3];

    /// <summary>
    /// Gets or sets the luma restoration-unit size shift.
    /// </summary>
    public int UnitShift { get; set; }

    /// <summary>
    /// Gets or sets the chroma restoration-unit size shift relative to luma.
    /// </summary>
    public int UVShift { get; set; }
}

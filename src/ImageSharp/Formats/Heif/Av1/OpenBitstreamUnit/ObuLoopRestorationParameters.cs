// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

/// <summary>
/// Contains the AV1 loop-restoration configuration for a frame.
/// </summary>
internal class ObuLoopRestorationParameters
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ObuLoopRestorationParameters"/> class.
    /// </summary>
    public ObuLoopRestorationParameters()
    {
        // AV1 addresses restoration state by plane, so all three plane entries must exist even
        // when the active color configuration uses fewer planes.
        this.Items = new ObuLoopRestorationItem[3];
        this.Items[0] = new();
        this.Items[1] = new();
        this.Items[2] = new();
    }

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
    public ObuLoopRestorationItem[] Items { get; }

    /// <summary>
    /// Gets or sets the luma restoration-unit size shift.
    /// </summary>
    public int UnitShift { get; set; }

    /// <summary>
    /// Gets or sets the chroma restoration-unit size shift relative to luma.
    /// </summary>
    public int UVShift { get; set; }
}

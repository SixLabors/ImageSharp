// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

internal class ObuLoopRestorationParameters
{
    internal ObuLoopRestorationParameters()
    {
        this.Items = new ObuLoopRestorationItem[3];
        this.Items[0] = new();
        this.Items[1] = new();
        this.Items[2] = new();
    }

    internal bool UsesLoopRestoration { get; set; }

    internal bool UsesChromaLoopRestoration { get; set; }

    internal ObuLoopRestorationItem[] Items { get; }

    internal int UnitShift { get; set; }

    internal int UVShift { get; set; }
}

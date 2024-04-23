// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

internal class ObuFrameSize
{
    internal int FrameWidth { get; set; }

    internal int FrameHeight { get; set; }

    internal int SuperResolutionDenominator { get; set; }

    internal int SuperResolutionUpscaledWidth { get; set; }

    internal int RenderWidth { get; set; }

    internal int RenderHeight { get; set; }
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

internal class ObuOrderHintInfo
{
    public bool EnableOrderHint { get; internal set; }

    internal bool EnableJointCompound { get; set; }

    internal bool EnableReferenceFrameMotionVectors { get; set; }

    internal int OrderHintBits { get; set; }
}

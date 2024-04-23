// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

internal class ObuOperatingPoint
{
    internal int OperatorIndex { get; set; }

    internal int SequenceLevelIndex { get; set; }

    internal int SequenceTier { get; set; }

    internal bool IsDecoderModelPresent { get; set; }

    internal bool IsInitialDisplayDelayPresent { get; set; }
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Quantization;

internal class Av1InverseQuantizer
{

    public int InverseQuantize(ObuSequenceHeader sequenceHeader, ObuFrameHeader frameHeader, ObuPartitionInfo part, Av1BlockModeInfo mode, int[] qCoefficients, Av1TransformMode txType, Av1TransformSize txSize, Av1Plane plane)
    {
        return 0;
    }
}

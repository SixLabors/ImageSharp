// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

internal class Av1EncoderPredictionUnit
{
    public required byte[] AngleDelta { get; set; }

    public int ChromaFromLumaIndex { get; internal set; }

    public int ChromaFromLumaSigns { get; internal set; }
}

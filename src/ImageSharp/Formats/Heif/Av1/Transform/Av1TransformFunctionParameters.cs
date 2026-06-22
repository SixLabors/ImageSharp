// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

internal class Av1TransformFunctionParameters
{
    public Av1TransformType TransformType { get; internal set; }

    public Av1TransformSize TransformSize { get; internal set; }

    public int EndOfBuffer { get; internal set; }

    public bool IsLossless { get; internal set; }

    public int BitDepth { get; internal set; }

    public bool Is16BitPipeline { get; internal set; }
}
